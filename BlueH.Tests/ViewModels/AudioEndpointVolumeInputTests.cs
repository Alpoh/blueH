using System.ComponentModel;
using BlueH.Diagnostics;
using BlueH.Models;
using BlueH.Tests.Fakes;
using BlueH.ViewModels;
using Microsoft.Extensions.Time.Testing;

namespace BlueH.Tests.ViewModels;

[TestFixture]
public class AudioEndpointVolumeInputTests
{
    private const string EndpointId = "{0.0.0.00000000}.{63a7089b-a6c3-4900-b04a-d95f0654486c}";
    private const string EndpointName = "Headphones (OPPO Enco Air5)";
    private const int InitialVolumePercent = 76;

    private static readonly Guid EarbudsContainer = Guid.Parse("0ac66fc7-8a44-50f8-ae2b-25ffdd18b301");

    private FakeAudioEndpointService _endpointService = null!;
    private InMemoryDiagnosticsLog _diagnosticsLog = null!;

    [SetUp]
    public void SetUp()
    {
        _endpointService = new FakeAudioEndpointService();
        _diagnosticsLog = new InMemoryDiagnosticsLog(new FakeTimeProvider());
    }

    [Test]
    public void TheEditorIsClosedUntilItIsOpened()
    {
        AudioEndpointViewModel endpoint = CreateViewModel();

        Assert.That(endpoint.IsEditingVolume, Is.False);
    }

