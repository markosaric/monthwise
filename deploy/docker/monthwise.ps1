# Monthwise Docker one-click on Windows: start, stop, update and remove Monthwise.
# Usage: monthwise.ps1 start [-Open] | stop | update [version] | status | logs | network on|off | remove [-Data]
param(
    [Parameter(Position = 0)] [string] $Command,
    [Parameter(Position = 1)] [string] $Argument,
    [switch] $Open,
    [switch] $Data
)

$ErrorActionPreference = 'Stop'

$ComposeFile = Join-Path $PSScriptRoot 'compose.yml'
$ImageRepo = 'ghcr.io/markosaric/monthwise'
$Volume = 'monthwise-pgdata'
$BaseDir = Join-Path $env:LOCALAPPDATA 'Monthwise'
$ConfigDir = if ($env:MONTHWISE_CONFIG_DIR) { $env:MONTHWISE_CONFIG_DIR } else { Join-Path $BaseDir 'docker' }
$SecretsDir = Join-Path $ConfigDir 'secrets'
$EnvFile = Join-Path $ConfigDir 'compose.env'

function Fail([string] $Message) {
    Write-Host "Monthwise: $Message" -ForegroundColor Red
    exit 1
}

# UTF-8 without a byte order mark: PostgreSQL and Docker read these files byte for byte.
function Write-PlainFile([string] $Path, [string] $Text) {
    [System.IO.File]::WriteAllText($Path, $Text, (New-Object System.Text.UTF8Encoding $false))
}

function Invoke-Compose {
    & docker compose --project-name monthwise --file $ComposeFile --env-file $EnvFile @args
    if ($LASTEXITCODE -ne 0) { Fail "docker compose $($args -join ' ') failed." }
}

function Get-EnvValue([string] $Key, [string] $Default) {
    if (Test-Path $EnvFile) {
        $line = Get-Content $EnvFile | Where-Object { $_ -like "$Key=*" } | Select-Object -Last 1
        if ($line) { return $line.Substring($Key.Length + 1) }
    }
    return $Default
}

function Set-EnvValue([string] $Key, [string] $Value) {
    $lines = @(Get-Content $EnvFile | Where-Object { $_ -notlike "$Key=*" }) + "$Key=$Value"
    Write-PlainFile $EnvFile (($lines -join "`n") + "`n")
}

function Get-Address { "http://localhost:$(Get-EnvValue 'MONTHWISE_PORT' '8080')" }

function Test-Docker {
    & docker info *> $null
    return $LASTEXITCODE -eq 0
}

function Initialize-Docker {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        Fail 'Docker is not installed. Install Docker Desktop first (see the user manual).'
    }
    if (Test-Docker) { return }
    $desktop = Join-Path $env:ProgramFiles 'Docker\Docker\Docker Desktop.exe'
    if (Test-Path $desktop) {
        Write-Host 'Starting Docker Desktop...'
        Start-Process $desktop
    }
    for ($i = 0; $i -lt 60; $i++) {
        if (Test-Docker) { return }
        Start-Sleep -Seconds 2
    }
    Fail 'Docker is not running. Start Docker Desktop and try again.'
}

function New-RandomBase64 {
    $bytes = New-Object byte[] 32
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    [Convert]::ToBase64String($bytes)
}

function New-RandomHex {
    $bytes = New-Object byte[] 24
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    -join ($bytes | ForEach-Object { $_.ToString('x2') })
}

function Initialize-Config {
    New-Item -ItemType Directory -Force -Path $BaseDir, $ConfigDir | Out-Null
    # Only this user account (and the system) can open the Monthwise folder.
    & icacls $BaseDir /inheritance:r /grant:r "${env:USERNAME}:(OI)(CI)F" /grant:r 'SYSTEM:(OI)(CI)F' | Out-Null
    if (-not (Test-Path $EnvFile)) {
        Write-PlainFile $EnvFile ("MONTHWISE_CONFIG_DIR=$ConfigDir`nMONTHWISE_IMAGE=${ImageRepo}:latest`n" +
            "MONTHWISE_BIND=127.0.0.1`nMONTHWISE_PORT=8080`n")
    }
    Initialize-SecretFile
}

