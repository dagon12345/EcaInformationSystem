#!/usr/bin/env bash
# ECA-InFORMS biometric local sync — systemd service installer (Linux
# equivalent of windows/install-service.bat).
#
# Usage: copy this file, uninstall-service.sh, and patch-appsettings.py
# directly into the SAME folder as EcaInformationSystem.Api.dll (the
# `dotnet publish` output), then run:  sudo ./install-service.sh
#
# Safe to re-run: if the service already exists, it stops/removes the old
# one first, so this also works as the "update" procedure — just republish
# over this folder (keeping these 3 files) and re-run the script.

set -euo pipefail

if [ "$EUID" -ne 0 ]; then
    echo "This needs root (to install a systemd service). Re-run with: sudo $0"
    exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DLL_PATH="$SCRIPT_DIR/EcaInformationSystem.Api.dll"
SERVICE_NAME="eca-localsync"
UNIT_PATH="/etc/systemd/system/${SERVICE_NAME}.service"

echo
echo "============================================================"
echo " ECA-InFORMS Biometric Local Sync — Service Installer"
echo "============================================================"
echo " Install folder: $SCRIPT_DIR"
echo

if [ ! -f "$DLL_PATH" ]; then
    echo "ERROR: $DLL_PATH not found."
    echo "Make sure this script sits in the SAME folder as EcaInformationSystem.Api.dll"
    echo "(the \`dotnet publish\` output folder)."
    exit 1
fi

DOTNET_PATH="$(command -v dotnet || true)"
if [ -z "$DOTNET_PATH" ]; then
    echo "ERROR: 'dotnet' not found on PATH. Install the .NET runtime first."
    exit 1
fi

if ! command -v python3 >/dev/null 2>&1; then
    echo "ERROR: python3 not found — needed to update appsettings.json. Install python3 first."
    exit 1
fi

# Run as the invoking (non-root) user if this was launched via sudo — the
# service doesn't need root to run, only this installer does (to write the
# systemd unit file and manage the service).
RUN_AS_USER="${SUDO_USER:-$(whoami)}"

echo "Applying required settings to appsettings.json..."
python3 "$SCRIPT_DIR/patch-appsettings.py"

echo "Stopping and removing any existing service..."
systemctl stop "$SERVICE_NAME" >/dev/null 2>&1 || true
systemctl disable "$SERVICE_NAME" >/dev/null 2>&1 || true

echo "Writing systemd unit..."
cat > "$UNIT_PATH" <<EOF
[Unit]
Description=ECA-InFORMS biometric device local sync bridge
After=network.target

[Service]
Type=simple
User=${RUN_AS_USER}
WorkingDirectory=${SCRIPT_DIR}
ExecStart=${DOTNET_PATH} ${DLL_PATH} --urls=http://localhost:5010
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
EOF

echo "Enabling and starting service..."
systemctl daemon-reload
systemctl enable --now "$SERVICE_NAME"

sleep 3

echo
echo "-- Checking it's actually up ---------------------------------"
if command -v curl >/dev/null 2>&1; then
    code=$(curl -s -o /dev/null -w "%{http_code}" -X POST http://localhost:5010/api/dtr/test-connection || echo "000")
    if [ "$code" = "401" ]; then
        echo "Responded with HTTP 401 Unauthorized — this means it IS running correctly."
    elif [ "$code" = "000" ]; then
        echo "NOT RESPONDING — check: journalctl -u ${SERVICE_NAME} -n 50"
    else
        echo "Responded with HTTP $code — unexpected, but it is at least running."
    fi
else
    echo "(curl not available to verify — check manually: systemctl status ${SERVICE_NAME})"
fi

echo
echo "============================================================"
echo " Done. The service will now start automatically every time"
echo " this machine boots — nothing else to run, ever."
echo " Logs: journalctl -u ${SERVICE_NAME} -f"
echo "============================================================"
