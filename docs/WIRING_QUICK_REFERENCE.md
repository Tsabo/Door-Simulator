# DoorSim Wiring Quick Reference Card — Wiegand-GPIO-Direct Doors

**Print this page and keep it at your workbench!**

> This card is for the **Wiegand-GPIO-direct** wiring path (a per-door
> alternative). The current default rig is OSDP over RS-485 + Modbus relay —
> see [WIRING_DIAGRAM.md](WIRING_DIAGRAM.md). Pins below are an **example
> allocation**, not a fixed table — set the actual BCM pins per door in
> Door Config, and wire to match.

---

## ⚠️ CRITICAL SAFETY

- **D0/D1 (Wiegand):** Use level shifters (3.3V → 5V logic)
- **DPS/REX (if GPIO-direct):** Use relay modules (contact closure, NOT digital logic)
- **NEVER** connect 5V directly to Pi GPIO pins!

---

## Pin Quick Reference

### Breakout Board Strip Grouping

The breakout has 4 strips of 10 terminals. This example allocation lands
doors 1–4 each entirely on one strip; doors 5 & 6 use the leftover terminals
across strips.

| Door slot | Breakout Strip | D0        | D1        | DPS       | REX       |
|--------|---------------|-----------|-----------|-----------|----------|
| 1      | Strip 4       | **IO4**   | **IO27**  | **IO21**  | **IO13**  |
| 2      | Strip 3       | **IO23**  | **IO22**  | **IO12**  | **IO20**  |
| 3      | Strip 2       | **IO25**  | **IO24**  | **RXD**   | **TXD**   |
| 4      | Strip 1       | **MOSI**  | **MISO**  | **SCLK**  | **CE0**   |
| 5      | Strips 1+2    | **IO17**  | **IO18**  | **IO5**   | **IO6**   |
| 6      | Strips 3+2+4+1| **IO19**  | **IO16**  | **IO26**  | **CE1**   |

> Note: RXD=GPIO15, TXD=GPIO14. MOSI=GPIO10, MISO=GPIO9, SCLK=GPIO11, CE0=GPIO8, CE1=GPIO7.

> ⚠️ **OSDP/Modbus RS-485 conflict:** GPIO14 (TXD) and GPIO15 (RXD) are used above for
> **door slot 3's DPS/REX**. If you also use the onboard UART for OSDP or
> Modbus RS-485 on another door (per [WIRING_DIAGRAM.md](WIRING_DIAGRAM.md)),
> that repurposes these same pins — pick different GPIO pins for one side,
> or use a USB-RS485 adapter for the RS-485 bus instead.
> See [PI_SETUP.md](PI_SETUP.md) for details.

### Door slot 1  *(Strip 4)*
| Function | BCM GPIO | Breakout | Mercury Panel |
|----------|----------|----------|---------------|
| D0       | GPIO4    | **IO4**  | Wiegand D0 |
| D1       | GPIO27   | **IO27** | Wiegand D1 |
| DPS      | GPIO21   | **IO21** | Door Pos   |
| REX      | GPIO13   | **IO13** | REX        |

### Door slot 2  *(Strip 3)*
| Function | BCM GPIO | Breakout | Mercury Panel |
|----------|----------|----------|---------------|
| D0       | GPIO23   | **IO23** | Wiegand D0 |
| D1       | GPIO22   | **IO22** | Wiegand D1 |
| DPS      | GPIO12   | **IO12** | Door Pos   |
| REX      | GPIO20   | **IO20** | REX        |

### Door slot 3  *(Strip 2)*
| Function | BCM GPIO | Breakout | Mercury Panel |
|----------|----------|----------|---------------|
| D0       | GPIO25   | **IO25** | Wiegand D0 |
| D1       | GPIO24   | **IO24** | Wiegand D1 |
| DPS      | GPIO15   | **RXD**  | Door Pos   |
| REX      | GPIO14   | **TXD**  | REX        |

### Door slot 4  *(Strip 1 — SPI terminals)*
| Function | BCM GPIO | Breakout  | Mercury Panel |
|----------|----------|-----------|---------------|
| D0       | GPIO10   | **MOSI**  | Wiegand D0 |
| D1       | GPIO9    | **MISO**  | Wiegand D1 |
| DPS      | GPIO11   | **SCLK**  | Door Pos   |
| REX      | GPIO8    | **CE0**   | REX        |

### Door slot 5  *(Strips 1 + 2)*
| Function | BCM GPIO | Breakout | Mercury Panel |
|----------|----------|----------|---------------|
| D0       | GPIO17   | **IO17** | Wiegand D0 |
| D1       | GPIO18   | **IO18** | Wiegand D1 |
| DPS      | GPIO5    | **IO5**  | Door Pos   |
| REX      | GPIO6    | **IO6**  | REX        |

### Door slot 6  *(Remaining terminals)*
| Function | BCM GPIO | Breakout | Mercury Panel |
|----------|----------|----------|---------------|
| D0       | GPIO19   | **IO19** | Wiegand D0 |
| D1       | GPIO16   | **IO16** | Wiegand D1 |
| DPS      | GPIO26   | **IO26** | Door Pos   |
| REX      | GPIO7    | **CE1**  | REX        |

---

## Signal States (For Testing)

