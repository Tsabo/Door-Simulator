#!/bin/sh
# DoorSim .deb pre-remove. $1 is "remove" for an uninstall, "upgrade" when a new version replaces this one.
set -e

if [ -d /run/systemd/system ]; then
    if [ "$1" = "remove" ]; then
        systemctl disable --now doorsim.service || true
    else
        systemctl stop doorsim.service || true
    fi
fi
