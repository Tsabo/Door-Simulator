#!/bin/sh
# DoorSim .deb post-remove. The card database and logs in /var/lib/doorsim are deliberately kept,
# even on purge — delete that directory by hand if you really want them gone.
set -e

if [ -d /run/systemd/system ]; then
    systemctl daemon-reload || true
fi

if command -v udevadm >/dev/null 2>&1; then
    udevadm control --reload-rules || true
fi
