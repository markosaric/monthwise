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
$BackupDir = Join-Path $NativeDir 'update-backups'
$KeepUpdateBackups = 3
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

# Runs a PostgreSQL tool of this installation, signed in to its database server, and returns
# whether it succeeded. The password is set for this one call only.
function Invoke-PgTool([string] $Tool, [string[]] $Arguments) {
    $env:PGPASSWORD = Get-Content (Join-Path $SecretsDir 'postgres_password') -Raw
    try {
        & (Join-Path $PgBin "$Tool.exe") -h 127.0.0.1 -p $PgPort -U monthwise @Arguments *> $null
        return $LASTEXITCODE -eq 0
    }
    finally { Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue }
}

function Start-Database {
    if (Test-PostgresServer) { return $true }
    Start-ScheduledTask -TaskName $PgTask
    for ($i = 0; $i -lt 30; $i++) {
        if (Test-PostgresServer) { return $true }
        Start-Sleep -Seconds 1
    }
    return $false
}

function Get-RunningVersion {
    try { return (Invoke-RestMethod -Uri "$(Get-Address)/api/health" -TimeoutSec 2).version }
    catch { return $null }
}

# Saves a complete copy of the database before an update (FR-3a, Appendix 3) and returns its file,
# or nothing when it failed. The Monthwise folder, and so this file, is private to this user account.
function New-UpdateBackup([string] $FromVersion) {
    New-Item -ItemType Directory -Force -Path $BackupDir | Out-Null
    $safeVersion = $FromVersion -replace '[^A-Za-z0-9._-]', '_'
    $file = Join-Path $BackupDir "monthwise-$(Get-Date -Format 'yyyyMMdd-HHmmss')-$safeVersion.dump"
    if (-not (Start-Database)) { return $null }
    # Written to .partial first, so a half-written file never looks like a backup.
    if (-not (Invoke-PgTool 'pg_dump' @('-d', 'monthwise', '--format=custom', '--file', "$file.partial"))) {
        Remove-Item -Force -ErrorAction SilentlyContinue "$file.partial"
        return $null
    }
    Move-Item "$file.partial" $file
    # The names start with the date and time, so sorting by name keeps the newest.
    Get-ChildItem -Path $BackupDir -Filter 'monthwise-*.dump' | Sort-Object Name |
        Select-Object -SkipLast $KeepUpdateBackups | Remove-Item -Force
    return $file
}

# Puts the database back exactly as it was in the update backup, with the app stopped. The copy is
# restored into a database of its own and swapped in only once it is complete, so a failed restore
# leaves the database as it is.
function Restore-UpdateBackup([string] $File) {
    Stop-App
    if (-not (Start-Database)) { return $false }
    return (Invoke-PgTool 'dropdb' @('--if-exists', 'monthwise_restore')) -and
        (Invoke-PgTool 'createdb' @('monthwise_restore')) -and
        (Invoke-PgTool 'pg_restore' @('-d', 'monthwise_restore', '--no-owner', '--exit-on-error', $File)) -and
        (Invoke-PgTool 'psql' @('-d', 'postgres', '-v', 'ON_ERROR_STOP=1', '-q',
            '-c', "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = 'monthwise'",
            '-c', 'ALTER DATABASE monthwise RENAME TO monthwise_before_restore',
            '-c', 'ALTER DATABASE monthwise_restore RENAME TO monthwise')) -and
        (Invoke-PgTool 'dropdb' @('--force', 'monthwise_before_restore'))
}

function Update-Monthwise([string] $Version) {
    Assert-Gh
    if (-not (Test-Path $AppExe)) { Fail 'Monthwise is not installed. Use: monthwise.ps1 install' }
    $fromVersion = Get-RunningVersion
    if (-not $fromVersion) { $fromVersion = 'unknown' }
    Write-Host "Downloading Monthwise ($Version)..."
    $new = Join-Path $NativeDir 'app.new'
    $previous = Join-Path $NativeDir 'app.previous'
    Expand-Release $Version $new

    # The app is stopped first, so nothing changes in the database after its backup.
    Write-Host 'Saving an update backup of the database...'
    Stop-App
    $backup = New-UpdateBackup $fromVersion
    if (-not $backup) {
        Remove-Item -Recurse -Force $new
        Start-ScheduledTask -TaskName $AppTask
        Fail 'The update backup failed, so nothing was updated. Its log: monthwise.ps1 logs'
    }
    Write-Host "Update backup: $backup"

    if (Test-Path $previous) { Remove-Item -Recurse -Force $previous }
    Move-Item $AppDir $previous
    Move-Item $new $AppDir
    Start-ScheduledTask -TaskName $AppTask
    if (Wait-Ready) {
        Write-Host "Monthwise is updated: $(Get-RunningVersion)"
        return
    }

    Write-Host 'The new version did not start. Putting the data back from the update backup and going back to the previous version...'
    if (Restore-UpdateBackup $backup) {
        Remove-Item -Recurse -Force $AppDir
        Move-Item $previous $AppDir
        Start-ScheduledTask -TaskName $AppTask
        if (Wait-Ready) {
            Fail ("The update failed. Your data is as it was before the update, and the previous version is running again.`n" +
                "Update backup: $backup`nLog: monthwise.ps1 logs")
        }
        Fail ("The update failed. Your data is as it was before the update, but the previous version did not start either.`n" +
            "Update backup: $backup`nLog: monthwise.ps1 logs")
    }
    # The new version stays installed: it refuses to work on a database it could not bring up to date.
    Stop-App
    Fail ("The update failed, and putting the data back failed too, so Monthwise is stopped.`n" +
        "Your data is in the update backup: $backup`n" +
        'The user manual explains how to restore it by hand ("Restoring an update backup by hand").')
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
            $answer = Read-Host "Delete all Monthwise data, its update backups and its secrets in $NativeDir? This cannot be undone. Type DELETE"
            if ($answer -ceq 'DELETE') {
                Remove-Item -Recurse -Force $NativeDir
                Write-Host 'Monthwise and its data are removed.'
                return
            }
        }
        Write-Host "Monthwise is uninstalled. The data, update backups, secrets, PostgreSQL and logs are kept in $NativeDir."
    }
    default {
        Write-Host 'Usage: monthwise.ps1 install [version] | start | stop | update [version] | status | logs | network on|off | uninstall [-Data]'
        exit 2
    }
}
