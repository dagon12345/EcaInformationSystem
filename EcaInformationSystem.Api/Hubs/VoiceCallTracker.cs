using System.Collections.Concurrent;

namespace EcaInformationSystem.Api.Hubs
{
    public class VoiceCallTracker
    {
        // ✅ Tracks how many active VoiceCallHub connections each user has
        // (multiple tabs/devices) — same shape as ChatPresenceTracker, kept
        // separate since a user can be connected here independently of chat.
        private readonly ConcurrentDictionary<Guid, int> _connectionCounts = new();

        // Who each user is currently in a call with — lets a hard disconnect
        // (tab closed, network drop) still notify the other party instead of
        // leaving them hanging indefinitely.
        private readonly ConcurrentDictionary<Guid, Guid> _activeCallPartner = new();

        // The VoiceCallLog row backing the current call, keyed the same way
        // as _activeCallPartner so both are set/cleared together.
        private readonly ConcurrentDictionary<Guid, Guid> _activeCallLogId = new();

        public bool UserConnected(Guid userId)
        {
            var newCount = _connectionCounts.AddOrUpdate(userId, 1, (_, count) => count + 1);
            return newCount == 1;
        }

        public bool UserDisconnected(Guid userId)
        {
            if (!_connectionCounts.TryGetValue(userId, out var count))
                return false;

            var newCount = count - 1;
            if (newCount <= 0)
            {
                _connectionCounts.TryRemove(userId, out _);
                return true;
            }

            _connectionCounts[userId] = newCount;
            return false;
        }

        public bool IsOnline(Guid userId) => _connectionCounts.ContainsKey(userId);

        public void SetCallPartner(Guid userId, Guid partnerId)
        {
            _activeCallPartner[userId] = partnerId;
            _activeCallPartner[partnerId] = userId;
        }

        public Guid? GetCallPartner(Guid userId)
            => _activeCallPartner.TryGetValue(userId, out var partnerId) ? partnerId : null;

        public void ClearCallPartner(Guid userId)
        {
            if (_activeCallPartner.TryRemove(userId, out var partnerId))
            {
                _activeCallPartner.TryRemove(partnerId, out _);
                _activeCallLogId.TryRemove(userId, out _);
                _activeCallLogId.TryRemove(partnerId, out _);
            }
        }

        public void SetCallLog(Guid userId, Guid partnerId, Guid logId)
        {
            _activeCallLogId[userId] = logId;
            _activeCallLogId[partnerId] = logId;
        }

        public Guid? GetCallLogId(Guid userId)
            => _activeCallLogId.TryGetValue(userId, out var logId) ? logId : null;
    }
}
