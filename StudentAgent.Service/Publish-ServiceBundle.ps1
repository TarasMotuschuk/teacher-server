param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputDirectory = (Join-Path $PSScriptRoot "publish")
)

$serviceProject = Join-Path $PSScriptRoot "StudentAgent.Service.csproj"
$uiHostProject = Join-Path (Split-Path $PSScriptRoot -Parent) "StudentAgent.UIHost\StudentAgent.UIHost.csproj"
$vncHostProject = Join-Path (Split-Path $PSScriptRoot -Parent) "StudentAgent.VncHost\StudentAgent.VncHost.csproj"
$updaterProject = Join-Path (Split-Path $PSScriptRoot -Parent) "StudentAgent.Updater\StudentAgent.Updater.csproj"
$testRunnerProject = Join-Path (Split-Path $PSScriptRoot -Parent) "ClassCommander.TestRunner\ClassCommander.TestRunner.csproj"

if (-not (Test-Path $serviceProject)) {
    throw "StudentAgent.Service.csproj was not found."
}

if (-not (Test-Path $uiHostProject)) {
    throw "StudentAgent.UIHost.csproj was not found."
}

if (-not (Test-Path $vncHostProject)) {
    throw "StudentAgent.VncHost.csproj was not found."
}

if (-not (Test-Path $updaterProject)) {
    throw "StudentAgent.Updater.csproj was not found."
}

if (-not (Test-Path $testRunnerProject)) {
    throw "ClassCommander.TestRunner.csproj was not found."
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

dotnet publish $serviceProject `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -o $OutputDirectory

if ($LASTEXITCODE -ne 0) {
    throw "Publishing StudentAgent.Service failed."
}

dotnet publish $uiHostProject `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -o $OutputDirectory

if ($LASTEXITCODE -ne 0) {
    throw "Publishing StudentAgent.UIHost failed."
}

dotnet publish $vncHostProject `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -o $OutputDirectory

if ($LASTEXITCODE -ne 0) {
    throw "Publishing StudentAgent.VncHost failed."
}

dotnet publish $updaterProject `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -o $OutputDirectory

if ($LASTEXITCODE -ne 0) {
    throw "Publishing StudentAgent.Updater failed."
}

# TestRunner lives in its own subdirectory so its self-contained payload does not
# collide with the agent binaries; the installer harvest and the agent updater both
# copy the bundle recursively.
dotnet publish $testRunnerProject `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -o (Join-Path $OutputDirectory "TestRunner")

if ($LASTEXITCODE -ne 0) {
    throw "Publishing ClassCommander.TestRunner failed."
}

Write-Host "Published StudentAgent.Service, StudentAgent.UIHost, StudentAgent.VncHost, StudentAgent.Updater, and ClassCommander.TestRunner to '$OutputDirectory'."
