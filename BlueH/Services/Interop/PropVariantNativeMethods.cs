using System.Runtime.InteropServices;

namespace BlueH.Services.Interop;

internal static class PropVariantNativeMethods
{
    [DllImport("ole32.dll")]
    internal static extern int PropVariantClear(ref PropVariant value);
}
