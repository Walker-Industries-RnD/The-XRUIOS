# Registers XRUIOS.Manager to auto-start at user logon (user session; can create OS accounts + see the
# desktop). Run from the folder that holds XRUIOS.Manager.exe, or pass -Exe.
param([string]$Exe = "$PSScriptRoot\XRUIOS.Manager.exe")
& $Exe install