| Line | Pi GPIO Idle | Device        | Mercury Panel Sees       |
|------|--------------|---------------|--------------------------|
| D0   | HIGH         | Level shifter | 5V (idle)                |
| D1   | HIGH         | Level shifter | 5V (idle)                |
| DPS  | HIGH         | Relay OFF     | NC contact closed (door closed) |
| REX  | HIGH         | Relay OFF     | NC contact closed (no motion) |

**Active states:**
- **D0/D1:** Pulse LOW (0V) for 50µs during Wiegand transmission
- **DPS:** GPIO LOW → relay ON → NC opens → door open
- **REX:** GPIO LOW → relay ON → NC opens → motion detected

---

## Wire Color Code

| Signal | Color  | Device Type |
|--------|--------|-------------|
| D0     | Yellow | Level shifter |
| D1     | Blue   | Level shifter |
| DPS    | Green  | Relay module |
| REX    | Orange | Relay module |
| GND    | Black  | Common |
| 3.3V   | Red    | Power |
| 5V     | Red    | Power |

**Label every wire on BOTH ends:** "Door1-D0", "Door3-REX", etc.

---

## Level Shifter Assignments (Wiegand D0/D1 Only)

**TXS0108E A/B side orientation:**
- **A side (VCCA = 3.3V)** — connects to Pi GPIO breakout terminals
- **B side (VCCB = 5V)** — connects to Mercury panel terminals

Each board carries 3 doors with D0 and D1 paired together (channels alternate D0/D1 per door).

### Board #1: Door slots 1, 2, 3 (D0+D1 per door)
| Ch | A side → Pi GPIO | B side → Mercury  |
|----|------------------|-------------------|
| 1  | IO4  (D0)        | Mercury D0        |
| 2  | IO27 (D1)        | Mercury D1        |
| 3  | IO23 (D0)        | Mercury D0        |
| 4  | IO22 (D1)        | Mercury D1        |
| 5  | IO25 (D0)        | Mercury D0        |
| 6  | IO24 (D1)        | Mercury D1        |
| 7-8| (unused)         | —                 |

### Board #2: Door slots 4, 5, 6 (D0+D1 per door)
| Ch | A side → Pi GPIO | B side → Mercury  |
|----|------------------|-------------------|
| 1  | MOSI (D0)        | Mercury D0        |
| 2  | MISO (D1)        | Mercury D1        |
| 3  | IO17 (D0)        | Mercury D0        |
| 4  | IO18 (D1)        | Mercury D1        |
| 5  | IO19 (D0)        | Mercury D0        |
| 6  | IO16 (D1)        | Mercury D1        |
| 7-8| (unused)         | —                 |

---

## Relay Module Assignments (GPIO-Direct DPS/REX Only)

Skip this whole section if routing DPS/REX through the Modbus relay board instead.

### Module #1: Door slots 1 & 2
| Relay | GPIO Input | Relay Contacts → Mercury |
|-------|------------|--------------------------|
| 1     | IO21       | COM+NC → DPS          |
| 2     | IO13       | COM+NC → REX          |
| 3     | IO12       | COM+NC → DPS          |
| 4     | IO20       | COM+NC → REX          |

### Module #2: Door slots 3 & 4
| Relay | GPIO Input | Relay Contacts → Mercury |
|-------|------------|--------------------------|
| 1     | RXD        | COM+NC → DPS          |
| 2     | TXD        | COM+NC → REX          |
| 3     | SCLK       | COM+NC → DPS          |
| 4     | CE0        | COM+NC → REX          |

### Module #3: Door slots 5 & 6
| Relay | GPIO Input | Relay Contacts → Mercury |
|-------|------------|--------------------------|
| 1     | IO5        | COM+NC → DPS          |
| 2     | IO6        | COM+NC → REX          |
| 3     | IO26       | COM+NC → DPS          |
| 4     | CE1        | COM+NC → REX          |

**Use NC (Normally Closed) terminals:** When relay is OFF (GPIO HIGH), contact is closed (door closed/no motion)

---

## Power Connections

### Level Shifter Boards (as many as needed)
- **VCCA** ← Pi 3.3V (from breakout)
- **VCCB** ← External 5V supply
- **GND** ← Common ground

### Relay Modules (GPIO-direct DPS/REX only)
- **VCC** ← Pi 5V or external 5V supply
- **GND** ← Common ground (Pi + 5V supply + Mercury panel)

---

## Pre-Power Checklist

- [ ] All power OFF during wiring
- [ ] No 3.3V to 5V shorts (check with multimeter)
- [ ] All GNDs connected together
- [ ] External 5V supply tested and regulated
- [ ] Wire labels applied
- [ ] Connections tight in screw terminals
- [ ] Visual inspection: no exposed conductors touching
- [ ] Door Config pin values match what's actually wired

---

## Power-Up Sequence

1. External 5V supply ON
2. Raspberry Pi ON
3. Mercury panel ON (or enable reader circuits)

---

## Shopping List

- [ ] TXS0108E level shifter boards, 1 per 3 doors (SparkFun BOB-15439 — for Wiegand D0/D1)
- [ ] 4-channel 3.3V relay modules, 1 per 2 doors (SainSmart or similar — GPIO-direct DPS/REX only)
- [ ] 22-24 AWG stranded wire (multicolor kit)
- [ ] Wire ferrules + crimper (optional but recommended)
- [ ] 5V 1A external power supply (for level shifters)
- [ ] Multimeter (for testing)

---

**EMERGENCY: If you see smoke or smell burning, CUT POWER IMMEDIATELY**

Check for shorts, reversed polarity, or incorrect voltage connections before reapplying power.
