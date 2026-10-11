using System.Runtime.InteropServices;

namespace BlueH.Services.Interop;

[ComImport]
[Guid("2A07407E-6497-4A18-9787-32F79BD0D98F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDeviceTopology
{
    [PreserveSig]
    int GetConnectorCount(out uint count);

    [PreserveSig]
    int GetConnector(uint index, out IConnector connector);

    [PreserveSig]
    int GetSubunitCount(out uint count);

    [PreserveSig]
    int GetSubunit(uint index, out IntPtr subunit);

    [PreserveSig]
    int GetPartById(uint partId, out IntPtr part);

    [PreserveSig]
    int GetDeviceId([MarshalAs(UnmanagedType.LPWStr)] out string deviceId);

    [PreserveSig]
    int GetSignalPath(IntPtr from, IntPtr to, int rejectMixedPaths, out IntPtr parts);
}