    [Test]
    public void OpeningTheEditorOffersTheCurrentVolumeForEditing()
    {
        AudioEndpointViewModel endpoint = CreateViewModel();

        endpoint.BeginEditVolumeCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(endpoint.IsEditingVolume, Is.True);
            Assert.That(endpoint.VolumeInput, Is.EqualTo("76"));
        });
    }

    [Test]
    public void CommittingAWholeNumberAppliesItToTheEndpoint()
    {
        AudioEndpointViewModel endpoint = CreateViewModel();

        endpoint.BeginEditVolumeCommand.Execute(null);
        endpoint.VolumeInput = "30";
        endpoint.CommitVolumeInputCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(endpoint.VolumePercent, Is.EqualTo(30));
            Assert.That(_endpointService.VolumeCalls, Is.EqualTo(new[] { (EndpointId, 30) }));
            Assert.That(endpoint.IsEditingVolume, Is.False);
        });
    }

    [TestCase("101")]
    [TestCase("-5")]
    public void CommittingAVolumeOutsideTheRangeChangesNothingAndKeepsTheEditorOpen(string text)
    {
        AudioEndpointViewModel endpoint = CreateViewModel();

        endpoint.BeginEditVolumeCommand.Execute(null);
        endpoint.VolumeInput = text;
        endpoint.CommitVolumeInputCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(endpoint.VolumePercent, Is.EqualTo(InitialVolumePercent));
            Assert.That(_endpointService.VolumeCalls, Is.Empty);
            Assert.That(endpoint.IsEditingVolume, Is.True);
        });
    }

    [Test]
    public void CommittingTextThatIsNotANumberChangesNothingAndKeepsTheEditorOpen()
    {
        AudioEndpointViewModel endpoint = CreateViewModel();

        endpoint.BeginEditVolumeCommand.Execute(null);
        endpoint.VolumeInput = "loud";
        endpoint.CommitVolumeInputCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(endpoint.VolumePercent, Is.EqualTo(InitialVolumePercent));
            Assert.That(_endpointService.VolumeCalls, Is.Empty);
            Assert.That(endpoint.IsEditingVolume, Is.True);
        });
    }

    [Test]
    public void AnUnusableEntryIsReportedAsAValidationErrorOnTheInput()
    {
        AudioEndpointViewModel endpoint = CreateViewModel();
        List<string> changedProperties = [];
        endpoint.ErrorsChanged += (_, args) => changedProperties.Add(args.PropertyName ?? string.Empty);

        endpoint.BeginEditVolumeCommand.Execute(null);
        endpoint.VolumeInput = "101";

        Assert.Multiple(() =>
        {
            Assert.That(endpoint.HasErrors, Is.True);
            Assert.That(ErrorsFor(endpoint, nameof(endpoint.VolumeInput)), Is.Not.Empty);
            Assert.That(changedProperties, Does.Contain(nameof(endpoint.VolumeInput)));
        });
    }

    [Test]
    public void AUsableEntryIsReportedWithoutAnError()
    {
        AudioEndpointViewModel endpoint = CreateViewModel();

        endpoint.BeginEditVolumeCommand.Execute(null);
        endpoint.VolumeInput = "101";
        endpoint.VolumeInput = "99";

        Assert.Multiple(() =>
        {
            Assert.That(endpoint.HasErrors, Is.False);
            Assert.That(ErrorsFor(endpoint, nameof(endpoint.VolumeInput)), Is.Empty);
        });
    }

    [Test]
    public void CancellingLeavesTheVolumeUntouchedAndClosesTheEditor()
    {
        AudioEndpointViewModel endpoint = CreateViewModel();

        endpoint.BeginEditVolumeCommand.Execute(null);
        endpoint.VolumeInput = "12";
        endpoint.CancelVolumeEditCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(endpoint.VolumePercent, Is.EqualTo(InitialVolumePercent));
            Assert.That(_endpointService.VolumeCalls, Is.Empty);
            Assert.That(endpoint.IsEditingVolume, Is.False);
        });
    }

    [Test]
    public void CancellingARejectedEntryTakesTheErrorWithIt()
    {
        AudioEndpointViewModel endpoint = CreateViewModel();

        endpoint.BeginEditVolumeCommand.Execute(null);
        endpoint.VolumeInput = "999";
        endpoint.CancelVolumeEditCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(endpoint.HasErrors, Is.False);
            Assert.That(ErrorsFor(endpoint, nameof(endpoint.VolumeInput)), Is.Empty);
            Assert.That(endpoint.IsEditingVolume, Is.False);
        });
    }

    [Test]
    public void ReopeningTheEditorDiscardsTheEarlierRejectedEntry()
    {
        AudioEndpointViewModel endpoint = CreateViewModel();

        endpoint.BeginEditVolumeCommand.Execute(null);
        endpoint.VolumeInput = "999";
        endpoint.CancelVolumeEditCommand.Execute(null);
        endpoint.BeginEditVolumeCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(endpoint.VolumeInput, Is.EqualTo("76"));
            Assert.That(endpoint.HasErrors, Is.False);
        });
    }

    [Test]
    public void CommittingIsUnavailableWhileTheEditorIsClosed()
    {
        AudioEndpointViewModel endpoint = CreateViewModel();

        Assert.Multiple(() =>
        {
            Assert.That(endpoint.CommitVolumeInputCommand.CanExecute(null), Is.False);
            Assert.That(endpoint.CancelVolumeEditCommand.CanExecute(null), Is.False);
        });
    }

    [Test]
    public void CommittingIsAvailableWhileTheEditorIsOpen()
    {
        AudioEndpointViewModel endpoint = CreateViewModel();

        endpoint.BeginEditVolumeCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(endpoint.CommitVolumeInputCommand.CanExecute(null), Is.True);
            Assert.That(endpoint.CancelVolumeEditCommand.CanExecute(null), Is.True);
        });
    }

    private static IEnumerable<string> ErrorsFor(INotifyDataErrorInfo endpoint, string propertyName)
    {
        return endpoint.GetErrors(propertyName).Cast<string>();
    }

    private AudioEndpointViewModel CreateViewModel()
    {
        AudioEndpoint endpoint = new(
            EndpointId,
            EarbudsContainer,
            EndpointName,
            InitialVolumePercent,
            IsMuted: false,
            IsDefaultForPlayback: false,
            IsDefaultForCommunications: false);

        return new AudioEndpointViewModel(endpoint, _endpointService, _diagnosticsLog);
    }
}
