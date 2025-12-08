# CreateRelease.ps1
# Quick script to create a release package

param(
    [Parameter(Mandatory=$true)]
    [string]$Version
)

Write-Host "Creating Test Booklet Processor v$Version..." -ForegroundColor Cyan

# Use the main build script
& .\build-and-package.ps1 -Version $Version -OpenFolder

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nRelease v$Version created successfully!" -ForegroundColor Green
    Write-Host "Don't forget to:" -ForegroundColor Yellow
    Write-Host "  • Test the release" -ForegroundColor White
    Write-Host "  • Update CHANGELOG" -ForegroundColor White
    Write-Host "  • Commit changes" -ForegroundColor White
    Write-Host "  • Create Git tag: git tag -a v$Version -m 'Release v$Version'" -ForegroundColor White
    Write-Host "  • Push: git push origin main --tags" -ForegroundColor White
}
