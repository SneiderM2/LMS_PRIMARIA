using System.Threading.Channels;
using LMS.API.Middleware;

namespace LMS.API.Services;

/// <summary>
/// Cola en memoria thread-safe sin bloqueo para persistir eventos de auditoría en la BD.
/// </summary>
public sealed class AuditLogQueue
{
    private readonly Channel<AuditLogEntry> _channel = Channel.CreateBounded<AuditLogEntry>(new BoundedChannelOptions(5000)
    {
        FullMode = BoundedChannelFullMode.DropOldest
    });

    public void Enqueue(AuditLogEntry entry)
    {
        _channel.Writer.TryWrite(entry);
    }

    public IAsyncEnumerable<AuditLogEntry> ReadAllAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
