using System.Runtime.InteropServices;

namespace BlueH.Services.Interop;

[ComImport]
[Guid("28F54685-06FD-11D2-B27A-00A0C9223196")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IKsControl
{
    [PreserveSig]
    int KsProperty(
        ref KsPropertyRequest property,
        uint propertyLength,
        ref uint propertyData,
        uint dataLength,
        out uint bytesReturned);

    [PreserveSig]
    int KsMethod(IntPtr method, uint methodLength, IntPtr methodData, uint dataLength, out uint bytesReturned);

    [PreserveSig]
    int KsEvent(IntPtr ksEvent, uint eventLength, IntPtr eventData, uint dataLength, out uint bytesReturned);
}
