using System.Runtime.InteropServices;

namespace BlueH.Services.Interop;

[StructLayout(LayoutKind.Sequential)]
internal struct KsPropertyRequest
{
    public Guid Set;
    public uint Id;
    public uint Flags;
}
