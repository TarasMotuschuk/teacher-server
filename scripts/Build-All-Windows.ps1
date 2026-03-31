param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$ReleaseSummary,

    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
$productWxsPath = Join-Path $repoRoot "TeacherServer.Setup\Product.wxs"
$changeLogPath = Join-Path $repoRoot "CHANGELOG.md"
$msiBuildScript = Join-Path $repoRoot "TeacherServer.Setup\Build-Msi.ps1"

$cleanPaths = @(
    "Teacher.Common\bin",
    "Teacher.Common\obj",
    "TeacherClient\bin",
    "TeacherClient\obj",
    "StudentAgent.Service\bin",
    "StudentAgent.Service\obj",
    "StudentAgent.UIHost\bin",
    "StudentAgent.UIHost\obj",
    "TeacherServer.Setup\bin",
    "TeacherServer.Setup\obj",
    "TeacherServer.Setup\artifacts",
    "TeacherServer.Setup\dist",
    "TeacherServer.Setup\Generated"
) | ForEach-Object { Join-Path $repoRoot $_ }

function Update-InstallerVersion {
    $content = Get-Content -LiteralPath $productWxsPath -Raw
    $updated = [System.Text.RegularExpressions.Regex]::Replace(
        $content,
        'Version="\d+\.\d+\.\d+"',
        "Version=""$Version""",
        1
    )

    if ($updated -eq $content) {
        throw "Could not update installer version in $productWxsPath."
    }

    Set-Content -LiteralPath $productWxsPath -Value $updated -Encoding UTF8
}

function Update-Changelog {
    $date = Get-Date -Format "yyyy-MM-dd"
    $releaseItems = $ReleaseSummary.Split(';') | ForEach-Object { $_.Trim() } | Where-Object { $_ }
    if ($releaseItems.Count -eq 0) {
        throw "ReleaseSummary must contain at least one ';'-separated item."
    }

    $existing = Get-Content -LiteralPath $changeLogPath -Raw
    $newEntryLines = @(
        "## [$Version] - $date",
        "",
        "### Changed",
        ""
    )

    foreach ($item in $releaseItems) {
        $newEntryLines += "- $item"
    }

    $newEntryLines += @(
        "",
        "### Notes",
        "",
        "- Windows release build only.",
        "- `TeacherClient.Avalonia` is not built for Windows by this command.",
        ""
    )

    $newEntry = ($newEntryLines -join [Environment]::NewLine)
    $marker = "## ["
    $markerIndex = $existing.IndexOf($marker)
    if ($markerIndex -lt 0) {
        throw "Could not find changelog insertion point in $changeLogPath."
    }

    $updated = $existing.Insert($markerIndex, $newEntry)
    Set-Content -LiteralPath $changeLogPath -Value $updated -Encoding UTF8
}

function Clean-WindowsBuildArtifacts {
    foreach ($path in $cleanPaths) {
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Recurse -Force
        }
    }
}

function Invoke-WindowsBuild {
    dotnet build ".\Teacher.Common\Teacher.Common.csproj" -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw "Teacher.Common build failed." }

    dotnet build ".\TeacherClient\TeacherClient.csproj" -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw "TeacherClient build failed." }

    dotnet build ".\StudentAgent.Service\StudentAgent.Service.csproj" -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw "StudentAgent.Service build failed." }

    dotnet build ".\StudentAgent.UIHost\StudentAgent.UIHost.csproj" -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw "StudentAgent.UIHost build failed." }

    & $msiBuildScript -Configuration $Configuration
    if ($LASTEXITCODE -ne 0) { throw "TeacherServer.Setup MSI build failed." }
}

function Commit-And-Push {
    git add AGENTS.md CHANGELOG.md TeacherServer.Setup\Product.wxs scripts\Build-All-Windows.ps1
    if ($LASTEXITCODE -ne 0) { throw "git add failed." }

    git commit -m "Release $Version"
    if ($LASTEXITCODE -ne 0) { throw "git commit failed." }

    git push origin HEAD
    if ($LASTEXITCODE -ne 0) { throw "git push failed." }
}

Push-Location $repoRoot
try {
    Update-InstallerVersion
    Update-Changelog
    Clean-WindowsBuildArtifacts
    Invoke-WindowsBuild
    Commit-And-Push
}
finally {
    Pop-Location
}
