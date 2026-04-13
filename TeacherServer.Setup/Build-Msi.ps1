param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputDirectory = (Join-Path $PSScriptRoot "dist"),
    [string]$OutputFileName = "ClassCommander.Setup.msi"
)

$root = Split-Path $PSScriptRoot -Parent
$artifactsDirectory = Join-Path $PSScriptRoot "artifacts"
$teacherPayloadDirectory = Join-Path $artifactsDirectory "Teacher"
$teacherAvaloniaPayloadDirectory = Join-Path $artifactsDirectory "TeacherAvalonia"
$studentPayloadDirectory = Join-Path $artifactsDirectory "Student"
$generatedDirectory = Join-Path $PSScriptRoot "Generated"

New-Item -ItemType Directory -Force -Path $teacherPayloadDirectory | Out-Null
New-Item -ItemType Directory -Force -Path $teacherAvaloniaPayloadDirectory | Out-Null
New-Item -ItemType Directory -Force -Path $studentPayloadDirectory | Out-Null
New-Item -ItemType Directory -Force -Path $generatedDirectory | Out-Null
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$teacherProject = Join-Path $root "TeacherClient\TeacherClient.csproj"
$teacherAvaloniaProject = Join-Path $root "TeacherClient.Avalonia\TeacherClient.Avalonia.csproj"
$servicePublishScript = Join-Path $root "StudentAgent.Service\Publish-ServiceBundle.ps1"
$fragmentGenerator = Join-Path $PSScriptRoot "Generate-WixFragment.ps1"
$installerProject = Join-Path $PSScriptRoot "TeacherServer.Setup.wixproj"

Write-Host "Publishing TeacherClient..."
dotnet publish $teacherProject `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -o $teacherPayloadDirectory

if ($LASTEXITCODE -ne 0) {
    throw "Publishing TeacherClient failed."
}

Write-Host "Publishing TeacherClient.Avalonia..."
dotnet publish $teacherAvaloniaProject `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -o $teacherAvaloniaPayloadDirectory

if ($LASTEXITCODE -ne 0) {
    throw "Publishing TeacherClient.Avalonia failed."
}

Write-Host "Publishing StudentAgent service bundle..."
& $servicePublishScript `
    -Configuration $Configuration `
    -Runtime $Runtime `
    -OutputDirectory $studentPayloadDirectory

if ($LASTEXITCODE -ne 0) {
    throw "Publishing StudentAgent service bundle failed."
}

Write-Host "Generating WiX payload fragments..."
& $fragmentGenerator `
    -SourceDirectory $teacherPayloadDirectory `
    -DirectoryRefId "TEACHERDIR" `
    -ComponentGroupId "TeacherPayloadGroup" `
    -OutputPath (Join-Path $generatedDirectory "TeacherPayload.wxs") `
    -ExcludeFiles @("TeacherClient.exe")

if ($LASTEXITCODE -ne 0) {
    throw "Generating teacher WiX fragment failed."
}

& $fragmentGenerator `
    -SourceDirectory $teacherAvaloniaPayloadDirectory `
    -DirectoryRefId "TEACHERAVALONIADIR" `
    -ComponentGroupId "TeacherAvaloniaPayloadGroup" `
    -OutputPath (Join-Path $generatedDirectory "TeacherAvaloniaPayload.wxs") `
    -ExcludeFiles @("TeacherClient.Avalonia.exe")

if ($LASTEXITCODE -ne 0) {
    throw "Generating teacher Avalonia WiX fragment failed."
}

& $fragmentGenerator `
    -SourceDirectory $studentPayloadDirectory `
    -DirectoryRefId "STUDENTDIR" `
    -ComponentGroupId "StudentPayloadGroup" `
    -OutputPath (Join-Path $generatedDirectory "StudentPayload.wxs") `
    -ExcludeFiles @("StudentAgent.Service.exe", "StudentAgent.UIHost.exe", "StudentAgent.VncHost.exe")

if ($LASTEXITCODE -ne 0) {
    throw "Generating student WiX fragment failed."
}

Write-Host "Building MSI..."
dotnet build $installerProject `
    -c $Configuration `
    -o $OutputDirectory

if ($LASTEXITCODE -ne 0) {
    throw "Building TeacherServer.Setup MSI failed."
}

$builtMsi = Get-ChildItem -Path $OutputDirectory -Filter "*.msi" | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
if (-not $builtMsi) {
    throw "MSI build completed but no .msi file was found in $OutputDirectory."
}

$finalMsiPath = Join-Path $OutputDirectory $OutputFileName
if ($builtMsi.FullName -ne $finalMsiPath) {
    Move-Item -Force -Path $builtMsi.FullName -Destination $finalMsiPath
}

Write-Host "MSI build completed. Output: $finalMsiPath"
