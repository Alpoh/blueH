using System.Globalization;
using System.Runtime.InteropServices;
using Windows.Devices.Enumeration;
using Windows.Media.Devices;
using BlueH.Diagnostics;
using BlueH.Models;
using BlueH.Services.Interop;

namespace BlueH.Services;

public sealed class CoreAudioEndpointService : IAudioEndpointService
{
    private const string ContainerIdProperty = "System.Devices.ContainerId";
    private const string FindEndpointsOperation = "Enumerate audio render endpoints";
    private const string SetVolumeOperation = "Set endpoint volume";
    private const string SetMuteOperation = "Set endpoint mute";
    private const string SetDefaultOperation = "Set default endpoint";
    private const string UnknownEndpointDetail = "no active render endpoint has that id";
    private const string RejectedVolumeDetail = "requested volume is out of range";

    private static readonly string[] RequestedProperties = [ContainerIdProperty];

    private readonly IDiagnosticsLog _diagnosticsLog;

    public CoreAudioEndpointService(IDiagnosticsLog diagnosticsLog)
    {
        _diagnosticsLog = diagnosticsLog;
    }

    public async Task<IReadOnlyList<AudioEndpoint>> FindRenderEndpointsAsync(CancellationToken cancellationToken)
    {
        Dictionary<string, (string Name, Guid ContainerId)> described = await DescribeEndpointsAsync(cancellationToken);

        IMMDeviceEnumerator enumerator = CreateEnumerator();

        try
        {
            string? defaultForPlayback = DefaultEndpointId(enumerator, CoreAudioRole.Console);
            string? defaultForCommunications = DefaultEndpointId(enumerator, CoreAudioRole.Communications);

            List<AudioEndpoint> endpoints = [];

            foreach (string mmDeviceId in ActiveRenderEndpointIds(enumerator))
            {
                (int? volumePercent, bool? isMuted) = ReadLevels(enumerator, mmDeviceId);

                described.TryGetValue(mmDeviceId, out (string Name, Guid ContainerId) description);

                endpoints.Add(new AudioEndpoint(
                    mmDeviceId,
                    description.ContainerId,
                    description.Name ?? string.Empty,
                    volumePercent,
                    isMuted,
                    mmDeviceId == defaultForPlayback,
                    mmDeviceId == defaultForCommunications));
            }

            _diagnosticsLog.Record(
                DiagnosticOutcome.Success,
                FindEndpointsOperation,
                endpoints.Count.ToString(CultureInfo.InvariantCulture));

            return endpoints;
        }
        finally
        {
            Marshal.ReleaseComObject(enumerator);
        }
    }

    public Task SetVolumePercentAsync(string mmDeviceId, int volumePercent, CancellationToken cancellationToken)
    {
        if (VolumeScale.ToScalar(volumePercent) is not float scalar)
        {
            _diagnosticsLog.Record(DiagnosticOutcome.Failure, SetVolumeOperation, RejectedVolumeDetail);

            return Task.CompletedTask;
        }

        WithVolume(mmDeviceId, SetVolumeOperation, volume =>
            volume.SetMasterVolumeLevelScalar(scalar, Guid.Empty));

        return Task.CompletedTask;
    }

    public Task SetMutedAsync(string mmDeviceId, bool isMuted, CancellationToken cancellationToken)
    {
        WithVolume(mmDeviceId, SetMuteOperation, volume => volume.SetMute(isMuted, Guid.Empty));

        return Task.CompletedTask;
    }

    public Task SetDefaultAsync(string mmDeviceId, AudioEndpointRole role, CancellationToken cancellationToken)
    {
        IMMDeviceEnumerator enumerator = CreateEnumerator();

        try
        {
            if (!ActiveRenderEndpointIds(enumerator).Contains(mmDeviceId))
            {
                _diagnosticsLog.Record(DiagnosticOutcome.Failure, SetDefaultOperation, UnknownEndpointDetail);

                return Task.CompletedTask;
            }
        }
        finally
        {
            Marshal.ReleaseComObject(enumerator);
        }

        object instance = Activator.CreateInstance(
            Type.GetTypeFromCLSID(CoreAudioConstants.PolicyConfigClassId)!)!;

        try
        {
            IPolicyConfig policyConfig = (IPolicyConfig)instance;
            int result = policyConfig.SetDefaultEndpoint(mmDeviceId, ToCoreAudioRole(role));

            _diagnosticsLog.Record(
                result == 0 ? DiagnosticOutcome.Success : DiagnosticOutcome.Failure,
                SetDefaultOperation,
                FormatResult(role, result));
        }
        finally
        {
            Marshal.ReleaseComObject(instance);
        }

        return Task.CompletedTask;
    }

    private static CoreAudioRole ToCoreAudioRole(AudioEndpointRole role)
    {
        return role == AudioEndpointRole.Communications ? CoreAudioRole.Communications : CoreAudioRole.Console;
    }

