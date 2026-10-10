<#
  Publishes Islet as a self-contained single-file executable.
  Usage:
    pwsh scripts/publish.ps1              # both win-x64 and win-arm64
    pwsh scripts/publish.ps1 -Rid win-x64
#>
param([string[]]$Rid = @('win-x64', 'win-arm64'))

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$proj = Join-Path $root 'src/Islet/Islet.csproj'

foreach ($r in $Rid) {
    Write-Host "== Publishing $r ==" -ForegroundColor Cyan
    dotnet publish $proj -c Release -r $r --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -o (Join-Path $root "dist/$r")
}
Write-Host "Done. Binaries in dist/<rid>/Islet.exe" -ForegroundColor Green
