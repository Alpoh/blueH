using System.Diagnostics;

namespace BlueH.Services;

public sealed class ShellExternalLinkOpener : IExternalLinkOpener
{
    public void Open(Uri address)
    {
        using Process? process = Process.Start(new ProcessStartInfo(address.AbsoluteUri) { UseShellExecute = true });
    }
}
