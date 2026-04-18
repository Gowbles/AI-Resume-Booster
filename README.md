# AI CV Booster

A .NET 8 WPF desktop application that uses Google Gemini AI to analyze, score, and rewrite CVs for clarity, impact, and ATS optimization — without fabricating any information.

---

## Key Features

- **PDF & DOCX Parsing** — Upload CVs via drag-and-drop or file picker; text extracted using PdfPig and OpenXml
- **AI-Powered Rewrite** — Google Gemini analyzes and rewrites your CV with stronger action verbs, better structure, and ATS-friendly keywords
- **CV Scoring** — Receives a 0–100 score with detailed weakness analysis and keyword suggestions
- **Job Description Targeting** — Paste a job posting to tailor the rewrite with role-specific keywords
- **Aggressive Mode** — Toggle for a bolder, high-agency writing tone
- **Side-by-Side Comparison** — View original and improved CV text in parallel
- **Export** — Copy to clipboard or save as `.txt`
- **Fully Async** — All AI and parsing work runs off the UI thread; the interface never freezes
- **Cancelable** — Cancel mid-analysis from the loading screen
- **Truth-Preserving** — The AI is explicitly instructed not to fabricate credentials, employers, dates, or qualifications

---

## Tech Stack

| Layer | Technology |
|---|---|
| **Language** | C# (latest) / .NET 8.0 |
| **UI Framework** | WPF (`net8.0-windows`) |
| **Architecture** | MVVM via `CommunityToolkit.Mvvm` 8.3.2 |
| **DI & Config** | `Microsoft.Extensions.Hosting` / `DependencyInjection` / `Configuration` 8.x |
| **UI Theme** | `MaterialDesignThemes` 5.1.0 + `MahApps.Metro` 2.4.10 (Dark theme, DeepPurple/Lime) |
| **PDF Parsing** | `UglyToad.PdfPig` 1.7.0 |
| **DOCX Parsing** | `DocumentFormat.OpenXml` 3.1.0 |
| **AI Provider** | Google Gemini API (REST via `HttpClient`) |
| **HTTP** | `Microsoft.Extensions.Http` with 90s timeout + exponential backoff retries |

---

## Prerequisites

