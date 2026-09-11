# Notice

This directory is a vendored, modified copy of
[OSDP.Net](https://github.com/Z-bit-Systems-LLC/OSDP.Net) by Z-bit Systems
LLC, licensed under the Apache License, Version 2.0 (see `LICENSE.txt` in
this directory).

- **Forked from:** v5.1.0 (originally forked from v5.0.44; merged forward
  2026-09-09 — the timeout patch below was still needed at v5.1.0, upstream
  has not fixed it)
- **Modified files:** `Device.cs`, `Messages/SecureChannel/PdMessageSecureChannel.cs`,
  `Connections/SerialPortConnectionListener.cs`
- **Reason for the fork:** the upstream connection/read timeouts in those
  files are hardcoded to 8 seconds (connection) and 200ms (reply/inter-byte),
  which are too tight for slower-polling panels (e.g. Mercury MP1502,
  observed polling at ~3s intervals with real-world jitter) and a shared USB
  bus with occasional inter-byte gaps beyond 200ms. Both caused `IsConnected`
  to flicker false, or a `TimeoutException` mid-frame, on an otherwise
  healthy link. The patch makes both timeouts configurable —
  `DeviceConfiguration.ConnectionTimeout` and
  `SerialPortConnectionListener.ReplyTimeout` — instead of hardcoded. See
  the header comment in `OSDP.Net.csproj` for the technical detail.

This is not an official Z-bit Systems LLC release. Bug reports specific to
these modifications should go to the DoorSim project, not upstream.

## Known CodeQL findings (suppressed, not bugs)

- `Messages/SecureChannel/SecurityContext.cs` — `cs/ecb-encryption` on the
  AES `CipherMode.ECB` setup for session-setup key derivation. Single-block
  KDF operation per the OSDP secure channel spec, not multi-block plaintext
  encryption; ECB's replay weakness doesn't apply. Suppressed inline via a
  `codeql[cs/ecb-encryption]` comment. Do not "fix" by changing the mode —
  it will break interop with real panels.
