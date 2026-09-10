# DoorSim — Raspberry Pi Setup & Deployment

Standing up DoorSim on a fresh Raspberry Pi, and deploying to an existing one.

- Adding or diagnosing a USB→RS-485 adapter: [RS485_ADAPTERS.md](RS485_ADAPTERS.md)
- Hardware wiring: [WIRING_GUIDE.md](WIRING_GUIDE.md)

---

## Current architecture

Worth stating plainly, because earlier revisions of this document described a
different rig:

| Concern | How it works now |
|---------|------------------|
| OSDP reader buses | **USB→quad-RS-485 adapters** on a powered USB hub, up to 4 channels each, named via udev as `/dev/ttyRS485_<device>_<channel>` |
| DPS / REX contacts | **Modbus TCP** relay board (`ModbusTcpHost`, default port 502), configured per door |
| Reader count | Driven entirely by the doors in the database, not by a fixed pin table |
| Protocol | OSDP (PD role — DoorSim answers panel polls). Wiegand GPIO is still supported per door |

The Pi's own GPIO UARTs (`/dev/ttyAMA0`, `/dev/ttyAMA2`) are **not** used for
OSDP in this configuration. `setup-pi.sh` still frees and enables them because
they remain available for the GPIO-UART and Modbus-RTU paths, but a USB-adapter
rig does not need them.

Example: `<pi-user>@<pi-host>`, app served at `http://<pi-host>:5000`.

---

## Part 1 — New host, from a fresh Raspberry Pi OS install

Tested on Raspberry Pi OS Bookworm 64-bit, Pi 4.

### 1. Base OS

Flash Raspberry Pi OS (64-bit), enable SSH, set the hostname, and confirm you
can reach it:

```bash
ssh <pi-user>@<pi-address>
```

### 2. Install .NET

DoorSim targets .NET 10 and is published **framework-dependent**, so the Pi
needs a runtime. Install to `/home/<pi-user>/.dotnet` — the path the service
unit expects:

```bash
curl -sSL https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 10.0 --runtime aspnetcore --install-dir ~/.dotnet
~/.dotnet/dotnet --list-runtimes    # expect Microsoft.AspNetCore.App 10.x
```

> Do **not** use a self-contained publish. It produces musl/glibc mismatches on
> Raspberry Pi OS.

### 3. Run the OS setup script

```bash
scp scripts/setup-pi.sh <pi-user>@<pi-address>:~/
scp scripts/99-waveshare485.rules <pi-user>@<pi-address>:~/
ssh <pi-user>@<pi-address> 'chmod +x ~/setup-pi.sh && sudo ~/setup-pi.sh'
```

It adds the run user to `dialout` and `gpio`, creates `/var/lib/doorsim`,
installs `99-waveshare485.rules` if it finds it beside the script, frees the
PL011 UART, enables UART2, and disables the serial console. It then offers to
reboot — take it, the overlay changes need it.

### 4. Wire and verify the RS-485 adapters

Plug the USB adapters into the powered hub, then **before** connecting field
wiring:

```bash
lsusb | grep -i quad          # one line per adapter
ls -l /dev/ttyRS485_*         # 4 symlinks per adapter
sudo python3 scripts/rs485-diag.py
```

Every channel should report `ok`. If symlinks are missing, or a channel fails,
work through [RS485_ADAPTERS.md](RS485_ADAPTERS.md) before going further —
diagnosing this after the field wiring is on is materially harder.

### 5. Deploy the app

From the Windows dev machine, at the repo root:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/deploy-to-pi.ps1 -Mode Deploy -RemoteHost <pi-address>
```

This installs and enables `doorsim.service` (rendered from
`scripts/doorsim.service.template` with `-RemoteUser`/`-InstallPath`
substituted in), publishes for `linux-arm64`, refreshes
`/home/<pi-user>/doorsim`, copies `wait-for-rs485.sh`, and starts the
service. See [Part 2](#part-2--deploying-to-an-existing-host) for what it does
step by step.

### 6. Create the doors

Open `http://<pi-address>:5000` → **Door Config**, and add one door per reader:

| Field | Value |
|-------|-------|
| Protocol | `Osdp` |
| OSDP Address | as configured on the panel (often `0`) |
| Serial Port | `/dev/ttyRS485_<device>_<channel>` |
| Baud Rate | `Default (9600)` |
| Door Contact (DPS) | Modbus channel on the relay board |
| Request to Exit (REX) | Modbus channel on the relay board |

The database is created and migrated automatically on first start — there are no
EF migration files. [DatabaseInitializer.cs](../source/src/DoorSim/Data/DatabaseInitializer.cs)
diffs the EF model against the live SQLite schema and applies additive changes,
so new columns appear on their own after a deploy.

### 7. Confirm

```bash
systemctl status doorsim
curl -s http://localhost:5000/api/doors | head
# per door:
curl -s http://localhost:5000/api/simulate/connectivity/<doorId>   # expect true
```

