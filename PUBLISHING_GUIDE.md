# Publishing Test Booklet Processor - Complete Guide

## Overview
This guide covers how to publish the Test Booklet Processor WPF application to a zipped folder with proper versioning.

## Version Numbering

### Version Format
The application uses **Semantic Versioning** (SemVer): `MAJOR.MINOR.PATCH`

- **MAJOR**: Breaking changes, major new features
- **MINOR**: New features, backward compatible
- **PATCH**: Bug fixes, small improvements

**Example**: `1.2.3`
- Major version: 1
- Minor version: 2
- Patch version: 3

### Version Properties in .csproj

The project file includes four version properties:

```xml
<Version>1.0.0</Version>
<AssemblyVersion>1.0.0.0</AssemblyVersion>
<FileVersion>1.0.0.0</FileVersion>
<InformationalVersion>1.0.0</InformationalVersion>
```

**Property Descriptions:**

| Property | Purpose | Format | Example |
|----------|---------|--------|---------|
| `Version` | NuGet package version | `X.Y.Z` | `1.0.0` |
| `AssemblyVersion` | .NET assembly identity | `X.Y.Z.W` | `1.0.0.0` |
| `FileVersion` | Windows file version | `X.Y.Z.W` | `1.0.0.0` |
| `InformationalVersion` | Display version | `X.Y.Z[-suffix]` | `1.0.0` or `1.0.0-beta` |

### When to Update Versions

**Major Version (1.0.0 ? 2.0.0)**
- Breaking changes to configuration format
- Major UI redesign
- Removal of features
- Changed behavior that breaks existing workflows

**Minor Version (1.0.0 ? 1.1.0)**
- New features (QR scanning, new settings)
- New configuration options
- Performance improvements
- New functionality that's backward compatible

**Patch Version (1.0.0 ? 1.0.1)**
- Bug fixes
- UI tweaks
- Documentation updates
- Small improvements

### Updating Version Numbers

**Option 1: Edit .csproj File Directly**

Open `TestBookletProcessor.WPF\TestBookletProcessor.WPF.csproj` and update:

```xml
<Version>1.1.0</Version>
<AssemblyVersion>1.1.0.0</AssemblyVersion>
<FileVersion>1.1.0.0</FileVersion>
<InformationalVersion>1.1.0</InformationalVersion>
```

**Option 2: Use Visual Studio Properties**
1. Right-click project ? Properties
2. Go to Package tab
3. Update version numbers
4. Save

**Option 3: Command Line with MSBuild**
```powershell
dotnet build /p:Version=1.1.0 /p:FileVersion=1.1.0.0
```

## Publishing Methods

### Method 1: Visual Studio Publish (Recommended)

#### Step 1: Configure Publish Profile

1. **Right-click** `TestBookletProcessor.WPF` project
2. Select **Publish**
3. Click **New** to create new profile
4. Choose **Folder** as target
5. Set folder path: `bin\Release\net8.0-windows10.0.17763.0\win-x64\publish`
6. Click **Finish**

#### Step 2: Configure Settings

In the publish profile:
- **Configuration**: Release
- **Target Framework**: net8.0-windows10.0.17763.0
- **Deployment Mode**: Self-contained
- **Target Runtime**: win-x64
- **File publish options**:
  - ? Produce single file
  - ? Enable ReadyToRun compilation (optional)
  - ? Trim unused assemblies (be careful with WPF)

#### Step 3: Publish

1. Click **Publish** button
2. Wait for build and publish to complete
3. Files will be in the publish folder

#### Step 4: Create Zip File

**PowerShell Script** (save as `CreateRelease.ps1`):

