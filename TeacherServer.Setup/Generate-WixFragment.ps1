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
$resolvedSourceDirectory = (Resolve-Path -LiteralPath $SourceDirectory).Path

function New-Id([string]$prefix, [string]$value) {
    $sanitized = ($value -replace '[^A-Za-z0-9_\.]', '_')
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hashBytes = $sha256.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($value))
    }
    finally {
        $sha256.Dispose()
    }
    $hash = ([System.BitConverter]::ToString($hashBytes)).Replace('-', '').Substring(0, 12)

    if ($sanitized.Length -gt 32) {
        $sanitized = $sanitized.Substring($sanitized.Length - 32)
    }

    return "${prefix}${sanitized}_$hash"
}

function New-DeterministicGuid([string]$value) {
    $md5 = [System.Security.Cryptography.MD5]::Create()
    try {
        $hashBytes = $md5.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($value))
    }
    finally {
        $md5.Dispose()
    }

    return [Guid]::new($hashBytes).ToString().ToUpperInvariant()
}

function Get-RelativePath([string]$basePath, [string]$targetPath) {
    $baseUri = [System.Uri]($basePath.TrimEnd('\') + '\')
    $targetUri = [System.Uri]$targetPath

    return [System.Uri]::UnescapeDataString($baseUri.MakeRelativeUri($targetUri).ToString()) -replace '/', '\'
}

function New-DirectoryNode([string]$id) {
    return [pscustomobject]@{
        Id = $id
        Children = [ordered]@{}
        Files = New-Object System.Collections.Generic.List[object]
    }
}

function Render-DirectoryNode($node, [int]$indentLevel) {
    $indent = '  ' * $indentLevel
    $lines = New-Object System.Collections.Generic.List[string]

    foreach ($fileEntry in $node.Files) {
        $lines.Add("$indent<Component Id=""$($fileEntry.ComponentId)"" Guid=""$($fileEntry.ComponentGuid)"">")
        $lines.Add("$indent  <File Id=""$($fileEntry.FileId)"" Source=""$($fileEntry.Source)"" KeyPath=""yes"" />")
        $lines.Add("$indent</Component>")
    }

    foreach ($child in $node.Children.Values) {
        $lines.Add("$indent<Directory Id=""$($child.Node.Id)"" Name=""$($child.Name)"">")
        foreach ($childLine in Render-DirectoryNode -node $child.Node -indentLevel ($indentLevel + 1)) {
            $lines.Add($childLine)
        }

        $lines.Add("$indent</Directory>")
    }

    return $lines
}

$componentRefs = New-Object System.Collections.Generic.List[string]
$rootNode = New-DirectoryNode -id $DirectoryRefId

foreach ($file in $files) {
    $resolvedFilePath = (Resolve-Path -LiteralPath $file.FullName).Path
    $relativePath = Get-RelativePath -basePath $resolvedSourceDirectory -targetPath $resolvedFilePath
    $normalizedRelativePath = $relativePath -replace '\\', '/'
    $idSource = "$DirectoryRefId/$normalizedRelativePath" -replace '/', '_'
    $componentId = New-Id "Cmp_" $idSource
    $fileId = New-Id "Fil_" $idSource
    $componentGuid = New-DeterministicGuid -value $idSource
    $escapedSource = $resolvedFilePath.Replace('&', '&amp;')
    $relativeDirectory = Split-Path -Path $relativePath -Parent
    if ($relativeDirectory -eq '.') {
        $relativeDirectory = ''
    }

    $componentRefs.Add("      <ComponentRef Id=""$componentId"" />")

    $currentNode = $rootNode
    if (-not [string]::IsNullOrWhiteSpace($relativeDirectory)) {
        $segments = $relativeDirectory -split '\\'
        $currentPath = ''
        foreach ($segment in $segments) {
            $currentPath = if ([string]::IsNullOrEmpty($currentPath)) { $segment } else { "$currentPath\$segment" }
            if (-not $currentNode.Children.Contains($segment)) {
                $currentNode.Children[$segment] = [pscustomobject]@{
                    Name = $segment
                    Node = (New-DirectoryNode -id (New-Id "Dir_" "$DirectoryRefId/$currentPath"))
                }
            }

            $currentNode = $currentNode.Children[$segment].Node
        }
    }

    $currentNode.Files.Add([pscustomobject]@{
        ComponentId = $componentId
        ComponentGuid = $componentGuid
        FileId = $fileId
        Source = $escapedSource
    })
}

$content = @(
    '<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">'
    '  <Fragment>'
    "    <DirectoryRef Id=""$DirectoryRefId"">"
)

$content += Render-DirectoryNode -node $rootNode -indentLevel 3

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
