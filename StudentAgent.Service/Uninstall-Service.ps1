param()

$serviceName = "StudentAgentService"

$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if (-not $service) {
    Write-Host "Service '$serviceName' is not installed."
    return
}

if ($service.Status -ne 'Stopped') {
    Stop-Service -Name $serviceName -Force
}

sc.exe delete $serviceName | Out-Null
Write-Host "Removed service '$serviceName'."