```powershell
# CreateRelease.ps1
param(
    [string]$Version = "1.0.0"
)

$publishFolder = "bin\Release\net8.0-windows10.0.17763.0\win-x64\publish"
$outputFolder = "releases"
$zipName = "TestBookletProcessor_v${Version}_win-x64.zip"

# Ensure output folder exists
New-Item -ItemType Directory -Force -Path $outputFolder | Out-Null

# Create zip file
$zipPath = Join-Path $outputFolder $zipName
Compress-Archive -Path "$publishFolder\*" -DestinationPath $zipPath -Force

Write-Host "Release created: $zipPath" -ForegroundColor Green
```

**Usage:**
```powershell
.\CreateRelease.ps1 -Version "1.0.0"
```

### Method 2: Command Line (dotnet CLI)

#### Single Command Publish

```powershell
dotnet publish TestBookletProcessor.WPF\TestBookletProcessor.WPF.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=false `
  -p:Version=1.0.0 `
  -o publish\TestBookletProcessor_v1.0.0
```

**Parameters:**
- `-c Release`: Build in Release configuration
- `-r win-x64`: Target Windows 64-bit
- `--self-contained true`: Include .NET runtime
- `-p:PublishSingleFile=false`: Keep files separate (better for WPF)
- `-p:Version=1.0.0`: Set version number
- `-o`: Output directory

#### Create Zip After Publishing

```powershell
$version = "1.0.0"
$publishDir = "publish\TestBookletProcessor_v$version"
$zipFile = "releases\TestBookletProcessor_v${version}_win-x64.zip"

Compress-Archive -Path "$publishDir\*" -DestinationPath $zipFile -Force
```

### Method 3: Automated Build Script

Create `build-and-package.ps1`:

```powershell
# build-and-package.ps1
param(
    [Parameter(Mandatory=$true)]
    [string]$Version,
    
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

Write-Host "Building Test Booklet Processor v$Version" -ForegroundColor Cyan

# Clean previous builds
Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
dotnet clean TestBookletProcessor.WPF\TestBookletProcessor.WPF.csproj -c $Configuration

# Build
Write-Host "Building..." -ForegroundColor Yellow
dotnet build TestBookletProcessor.WPF\TestBookletProcessor.WPF.csproj `
  -c $Configuration `
  -p:Version=$Version

# Publish
Write-Host "Publishing..." -ForegroundColor Yellow
$publishPath = "publish\TestBookletProcessor_v$Version"
dotnet publish TestBookletProcessor.WPF\TestBookletProcessor.WPF.csproj `
  -c $Configuration `
  -r $Runtime `
  --self-contained true `
  -p:Version=$Version `
  -p:PublishSingleFile=false `
  -o $publishPath

# Create releases folder
$releasesFolder = "releases"
New-Item -ItemType Directory -Force -Path $releasesFolder | Out-Null

# Create zip file
Write-Host "Creating release package..." -ForegroundColor Yellow
$zipName = "TestBookletProcessor_v${Version}_${Runtime}.zip"
$zipPath = Join-Path $releasesFolder $zipName
Compress-Archive -Path "$publishPath\*" -DestinationPath $zipPath -Force

# Create version info file
$versionInfo = @"
Test Booklet Processor
Version: $Version
Build Date: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
Runtime: $Runtime
Configuration: $Configuration
"@

$versionInfoPath = Join-Path $releasesFolder "version_$Version.txt"
$versionInfo | Out-File -FilePath $versionInfoPath -Encoding UTF8

Write-Host "`nRelease created successfully!" -ForegroundColor Green
Write-Host "Package: $zipPath" -ForegroundColor Green
Write-Host "Version Info: $versionInfoPath" -ForegroundColor Green

