using System.IO;
using AiCvBooster.Models;
using AiCvBooster.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AiCvBooster.ViewModels;

public partial class UploadViewModel : ObservableObject
{
    private readonly ICvParserService _parser;
    private readonly IDialogService _dialogs;
    private MainViewModel? _main;

    [ObservableProperty] private CvDocument? _document;
    [ObservableProperty] private string? _fileName;
    [ObservableProperty] private string? _statusText;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string _jobDescription = string.Empty;
    [ObservableProperty] private bool _aggressiveMode;
    [ObservableProperty] private bool _isBusy;

    public UploadViewModel(ICvParserService parser, IDialogService dialogs, MainViewModel main)
    {
        _parser = parser;
        _dialogs = dialogs;
        _main = main;
    }

    public bool HasDocument => Document is not null;
    public bool CanBoost => HasDocument && !IsBusy;

    partial void OnDocumentChanged(CvDocument? value)
    {
        OnPropertyChanged(nameof(HasDocument));
        OnPropertyChanged(nameof(CanBoost));
        BoostCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanBoost));
        BoostCommand.NotifyCanExecuteChanged();
        PickFileCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanInteract))]
    private async Task PickFileAsync()
    {
        var path = _dialogs.PickCvFile();
        if (string.IsNullOrWhiteSpace(path)) return;
        await LoadFileAsync(path).ConfigureAwait(true);
    }

    private bool CanInteract() => !IsBusy;

    public async Task LoadFileAsync(string path)
    {
        ErrorMessage = null;
        if (!_parser.IsSupported(path))
        {
            ErrorMessage = "Unsupported file. Please choose a PDF or DOCX.";
            return;
        }

        try
        {
            IsBusy = true;
            StatusText = "Reading " + Path.GetFileName(path) + "…";
            var doc = await _parser.ExtractAsync(path).ConfigureAwait(true);

            if (string.IsNullOrWhiteSpace(doc.RawText))
            {
                ErrorMessage = "No readable text found in the file.";
                Document = null;
                return;
            }

            Document = doc;
            FileName = doc.FileName;
            StatusText = $"Loaded — {doc.RawText.Length:N0} characters extracted.";
        }
        catch (Exception ex)
        {
            ErrorMessage = "Could not read file: " + ex.Message;
            Document = null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanBoost))]
    private async Task BoostAsync()
    {
        if (Document is null || _main is null) return;

        var request = new AnalysisRequest
        {
            OriginalText = Document.RawText,
            JobDescription = string.IsNullOrWhiteSpace(JobDescription) ? null : JobDescription,
            AggressiveMode = AggressiveMode
        };

        await _main.StartAnalysisAsync(request, this).ConfigureAwait(true);
    }
}
