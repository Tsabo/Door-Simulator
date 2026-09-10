#!/usr/bin/env bash
# =============================================================================
# DoorSim — Raspberry Pi One-Time Setup Script
# =============================================================================
# Run this once on the Pi before deploying DoorSim.
# Tested on Raspberry Pi OS (Bookworm / 64-bit) on Pi 4.
#
# Usage:
#   chmod +x setup-pi.sh
#   sudo ./setup-pi.sh
#
# What this script does:
#   1. Disables the serial console that occupies /dev/ttyAMA0
#   2. Disables Bluetooth to free the PL011 UART (/dev/ttyAMA0)
#   2b. Enables UART2 on GPIO0/1 (/dev/ttyAMA2)
#   3. Adds the run user to the 'dialout' group (serial port access)
#   4. Adds the run user to the 'gpio' group (GPIO access)
#   5. Creates the DoorSim data directory
#   6. Installs 99-waveshare485.rules if found beside this script
#   7. Prints a reboot reminder
#
# 📡  WHERE THE OSDP BUSES ACTUALLY COME FROM
#   The current rig runs OSDP over USB→quad-RS-485 adapters on a powered hub,
#   named by udev as /dev/ttyRS485_<device>_<channel>. That is what step 6
#   installs the rules for, and it is what the doors in the database point at.
#   DPS/REX contacts run over a Modbus TCP relay board, configured per door.
#
#   Steps 1, 2 and 2b free the Pi's own GPIO UARTs (/dev/ttyAMA0, /dev/ttyAMA2).
#   A USB-adapter rig does not need them, but they are left enabled so the
#   GPIO-UART and Modbus-RTU paths stay available.
#
#   See RS485_ADAPTERS.md for adding an adapter, and PI_SETUP.md for deployment.
# =============================================================================

set -euo pipefail

# -----------------------------------------------------------------------------
# Helpers
# -----------------------------------------------------------------------------
info()  { echo -e "\033[0;32m[INFO]\033[0m  $*"; }
warn()  { echo -e "\033[0;33m[WARN]\033[0m  $*"; }
error() { echo -e "\033[0;31m[ERROR]\033[0m $*" >&2; exit 1; }

if [[ $EUID -ne 0 ]]; then
    error "This script must be run as root: sudo $0"
fi

# Detect the real user even when run with sudo
REAL_USER="${SUDO_USER:-$(logname 2>/dev/null || echo '')}"
if [[ -z "$REAL_USER" ]]; then
    error "Could not determine the non-root user. Run with: sudo -u <user> $0"
fi
info "Configuring for user: $REAL_USER"

CONFIG_TXT="/boot/firmware/config.txt"
CMDLINE_TXT="/boot/firmware/cmdline.txt"

# Fall back to legacy paths (older Pi OS)
[[ -f "$CONFIG_TXT" ]]  || CONFIG_TXT="/boot/config.txt"
[[ -f "$CMDLINE_TXT" ]] || CMDLINE_TXT="/boot/cmdline.txt"

# -----------------------------------------------------------------------------
# 1. Disable the serial console
# -----------------------------------------------------------------------------
info "Step 1: Disabling serial console on /dev/ttyAMA0..."

if grep -q "console=serial0" "$CMDLINE_TXT"; then
    sed -i 's/console=serial0,[0-9]* //' "$CMDLINE_TXT"
    info "  Removed console=serial0 from $CMDLINE_TXT"
else
    info "  Already removed (or was never set)"
fi

if systemctl is-enabled serial-getty@ttyAMA0.service &>/dev/null; then
    systemctl disable --now serial-getty@ttyAMA0.service
    info "  Disabled serial-getty@ttyAMA0.service"
else
    info "  serial-getty@ttyAMA0.service already disabled"
fi

# -----------------------------------------------------------------------------
# 2. Disable Bluetooth to free PL011 UART → /dev/ttyAMA0
# -----------------------------------------------------------------------------
info "Step 2: Disabling Bluetooth to free PL011 UART..."

if grep -q "dtoverlay=disable-bt" "$CONFIG_TXT"; then
    info "  dtoverlay=disable-bt already present"
else
    echo "" >> "$CONFIG_TXT"
    echo "# DoorSim: free PL011 UART for RS-485/OSDP by disabling Bluetooth" >> "$CONFIG_TXT"
    echo "dtoverlay=disable-bt" >> "$CONFIG_TXT"
    info "  Added dtoverlay=disable-bt to $CONFIG_TXT"
fi

# Also disable the Bluetooth services (saves startup time / resources)
for svc in hciuart.service bluetooth.service; do
    systemctl disable --now "$svc" 2>/dev/null || true
done

# -----------------------------------------------------------------------------
# 2b. Enable UART2 (GPIO0/1 → /dev/ttyAMA1) for Modbus RS-485 relay module
# -----------------------------------------------------------------------------
info "Step 2b: Enabling UART2 (GPIO0/1 → /dev/ttyAMA2) for Modbus RS-485..."

if grep -q "dtoverlay=uart2" "$CONFIG_TXT"; then
    info "  dtoverlay=uart2 already present"
