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
        string _error;
        bool _isListening;
        ArtNetServer _server;
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

            var receivers = VLiveArtNetReceiver.ActiveReceivers;
            if (receivers.Count == 0)
            {
                EditorGUILayout.HelpBox("No active VLiveArtNetReceiver found in the scene.", MessageType.None);
                return;
            }

            EditorGUILayout.LabelField("Scene Receivers", EditorStyles.boldLabel);
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
                EditorGUILayout.LabelField("Standalone Monitor", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(_isListening))
                    {
                        _host = EditorGUILayout.TextField("Host", _host);
                        _port = EditorGUILayout.IntField("Port", _port, GUILayout.Width(180f));
                    }

                    if (!_isListening)
                    {
                        if (GUILayout.Button("Start", GUILayout.Width(72f)))
                            StartStandaloneMonitor();
                    }
                    else
                    {
                        if (GUILayout.Button("Stop", GUILayout.Width(72f)))
                            StopStandaloneMonitor();
                    }
                }

                if (!string.IsNullOrEmpty(_error))
                    EditorGUILayout.HelpBox(_error, MessageType.Error);

                var snapshots = GetStandaloneSnapshots();
                if (snapshots.Length == 0)
                {
                    EditorGUILayout.HelpBox(_isListening ? "Waiting for Art-Net DMX packets." : "Start listening to check Art-Net receive data.", MessageType.None);
                    return;
                }

                foreach (var snapshot in snapshots)
                    DrawUniverse(snapshot);
            }
        }

        void StartStandaloneMonitor()
        {
            StopStandaloneMonitor();
            _error = null;

            try
            {
                _server = ArtNetMaster.GetSharedServer(_host, _port);
                _server.MessageDispatcher.AddCallback(OnStandaloneDataReceive);
                _isListening = true;
            }
            catch (Exception e)
            {
                _error = e.Message;
                _server = null;
                _isListening = false;
            }
        }

        void StopStandaloneMonitor()
        {
            if (_server != null)
                _server.MessageDispatcher.RemoveCallback(OnStandaloneDataReceive);

            _server = null;
            _isListening = false;
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

        void OnStandaloneDataReceive(ArtNetDataHandle data)
        {
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
