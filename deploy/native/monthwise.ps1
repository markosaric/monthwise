# Monthwise native background service on Windows (FR-9): PostgreSQL and Monthwise start at login,
# without Docker and without administrator rights. Needs the GitHub CLI (gh auth login, once).
# Usage: monthwise.ps1 install [version] | start | stop | update [version] | status | logs
#        | network on|off | uninstall [-Data]
param(
    [Parameter(Position = 0)] [string] $Command,
    [Parameter(Position = 1)] [string] $Argument,
    [switch] $Data
)

$ErrorActionPreference = 'Stop'

$Repo = 'markosaric/monthwise'
$Runtime = 'win-x64'
$PgPort = 5442
$AppPort = 8080
$BaseDir = Join-Path $env:LOCALAPPDATA 'Monthwise'
$NativeDir = Join-Path $BaseDir 'native'
$AppDir = Join-Path $NativeDir 'app'
$PgDir = Join-Path $NativeDir 'pgsql'
$PgBin = Join-Path $PgDir 'bin'
$PgData = Join-Path $NativeDir 'pgdata'
$ConfigDir = Join-Path $NativeDir 'config'
$SecretsDir = Join-Path $ConfigDir 'secrets'
$SettingsFile = Join-Path $ConfigDir 'settings.json'
$LogDir = Join-Path $NativeDir 'logs'
$PgTask = 'Monthwise PostgreSQL'
$AppTask = 'Monthwise'
$AppExe = Join-Path $AppDir 'Monthwise.Api.exe'

function Fail([string] $Message) {
    Write-Host "Monthwise: $Message" -ForegroundColor Red
    exit 1
}

# UTF-8 without a byte order mark: PostgreSQL reads the password file byte for byte.
function Write-PlainFile([string] $Path, [string] $Text) {
    [System.IO.File]::WriteAllText($Path, $Text, (New-Object System.Text.UTF8Encoding $false))
}

function New-RandomByteArray([int] $Count) {
    $bytes = New-Object byte[] $Count
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    return , $bytes
}

function Get-Address { "http://localhost:$AppPort" }

function Assert-Gh {
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        Fail 'The GitHub CLI is needed: winget install GitHub.cli, then gh auth login'
    }
    & gh auth status *> $null
    if ($LASTEXITCODE -ne 0) { Fail 'Sign in to GitHub once: gh auth login' }
}

