namespace AiCvBooster.Services;

public interface IDialogService
{
    string? PickCvFile();
    string? PickSavePath(string suggestedName);
}
