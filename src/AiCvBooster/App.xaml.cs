using System.IO;
using System.Net.Http.Headers;
using System.Text;
using System.Windows;
using AiCvBooster.Models;
using AiCvBooster.Services;
using AiCvBooster.ViewModels;
using AiCvBooster.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AiCvBooster;

public partial class App : Application
{
    private IHost? _host;
    private static readonly string CrashLogPath = Path.Combine(AppContext.BaseDirectory, "startup-error.log");

    public App()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            ReportFatalError("UI thread exception", args.Exception);
            args.Handled = true;
            Shutdown(1);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var ex = args.ExceptionObject as Exception ?? new Exception("Unknown unhandled exception.");
            ReportFatalError("AppDomain unhandled exception", ex);
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            ReportFatalError("TaskScheduler unobserved task exception", args.Exception);
            args.SetObserved();
        };
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((ctx, cfg) =>
                {
                    var baseDir = AppContext.BaseDirectory;
                    cfg.SetBasePath(baseDir);
                    cfg.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                })
                .ConfigureServices((ctx, services) =>
                {
                    services.Configure<AppSettings>(ctx.Configuration);

                    services.AddSingleton<ICvParserService, CvParserService>();
                    services.AddSingleton<IDialogService, DialogService>();

                    // Transport layer — an HttpClient-backed, retry-aware
                    // wrapper for the Gemini REST API. Completely UI-agnostic.
                    services.AddHttpClient<GeminiClient>(client =>
                    {
                        client.Timeout = TimeSpan.FromSeconds(90);
                        client.DefaultRequestHeaders.Accept.Add(
                            new MediaTypeWithQualityHeaderValue("application/json"));
                    });

                    // Domain adapter — turns CV requests into prompts and
                    // Gemini text into CvAnalysisResult instances.
                    services.AddSingleton<IAiCvService, GeminiCvService>();

                    services.AddSingleton<MainViewModel>();
                    services.AddSingleton<MainWindow>();
                })
                .Build();

            await _host.StartAsync();

            var window = _host.Services.GetRequiredService<MainWindow>();
            window.Show();
        }
        catch (Exception ex)
        {
            ReportFatalError("Startup failed", ex);
            Shutdown(1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
        base.OnExit(e);
    }

    private static void ReportFatalError(string title, Exception ex)
    {
        try
        {
            var message = new StringBuilder()
                .AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {title}")
                .AppendLine(ex.ToString())
                .AppendLine(new string('-', 80))
                .ToString();

            File.AppendAllText(CrashLogPath, message, Encoding.UTF8);
        }
        catch
        {
            // Logging must not crash the process again.
        }

        MessageBox.Show(
            $"Uygulama beklenmedik bir hatayla kapandi.\nDetay logu:\n{CrashLogPath}\n\n{ex.Message}",
            "AiCvBooster - Hata",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
