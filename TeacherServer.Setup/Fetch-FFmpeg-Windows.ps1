param(
    [string]$OutputDirectory
)

$ErrorActionPreference = "Stop"

if (-not $OutputDirectory) {
    throw "OutputDirectory is required."
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$zipPath = Join-Path $OutputDirectory "ffmpeg-shared.zip"
$extractDir = Join-Path $OutputDirectory "extract"

if (Test-Path $extractDir) {
    Remove-Item -Recurse -Force $extractDir
}

New-Item -ItemType Directory -Force -Path $extractDir | Out-Null

# Shared build includes the required DLLs (avcodec/avformat/avutil/swscale/...).
# Prefer a stable GitHub-hosted shared ZIP; override via env var if needed.
$url = $env:CLASSCOMMANDER_FFMPEG_WINDOWS_SHARED_ZIP_URL
if (-not $url) {
    # Source: https://github.com/BtbN/FFmpeg-Builds/releases
    $url = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl-shared.zip"
}

Write-Host "Downloading FFmpeg shared build from $url"
Invoke-WebRequest -Uri $url -OutFile $zipPath

Write-Host "Extracting FFmpeg..."
Expand-Archive -Path $zipPath -DestinationPath $extractDir -Force

$ffmpegRoot = Get-ChildItem -Path $extractDir | Where-Object { $_.PSIsContainer } | Select-Object -First 1
if (-not $ffmpegRoot) {
    throw "FFmpeg archive extraction failed: no root folder found."
}

$binDir = Join-Path $ffmpegRoot.FullName "bin"
if (-not (Test-Path $binDir)) {
    throw "FFmpeg archive extraction failed: bin directory not found."
}

$outBin = Join-Path $OutputDirectory "bin"
New-Item -ItemType Directory -Force -Path $outBin | Out-Null

Copy-Item -Force -Path (Join-Path $binDir "*.dll") -Destination $outBin

Write-Host "FFmpeg DLLs staged at $outBin"

