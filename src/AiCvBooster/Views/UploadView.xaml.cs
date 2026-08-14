using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AiCvBooster.ViewModels;

namespace AiCvBooster.Views;

public partial class UploadView : UserControl
{
    public UploadView()
    {
        InitializeComponent();
    }

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        if (HasCvFile(e))
        {
            e.Effects = DragDropEffects.Copy;
            DropZone.BorderBrush = (Brush)FindResource("PrimaryBrush");
            DropZone.Background = new SolidColorBrush(Color.FromRgb(0x1B, 0x27, 0x44));
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        DropZone.BorderBrush = (Brush)FindResource("BorderBrush.Soft");
        DropZone.Background = new SolidColorBrush(Color.FromRgb(0x14, 0x1E, 0x33));
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        DropZone.BorderBrush = (Brush)FindResource("BorderBrush.Soft");
        DropZone.Background = new SolidColorBrush(Color.FromRgb(0x14, 0x1E, 0x33));

        if (!HasCvFile(e)) return;
        var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
        if (files.Length == 0) return;

        if (DataContext is UploadViewModel vm)
            await vm.LoadFileAsync(files[0]);
    }

    private static bool HasCvFile(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return false;
        var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
        return files.Length > 0;
    }
}
