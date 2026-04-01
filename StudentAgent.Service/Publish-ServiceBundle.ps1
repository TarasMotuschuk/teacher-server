param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputDirectory = (Join-Path $PSScriptRoot "publish")
)

$serviceProject = Join-Path $PSScriptRoot "StudentAgent.Service.csproj"
$uiHostProject = Join-Path (Split-Path $PSScriptRoot -Parent) "StudentAgent.UIHost\StudentAgent.UIHost.csproj"
$updaterProject = Join-Path (Split-Path $PSScriptRoot -Parent) "StudentAgent.Updater\StudentAgent.Updater.csproj"

if (-not (Test-Path $serviceProject)) {
    throw "StudentAgent.Service.csproj was not found."
}

if (-not (Test-Path $uiHostProject)) {
    throw "StudentAgent.UIHost.csproj was not found."
}

if (-not (Test-Path $updaterProject)) {
    throw "StudentAgent.Updater.csproj was not found."
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

dotnet publish $updaterProject `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -o $OutputDirectory

if ($LASTEXITCODE -ne 0) {
    throw "Publishing StudentAgent.Updater failed."
}

Write-Host "Published StudentAgent.Service, StudentAgent.UIHost, and StudentAgent.Updater to '$OutputDirectory'."
