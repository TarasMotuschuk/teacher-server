param(
    [string]$BinaryPath = (Join-Path $PSScriptRoot "publish\StudentAgent.Service.exe"),
    [switch]$StartAfterInstall
)

$serviceName = "StudentAgentService"
$displayName = "ClassCommander Student Agent"

if (-not (Test-Path $BinaryPath)) {
    throw "Service binary not found at '$BinaryPath'. Publish StudentAgent.Service first."
}

$uiHostPath = Join-Path (Split-Path $BinaryPath -Parent) "StudentAgent.UIHost.exe"
if (-not (Test-Path $uiHostPath)) {
    Write-Warning "StudentAgent.UIHost.exe was not found next to the service binary. Visible lock screens and tray UI will not appear until the UI host is deployed beside the service."
}

if (Get-Service -Name $serviceName -ErrorAction SilentlyContinue) {
    throw "Service '$serviceName' is already installed."
}

New-Service `
    -Name $serviceName `
    -DisplayName $displayName `
    -BinaryPathName ('"{0}"' -f $BinaryPath) `
    -StartupType Automatic `
    -Description "Privileged StudentAgent runtime host for ClassCommander classroom control."

sc.exe failure $serviceName reset= 86400 actions= restart/5000/restart/5000/restart/5000 | Out-Null

if ($StartAfterInstall) {
    Start-Service -Name $serviceName
}

Write-Host "Installed $displayName."
