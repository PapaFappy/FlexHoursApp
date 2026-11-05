param([string]$ExtensionId)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ExtensionId)) {
    Write-Host 'In Chrome: chrome://extensions > Developer mode > Load unpacked > chrome-extension folder.'
    $ExtensionId = Read-Host 'Paste the ID shown under FlexHours Chrome Sync'
}
if ($ExtensionId -notmatch '^[a-p]{32}$') { throw 'Chrome extension IDs contain exactly 32 letters from a to p.' }
$executable = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'bin\FlexHours-ChromeHost.exe'))
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) { throw 'Extract the complete package first; the Chrome host executable is missing.' }
$folder = Join-Path $env:LOCALAPPDATA 'FlexHours\Chrome'
New-Item -ItemType Directory -Path $folder -Force | Out-Null
$manifestPath = Join-Path $folder 'native-host.json'
$manifest = [ordered]@{
    name = 'com.flexhours.chrome_sync'
    description = 'FlexHours local Chrome timesheet receiver'
    path = $executable
    type = 'stdio'
    allowed_origins = @('chrome-extension://' + $ExtensionId + '/')
}
[IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 4), [Text.UTF8Encoding]::new($false))
$key = 'HKCU:\Software\Google\Chrome\NativeMessagingHosts\com.flexhours.chrome_sync'
New-Item -Path $key -Force | Out-Null
Set-Item -LiteralPath $key -Value $manifestPath
Write-Host 'Helper registered for your Windows account. No administrator access is required.'
Write-Host 'Keep this package in a stable folder. Re-run setup after moving it.'
Write-Host 'Now connect your own current-month Timesheet using the extension popup.'
Write-Host 'In FlexHours > Chrome sync, enter the same company and enable receiving updates.'
