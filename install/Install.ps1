<#
.SYNOPSIS
  Replaces the old M-Audio M-Track control panel (MAPanel.exe) with the new MTrackPanel.exe.

.DESCRIPTION
  - Copies MTrackPanel.exe next to the original panel (C:\Program Files (x86)\M-Audio\M-Track\).
  - Points the driver's "ControlPanelApp" registry value at it, so the ASIO "Control Panel"
    button in DAWs opens the new panel.
  - Replaces the "M-Audio Panel Launcher" autostart entry so the new panel starts in the tray.
  - Saves the original values to backup.json (used by Uninstall.ps1). The driver and the old
    panel files are not modified or deleted.

  Run from an elevated PowerShell, or let the script elevate itself.

.PARAMETER Build
  Publish the app from source first (requires the .NET 10 SDK).
#>
param([switch]$Build)

$ErrorActionPreference = 'Stop'

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    $argList = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"")
    if ($Build) { $argList += '-Build' }
    Start-Process powershell.exe -Verb RunAs -ArgumentList $argList
    return
}

$repo = Split-Path -Parent $PSScriptRoot
$source = Join-Path $repo 'publish\MTrackPanel.exe'

if ($Build -or -not (Test-Path $source)) {
    Write-Host 'Publishing MTrackPanel...'
    & dotnet publish (Join-Path $repo 'src\MTrackPanel') -c Release -o (Join-Path $repo 'publish') -nologo
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }
}

$driverKey = 'HKLM:\SOFTWARE\WOW6432Node\MAudio\Audio\Drivers\{CE869237-0350-49E7-BE8D-57C8EA29C0FD}'
$runKey = 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run'
$runName = 'M-Audio Panel Launcher'
$backupFile = Join-Path $PSScriptRoot 'backup.json'

if (-not (Test-Path $driverKey)) { throw "M-Track driver registry key not found ($driverKey). Is the M-Audio M-Track driver installed?" }

$driver = Get-ItemProperty $driverKey
$targetDir = if ($driver.SoftwarePath) { $driver.SoftwarePath } else { 'C:\Program Files (x86)\M-Audio\M-Track\' }
$target = Join-Path $targetDir 'MTrackPanel.exe'

# Back up the original values only once, so re-running the installer keeps the true originals.
if (-not (Test-Path $backupFile)) {
    $run = Get-ItemProperty $runKey -ErrorAction SilentlyContinue
    [ordered]@{
        ControlPanelApp = $driver.ControlPanelApp
        RunValue        = if ($run) { $run.$runName } else { $null }
        InstalledExe    = $target
        Date            = (Get-Date).ToString('s')
    } | ConvertTo-Json | Set-Content -Encoding UTF8 $backupFile
    Write-Host "Saved original settings to $backupFile"
}

Write-Host 'Stopping old and running panels...'
Get-Process MAPanel, MTrackPanel -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

Write-Host "Copying to $target"
Copy-Item $source $target -Force

Set-ItemProperty $driverKey -Name ControlPanelApp -Value 'MTrackPanel.exe'
Set-ItemProperty $runKey -Name $runName -Value "`"$target`" 0"

# Start the panel for the logged-on user (non-elevated) via Explorer.
Start-Process explorer.exe -ArgumentList "`"$target`""

Write-Host ''
Write-Host 'Done. The new M-Track panel is installed and will start in the tray at logon.' -ForegroundColor Green
Write-Host 'Run Uninstall.ps1 to restore the original panel.'
Start-Sleep -Seconds 3
