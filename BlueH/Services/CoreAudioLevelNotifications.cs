using System.Globalization;
using System.Runtime.InteropServices;
using BlueH.Diagnostics;
using BlueH.Services.Interop;

namespace BlueH.Services;

public sealed class CoreAudioLevelNotifications : IDisposable
{
    private const string RegisterOperation = "Register endpoint level notifications";
    private const string UnregisterOperation = "Unregister endpoint level notifications";

    private readonly IDiagnosticsLog _diagnosticsLog;
    private readonly Dictionary<string, IAudioEndpointVolume> _registered = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    private IntPtr _callbackPointer;
    private bool _isDisposed;

    public CoreAudioLevelNotifications(IDiagnosticsLog diagnosticsLog)
    {
        _diagnosticsLog = diagnosticsLog;
    }

    public event EventHandler? LevelsChanged;

    public void FollowActiveEndpoints()
    {
        lock (_gate)
        {
            if (_isDisposed)
            {
                return;
            }

            if (_callbackPointer == IntPtr.Zero)
            {
                _callbackPointer = Marshal.GetComInterfaceForObject<AudioEndpointVolumeCallback, IAudioEndpointVolumeCallback>(
                    new AudioEndpointVolumeCallback(OnLevelsChanged));
            }

            IMMDeviceEnumerator enumerator = CoreAudioEndpointService.CreateEnumerator();

            try
            {
                List<string> activeIds = CoreAudioEndpointService.ActiveRenderEndpointIds(enumerator);

                foreach (string goneId in _registered.Keys.Except(activeIds, StringComparer.Ordinal).ToArray())
                {
                    Unregister(goneId);
                }

                foreach (string newId in activeIds.Except(_registered.Keys, StringComparer.Ordinal).ToArray())
                {
                    Register(enumerator, newId);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(enumerator);
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _isDisposed = true;

            foreach (string mmDeviceId in _registered.Keys.ToArray())
            {
                Unregister(mmDeviceId);
            }

            if (_callbackPointer != IntPtr.Zero)
            {
                Marshal.Release(_callbackPointer);
                _callbackPointer = IntPtr.Zero;
            }
        }
    }

    private void Register(IMMDeviceEnumerator enumerator, string mmDeviceId)
    {
        if (enumerator.GetDevice(mmDeviceId, out IMMDevice device) != 0)
        {
            _diagnosticsLog.Record(DiagnosticOutcome.Failure, RegisterOperation, mmDeviceId);

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
                _diagnosticsLog.Record(DiagnosticOutcome.Failure, RegisterOperation, mmDeviceId);

                return;
            }

            IAudioEndpointVolume volume = (IAudioEndpointVolume)instance;
            int result = volume.RegisterControlChangeNotify(_callbackPointer);

            if (result != 0)
            {
                Marshal.ReleaseComObject(volume);
                _diagnosticsLog.Record(DiagnosticOutcome.Failure, RegisterOperation, FormatResult(result));

                return;
            }

            _registered[mmDeviceId] = volume;
        }
        finally
        {
            Marshal.ReleaseComObject(device);
        }
    }

    private void Unregister(string mmDeviceId)
    {
        IAudioEndpointVolume volume = _registered[mmDeviceId];
        _registered.Remove(mmDeviceId);

        try
        {
            int result = volume.UnregisterControlChangeNotify(_callbackPointer);

            if (result != 0)
            {
                _diagnosticsLog.Record(DiagnosticOutcome.Failure, UnregisterOperation, FormatResult(result));
            }
        }
        finally
        {
            Marshal.ReleaseComObject(volume);
        }
    }

    private void OnLevelsChanged()
    {
        LevelsChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string FormatResult(int result)
    {
        return string.Create(CultureInfo.InvariantCulture, $"hr=0x{result:X8}");
    }
}
