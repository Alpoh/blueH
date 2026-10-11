using System.Runtime.InteropServices;

namespace BlueH.Services.Interop;

[ComImport]
[Guid("9C2C4058-23F5-41DE-877A-DF3AF236A09E")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IConnector
{
    [PreserveSig]
    int GetType(out int connectorType);

    [PreserveSig]
    int GetDataFlow(out int dataFlow);

    [PreserveSig]
    int ConnectTo(IntPtr connectTo);

    [PreserveSig]
    int Disconnect();

    [PreserveSig]
    int IsConnected(out int isConnected);

    [PreserveSig]
    int GetConnectedTo(out IntPtr connectedTo);

    [PreserveSig]
    int GetConnectorIdConnectedTo([MarshalAs(UnmanagedType.LPWStr)] out string connectorId);

    [PreserveSig]
    int GetDeviceIdConnectedTo([MarshalAs(UnmanagedType.LPWStr)] out string deviceId);
}
