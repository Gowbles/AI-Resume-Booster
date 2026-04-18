using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AiCvBooster.ViewModels;

public partial class LoadingViewModel : ObservableObject
{
    private readonly CancellationTokenSource _cts = new();

    [ObservableProperty] private string _statusText = "Analyzing your CV…";
    [ObservableProperty] private string _subText = "Our AI is reviewing tone, structure, and impact.";

    public CancellationToken CancellationToken => _cts.Token;

    [RelayCommand]
    private void Cancel()
    {
        if (!_cts.IsCancellationRequested)
            _cts.Cancel();
    }
}
