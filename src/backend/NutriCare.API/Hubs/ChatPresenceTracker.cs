namespace NutriCare.API.Hubs;

public interface IChatPresenceTracker
{
    Task<bool> UserConnectedAsync(long userId, string connectionId);
    Task<bool> UserDisconnectedAsync(long userId, string connectionId);
    bool IsOnline(long userId);
    IEnumerable<long> OnlineUsers { get; }
}

public class ChatPresenceTracker : IChatPresenceTracker
{
    private readonly Dictionary<long, HashSet<string>> _connections = new();
    private readonly object _gate = new();

    public Task<bool> UserConnectedAsync(long userId, string connectionId)
    {
        lock (_gate)
        {
            if (!_connections.TryGetValue(userId, out var connections)) _connections[userId] = connections = new HashSet<string>();
            var wasOffline = connections.Count == 0;
            connections.Add(connectionId);
            return Task.FromResult(wasOffline);
        }
    }

    public Task<bool> UserDisconnectedAsync(long userId, string connectionId)
    {
        lock (_gate)
        {
            if (!_connections.TryGetValue(userId, out var connections)) return Task.FromResult(false);
            connections.Remove(connectionId);
            if (connections.Count > 0)
            {
                return Task.FromResult(false);
            }

            _connections.Remove(userId);
            return Task.FromResult(true);
        }
    }

    public bool IsOnline(long userId) { lock (_gate) return _connections.TryGetValue(userId, out var connections) && connections.Count > 0; }

    public IEnumerable<long> OnlineUsers { get { lock (_gate) return _connections.Keys.ToArray(); } }
}
