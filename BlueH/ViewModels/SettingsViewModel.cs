using System.Globalization;
using System.IO;
using System.Windows.Input;
using BlueH.Diagnostics;
using BlueH.Models;
using BlueH.Services;
using AppResources = BlueH.Properties.Resources;

namespace BlueH.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    public static readonly Uri GadgetbridgeAddress = new("https://gadgetbridge.org/");

    private const string SaveSettingsOperation = "Save settings";

    private readonly ISettingsStore _settingsStore;
    private readonly IDiagnosticsLog _diagnosticsLog;
    private readonly string? _activeLanguageTag;

    private LanguageOption _selectedLanguage;
    private bool _isDeveloperModeEnabled;
    private string? _errorDetail;

    public SettingsViewModel(
        ISettingsStore settingsStore,
        IDiagnosticsLog diagnosticsLog,
        IApplicationInfo applicationInfo,
        IExternalLinkOpener linkOpener,
        string? activeLanguageTag)
    {
        _settingsStore = settingsStore;
        _diagnosticsLog = diagnosticsLog;
        _activeLanguageTag = CultureSelection.NormalizeSupportedTag(activeLanguageTag);
        Languages =
        [
            new LanguageOption(LanguageTag: null, AppResources.LanguageSystemDefault),
            .. CultureSelection.SupportedLanguageTags.Select(tag =>
                new LanguageOption(tag, CultureSelection.NativeDisplayName(tag)))
        ];

        AppSettings stored = _settingsStore.Load().Settings;
        string? storedTag = CultureSelection.NormalizeSupportedTag(stored.LanguageTag);
        _selectedLanguage = Languages.First(option => option.LanguageTag == storedTag);
        _isDeveloperModeEnabled = stored.IsDeveloperModeEnabled;
        VersionText = string.Format(CultureInfo.CurrentCulture, AppResources.SettingsVersionFormat, applicationInfo.Version);
        OpenCreditLinkCommand = new RelayCommand(_ => linkOpener.Open(GadgetbridgeAddress));
    }

    public IReadOnlyList<LanguageOption> Languages { get; }

    public LanguageOption SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (!SetField(ref _selectedLanguage, value))
            {
                return;
            }

            Save(current => current with { LanguageTag = value.LanguageTag });
            OnPropertyChanged(nameof(IsRestartRequired));
        }
    }

    public bool IsDeveloperModeEnabled
    {
        get => _isDeveloperModeEnabled;
        set
        {
            if (SetField(ref _isDeveloperModeEnabled, value))
            {
                Save(current => current with { IsDeveloperModeEnabled = value });
            }
        }
    }

    public bool IsRestartRequired => _selectedLanguage.LanguageTag != _activeLanguageTag;

    public string? ErrorDetail
    {
        get => _errorDetail;
        private set
        {
            if (SetField(ref _errorDetail, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => _errorDetail is not null;

    public string VersionText { get; }

    public ICommand OpenCreditLinkCommand { get; }

    public string Header => AppResources.SettingsHeader;

    public string LanguageLabel => AppResources.SettingsLanguageLabel;

    public string LanguageDescription => AppResources.SettingsLanguageDescription;

    public string RestartRequiredMessage => AppResources.SettingsRestartRequiredMessage;

    public string DeveloperModeLabel => AppResources.SettingsDeveloperModeLabel;

    public string DeveloperModeDescription => AppResources.SettingsDeveloperModeDescription;

    public string AboutHeader => AppResources.SettingsAboutHeader;

    public string ApplicationName => AppResources.ApplicationTitle;

    public string CreditText => AppResources.SettingsCreditText;

    public string CreditLinkLabel => AppResources.SettingsCreditLinkLabel;

    private void Save(Func<AppSettings, AppSettings> change)
    {
        try
        {
            AppSettings current = _settingsStore.Load().Settings;
            _settingsStore.Save(change(current));
            ErrorDetail = null;
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            ErrorDetail = string.Format(CultureInfo.CurrentCulture, AppResources.SettingsSaveFailedFormat, failure.Message);
            _diagnosticsLog.Record(DiagnosticOutcome.Failure, SaveSettingsOperation, failure.Message);
        }
    }
}