function Initialize-SecretFile {
    if (Test-Path (Join-Path $SecretsDir 'SensitiveData__HashKey')) { return }
    & docker volume inspect $Volume *> $null
    if ($LASTEXITCODE -eq 0) {
        Fail ("The database exists but the secrets are missing in:`n  $SecretsDir`n" +
            "Put back the files saved in your password manager (see the user manual). New keys would make`n" +
            'the stored card numbers and IBANs unreadable, so none are created.')
    }
    New-Item -ItemType Directory -Force -Path $SecretsDir | Out-Null
    $password = New-RandomHex
    Write-PlainFile (Join-Path $SecretsDir 'postgres_password') $password
    Write-PlainFile (Join-Path $SecretsDir 'ConnectionStrings__Monthwise') `
        "Host=db;Port=5432;Database=monthwise;Username=monthwise;Password=$password"
    Write-PlainFile (Join-Path $SecretsDir 'SensitiveData__ActiveKeyId') 'k1'
    Write-PlainFile (Join-Path $SecretsDir 'SensitiveData__EncryptionKeys__k1') (New-RandomBase64)
    Write-PlainFile (Join-Path $SecretsDir 'SensitiveData__HashKey') (New-RandomBase64)
    Write-Host ''
    Write-Host 'Monthwise created its secrets (database password and card number encryption keys) in:'
    Write-Host "  $SecretsDir"
    Write-Host 'Copy these files into your password manager now. Without them the stored card numbers' -ForegroundColor Yellow
    Write-Host 'and IBANs can never be read again.' -ForegroundColor Yellow
    Write-Host ''
}

function Connect-Registry {
    # The repository is private: sign in to GHCR with the GitHub CLI (gh auth login, once).
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        Fail 'The GitHub CLI (gh) is needed to download Monthwise. Install it and run: gh auth login'
    }
    $user = & gh api user --jq .login
    & gh auth token | docker login ghcr.io --username $user --password-stdin | Out-Null
    if ($LASTEXITCODE -ne 0) { Fail 'Signing in to ghcr.io failed. Allow GitHub packages once: gh auth refresh --scopes read:packages' }
}

function Initialize-Image {
    $image = Get-EnvValue 'MONTHWISE_IMAGE' "${ImageRepo}:latest"
    & docker image inspect $image *> $null
    if ($LASTEXITCODE -eq 0) { return }
    Connect-Registry
    & docker pull $image
    if ($LASTEXITCODE -ne 0) { Fail "Downloading $image failed. Allow GitHub packages once: gh auth refresh --scopes read:packages" }
}

function Wait-Ready {
    $url = "$(Get-Address)/api/health"
    for ($i = 0; $i -lt 45; $i++) {
        try {
            Invoke-RestMethod -Uri $url -TimeoutSec 2 | Out-Null
            return
        }
        catch { Start-Sleep -Seconds 2 }
    }
    Fail 'Monthwise did not become ready. Its log: monthwise.ps1 logs'
}

switch ($Command) {
    'start' {
        Initialize-Docker
        Initialize-Config
        Initialize-Image
        Invoke-Compose up --detach
        Write-Host 'Waiting for Monthwise...'
        Wait-Ready
        Write-Host "Monthwise is running at $(Get-Address)"
        if ($Open) { Start-Process (Get-Address) }
    }
    'stop' {
        Initialize-Docker
        Invoke-Compose stop
        Write-Host 'Monthwise is stopped. The data is kept.'
    }
    'update' {
        Initialize-Docker
        Initialize-Config
        if ($Argument) { Set-EnvValue 'MONTHWISE_IMAGE' "${ImageRepo}:$($Argument.TrimStart('v'))" }
        Connect-Registry
        Invoke-Compose pull app
        Invoke-Compose up --detach
        Wait-Ready
        Write-Host "Monthwise is updated: $((Invoke-RestMethod "$(Get-Address)/api/health").version)"
    }
    'status' {
        Initialize-Docker
        Invoke-Compose ps
        try { Invoke-RestMethod "$(Get-Address)/api/health" -TimeoutSec 2 } catch { Write-Host 'Monthwise is not answering.' }
    }
    'logs' {
        Initialize-Docker
        Invoke-Compose logs --tail 200
    }
    'network' {
        if (-not (Test-Path $EnvFile)) { Fail 'Start Monthwise once first: monthwise.ps1 start' }
        switch ($Argument) {
            'on' { Set-EnvValue 'MONTHWISE_BIND' '0.0.0.0' }
            'off' { Set-EnvValue 'MONTHWISE_BIND' '127.0.0.1' }
            default { Fail 'Usage: monthwise.ps1 network on|off' }
        }
        Initialize-Docker
        Invoke-Compose up --detach
        if ($Argument -eq 'on') {
            Write-Host "Other devices on your network can now connect to port $(Get-EnvValue 'MONTHWISE_PORT' '8080') of this computer."
            Write-Host 'Until login exists, anyone who can reach it can see and change all data.' -ForegroundColor Yellow
        }
        else {
            Write-Host 'Only this computer can connect to Monthwise.'
        }
    }
    'remove' {
        Initialize-Docker
        Invoke-Compose down
        if ($Data) {
            $answer = Read-Host "Delete all Monthwise data in Docker volume $Volume? This cannot be undone. Type DELETE"
            if ($answer -ceq 'DELETE') {
                & docker volume rm $Volume | Out-Null
                Write-Host "The data is deleted. The secrets in $SecretsDir are kept; delete them yourself if you no longer need them."
            }
            else { Write-Host 'The data is kept.' }
        }
        else {
            Write-Host "Monthwise is removed from Docker. The data is kept in Docker volume $Volume."
        }
    }
    default {
        Write-Host 'Usage: monthwise.ps1 start [-Open] | stop | update [version] | status | logs | network on|off | remove [-Data]'
        exit 2
    }
}
