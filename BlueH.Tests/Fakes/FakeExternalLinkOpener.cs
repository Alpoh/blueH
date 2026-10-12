using BlueH.Services;

namespace BlueH.Tests.Fakes;

public sealed class FakeExternalLinkOpener : IExternalLinkOpener
{
    public List<Uri> Opened { get; } = [];

    public void Open(Uri address)
    {
        Opened.Add(address);
    }
}
