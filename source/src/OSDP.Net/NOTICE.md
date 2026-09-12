# Notice

This directory is a vendored, modified copy of
[OSDP.Net](https://github.com/Z-bit-Systems-LLC/OSDP.Net) by Z-bit Systems
LLC, licensed under the Apache License, Version 2.0 (see `LICENSE.txt` in
this directory).

- **Forked from:** v5.1.0 (originally forked from v5.0.44; merged forward
  2026-09-09 — the timeout patch below was still needed at v5.1.0, upstream
  has not fixed it)
- **Modified / added files:**
  - `Device.cs`
  - `Messages/SecureChannel/PdMessageSecureChannel.cs`
  - `Connections/SerialPortConnectionListener.cs`
  - `Connections/SerialPortOsdpConnection.cs`
  - `Connections/ReadOnlySerialPortOsdpConnection.cs`
  - `Connections/SerialPortUtils.cs` (added)
- **Reason for the fork & local modifications:**
  1. **Configurable timeouts:** Upstream connection/read timeouts in `Device.cs`,
     `PdMessageSecureChannel.cs`, and `SerialPortConnectionListener.cs` are hardcoded
     to 8 seconds (connection) and 200ms (reply/inter-byte), which are too tight
     for slower-polling panels (e.g. Mercury MP1502 polling at ~3s intervals with
     real-world jitter) and shared USB buses with inter-byte gaps. Both timeouts are
     made configurable via `DeviceConfiguration.ConnectionTimeout` and
     `SerialPortConnectionListener.ReplyTimeout`.
  2. **Serial port reconnection & missing port resilience:**
     - `SerialPortConnectionListener.cs` was refactored from recursive async reopen
       calls on error to an iterative, cancellation-aware `ListenLoop`.
     - `SerialPortUtils.cs` was added to check port existence (`SerialPort.GetPortNames()`
       on Windows and `/dev` file checks on Linux) before attempting `SerialPort.Open()`,
       preventing first-chance `FileNotFoundException` loops when a configured port is
       unplugged or missing.
     - `SerialPortOsdpConnection.cs` and `ReadOnlySerialPortOsdpConnection.cs` were patched
       to safely dispose underlying streams on `Open()` failure or `Close()` and avoid
       unobserved task exceptions during stream reads.

This is not an official Z-bit Systems LLC release. Bug reports specific to
these modifications should go to the DoorSim project, not upstream.

## Known CodeQL findings (suppressed, not bugs)

- `Messages/SecureChannel/SecurityContext.cs` — `cs/ecb-encryption` on the
  AES `CipherMode.ECB` setup for session-setup key derivation. Single-block
  KDF operation per the OSDP secure channel spec, not multi-block plaintext
  encryption; ECB's replay weakness doesn't apply. Suppressed inline via a
  `codeql[cs/ecb-encryption]` comment. Do not "fix" by changing the mode —
  it will break interop with real panels.
