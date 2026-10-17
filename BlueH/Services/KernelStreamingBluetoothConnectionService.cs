using System.Globalization;
using System.Runtime.InteropServices;
using BlueH.Diagnostics;
using BlueH.Services.Interop;

namespace BlueH.Services;

public sealed class KernelStreamingBluetoothConnectionService : IBluetoothConnectionService
{
    private const string ConnectOperation = "Request Bluetooth audio reconnect";
    private const string DisconnectOperation = "Request Bluetooth audio disconnect";
    private const string NoBluetoothFilterDetail = "no Bluetooth audio filter found for the device";
    private const string EnumerationFailedFormat = "audio endpoint enumeration failed hr=0x{0:X8}";
    private const string RequestFailedFormat = "every Bluetooth audio filter refused the request, last hr=0x{0:X8}";

    private readonly IDiagnosticsLog _diagnosticsLog;

    public KernelStreamingBluetoothConnectionService(IDiagnosticsLog diagnosticsLog)
    {
        _diagnosticsLog = diagnosticsLog;
    }

    public Task ConnectAsync(Guid containerId, CancellationToken cancellationToken)
    {
        return Task.Run(
            () => RequestOneShot(containerId, KernelStreamingConstants.OneShotReconnectProperty, ConnectOperation),
            cancellationToken);
    }

    public Task DisconnectAsync(Guid containerId, CancellationToken cancellationToken)
    {
        return Task.Run(
            () => RequestOneShot(containerId, KernelStreamingConstants.OneShotDisconnectProperty, DisconnectOperation),
            cancellationToken);
    }

    private static IMMDeviceEnumerator CreateEnumerator()
    {
        Type enumeratorType = Type.GetTypeFromCLSID(CoreAudioConstants.DeviceEnumeratorClassId, throwOnError: true)!;

        return (IMMDeviceEnumerator)Activator.CreateInstance(enumeratorType)!;
    }

    private static Guid? ReadContainerId(IMMDevice device)
    {
        if (device.OpenPropertyStore(CoreAudioConstants.StorageAccessRead, out IntPtr storePointer) != 0)
        {
            return null;
        }

        IPropertyStore store = (IPropertyStore)Marshal.GetObjectForIUnknown(storePointer);
        Marshal.Release(storePointer);

        try
        {
            PropertyKey key = new()
            {
                FormatId = KernelStreamingConstants.DeviceContainerIdFormat,
                PropertyId = KernelStreamingConstants.ContainerIdPropertyId
            };

            if (store.GetValue(ref key, out PropVariant value) != 0)
            {
                return null;
            }

            try
            {
                return value.VariantType == KernelStreamingConstants.VariantTypeClassId
                    ? Marshal.PtrToStructure<Guid>(value.Value)
                    : null;
            }
            finally
            {
                PropVariantNativeMethods.PropVariantClear(ref value);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(store);
        }
    }

    private static string? ReadConnectedFilterId(IMMDevice device)
    {
        Guid topologyInterfaceId = typeof(IDeviceTopology).GUID;

        if (device.Activate(
                ref topologyInterfaceId,
                CoreAudioConstants.ClassContextAll,
                IntPtr.Zero,
                out object instance) != 0)
        {
            return null;
        }

        IDeviceTopology topology = (IDeviceTopology)instance;

        try
        {
            if (topology.GetConnector(0, out IConnector connector) != 0)
            {
                return null;
            }

            try
            {
                return connector.GetDeviceIdConnectedTo(out string filterId) == 0 ? filterId : null;
            }
            finally
            {
                Marshal.ReleaseComObject(connector);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(topology);
        }
    }

    private static int? SendToFilter(IMMDeviceEnumerator enumerator, string filterId, uint propertyId)
    {
        if (enumerator.GetDevice(filterId, out IMMDevice filter) != 0)
        {
            return null;
        }

        try
        {
            Guid controlInterfaceId = typeof(IKsControl).GUID;

            if (filter.Activate(
                    ref controlInterfaceId,
                    CoreAudioConstants.ClassContextAll,
                    IntPtr.Zero,
                    out object instance) != 0)
            {
                return null;
            }

            IKsControl control = (IKsControl)instance;

            try
            {
                if (Query(control, propertyId, KernelStreamingConstants.PropertyTypeBasicSupport) != 0)
                {
                    return null;
                }

                return Query(control, propertyId, KernelStreamingConstants.PropertyTypeGet);
            }
            finally
            {
                Marshal.ReleaseComObject(control);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(filter);
        }
    }

    private static int Query(IKsControl control, uint propertyId, uint flags)
    {
        KsPropertyRequest request = new()
        {
            Set = KernelStreamingConstants.BluetoothAudioPropertySet,
            Id = propertyId,
            Flags = flags
        };
        uint access = 0;
        uint dataLength = flags == KernelStreamingConstants.PropertyTypeBasicSupport
            ? KernelStreamingConstants.BasicSupportDataLength
            : KernelStreamingConstants.NoDataLength;

        return control.KsProperty(
            ref request,
            (uint)Marshal.SizeOf<KsPropertyRequest>(),
            ref access,
            dataLength,
            out _);
    }

    private void RequestOneShot(Guid containerId, uint propertyId, string operation)
    {
        IMMDeviceEnumerator enumerator = CreateEnumerator();

        try
        {
            HashSet<string> filterIds = CollectBluetoothFilterIds(enumerator, containerId);
            int? lastFailure = null;
            bool anyAccepted = false;

            foreach (string filterId in filterIds)
            {
                int? result = SendToFilter(enumerator, filterId, propertyId);

                if (result is not int hresult)
                {
                    continue;
                }

                _diagnosticsLog.Record(
                    hresult == 0 ? DiagnosticOutcome.Success : DiagnosticOutcome.Failure,
                    operation,
                    string.Create(CultureInfo.InvariantCulture, $"{filterId} hr=0x{hresult:X8}"));

                anyAccepted |= hresult == 0;
                lastFailure = hresult == 0 ? lastFailure : hresult;
            }

            if (anyAccepted)
            {
                return;
            }

            string detail = lastFailure is int failure
                ? string.Format(CultureInfo.InvariantCulture, RequestFailedFormat, failure)
                : NoBluetoothFilterDetail;

            _diagnosticsLog.Record(DiagnosticOutcome.Failure, operation, detail);

            throw new InvalidOperationException(detail);
        }
        finally
        {
            Marshal.ReleaseComObject(enumerator);
        }
    }

    private static HashSet<string> CollectBluetoothFilterIds(IMMDeviceEnumerator enumerator, Guid containerId)
    {
        int enumerationResult = enumerator.EnumAudioEndpoints(
            AudioDataFlow.All,
            CoreAudioConstants.DeviceStateMaskAll,
            out IMMDeviceCollection collection);

        if (enumerationResult != 0)
        {
            throw new InvalidOperationException(
                string.Format(CultureInfo.InvariantCulture, EnumerationFailedFormat, enumerationResult));
        }

        HashSet<string> filterIds = new(StringComparer.Ordinal);

        try
        {
            if (collection.GetCount(out int count) != 0)
            {
                return filterIds;
            }

            for (int index = 0; index < count; index++)
            {
                if (collection.Item(index, out IMMDevice device) != 0)
                {
                    continue;
                }

                try
                {
                    if (ReadContainerId(device) == containerId && ReadConnectedFilterId(device) is { } filterId)
                    {
                        filterIds.Add(filterId);
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

        return filterIds;
    }
}
