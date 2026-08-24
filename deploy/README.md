# Deploying XRUIOS.Manager

One Manager per device; it supervises the per-class workers and holds the keys.

## Start it now
- `XRUIOS.Manager login`  — set up / unlock the account (seals the key to this OS session)
- `XRUIOS.Manager start`  — launch it detached in the background
- `XRUIOS.Manager status` / `stop`

## Auto-start
- **At logon (user session):** `XRUIOS.Manager install`  (Windows Scheduled Task / Linux systemd --user)
- **At boot (Windows Service):** run `deploy/windows/install-service.ps1` as Administrator
- Remove with `uninstall` / `uninstall-service`.

Workers are found next to the Manager, or point `XRUIOS_WORKERS_ROOT` at the deploy folder.
