# Builds the single-exe asset payload embedded in the exe.
# Bundles the loose files the app reads at runtime:
#   images\        -> images/...       (character/weapon art, backgrounds, western planks)
#   bin\Debug\bin\ -> bin/...          (modscoj.dll + Net_* class template files)
#   CoJ Mods.chm   -> CoJ Mods.chm     (help file opened by the Help / F1 buttons)
#
# Uses a simple UNCOMPRESSED container (not zip) so extraction needs only
# System.IO at runtime - this keeps the app runnable on plain .NET Framework 4.0
# (e.g. under Wine with dotnet40), which lacks System.IO.Compression (ZipArchive).
# The art and help file are already-compressed formats, so zip would not shrink
# them meaningfully anyway.
#
# Container format (little-endian, repeated until end of stream):
#   Int32  path length (bytes)
#   bytes  relative path, UTF-8, forward slashes
#   Int64  data length
#   bytes  file data
param([Parameter(Mandatory = $true)][string]$out)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$entries = New-Object System.Collections.Generic.List[object]

function Collect($dir, $prefix) {
    if (Test-Path $dir) {
        $base = (Resolve-Path $dir).Path
        Get-ChildItem -LiteralPath $dir -Recurse -File | ForEach-Object {
            $rel = $prefix + "/" + $_.FullName.Substring($base.Length).TrimStart('\', '/').Replace('\', '/')
            $entries.Add([pscustomobject]@{ Rel = $rel; Full = $_.FullName })
        }
    }
}

Collect (Join-Path $root "images") "images"
Collect (Join-Path $root "bin\Debug\bin") "bin"
$chm = Join-Path $root "CoJ Mods.chm"
if (Test-Path $chm) { $entries.Add([pscustomobject]@{ Rel = "CoJ Mods.chm"; Full = $chm }) }
# In-process admin mod. Packed under mod/ (NOT the payload root) on purpose: it is
# extracted to <exe dir>\mod\version.dll, a subfolder that is NOT on the controller's
# DLL search path, so a WINEDLLOVERRIDES=version=n,b never loads the game mod into the
# controller itself. FrmMain copies it from there into the dedicated-server folder,
# where the server's own directory IS searched, so the server loads it.
$vdll = Join-Path $root "version.dll"
if (Test-Path $vdll) { $entries.Add([pscustomobject]@{ Rel = "mod/version.dll"; Full = $vdll }) }

$outDir = Split-Path -Parent $out
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Force -Path $outDir | Out-Null }
if (Test-Path $out) { Remove-Item -Force $out }

if ($entries.Count -eq 0) {
    Write-Host "build_assets: no images\ or bin\ found under $root; skipping payload."
    New-Item -ItemType File -Path $out | Out-Null
    exit 0
}

$fs = [System.IO.File]::Create($out)
$bw = New-Object System.IO.BinaryWriter($fs)
try {
    $enc = [System.Text.Encoding]::UTF8
    foreach ($e in $entries) {
        $pathBytes = $enc.GetBytes($e.Rel)
        $data = [System.IO.File]::ReadAllBytes($e.Full)
        $bw.Write([int]$pathBytes.Length)
        $bw.Write($pathBytes)
        $bw.Write([long]$data.LongLength)
        $bw.Write($data)
    }
}
finally {
    $bw.Close()
    $fs.Close()
}

$size = [math]::Round((Get-Item $out).Length / 1MB, 2)
Write-Host "build_assets: wrote $out ($size MB, $($entries.Count) files)"
