# DoorSim Wiring Diagrams

These diagrams cover the **default rig**: OSDP over RS-485 for card reads,
with DPS/REX on a Modbus relay board. For the alternative per-door path —
`Protocol: Wiegand` wired straight to GPIO, with DPS/REX also on GPIO
instead of the Modbus board — see
[WIRING_GUIDE.md](WIRING_GUIDE.md) (full guide) or
[WIRING_QUICK_REFERENCE.md](WIRING_QUICK_REFERENCE.md) (printable card).

---

## OSDP RS-485 — Single ACU Reader Port, Multi-Drop

All simulated OSDP readers share **one RS-485 segment** on a single ACU reader port.
Each door is assigned a unique OSDP address (0–5). The Pi is the OSDP slave;
the Mercury ACU reader port is the master that polls each address.

```mermaid
graph LR
    subgraph Pi["Raspberry Pi 4"]
        TXD["GPIO14\nbreakout: TXD"]
        RXD["GPIO15\nbreakout: RXD"]
    end

    subgraph MAX485["MAX485 Module\n3.3V TTL ↔ RS-485\nVCC = 3V3"]
        TTL["TTL side\nVCC · GND · TXD · RXD"]
        RS485["RS-485 side\nTRx+ / A  ·  TR− / B"]
    end

    subgraph Bus["RS-485 Bus — 2-wire twisted pair — 9600 baud"]
        A["A+ wire"]
        B["B− wire"]
    end

    subgraph Mercury["Mercury ACU — Single OSDP reader port"]
        PORT["RS-485 port\nA / B"]
    end

    subgraph DoorSim["DoorSim  ·  /dev/ttyAMA0  ·  9600 baud"]
        S0["OSDP Addr 0 — Door 1"]
        S1["OSDP Addr 1 — Door 2"]
        S2["OSDP Addr 2 — Door 3"]
        S3["OSDP Addr 3 — Door 4"]
        S4["OSDP Addr 4 — Door 5"]
        S5["OSDP Addr 5 — Door 6"]
    end

    TXD <-->|"3.3V serial TX"| TTL
    RXD <-->|"3.3V serial RX"| TTL
    TTL <--> RS485
    RS485 --- A & B
    A & B --- PORT
    S0 & S1 & S2 & S3 & S4 & S5 -.- DoorSim
```

### MAX485 pin connections

| MAX485 pin  | Connect to                    |
|-------------|-------------------------------|
| VCC         | 3V3 (breakout block 2)        |
| GND         | GND (breakout block 2)        |
| TXD         | TXD terminal (GPIO14)         |
| RXD         | RXD terminal (GPIO15)         |
| TRx+ / A    | Mercury ACU RS-485 A          |
| TR− / B     | Mercury ACU RS-485 B          |

> **Label note:** Some modules mark the RS-485 side `A+/B−` or `TRx+/TR−` — these are equivalent.
> If OSDP never connects, swap the A and B wires at the ACU port.

---

## OSDP RS-485 — Multiple ACU Reader Ports

Use one USB-to-RS485 dongle per ACU reader port. Each dongle is an independent
RS-485 segment — no address conflicts. Each door configured with OSDP address 0.

```mermaid
graph LR
    subgraph Pi["Raspberry Pi 4"]
        UART["/dev/ttyAMA0\nGPIO14/15 + MAX485"]
        USB0["/dev/ttyUSB0\nUSB dongle"]
        USB1["/dev/ttyUSB1\nUSB dongle"]
        USB2["/dev/ttyUSB2\nUSB dongle"]
        USB3["/dev/ttyUSB3\nUSB dongle"]
    end

    subgraph Mercury["Mercury ACU — Reader Ports"]
        P1["Port 1"]
        P2["Port 2"]
        P3["Port 3"]
        P4["Port 4"]
        P5["Port 5"]
    end

    UART  -->|"A / B"| P1
    USB0  -->|"A / B"| P2
    USB1  -->|"A / B"| P3
    USB2  -->|"A / B"| P4
    USB3  -->|"A / B"| P5
```

