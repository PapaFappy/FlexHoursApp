param([switch]$RequireChromeTests)
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$output = Join-Path $PSScriptRoot 'tests\output'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$sourceFiles = @(Get-ChildItem (Join-Path $PSScriptRoot 'source') -Recurse -Filter *.cs | Where-Object { $_.FullName -notlike '*\legacy-oauth\*' } | Sort-Object FullName | ForEach-Object FullName)
$testFiles = @(Get-ChildItem (Join-Path $PSScriptRoot 'tests') -Filter *.cs | Sort-Object FullName | ForEach-Object FullName)
& $compiler /nologo /codepage:65001 /target:exe /debug /warn:4 /main:FlexHours.TestProgram "/out:$output\FlexHours.Tests.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll $sourceFiles $testFiles
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed' }
Push-Location $output
try {
    & '.\FlexHours.Tests.exe'
    $testExitCode = $LASTEXITCODE
    Copy-Item -LiteralPath 'test-results.txt' -Destination (Join-Path $PSScriptRoot 'docs\test-results.txt') -Force
    if ($testExitCode -ne 0) { throw 'Tests failed. See docs\test-results.txt.' }
} finally { Pop-Location }
$node = Get-Command node -ErrorAction SilentlyContinue
if ($node) {
    & $node.Source --test --test-reporter=tap (Join-Path $PSScriptRoot 'tests\chrome-extension.test.mjs') | Tee-Object -FilePath (Join-Path $PSScriptRoot 'docs\chrome-extension-test-results.txt')
    if ($LASTEXITCODE -ne 0) { throw 'Chrome extension tests failed.' }
} elseif ($RequireChromeTests) {
    throw 'Node.js is required to run the Chrome extension tests.'
} else {
    Write-Output 'Chrome JavaScript tests skipped: install Node.js or use -RequireChromeTests to require them.'
}
