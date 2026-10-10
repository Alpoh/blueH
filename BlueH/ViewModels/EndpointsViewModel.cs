using System.Collections.ObjectModel;
using BlueH.Diagnostics;
using BlueH.Models;
using BlueH.Services;
using AppResources = BlueH.Properties.Resources;

namespace BlueH.ViewModels;

public sealed class EndpointsViewModel : ViewModelBase
{
    private const string RefreshEndpointsOperation = "Refresh audio endpoints";

    private readonly IAudioEndpointService _endpointService;
    private readonly IUiScheduler _uiScheduler;
    private readonly IDiagnosticsLog _diagnosticsLog;

    private Guid? _containerFilter;
    private string? _errorDetail;

    public EndpointsViewModel(
        IAudioEndpointService endpointService,
        IUiScheduler uiScheduler,
        IDiagnosticsLog diagnosticsLog)
    {
        _endpointService = endpointService;
        _uiScheduler = uiScheduler;
        _diagnosticsLog = diagnosticsLog;
    }

    public ObservableCollection<AudioEndpointViewModel> Endpoints { get; } = [];

    public Guid? ContainerFilter
    {
        get => _containerFilter;
        set => SetField(ref _containerFilter, value);
    }

    public bool HasError => _errorDetail is not null;

    public bool HasNoEndpoints => Endpoints.Count == 0 && !HasError;

    public string? ErrorDetail
    {
        get => _errorDetail;
        private set
        {
            if (SetField(ref _errorDetail, value))
            {
                OnPropertyChanged(nameof(HasError));
                OnPropertyChanged(nameof(HasNoEndpoints));
            }
        }
    }

    public string Header => AppResources.EndpointsHeader;

    public string EmptyMessage => AppResources.EndpointsEmptyMessage;

    public string ErrorMessage => AppResources.EndpointsErrorMessage;

    public string VolumeHeader => AppResources.EndpointVolumeHeader;

    public string MuteLabel => AppResources.EndpointMuteLabel;

    public string MakeDefaultForPlaybackLabel => AppResources.EndpointMakeDefaultForPlaybackLabel;

    public string MakeDefaultForCommunicationsLabel => AppResources.EndpointMakeDefaultForCommunicationsLabel;

    public string DefaultForPlaybackLabel => AppResources.EndpointDefaultForPlaybackLabel;

    public string DefaultForCommunicationsLabel => AppResources.EndpointDefaultForCommunicationsLabel;

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<AudioEndpoint> endpoints =
                await _endpointService.FindRenderEndpointsAsync(cancellationToken);

            _uiScheduler.Post(() =>
            {
                ErrorDetail = null;
                Apply(endpoints);
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception failure)
        {
            _uiScheduler.Post(() => ErrorDetail = failure.Message);
            _diagnosticsLog.Record(DiagnosticOutcome.Failure, RefreshEndpointsOperation, failure.Message);
        }
    }

    private void Apply(IReadOnlyList<AudioEndpoint> endpoints)
    {
        AudioEndpoint[] matching =
        [
            .. endpoints.Where(endpoint =>
                _containerFilter is null || endpoint.ContainerId == _containerFilter)
        ];

        for (int index = Endpoints.Count - 1; index >= 0; index--)
        {
            if (!matching.Any(endpoint => endpoint.MmDeviceId == Endpoints[index].MmDeviceId))
            {
                Endpoints.RemoveAt(index);
            }
        }

        foreach (AudioEndpoint endpoint in matching)
        {
            AudioEndpointViewModel? existing =
                Endpoints.FirstOrDefault(candidate => candidate.MmDeviceId == endpoint.MmDeviceId);

            if (existing is null)
            {
                Endpoints.Add(new AudioEndpointViewModel(endpoint, _endpointService, _diagnosticsLog));
            }
            else
            {
                existing.Update(endpoint);
            }
        }

        OnPropertyChanged(nameof(HasNoEndpoints));
    }
}