> The onboard UART (`/dev/ttyAMA0`) handles one port; USB dongles handle the rest.
> Each DoorSim door entry gets its own **Serial Port** value and **OSDP Address 0**.

---

## Modbus RS-485 — 16-Channel Relay Module (DPS / REX)

DPS and REX contacts for all 6 doors are driven by a single 16-channel DC 12V Modbus RTU
relay module on Bus 2. UART2 (GPIO0/1 → `/dev/ttyAMA2`) connects via a second MAX485 module.

```mermaid
graph LR
    subgraph Pi["Raspberry Pi 4"]
        TX2["GPIO0 / UART2 TX\nbreakout: IDSD"]
        RX2["GPIO1 / UART2 RX\nbreakout: IDSC"]
    end

    subgraph MAX2["MAX485 Module — Bus 2\n3.3V TTL ↔ RS-485  ·  VCC = 3V3"]
        TTL2["TXD · RXD"]
        AB2["TRx+ A  ·  TR− B"]
    end

    subgraph Relay["16-Ch Modbus RTU Relay Module\n12V DC  ·  /dev/ttyAMA2  ·  9600 baud"]
        DPS1["Ch 1   Door 1 DPS"]
        REX1["Ch 2   Door 1 REX"]
        DPS2["Ch 3   Door 2 DPS"]
        REX2["Ch 4   Door 2 REX"]
        DPS3["Ch 5   Door 3 DPS"]
        REX3["Ch 6   Door 3 REX"]
        DPS4["Ch 7   Door 4 DPS"]
        REX4["Ch 8   Door 4 REX"]
        DPS5["Ch 9   Door 5 DPS"]
        REX5["Ch 10  Door 5 REX"]
        DPS6["Ch 11  Door 6 DPS"]
        REX6["Ch 12  Door 6 REX"]
    end

    subgraph Mercury["Mercury ACU — Door Input Terminals"]
        M1["Door 1  Contact · REX"]
        M2["Door 2  Contact · REX"]
        M3["Door 3  Contact · REX"]
        M4["Door 4  Contact · REX"]
        M5["Door 5  Contact · REX"]
        M6["Door 6  Contact · REX"]
    end

    TX2 <-->|"3.3V serial"| TTL2
    RX2 <-->|"3.3V serial"| TTL2
    TTL2 <--> AB2
    AB2 -->|"RS-485 A / B"| Relay

    DPS1 & REX1 -->|"COM / NC"| M1
    DPS2 & REX2 -->|"COM / NC"| M2
    DPS3 & REX3 -->|"COM / NC"| M3
    DPS4 & REX4 -->|"COM / NC"| M4
    DPS5 & REX5 -->|"COM / NC"| M5
    DPS6 & REX6 -->|"COM / NC"| M6
```

### MAX485 Bus 2 pin connections

| MAX485 pin  | Connect to                                      |
|-------------|-------------------------------------------------|
| VCC         | 3V3                                             |
| GND         | GND                                             |
| TXD         | **IDSD** terminal (GPIO0 / UART2 TX) — Block 3 |
| RXD         | **IDSC** terminal (GPIO1 / UART2 RX) — Block 3 |
| DE + RE     | **GPIO2** (physical pin 3) — direction control  |
| TRx+ / A    | Relay module RS-485 A                           |
| TR− / B     | Relay module RS-485 B                           |

> **DE/RE wiring:** Tie `DE` and `RE` together on the MAX485 module and connect to **GPIO2**.
> HIGH = transmit, LOW = receive. Set `"Modbus": { "DeRePinBcm": 2 }` in `appsettings.json`.

> **Relay wiring:** Use **COM** and **NC** on each relay.
> NC closed (relay off) = contact closed = door closed / no REX.
> NC open (relay on) = contact open = door open / REX active.
