using System.Collections.Concurrent;

namespace EcaInformationSystem.Api.Hubs
{
    public class ChatPresenceTracker
    {
        // ✅ Tracks how many active connections each user has (multiple tabs/devices).
        // A user is "online" as long as this count is > 0; only goes to 0 when
        // every single connection has closed.
        private readonly ConcurrentDictionary<Guid, int> _connectionCounts = new();

        public bool UserConnected(Guid userId)
        {
            var newCount = _connectionCounts.AddOrUpdate(userId, 1, (_, count) => count + 1);
            return newCount == 1; // true = this user just went online (was offline before)
        }

        public bool UserDisconnected(Guid userId)
        {
            if (!_connectionCounts.TryGetValue(userId, out var count))
                return false;

            var newCount = count - 1;
            if (newCount <= 0)
            {
                _connectionCounts.TryRemove(userId, out _);
                return true; // true = this user just went offline (no connections left)
            }

            _connectionCounts[userId] = newCount;
            return false;
        }

        public bool IsOnline(Guid userId) => _connectionCounts.ContainsKey(userId);

        public List<Guid> GetOnlineUserIds() => _connectionCounts.Keys.ToList();
    }
}