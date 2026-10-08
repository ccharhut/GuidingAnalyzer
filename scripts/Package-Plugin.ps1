param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$output = Join-Path $repoRoot "bin/$Configuration/net8.0-windows7.0"
$manifest = Get-Content -LiteralPath (Join-Path $repoRoot 'plugin.json') -Raw | ConvertFrom-Json
$dll = Join-Path $output $manifest.AssemblyName
if (!(Test-Path -LiteralPath $dll)) { throw 'Build the Release configuration before packaging.' }
$builtVersion = [Reflection.AssemblyName]::GetAssemblyName($dll).Version.ToString()
if ($builtVersion -ne $manifest.Version) { throw "DLL version $builtVersion differs from manifest $($manifest.Version). Rebuild first." }
$artifactDir = Join-Path $repoRoot 'artifacts'
New-Item -ItemType Directory -Path $artifactDir -Force | Out-Null
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Write-Zip($zipPath, $entries) {
    $stream = [IO.File]::Open($zipPath, [IO.FileMode]::Create)
    $zip = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($entry in $entries) {
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $entry.Source,
                $entry.Name, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    } finally { $zip.Dispose(); $stream.Dispose() }
}

# NINA and its native dependencies are supplied by the host. Include plugin dependencies.
$entries = @(Get-ChildItem -LiteralPath $output -Filter '*.dll' | Where-Object {
    $_.Name -eq $manifest.AssemblyName -or
    ($_.Name -notlike 'NINA.*' -and $_.Name -notlike 'Microsoft.Web.*' -and $_.Name -notlike 'Microsoft.Extensions.*')
} | ForEach-Object { @{ Source = $_.FullName; Name = $_.Name } })
foreach ($name in @('plugin.json', 'LICENSE.txt')) {
    $entries += @{ Source = Join-Path $repoRoot $name; Name = $name }
}
$validationName = "Validation-$($manifest.Version).md"
$entries += @{ Source = Join-Path $repoRoot "Docs/$validationName"; Name = $validationName }
foreach ($previousNotes in Get-ChildItem -LiteralPath (Join-Path $repoRoot 'Docs') -Filter 'Validation-*.md' | Where-Object Name -ne $validationName) {
    $entries += @{ Source = $previousNotes.FullName; Name = $previousNotes.Name }
}
$binaryZip = Join-Path $artifactDir "GuidingAnalyzer-v$($manifest.Version)-preview.zip"
Write-Zip $binaryZip $entries

Push-Location $repoRoot
try {
    $sourceFiles = @(git ls-files --cached --others --exclude-standard)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate source files.' }
} finally { Pop-Location }
$sourceEntries = @($sourceFiles | Where-Object { $_ -notlike '*.zip' } | ForEach-Object {
    @{ Source = Join-Path $repoRoot $_; Name = $_ }
})
$sourceZip = Join-Path $artifactDir "GuidingAnalyzer-v$($manifest.Version)-sources.zip"
Write-Zip $sourceZip $sourceEntries
Get-FileHash -Algorithm SHA256 -LiteralPath $binaryZip, $sourceZip |
    Select-Object @{Name='File'; Expression={ [IO.Path]::GetFileName($_.Path) }}, Hash |
    Format-Table -AutoSize
