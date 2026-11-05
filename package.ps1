param([switch]$SkipTests)
$ErrorActionPreference = 'Stop'
if (-not $SkipTests) { & (Join-Path $PSScriptRoot 'test.ps1') }
& (Join-Path $PSScriptRoot 'build.ps1')
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$destination = Join-Path (Split-Path $PSScriptRoot -Parent) 'FlexHours-Windows.zip'
$temporary = $destination + '.new'
$stream = [IO.File]::Open($temporary, [IO.FileMode]::Create)
$archive = New-Object System.IO.Compression.ZipArchive($stream, [IO.Compression.ZipArchiveMode]::Create)
try {
    $files = @(Get-ChildItem $PSScriptRoot -File -Filter *.ps1)
    $files += Get-Item (Join-Path $PSScriptRoot 'README.txt'), (Join-Path $PSScriptRoot 'README.md')
    foreach ($folder in @('bin', 'source', 'docs', 'assets', 'chrome-extension')) {
        $files += Get-ChildItem (Join-Path $PSScriptRoot $folder) -File -Recurse
    }
    $files += Get-ChildItem (Join-Path $PSScriptRoot 'tests') -File -Filter *.cs
    $files += Get-ChildItem (Join-Path $PSScriptRoot 'tests') -File -Filter *.mjs
    foreach ($file in $files) {
        $relative = $file.FullName.Substring($PSScriptRoot.Length + 1).Replace('\', '/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $relative, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally {
    $archive.Dispose()
    $stream.Dispose()
}
Move-Item -LiteralPath $temporary -Destination $destination -Force
Write-Output ('Package ready: ' + $destination)

