#!/bin/sh
# DoorSim .deb post-install: runs on first install and on every upgrade.
set -e

# Dedicated service account; home is the data directory systemd's StateDirectory= creates.
if ! getent passwd doorsim >/dev/null; then
    useradd --system --user-group --home-dir /var/lib/doorsim --no-create-home \
        --shell /usr/sbin/nologin --comment "DoorSim service" doorsim
fi

# Serial ports (dialout) and GPIO (gpio, Raspberry Pi OS only) — only groups that exist here.
for group in dialout gpio; do
    if getent group "$group" >/dev/null; then
        usermod -a -G "$group" doorsim
    fi
done

if [ -f /etc/systemd/system/doorsim.service ]; then
    echo "WARNING: /etc/systemd/system/doorsim.service (from deploy-to-pi.ps1) overrides the packaged" >&2
    echo "         unit. Remove it to use the package:  sudo rm /etc/systemd/system/doorsim.service" >&2
fi

if command -v udevadm >/dev/null 2>&1; then
    udevadm control --reload-rules || true
    udevadm trigger --subsystem-match=tty || true
fi

if [ -d /run/systemd/system ]; then
    systemctl daemon-reload
    systemctl enable doorsim.service
    systemctl restart doorsim.service || true
fi
