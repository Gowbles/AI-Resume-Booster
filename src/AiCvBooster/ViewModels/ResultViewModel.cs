using System.IO;
using System.Windows;
using AiCvBooster.Models;
using AiCvBooster.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AiCvBooster.ViewModels;

public partial class ResultViewModel : ObservableObject
{
    private readonly IDialogService _dialogs;
    private readonly MainViewModel _main;
    private readonly UploadViewModel _uploadVm;

    [ObservableProperty] private int _score;
    [ObservableProperty] private string _scoreLabel = string.Empty;
    [ObservableProperty] private string _originalText = string.Empty;
    [ObservableProperty] private string _improvedText = string.Empty;
    [ObservableProperty] private string? _toastMessage;

    public IReadOnlyList<string> Weaknesses { get; }
    public IReadOnlyList<string> Keywords { get; }

    public ResultViewModel(CvAnalysisResult result, IDialogService dialogs, MainViewModel main, UploadViewModel uploadVm)
    {
        _dialogs = dialogs;
        _main = main;
        _uploadVm = uploadVm;

        Score = result.Score;
        ScoreLabel = BuildLabel(result.Score);
        OriginalText = result.OriginalText;
        ImprovedText = result.ImprovedText;
        Weaknesses = result.Weaknesses.Count > 0
            ? result.Weaknesses
            : new[] { "No major weaknesses detected. Polish performed on tone and clarity." };
        Keywords = result.Keywords;
    }

    private static string BuildLabel(int score) => score switch
    {
        >= 80 => "Strong",
        >= 60 => "Solid — room to grow",
        >= 40 => "Needs work",
        _ => "Weak — big upside"
    };

    [RelayCommand]
    private void CopyImproved()
    {
        if (string.IsNullOrEmpty(ImprovedText)) return;
        try
        {
            Clipboard.SetText(ImprovedText);
            ToastMessage = "Improved CV copied to clipboard.";
        }
        catch
        {
            ToastMessage = "Could not access clipboard.";
        }
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrEmpty(ImprovedText)) return;

        var suggested = "ImprovedCV.txt";
        var path = _dialogs.PickSavePath(suggested);
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            File.WriteAllText(path, ImprovedText);
            ToastMessage = "Saved to " + Path.GetFileName(path);
        }
        catch (Exception ex)
        {
            ToastMessage = "Save failed: " + ex.Message;
        }
    }

    [RelayCommand]
    private void StartOver() => _main.ReturnToUpload(_uploadVm);
}
