param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputDirectory = (Join-Path $PSScriptRoot "dist"),
    [string]$OutputFileName = "ClassCommander.Setup.msi"
)

$root = Split-Path $PSScriptRoot -Parent
$artifactsDirectory = Join-Path $PSScriptRoot "artifacts"
$teacherAvaloniaPayloadDirectory = Join-Path $artifactsDirectory "TeacherAvalonia"
$testEditorPayloadDirectory = Join-Path $artifactsDirectory "TestEditor"
$testPlatformPayloadDirectory = Join-Path $artifactsDirectory "TestPlatform"
$studentPayloadDirectory = Join-Path $artifactsDirectory "Student"
$generatedDirectory = Join-Path $PSScriptRoot "Generated"

New-Item -ItemType Directory -Force -Path $teacherAvaloniaPayloadDirectory | Out-Null
New-Item -ItemType Directory -Force -Path $testEditorPayloadDirectory | Out-Null
New-Item -ItemType Directory -Force -Path $testPlatformPayloadDirectory | Out-Null
New-Item -ItemType Directory -Force -Path $studentPayloadDirectory | Out-Null
New-Item -ItemType Directory -Force -Path $generatedDirectory | Out-Null
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$teacherAvaloniaProject = Join-Path $root "TeacherClient.Avalonia\TeacherClient.Avalonia.csproj"
$testEditorProject = Join-Path $root "ClassCommander.TestEditor\ClassCommander.TestEditor.csproj"
$testPlatformProject = Join-Path $root "ClassCommander.TestPlatform\ClassCommander.TestPlatform.csproj"
$servicePublishScript = Join-Path $root "StudentAgent.Service\Publish-ServiceBundle.ps1"
$fragmentGenerator = Join-Path $PSScriptRoot "Generate-WixFragment.ps1"
$installerProject = Join-Path $PSScriptRoot "TeacherServer.Setup.wixproj"

Write-Host "Publishing TeacherClient.Avalonia..."
dotnet publish $teacherAvaloniaProject `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -o $teacherAvaloniaPayloadDirectory

if ($LASTEXITCODE -ne 0) {
    throw "Publishing TeacherClient.Avalonia failed."
}

Write-Host "Publishing ClassCommander.TestEditor..."
dotnet publish $testEditorProject `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -o $testEditorPayloadDirectory

if ($LASTEXITCODE -ne 0) {
    throw "Publishing ClassCommander.TestEditor failed."
}

Write-Host "Publishing ClassCommander.TestPlatform..."
dotnet publish $testPlatformProject `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -o $testPlatformPayloadDirectory

if ($LASTEXITCODE -ne 0) {
    throw "Publishing ClassCommander.TestPlatform failed."
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
    -SourceDirectory $teacherAvaloniaPayloadDirectory `
    -DirectoryRefId "TEACHERAVALONIADIR" `
    -ComponentGroupId "TeacherAvaloniaPayloadGroup" `
    -OutputPath (Join-Path $generatedDirectory "TeacherAvaloniaPayload.wxs") `
    -ExcludeFiles @("TeacherClient.Avalonia.exe")

if ($LASTEXITCODE -ne 0) {
    throw "Generating teacher Avalonia WiX fragment failed."
}

& $fragmentGenerator `
    -SourceDirectory $testEditorPayloadDirectory `
    -DirectoryRefId "TESTEDITORDIR" `
    -ComponentGroupId "TestEditorPayloadGroup" `
    -OutputPath (Join-Path $generatedDirectory "TestEditorPayload.wxs")

if ($LASTEXITCODE -ne 0) {
    throw "Generating TestEditor WiX fragment failed."
}

& $fragmentGenerator `
    -SourceDirectory $testPlatformPayloadDirectory `
    -DirectoryRefId "TESTPLATFORMDIR" `
    -ComponentGroupId "TestPlatformPayloadGroup" `
    -OutputPath (Join-Path $generatedDirectory "TestPlatformPayload.wxs") `
    -ExcludeFiles @("ClassCommander.TestPlatform.exe")

if ($LASTEXITCODE -ne 0) {
    throw "Generating TestPlatform WiX fragment failed."
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
