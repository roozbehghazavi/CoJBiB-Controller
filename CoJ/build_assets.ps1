# Builds the single-exe asset payload (assets.zip) that gets embedded in the exe.
# Zips the loose files the app reads at runtime:
#   images\        -> images/...       (character/weapon art, backgrounds, western planks)
#   bin\Debug\bin\ -> bin/...          (modscoj.dll + Net_* class template files)
#   CoJ Mods.chm   -> CoJ Mods.chm     (help file opened by the Help / F1 buttons)
# Called from the .vbproj before compilation. -out is the target zip path.
param([Parameter(Mandatory = $true)][string]$out)

$ErrorActionPreference = "Stop"
$root   = Split-Path -Parent $MyInvocation.MyCommand.Path
$images = Join-Path $root "images"
$bin    = Join-Path $root "bin\Debug\bin"
$chm    = Join-Path $root "CoJ Mods.chm"

$outDir = Split-Path -Parent $out
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Force -Path $outDir | Out-Null }
if (Test-Path $out) { Remove-Item -Force $out }

$paths = @()
if (Test-Path $images) { $paths += $images }
if (Test-Path $bin)    { $paths += $bin }
if (Test-Path $chm)    { $paths += $chm }

if ($paths.Count -eq 0) {
    Write-Host "build_assets: no images\ or bin\ found under $root; skipping payload."
    exit 0
}

Compress-Archive -Path $paths -DestinationPath $out -Force
$size = [math]::Round((Get-Item $out).Length / 1MB, 2)
Write-Host "build_assets: wrote $out ($size MB) from: $($paths -join ', ')"