`true` means the panel is actively polling that simulated reader.

---

## Part 2 — Deploying to an existing host

```powershell
# from the repo root
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/deploy-to-pi.ps1 -Mode Deploy
```

Or in VS Code: **Terminal → Run Task → deploy-to-pi**.

[deploy-to-pi.ps1](../scripts/deploy-to-pi.ps1) does, in order:

1. Renders `doorsim.service` from `doorsim.service.template` and copies it to
   `/etc/systemd/system`, `daemon-reload`, `enable`
2. `dotnet publish -c Release -r linux-arm64 --no-self-contained`
3. Stops the service
4. Kills orphaned `DoorSim`/`vsdbg` processes from an unclean debug session —
   these otherwise keep holding the HTTP port and every serial device, and the
   real service crash-loops fighting them for the same ports
5. Wipes `/home/<pi-user>/doorsim` **except `*.db`**, then copies the new build
6. Copies `wait-for-rs485.sh` and makes it executable
7. Starts the service and asserts it is active

The production database at `/var/lib/doorsim/cards.db` is never touched, so card
library and door config survive every deploy.

### Manual service install

Prefer `deploy-to-pi.ps1` — it renders the placeholders in
`doorsim.service.template` for you. To do it by hand instead, substitute
`__DEPLOY_USER__` and `__INSTALL_PATH__` in the template yourself first:

```bash
sed -e 's/__DEPLOY_USER__/<pi-user>/g' -e 's|__INSTALL_PATH__|/home/<pi-user>/doorsim|g' \
  scripts/doorsim.service.template > doorsim.service
scp doorsim.service scripts/wait-for-rs485.sh <pi-user>@<pi-address>:~/
ssh <pi-user>@<pi-address> 'sudo install -m 644 ~/doorsim.service /etc/systemd/system/doorsim.service \
  && install -m 755 ~/wait-for-rs485.sh /home/<pi-user>/doorsim/wait-for-rs485.sh \
  && sudo systemctl daemon-reload && sudo systemctl enable --now doorsim'
```

### Debug workflow

Use the **Deploy & Debug on Pi** launch configuration. It runs the
`prepare-pi-debug` task first, which stops the service and refreshes files
without restarting it, then attaches over SSH.

If a debug session ends uncleanly, run a normal deploy before trusting the
service again — step 4 above is what clears the orphans.

---

## The startup gate

`doorsim.service` runs [wait-for-rs485.sh](../scripts/wait-for-rs485.sh) as
`ExecStartPre`.

DoorSim opens its OSDP ports by udev symlink name, and those symlinks do not
exist until udev has processed the USB coldplug events — a few seconds after
`network-online.target` on a Pi 4. Without the gate the app starts first and
logs `Failed to open serial port` in a retry loop until udev catches up. It
self-heals, but it makes every boot look broken and buries genuine faults under
hundreds of identical lines. During the 2026-08-27 investigation that noise hid
the real problem for the first twenty minutes.

The gate runs `udevadm settle`, then polls up to 30 s for the first
`/dev/ttyRS485_*` symlink. It **always exits 0**, and the unit calls it with a
leading `-`, so a missing adapter or a missing gate script can never block
startup — the readers on the remaining adapters still come up, and the app's own
retry loop remains the fallback.

---

## Troubleshooting

| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| Service fails immediately | .NET runtime missing or wrong path | `~/.dotnet/dotnet --list-runtimes`; unit expects `DOTNET_ROOT=/home/<pi-user>/.dotnet` |
| `Permission denied` on a serial port | User not in `dialout` | `sudo usermod -aG dialout <pi-user>`, then re-login |
| No `/dev/ttyRS485_*` at all | udev rules not installed | See [RS485_ADAPTERS.md](RS485_ADAPTERS.md) |
| `Failed to open serial port` on every boot | Startup gate missing | Confirm `/home/<pi-user>/doorsim/wait-for-rs485.sh` exists and is executable |
| Service active, port 5000 refused | Orphaned debug process holding the port | Run a normal deploy, or `sudo pkill -9 -f vsdbg` |
| Door shows `false` forever | Panel not polling, address mismatch, or channel fault | [RS485_ADAPTERS.md](RS485_ADAPTERS.md) troubleshooting table |
| All doors `false` at once | USB host-level wedge | Check `dmesg` for `xhci` / `hub_ext` / `-110`; **do not** USB-reset the hub |
| New DB column missing after deploy | Schema diff did not run | Check startup logs for `Database schema` lines |

### Useful commands

```bash
# app log without EF SQL and stack traces
sudo journalctl -u doorsim -f | grep -vE '^\s|   at |SELECT|FROM '

# which ports the app currently holds
sudo lsof /dev/ttyACM*

# every door's connectivity in one pass
for id in $(curl -s localhost:5000/api/doors | grep -o '"id":[0-9]*' | cut -d: -f2); do
  printf '%s=%s\n' "$id" "$(curl -s localhost:5000/api/simulate/connectivity/$id)"
done
```
