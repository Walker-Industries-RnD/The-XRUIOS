# Installs XRUIOS.Manager as a Windows Service that starts at boot (session 0). RUN AS ADMINISTRATOR.
param([string]$Exe = "$PSScriptRoot\XRUIOS.Manager.exe")
& $Exe install-service
Write-Host "Installed. Start now with:  sc.exe start XRUIOS.Manager"