# Downloads release assets matching the patterns into a new folder and checks them against SHA256SUMS.
function Get-ReleaseAsset([string] $Version, [string[]] $Patterns) {
    $tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("monthwise-" + [guid]::NewGuid())
    New-Item -ItemType Directory -Path $tmp | Out-Null
    $arguments = @('release', 'download')
    if ($Version -ne 'latest') { $arguments += "v$($Version.TrimStart('v'))" }
    $arguments += @('--repo', $Repo, '--dir', $tmp, '--pattern', 'SHA256SUMS')
    foreach ($pattern in $Patterns) { $arguments += @('--pattern', $pattern) }
    & gh @arguments
    if ($LASTEXITCODE -ne 0) { Fail 'Downloading the release failed.' }
    $sums = Get-Content (Join-Path $tmp 'SHA256SUMS')
    foreach ($file in Get-ChildItem $tmp -Filter *.zip) {
        $expected = ($sums | Where-Object { $_ -match "\s\*?$([regex]::Escape($file.Name))$" }) -replace '\s.*$', ''
        $actual = (Get-FileHash $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        if (-not $expected -or $expected.ToLowerInvariant() -ne $actual) { Fail "$($file.Name) does not match its checksum." }
    }
    return $tmp
}

function Expand-Release([string] $Version, [string] $Target) {
    $tmp = Get-ReleaseAsset $Version @("monthwise-*-$Runtime.zip")
    if (Test-Path $Target) { Remove-Item -Recurse -Force $Target }
    Expand-Archive -Path (Get-ChildItem $tmp -Filter "monthwise-*-$Runtime.zip").FullName -DestinationPath $Target
    Remove-Item -Recurse -Force $tmp
}

function New-SecretFile {
    New-Item -ItemType Directory -Force -Path $SecretsDir | Out-Null
    $password = -join ((New-RandomByteArray 24) | ForEach-Object { $_.ToString('x2') })
    Write-PlainFile (Join-Path $SecretsDir 'postgres_password') $password
    Write-PlainFile (Join-Path $SecretsDir 'ConnectionStrings__Monthwise') `
        "Host=127.0.0.1;Port=$PgPort;Database=monthwise;Username=monthwise;Password=$password"
    Write-PlainFile (Join-Path $SecretsDir 'SensitiveData__ActiveKeyId') 'k1'
    Write-PlainFile (Join-Path $SecretsDir 'SensitiveData__EncryptionKeys__k1') ([Convert]::ToBase64String((New-RandomByteArray 32)))
    Write-PlainFile (Join-Path $SecretsDir 'SensitiveData__HashKey') ([Convert]::ToBase64String((New-RandomByteArray 32)))
    Write-Host ''
    Write-Host 'Monthwise created its secrets (database password and card number encryption keys) in:'
    Write-Host "  $SecretsDir"
    Write-Host 'Copy these files into your password manager now. Without them the stored card numbers' -ForegroundColor Yellow
    Write-Host 'and IBANs can never be read again.' -ForegroundColor Yellow
    Write-Host ''
}

function Write-SettingFile([string] $Bind) {
    $settings = [ordered]@{
        Urls = "http://${Bind}:$AppPort"
        Database = [ordered]@{ MigrateOnStartup = $true; StartupWaitSeconds = 60 }
        Logging = [ordered]@{ File = [ordered]@{ Path = $LogDir; RetainedDays = 14 } }
    }
    Write-PlainFile $SettingsFile ($settings | ConvertTo-Json -Depth 5)
}

# A task that starts the program hidden at this user's login and restarts it if it stops.
function Register-LogonTask([string] $Name, [string] $Program, [string] $Arguments) {
    $action = New-ScheduledTaskAction -Execute 'conhost.exe' -Argument "--headless `"$Program`" $Arguments"
    $trigger = New-ScheduledTaskTrigger -AtLogOn -User $env:USERNAME
    $settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -RestartCount 999 `
        -RestartInterval (New-TimeSpan -Minutes 1) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
    $principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive -RunLevel Limited
    Register-ScheduledTask -TaskName $Name -Action $action -Trigger $trigger -Settings $settings `
        -Principal $principal -Force | Out-Null
}

function Test-PostgresServer {
    & (Join-Path $PgBin 'pg_isready.exe') -h 127.0.0.1 -p $PgPort -q
    return $LASTEXITCODE -eq 0
}

function Start-Monthwise {
    Start-ScheduledTask -TaskName $PgTask
    for ($i = 0; $i -lt 30 -and -not (Test-PostgresServer); $i++) { Start-Sleep -Seconds 1 }
    Start-ScheduledTask -TaskName $AppTask
}

function Stop-App {
    Stop-ScheduledTask -TaskName $AppTask -ErrorAction SilentlyContinue
    Get-Process -Name 'Monthwise.Api' -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -and $_.Path.StartsWith($NativeDir, [StringComparison]::OrdinalIgnoreCase) } |
        Stop-Process -Force
}

function Stop-PostgresServer {
    Stop-ScheduledTask -TaskName $PgTask -ErrorAction SilentlyContinue
    if (Test-Path (Join-Path $PgData 'postmaster.pid')) {
        & (Join-Path $PgBin 'pg_ctl.exe') stop -D $PgData -m fast *> $null
    }
}

function Wait-Ready {
    for ($i = 0; $i -lt 45; $i++) {
        try {
            Invoke-RestMethod -Uri "$(Get-Address)/api/health" -TimeoutSec 2 | Out-Null
            return $true
        }
        catch { Start-Sleep -Seconds 2 }
    }
    return $false
}

