using System.Globalization;
using System.IO;
using BlueH.Diagnostics;
using BlueH.Models;
using BlueH.Services;
using AppResources = BlueH.Properties.Resources;

namespace BlueH.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private const string SaveLanguageOperation = "Save language";

    private readonly ISettingsStore _settingsStore;
    private readonly IDiagnosticsLog _diagnosticsLog;
    private readonly string? _activeLanguageTag;

    private LanguageOption _selectedLanguage;
    private string? _errorDetail;

    public SettingsViewModel(ISettingsStore settingsStore, IDiagnosticsLog diagnosticsLog, string? activeLanguageTag)
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

        string? storedTag = CultureSelection.NormalizeSupportedTag(_settingsStore.Load().Settings.LanguageTag);
        _selectedLanguage = Languages.First(option => option.LanguageTag == storedTag);
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

            Save(value.LanguageTag);
            OnPropertyChanged(nameof(IsRestartRequired));
        }
    }

    public bool IsRestartRequired => _selectedLanguage.LanguageTag != _activeLanguageTag;

    public string? ErrorDetail
    {
        get => _errorDetail;
        private set => SetField(ref _errorDetail, value);
    }

    public string Header => AppResources.SettingsHeader;

    public string LanguageLabel => AppResources.SettingsLanguageLabel;

    public string RestartRequiredMessage => AppResources.SettingsRestartRequiredMessage;

    private void Save(string? languageTag)
    {
        try
        {
            AppSettings current = _settingsStore.Load().Settings;
            _settingsStore.Save(current with { LanguageTag = languageTag });
            ErrorDetail = null;
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            ErrorDetail = string.Format(CultureInfo.CurrentCulture, AppResources.SettingsSaveFailedFormat, failure.Message);
            _diagnosticsLog.Record(DiagnosticOutcome.Failure, SaveLanguageOperation, failure.Message);
        }
    }
}
