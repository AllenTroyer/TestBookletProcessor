# Quick Reference - Publishing & Versioning

## Update Version Number

Edit `TestBookletProcessor.WPF\TestBookletProcessor.WPF.csproj`:

```xml
<Version>1.0.0</Version>              <!-- Change this -->
<AssemblyVersion>1.0.0.0</AssemblyVersion>
<FileVersion>1.0.0.0</FileVersion>
```

## Create Release Package

### Method 1: Simple (Recommended)
```powershell
.\CreateRelease.ps1 -Version "1.0.0"
```

### Method 2: Full Control
```powershell
.\build-and-package.ps1 -Version "1.0.0" -OpenFolder
```

### Method 3: Manual
```powershell
# Publish
dotnet publish TestBookletProcessor.WPF\TestBookletProcessor.WPF.csproj -c Release -r win-x64 --self-contained true -o publish

# Create Zip
Compress-Archive -Path publish\* -DestinationPath releases\TestBookletProcessor_v1.0.0.zip
```

## Git Workflow

```bash
# 1. Commit changes
git add .
git commit -m "Release v1.0.0"

# 2. Create tag
git tag -a v1.0.0 -m "Release version 1.0.0"

# 3. Push everything
git push origin main --tags
```

## Version Number Guide

| Change Type | Old ? New | Example |
|-------------|-----------|---------|
| Bug fix | 1.0.0 ? 1.0.1 | Fix QR scanning bug |
| New feature | 1.0.0 ? 1.1.0 | Add new settings |
| Breaking change | 1.0.0 ? 2.0.0 | Remove feature |

## Output Location

After running the script:
- **Zip file**: `releases\TestBookletProcessor_v1.0.0_win-x64.zip`
- **Unpacked**: `publish\TestBookletProcessor_v1.0.0\`
- **Release notes**: `releases\ReleaseNotes_v1.0.0.txt`

## File Size

Typical sizes:
- **Zipped**: ~100-150 MB (self-contained with .NET runtime)
- **Unpacked**: ~200-250 MB

## Distribution

Share the zip file from `releases\` folder with users.

Users should:
1. Extract all files
2. Run `TestBookletProcessor.WPF.exe`
3. No .NET installation required!

## Troubleshooting

**Script execution error?**
```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
```

**Build fails?**
```powershell
dotnet clean
dotnet restore
.\build-and-package.ps1 -Version "1.0.0"
```

**Need help?**
```powershell
Get-Help .\build-and-package.ps1 -Detailed
```

## Full Documentation

See `PUBLISHING_GUIDE.md` for complete details.
