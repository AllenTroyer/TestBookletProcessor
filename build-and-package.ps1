# build-and-package.ps1
# Script to build and package Test Booklet Processor for release

param(
    [Parameter(Mandatory=$true, HelpMessage="Version number (e.g., 1.0.0)")]
    [string]$Version,
    
    [Parameter(HelpMessage="Build configuration")]
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",
    
    [Parameter(HelpMessage="Target runtime")]
    [ValidateSet("win-x64", "win-x86", "win-arm64")]
    [string]$Runtime = "win-x64",
    
    [Parameter(HelpMessage="Skip tests")]
    [switch]$SkipTests,
    
    [Parameter(HelpMessage="Open releases folder after completion")]
    [switch]$OpenFolder
)

$ErrorActionPreference = "Stop"
$projectPath = "TestBookletProcessor.WPF\TestBookletProcessor.WPF.csproj"

# Display banner
Write-Host ""
Write-Host "???????????????????????????????????????????????????????" -ForegroundColor Cyan
Write-Host "   Test Booklet Processor - Build & Package Script     " -ForegroundColor Cyan
Write-Host "???????????????????????????????????????????????????????" -ForegroundColor Cyan
Write-Host ""
Write-Host "Version:       $Version" -ForegroundColor White
Write-Host "Configuration: $Configuration" -ForegroundColor White
Write-Host "Runtime:       $Runtime" -ForegroundColor White
Write-Host ""

# Validate project file exists
if (-not (Test-Path $projectPath)) {
    Write-Host "Error: Project file not found: $projectPath" -ForegroundColor Red
    exit 1
}

try {
    # Step 1: Clean previous builds
    Write-Host "[1/6] Cleaning previous builds..." -ForegroundColor Yellow
    dotnet clean $projectPath -c $Configuration -v quiet
    if ($LASTEXITCODE -ne 0) { throw "Clean failed" }
    Write-Host "      ? Clean completed" -ForegroundColor Green
    
    # Step 2: Restore NuGet packages
    Write-Host "[2/6] Restoring NuGet packages..." -ForegroundColor Yellow
    dotnet restore $projectPath -v quiet
    if ($LASTEXITCODE -ne 0) { throw "Restore failed" }
    Write-Host "      ? Restore completed" -ForegroundColor Green
    
    # Step 3: Build
    Write-Host "[3/6] Building project..." -ForegroundColor Yellow
    dotnet build $projectPath `
        -c $Configuration `
        -p:Version=$Version `
        --no-restore `
        -v quiet
    if ($LASTEXITCODE -ne 0) { throw "Build failed" }
    Write-Host "      ? Build completed" -ForegroundColor Green
    
    # Step 4: Run tests (optional)
    if (-not $SkipTests) {
        Write-Host "[4/6] Running tests..." -ForegroundColor Yellow
        # Add test project path if you have tests
        # dotnet test YourTestProject.csproj --no-build -c $Configuration
        Write-Host "      ? No tests configured (use -SkipTests to skip this message)" -ForegroundColor Gray
    } else {
        Write-Host "[4/6] Skipping tests..." -ForegroundColor Gray
    }
    
    # Step 5: Publish
    Write-Host "[5/6] Publishing application..." -ForegroundColor Yellow
    $publishPath = "publish\TestBookletProcessor_v$Version"
    
    dotnet publish $projectPath `
        -c $Configuration `
        -r $Runtime `
        --self-contained true `
        -p:Version=$Version `
        -p:PublishSingleFile=false `
        -p:PublishReadyToRun=true `
        -p:IncludeNativeLibrariesForSelfContained=true `
        -o $publishPath `
        --no-build `
        -v quiet
    
    if ($LASTEXITCODE -ne 0) { throw "Publish failed" }
    Write-Host "      ? Publish completed" -ForegroundColor Green
    
    # Step 6: Create release package
    Write-Host "[6/6] Creating release package..." -ForegroundColor Yellow
    
    # Create releases folder
    $releasesFolder = "releases"
    New-Item -ItemType Directory -Force -Path $releasesFolder | Out-Null
    
    # Create zip file
    $zipName = "TestBookletProcessor_v${Version}_${Runtime}.zip"
    $zipPath = Join-Path $releasesFolder $zipName
    
    # Remove existing zip if present
    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }
    
    Compress-Archive -Path "$publishPath\*" -DestinationPath $zipPath -CompressionLevel Optimal
    Write-Host "      ? Package created" -ForegroundColor Green
    
    # Create version info file
    $buildDate = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $versionInfo = @"
Test Booklet Processor
???????????????????????????????????????????????????????

Version:       $Version
Build Date:    $buildDate
Configuration: $Configuration
Runtime:       $Runtime

Features:
- PDF booklet processing
- QR code scanning with wildcard patterns
- Red pixel removal with conditional logic
- DPI-independent configuration
- Inch-based QR region positioning

System Requirements:
- Windows 10 or later (64-bit)
- No .NET runtime required (self-contained)

Installation:
1. Extract all files from zip
2. Run TestBookletProcessor.WPF.exe
3. Configure settings through Settings window
4. Edit appsettings.json for advanced options

Support:
- Documentation: See included markdown files
- GitHub: https://github.com/AllenTroyer/TestBookletProcessor
"@
    
    $versionInfoPath = Join-Path $releasesFolder "ReleaseNotes_v$Version.txt"
    $versionInfo | Out-File -FilePath $versionInfoPath -Encoding UTF8
    
    # Get file sizes
    $zipSize = (Get-Item $zipPath).Length / 1MB
    $publishSize = (Get-ChildItem -Path $publishPath -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB
    
    # Display summary
    Write-Host ""
    Write-Host "???????????????????????????????????????????????????????" -ForegroundColor Green
    Write-Host "   Build Completed Successfully!                       " -ForegroundColor Green
    Write-Host "???????????????????????????????????????????????????????" -ForegroundColor Green
    Write-Host ""
    Write-Host "Release Package:" -ForegroundColor Cyan
    Write-Host "  Location:      $zipPath" -ForegroundColor White
    Write-Host "  Size:          $($zipSize.ToString("F2")) MB (compressed)" -ForegroundColor White
    Write-Host "  Unpacked Size: $($publishSize.ToString("F2")) MB" -ForegroundColor White
    Write-Host ""
    Write-Host "Version Info:" -ForegroundColor Cyan
    Write-Host "  Location: $versionInfoPath" -ForegroundColor White
    Write-Host ""
    Write-Host "Next Steps:" -ForegroundColor Cyan
    Write-Host "  1. Test the application from: $publishPath" -ForegroundColor White
    Write-Host "  2. Review release notes" -ForegroundColor White
    Write-Host "  3. Distribute the zip file" -ForegroundColor White
    Write-Host "  4. Tag in Git: git tag -a v$Version -m 'Release v$Version'" -ForegroundColor White
    Write-Host ""
    
    # Open folder if requested
    if ($OpenFolder) {
        Start-Process explorer.exe (Resolve-Path $releasesFolder)
    }
    
    # Return success
    exit 0
}
catch {
    Write-Host ""
    Write-Host "???????????????????????????????????????????????????????" -ForegroundColor Red
    Write-Host "   Build Failed!                                       " -ForegroundColor Red
    Write-Host "???????????????????????????????????????????????????????" -ForegroundColor Red
    Write-Host ""
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Stack Trace:" -ForegroundColor Yellow
    Write-Host $_.ScriptStackTrace -ForegroundColor Gray
    Write-Host ""
    exit 1
}
