$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $projectRoot

function Import-DotEnv([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) { return }
    foreach ($line in Get-Content -LiteralPath $path) {
        $trimmed = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed) -or $trimmed.StartsWith('#')) { continue }
        $separator = $trimmed.IndexOf('=')
        if ($separator -lt 1) { continue }
        $name = $trimmed.Substring(0, $separator).Trim()
        $value = $trimmed.Substring($separator + 1).Trim()
        if (($value.StartsWith('"') -and $value.EndsWith('"')) -or ($value.StartsWith("'") -and $value.EndsWith("'"))) {
            $value = $value.Substring(1, $value.Length - 2)
        }
        [Environment]::SetEnvironmentVariable($name, $value, 'Process')
    }
}

function Set-Default([string]$name, [string]$value) {
    $current = [Environment]::GetEnvironmentVariable($name, 'Process')
    if ([string]::IsNullOrWhiteSpace($current)) {
        [Environment]::SetEnvironmentVariable($name, $value, 'Process')
    }
}

function Test-Endpoint([string]$baseUrl, [string]$path = '/models') {
    try {
        $checkUrl = ($baseUrl -replace 'host\.docker\.internal', 'localhost').TrimEnd('/') + $path
        Invoke-WebRequest -Uri $checkUrl -UseBasicParsing -TimeoutSec 3 | Out-Null
        return $true
    } catch { return $false }
}

function Test-TrackedProcess([string]$pidPath, [string]$ownerPath) {
    if (-not (Test-Path -LiteralPath $pidPath)) { return $false }
    $trackedPid = 0
    if (-not [int]::TryParse((Get-Content -LiteralPath $pidPath -TotalCount 1), [ref]$trackedPid)) { return $false }
    if ($trackedPid -le 0) { return $false }
    $owner = if (Test-Path -LiteralPath $ownerPath) { (Get-Content -LiteralPath $ownerPath -TotalCount 1).Trim().ToLowerInvariant() } else { '' }
    $processInfo = Get-CimInstance Win32_Process -Filter "ProcessId = $trackedPid" -ErrorAction SilentlyContinue
    if (-not $processInfo) { return $false }
    $commandLine = $processInfo.CommandLine
    return ($owner -eq 'ollama' -and $commandLine -match 'ollama(\.exe)?\s+serve') -or
        ($owner -eq 'mlx' -and $commandLine -match 'mlx_lm\.server')
}

function Wait-Endpoint([string]$baseUrl, [string]$description, [string]$path = '/models', [int]$attempts = 30) {
    for ($attempt = 1; $attempt -le $attempts; $attempt++) {
        if (Test-Endpoint $baseUrl $path) { return }
        Start-Sleep -Seconds 2
    }
    throw "$description did not become ready in time."
}

Import-DotEnv (Join-Path $projectRoot '.env')
$configuredBaseUrl = -not [string]::IsNullOrWhiteSpace($env:LLM_BASE_URL)
$configuredModel = -not [string]::IsNullOrWhiteSpace($env:LLM_MODEL)
Set-Default 'APP_PORT' '8088'
Set-Default 'POSTGRES_DB' 'yuki_demo'
Set-Default 'POSTGRES_USER' 'yuki'
Set-Default 'POSTGRES_PASSWORD' 'yuki_local_only'
Set-Default 'LLM_PROVIDER' 'ollama'
Set-Default 'LLM_AUTOSTART' 'false'

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { throw 'Docker CLI is required. Install Docker Desktop and try again.' }
docker info *> $null
if ($LASTEXITCODE -ne 0) { throw 'Docker Engine is not available. Start Docker Desktop and try again.' }
docker compose version *> $null
if ($LASTEXITCODE -ne 0) { throw 'Docker Compose is required.' }

$provider = $env:LLM_PROVIDER.ToLowerInvariant()
if ($provider -notin @('ollama', 'mlx')) { throw "Unsupported LLM_PROVIDER '$provider'. Use ollama or mlx." }
if ($provider -eq 'mlx') {
    if (-not $configuredBaseUrl) { $env:LLM_BASE_URL = 'http://host.docker.internal:8080/v1' }
    if (-not $configuredModel) { $env:LLM_MODEL = 'mlx-community/Qwen3-4B-Instruct-2507-4bit' }
} else {
    if (-not $configuredBaseUrl) { $env:LLM_BASE_URL = 'http://host.docker.internal:11434/v1' }
    if (-not $configuredModel) { $env:LLM_MODEL = 'qwen3.5:4b' }
}
$runtimeDir = Join-Path $projectRoot '.runtime'
New-Item -ItemType Directory -Path $runtimeDir -Force | Out-Null
$pidPath = Join-Path $runtimeDir 'llm.pid'
$ownerPath = Join-Path $runtimeDir 'llm.owner'
$stdoutLogPath = Join-Path $runtimeDir 'llm.stdout.log'
$stderrLogPath = Join-Path $runtimeDir 'llm.stderr.log'

