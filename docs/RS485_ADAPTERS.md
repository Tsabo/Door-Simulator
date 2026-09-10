# USB → RS-485 Adapters

How the OSDP serial ports on the DoorSim Pi are named, how to add another
adapter, and how to tell a genuinely broken channel from a working one.

Hardware wiring is in [WIRING_GUIDE.md](WIRING_GUIDE.md). OS and deployment
setup is in [PI_SETUP.md](PI_SETUP.md).

---

## Why the udev rules exist

Each Waveshare industrial USB→quad-RS485 adapter (WCH `1a86:55d5`, "USB
Quad_Serial") exposes its four RS-485 channels as four `cdc_acm` devices:

| USB interface | RS-485 channel |
|---------------|----------------|
| `1.0`         | Port 1         |
| `1.2`         | Port 2         |
| `1.4`         | Port 3         |
| `1.6`         | Port 4         |

The kernel numbers those `/dev/ttyACM*` in **enumeration order**, which changes
between boots and whenever an adapter is added, removed, or re-plugged. With
three adapters that is 12 `ttyACM` nodes whose numbering you cannot rely on.

[`99-waveshare485.rules`](../scripts/99-waveshare485.rules) pins each channel to a stable
name using two attributes that never change:

- `ATTRS{serial}` — the adapter's unique USB serial (e.g. `BD63CCABCD`)
- `ENV{ID_PATH}` — the USB topology path plus interface number

giving `/dev/ttyRS485_<device>_<channel>`:

```
/dev/ttyRS485_1_1 … ttyRS485_1_4   Device 1
/dev/ttyRS485_2_1 … ttyRS485_2_4   Device 2
/dev/ttyRS485_3_1 … ttyRS485_3_4   Device 3
```

Device numbers are ours, not the hardware's — they map to physical units by
serial. Channel numbers are 1-based to match the labels printed on the adapter.

DoorSim stores these symlink names per door (`OsdpSerialPort`), so the app never
sees a `ttyACM` number.

> **This is why `doorsim.service` gates on udev.** The app opens ports by
> symlink name, and those symlinks appear a few seconds after
> `network-online.target`. See [scripts/wait-for-rs485.sh](../scripts/wait-for-rs485.sh).

---

## Adding another USB → RS-485 adapter

### 1. Plug it in and find its identity

```bash
lsusb | grep -i quad
# Bus 001 Device 006: ID 1a86:55d5 QinHeng Electronics USB Quad_Serial

# Serial + USB path of every quad-serial channel currently attached:
for d in /dev/ttyACM*; do
  printf '%-16s serial=%-14s path=%s\n' "$d" \
    "$(udevadm info -q property -n "$d" | sed -n 's/^ID_SERIAL_SHORT=//p')" \
    "$(udevadm info -q property -n "$d" | sed -n 's/^ID_PATH=//p')"
done
```

You are looking for the four lines sharing a serial you have not seen before.
Their `ID_PATH` values differ only in the trailing interface number:

```
platform-fd500000.pcie-pci-0000:01:00.0-usb-0:1.2:1.0   <- channel 1
platform-fd500000.pcie-pci-0000:01:00.0-usb-0:1.2:1.2   <- channel 2
platform-fd500000.pcie-pci-0000:01:00.0-usb-0:1.2:1.4   <- channel 3
platform-fd500000.pcie-pci-0000:01:00.0-usb-0:1.2:1.6   <- channel 4
```

> The `usb-0:1.<n>` segment is the **hub port** the adapter is plugged into.
> Moving the adapter to a different hub port changes `ID_PATH` and the rules
> will no longer match — you get `ttyACM` nodes and no symlinks. Either keep it
> in the same port or update the rules.

### 2. Add a rule block

Append to [99-waveshare485.rules](../scripts/99-waveshare485.rules), substituting your
serial and path. One block per channel:

```udev
###############################
# Device 4 - Serial BD1234ABCD
# USB Path Prefix: usb-0:1.1
###############################

SUBSYSTEM=="tty", ATTRS{serial}=="BD1234ABCD", \
  ENV{ID_PATH}=="platform-fd500000.pcie-pci-0000:01:00.0-usb-0:1.1:1.0", \
  SYMLINK+="ttyRS485_4_1"

SUBSYSTEM=="tty", ATTRS{serial}=="BD1234ABCD", \
  ENV{ID_PATH}=="platform-fd500000.pcie-pci-0000:01:00.0-usb-0:1.1:1.2", \
  SYMLINK+="ttyRS485_4_2"

SUBSYSTEM=="tty", ATTRS{serial}=="BD1234ABCD", \
  ENV{ID_PATH}=="platform-fd500000.pcie-pci-0000:01:00.0-usb-0:1.1:1.4", \
  SYMLINK+="ttyRS485_4_3"

SUBSYSTEM=="tty", ATTRS{serial}=="BD1234ABCD", \
  ENV{ID_PATH}=="platform-fd500000.pcie-pci-0000:01:00.0-usb-0:1.1:1.6", \
  SYMLINK+="ttyRS485_4_4"
```

Keep the header comment accurate — a stale serial in a comment costs real
debugging time later. (The Device 3 header in that file was wrong for weeks.)

### 3. Install and reload

```bash
sudo install -m 644 99-waveshare485.rules /etc/udev/rules.d/99-waveshare485.rules
sudo udevadm control --reload-rules
sudo udevadm trigger --subsystem-match=tty
```

> **`udevadm trigger` disrupts readers that are already online.** It re-emits
> change events for every tty, and on 2026-08-27 that knocked four healthy
> readers offline mid-session — they only recovered after
> `systemctl restart doorsim`. On a live rig, either accept that and restart the
> service afterwards, or skip the trigger: the rules apply on their own at the
> next boot or when the adapter is re-plugged.

### 4. Verify before wiring anything

```bash
ls -l /dev/ttyRS485_*
sudo python3 scripts/rs485-diag.py --ports /dev/ttyRS485_4_1 /dev/ttyRS485_4_2 /dev/ttyRS485_4_3 /dev/ttyRS485_4_4
```

All four channels should report `ok`. Do this **before** connecting field wiring
— see [Verifying a channel](#verifying-a-channel) for why that ordering matters.

### 5. Configure the doors

In the web UI → **Door Config**, for each reader set Protocol `Osdp`, the OSDP
address, the new Serial Port, and leave **Baud Rate** on `Default (9600)` unless
you have a specific reason. See [Baud rate](#baud-rate).

No restart needed — saving a door rebuilds that reader in the simulator bank.

---

## Verifying a channel

[scripts/rs485-diag.py](../scripts/rs485-diag.py) measures **achieved** throughput
against the **requested** baud rate. A healthy port sustains about `baud / 10`
bytes per second.

```bash
# every idle channel, at the OSDP rate
sudo python3 scripts/rs485-diag.py

# specific channels, multiple rates
sudo python3 scripts/rs485-diag.py --ports /dev/ttyRS485_3_3 --bauds 9600 19200
```

This catches failures nothing else does. A channel can enumerate, get a correct
symlink, and open without error — yet not move data, or not apply the rate you
asked for. `lsusb`, `dmesg`, and `udevadm` all look perfectly healthy in that
state.

### Two rules, both learned the hard way

**Disconnect the field wiring first.** A live bus with a panel driving it causes
contention that is indistinguishable from a dead channel. On 2026-08-27 two
channels measured as completely dead — total TX stall, control transfers timing
out — and then tested perfectly once unwired. About an hour went into chasing a
fault that was a measurement artifact.

**Never open a port DoorSim is holding.** A second opener can silently
reconfigure the port underneath the running app. Doing this to `ttyRS485_3_1`
left it at 115200 while DoorSim expected 9600 and produced a reader that flapped
online/offline — an entirely invented symptom that took a service restart to
clear. `rs485-diag.py` skips in-use ports by default; `--allow-in-use` requires
stopping the service first.

In DoorSim's favour, .NET marks a serial port exclusive once opened, so a second
open returns `EBUSY` and is refused. The vulnerable window is app startup, before
it has claimed everything — which is exactly when the `3_1` damage happened.
Do not rely on the exclusivity; check `sudo lsof /dev/ttyACM*` first.

---

## Baud rate

OSDP runs at **9600** by default and that is the rate used for discovery. Leave
doors on `Default (9600)` unless a bus genuinely needs otherwise.

A per-door override exists (`OsdpBaudRate`, shown as **Baud Rate** in Door
Config) supporting 9600, 19200, 38400, 57600 and 115200. Null means 9600, so
existing doors are unaffected. In the doors table an override renders as
`19200 *`.

> Changing a door's rate requires the **panel** to be set to the same rate. Many
> panels only *discover* PDs at 9600, so a PD on a non-default rate may need to
> be configured explicitly on the panel rather than auto-discovered.

---

## Troubleshooting

| Symptom | Likely cause | Next step |
|---------|--------------|-----------|
| `ttyACM*` present, no `ttyRS485_*` | Rules not matching — usually the adapter moved hub ports so `ID_PATH` changed | Re-run step 1, compare `ID_PATH` to the rule, reload rules |
| Some `ttyRS485_*` missing | Rule block missing or serial typo'd | Compare `grep -c SYMLINK /etc/udev/rules.d/99-waveshare485.rules` to your channel count |
| `Failed to open serial port` at boot, then stops | udev had not created symlinks yet | Should be prevented by the startup gate; confirm `wait-for-rs485.sh` is present and executable in `/home/<pi-user>/doorsim` |
| `Failed to open serial port` forever | Symlink genuinely absent, or another process holds it | `ls -l /dev/ttyRS485_*`, `sudo lsof /dev/ttyACM*` |
| Reader never comes online, no traffic at all | Panel not polling, A/B swapped, or channel not transmitting | Run `rs485-diag.py` on that channel with wiring off; if `ok`, suspect panel or wiring |
| **Panel polls but reader never comes online** | Address mismatch, secure-channel key, baud mismatch, or a channel fault | Move that reader to a known-good channel to bisect hardware from config — see [Unresolved](#unresolved) |
| Reader flaps online/offline | Something reconfigured the port under the app | Check nothing else opened it, then `systemctl restart doorsim` to reassert settings |
| All readers on all adapters dead at once | USB host-level wedge — see below | Check `dmesg` for `xhci`, `hub_ext` and `-110` |

### The USB host wedge (confirmed 2026-08-27)

With three adapters on the VIA Labs hub, the Pi 4's VL805 xHCI controller
wedged. Signature in `dmesg`:

```
hub 1-1:1.0: hub_ext_port_status failed (err = -110)
```

and under `usbmon`, no URB completing on **any** data endpoint — bulk IN, bulk
OUT and interrupt IN all submitted, none returning — plus
`SET_CONTROL_LINE_STATE` timing out after ~5 s. Every reader on every adapter
was offline while USB enumeration, `lsusb`, and the `dmesg` boot output all
looked clean.

**Do not USB-reset a hub that is returning `-110`.** Attempting it escalated the
wedge into a dead controller:

```
xhci_hcd 0000:01:00.0: xHCI host controller not responding, assume dead
xhci_hcd 0000:01:00.0: HC died; cleaning up
```

which killed all USB and forced a reboot. Power-cycle the hub or reboot
instead. Recovery was clean: a reboot brought the controller up with no errors.

Worth ruling out if it recurs: the hub's external power supply may not have been
connected. Three quad adapters plus a hub on Pi bus power is marginal, since the
Pi 4's ports share a limited current budget.

### Resolved: the channels were fine

Two firm diagnoses were made about `ttyRS485_3_3` / `3_4` on adapter
`BD63CCABCD` during that investigation, and **both were wrong**:

1. *"Both channels are dead."* Every supporting measurement was taken with the
   Azure field wiring attached. Unwired, the channels opened, passed control
   transfers, and drained TX normally.
2. *"Both channels cannot apply 9600 baud."* Two runs measured ~11605 B/s when
   9600 was requested, which would have neatly explained a panel polling a
   reader that never comes online. It never reproduced: repeated controlled runs
   measured 9600 correctly, including after deliberately priming the port at
   115200 to rule out stale-baud carry-over.

Settled by putting a reader back on the channel. With door 11 pointed at
`ttyRS485_3_3`, the Azure BLU-CP2 came online and drove it with real traffic:

```
Door 11 (Azure BLU-CP2 Reader 1): CP sent osdp_OUT (relay/output control)
Door 11 (Azure BLU-CP2 Reader 1): CP sent osdp_LED - panel is responding to a card event
```

Opening the channel with nothing wired was also watched for three minutes: the
other eight readers stayed online, ports held steady, and no new wedge signature
appeared in `dmesg`. So merely opening it does not destabilise the hub either.

An adapter RMA was recommended on the strength of diagnosis 2 and that
recommendation is **withdrawn** — there is no evidence of a per-channel fault.
The only confirmed hardware event was the USB host wedge described above.

The general lesson, which is the durable part: **measure achieved behaviour, not
requested configuration, and change one variable at a time.** Both wrong
diagnoses came from measuring without controlling a variable — first the field
wiring, then the adapter's post-wedge state.
