param([string]$OutputPath = 'bin\FlexHours-Browser.exe')
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
Push-Location $PSScriptRoot
try {
    $absoluteOutput = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot $OutputPath))
    New-Item -ItemType Directory -Path (Split-Path $absoluteOutput) -Force | Out-Null
    $sourceFiles = @(Get-ChildItem source -Recurse -Filter *.cs | Where-Object { $_.FullName -notlike '*\legacy-oauth\*' } | Sort-Object FullName | ForEach-Object FullName)
    & $compiler /nologo /codepage:65001 /target:winexe /optimize+ /warn:4 /main:FlexHours.Program /win32icon:assets\FlexHours.ico "/out:$absoluteOutput" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll $sourceFiles
    if ($LASTEXITCODE -ne 0) { throw 'Compilation failed' }
    & $compiler /nologo /codepage:65001 /target:exe /optimize+ /warn:4 /main:FlexHours.ChromeNativeHostProgram /out:bin\FlexHours-ChromeHost.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll $sourceFiles
    if ($LASTEXITCODE -ne 0) { throw 'Chrome host compilation failed' }
} finally { Pop-Location }
