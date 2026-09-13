param(
    [ValidateSet('Deploy', 'Debug')]
    [string]$Mode = 'Deploy',
    [Parameter(Mandatory = $true)]
    [string]$RemoteUser,
    [Parameter(Mandatory = $true)]
    [string]$RemoteHost,
    [string]$InstallPath,
    [string]$ServiceName = 'doorsim',
    [string]$PublishDir = '/tmp/doorsim-publish',
    [string]$RemoteServiceUnitDir = '/etc/systemd/system'
)

$ErrorActionPreference = 'Stop'
if (-not $InstallPath) {
    $InstallPath = "/home/$RemoteUser/doorsim"
}
$serviceUnitName = if ($ServiceName.EndsWith('.service')) { $ServiceName } else { "$ServiceName.service" }
$serviceUnitTemplate = Join-Path -Path (Split-Path -Parent $PSCommandPath) -ChildPath 'doorsim.service.template'

function Invoke-RemoteCommand {
    param(
        [string]$Command
    )

    $remoteTarget = "$RemoteUser@$RemoteHost"
    & ssh $remoteTarget $Command
    if ($LASTEXITCODE -ne 0) {
        throw "Remote command failed ($LASTEXITCODE): $Command"
    }
}

function Copy-ToRemote {
    param(
        [string]$Source,
        [string]$Destination
    )

    & scp -r $Source $Destination
    if ($LASTEXITCODE -ne 0) {
        throw "Copy failed ($LASTEXITCODE): $Source -> $Destination"
    }
}

function Sync-RemoteServiceUnit {
    param(
        [string]$RemoteTarget,
        [string]$UnitName,
        [string]$UnitTemplate,
        [string]$RemoteUnitDir,
        [string]$DeployUser,
        [string]$DeployInstallPath
    )

    if (-not (Test-Path -LiteralPath $UnitTemplate)) {
        throw "Service unit template was not found: $UnitTemplate"
    }

    Write-Host "Syncing service unit $UnitName on $RemoteTarget..."

    $rendered = (Get-Content -LiteralPath $UnitTemplate -Raw) `
        -replace '__DEPLOY_USER__', $DeployUser `
        -replace '__INSTALL_PATH__', $DeployInstallPath

    $renderedUnitFile = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath $UnitName
    Set-Content -LiteralPath $renderedUnitFile -Value $rendered -NoNewline

    $remoteTempUnit = "/tmp/$UnitName"
    Copy-ToRemote -Source $renderedUnitFile -Destination "${RemoteTarget}:$remoteTempUnit"
    Remove-Item -LiteralPath $renderedUnitFile -Force
    Invoke-RemoteCommand "sudo install -m 644 '$remoteTempUnit' '$RemoteUnitDir/$UnitName'; rm -f '$remoteTempUnit'; sudo systemctl daemon-reload; sudo systemctl enable '$UnitName'"
}

$remoteTarget = "$RemoteUser@$RemoteHost"
$configuration = if ($Mode -eq 'Deploy') { 'Release' } else { 'Debug' }

Sync-RemoteServiceUnit -RemoteTarget $remoteTarget -UnitName $serviceUnitName -UnitTemplate $serviceUnitTemplate -RemoteUnitDir $RemoteServiceUnitDir -DeployUser $RemoteUser -DeployInstallPath $InstallPath

Write-Host "Publishing DoorSim for linux-arm64 ($configuration)..."
Remove-Item -Recurse -Force $PublishDir -ErrorAction SilentlyContinue
dotnet publish 'source/src/DoorSim' -c $configuration -r linux-arm64 --no-self-contained --force -o $PublishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)" }

Write-Host "Stopping $ServiceName on $remoteTarget..."
Invoke-RemoteCommand "sudo systemctl stop $ServiceName"

# A prior "Deploy & Debug on Pi" session that disconnected uncleanly (VS Code closed,
# network drop, etc.) leaves vsdbg and the debuggee running outside systemd's control.
# That orphan keeps holding the HTTP port and every OSDP serial device, so the
# systemd-managed service can never bind and crash-loops forever fighting it for the
# same serial ports. Kill any stray copy before starting a fresh one.
Write-Host "Checking for orphaned DoorSim/vsdbg processes on $remoteTarget..."
Invoke-RemoteCommand "sudo pkill -9 -f '^$InstallPath/DoorSim$' 2>/dev/null; sudo pkill -9 -f '^/home/$RemoteUser/vsdbg/vsdbg$' 2>/dev/null; true"

Write-Host "Refreshing $InstallPath on $remoteTarget..."
Invoke-RemoteCommand "mkdir -p '$InstallPath'; find '$InstallPath' -mindepth 1 -maxdepth 1 ! -name '*.db' ! -name 'appsettings.Production.json' -exec rm -rf {} +"

Write-Host "Copying new build to $remoteTarget..."
Get-ChildItem -Force $PublishDir | ForEach-Object {
    Copy-ToRemote -Source $_.FullName -Destination "${remoteTarget}:$InstallPath/"
}

Write-Host "Making DoorSim executable on $remoteTarget..."
Invoke-RemoteCommand "chmod +x '$InstallPath/DoorSim'"

# wait-for-rs485.sh lives in scripts/, not in the publish output, and the
# refresh above wipes InstallPath - so it has to be copied after that point.
# doorsim.service runs it as ExecStartPre to gate startup on udev.
Write-Host "Copying wait-for-rs485.sh to $remoteTarget..."
$waitScriptSource = Join-Path -Path (Split-Path -Parent $PSCommandPath) -ChildPath 'wait-for-rs485.sh'
if (-not (Test-Path -LiteralPath $waitScriptSource)) {
    throw "Startup gate script was not found: $waitScriptSource"
}
Copy-ToRemote -Source $waitScriptSource -Destination "${remoteTarget}:$InstallPath/wait-for-rs485.sh"
Invoke-RemoteCommand "chmod +x '$InstallPath/wait-for-rs485.sh'"

if ($Mode -eq 'Deploy') {
    Write-Host "Starting $ServiceName on $remoteTarget..."
    Invoke-RemoteCommand "sudo systemctl start $ServiceName && sudo systemctl is-active --quiet $ServiceName"
    Write-Host "Deploy complete."
}
else {
    Write-Host "Debug prep complete. Leave the service stopped before attaching the debugger."
}