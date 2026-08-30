$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $projectRoot

if (Get-Command docker -ErrorAction SilentlyContinue) {
    docker compose down
}

$runtimeDir = Join-Path $projectRoot '.runtime'
$pidPath = Join-Path $runtimeDir 'llm.pid'
$ownerPath = Join-Path $runtimeDir 'llm.owner'
if (Test-Path -LiteralPath $pidPath) {
    $trackedPid = 0
    [int]::TryParse((Get-Content -LiteralPath $pidPath -TotalCount 1), [ref]$trackedPid) | Out-Null
    $owner = if (Test-Path -LiteralPath $ownerPath) { (Get-Content -LiteralPath $ownerPath -TotalCount 1).Trim().ToLowerInvariant() } else { '' }
    if ($trackedPid -gt 0) {
        $processInfo = Get-CimInstance Win32_Process -Filter "ProcessId = $trackedPid" -ErrorAction SilentlyContinue
        $commandLine = if ($processInfo) { $processInfo.CommandLine } else { '' }
        $ownedCommand = ($owner -eq 'ollama' -and $commandLine -match 'ollama(\.exe)?\s+serve') -or ($owner -eq 'mlx' -and $commandLine -match 'mlx_lm\.server')
        if ($ownedCommand) { Stop-Process -Id $trackedPid -Force -ErrorAction SilentlyContinue }
    }
    Remove-Item -LiteralPath $pidPath, $ownerPath -Force -ErrorAction SilentlyContinue
}
