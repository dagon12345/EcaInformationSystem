#!/usr/bin/env bash
# Removes the eca-localsync systemd service. Doesn't touch any files —
# safe to run before deleting the install folder, or before copying a
# fresh republish over it (install-service.sh also does this
# automatically, so you don't normally need to run this separately).

set -euo pipefail

if [ "$EUID" -ne 0 ]; then
    echo "This needs root. Re-run with: sudo $0"
    exit 1
fi

SERVICE_NAME="eca-localsync"

if ! systemctl list-unit-files "${SERVICE_NAME}.service" >/dev/null 2>&1 || \
   ! systemctl list-unit-files "${SERVICE_NAME}.service" | grep -q "$SERVICE_NAME"; then
    echo "Service ${SERVICE_NAME} is not installed — nothing to do."
    exit 0
fi

echo "Stopping service..."
systemctl stop "$SERVICE_NAME" >/dev/null 2>&1 || true

echo "Disabling service..."
systemctl disable "$SERVICE_NAME" >/dev/null 2>&1 || true

echo "Removing unit file..."
rm -f "/etc/systemd/system/${SERVICE_NAME}.service"
systemctl daemon-reload

echo "Done."
