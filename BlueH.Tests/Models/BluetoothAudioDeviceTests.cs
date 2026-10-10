using BlueH.Models;

namespace BlueH.Tests.Models;

[TestFixture]
public class BluetoothAudioDeviceTests
{
    private static readonly Guid EarbudsContainer = Guid.Parse("0ac66fc7-8a44-50f8-ae2b-25ffdd18b301");
    private static readonly Guid SpeakerContainer = Guid.Parse("62137bd6-c515-5357-8cb8-fdfef0bf1ecc");

    private const string EarbudsName = "OPPO Enco Air5";
    private const string SpeakerName = "SRS-XE300";
    private const uint WearableHeadsetMinorClass = 1;
    private const uint LoudspeakerMinorClass = 5;

    [Test]
    public void OneEndpointBecomesOneDevice()
    {
        IReadOnlyList<BluetoothAudioDevice> devices = BluetoothAudioDevice.FromEndpoints(
            [Endpoint("classic", EarbudsContainer, EarbudsName, isConnected: true, WearableHeadsetMinorClass)]);

        Assert.That(devices.Select(device => device.Name), Is.EqualTo(new[] { EarbudsName }));
    }

    [Test]
    public void TwoEndpointsSharingAContainerCollapseIntoOneDevice()
    {
        IReadOnlyList<BluetoothAudioDevice> devices = BluetoothAudioDevice.FromEndpoints(
        [
            Endpoint("classic", EarbudsContainer, EarbudsName, isConnected: true, WearableHeadsetMinorClass),
            Endpoint("lowEnergy", EarbudsContainer, EarbudsName, isConnected: false, minorClass: null)
        ]);

        Assert.That(devices, Has.Count.EqualTo(1));
    }

    [Test]
    public void EndpointsWithDifferentContainersStaySeparateDevices()
    {
        IReadOnlyList<BluetoothAudioDevice> devices = BluetoothAudioDevice.FromEndpoints(
        [
            Endpoint("classic", EarbudsContainer, EarbudsName, isConnected: true, WearableHeadsetMinorClass),
            Endpoint("speaker", SpeakerContainer, SpeakerName, isConnected: false, LoudspeakerMinorClass)
        ]);

        Assert.That(devices.Select(device => device.Name), Is.EqualTo(new[] { EarbudsName, SpeakerName }));
    }

    [Test]
    public void ADeviceIsConnectedWhenAnyOfItsEndpointsIs()
    {
        IReadOnlyList<BluetoothAudioDevice> devices = BluetoothAudioDevice.FromEndpoints(
        [
            Endpoint("lowEnergy", EarbudsContainer, EarbudsName, isConnected: false, minorClass: null),
            Endpoint("classic", EarbudsContainer, EarbudsName, isConnected: true, WearableHeadsetMinorClass)
        ]);

        Assert.That(devices[0].IsConnected, Is.True);
    }

    [Test]
    public void ADeviceIsDisconnectedOnlyWhenEveryEndpointSaysSo()
    {
        IReadOnlyList<BluetoothAudioDevice> devices = BluetoothAudioDevice.FromEndpoints(
        [
            Endpoint("classic", EarbudsContainer, EarbudsName, isConnected: false, WearableHeadsetMinorClass),
            Endpoint("lowEnergy", EarbudsContainer, EarbudsName, isConnected: false, minorClass: null)
        ]);

        Assert.That(devices[0].IsConnected, Is.False);
    }

    [Test]
    public void AConnectionStateNoEndpointReportsStaysUnknown()
    {
        IReadOnlyList<BluetoothAudioDevice> devices = BluetoothAudioDevice.FromEndpoints(
            [Endpoint("classic", EarbudsContainer, EarbudsName, isConnected: null, WearableHeadsetMinorClass)]);

        Assert.That(devices[0].IsConnected, Is.Null);
    }

    [Test]
    public void TheKindComesFromWhicheverEndpointKnowsIt()
    {
        IReadOnlyList<BluetoothAudioDevice> devices = BluetoothAudioDevice.FromEndpoints(
        [
            Endpoint("lowEnergy", EarbudsContainer, EarbudsName, isConnected: false, minorClass: null),
            Endpoint("classic", EarbudsContainer, EarbudsName, isConnected: true, WearableHeadsetMinorClass)
        ]);

        Assert.That(devices[0].Kind, Is.EqualTo(BluetoothAudioDeviceKind.Headphones));
    }

    [Test]
    public void ANameIsTakenFromWhicheverEndpointHasOne()
    {
        IReadOnlyList<BluetoothAudioDevice> devices = BluetoothAudioDevice.FromEndpoints(
        [
            Endpoint("lowEnergy", EarbudsContainer, "   ", isConnected: false, minorClass: null),
            Endpoint("classic", EarbudsContainer, EarbudsName, isConnected: true, WearableHeadsetMinorClass)
        ]);

        Assert.That(devices[0].Name, Is.EqualTo(EarbudsName));
    }

    [Test]
    public void ADeviceWithNoUsableNameGetsAnEmptyOneRatherThanNull()
    {
        IReadOnlyList<BluetoothAudioDevice> devices = BluetoothAudioDevice.FromEndpoints(
            [Endpoint("classic", EarbudsContainer, "   ", isConnected: true, WearableHeadsetMinorClass)]);

        Assert.That(devices[0].Name, Is.Empty);
    }

    [Test]
    public void NoEndpointsMeansNoDevices()
    {
        Assert.That(BluetoothAudioDevice.FromEndpoints([]), Is.Empty);
    }

    [TestCase(1u, BluetoothAudioDeviceKind.Headphones)]
    [TestCase(2u, BluetoothAudioDeviceKind.Headphones)]
    [TestCase(6u, BluetoothAudioDeviceKind.Headphones)]
    [TestCase(5u, BluetoothAudioDeviceKind.Loudspeaker)]
    [TestCase(4u, BluetoothAudioDeviceKind.Unknown)]
    [TestCase(0u, BluetoothAudioDeviceKind.Unknown)]
    [TestCase(null, BluetoothAudioDeviceKind.Unknown)]
    public void TheMinorClassDecidesWhetherSomethingIsHeadphonesOrASpeaker(
        uint? minorClass,
        BluetoothAudioDeviceKind expected)
    {
        Assert.That(BluetoothAudioDeviceKinds.FromMinorClass(minorClass), Is.EqualTo(expected));
    }

    private static BluetoothAudioEndpoint Endpoint(
        string id,
        Guid containerId,
        string name,
        bool? isConnected,
        uint? minorClass)
    {
        return new BluetoothAudioEndpoint(id, containerId, name, isConnected, minorClass);
    }
}