# Display file size
$zipSize = (Get-Item $zipPath).Length / 1MB
Write-Host "Package Size: $($zipSize.ToString("F2")) MB" -ForegroundColor Cyan
```

**Usage:**
```powershell
.\build-and-package.ps1 -Version "1.0.0"
.\build-and-package.ps1 -Version "1.1.0" -Runtime "win-x64"
```

## Publishing Configuration Options

### Self-Contained vs Framework-Dependent

**Self-Contained (Recommended)**
```xml
<SelfContained>true</SelfContained>
```
- **Pros**: Works without .NET installed, isolated runtime
- **Cons**: Larger file size (~150-200 MB)
- **Use When**: Deploying to end users

**Framework-Dependent**
```xml
<SelfContained>false</SelfContained>
```
- **Pros**: Smaller file size (~5-10 MB)
- **Cons**: Requires .NET 8 runtime installed
- **Use When**: Deploying to controlled environments

### Single File vs Multiple Files

**Multiple Files (Recommended for WPF)**
```xml
<PublishSingleFile>false</PublishSingleFile>
```
- Better for WPF applications
- Easier to troubleshoot
- Allows config file updates

**Single File**
```xml
<PublishSingleFile>true</PublishSingleFile>
```
- Simpler distribution
- May have issues with WPF resources
- Larger initial file

### ReadyToRun (R2R) Compilation

```xml
<PublishReadyToRun>true</PublishReadyToRun>
```
- Faster startup time
- Slightly larger file size
- Recommended for production

## Release Folder Structure

Recommended folder structure:

```
TestBookletProcessor/
??? releases/
?   ??? TestBookletProcessor_v1.0.0_win-x64.zip
?   ??? TestBookletProcessor_v1.0.1_win-x64.zip
?   ??? TestBookletProcessor_v1.1.0_win-x64.zip
?   ??? version_1.0.0.txt
?   ??? version_1.0.1.txt
?   ??? version_1.1.0.txt
??? publish/
?   ??? (temporary publish output)
??? build-and-package.ps1
```

## Zip File Contents

A typical release zip should contain:

```
TestBookletProcessor_v1.0.0_win-x64.zip
??? TestBookletProcessor.WPF.exe           (Main executable)
??? TestBookletProcessor.Core.dll
??? TestBookletProcessor.Services.dll
??? QrRegionScanner.dll
??? appsettings.json                       (Configuration)
??? *.dll                                  (Dependencies)
??? *.json                                 (Runtime config)
??? runtimes/                              (Native libraries)
    ??? win-x64/
```

## Version Display in Application

### Option 1: Add Version to Title Bar

Update `MainWindow.xaml.cs` constructor:

```csharp
public MainWindow()
{
    InitializeComponent();
    
    // Get version from assembly
    var version = System.Reflection.Assembly.GetExecutingAssembly()
        .GetName().Version;
    
    // Update window title
    this.Title = $"Test Booklet Processor - v{version?.ToString(3)}";
}
```

### Option 2: Add About Dialog

Create `AboutWindow.xaml`:

```xml
<Window x:Class="TestBookletProcessor.WPF.AboutWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="About" Height="250" Width="400" 
        WindowStartupLocation="CenterOwner" ResizeMode="NoResize">
    <Grid Margin="20">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>
        
        <TextBlock Grid.Row="0" Text="Test Booklet Processor" 
                   FontSize="20" FontWeight="Bold" Margin="0,0,0,10"/>
        <TextBlock Grid.Row="1" x:Name="VersionText" 
                   FontSize="14" Margin="0,0,0,10"/>
        <TextBlock Grid.Row="2" Text="Copyright © 2024" 
                   Margin="0,0,0,10"/>
        <TextBlock Grid.Row="3" TextWrapping="Wrap"
                   Text="Test booklet processing application with QR code scanning and red pixel removal capabilities."/>
        <Button Grid.Row="4" Content="Close" Width="80" 
                HorizontalAlignment="Right" Click="Close_Click"/>
    </Grid>
