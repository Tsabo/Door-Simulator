using System.ComponentModel;

namespace DoorSim.Shared.Models;

/// <summary>
/// How the PD replies to an incoming osdp_COMSET address/baud-rate change command.
/// </summary>
/// <remarks>
/// <see cref="Ignore" /> must remain the first (zero) member: it is the simulator's original
/// behaviour, and it is what a column SQLite adds to pre-existing rows resolves to.
/// <para>
/// The names describe what actually happens on the wire, not what a PD is nominally
/// "agreeing" to. OSDP.Net's base <c>Device</c> inspects the reply: a pdCOMMSET reply whose
/// address differs from the current one causes the base class to update its own device
/// configuration, which the connection loop re-reads on every incoming connection. Since
/// that update is not reachable from a derived class, a reply carrying a new address
/// unavoidably takes effect on the next reconnect — hence <see cref="AcceptAddress" /> rather
/// than any name implying the change is discarded.
/// </para>
/// </remarks>
[Description("How the PD replies to an osdp_COMSET address/baud-rate change command.")]
public enum OsdpComsetBehavior
{
    [Description("Reply with the PD's actual address and baud rate, discarding the request. Default.")]
    Ignore,

    [Description("Reply with the requested address and baud rate. The new address takes effect on the next connection; the baud rate is never applied.")]
    AcceptAddress,

    [Description("Reply Nak (unsupported command) — the PD declares it does not support COMSET.")]
    Nak
}
