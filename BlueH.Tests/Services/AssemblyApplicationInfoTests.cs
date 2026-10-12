using BlueH.Services;

namespace BlueH.Tests.Services;

[TestFixture]
public class AssemblyApplicationInfoTests
{
    [TestCase("0.1.0+2c4f7e1a9b", "0.1.0")]
    [TestCase("0.1.0", "0.1.0")]
    [TestCase("1.0.0-beta.2+abc", "1.0.0-beta.2")]
    public void BuildMetadataIsDropped(string informational, string expected)
    {
        Assert.That(AssemblyApplicationInfo.WithoutBuildMetadata(informational), Is.EqualTo(expected));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ")]
    public void NoInformationalVersionGivesNothing(string? informational)
    {
        Assert.That(AssemblyApplicationInfo.WithoutBuildMetadata(informational), Is.Null);
    }

    [Test]
    public void TheApplicationsOwnVersionIsRead()
    {
        AssemblyApplicationInfo info = new(typeof(AssemblyApplicationInfo).Assembly);

        Assert.That(info.Version, Does.StartWith("0.1.0").And.Not.Contain("+"));
    }
}
