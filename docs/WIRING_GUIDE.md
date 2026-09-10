# DoorSim Wiring Guide — Wiegand-GPIO-Direct Doors

> **This is not the current default rig.** The primary, currently-deployed
> architecture is OSDP over RS-485 with DPS/REX on a Modbus relay board —
> see [WIRING_DIAGRAM.md](WIRING_DIAGRAM.md), [PI_SETUP.md](PI_SETUP.md), and
> [RS485_ADAPTERS.md](RS485_ADAPTERS.md) for that. This guide covers the
> **alternative** path: wiring a door with `Protocol: Wiegand` straight to a
> Mercury panel over GPIO, with DPS/REX also on GPIO instead of the Modbus
> relay board. It's still fully supported per door via the
> `D0Pin`/`D1Pin`/`DpsPin`/`RexPin` fields on `DoorConfiguration` — there's
> just no longer a fixed reader-to-pin table. **Every pin below is an
> example**; you assign the actual BCM pin numbers per door in the web UI's
> Door Config, and wire to match whatever you chose there. See
> [`../source/src/DoorSim.Shared/Models/DoorConfiguration.cs`](../source/src/DoorSim.Shared/Models/DoorConfiguration.cs)
> for the full set of per-door fields.

## ⚠️ CRITICAL SAFETY WARNINGS

### Signal Type Differences
Mercury panels have TWO types of inputs that require different approaches:

**Wiegand (D0/D1):** Digital logic signals
- **Raspberry Pi GPIO:** 3.3V logic (NOT 5V tolerant!)
- **Mercury Panel:** Expects 5V logic
- **REQUIRED:** Level shifters for D0/D1 signals

**DPS/REX:** Contact closure inputs (supervised or unsupervised)
- **Mercury Panel:** Monitors open/closed contacts or resistance values
- **NOT digital logic** — panel has pull-up resistors and expects relay contacts
- **REQUIRED:** Relay modules for DPS/REX signals (only if wiring DPS/REX to
  GPIO — skip this if routing DPS/REX through the Modbus relay board instead)

### Before ANY Wiring
1. **POWER OFF** all equipment (Pi, Mercury panels, power supplies)
2. **DOUBLE CHECK** all connections before applying power
3. **TEST** with a multimeter — verify no shorts between 3.3V/5V/GND
4. **START** with ONE door, verify operation, then add remaining doors

---

## Hardware Requirements

### Core Components
- **Raspberry Pi** with a 40-pin GPIO header
- **40-Pin GPIO Breakout Board** with screw terminals (recommended)
- ⚠️ **Bidirectional Level Shifters** for Wiegand D0/D1 (REQUIRED)
- ⚠️ **Relay Modules** for GPIO-direct DPS/REX (REQUIRED, unless using the
  Modbus relay board instead)
- 🔌 **Wire:** 22-24 AWG stranded, multiple colors for signal identification
- 🔩 **Crimp terminals or ferrules** for screw terminal connections (optional but recommended)

Size level shifters and relay modules to however many doors you actually
plan to wire this way — the example below sizes for up to 6 concurrent
Wiegand-GPIO doors, but a single door only needs one level-shifter channel
pair and (if GPIO-direct) one relay pair.

### Level Shifter Recommendation (D0/D1 Only)

**TXS0108E 8-Channel Bidirectional Level Shifter (RECOMMENDED)**
- Product: SparkFun BOB-15439 or Adafruit 395
- Channels: 8 per board
- Speed: Fast enough for Wiegand (supports MHz range)
- Quantity needed: **1 board per 3 doors** (2 Wiegand channels per door)
- Cost: ~$5-8 each
- Auto-direction sensing (no need to configure input/output)

**TXS0108E A/B side orientation:**
- **A side (VCCA)** — low-voltage side, connect to **Pi 3.3V and Pi GPIO** signals
- **B side (VCCB)** — high-voltage side, connect to **5V supply and Mercury panel** inputs
- Rule of thumb: A = Pi, B = Mercury

**Alternative:** TXB0108 8-Channel Bidirectional Level Shifter
- Similar specs, also works well for this application

