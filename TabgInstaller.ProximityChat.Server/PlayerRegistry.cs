using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;

namespace TabgInstaller.ProximityChat.Server
{
    public class PlayerRegistry
    {
        private readonly ConcurrentDictionary<int, IPEndPoint> _playerEndpoints = new ConcurrentDictionary<int, IPEndPoint>();
        private readonly ConcurrentDictionary<string, int> _ipToPlayerId = new ConcurrentDictionary<string, int>();

        public bool TryRegister(IPEndPoint endpoint, int playerId)
        {
            string ipKey = endpoint.Address.ToString();
            _ipToPlayerId[ipKey] = playerId;
            _playerEndpoints[playerId] = endpoint;
            return true;
        }

        public bool TryGetPlayerIdByIp(IPEndPoint endpoint, out int playerId)
        {
            string ipKey = endpoint.Address.ToString();
            return _ipToPlayerId.TryGetValue(ipKey, out playerId);
        }

        public bool TryGetEndpoint(int playerId, out IPEndPoint endpoint)
        {
            return _playerEndpoints.TryGetValue(playerId, out endpoint);
        }

        public void Remove(int playerId)
        {
            if (_playerEndpoints.TryRemove(playerId, out var ep))
            {
                string ipKey = ep.Address.ToString();
                _ipToPlayerId.TryRemove(ipKey, out _);
            }
        }

        public ICollection<int> GetAllPlayerIds()
        {
            return _playerEndpoints.Keys;
        }

        public void Clear()
        {
            _playerEndpoints.Clear();
            _ipToPlayerId.Clear();
        }
    }
}