</Window>
```

`AboutWindow.xaml.cs`:

```csharp
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        
        var version = System.Reflection.Assembly.GetExecutingAssembly()
            .GetName().Version;
        var infoVersion = System.Reflection.Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
            
        VersionText.Text = $"Version {infoVersion ?? version?.ToString(3)}";
    }
    
    private void Close_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }
}
```

## Release Checklist

Before creating a release:

- [ ] Update version numbers in .csproj
- [ ] Update CHANGELOG.md (if you have one)
- [ ] Test the application thoroughly
- [ ] Update documentation
- [ ] Clean solution
- [ ] Build in Release mode
- [ ] Test the published version
- [ ] Create zip file
- [ ] Create release notes
- [ ] Tag version in Git
- [ ] Push to repository

## Git Tagging for Releases

```bash
# Create annotated tag
git tag -a v1.0.0 -m "Release version 1.0.0"

# Push tag to remote
git push origin v1.0.0

# List all tags
git tag -l

# Delete tag (if needed)
git tag -d v1.0.0
git push origin --delete v1.0.0
```

## GitHub Releases (If Using GitHub)

1. Go to repository on GitHub
2. Click "Releases" ? "Create a new release"
3. Choose tag: `v1.0.0`
4. Release title: `Test Booklet Processor v1.0.0`
5. Upload the zip file
6. Add release notes
7. Publish release

## Continuous Integration (Optional)

If you want to automate releases with GitHub Actions:

Create `.github/workflows/release.yml`:

```yaml
name: Create Release

on:
  push:
    tags:
      - 'v*'

jobs:
  build:
    runs-on: windows-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: 8.0.x
    
    - name: Get version from tag
      id: get_version
      run: echo "VERSION=${GITHUB_REF#refs/tags/v}" >> $GITHUB_OUTPUT
    
    - name: Build and Publish
      run: |
        dotnet publish TestBookletProcessor.WPF/TestBookletProcessor.WPF.csproj `
          -c Release `
          -r win-x64 `
          --self-contained true `
          -p:Version=${{ steps.get_version.outputs.VERSION }} `
          -o publish
    
    - name: Create Zip
      run: |
        Compress-Archive -Path publish\* `
          -DestinationPath TestBookletProcessor_v${{ steps.get_version.outputs.VERSION }}_win-x64.zip
    
    - name: Create Release
      uses: softprops/action-gh-release@v1
      with:
        files: TestBookletProcessor_v${{ steps.get_version.outputs.VERSION }}_win-x64.zip
      env:
        GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
```

## Quick Reference

### Update Version and Create Release

```powershell
# 1. Update version in .csproj file
# Edit: TestBookletProcessor.WPF\TestBookletProcessor.WPF.csproj
# Change: <Version>1.0.0</Version> to <Version>1.1.0</Version>

# 2. Build and package
.\build-and-package.ps1 -Version "1.1.0"

# 3. Commit and tag
git add .
git commit -m "Release v1.1.0"
git tag -a v1.1.0 -m "Release version 1.1.0"
git push origin main --tags

# 4. Distribute
# Upload releases/TestBookletProcessor_v1.1.0_win-x64.zip
```

## Troubleshooting

### Issue: Zip file is very large
**Solution**: This is normal for self-contained apps. Typical size is 150-200 MB.

### Issue: Application won't start after publishing
**Solution**: 
- Check if all dependencies are included
- Verify appsettings.json is in the output
- Check for missing native libraries

### Issue: Version not showing correctly
**Solution**: 
- Rebuild the project after changing version
- Clean solution before building
- Check if version properties are correct in .csproj

### Issue: ReadyToRun errors
**Solution**: 
- Try disabling ReadyToRun: `<PublishReadyToRun>false</PublishReadyToRun>`
- Some WPF apps have issues with R2R

## Summary

**Simple Workflow:**

1. **Update Version**: Edit .csproj ? Change `<Version>1.0.0</Version>`
2. **Publish**: `dotnet publish -c Release -r win-x64 --self-contained true`
3. **Create Zip**: `Compress-Archive -Path publish\* -DestinationPath release.zip`
4. **Distribute**: Upload zip file

**Professional Workflow:**

1. Use the `build-and-package.ps1` script
2. Tag releases in Git
3. Create GitHub releases
4. Maintain version history
5. Include release notes

Your application is now ready for versioned releases!
