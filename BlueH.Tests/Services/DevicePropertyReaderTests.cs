using BlueH.Services;

namespace BlueH.Tests.Services;

[TestFixture]
public class DevicePropertyReaderTests
{
    private static readonly Guid AnyContainer = Guid.Parse("0ac66fc7-8a44-50f8-ae2b-25ffdd18b301");

    [Test]
    public void TheContainerIdIsReadWhenWindowsSuppliesAGuid()
    {
        Dictionary<string, object?> properties = new()
        {
            [DevicePropertyReader.ContainerIdProperty] = AnyContainer
        };

        Assert.That(DevicePropertyReader.ReadContainerId(properties), Is.EqualTo(AnyContainer));
    }

    [Test]
    public void AMissingContainerIdReadsAsEmptyRatherThanThrowing()
    {
        Assert.That(DevicePropertyReader.ReadContainerId(new Dictionary<string, object?>()), Is.EqualTo(Guid.Empty));
    }

    [Test]
    public void AContainerIdOfTheWrongTypeReadsAsEmpty()
    {
        Dictionary<string, object?> properties = new()
        {
            [DevicePropertyReader.ContainerIdProperty] = AnyContainer.ToString()
        };

        Assert.That(DevicePropertyReader.ReadContainerId(properties), Is.EqualTo(Guid.Empty));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void TheConnectionStateIsReadWhenWindowsSuppliesABoolean(bool isConnected)
    {
        Dictionary<string, object?> properties = new()
        {
            [DevicePropertyReader.IsConnectedProperty] = isConnected
        };

        Assert.That(DevicePropertyReader.ReadConnectionState(properties), Is.EqualTo(isConnected));
    }

    [Test]
    public void AMissingConnectionStateStaysUnknownRatherThanBecomingDisconnected()
    {
        Assert.That(DevicePropertyReader.ReadConnectionState(new Dictionary<string, object?>()), Is.Null);
    }

    [Test]
    public void TheMinorClassIsReadFromTheUnsignedShortWindowsActuallyReturns()
    {
        Dictionary<string, object?> properties = new()
        {
            [DevicePropertyReader.MinorClassProperty] = (ushort)1
        };

        Assert.That(DevicePropertyReader.ReadMinorClass(properties), Is.EqualTo(1u));
    }

    [Test]
    public void TheMinorClassIsAlsoReadFromAnUnsignedInteger()
    {
        Dictionary<string, object?> properties = new()
        {
            [DevicePropertyReader.MinorClassProperty] = 5u
        };

        Assert.That(DevicePropertyReader.ReadMinorClass(properties), Is.EqualTo(5u));
    }

    [Test]
    public void TheMinorClassIsAlsoReadFromAByte()
    {
        Dictionary<string, object?> properties = new()
        {
            [DevicePropertyReader.MinorClassProperty] = (byte)6
        };

        Assert.That(DevicePropertyReader.ReadMinorClass(properties), Is.EqualTo(6u));
    }

    [Test]
    public void TheMinorClassIsAlsoReadFromASignedInteger()
    {
        Dictionary<string, object?> properties = new()
        {
            [DevicePropertyReader.MinorClassProperty] = 2
        };

        Assert.That(DevicePropertyReader.ReadMinorClass(properties), Is.EqualTo(2u));
    }

    [Test]
    public void ANegativeMinorClassIsRejectedRatherThanWrappingAround()
    {
        Dictionary<string, object?> properties = new()
        {
            [DevicePropertyReader.MinorClassProperty] = -1
        };

        Assert.That(DevicePropertyReader.ReadMinorClass(properties), Is.Null);
    }

    [Test]
    public void AMissingMinorClassStaysUnknown()
    {
        Assert.That(DevicePropertyReader.ReadMinorClass(new Dictionary<string, object?>()), Is.Null);
    }

    [Test]
    public void AMinorClassOfTheWrongTypeStaysUnknown()
    {
        Dictionary<string, object?> properties = new()
        {
            [DevicePropertyReader.MinorClassProperty] = "1"
        };

        Assert.That(DevicePropertyReader.ReadMinorClass(properties), Is.Null);
    }
}
