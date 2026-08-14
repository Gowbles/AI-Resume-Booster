using System.Windows;
using AiCvBooster.ViewModels;

namespace AiCvBooster.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        StateChanged += OnStateChanged;
    }

    // ─── Mac-style traffic-light handlers ────────────────────────────────
    private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();

    private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void MaximizeBtn_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    // When maximized with AllowsTransparency=true the outer 12px shadow-margin
    // would otherwise create a gap — collapse it so the window fills the work
    // area cleanly, and restore it on un-maximize.
    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (RootBorder is null) return;
        RootBorder.Margin = WindowState == WindowState.Maximized
            ? new Thickness(0)
            : new Thickness(12);
        RootBorder.CornerRadius = WindowState == WindowState.Maximized
            ? new CornerRadius(0)
            : new CornerRadius(14);
    }
}
