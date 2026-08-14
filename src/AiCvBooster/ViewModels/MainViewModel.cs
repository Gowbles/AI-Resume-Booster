using System.Diagnostics;
using AiCvBooster.Models;
using AiCvBooster.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AiCvBooster.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IAiCvService _ai;
    private readonly IDialogService _dialogs;

    [ObservableProperty]
    private ObservableObject? _currentView;

    public MainViewModel(
        IAiCvService ai,
        IDialogService dialogs,
        ICvParserService parser)
    {
        _ai = ai;
        _dialogs = dialogs;
        ShowUpload(new UploadViewModel(parser, dialogs, this));
    }

    public void ShowUpload(UploadViewModel vm) => CurrentView = vm;

    // ─────────────────────────────────────────────────────────────────────
    //  StartAnalysisAsync
    // ─────────────────────────────────────────────────────────────────────
    //  Orchestrates the Upload → Loading → Result transition and owns the
    //  error-handling policy for the whole pipeline.
    //
    //  Error handling strategy:
    //    • AiServiceException  → already has a friendly message, show it
    //                            verbatim and log the technical detail.
    //    • OperationCanceledException → user cancelled from LoadingView,
    //                            silently return to Upload (no banner).
    //    • Anything else       → wrap with a generic friendly message so
    //                            the UI never displays raw stack text.
    //  The app should NEVER crash because of a bad API call.
    // ─────────────────────────────────────────────────────────────────────
    public async Task StartAnalysisAsync(AnalysisRequest request, UploadViewModel uploadVm)
    {
        var loading = new LoadingViewModel();
        CurrentView = loading;

        try
        {
            var result = await _ai.AnalyzeAsync(request, loading.CancellationToken)
                                  .ConfigureAwait(true);
            CurrentView = new ResultViewModel(result, _dialogs, this, uploadVm);
        }
        catch (AiServiceException aiex) when (aiex.Kind == AiFailureKind.Cancelled)
        {
            // User hit Cancel — no banner, just go back.
            CurrentView = uploadVm;
        }
        catch (OperationCanceledException)
        {
            CurrentView = uploadVm;
        }
        catch (AiServiceException aiex)
        {
            Debug.WriteLine($"[AI] {aiex.Kind}: {aiex.TechnicalDetail}");
            uploadVm.ErrorMessage = aiex.FriendlyMessage;
            CurrentView = uploadVm;
        }
        catch (Exception ex)
        {
            // Last-resort net: we should rarely reach here because GeminiClient
            // maps everything to AiServiceException, but if anything slips
            // through we still refuse to crash and refuse to leak the raw text.
            Debug.WriteLine($"[AI] Unexpected: {ex}");
            uploadVm.ErrorMessage = "Something went wrong while analyzing your CV. Please try again.";
            CurrentView = uploadVm;
        }
    }

    public void ReturnToUpload(UploadViewModel uploadVm)
    {
        uploadVm.ErrorMessage = null;
        CurrentView = uploadVm;
    }
}
