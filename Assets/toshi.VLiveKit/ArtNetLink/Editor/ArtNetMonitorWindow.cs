using System;
using System.Collections.Generic;
using System.Text;
using toshi.VLiveKit.Lighting;
using UnityEditor;
using UnityEngine;

namespace toshi.VLiveKit.ArtNetLink.Editor
{
    public sealed class ArtNetMonitorWindow : EditorWindow
    {
        const int PreviewChannelCount = 32;
        Vector2 _scroll;
        double _nextRepaintTime;
        string _host = "127.0.0.1";
        int _port = 6454;
        int _universeToUse = 1;
        string _error;
        bool _isListening;
        ArtNetServer _server;
        ArtNetMessageDispatcher.MessageCallback _standaloneCallback;
        int _sessionId;
        readonly object _syncRoot = new object();
        readonly Dictionary<int, MonitorState> _standaloneStates = new Dictionary<int, MonitorState>();

        [MenuItem("toshi/VLiveKit/Lighting/ArtNet Monitor")]
        public static void Open()
        {
            var window = GetWindow<ArtNetMonitorWindow>("ArtNet Monitor");
            window.minSize = new Vector2(520f, 320f);
        }

        void OnGUI()
        {
            DrawToolbar();
            DrawStandaloneMonitor();

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Scene Receivers (Optional)", EditorStyles.boldLabel);
            var receivers = VLiveArtNetReceiver.ActiveReceivers;
            if (receivers.Count == 0)
            {
                EditorGUILayout.HelpBox("No scene VLiveArtNetReceiver is active. Standalone Monitor above can still receive Art-Net without any scene object.", MessageType.None);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var receiver in receivers)
            {
                if (receiver == null) continue;
                DrawReceiver(receiver);
                EditorGUILayout.Space(8f);
            }
            EditorGUILayout.EndScrollView();
        }

        void OnDisable()
        {
            StopStandaloneMonitor();
        }

        void OnDestroy()
        {
            StopStandaloneMonitor();
        }

        void Update()
        {
            if (EditorApplication.timeSinceStartup < _nextRepaintTime) return;
            _nextRepaintTime = EditorApplication.timeSinceStartup + 0.1d;
            Repaint();
        }

        void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("ArtNet Monitor", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                GUILayout.Label(_isListening ? "LISTENING" : "STOPPED", EditorStyles.miniLabel);
            }
        }

        void DrawStandaloneMonitor()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Standalone VLiveArtNetReceiver", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("This creates a temporary UDP receiver owned by this window. A scene VLiveArtNetReceiver is not required.", MessageType.None);
                EditorGUILayout.Space(2f);
                EditorGUILayout.LabelField("[ArtNet IP Address & Port]", EditorStyles.boldLabel);

