#!/usr/bin/env python3
"""
Per-channel health check for USB->RS485 adapter channels.

Why this exists
---------------
A USB serial channel can enumerate perfectly, get a correct udev symlink, open
without error, and still be unusable - because it silently fails to APPLY the
baud rate you asked for. On 2026-08-27 a Waveshare quad adapter was found with
two channels that accepted a 9600 request but actually clocked at 115200. OSDP
requires 9600, so the panel polled forever and the reader never came online.

Nothing in lsusb, dmesg, udevadm, or a plain port-open test reveals that. The
only reliable detection is to measure achieved throughput against the requested
rate: a healthy port sustains about baud/10 bytes per second.

Usage
-----
    # check every idle RS485 channel at the OSDP rate
    sudo python3 rs485-diag.py

    # check specific ports, and additional rates
    sudo python3 rs485-diag.py --ports /dev/ttyRS485_3_3 --bauds 9600 19200

Two hard-won cautions, both enforced below:

  * Ports held by DoorSim are SKIPPED by default. Linux does not lock ttys, so
    writing to a live port silently reconfigures it underneath the running app
    and manufactures symptoms (a reader that "bounces" online/offline). Use
    --allow-in-use only with the service stopped.

  * Always test with the FIELD WIRING DISCONNECTED where possible. A live bus
    with a panel driving it causes contention that looks exactly like a dead
    channel, which sent this investigation down the wrong path for an hour.
"""
import argparse
import glob
import os
import subprocess
import sys
import time

try:
    import serial
except ImportError:
    sys.exit("pyserial missing. Install with: sudo apt install python3-serial")

DEFAULT_BAUDS = [9600]
TOLERANCE = 0.35          # achieved rate must be within +/-35% of theoretical
SETTLE = 1.0              # seconds of buffer fill to discard before measuring
MEASURE = 4.0             # seconds of steady-state measurement


def ports_in_use():
    """Ports currently held open by another process, via lsof."""
    try:
        out = subprocess.run(["lsof", "-F", "n"] + glob.glob("/dev/ttyACM*")
                             + glob.glob("/dev/ttyRS485_*"),
                             capture_output=True, text=True, timeout=20).stdout
    except (FileNotFoundError, subprocess.TimeoutExpired):
        return set()
    held = set()
    for line in out.splitlines():
        if line.startswith("n/dev/"):
            held.add(os.path.realpath(line[1:]))
    return held


def measure(port, baud):
    """Bytes per second the port actually sustains after its buffer fills."""
    sp = serial.Serial(port, baud, timeout=0.1, bytesize=8, parity="N",
                       stopbits=1, rtscts=False, dsrdtr=False)
    fd = sp.fileno()
    os.set_blocking(fd, False)
    total = 0
    baseline = None
    start = time.time()
    while True:
        elapsed = time.time() - start
        if elapsed >= SETTLE + MEASURE:
            break
        if baseline is None and elapsed >= SETTLE:
            baseline = total
        try:
            total += os.write(fd, b"\x55" * 512)
        except BlockingIOError:
            time.sleep(0.01)
        except OSError:
            break
    sp.close()
    return (total - (baseline or 0)) / MEASURE


def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--ports", nargs="+", help="default: all /dev/ttyRS485_*")
    ap.add_argument("--bauds", nargs="+", type=int, default=DEFAULT_BAUDS)
    ap.add_argument("--allow-in-use", action="store_true",
                    help="test ports held by another process (STOP DoorSim FIRST)")
    args = ap.parse_args()

    targets = sorted(args.ports or glob.glob("/dev/ttyRS485_*"))
    if not targets:
        sys.exit("No /dev/ttyRS485_* symlinks found. Check 99-waveshare485.rules.")

    held = set() if args.allow_in_use else ports_in_use()

    print("%-20s %-9s %-11s %-11s %s"
          % ("PORT", "REQUESTED", "EXPECTED", "MEASURED", "RESULT"))
    print("-" * 72)

    failures, skipped = [], []
    for port in targets:
        if os.path.realpath(port) in held:
            print("%-20s %s" % (port.split("/")[-1],
                                "SKIPPED - in use (stop doorsim, or --allow-in-use)"))
            skipped.append(port)
            continue
        for baud in args.bauds:
            expected = baud / 10.0
            try:
                rate = measure(port, baud)
            except Exception as exc:                      # noqa: BLE001
                print("%-20s %-9d %-11.0f %-11s ERROR %s"
                      % (port.split("/")[-1], baud, expected, "-",
                         type(exc).__name__))
                failures.append((port, baud, "error"))
                continue

            if rate < expected * (1 - TOLERANCE):
                verdict = "STALLED - no data leaving the UART"
            elif abs(rate - expected) / expected <= TOLERANCE:
                verdict = "ok"
            else:
                actual = round(rate * 10)
                verdict = "BAUD NOT APPLIED - running near %d" % actual
            print("%-20s %-9d %-11.0f %-11.0f %s"
                  % (port.split("/")[-1], baud, expected, rate, verdict))
            if verdict != "ok":
                failures.append((port, baud, verdict))

    print()
    if skipped:
        print("%d port(s) skipped as in-use." % len(skipped))
    if failures:
        print("%d channel/rate combination(s) FAILED:" % len(failures))
        for port, baud, why in failures:
            print("  %s @ %d - %s" % (port, baud, why))
        print("\nA channel that fails only at some rates is a baud-divisor")
        print("defect, not a dead port. Either run that door at a rate the")
        print("channel does apply (and match the panel), or replace the adapter.")
        return 1
    print("All tested channels applied every requested rate correctly.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