### Relay Module Recommendation (GPIO-Direct DPS/REX Only)

**4-Channel 3.3V Relay Module with Optocoupler (RECOMMENDED)**
- Trigger voltage: 3.3V (Pi GPIO compatible)
- Relay type: SPDT (Single Pole Double Throw) or SPST-NO
- Optocoupler isolation: YES (protects Pi from relay coil back-EMF)
- Quantity needed: **1 module per 2 doors** (2 relay channels per door)
- Cost: ~$8-15 each
- Examples: SainSmart, ELEGOO, or HiLetgo 4-channel modules

**Key relay specs to verify:**
- Coil voltage: 3.3V or 5V (if 5V, must support 3.3V trigger via optocoupler)
- Contact rating: 5A @ 250VAC is typical (way overkill for Mercury panel, but standard)
- NC (Normally Closed) contacts available: YES (you'll use COM and NC terminals)

---

## Example Pin Allocation (up to 6 Wiegand-GPIO doors)

### BCM GPIO to Breakout Terminal Mapping

The breakout board terminals are labeled with "IO" + BCM number. This table
is a **suggested starting allocation** — enter whichever pins you actually
wire into each door's `D0Pin`/`D1Pin`/`DpsPin`/`RexPin` fields in Door
Config, and `DoorConfigService` will reject any pin you reuse across doors.

| Door slot | Function | BCM GPIO | Breakout Terminal | Mercury Panel Connection |
|--------|----------|----------|-------------------|--------------------------|
| **1**  | D0       | GPIO4    | **IO4**           | Door 1 Wiegand D0      |
| **1**  | D1       | GPIO27   | **IO27**          | Door 1 Wiegand D1      |
| **1**  | DPS      | GPIO21   | **IO21**          | Door 1 Door Position   |
| **1**  | REX      | GPIO13   | **IO13**          | Door 1 Request to Exit |
| **2**  | D0       | GPIO23   | **IO23**          | Door 2 Wiegand D0      |
| **2**  | D1       | GPIO22   | **IO22**          | Door 2 Wiegand D1      |
| **2**  | DPS      | GPIO12   | **IO12**          | Door 2 Door Position   |
| **2**  | REX      | GPIO20   | **IO20**          | Door 2 Request to Exit |
| **3**  | D0       | GPIO25   | **IO25**          | Door 3 Wiegand D0      |
| **3**  | D1       | GPIO24   | **IO24**          | Door 3 Wiegand D1      |
| **3**  | DPS      | GPIO15   | **RXD**           | Door 3 Door Position   |
| **3**  | REX      | GPIO14   | **TXD**           | Door 3 Request to Exit |
| **4**  | D0       | GPIO10   | **MOSI**          | Door 4 Wiegand D0      |
| **4**  | D1       | GPIO9    | **MISO**          | Door 4 Wiegand D1      |
| **4**  | DPS      | GPIO11   | **SCLK**          | Door 4 Door Position   |
| **4**  | REX      | GPIO8    | **CE0**           | Door 4 Request to Exit |
| **5**  | D0       | GPIO17   | **IO17**          | Door 5 Wiegand D0      |
| **5**  | D1       | GPIO18   | **IO18**          | Door 5 Wiegand D1      |
| **5**  | DPS      | GPIO5    | **IO5**           | Door 5 Door Position   |
| **5**  | REX      | GPIO6    | **IO6**           | Door 5 Request to Exit |
| **6**  | D0       | GPIO19   | **IO19**          | Door 6 Wiegand D0      |
| **6**  | D1       | GPIO16   | **IO16**          | Door 6 Wiegand D1      |
| **6**  | DPS      | GPIO26   | **IO26**          | Door 6 Door Position   |
| **6**  | REX      | GPIO7    | **CE1**           | Door 6 Request to Exit |

> ⚠️ **RS-485/OSDP conflict:** GPIO14 (TXD) and GPIO15 (RXD) are the same
> pins used by the onboard-UART OSDP/Modbus RS-485 option in
> [WIRING_DIAGRAM.md](WIRING_DIAGRAM.md). If you assign door slot 3's
> DPS/REX to TXD/RXD *and* also use the onboard UART for OSDP or Modbus on
> another door, they'll conflict — pick different GPIO pins for one of them,
> or use a USB-RS485 adapter instead of the onboard UART.

### Ground Connections
- Connect **GND** terminal from breakout board to level shifters
- Connect level shifter GND to Mercury panel GND
- **CRITICAL:** All grounds must be common (Pi + level shifters + Mercury panels)

---

## Wiring Architecture

```
┌─────────────────┐
│ Raspberry Pi    │
│   40-Pin Header │
└────────┬────────┘
         │ Ribbon cable or direct connection
         │
┌────────▼────────────────────────────────────┐
│  GPIO Breakout Board (screw terminals)     │
│  IO4, IO5, IO6, IO7, IO8, IO9, IO10, ...   │
└────┬──────────────────┬────────────────────┘
     │                  │
     │ WIEGAND (D0/D1)  │ DPS/REX (if GPIO-direct)
     │ 22-24 AWG wire   │ 22-24 AWG wire
     │ (3.3V logic)     │ (3.3V logic)
     │                  │
┌────▼─────────────┐  ┌─▼──────────────────┐
│ Level Shifter #1 │  │ Relay Module #1    │
│ (Doors 1-3 D0+D1)│  │ (Doors 1 & 2)      │
│ VCCA: 3.3V       │  │ VCC: 5V (Pi or ext)│
│ VCCB: 5V         │  │ Inputs: 4 channels │
│ A1-A6 ← Pi GPIO  │  │ IO21,IO13,IO12,IO20│
│ B1-B6 → Mercury  │  │ Relays: COM/NC     │
└────┬─────────────┘  └─┬──────────────────┘
     │                  │
┌────▼─────────────┐  ┌─▼──────────────────┐
│ Level Shifter #2 │  │ Relay Module #2    │
│ (Doors 4-6 D0+D1)│  │ (Doors 3 & 4)      │
│ A1-A6 ← Pi GPIO  │  │ RXD,TXD,SCLK,CE0   │
│ B1-B6 → Mercury  │  └─┬──────────────────┘
└────┬─────────────┘    │
     │ 5V logic       ┌─▼──────────────────┐
     │                │ Relay Module #3    │
     │                │ (Doors 5 & 6)      │
     │                │ IO5,IO6,IO26,CE1   │
     │                └─┬──────────────────┘
     │                  │ Relay contacts (NC)
     │                  │
┌────▼──────────────────▼────────────────────┐
│  Mercury Security Panel                    │
│  Reader Input Terminals (one set per door) │
│  D0/D1: 5V logic | DPS/REX: contact closure│
└────────────────────────────────────────────┘
```

---

## Level Shifter Wiring Details (Wiegand D0/D1 Only)

Each board covers 3 doors with D0 and D1 paired on consecutive channels, so all wires for one door run to the same board.

**A side (VCCA = 3.3V) → Pi GPIO breakout** — **B side (VCCB = 5V) → Mercury panel**

**OE (Output Enable) pin:**
- Tie OE → **VCCA (3.3V)** on each board — permanently enabled (active HIGH)
- Do **NOT** leave OE floating — the chip powers up in a disabled/indeterminate state when OE is unconnected

### Level Shifter Board #1 — Doors 1, 2, 3 (example allocation)

**Power connections:**
- VCCA → Pi 3.3V (from breakout board 3V3 terminal) — **A side power**
- VCCB → External 5V supply — **B side power**
- GND → Common ground

**Signal mapping:**
| Shifter Channel | A side → Pi GPIO (3.3V) | B side → Mercury (5V) |
|-----------------|------------------------|----------------------|
| A1 / B1         | IO4  (Door 1 D0)       | Mercury D0        |
| A2 / B2         | IO27 (Door 1 D1)       | Mercury D1        |
| A3 / B3         | IO23 (Door 2 D0)       | Mercury D0        |
| A4 / B4         | IO22 (Door 2 D1)       | Mercury D1        |
| A5 / B5         | IO25 (Door 3 D0)       | Mercury D0        |
| A6 / B6         | IO24 (Door 3 D1)       | Mercury D1        |
| A7 / B7         | (unused)               | —                    |
| A8 / B8         | (unused)               | —                    |

### Level Shifter Board #2 — Doors 4, 5, 6 (example allocation)

**Power connections:** (same as board #1)

**Signal mapping:**
| Shifter Channel | A side → Pi GPIO (3.3V) | B side → Mercury (5V) |
|-----------------|------------------------|----------------------|
| A1 / B1         | MOSI (Door 4 D0)       | Mercury D0        |
| A2 / B2         | MISO (Door 4 D1)       | Mercury D1        |
| A3 / B3         | IO17 (Door 5 D0)       | Mercury D0        |
| A4 / B4         | IO18 (Door 5 D1)       | Mercury D1        |
| A5 / B5         | IO19 (Door 6 D0)       | Mercury D0        |
| A6 / B6         | IO16 (Door 6 D1)       | Mercury D1        |
| A7 / B7         | (unused)               | —                    |
| A8 / B8         | (unused)               | —                    |

---

## Relay Module Wiring Details (GPIO-Direct DPS/REX Only)

Skip this section entirely if routing DPS/REX through the Modbus relay
board instead — see [WIRING_DIAGRAM.md](WIRING_DIAGRAM.md).

### Relay Module #1 — Doors 1 & 2 (4 relays, example allocation)

**Power connections:**
- VCC → 5V (can use Pi 5V pin or external supply)
- GND → Common ground

**Control signal mapping:**
| Relay | GPIO Control (3.3V)| Relay Contacts → Mercury Panel |
|-------|--------------------|---------------------------------|
| 1     | IO21 (Door 1 DPS)  | COM + NC → Mercury DPS       |
| 2     | IO13 (Door 1 REX)  | COM + NC → Mercury REX       |
| 3     | IO12 (Door 2 DPS)  | COM + NC → Mercury DPS       |
| 4     | IO20 (Door 2 REX)  | COM + NC → Mercury REX       |

**Relay contact wiring:**
- Use **COM (Common)** and **NC (Normally Closed)** terminals
- When GPIO is HIGH (idle), relay is off → NC contact closed → Mercury sees "door closed"
- When GPIO is LOW (active), relay energizes → NC contact opens → Mercury sees "door open"

### Relay Module #2 — Doors 3 & 4 (4 relays, example allocation)

**Power connections:** (same as module #1)

**Control signal mapping:**
| Relay | GPIO Control (3.3V)| Relay Contacts → Mercury Panel |
|-------|--------------------|---------------------------------|
| 1     | RXD (Door 3 DPS)   | COM + NC → Mercury DPS       |
| 2     | TXD (Door 3 REX)   | COM + NC → Mercury REX       |
| 3     | SCLK (Door 4 DPS)  | COM + NC → Mercury DPS       |
| 4     | CE0 (Door 4 REX)   | COM + NC → Mercury REX       |

### Relay Module #3 — Doors 5 & 6 (4 relays, example allocation)

**Power connections:** (same as module #1)

**Control signal mapping:**
| Relay | GPIO Control (3.3V)| Relay Contacts → Mercury Panel |
|-------|--------------------|---------------------------------|
| 1     | IO5 (Door 5 DPS)   | COM + NC → Mercury DPS       |
| 2     | IO6 (Door 5 REX)   | COM + NC → Mercury REX       |
| 3     | IO26 (Door 6 DPS)  | COM + NC → Mercury DPS       |
| 4     | CE1 (Door 6 REX)   | COM + NC → Mercury REX       |

**Important:** Your Mercury panel may have supervision resistors configured. If so:
- Check panel documentation for required resistance values
- You may need to add resistors in series with relay contacts
- Typical values: 1kΩ - 10kΩ depending on panel model

---

## Power Supply Considerations

### Raspberry Pi Power
- Pi power: 5V, sized for your board (Pi 3B: 2.5A minimum via USB or GPIO header)

### External 5V Supply for Level Shifters
- Requirement: 5V regulated, 500mA minimum
- Connect to both level shifter VCCB pins
- Connect GND to common ground
- Options:
  - USB 5V power adapter with screw terminal breakout
  - 5V buck converter from 12V supply
  - Mercury panel's 5V output (check current capacity in manual)

### Relay Module Power
- Can use Pi's 5V pin (12 relays × ~70mA = ~840mA total, for 6 doors' worth)
- Monitor current — if Pi brownouts, use external 5V supply for relays
- Alternative: Power relay modules from same external 5V supply as level shifters
- Ensure common ground across all modules

---

## Wire Color Code Recommendation

Consistent color coding prevents errors:

| Signal Type | Suggested Color |
|-------------|-----------------|
| D0 (Wiegand)| Yellow          |
| D1 (Wiegand)| Blue            |
| DPS control | Green           |
| REX control | Orange          |
| Relay wiring| Brown/White     |
| GND         | Black           |
| 3.3V        | Red             |
| 5V          | Red (thicker)   |

Label both ends of every wire with the door label + function (e.g., "Door1-D0", "Door3-REX")

---

## Step-by-Step Assembly Procedure

### Phase 1: Bench Testing (BEFORE connecting to Mercury panels)

1. **Mount breakout board onto Raspberry Pi**
   - Ensure all 40 pins are properly seated
   - Secure with standoffs if provided

2. **Add the door in Door Config first**
   - Create the door in the web UI with `Protocol: Wiegand`, and set
     `D0Pin`/`D1Pin` (and `DpsPin`/`RexPin` if GPIO-direct) to the BCM pins
     you're about to wire
   - This is what tells DoorSim which pins to drive — wiring pins that
     aren't configured on a door does nothing

3. **Wire level shifter board #1 (one door only for initial test)**
   - Power off Pi
   - Connect VCCA to Pi 3.3V (from breakout board 3V3 terminal) — A side power
   - Connect VCCB to external 5V supply — NOT connected yet (B side power)
   - Connect GND to common ground
   - Wire A1/A2 to the breakout terminals matching the door's `D0Pin`/`D1Pin`

4. **Test with multimeter (5V supply still OFF)**
   - Verify NO continuity between 3.3V and 5V rails
   - Verify continuity between all GND points
   - Check each signal wire for correct terminal connection

5. **Apply power and test with LEDs**
   - Power on Pi
   - Power on external 5V supply
   - Connect LED + resistor (1kΩ) from each B-side output to GND
   - Run DoorSim software
   - Trigger a card read for that door — you should see LED flashing on D0/D1
   - Verify DPS and REX outputs change state (if GPIO-direct)

6. **Measure voltages**
   - Verify A-side signals are 0V / 3.3V
   - Verify B-side signals are 0V / 5V
   - If voltages incorrect, DO NOT PROCEED — troubleshoot first

### Phase 2: Connect to the Mercury Panel

7. **Mercury panel prep**
   - POWER OFF Mercury panel
   - Locate this door's reader input terminals (D0, D1, typically also has ground)
   - Locate DPS and REX input terminals for this door

8. **Wire connection**
   - Connect the level shifter's B-side outputs to the Mercury panel
   - Connect GND from level shifter to Mercury panel GND
   - **DO NOT apply power yet**

9. **Final inspection**
   - Visual check: no loose wires, no exposed conductor near other terminals
   - Multimeter: verify no shorts between signals
   - Verify polarity of all power connections

10. **Power-up sequence**
    - Power on external 5V supply
    - Power on Raspberry Pi
    - Power on Mercury panel (or enable that reader's circuit)

11. **Functional test**
    - Run DoorSim application
    - Send a test card from this door
    - Check Mercury panel logs for credential received
    - Test DPS door cycle
    - Test REX trip

### Phase 3: Add Remaining Doors

12. **Repeat for each additional door**
    - Power off ALL equipment
    - Add the door in Door Config with its own pins
    - Wire the corresponding level-shifter/relay channels
    - Test each door before proceeding to the next

---

## Troubleshooting Guide

### Pi GPIO Not Changing State
- Check code is running: `sudo chrt -f 99 dotnet DoorSim.dll`
- Verify GPIO permissions: user must be in `gpio` group
- Check breakout board connection: reseat 40-pin connector
- Verify the door's `D0Pin`/`D1Pin`/`DpsPin`/`RexPin` in Door Config
  actually match what you wired
- Test with `raspi-gpio get [pin]` command

### Level Shifter Not Working
- **Symptom:** A-side changes, B-side stays constant
  - Check VCCB 5V supply is connected and powered
  - Check VCCA 3.3V connection
  - Verify GND is common across all boards
- **Symptom:** B-side voltage is 3.3V instead of 5V
  - VCCB not connected or not powered
  - Wrong level shifter type (non-auto, wrong direction)

### Mercury Panel Not Responding
- **Wiegand not detected:**
  - Check D0/D1 polarity — may be reversed
  - Verify panel Wiegand format matches the door's configured `WiegandFormat`
  - Check panel configuration — reader enabled?
  - Measure signal at panel terminals with oscilloscope
- **DPS/REX not working:**
  - Check relay circuit configuration on panel
  - Verify normally-closed vs. normally-open setting (`DpsNormallyOpen`/`RexNormallyOpen`)
  - Test signal state at rest (should be HIGH = 5V)

### Intermittent Operation
- **Check for:**
  - Loose screw terminal connections — tighten all
  - Wire too thin (resistance causing voltage drop)
  - EMI from nearby equipment — route wires away from power cables
  - Ground loop issues — verify single-point ground connection

---

## Signal Idle States (For Testing)

DoorSim simulates **normally closed (NC)** door switches and REX sensors using relay contacts:

| Line | Pi GPIO State | Relay State    | Mercury Panel Sees      | Meaning                 |
|------|---------------|----------------|-------------------------|-------------------------|
| D0   | HIGH          | (N/A - direct) | 5V idle                 | Wiegand idle            |
| D1   | HIGH          | (N/A - direct) | 5V idle                 | Wiegand idle            |
| DPS  | HIGH          | Relay OFF      | **NC contact closed**   | Door closed (normal)    |
| REX  | HIGH          | Relay OFF      | **NC contact closed**   | No motion (normal)      |

**When simulation runs:**

| Event          | Pi GPIO | Relay    | NC Contact | Mercury Sees  | Panel Interprets |
|----------------|---------|----------|------------|---------------|------------------|
| Wiegand D0 bit | Pulses  | —        | —          | 0V pulse      | Data bit 0       |
| Wiegand D1 bit | Pulses  | —        | —          | 0V pulse      | Data bit 1       |
| Door opens     | LOW     | Energize | **Opens**  | Open circuit  | Door open        |
| Door closes    | HIGH    | De-energize | **Closes** | Closed circuit | Door closed      |
| REX trips      | LOW     | Energize | **Opens**  | Open circuit  | Motion detected  |
| REX resets     | HIGH    | De-energize | **Closes** | Closed circuit | No motion        |

Test procedure:
1. **At idle:** D0/D1 measure 5V on Mercury terminals; DPS/REX contacts should be closed
2. **During Wiegand:** D0/D1 should pulse LOW (0V) for ~50µs
3. **Door opens:** DPS GPIO goes LOW → relay energizes → NC contact opens → Mercury sees open circuit
4. **REX trips:** REX GPIO goes LOW → relay energizes → NC contact opens → Mercury sees open circuit

**Important:** If your Mercury panel has supervision resistors enabled, the idle state won't be a simple closed contact but rather a specific resistance value. Check your panel configuration and add resistors if needed.

---

## Mercury Panel Input Configuration

You'll need to configure each Mercury panel to match your simulator:

### Wiegand Settings
- Format: per the door's configured `WiegandFormat` (26-bit, 34-bit, 37-bit, or HID Corporate 1000)
- Facility code verification: disabled (simulator sends test codes)
- Pulse width: 50µs (matches `WiegandTransmitter.PulseUs`)
- Bit gap: 2000µs (matches `WiegandTransmitter.BitGapUs`)

### Door Contact Settings
- DPS (Door Position Switch): Normally Closed (unless `DpsNormallyOpen` is set on the door)
- Supervised: No (unless you add a supervision resistor)
- Timeout: Configure per security policy

### REX Settings
- Circuit type: Normally Closed (unless `RexNormallyOpen` is set on the door)
- Timeout: Configure per security policy (typically 30 seconds)

---

## Testing Checklist

Before connecting to live Mercury panels:

- [ ] All level shifter boards wired and tested with LED/multimeter
- [ ] Voltage levels verified: 3.3V on A-side, 5V on B-side
- [ ] No shorts between power rails
- [ ] Common ground verified across all equipment
- [ ] DoorSim software tested on bench with oscilloscope/LED
- [ ] Wire labels applied to both ends
- [ ] External 5V supply tested under load
- [ ] Cable routing planned (away from power/EMI sources)

Initial Mercury connection (one door only):

- [ ] Mercury panel powered OFF during wiring
- [ ] All connections double-checked before power-on
- [ ] Power-up sequence followed (5V ext → Pi → Mercury)
- [ ] Test card send successful
- [ ] Mercury panel logs show credential received
- [ ] DPS door cycle successful
- [ ] REX trip cycle successful

Repeat for each additional door before considering the rig complete.

---

## Component Shopping List (example: 6 Wiegand-GPIO doors)

| Item | Quantity | Estimated Cost | Notes |
|------|----------|----------------|-------|
| TXS0108E Level Shifter Boards | 2 | $10-16 | SparkFun BOB-15439 (for Wiegand only) |
| 4-Channel 3.3V Relay Modules | 3 | $24-45 | SainSmart or equivalent (GPIO-direct DPS/REX only) |
| 22 AWG Stranded Wire (multiple colors) | 100+ ft | $15-30 | Buy 10-color kit |
| Wire ferrules (22-24 AWG) | 100 pk | $8-12 | Optional but highly recommended |
| Ferrule crimper tool | 1 | $15-30 | If using ferrules |
| 5V 1A Power Supply | 1 | $8-15 | USB or barrel jack with terminals |
| Multimeter | 1 | $15-50 | If you don't already have one |
| **Total** | | **~$95-198** | Varies by quality/brand; scale down for fewer doors |

---

## Additional Resources

### Raspberry Pi GPIO Documentation
- Official pinout: https://pinout.xyz
- GPIO documentation: https://www.raspberrypi.com/documentation/computers/os.html#gpio

### Wiegand Protocol
- Standard: 26-bit, 34-bit, 37-bit, HID Corporate 1000 formats
- Timing: 50µs pulses, 2ms gaps (`WiegandTransmitter` already implements this)

### Level Shifter Datasheets
- TXS0108E: https://www.ti.com/product/TXS0108E
- Application notes for bidirectional level shifting

### Mercury Panel Documentation
- Consult your specific Mercury model manual for:
  - Reader input specifications
  - Terminal block pinout
  - Configuration procedures
  - Diagnostic LED meanings

---

## Support & Maintenance

### If You Encounter Issues

1. Test each component in isolation (Pi → breakout → level shifter → multimeter)
2. Use an oscilloscope to verify Wiegand signal timing
3. Check Mercury panel event logs for diagnostic info
4. Verify the door's Door Config pin values match what's actually wired

### Regular Maintenance

- Periodically check screw terminal tightness
- Verify no corrosion on exposed connections
- Test all Wiegand-GPIO doors monthly to ensure reliability
- Keep spare level shifter boards on hand

---

**Document Version:** 2.0 — rewritten to reflect dynamic per-door pin
configuration instead of a fixed 6-reader table.
**Last Updated:** 2026-09-09
**Author:** DoorSim Project
**Hardware Target:** Raspberry Pi + Mercury Security Panels (Wiegand-GPIO-direct doors only)
