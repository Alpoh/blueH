using System.Globalization;
using BlueH.Converters;
using BlueH.Models;

namespace BlueH.Tests.Converters;

[TestFixture]
public class ConverterTests
{
    private DeviceKindToTextConverter _kindConverter = null!;
    private ConnectionStateToTextConverter _connectionConverter = null!;

    [SetUp]
    public void SetUp()
    {
        _kindConverter = new DeviceKindToTextConverter();
        _connectionConverter = new ConnectionStateToTextConverter();
    }

    [TestCase(BluetoothAudioDeviceKind.Headphones)]
    [TestCase(BluetoothAudioDeviceKind.Loudspeaker)]
    [TestCase(BluetoothAudioDeviceKind.Unknown)]
    public void EveryDeviceKindHasText(BluetoothAudioDeviceKind kind)
    {
        Assert.That(Convert(_kindConverter.Convert, kind), Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void DeviceKindsGetDifferentText()
    {
        object?[] texts =
        [
            _kindConverter.Convert(BluetoothAudioDeviceKind.Headphones, typeof(string), null, CultureInfo.InvariantCulture),
            _kindConverter.Convert(BluetoothAudioDeviceKind.Loudspeaker, typeof(string), null, CultureInfo.InvariantCulture),
            _kindConverter.Convert(BluetoothAudioDeviceKind.Unknown, typeof(string), null, CultureInfo.InvariantCulture)
        ];

        Assert.That(texts, Is.Unique);
    }

    [Test]
    public void AnUnexpectedValueFallsBackToTheUnknownKindRatherThanThrowing()
    {
        Assert.That(Convert(_kindConverter.Convert, "not a kind"), Is.Not.Null.And.Not.Empty);
    }

    [TestCase(true)]
    [TestCase(false)]
    [TestCase(null)]
    public void EveryConnectionStateHasText(bool? isConnected)
    {
        Assert.That(Convert(_connectionConverter.Convert, isConnected), Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void ConnectedAndDisconnectedReadDifferently()
    {
        Assert.That(
            Convert(_connectionConverter.Convert, true),
            Is.Not.EqualTo(Convert(_connectionConverter.Convert, false)));
    }

    [Test]
    public void AnUnknownConnectionStateIsNotShownAsDisconnected()
    {
        Assert.That(
            Convert(_connectionConverter.Convert, null),
            Is.Not.EqualTo(Convert(_connectionConverter.Convert, false)));
    }

    [Test]
    public void TheKindConverterRefusesToConvertBack()
    {
        Assert.That(
            () => _kindConverter.ConvertBack(null, typeof(object), null, CultureInfo.InvariantCulture),
            Throws.InstanceOf<NotSupportedException>());
    }

    [Test]
    public void TheConnectionConverterRefusesToConvertBack()
    {
        Assert.That(
            () => _connectionConverter.ConvertBack(null, typeof(object), null, CultureInfo.InvariantCulture),
            Throws.InstanceOf<NotSupportedException>());
    }

    private static object? Convert(Func<object?, Type, object?, CultureInfo, object?> convert, object? value)
    {
        return convert(value, typeof(string), null, CultureInfo.InvariantCulture);
    }
}
