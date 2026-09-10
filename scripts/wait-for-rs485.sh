#!/bin/sh
# Gate DoorSim startup on udev having created the RS485 symlinks.
#
# DoorSim opens its OSDP ports by udev symlink name
# (/dev/ttyRS485_<device>_<channel>, created by
# /etc/udev/rules.d/99-waveshare485.rules). Those symlinks do not exist until
# udev has processed the USB coldplug events, which on a Pi 4 lands several
# seconds after network-online.target. Without this gate DoorSim starts first
# and logs "Failed to open serial port" in a retry loop until udev catches up.
# That is noisy, it makes every boot look broken, and it buries real faults
# under hundreds of identical lines.
#
# Exits 0 unconditionally. A genuinely absent adapter must not block startup,
# because the readers on the remaining adapters should still come online.
# DoorSim's own retry loop covers a late or missing device.

TIMEOUT=30

# Drain the udev event queue (coldplug processing). Safe to run unprivileged.
if ! udevadm settle --timeout="$TIMEOUT"; then
    echo "doorsim: udevadm settle timed out after ${TIMEOUT}s" >&2
fi

# cdc_acm can bind after settle returns, so poll briefly for the first symlink.
i=0
while [ "$i" -lt "$TIMEOUT" ]; do
    for link in /dev/ttyRS485_*; do
        if [ -e "$link" ]; then
            echo "doorsim: RS485 symlinks present after ${i}s (first: $link)"
            exit 0
        fi
    done
    i=$((i + 1))
    sleep 1
done

echo "doorsim: no /dev/ttyRS485_* symlinks after ${TIMEOUT}s —" \
     "check 99-waveshare485.rules, USB cabling, and lsusb" >&2
exit 0
