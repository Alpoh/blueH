using System.Runtime.InteropServices;

namespace BlueH.Services.Interop;

[StructLayout(LayoutKind.Sequential)]
internal struct PropVariant
{
    public ushort VariantType;
    public ushort Reserved1;
    public ushort Reserved2;
    public ushort Reserved3;
    public IntPtr Value;
    public IntPtr Extra;
}
