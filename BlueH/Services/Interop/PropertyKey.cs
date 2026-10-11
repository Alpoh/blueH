using System.Runtime.InteropServices;

namespace BlueH.Services.Interop;

[StructLayout(LayoutKind.Sequential)]
internal struct PropertyKey
{
    public Guid FormatId;
    public uint PropertyId;
}
