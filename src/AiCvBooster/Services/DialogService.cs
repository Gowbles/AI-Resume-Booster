using Microsoft.Win32;

namespace AiCvBooster.Services;

public sealed class DialogService : IDialogService
{
    public string? PickCvFile()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select your CV",
            Filter = "CV files (*.pdf;*.docx)|*.pdf;*.docx|PDF (*.pdf)|*.pdf|Word (*.docx)|*.docx",
            CheckFileExists = true,
            Multiselect = false
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public string? PickSavePath(string suggestedName)
    {
        var dlg = new SaveFileDialog
        {
            Title = "Save improved CV",
            FileName = suggestedName,
            Filter = "Text file (*.txt)|*.txt",
            DefaultExt = ".txt",
            AddExtension = true
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }
}
