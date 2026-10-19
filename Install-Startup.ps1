$ErrorActionPreference = 'Stop'
$exe = Join-Path $PSScriptRoot 'MxThumbWheelReader.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Rozbalte release ZIP a spusťte tento skript vedle EXE.' }
$settings = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'appsettings.json') -Raw | ConvertFrom-Json
if ($null -eq $settings.DesiredDirectionInverted) { throw 'Nejdřív spusťte EXE s --inverted nebo --normal.' }
$user = [Security.Principal.WindowsIdentity]::GetCurrent().Name
$sid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$taskName = "MxThumbWheelReader-$sid"
$escapedExe = $exe.Replace("'", "''")
$command = "& '$escapedExe' --watch; exit `$LASTEXITCODE"
$encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))
$action = New-ScheduledTaskAction -Execute "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -Argument "-NoProfile -NonInteractive -WindowStyle Hidden -EncodedCommand $encoded" -WorkingDirectory $PSScriptRoot
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $user
$principal = New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Limited
$options = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit ([TimeSpan]::Zero) -MultipleInstances IgnoreNew -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1)
if (Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue) { Stop-ScheduledTask -TaskName $taskName }
Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Principal $principal -Settings $options -Force | Out-Null
Start-ScheduledTask -TaskName $taskName
Write-Host 'Hlídání spuštěno skrytě a nastaveno po přihlášení. Tuto složku nepřesouvejte.'