function Install-Monthwise([string] $Version) {
    Assert-Gh
    if (Test-Path $AppExe) { Fail 'Monthwise is already installed. Use: monthwise.ps1 update' }
    if ((Test-Path $PgData) -and -not (Test-Path (Join-Path $SecretsDir 'SensitiveData__HashKey'))) {
        Fail ("The database exists but the secrets are missing in:`n  $SecretsDir`n" +
            'Put back the files saved in your password manager. New keys would make the stored card numbers and IBANs unreadable.')
    }
    New-Item -ItemType Directory -Force -Path $BaseDir, $NativeDir, $ConfigDir, $LogDir | Out-Null
    # Only this user account (and the system) can open the Monthwise folder.
    & icacls $BaseDir /inheritance:r /grant:r "${env:USERNAME}:(OI)(CI)F" /grant:r 'SYSTEM:(OI)(CI)F' | Out-Null
    if (-not (Test-Path (Join-Path $SecretsDir 'SensitiveData__HashKey'))) { New-SecretFile }
    if (-not (Test-Path $SettingsFile)) { Write-SettingFile '127.0.0.1' }

    Write-Host "Downloading Monthwise ($Version) and PostgreSQL..."
    $tmp = Get-ReleaseAsset $Version @("monthwise-*-$Runtime.zip", "postgresql-*-$Runtime.zip")
    if (-not (Test-Path $PgBin)) {
        Expand-Archive -Path (Get-ChildItem $tmp -Filter "postgresql-*-$Runtime.zip").FullName -DestinationPath $NativeDir
    }
    Expand-Archive -Path (Get-ChildItem $tmp -Filter "monthwise-*-$Runtime.zip").FullName -DestinationPath $AppDir -Force
    Remove-Item -Recurse -Force $tmp

    if (-not (Test-Path $PgData)) {
        Write-Host 'Creating the Monthwise database...'
        & (Join-Path $PgBin 'initdb.exe') --pgdata $PgData --username monthwise `
            --pwfile (Join-Path $SecretsDir 'postgres_password') --auth scram-sha-256 --encoding UTF8 `
            --locale-provider icu --icu-locale en-US --locale C | Out-Null
        if ($LASTEXITCODE -ne 0) { Fail 'Creating the database failed.' }
        Add-Content -Path (Join-Path $PgData 'postgresql.conf') -Value @(
            "port = $PgPort", "listen_addresses = '127.0.0.1'", 'logging_collector = off')
    }

    Register-LogonTask $PgTask (Join-Path $PgBin 'postgres.exe') "-D `"$PgData`""
    Register-LogonTask $AppTask $AppExe "--config-dir `"$ConfigDir`""
    Start-ScheduledTask -TaskName $PgTask
    for ($i = 0; $i -lt 30 -and -not (Test-PostgresServer); $i++) { Start-Sleep -Seconds 1 }
    $env:PGPASSWORD = Get-Content (Join-Path $SecretsDir 'postgres_password') -Raw
    $exists = & (Join-Path $PgBin 'psql.exe') -h 127.0.0.1 -p $PgPort -U monthwise -d postgres -tAc `
        "SELECT 1 FROM pg_database WHERE datname = 'monthwise'"
    if ("$exists".Trim() -ne '1') { & (Join-Path $PgBin 'createdb.exe') -h 127.0.0.1 -p $PgPort -U monthwise monthwise }
    Remove-Item Env:PGPASSWORD
    Start-ScheduledTask -TaskName $AppTask
    Copy-Item $PSCommandPath (Join-Path $NativeDir 'monthwise.ps1') -Force -ErrorAction SilentlyContinue

    if (-not (Wait-Ready)) { Fail 'Monthwise did not become ready. Its log: monthwise.ps1 logs' }
    Write-Host "Monthwise is running at $(Get-Address) and starts by itself when you log in."
    Write-Host "Manage it with: $(Join-Path $NativeDir 'monthwise.ps1') start|stop|update|status|logs|network|uninstall"
}

function Update-Monthwise([string] $Version) {
    Assert-Gh
    if (-not (Test-Path $AppExe)) { Fail 'Monthwise is not installed. Use: monthwise.ps1 install' }
    Write-Host "Downloading Monthwise ($Version)..."
    $new = Join-Path $NativeDir 'app.new'
    $previous = Join-Path $NativeDir 'app.previous'
    Expand-Release $Version $new
    Stop-App
    if (Test-Path $previous) { Remove-Item -Recurse -Force $previous }
    Move-Item $AppDir $previous
    Move-Item $new $AppDir
    Start-ScheduledTask -TaskName $AppTask
    if (Wait-Ready) {
        Write-Host "Monthwise is updated: $((Invoke-RestMethod "$(Get-Address)/api/health").version)"
        return
    }
    Write-Host 'The new version did not start; going back to the previous one.'
    Stop-App
    Remove-Item -Recurse -Force $AppDir
    Move-Item $previous $AppDir
    Start-ScheduledTask -TaskName $AppTask
    Fail 'Update failed. The previous version is running again; its log: monthwise.ps1 logs'
}

switch ($Command) {
    'install' { Install-Monthwise $(if ($Argument) { $Argument } else { 'latest' }) }
    'start' {
        Start-Monthwise
        if (Wait-Ready) { Write-Host "Monthwise is running at $(Get-Address)" } else { Fail 'Monthwise did not become ready.' }
    }
    'stop' {
        Stop-App
        Stop-PostgresServer
        Write-Host 'Monthwise is stopped until you log in again or run: monthwise.ps1 start'
    }
    'update' { Update-Monthwise $(if ($Argument) { $Argument } else { 'latest' }) }
    'status' {
        try { Invoke-RestMethod "$(Get-Address)/api/health" -TimeoutSec 2 } catch { Write-Host "Monthwise is not answering at $(Get-Address)." }
    }
    'logs' {
        $log = Get-ChildItem $LogDir -Filter 'monthwise-*.log' | Sort-Object LastWriteTime | Select-Object -Last 1
        if ($log) { Get-Content $log.FullName -Tail 200 } else { Write-Host 'No log yet.' }
    }
    'network' {
        if (-not (Test-Path $SettingsFile)) { Fail 'Monthwise is not installed.' }
        switch ($Argument) {
            'on' { Write-SettingFile '0.0.0.0' }
            'off' { Write-SettingFile '127.0.0.1' }
            default { Fail 'Usage: monthwise.ps1 network on|off' }
        }
        Stop-App
        Start-ScheduledTask -TaskName $AppTask
        if ($Argument -eq 'on') {
            Write-Host "Other devices on your network can now connect to port $AppPort of this computer (allow it in Windows Firewall when asked)."
            Write-Host 'Until login exists, anyone who can reach it can see and change all data.' -ForegroundColor Yellow
        }
        else { Write-Host 'Only this computer can connect to Monthwise.' }
    }
    'uninstall' {
        Stop-App
        Stop-PostgresServer
        Unregister-ScheduledTask -TaskName $AppTask -Confirm:$false -ErrorAction SilentlyContinue
        Unregister-ScheduledTask -TaskName $PgTask -Confirm:$false -ErrorAction SilentlyContinue
        foreach ($folder in @($AppDir, (Join-Path $NativeDir 'app.previous'), (Join-Path $NativeDir 'app.new'))) {
            if (Test-Path $folder) { Remove-Item -Recurse -Force $folder }
        }
        if ($Data) {
            $answer = Read-Host "Delete all Monthwise data and its secrets in $NativeDir? This cannot be undone. Type DELETE"
            if ($answer -ceq 'DELETE') {
                Remove-Item -Recurse -Force $NativeDir
                Write-Host 'Monthwise and its data are removed.'
                return
            }
        }
        Write-Host "Monthwise is uninstalled. The data, secrets, PostgreSQL and logs are kept in $NativeDir."
    }
    default {
        Write-Host 'Usage: monthwise.ps1 install [version] | start | stop | update [version] | status | logs | network on|off | uninstall [-Data]'
        exit 2
    }
}