    private static string FormatResult(AudioEndpointRole role, int result)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{role} hr=0x{result:X8}");
    }

    private static IMMDeviceEnumerator CreateEnumerator()
    {
        return (IMMDeviceEnumerator)Activator.CreateInstance(
            Type.GetTypeFromCLSID(CoreAudioConstants.DeviceEnumeratorClassId)!)!;
    }

    private static string? DefaultEndpointId(IMMDeviceEnumerator enumerator, CoreAudioRole role)
    {
        if (enumerator.GetDefaultAudioEndpoint(AudioDataFlow.Render, role, out IMMDevice device) != 0)
        {
            return null;
        }

        try
        {
            return device.GetId(out string id) == 0 ? id : null;
        }
        finally
        {
            Marshal.ReleaseComObject(device);
        }
    }

    private static List<string> ActiveRenderEndpointIds(IMMDeviceEnumerator enumerator)
    {
        List<string> ids = [];

        if (enumerator.EnumAudioEndpoints(
                AudioDataFlow.Render,
                CoreAudioConstants.DeviceStateActive,
                out IMMDeviceCollection collection) != 0)
        {
            return ids;
        }

        try
        {
            if (collection.GetCount(out int count) != 0)
            {
                return ids;
            }

            for (int index = 0; index < count; index++)
            {
                if (collection.Item(index, out IMMDevice device) != 0)
                {
                    continue;
                }

                try
                {
                    if (device.GetId(out string id) == 0)
                    {
                        ids.Add(id);
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(device);
                }
            }
        }
        finally
        {
            Marshal.ReleaseComObject(collection);
        }

        return ids;
    }

    private static (int? VolumePercent, bool? IsMuted) ReadLevels(IMMDeviceEnumerator enumerator, string mmDeviceId)
    {
        if (enumerator.GetDevice(mmDeviceId, out IMMDevice device) != 0)
        {
            return (null, null);
        }

        try
        {
            Guid volumeInterfaceId = typeof(IAudioEndpointVolume).GUID;

            if (device.Activate(
                    ref volumeInterfaceId,
                    CoreAudioConstants.ClassContextAll,
                    IntPtr.Zero,
                    out object instance) != 0)
            {
                return (null, null);
            }

            IAudioEndpointVolume volume = (IAudioEndpointVolume)instance;

            try
            {
                int? volumePercent = volume.GetMasterVolumeLevelScalar(out float scalar) == 0
                    ? VolumeScale.ToPercent(scalar)
                    : null;

                bool? isMuted = volume.GetMute(out bool muted) == 0 ? muted : null;

                return (volumePercent, isMuted);
            }
            finally
            {
                Marshal.ReleaseComObject(volume);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(device);
        }
    }

    private async Task<Dictionary<string, (string Name, Guid ContainerId)>> DescribeEndpointsAsync(
        CancellationToken cancellationToken)
    {
        DeviceInformationCollection endpoints = await DeviceInformation
            .FindAllAsync(MediaDevice.GetAudioRenderSelector(), RequestedProperties)
            .AsTask(cancellationToken);

        Dictionary<string, (string Name, Guid ContainerId)> described = [];

        foreach (DeviceInformation endpoint in endpoints)
        {
            Guid containerId =
                endpoint.Properties.TryGetValue(ContainerIdProperty, out object? value) && value is Guid parsed
                    ? parsed
                    : Guid.Empty;

            described[AudioEndpointIdReader.ExtractMmDeviceId(endpoint.Id)] = (endpoint.Name, containerId);
        }

        return described;
    }

    private void WithVolume(string mmDeviceId, string operation, Func<IAudioEndpointVolume, int> action)
    {
        IMMDeviceEnumerator enumerator = CreateEnumerator();

        try
        {
            if (enumerator.GetDevice(mmDeviceId, out IMMDevice device) != 0)
            {
                _diagnosticsLog.Record(DiagnosticOutcome.Failure, operation, UnknownEndpointDetail);

                return;
            }

            try
            {
                Guid volumeInterfaceId = typeof(IAudioEndpointVolume).GUID;

                if (device.Activate(
                        ref volumeInterfaceId,
                        CoreAudioConstants.ClassContextAll,
                        IntPtr.Zero,
                        out object instance) != 0)
                {
                    _diagnosticsLog.Record(DiagnosticOutcome.Failure, operation, mmDeviceId);

                    return;
                }

                IAudioEndpointVolume volume = (IAudioEndpointVolume)instance;

                try
                {
                    int result = action(volume);

                    _diagnosticsLog.Record(
                        result == 0 ? DiagnosticOutcome.Success : DiagnosticOutcome.Failure,
                        operation,
                        string.Create(CultureInfo.InvariantCulture, $"hr=0x{result:X8}"));
                }
                finally
                {
                    Marshal.ReleaseComObject(volume);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(device);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(enumerator);
        }
    }
}
