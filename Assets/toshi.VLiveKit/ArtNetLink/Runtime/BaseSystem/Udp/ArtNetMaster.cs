// VLiveKit is all Unlicense.
// unlicense: https://unlicense.org/
// this comment & namespace can be removed.
// last update: 2024/11/27

using System.Collections.Generic;
using System.Text;

namespace toshi.VLiveKit.Lighting
{
    public static class ArtNetMaster
    {
        public static ArtNetServer GetSharedServer(string host, int port)
        {
            ArtNetServer server;
            if (!_servers.TryGetValue(port, out server))
            {
                server = new ArtNetServer(host, port);
                _servers[port] = server;
            }
            return server;
        }

        static Dictionary<int, ArtNetServer> _servers = new Dictionary<int, ArtNetServer>();

        public static void ClearServers()
        {
            // サーバがなければreturn
            if (_servers.Count == 0) 
            {
                // UnityEngine.Debug.Log("ClearServers: _servers.Count == 0 returned");
                return;
            }
            else
            {
                //UnityEngine.Debug.Log("ClearServers: _servers.Count != 0 clear");
            }
            // 全てのサーバをDisposeする
            foreach (var server in _servers)
            {
                server.Value.Dispose();
            }
            _servers.Clear();
        }
    }
}
