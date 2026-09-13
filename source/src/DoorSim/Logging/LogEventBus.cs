using System.Threading.Channels;

namespace DoorSim.Logging;

/// <summary>
/// In-memory log fan-out for the live log viewer: keeps the last 200 entries and broadcasts new
/// ones to live subscribers (the /api/logs/stream SSE endpoint). Instantiated as a plain object
/// before <c>UseSerilog</c> runs — Serilog sinks are built before the DI container exists — then
/// registered into DI by instance so <c>LogsEndpoints</c> can inject the same object the sink
/// writes into.
/// </summary>
public sealed class LogEventBus
{
    private const int BacklogCapacity = 200;
    private const int MaxSubscribers = 16;

    private readonly object _lock = new();
    private readonly Queue<LogLine> _backlog = new(BacklogCapacity);
    private readonly List<Channel<LogLine>> _subscribers = [];

    /// <summary>Called from the sink's Emit — must never block or throw.</summary>
    public void Publish(LogLine line)
    {
        lock (_lock)
        {
            _backlog.Enqueue(line);
            if (_backlog.Count > BacklogCapacity)
                _backlog.Dequeue();

            foreach (var channel in _subscribers)
                channel.Writer.TryWrite(line);
        }
    }

    /// <summary>Snapshot of the current backlog, oldest first.</summary>
    public LogLine[] GetBacklog()
    {
        lock (_lock)
            return [.. _backlog];
    }

    /// <summary>Registers a new live subscriber. Caller must call <see cref="Unsubscribe" /> when done.</summary>
    public ChannelReader<LogLine> Subscribe(out Channel<LogLine> channel)
    {
        channel = Channel.CreateBounded<LogLine>(new BoundedChannelOptions(64)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });

        lock (_lock)
        {
            if (_subscribers.Count >= MaxSubscribers)
                throw new InvalidOperationException("Too many concurrent log stream subscribers.");

            _subscribers.Add(channel);
        }

        return channel.Reader;
    }

    public void Unsubscribe(Channel<LogLine> channel)
    {
        lock (_lock)
            _subscribers.Remove(channel);
    }
}
