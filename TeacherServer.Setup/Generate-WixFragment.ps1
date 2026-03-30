param(
    [Parameter(Mandatory = $true)]
    [string]$SourceDirectory,
    [Parameter(Mandatory = $true)]
    [string]$DirectoryRefId,
    [Parameter(Mandatory = $true)]
    [string]$ComponentGroupId,
    [Parameter(Mandatory = $true)]
    [string]$OutputPath,
    [string[]]$ExcludeFiles = @()
)

if (-not (Test-Path $SourceDirectory)) {
    throw "Source directory '$SourceDirectory' was not found."
}

$excludeSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($fileName in $ExcludeFiles) {
    [void]$excludeSet.Add($fileName)
}

$files = Get-ChildItem -Path $SourceDirectory -File -Recurse | Where-Object { -not $excludeSet.Contains($_.Name) }

function New-Id([string]$prefix, [string]$value) {
    $sanitized = ($value -replace '[^A-Za-z0-9_\.]', '_')
    $hashBytes = [System.Security.Cryptography.SHA256]::HashData([System.Text.Encoding]::UTF8.GetBytes($value))
    $hash = ([System.BitConverter]::ToString($hashBytes)).Replace('-', '').Substring(0, 12)

    if ($sanitized.Length -gt 32) {
        $sanitized = $sanitized.Substring($sanitized.Length - 32)
    }

    return "${prefix}${sanitized}_$hash"
}

$componentRefs = New-Object System.Collections.Generic.List[string]
$componentXml = New-Object System.Collections.Generic.List[string]

foreach ($file in $files) {
    $relativePath = [System.IO.Path]::GetRelativePath($SourceDirectory, $file.FullName)
    $normalizedRelativePath = $relativePath -replace '\\', '/'
    $idSource = $normalizedRelativePath -replace '/', '_'
    $componentId = New-Id "Cmp_" $idSource
    $fileId = New-Id "Fil_" $idSource
    $escapedSource = $file.FullName.Replace('&', '&amp;')

    $componentRefs.Add("      <ComponentRef Id=""$componentId"" />")
    $componentXml.Add("      <Component Id=""$componentId"" Guid=""*"">")
    $componentXml.Add("        <File Id=""$fileId"" Source=""$escapedSource"" KeyPath=""yes"" />")
    $componentXml.Add("      </Component>")
}

$content = @(
    '<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">'
    '  <Fragment>'
    "    <DirectoryRef Id=""$DirectoryRefId"">"
)

$content += $componentXml

$content += @(
    '    </DirectoryRef>'
    '  </Fragment>'
    '  <Fragment>'
    "    <ComponentGroup Id=""$ComponentGroupId"">"
)

$content += $componentRefs

$content += @(
    '    </ComponentGroup>'
    '  </Fragment>'
    '</Wix>'
)

$outputDirectory = Split-Path $OutputPath -Parent
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
Set-Content -Path $OutputPath -Value $content -Encoding UTF8