else
    echo "" >> "$CONFIG_TXT"
    echo "# DoorSim: UART2 on GPIO0/1 for Modbus RS-485 relay module" >> "$CONFIG_TXT"
    echo "dtoverlay=uart2" >> "$CONFIG_TXT"
    info "  Added dtoverlay=uart2 to $CONFIG_TXT"
fi

# -----------------------------------------------------------------------------
# 3. Add user to dialout group (serial port access)
# -----------------------------------------------------------------------------
info "Step 3: Adding $REAL_USER to 'dialout' group..."
if groups "$REAL_USER" | grep -q '\bdialout\b'; then
    info "  Already in dialout"
else
    usermod -aG dialout "$REAL_USER"
    info "  Done"
fi

# -----------------------------------------------------------------------------
# 4. Add user to gpio group (GPIO access)
# -----------------------------------------------------------------------------
info "Step 4: Adding $REAL_USER to 'gpio' group..."
if groups "$REAL_USER" | grep -q '\bgpio\b'; then
    info "  Already in gpio"
else
    usermod -aG gpio "$REAL_USER"
    info "  Done"
fi

# -----------------------------------------------------------------------------
# 5. Create data directory
# -----------------------------------------------------------------------------
DATA_DIR="/var/lib/doorsim"
info "Step 5: Creating data directory $DATA_DIR..."
mkdir -p "$DATA_DIR"
chown "$REAL_USER:$REAL_USER" "$DATA_DIR"
info "  Done (owner: $REAL_USER)"

# -----------------------------------------------------------------------------
# 6. Install the USB→RS-485 udev rules (if shipped alongside this script)
# -----------------------------------------------------------------------------
# Without these, the quad-RS-485 adapters only appear as /dev/ttyACM* with
# boot-order-dependent numbering, and the doors in the database - which point at
# /dev/ttyRS485_* - can never open their ports.
RULES_NAME="99-waveshare485.rules"
RULES_DEST="/etc/udev/rules.d/$RULES_NAME"
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"

info "Step 6: Installing $RULES_NAME..."
RULES_SRC=""
for candidate in "$SCRIPT_DIR/$RULES_NAME" "$SCRIPT_DIR/../$RULES_NAME" "$HOME/$RULES_NAME" "/home/$REAL_USER/$RULES_NAME"; do
    if [[ -f "$candidate" ]]; then
        RULES_SRC="$candidate"
        break
    fi
done

if [[ -n "$RULES_SRC" ]]; then
    install -m 644 "$RULES_SRC" "$RULES_DEST"
    udevadm control --reload-rules
    udevadm trigger --subsystem-match=tty
    info "  Installed from $RULES_SRC and reloaded udev"
    LINK_COUNT="$(find /dev -maxdepth 1 -name 'ttyRS485_*' 2>/dev/null | wc -l)"
    info "  /dev/ttyRS485_* symlinks now present: $LINK_COUNT"
    if [[ "$LINK_COUNT" -eq 0 ]]; then
        warn "  No symlinks yet. If adapters are plugged in, the rules may not"
        warn "  match this host's USB paths — see RS485_ADAPTERS.md step 1."
    fi
else
    warn "  $RULES_NAME not found next to this script — SKIPPED."
    warn "  Copy it to the Pi and install it manually, or OSDP ports will not appear:"
    warn "    sudo install -m 644 $RULES_NAME $RULES_DEST"
    warn "    sudo udevadm control --reload-rules && sudo udevadm trigger --subsystem-match=tty"
fi

# -----------------------------------------------------------------------------
# Verify critical config.txt entries were written
# -----------------------------------------------------------------------------
info "Verifying config.txt entries..."
for overlay in "disable-bt" "uart2"; do
    if grep -q "dtoverlay=$overlay" "$CONFIG_TXT"; then
        info "  dtoverlay=$overlay ✓"
    else
        error "  dtoverlay=$overlay NOT found in $CONFIG_TXT — setup incomplete!"
    fi
done

# -----------------------------------------------------------------------------
# Done
# -----------------------------------------------------------------------------
echo ""
echo "============================================================"
echo "  DoorSim Pi setup complete."
echo ""
echo "  ⚠️  A REBOOT IS REQUIRED for all changes to take effect."
echo ""
echo "  After rebooting, verify:"
echo "    lsusb | grep -i quad     # one line per USB→RS-485 adapter"
echo "    ls -l /dev/ttyRS485_*    # 4 symlinks per adapter"
echo "    groups $REAL_USER      # should include dialout and gpio"
echo "    ls -la /dev/ttyAMA0 /dev/ttyAMA2   # GPIO UARTs, if you use them"
echo ""
echo "  Then, BEFORE connecting field wiring, check every channel:"
echo "    sudo python3 scripts/rs485-diag.py"
echo ""
echo "  Testing a channel with a panel already wired to it causes bus"
echo "  contention that looks exactly like a dead channel. Verify first."
echo ""
echo "  Next: deploy the app — see PI_SETUP.md"
echo "============================================================"
echo ""
read -rp "Reboot now? [y/N] " REBOOT
if [[ "$REBOOT" =~ ^[Yy]$ ]]; then
    reboot
fi
