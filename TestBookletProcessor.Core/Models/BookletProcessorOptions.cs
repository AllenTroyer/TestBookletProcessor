using System.Collections.Generic;

namespace TestBookletProcessor.Core.Models;

/// <summary>
/// Settings bound from the "BookletProcessor" section of appsettings.json.
/// List properties default to empty so the configuration binder does not append
/// configured values onto in-code defaults; missing-section defaults are applied
/// by the configuration loader.
/// </summary>
public class BookletProcessorOptions
{
    public int MaxConcurrency { get; set; } = 4;
    public string DefaultInputFolder { get; set; } = "";
    public string DefaultTemplateFolder { get; set; } = "";
    public string DefaultOutputFolder { get; set; } = "";
    public int DefaultDpi { get; set; } = 300;
    public byte RedPixelThreshold { get; set; } = 200;
    public bool EnableRedPixelRemover { get; set; }
    public QrScannerOptions QrScanner { get; set; } = new();
    public List<string> TemplateExclusionPatterns { get; set; } = new();
    public ScannedSheetOptions ScannedSheets { get; set; } = new();
    public List<RedPixelExclusionRegion> RedPixelExclusionRegions { get; set; } = new();
}

/// <summary>
/// QR scanning settings ("BookletProcessor:QrScanner").
/// </summary>
public class QrScannerOptions
{
    public bool EnableQrScanning { get; set; }
    public double QrRegionXInches { get; set; } = 6.5;
    public double QrRegionYInches { get; set; } = 9.0;
    public double QrRegionWidthInches { get; set; } = 2.0;
    public double QrRegionHeightInches { get; set; } = 2.0;
    public List<string> QrValuesExcludingRedRemoval { get; set; } = new();
}

/// <summary>
/// Scanned sheet mode settings ("BookletProcessor:ScannedSheets").
/// </summary>
public class ScannedSheetOptions
{
    /// <summary>
    /// Template filename that activates scanned sheet mode. Empty disables the mode.
    /// </summary>
    public string TemplateName { get; set; } = "";

    /// <summary>
    /// Maps QR code patterns (wildcards supported) to zero-based template page indices.
    /// </summary>
    public Dictionary<string, int> QrToPageMapping { get; set; } = new();

    /// <summary>
    /// Null when the config section is absent, which disables secondary QR scanning.
    /// </summary>
    public SecondaryQrScanConfig? SecondaryQrScan { get; set; }

    /// <summary>
    /// Null when the config section is absent, which disables raw form extraction.
    /// </summary>
    public RawFormExtractionConfig? RawFormExtraction { get; set; }
}
