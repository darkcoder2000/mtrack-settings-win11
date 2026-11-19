<#
.SYNOPSIS
  Restores the original M-Audio panel (MAPanel.exe) and removes MTrackPanel.exe.
#>
$ErrorActionPreference = 'Stop'

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Start-Process powershell.exe -Verb RunAs -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"")
    return
}

$driverKey = 'HKLM:\SOFTWARE\WOW6432Node\MAudio\Audio\Drivers\{CE869237-0350-49E7-BE8D-57C8EA29C0FD}'
$runKey = 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run'
$runName = 'M-Audio Panel Launcher'
$backupFile = Join-Path $PSScriptRoot 'backup.json'

$driver = Get-ItemProperty $driverKey
$installDir = if ($driver.SoftwarePath) { $driver.SoftwarePath } else { 'C:\Program Files (x86)\M-Audio\M-Track\' }

if (Test-Path $backupFile) {
    $b = Get-Content $backupFile -Raw | ConvertFrom-Json
} else {
    Write-Warning 'backup.json not found - restoring the M-Audio defaults.'
    $b = [pscustomobject]@{
        ControlPanelApp = 'MAPanel.exe'
        RunValue        = 'C:\PROGRA~2\M-Audio\M-Track\MAPanel.exe 0'
        InstalledExe    = Join-Path $installDir 'MTrackPanel.exe'
    }
}

Get-Process MTrackPanel -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

if ($b.ControlPanelApp) { Set-ItemProperty $driverKey -Name ControlPanelApp -Value $b.ControlPanelApp }
if ($b.RunValue) { Set-ItemProperty $runKey -Name $runName -Value $b.RunValue }
else { Remove-ItemProperty $runKey -Name $runName -ErrorAction SilentlyContinue }

if ($b.InstalledExe -and (Test-Path $b.InstalledExe)) { Remove-Item $b.InstalledExe -Force }
if (Test-Path $backupFile) { Remove-Item $backupFile -Force }

Write-Host 'Original M-Audio panel restored.' -ForegroundColor Green
Start-Sleep -Seconds 3