| Requirement | Details |
|---|---|
| **OS** | Windows 10 or later (WPF — Windows only) |
| **.NET SDK** | [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) |
| **Gemini API Key** | Obtain from [Google AI Studio](https://aistudio.google.com/apikey) |

---

## Installation & Setup

### 1. Clone the repository

```bash
git clone <repository-url>
cd new_cv
```

### 2. Configure the Gemini API key

Copy the sample config and add your API key:

```bash
cp src/AiCvBooster/appsettings.sample.json src/AiCvBooster/appsettings.json
```

Edit `src/AiCvBooster/appsettings.json`:

```json
{
  "Gemini": {
    "ApiKey": "YOUR_GEMINI_API_KEY_HERE",
    "Model": "gemini-2.0-flash",
    "BaseUrl": "https://generativelanguage.googleapis.com/v1beta"
  }
}
```

> **Note:** `appsettings.json` is listed in `.gitignore` and will not be committed. Never share your API key.

### 3. Restore and build

```bash
dotnet restore
dotnet build src/AiCvBooster/AiCvBooster.csproj -c Debug
```

### 4. Run

```bash
dotnet run --project src/AiCvBooster
```

Or run the compiled binary directly:

```
src\AiCvBooster\bin\Debug\net8.0-windows\AiCvBooster.exe
```

---

## Usage Guide

1. **Upload** — Drag a `.pdf` or `.docx` file onto the drop zone, or click to browse
2. **Target (optional)** — Paste a job description into the targeting field to tailor keyword optimization
3. **Aggressive Mode (optional)** — Enable for a bolder, more assertive rewrite tone
4. **Boost** — Click **Boost My CV** to start analysis
5. **Review** — Examine the score (0–100), weaknesses, suggested keywords, and the rewritten CV side-by-side
6. **Export** — Copy the improved CV to clipboard or save as a `.txt` file

### Configuration Options

| Setting | Description | Example Values |
|---|---|---|
| `Gemini:ApiKey` | Your Google Gemini API key | `AIzaSy...` |
| `Gemini:Model` | Gemini model to use | `gemini-2.0-flash`, `gemini-1.5-pro`, `gemini-1.5-flash` |
| `Gemini:BaseUrl` | API endpoint | `https://generativelanguage.googleapis.com/v1beta` |

---

## Preview

<p align="center">
  <img src="https://github.com/user-attachments/assets/a559f1fa-0a98-4c30-906e-c86c51b8c667" width="700" />
</p>

<p align="center">
  <img src="https://github.com/user-attachments/assets/d216d972-b59f-4691-8783-78386a07dd88" width="700" />
</p>

## Architecture & Technical Details

### Project Structure

```
AiCvBooster.sln
src/AiCvBooster/
├── App.xaml(.cs)           # DI host bootstrap, global exception handlers
├── appsettings.json        # Runtime config (not committed)
├── appsettings.sample.json # Template for setup
├── Models/                 # CvDocument, AnalysisRequest, CvAnalysisResult, AppSettings
├── Services/               # CvParserService, GeminiClient, GeminiCvService, PromptBuilder, DialogService
├── ViewModels/             # MainViewModel, UploadViewModel, LoadingViewModel, ResultViewModel
├── Views/                  # MainWindow + Upload/Loading/Result XAML views
├── Resources/              # Colors, Typography, Controls (XAML resource dictionaries)
└── Converters/             # BoolToVisibilityConverter, ScoreToBrushConverter
```

### Data Flow

```
PDF/DOCX File
    → CvParserService (text extraction)
    → CvDocument { RawText, Source }
    → PromptBuilder (constructs AI prompt with job desc + mode)
    → GeminiClient (HTTP POST to Gemini API, retries on 429/5xx)
    → GeminiCvService (JSON parse response)
    → CvAnalysisResult { Score, Weaknesses, Keywords, ImprovedText }
    → ResultViewModel (displayed in UI)
```

### Key Design Decisions

- **MVVM + DI** — ViewModels are injected via `Microsoft.Extensions.DependencyInjection`; Views bind through XAML `DataContext`
- **Service Abstraction** — AI logic sits behind `IAiCvService`, making the Gemini provider swappable
- **Resilient HTTP** — `GeminiClient` implements exponential backoff (1s → 3s → 6s) for transient failures (429, 5xx, timeouts)
- **Structured Errors** — All AI failures are wrapped in `AiServiceException` with an `AiFailureKind` enum for clean UI error messaging
- **State Machine** — `MainViewModel` manages view transitions: Upload → Loading → Result (or back to Upload on error/cancel)

---

## Troubleshooting

### Common Issues and Fixes

| Problem | Cause | Fix |
|---|---|---|
| **App crashes on startup with no UI** | Missing or malformed `appsettings.json` | Copy `appsettings.sample.json` to `appsettings.json` and fill in your API key. Check `startup-error.log` in the app directory for details |
| **"Authentication" error on analysis** | Invalid or expired Gemini API key | Verify your API key at [Google AI Studio](https://aistudio.google.com/apikey) and update `appsettings.json` |
| **"RateLimited" error** | Too many requests to Gemini API | Wait a minute and retry. The app already retries with backoff, so this means the quota is genuinely exhausted |
| **"Network" error** | No internet or firewall blocking `generativelanguage.googleapis.com` | Check connectivity. Ensure your firewall/proxy allows HTTPS to `generativelanguage.googleapis.com` |
| **"InvalidResponse" error** | Gemini returned unexpected JSON | Try a different model in `appsettings.json` (e.g., `gemini-2.0-flash`). Some preview models have inconsistent output |
| **PDF text is empty or garbled** | Scanned/image-based PDF without embedded text | Use a DOCX file instead, or run the PDF through OCR software first |
| **Build fails: "net8.0-windows" not found** | .NET 8 SDK not installed or wrong OS | Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0). This app requires Windows |
| **UI elements look broken** | Missing NuGet packages | Run `dotnet restore` before building |
| **90-second timeout** | Very large CV or slow API response | Split the CV into fewer pages, or try a faster model like `gemini-2.0-flash` |

### Diagnostic Files

- **`startup-error.log`** — Written to the app directory if the application crashes during startup. Contains the full exception stack trace.

---

## Contributing

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/your-feature`
3. Make your changes, ensuring they follow the existing MVVM architecture
4. Test locally with `dotnet build` and `dotnet run --project src/AiCvBooster`
5. Submit a pull request with a clear description of what changed and why

### Guidelines

- Follow the existing service abstraction pattern — new AI providers should implement `IAiCvService`
- Keep ViewModels free of direct UI dependencies
- Do not commit `appsettings.json` or any API keys

---

## License

See [LICENSE](LICENSE) for details.
