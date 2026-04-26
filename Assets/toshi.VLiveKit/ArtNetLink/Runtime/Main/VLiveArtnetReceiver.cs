// VLiveKit is all Unlicense.
// unlicense: https://unlicense.org/
// this comment & namespace can be removed.
// last update: 2024/11/25

using UnityEngine;
using System.Collections.Generic;
using System;
using toshi.VLiveKit.Lighting;

namespace toshi.VLiveKit.Lighting
{
    [AddComponentMenu("toshi.VLiveKit.Lighting/VLiveArtNetReceiver")]
    public sealed class VLiveArtNetReceiver : MonoBehaviour
    {
        public event Action<byte[]> OnDataReceived;
        [Range(0, 64)]
        [SerializeField] public int _universeToUse = 1;
        private Dictionary<int, Queue<byte[]>> _universeQueues = new Dictionary<int, Queue<byte[]>>();
        // private Dictionary<int, int> _universeQueueCounts = new Dictionary<int, int>();

        [Header("[ArtNet IP Address & Port]")]
        [SerializeField] ArtNetConnection _connection = null;

        int _currentPort;
        string _currentHost;

        // in touch designer, 5 is used
        const int MaxQueueSize = 5;

        void RegisterCallback()
        {
            var port = _connection?.port ?? 6454;
            var host = _connection?.host ?? "127.0.0.1";

            var server = ArtNetMaster.GetSharedServer(host, port);
            server.MessageDispatcher.AddCallback(OnDataReceive);

            _currentPort = port;
            _currentHost = host;
        }

        void UnregisterCallback()
        {
            if (_currentPort == 0)
            {
                // OnEnableで呼ばれるので、ここではreturn
                return;
            }
            

            var server = ArtNetMaster.GetSharedServer(_currentHost, _currentPort);
            server.MessageDispatcher.RemoveCallback(OnDataReceive);
        }

        void OnEnable()
        {
            UnregisterCallback();
            RegisterCallback();
        }

        void OnDisable()
        {
            UnregisterCallback();
            // ArtNetMaster.ClearServers();
        }

        void OnDestroy()
        {
            ArtNetMaster.ClearServers();
        }

        void Update()
        {
            if (_universeQueues.ContainsKey(_universeToUse))
            {
                ProcessQueue(_universeToUse);
            }
            else
            {
                // Debug.Log("No universe queue found for universe: " + _universeToUse);
            }
        }

        void ProcessQueue(int universe)
        {
            // キューになってる時点でArtNetであることは確定
            var queue = _universeQueues[universe];
            while (queue.Count > 0)
            {
                var buffer = queue.Dequeue();
                OnDataReceived?.Invoke(buffer);
            }
        }

        void OnDataReceive(ArtNetDataHandle data)
        {
            var _sharedBuffer = data.GetSharedBuffer();
            var bufferCopy = new byte[_sharedBuffer.Length];
            Array.Copy(_sharedBuffer, bufferCopy, _sharedBuffer.Length);
            int universe = data.GetUniverse();

            lock (this)
            {
                if (!_universeQueues.ContainsKey(universe))
                {
                    _universeQueues[universe] = new Queue<byte[]>();
                }

                var queue = _universeQueues[universe];
                if (queue.Count >= MaxQueueSize)
                {
                    queue.Dequeue();
                }
                queue.Enqueue(bufferCopy);
            }
        }
    }
}