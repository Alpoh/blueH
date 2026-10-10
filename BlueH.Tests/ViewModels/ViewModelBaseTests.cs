using System.ComponentModel;
using BlueH.ViewModels;

namespace BlueH.Tests.ViewModels;

[TestFixture]
public class ViewModelBaseTests
{
    private const string DeviceNameProperty = nameof(HeadphonesViewModelStub.DeviceName);
    private const string BatteryChargePercentProperty = nameof(HeadphonesViewModelStub.BatteryChargePercent);
    private const string AnyDeviceName = "WH-1000XM5";

    private HeadphonesViewModelStub _viewModel = null!;
    private List<string?> _raisedPropertyNames = null!;
    private List<object?> _senders = null!;

    [SetUp]
    public void SetUp()
    {
        _viewModel = new HeadphonesViewModelStub();
        _raisedPropertyNames = [];
        _senders = [];
        _viewModel.PropertyChanged += RecordPropertyChanged;
    }

    [TearDown]
    public void TearDown()
    {
        _viewModel.PropertyChanged -= RecordPropertyChanged;
    }

    [Test]
    public void SetFieldWithAChangedValueRaisesPropertyChangedForThatProperty()
    {
        _viewModel.DeviceName = AnyDeviceName;

        Assert.That(_raisedPropertyNames, Is.EqualTo(new[] { DeviceNameProperty }));
    }

    [Test]
    public void SetFieldWithAChangedValueReturnsTrue()
    {
        _viewModel.DeviceName = AnyDeviceName;

        Assert.That(_viewModel.LastSetFieldResult, Is.True);
    }

    [Test]
    public void SetFieldWithAChangedValueStoresTheNewValue()
    {
        _viewModel.DeviceName = AnyDeviceName;

        Assert.That(_viewModel.DeviceName, Is.EqualTo(AnyDeviceName));
    }

    [Test]
    public void SetFieldWithAnUnchangedValueRaisesNothing()
    {
        _viewModel.DeviceName = AnyDeviceName;
        _raisedPropertyNames.Clear();

        _viewModel.DeviceName = EqualButDistinctInstanceOf(AnyDeviceName);

        Assert.That(_raisedPropertyNames, Is.Empty);
    }

    [Test]
    public void SetFieldWithAnUnchangedValueReturnsFalse()
    {
        _viewModel.DeviceName = AnyDeviceName;

        _viewModel.DeviceName = EqualButDistinctInstanceOf(AnyDeviceName);

        Assert.That(_viewModel.LastSetFieldResult, Is.False);
    }

    [Test]
    public void SetFieldAssigningNullOverAValueRaisesPropertyChanged()
    {
        _viewModel.DeviceName = AnyDeviceName;
        _raisedPropertyNames.Clear();

        _viewModel.DeviceName = null;

        Assert.That(_raisedPropertyNames, Is.EqualTo(new[] { DeviceNameProperty }));
    }

    [Test]
    public void SetFieldAssigningNullOverNullRaisesNothing()
    {
        _viewModel.DeviceName = null;

        Assert.That(_raisedPropertyNames, Is.Empty);
    }

    [Test]
    public void SetFieldWithAChangedValueTypeRaisesPropertyChanged()
    {
        _viewModel.BatteryChargePercent = 85;

        Assert.That(_raisedPropertyNames, Is.EqualTo(new[] { BatteryChargePercentProperty }));
    }

    [Test]
    public void SetFieldComparesValueTypesByValueRatherThanByReference()
    {
        _viewModel.BatteryChargePercent = 0;

        Assert.That(_raisedPropertyNames, Is.Empty);
    }

    [Test]
    public void SetFieldRaisesPropertyChangedWithTheViewModelAsSender()
    {
        _viewModel.DeviceName = AnyDeviceName;

        Assert.That(_senders, Is.EqualTo(new[] { _viewModel }));
    }

    [Test]
    public void OnPropertyChangedWithAnExplicitNameRaisesThatName()
    {
        _viewModel.NotifyExplicitly(DeviceNameProperty);

        Assert.That(_raisedPropertyNames, Is.EqualTo(new[] { DeviceNameProperty }));
    }

    private static string EqualButDistinctInstanceOf(string value)
    {
        return new string(value.ToCharArray());
    }

    private void RecordPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _raisedPropertyNames.Add(e.PropertyName);
        _senders.Add(sender);
    }

    private sealed class HeadphonesViewModelStub : ViewModelBase
    {
        private string? _deviceName;
        private int _batteryChargePercent;

        public bool LastSetFieldResult { get; private set; }

        public string? DeviceName
        {
            get => _deviceName;
            set => LastSetFieldResult = SetField(ref _deviceName, value);
        }

        public int BatteryChargePercent
        {
            get => _batteryChargePercent;
            set => LastSetFieldResult = SetField(ref _batteryChargePercent, value);
        }

        public void NotifyExplicitly(string propertyName)
        {
            OnPropertyChanged(propertyName);
        }
    }
}