                using (new EditorGUI.DisabledScope(_isListening))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("Host", GUILayout.Width(92f));
                        _host = EditorGUILayout.TextField(_host);
                        EditorGUILayout.LabelField("Port", GUILayout.Width(34f));
                        _port = Mathf.Clamp(EditorGUILayout.IntField(_port, GUILayout.Width(72f)), 1, 65535);
                    }
                }

                EditorGUILayout.Space(2f);
                EditorGUILayout.LabelField("[ArtNet Receiver]", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Universe To Use", GUILayout.Width(126f));
                    _universeToUse = Mathf.Clamp(EditorGUILayout.IntField(_universeToUse, GUILayout.Width(72f)), 0, 64);
                    GUILayout.FlexibleSpace();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (!_isListening)
                    {
                        if (GUILayout.Button("Start Receiver", GUILayout.Width(120f)))
                            StartStandaloneMonitor();
                    }
                    else
                    {
                        if (GUILayout.Button("Stop Receiver", GUILayout.Width(120f)))
                            StopStandaloneMonitor();
                    }

                    if (GUILayout.Button("Clear", GUILayout.Width(72f)))
                        ClearStandaloneMonitor();

                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField(_isListening ? "Listening" : "Stopped", EditorStyles.miniLabel, GUILayout.Width(72f));
                }

                if (!string.IsNullOrEmpty(_error))
                    EditorGUILayout.HelpBox(_error, MessageType.Error);

                var snapshots = GetStandaloneSnapshots();
                if (snapshots.Length == 0)
                {
                    EditorGUILayout.HelpBox(_isListening ? "Waiting for Art-Net DMX packets." : "Start Receiver to check Art-Net receive data.", MessageType.None);
                    return;
                }

                var selectedSnapshot = FindSnapshot(snapshots, _universeToUse);
                EditorGUILayout.LabelField("Selected Universe", EditorStyles.boldLabel);
                if (selectedSnapshot.HasValue)
                    DrawUniverse(selectedSnapshot.Value);
                else
                    EditorGUILayout.HelpBox($"No packet received for Universe {_universeToUse}.", MessageType.None);

                EditorGUILayout.LabelField("Received Universes", EditorStyles.boldLabel);
                foreach (var snapshot in snapshots)
                {
                    if (snapshot.Universe == _universeToUse) continue;
                    DrawUniverse(snapshot);
                }
            }
        }

        void StartStandaloneMonitor()
        {
            StopStandaloneMonitor();
            _error = null;

            ArtNetServer server = null;
            try
            {
                ClearStandaloneMonitor();
                server = new ArtNetServer(_host, _port);
                var sessionId = ++_sessionId;
                _standaloneCallback = data => OnStandaloneDataReceive(sessionId, data);
                server.MessageDispatcher.AddCallback(_standaloneCallback);
                _server = server;
                server = null;
                _isListening = true;
            }
            catch (Exception e)
            {
                _error = e.Message;
                _server = null;
                _isListening = false;
                server?.Dispose();
            }
        }

        void StopStandaloneMonitor()
        {
            var server = _server;
            var callback = _standaloneCallback;
            _server = null;
            _standaloneCallback = null;
            _isListening = false;

            if (server == null)
                return;

            try
            {
                _sessionId++;
                if (callback != null)
                    server.MessageDispatcher?.RemoveCallback(callback);
            }
            finally
            {
                server.Dispose();
            }
        }

        void ClearStandaloneMonitor()
        {
            lock (_syncRoot)
            {
                _standaloneStates.Clear();
            }
        }

        ArtNetUniverseMonitorSnapshot[] GetStandaloneSnapshots()
        {
            lock (_syncRoot)
            {
                var snapshots = new ArtNetUniverseMonitorSnapshot[_standaloneStates.Count];
                var index = 0;
                foreach (var pair in _standaloneStates)
                    snapshots[index++] = pair.Value.CreateSnapshot(pair.Key);

                Array.Sort(snapshots, (a, b) => a.Universe.CompareTo(b.Universe));
                return snapshots;
            }
        }

        void OnStandaloneDataReceive(int sessionId, ArtNetDataHandle data)
        {
            if (sessionId != _sessionId) return;

            lock (_syncRoot)
            {
                var universe = data.GetUniverse();
                if (!_standaloneStates.TryGetValue(universe, out var state))
                {
                    state = new MonitorState();
                    _standaloneStates[universe] = state;
                }

                state.Update(data);
            }
        }

        static ArtNetUniverseMonitorSnapshot? FindSnapshot(ArtNetUniverseMonitorSnapshot[] snapshots, int universe)
        {
            for (var i = 0; i < snapshots.Length; i++)
            {
                if (snapshots[i].Universe == universe)
                    return snapshots[i];
            }

            return null;
        }

        void DrawReceiver(VLiveArtNetReceiver receiver)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.ObjectField("Receiver", receiver, typeof(VLiveArtNetReceiver), true);
                EditorGUILayout.LabelField("Endpoint", $"{receiver.CurrentHost}:{receiver.CurrentPort}");
                EditorGUILayout.LabelField("Selected Universe", receiver.UniverseToUse.ToString());

                var snapshots = receiver.GetMonitorSnapshots();
                if (snapshots.Length == 0)
                {
                    EditorGUILayout.HelpBox("Waiting for Art-Net DMX packets.", MessageType.None);
                    return;
                }

                foreach (var snapshot in snapshots)
                {
                    DrawUniverse(snapshot);
                }
            }
        }

        void DrawUniverse(ArtNetUniverseMonitorSnapshot snapshot)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var age = snapshot.SecondsSinceLastReceived;
                var ageText = double.IsInfinity(age) ? "-" : $"{age:0.00}s ago";
                var nonZeroCount = CountNonZero(snapshot.Channels, snapshot.Length);

                EditorGUILayout.LabelField($"Universe {snapshot.Universe}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Packets", snapshot.PacketCount.ToString());
                EditorGUILayout.LabelField("Last Received", ageText);
                EditorGUILayout.LabelField("Length / Non Zero", $"{snapshot.Length} / {nonZeroCount}");
                EditorGUILayout.LabelField("Sequence / Physical", $"{snapshot.Sequence} / {snapshot.Physical}");

                EditorGUILayout.SelectableLabel(BuildChannelPreview(snapshot.Channels, snapshot.Length), EditorStyles.textField, GUILayout.Height(34f));
            }
        }

        static int CountNonZero(byte[] channels, int length)
        {
            var count = 0;
            var max = Mathf.Min(length, channels.Length);
            for (var i = 0; i < max; i++)
            {
                if (channels[i] != 0) count++;
            }
            return count;
        }

        static string BuildChannelPreview(byte[] channels, int length)
        {
            var max = Mathf.Min(PreviewChannelCount, length, channels.Length);
            var builder = new StringBuilder(max * 5);
            for (var i = 0; i < max; i++)
            {
                if (i > 0) builder.Append(' ');
                builder.Append(i + 1);
                builder.Append(':');
                builder.Append(channels[i]);
            }
            return builder.ToString();
        }

        sealed class MonitorState
        {
            public long PacketCount { get; private set; }
            public DateTime LastReceivedUtc { get; private set; }
            public int Sequence { get; private set; }
            public int Physical { get; private set; }
            public int Length { get; private set; }
            readonly byte[] _channels = new byte[512];

            public void Update(ArtNetDataHandle data)
            {
                PacketCount++;
                LastReceivedUtc = DateTime.UtcNow;
                Sequence = data.GetSequence();
                Physical = data.GetPhysical();
                Length = (data.GetLengthHi() << 8) | data.GetLengthLo();

                var channels = data.GetArtNetChannels();
                var length = Math.Min(_channels.Length, channels.Length);
                Array.Clear(_channels, 0, _channels.Length);
                Array.Copy(channels, _channels, length);
            }

            public ArtNetUniverseMonitorSnapshot CreateSnapshot(int universe)
            {
                var channels = new byte[_channels.Length];
                Array.Copy(_channels, channels, _channels.Length);
                return new ArtNetUniverseMonitorSnapshot(universe, PacketCount, LastReceivedUtc, Sequence, Physical, Length, channels);
            }
        }
    }
}
