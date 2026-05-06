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
        const double LiveThresholdSeconds = 0.75d;
        const double StaleThresholdSeconds = 3d;
        Vector2 _mainScroll;
        Vector2 _sceneScroll;
        double _nextRepaintTime;
        string _host = "127.0.0.1";
        int _port = 6454;
        int _universeToUse = 1;
        string _error;
        bool _isListening;
        ArtNetServer _server;
        ArtNetMessageDispatcher.MessageCallback _standaloneCallback;
        int _sessionId;
        DateTime _standaloneStoppedUtc;
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

            _mainScroll = EditorGUILayout.BeginScrollView(_mainScroll);
            DrawStandaloneMonitor();

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Scene Receivers (Optional)", EditorStyles.boldLabel);
            var receivers = VLiveArtNetReceiver.ActiveReceivers;
            if (receivers.Count == 0)
            {
                EditorGUILayout.HelpBox("No scene VLiveArtNetReceiver is active. Standalone Monitor above can still receive Art-Net without any scene object.", MessageType.None);
                EditorGUILayout.EndScrollView();
                return;
            }

            _sceneScroll = EditorGUILayout.BeginScrollView(_sceneScroll, GUILayout.MinHeight(120f));
            foreach (var receiver in receivers)
            {
                if (receiver == null) continue;
                DrawReceiver(receiver);
                EditorGUILayout.Space(8f);
            }
            EditorGUILayout.EndScrollView();
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
                    DrawTrafficBanner(null, _isListening, _standaloneStoppedUtc);
                    EditorGUILayout.HelpBox(_isListening ? "Waiting for Art-Net DMX packets." : "Start Receiver to check Art-Net receive data.", MessageType.None);
                    return;
                }

                DrawTrafficBanner(FindFreshestSnapshot(snapshots), _isListening, _standaloneStoppedUtc);
                DrawUniverseTable(snapshots, _isListening, _standaloneStoppedUtc, true);

                var selectedSnapshot = FindSnapshot(snapshots, _universeToUse);
                EditorGUILayout.LabelField("Selected Universe", EditorStyles.boldLabel);
                if (selectedSnapshot.HasValue)
                    DrawUniverseDetail(selectedSnapshot.Value, _isListening, _standaloneStoppedUtc);
                else
                    EditorGUILayout.HelpBox($"No packet received for Universe {_universeToUse}.", MessageType.None);
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
                _standaloneStoppedUtc = default;
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
            _standaloneStoppedUtc = DateTime.UtcNow;

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
                    DrawTrafficBanner(null, true, default);
                    EditorGUILayout.HelpBox("Waiting for Art-Net DMX packets.", MessageType.None);
                    return;
                }

                DrawTrafficBanner(FindFreshestSnapshot(snapshots), true, default);
                DrawUniverseTable(snapshots, true, default, false);
            }
        }

        void DrawTrafficBanner(ArtNetUniverseMonitorSnapshot? snapshot, bool isLiveClock, DateTime stoppedUtc)
        {
            var age = snapshot.HasValue ? GetAge(snapshot.Value, isLiveClock, stoppedUtc) : double.PositiveInfinity;
            var status = GetStatusLabel(snapshot, isLiveClock, age);
            var color = GetStatusColor(snapshot, isLiveClock, age);
            var rect = GUILayoutUtility.GetRect(1f, 30f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, color);

            var labelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            var ageText = snapshot.HasValue ? FormatAge(age, isLiveClock) : "no packets";
            GUI.Label(rect, $"{status}  {ageText}", labelStyle);
        }

        void DrawUniverseTable(ArtNetUniverseMonitorSnapshot[] snapshots, bool isLiveClock, DateTime stoppedUtc, bool allowSelect)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Universe Packet State", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Uni", GUILayout.Width(34f));
                GUILayout.Label("Signal", GUILayout.Width(62f));
                GUILayout.Label("Packets", GUILayout.Width(62f));
                GUILayout.Label("Last", GUILayout.Width(72f));
                GUILayout.Label("NonZero", GUILayout.Width(62f));
                GUILayout.Label("Seq", GUILayout.Width(44f));
                GUILayout.Label("Ch 1-8", GUILayout.MinWidth(100f));
            }

            for (var i = 0; i < snapshots.Length; i++)
            {
                var snapshot = snapshots[i];
                var age = GetAge(snapshot, isLiveClock, stoppedUtc);
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    if (allowSelect)
                    {
                        if (GUILayout.Button(snapshot.Universe.ToString(), GUILayout.Width(34f)))
                            _universeToUse = snapshot.Universe;
                    }
                    else
                    {
                        GUILayout.Label(snapshot.Universe.ToString(), GUILayout.Width(34f));
                    }

                    DrawSignalPill(snapshot, isLiveClock, age, GUILayout.Width(62f), GUILayout.Height(18f));
                    GUILayout.Label(snapshot.PacketCount.ToString(), GUILayout.Width(62f));
                    GUILayout.Label(FormatAge(age, isLiveClock), GUILayout.Width(72f));
                    GUILayout.Label(CountNonZero(snapshot.Channels, snapshot.Length).ToString(), GUILayout.Width(62f));
                    GUILayout.Label(snapshot.Sequence.ToString(), GUILayout.Width(44f));
                    GUILayout.Label(BuildShortChannelPreview(snapshot.Channels, snapshot.Length, 8), GUILayout.MinWidth(100f));
                }
            }
        }

        void DrawUniverseDetail(ArtNetUniverseMonitorSnapshot snapshot, bool isLiveClock, DateTime stoppedUtc)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var age = GetAge(snapshot, isLiveClock, stoppedUtc);
                var ageText = FormatAge(age, isLiveClock);
                var nonZeroCount = CountNonZero(snapshot.Channels, snapshot.Length);

                EditorGUILayout.LabelField($"Universe {snapshot.Universe}", EditorStyles.boldLabel);
                DrawSignalPill(snapshot, isLiveClock, age, GUILayout.Height(20f));
                EditorGUILayout.LabelField("Packets", snapshot.PacketCount.ToString());
                EditorGUILayout.LabelField("Last Received", ageText);
                EditorGUILayout.LabelField("Length / Non Zero", $"{snapshot.Length} / {nonZeroCount}");
                EditorGUILayout.LabelField("Sequence / Physical", $"{snapshot.Sequence} / {snapshot.Physical}");
                DrawChannelMeters(snapshot.Channels, snapshot.Length);
            }
        }

        void DrawSignalPill(ArtNetUniverseMonitorSnapshot snapshot, bool isLiveClock, double age, params GUILayoutOption[] options)
        {
            var rect = GUILayoutUtility.GetRect(1f, 18f, options);
            EditorGUI.DrawRect(rect, GetStatusColor(snapshot, isLiveClock, age));

            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                fontSize = 10
            };
            GUI.Label(rect, GetStatusLabel(snapshot, isLiveClock, age), style);
        }

        void DrawChannelMeters(byte[] channels, int length)
        {
            EditorGUILayout.LabelField("Channel Preview 1-32", EditorStyles.boldLabel);
            const int columns = 16;
            const float rowHeight = 36f;
            var rows = Mathf.CeilToInt(PreviewChannelCount / (float)columns);
            var rect = GUILayoutUtility.GetRect(1f, rows * rowHeight, GUILayout.ExpandWidth(true));
            var cellWidth = rect.width / columns;
            var max = Mathf.Min(PreviewChannelCount, length, channels.Length);

            for (var i = 0; i < PreviewChannelCount; i++)
            {
                var row = i / columns;
                var column = i % columns;
                var cell = new Rect(rect.x + column * cellWidth + 1f, rect.y + row * rowHeight + 1f, cellWidth - 2f, rowHeight - 2f);
                var value = i < max ? channels[i] : 0;
                EditorGUI.DrawRect(cell, new Color(0.16f, 0.16f, 0.16f));

                var fillHeight = Mathf.Lerp(0f, cell.height - 14f, value / 255f);
                var fill = new Rect(cell.x + 2f, cell.yMax - 12f - fillHeight, cell.width - 4f, fillHeight);
                EditorGUI.DrawRect(fill, new Color(0.1f, 0.72f, 0.34f));

                var label = new Rect(cell.x, cell.y + 1f, cell.width, 12f);
                var valueRect = new Rect(cell.x, cell.yMax - 13f, cell.width, 12f);
                var style = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
                GUI.Label(label, (i + 1).ToString(), style);
                GUI.Label(valueRect, value.ToString(), style);
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

        static ArtNetUniverseMonitorSnapshot? FindFreshestSnapshot(ArtNetUniverseMonitorSnapshot[] snapshots)
        {
            if (snapshots.Length == 0) return null;

            var freshest = snapshots[0];
            for (var i = 1; i < snapshots.Length; i++)
            {
                if (snapshots[i].LastReceivedUtc > freshest.LastReceivedUtc)
                    freshest = snapshots[i];
            }

            return freshest;
        }

        static double GetAge(ArtNetUniverseMonitorSnapshot snapshot, bool isLiveClock, DateTime stoppedUtc)
        {
            if (snapshot.LastReceivedUtc == default) return double.PositiveInfinity;
            var now = isLiveClock || stoppedUtc == default ? DateTime.UtcNow : stoppedUtc;
            return Math.Max(0d, (now - snapshot.LastReceivedUtc).TotalSeconds);
        }

        static string FormatAge(double age, bool isLiveClock)
        {
            if (double.IsInfinity(age)) return "-";
            var suffix = isLiveClock ? " ago" : " at stop";
            return $"{age:0.00}s{suffix}";
        }

        static string GetStatusLabel(ArtNetUniverseMonitorSnapshot? snapshot, bool isLiveClock, double age)
        {
            if (!snapshot.HasValue) return isLiveClock ? "WAITING" : "STOPPED";
            if (!isLiveClock) return "STOPPED";
            if (age <= LiveThresholdSeconds) return "LIVE";
            if (age <= StaleThresholdSeconds) return "STALE";
            return "LOST";
        }

        static Color GetStatusColor(ArtNetUniverseMonitorSnapshot? snapshot, bool isLiveClock, double age)
        {
            if (!snapshot.HasValue) return new Color(0.25f, 0.25f, 0.25f);
            if (!isLiveClock) return new Color(0.34f, 0.34f, 0.34f);
            if (age <= LiveThresholdSeconds) return new Color(0.08f, 0.58f, 0.22f);
            if (age <= StaleThresholdSeconds) return new Color(0.8f, 0.48f, 0.08f);
            return new Color(0.55f, 0.12f, 0.12f);
        }

        static string BuildShortChannelPreview(byte[] channels, int length, int count)
        {
            var max = Mathf.Min(count, length, channels.Length);
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
