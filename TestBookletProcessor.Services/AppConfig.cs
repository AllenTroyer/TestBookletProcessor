using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using TestBookletProcessor.Core.Models;

namespace TestBookletProcessor.Services;

/// <summary>
/// All application settings loaded from appsettings.json.
/// </summary>
public class AppOptions
{
    public BookletProcessorOptions BookletProcessor { get; set; } = new();
    public LoggingServiceConfig Logging { get; set; } = new();
}

/// <summary>
/// Single source of truth for locating and loading appsettings.json.
/// The file always lives next to the executable (not the current working directory,
/// which varies with how the app is launched).
/// </summary>
public static class AppConfig
{
    public static string ConfigFilePath => Path.Combine(AppContext.BaseDirectory, "appsettings.json");

    public static AppOptions Load()
    {
        var root = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .Build();

        var options = new AppOptions();
        root.GetSection("BookletProcessor").Bind(options.BookletProcessor);
        root.GetSection("Logging").Bind(options.Logging);

        // The binder appends configured list items onto in-code defaults, so the option
        // classes declare empty lists and the historical defaults are applied here only
        // when the section is absent. An explicitly empty list in config stays empty.
        var bp = options.BookletProcessor;
        if (!root.GetSection("BookletProcessor:QrScanner:QrValuesExcludingRedRemoval").Exists())
            bp.QrScanner.QrValuesExcludingRedRemoval = new List<string> { "MACHINE_SCORED", "NO_RED_INK", "CLEAN" };
        if (!root.GetSection("BookletProcessor:TemplateExclusionPatterns").Exists())
            bp.TemplateExclusionPatterns = new List<string> { "*TEMPLATE*", "*BLANK*", "*SAMPLE*" };

        return options;
    }
}
