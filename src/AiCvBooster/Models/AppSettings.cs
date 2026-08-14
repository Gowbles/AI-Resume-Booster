namespace AiCvBooster.Models;

public sealed class AppSettings
{
    public GeminiOptions Gemini { get; set; } = new();
}

public sealed class GeminiOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-3.1-flash-lite-preview";
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";
}
