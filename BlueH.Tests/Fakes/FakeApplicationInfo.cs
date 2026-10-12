using BlueH.Services;

namespace BlueH.Tests.Fakes;

public sealed class FakeApplicationInfo : IApplicationInfo
{
    public string Version { get; set; } = "0.1.0";
}