if (-not (Test-Endpoint $env:LLM_BASE_URL)) {
    if ($env:LLM_AUTOSTART.ToLowerInvariant() -ne 'true') {
        throw "The $provider endpoint is unavailable. Start it or set LLM_AUTOSTART=true. Expected endpoint: $env:LLM_BASE_URL"
    }

    if ($provider -eq 'ollama') {
        if (-not (Get-Command ollama -ErrorAction SilentlyContinue)) { throw 'LLM_AUTOSTART=true requires the ollama CLI.' }
        & ollama show $env:LLM_MODEL *> $null
        if ($LASTEXITCODE -ne 0) { & ollama pull $env:LLM_MODEL }
        $llmProcess = Start-Process -FilePath 'ollama' -ArgumentList 'serve' -PassThru -WindowStyle Hidden -RedirectStandardOutput $stdoutLogPath -RedirectStandardError $stderrLogPath
    } else {
        if (-not (Get-Command python -ErrorAction SilentlyContinue)) { throw 'LLM_AUTOSTART=true for mlx requires python.' }
        & python -c "import mlx_lm" *> $null
        if ($LASTEXITCODE -ne 0) { throw 'LLM_AUTOSTART=true for mlx requires the mlx-lm package.' }
        $mlxPort = ([Uri]$env:LLM_BASE_URL).Port
        if ($mlxPort -le 0) { $mlxPort = 8080 }
        $llmProcess = Start-Process -FilePath 'python' -ArgumentList @('-m', 'mlx_lm.server', '--model', $env:LLM_MODEL, '--host', '0.0.0.0', '--port', $mlxPort) -PassThru -WindowStyle Hidden -RedirectStandardOutput $stdoutLogPath -RedirectStandardError $stderrLogPath
    }
    Set-Content -LiteralPath $pidPath -Value $llmProcess.Id
    Set-Content -LiteralPath $ownerPath -Value $provider
    Wait-Endpoint $env:LLM_BASE_URL "The $provider endpoint"
} elseif (Test-Path -LiteralPath $pidPath) {
    if (-not (Test-TrackedProcess $pidPath $ownerPath)) {
        Remove-Item -LiteralPath $pidPath, $ownerPath -Force -ErrorAction SilentlyContinue
    }
}

if ($provider -eq 'ollama') {
    if (-not (Get-Command ollama -ErrorAction SilentlyContinue)) { throw 'The configured ollama provider requires the ollama CLI.' }
    & ollama show $env:LLM_MODEL *> $null
    if ($LASTEXITCODE -ne 0) {
        if ($env:LLM_AUTOSTART.ToLowerInvariant() -eq 'true') {
            & ollama pull $env:LLM_MODEL
        } else {
            throw "Ollama is running, but model '$env:LLM_MODEL' is not installed. Run 'ollama pull $env:LLM_MODEL' or set LLM_AUTOSTART=true."
        }
    }
}

docker compose up -d db
for ($attempt = 1; $attempt -le 30; $attempt++) {
    docker compose exec -T db pg_isready -U $env:POSTGRES_USER -d $env:POSTGRES_DB *> $null
    if ($LASTEXITCODE -eq 0) { break }
    if ($attempt -eq 30) { throw 'PostgreSQL did not become ready in time.' }
    Start-Sleep -Seconds 2
}
docker compose run --rm db-init
docker compose up -d --build app
Wait-Endpoint "http://localhost:$env:APP_PORT" 'The Yuki Assistant app' '/health'

Write-Host ''
Write-Host 'Yuki Assistant V1 is ready'
Write-Host "App:      http://localhost:$env:APP_PORT"
Write-Host "LLM:      $provider / $env:LLM_MODEL"
Write-Host 'Database: PostgreSQL 18 / healthy'
