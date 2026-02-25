using ApexUIBridge.Core;
using ApexUIBridge.Core.Logger;
using ApexUIBridge.Forms;
using ApexUIBridge.Settings;

using LLama.Native;

using Microsoft.Extensions.DependencyInjection;

using System.IO;
using System.Windows.Forms;

namespace ApexUIBridge;

/// <summary>
/// Application entry point. Bootstraps the DI container, loads app settings,
/// converts overlay configurations into factory delegates, initializes the
/// LlamaSharp native library (CPU-only mode, no CUDA/Vulkan), and launches
/// <see cref="Forms.StartupForm"/> as the WinForms main window.
/// </summary>
internal static class Program {
    /// <summary>
    /// Main entry point. Must run on an STA thread for COM/WinForms compatibility.
    /// </summary>
    [STAThread]
    private static void Main() {
        ServiceCollection services = new();
        services.AddSingleton<ISettingsService<FlaUiAppSettings>>(_ => new JsonSettingsService<FlaUiAppSettings>(Path.Combine(AppContext.BaseDirectory, "appsettings.json")));
        App.Services = services.BuildServiceProvider();

        ISettingsService<FlaUiAppSettings> settingsService = App.Services.GetRequiredService<ISettingsService<FlaUiAppSettings>>();
        FlaUiAppSettings settings = settingsService.Load();
        App.ApplyAppOption(settings);






        var showLLamaCppLogs = true;
        NativeLibraryConfig
           .All
           .WithLogCallback((level, message) =>
           {
               if (showLLamaCppLogs)
                   Console.WriteLine($"[llama {level}]: {message.TrimEnd('\n')}");
           });

        // Configure native library to use. This must be done before any other llama.cpp methods are called!
        NativeLibraryConfig
           .All
           .WithCuda(false)
           .WithVulkan(false);

        // Calling this method forces loading to occur now.
        NativeApi.llama_empty_call();











        ApplicationConfiguration.Initialize();
        Application.Run(new StartupForm(App.Logger));
    }
}
