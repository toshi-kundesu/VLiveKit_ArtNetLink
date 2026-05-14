// VLiveKit is all Unlicense.
// unlicense: https://unlicense.org/
// this comment & namespace can be removed.
// last update: 2024/11/25

using UnityEngine;
using System.Collections.Generic;
using System;
using System.Collections.ObjectModel;

namespace toshi.VLiveKit.Lighting
{
    [AddComponentMenu("toshi.VLiveKit.Lighting/VLiveArtNetReceiver")]
    public sealed class VLiveArtNetReceiver : MonoBehaviour
    {
        public event Action<byte[]> OnDataReceived;

        public static ReadOnlyCollection<VLiveArtNetReceiver> ActiveReceivers
        {
            get
            {
                if (_activeReceiversReadOnly == null)
                    _activeReceiversReadOnly = new ReadOnlyCollection<VLiveArtNetReceiver>(_activeReceivers);
                return _activeReceiversReadOnly;
            }
        }

        public int UniverseToUse => _universeToUse;
        public string CurrentHost => string.IsNullOrEmpty(_currentHost) ? (_connection?.host ?? "127.0.0.1") : _currentHost;
        public int CurrentPort => _currentPort == 0 ? (_connection?.port ?? 6454) : _currentPort;

        public void UseLocalhost(int universeToUse = 0)
        {
            var shouldRestart = isActiveAndEnabled && _currentPort != 0;
            if (shouldRestart)
            {
                UnregisterCallback();
            }

            _connection = null;
            _universeToUse = Mathf.Clamp(universeToUse, 0, 64);
            _currentHost = null;
            _currentPort = 0;

            if (shouldRestart)
            {
                RegisterCallback();
            }
        }

        [Range(0, 64)]
        [SerializeField] public int _universeToUse = 0;
        private Dictionary<int, Queue<byte[]>> _universeQueues = new Dictionary<int, Queue<byte[]>>();
        private Dictionary<int, ArtNetUniverseMonitorState> _monitorStates = new Dictionary<int, ArtNetUniverseMonitorState>();

        [Header("[ArtNet IP Address & Port]")]
        [SerializeField] ArtNetConnection _connection = null;

        int _currentPort;
        string _currentHost;

        // in touch designer, 5 is used
        const int MaxQueueSize = 5;
        readonly object _syncRoot = new object();
        static List<VLiveArtNetReceiver> _activeReceivers = new List<VLiveArtNetReceiver>();
        static ReadOnlyCollection<VLiveArtNetReceiver> _activeReceiversReadOnly;

        public ArtNetUniverseMonitorSnapshot[] GetMonitorSnapshots()
        {
            lock (_syncRoot)
            {
                var snapshots = new ArtNetUniverseMonitorSnapshot[_monitorStates.Count];
                int index = 0;
                foreach (var pair in _monitorStates)
                    snapshots[index++] = pair.Value.CreateSnapshot(pair.Key);

                Array.Sort(snapshots, (a, b) => a.Universe.CompareTo(b.Universe));
                return snapshots;
            }
        }

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
                return;
            }

            var server = ArtNetMaster.GetSharedServer(_currentHost, _currentPort);
            server.MessageDispatcher.RemoveCallback(OnDataReceive);
        }

        void OnEnable()
        {
            if (!_activeReceivers.Contains(this))
                _activeReceivers.Add(this);

            UnregisterCallback();
            RegisterCallback();
        }

        void OnDisable()
        {
            UnregisterCallback();
            _activeReceivers.Remove(this);
            // ArtNetMaster.ClearServers();
        }

        void OnDestroy()
        {
            ArtNetMaster.ClearServers();
        }

        void Update()
        {
            List<byte[]> buffers = null;
            lock (_syncRoot)
            {
                if (_universeQueues.TryGetValue(_universeToUse, out var queue))
                {
                    buffers = new List<byte[]>(queue.Count);
                    while (queue.Count > 0)
                        buffers.Add(queue.Dequeue());
                }
            }

            if (buffers == null) return;
            for (var i = 0; i < buffers.Count; i++)
                OnDataReceived?.Invoke(buffers[i]);
        }

        void OnDataReceive(ArtNetDataHandle data)
        {
            var sharedBuffer = data.GetSharedBuffer();
            var bufferCopy = new byte[sharedBuffer.Length];
            Array.Copy(sharedBuffer, bufferCopy, sharedBuffer.Length);
            int universe = data.GetUniverse();

            lock (_syncRoot)
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

                if (!_monitorStates.TryGetValue(universe, out var monitorState))
                {
                    monitorState = new ArtNetUniverseMonitorState();
                    _monitorStates[universe] = monitorState;
                }
                monitorState.Update(data);
            }
        }

        class ArtNetUniverseMonitorState
        {
            public long PacketCount { get; private set; }
            public DateTime LastReceivedUtc { get; private set; }
            public int Sequence { get; private set; }
            public int Physical { get; private set; }
            public int Length { get; private set; }
            public byte[] Channels { get; private set; } = new byte[512];

            public void Update(ArtNetDataHandle data)
            {
                PacketCount++;
                LastReceivedUtc = DateTime.UtcNow;
                Sequence = data.GetSequence();
                Physical = data.GetPhysical();
                Length = (data.GetLengthHi() << 8) | data.GetLengthLo();

                var channels = data.GetArtNetChannels();
                var length = Math.Min(Channels.Length, channels.Length);
                Array.Clear(Channels, 0, Channels.Length);
                Array.Copy(channels, Channels, length);
            }

            public ArtNetUniverseMonitorSnapshot CreateSnapshot(int universe)
            {
                var channels = new byte[Channels.Length];
                Array.Copy(Channels, channels, Channels.Length);
                return new ArtNetUniverseMonitorSnapshot(universe, PacketCount, LastReceivedUtc, Sequence, Physical, Length, channels);
            }
        }
    }

    public readonly struct ArtNetUniverseMonitorSnapshot
    {
        public ArtNetUniverseMonitorSnapshot(int universe, long packetCount, DateTime lastReceivedUtc, int sequence, int physical, int length, byte[] channels)
        {
            Universe = universe;
            PacketCount = packetCount;
            LastReceivedUtc = lastReceivedUtc;
            Sequence = sequence;
            Physical = physical;
            Length = length;
            Channels = channels;
        }

        public int Universe { get; }
        public long PacketCount { get; }
        public DateTime LastReceivedUtc { get; }
        public int Sequence { get; }
        public int Physical { get; }
        public int Length { get; }
        public byte[] Channels { get; }

        public double SecondsSinceLastReceived
        {
            get
            {
                if (LastReceivedUtc == default) return double.PositiveInfinity;
                return (DateTime.UtcNow - LastReceivedUtc).TotalSeconds;
            }
        }
    }
}
