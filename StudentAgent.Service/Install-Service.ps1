param(
    [string]$BinaryPath = (Join-Path $PSScriptRoot "publish\StudentAgent.Service.exe"),
    [switch]$StartAfterInstall
)

$serviceName = "StudentAgentService"
$displayName = "ClassCommander Student Agent"

function Ensure-FirewallRule {
    param(
        [Parameter(Mandatory = $true)][string]$RuleName,
        [Parameter(Mandatory = $true)][string]$ProgramPath,
        [Parameter(Mandatory = $true)][string]$Description
    )

    if (-not (Test-Path $ProgramPath)) {
        Write-Warning "Firewall rule '$RuleName' was skipped because '$ProgramPath' was not found."
        return
    }

    if (Get-Command Get-NetFirewallRule -ErrorAction SilentlyContinue) {
        $existing = Get-NetFirewallRule -DisplayName $RuleName -ErrorAction SilentlyContinue
        if ($existing) {
            Remove-NetFirewallRule -DisplayName $RuleName -ErrorAction SilentlyContinue | Out-Null
        }

        New-NetFirewallRule `
            -DisplayName $RuleName `
            -Direction Inbound `
            -Action Allow `
            -Profile Any `
            -Program $ProgramPath `
            -Description $Description | Out-Null
        return
    }

    & netsh advfirewall firewall delete rule name="$RuleName" program="$ProgramPath" | Out-Null
    & netsh advfirewall firewall add rule `
        name="$RuleName" `
        dir=in `
        action=allow `
        profile=any `
        program="$ProgramPath" `
        enable=yes | Out-Null
}

if (-not (Test-Path $BinaryPath)) {
    throw "Service binary not found at '$BinaryPath'. Publish StudentAgent.Service first."
}

$uiHostPath = Join-Path (Split-Path $BinaryPath -Parent) "StudentAgent.UIHost.exe"
if (-not (Test-Path $uiHostPath)) {
    Write-Warning "StudentAgent.UIHost.exe was not found next to the service binary. Visible lock screens and tray UI will not appear until the UI host is deployed beside the service."
}

$vncHostPath = Join-Path (Split-Path $BinaryPath -Parent) "StudentAgent.VncHost.exe"
if (-not (Test-Path $vncHostPath)) {
    Write-Warning "StudentAgent.VncHost.exe was not found next to the service binary. Remote management preview/control will not be available until the VNC host is deployed beside the service."
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

Ensure-FirewallRule `
    -RuleName "ClassCommander StudentAgent Service" `
    -ProgramPath $BinaryPath `
    -Description "Allows the StudentAgent service to accept classroom control connections."

Ensure-FirewallRule `
    -RuleName "ClassCommander StudentAgent VNC Host" `
    -ProgramPath $vncHostPath `
    -Description "Allows the student VNC host to accept remote management connections."

if ($StartAfterInstall) {
    Start-Service -Name $serviceName
}

Write-Host "Installed $displayName."
