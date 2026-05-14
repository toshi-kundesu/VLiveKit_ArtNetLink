// VLiveKit is all Unlicense.
// unlicense: https://unlicense.org/
// this comment & namespace can be removed.
// last update: 2026/05/08

using System;
using toshi.VLiveKit.Lighting;
using UnityEditor;
using UnityEngine;

namespace toshi.VLiveKit.ArtNetLink.Editor
{
    enum ArtNetSendTestOutputMode
    {
        LiveDesk,
        Once
    }

    enum FixturePanTiltTestMode
    {
        Center,
        Circle
    }

    enum FixtureDimmerTestMode
    {
        Full,
        Sine,
        FlashSmoothFade
    }

    enum FixtureColorTestMode
    {
        White,
        CmyLoop
    }

    public sealed class ArtNetSendTestWindow : EditorWindow
    {
        const int UniverseCount = 10;
        const int FaderCount = ArtNetDmxSender.ChannelCount;
        const int ButtonWidth = 64;
        const int TestFixtureFootprint = 6;
        const float TransportButtonHeight = 28f;
        static readonly Color SendingBlue = new Color(0.02f, 0.42f, 0.82f);
        static readonly Color TestSignalBlue = new Color(0.16f, 0.34f, 0.82f);
        static readonly Color StoppedGray = new Color(0.28f, 0.28f, 0.28f);
        static readonly Color OnceGray = new Color(0.34f, 0.34f, 0.34f);

        [SerializeField] ArtNetSendTargetMode _targetMode = ArtNetSendTargetMode.Broadcast;
        [SerializeField] ArtNetSendTestOutputMode _outputMode = ArtNetSendTestOutputMode.LiveDesk;
        [SerializeField] string _unicastAddress = "127.0.0.1";
        [SerializeField] string _broadcastAddress = "255.255.255.255";
        [SerializeField] int _port = ArtNetDmxSender.DefaultPort;
        [SerializeField] bool _isLiveSending;
        [SerializeField] bool _isTestSignalSending;
        [SerializeField] float _sendRate = 30f;
        [SerializeField] int _visibleChannelCount = 16;
        [SerializeField] int[] _values;
        [SerializeField] bool[] _universeEnabled;
        [SerializeField] bool[] _universeFoldouts;
        [SerializeField] bool _showDestination = true;
        [SerializeField] bool _showTransport = true;
        [SerializeField] bool _showTestSignalControls = true;
        [SerializeField] int _testUniverse = 0;
        [SerializeField] int _testStartAddress = 1;
        [SerializeField] int _testFixtureCount = 1;
        [SerializeField] float _testSpeed = 0.1f;
        [SerializeField] FixturePanTiltTestMode _panTiltMode = FixturePanTiltTestMode.Circle;
        [SerializeField] FixtureDimmerTestMode _dimmerMode = FixtureDimmerTestMode.Sine;
        [SerializeField] FixtureColorTestMode _colorMode = FixtureColorTestMode.CmyLoop;

        ArtNetDmxSender _sender;
        Vector2 _scrollPosition;
        double _nextSendTime;
        string _status = "Idle";

        [MenuItem("toshi/VLiveKit/Lighting/SimpleLightConsole")]
        public static void Open()
        {
            GetWindow<ArtNetSendTestWindow>("SimpleLightConsole");
        }

        public static void OpenSimpleLightConsole()
        {
            Open();
        }

        void OnEnable()
        {
            EnsureState();
            _isLiveSending = false;
            _sender = new ArtNetDmxSender();
            EditorApplication.update += OnEditorUpdate;
        }

        void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            StopLiveSending("Stopped");

            if (_sender != null)
            {
                _sender.Dispose();
                _sender = null;
            }
        }

        void OnGUI()
        {
            EnsureState();

            DrawOutputBanner();
            EditorGUILayout.Space(4f);
            DrawDestination();
            EditorGUILayout.Space(6f);
            DrawTransport();
            EditorGUILayout.Space(6f);
            DrawTestSignal();
            EditorGUILayout.Space(6f);
            DrawFaders();
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(_status, EditorStyles.miniLabel);
        }

        void DrawOutputBanner()
        {
            var rect = GUILayoutUtility.GetRect(1f, 42f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, GetPrimaryStateColor());

            var titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                normal = { textColor = Color.white }
            };

            var detailStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 1f, 1f, 0.82f) }
            };

            GUI.Label(new Rect(rect.x + 10f, rect.y + 5f, rect.width - 20f, 18f), GetPrimaryStateLabel(), titleStyle);
            GUI.Label(new Rect(rect.x + 10f, rect.y + 23f, rect.width - 20f, 15f), GetPrimaryStateDetail(), detailStyle);
        }

        void DrawDestination()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _showDestination = EditorGUILayout.Foldout(_showDestination, "Destination", true, EditorStyles.foldoutHeader);
                if (!_showDestination)
                {
                    EditorGUILayout.LabelField(CurrentAddress + ":" + _port, EditorStyles.miniLabel);
                    return;
                }

                _targetMode = (ArtNetSendTargetMode)EditorGUILayout.EnumPopup("Target Mode", _targetMode);

                if (_targetMode == ArtNetSendTargetMode.Unicast)
                    _unicastAddress = EditorGUILayout.TextField("Unicast Address", _unicastAddress);
                else
                    _broadcastAddress = EditorGUILayout.TextField("Broadcast Address", _broadcastAddress);

                _port = EditorGUILayout.IntField("Port", _port);
                _port = Mathf.Clamp(_port, 1, 65535);
            }
        }

        void DrawTransport()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _showTransport = EditorGUILayout.Foldout(_showTransport, "Transport", true, EditorStyles.foldoutHeader);
                if (!_showTransport)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("State", GetOutputStateLabel(), EditorStyles.miniLabel);
                        GUILayout.FlexibleSpace();
                        DrawStatePill(GetCompactStateLabel(), GetPrimaryStateColor(), GUILayout.Width(108f), GUILayout.Height(18f));
                    }
                    return;
                }

                EditorGUI.BeginChangeCheck();
                var outputMode = (ArtNetSendTestOutputMode)EditorGUILayout.EnumPopup("Output Mode", _outputMode);
                if (EditorGUI.EndChangeCheck())
                {
                    StopLiveSending("Stopped");
                    StopTestSignal(null);
                    _outputMode = outputMode;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (_outputMode == ArtNetSendTestOutputMode.LiveDesk)
                    {
                        if (!_isLiveSending && !_isTestSignalSending)
                        {
                            if (GUILayout.Button("Start Sending", EditorStyles.miniButtonLeft, GUILayout.Width(132f), GUILayout.Height(TransportButtonHeight)))
                                StartLiveSending();
                        }
                        else
                        {
                            if (GUILayout.Button("Stop Sending", EditorStyles.miniButtonLeft, GUILayout.Width(132f), GUILayout.Height(TransportButtonHeight)))
                            {
                                StopLiveSending("Stopped");
                                StopTestSignal("Stopped");
                            }
                        }
                    }
                    else
                    {
                        if (GUILayout.Button("Send Once", EditorStyles.miniButtonLeft, GUILayout.Width(132f), GUILayout.Height(TransportButtonHeight)))
                            SendAllEnabledUniverses();
                    }

                    if (GUILayout.Button("Blackout", EditorStyles.miniButtonMid, GUILayout.Width(82f), GUILayout.Height(TransportButtonHeight)))
                    {
                        StopTestSignal("Test signal stopped");
                        SetAllFaders(0, true);
                    }

                    if (GUILayout.Button("Full", EditorStyles.miniButtonRight, GUILayout.Width(70f), GUILayout.Height(TransportButtonHeight)))
                    {
                        StopTestSignal("Test signal stopped");
                        SetAllFaders(255, true);
                    }

                    GUILayout.FlexibleSpace();
                    DrawStatePill(GetCompactStateLabel(), GetPrimaryStateColor(), GUILayout.Width(118f), GUILayout.Height(TransportButtonHeight));
                }

                using (new EditorGUI.DisabledScope(_outputMode != ArtNetSendTestOutputMode.LiveDesk))
                {
                    _sendRate = EditorGUILayout.Slider("Rate (fps)", _sendRate, 1f, 60f);
                }

                EditorGUILayout.LabelField("State", GetOutputStateLabel(), EditorStyles.miniLabel);
            }
        }

        void DrawTestSignal()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _showTestSignalControls = EditorGUILayout.Foldout(_showTestSignalControls, "Test Signal", true, EditorStyles.foldoutHeader);
                    GUILayout.FlexibleSpace();
                    DrawStatePill(_isTestSignalSending ? "SENDING" : "STOPPED", _isTestSignalSending ? TestSignalBlue : StoppedGray, GUILayout.Width(86f), GUILayout.Height(18f));
                }

                if (!_showTestSignalControls)
                {
                    EditorGUILayout.LabelField("Automatic pan, tilt, dimmer, and color pattern for fixture checks.", EditorStyles.miniLabel);
                    return;
                }

                EditorGUILayout.LabelField("SimpleLightConsole can drive the faders below with an automatic test pattern.", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("Channel Map", "1: Pan, 2: Tilt, 3: Dimmer, 4: R, 5: G, 6: B", EditorStyles.miniLabel);

                _testUniverse = Mathf.Clamp(EditorGUILayout.IntField("Universe (0-based)", _testUniverse), 0, UniverseCount - 1);
                _testStartAddress = Mathf.Clamp(EditorGUILayout.IntField("Start Address", _testStartAddress), 1, ArtNetDmxSender.ChannelCount);
                var maxCount = Mathf.Max(1, (ArtNetDmxSender.ChannelCount - _testStartAddress + 1) / TestFixtureFootprint);
                _testFixtureCount = Mathf.Clamp(EditorGUILayout.IntField("Fixture Count", _testFixtureCount), 1, maxCount);
                _testSpeed = EditorGUILayout.Slider("Speed", _testSpeed, 0.1f, 4f);

                EditorGUILayout.Space(2f);
                _panTiltMode = (FixturePanTiltTestMode)EditorGUILayout.EnumPopup("Pan / Tilt", _panTiltMode);
                _dimmerMode = (FixtureDimmerTestMode)EditorGUILayout.EnumPopup("Dimmer", _dimmerMode);
                _colorMode = (FixtureColorTestMode)EditorGUILayout.EnumPopup("Color", _colorMode);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (!_isTestSignalSending)
                    {
                        if (GUILayout.Button("Start Test Signal", EditorStyles.miniButtonLeft, GUILayout.Width(150f), GUILayout.Height(TransportButtonHeight)))
                            StartTestSignal();
                    }
                    else
                    {
                        if (GUILayout.Button("Stop Test Signal", EditorStyles.miniButtonLeft, GUILayout.Width(150f), GUILayout.Height(TransportButtonHeight)))
                            StopTestSignal("Test signal stopped");
                    }

                    if (GUILayout.Button("Blackout", EditorStyles.miniButtonRight, GUILayout.Width(86f), GUILayout.Height(TransportButtonHeight)))
                        SendBlackoutTestUniverse();

                    GUILayout.FlexibleSpace();
                    DrawStatePill(_isTestSignalSending ? "SENDING" : "STOPPED", _isTestSignalSending ? TestSignalBlue : StoppedGray, GUILayout.Width(92f), GUILayout.Height(TransportButtonHeight));
                }
            }
        }

        void DrawFaders()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Faders", EditorStyles.boldLabel);
                _visibleChannelCount = Mathf.Clamp(EditorGUILayout.IntSlider("Visible Faders", _visibleChannelCount, 6, 128), 6, FaderCount);
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            for (var universe = 0; universe < UniverseCount; universe++)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _universeFoldouts[universe] = EditorGUILayout.Foldout(_universeFoldouts[universe], "Universe " + universe, true);
                        _universeEnabled[universe] = EditorGUILayout.ToggleLeft("Enabled", _universeEnabled[universe], GUILayout.Width(82f));

                        if (GUILayout.Button("0", EditorStyles.miniButtonLeft, GUILayout.Width(ButtonWidth)))
                            SetUniverseFaders(universe, 0, true);

                        if (GUILayout.Button("255", EditorStyles.miniButtonMid, GUILayout.Width(ButtonWidth)))
                            SetUniverseFaders(universe, 255, true);

                        if (GUILayout.Button("Send", EditorStyles.miniButtonRight, GUILayout.Width(ButtonWidth)))
                            SendUniverse(universe);
                    }

                    if (!_universeFoldouts[universe])
                    {
                        EditorGUILayout.LabelField(BuildUniverseSummary(universe), EditorStyles.miniLabel);
                        continue;
                    }

                    using (new EditorGUI.DisabledScope(!_universeEnabled[universe]))
                    {
                        for (var fader = 0; fader < _visibleChannelCount; fader++)
                        {
                            var valueIndex = GetValueIndex(universe, fader);
                            EditorGUI.BeginChangeCheck();
                            var value = EditorGUILayout.IntSlider("Ch " + (fader + 1), _values[valueIndex], 0, 255);
                            if (EditorGUI.EndChangeCheck())
                            {
                                _values[valueIndex] = value;
                                if (_isLiveSending) SendUniverse(universe);
                            }
                        }
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        void OnEditorUpdate()
        {
            if (!_isLiveSending && !_isTestSignalSending) return;

            var now = EditorApplication.timeSinceStartup;
            if (now < _nextSendTime) return;

            if (_isTestSignalSending)
            {
                ApplyTestSignalToFaders();
                SendUniverse(_testUniverse);
            }
            else if (_outputMode == ArtNetSendTestOutputMode.LiveDesk)
            {
                SendAllEnabledUniverses();
            }

            _nextSendTime = now + 1.0 / Mathf.Clamp(_sendRate, 1f, 60f);
        }

        void StartLiveSending()
        {
            _outputMode = ArtNetSendTestOutputMode.LiveDesk;
            _isLiveSending = true;
            _nextSendTime = 0;
            SendAllEnabledUniverses();
            _status = "Live Desk started: " + CurrentAddress + ":" + _port;
        }

        void StopLiveSending(string status)
        {
            _isLiveSending = false;
            _nextSendTime = 0;
            if (!string.IsNullOrEmpty(status))
                _status = status;
        }

        void SendAllEnabledUniverses()
        {
            var sentCount = 0;
            for (var universe = 0; universe < UniverseCount; universe++)
            {
                if (!_universeEnabled[universe]) continue;
                if (TrySendUniverse(universe)) sentCount++;
            }

            if (sentCount > 0)
                _status = "Sent " + sentCount + " universe(s) to " + CurrentAddress + ":" + _port;
        }

        void SendUniverse(int universe)
        {
            TrySendUniverse(universe);
        }

        bool TrySendUniverse(int universe)
        {
            if (_sender == null)
                _sender = new ArtNetDmxSender();

            try
            {
                _sender.SendDmx(CurrentAddress, _port, universe, BuildDmxData(universe), _targetMode);
                _status = "Sent universe " + universe + " to " + CurrentAddress + ":" + _port;
                return true;
            }
            catch (Exception exception)
            {
                _status = "Send failed: " + exception.Message;
                return false;
            }
        }

        byte[] BuildDmxData(int universe)
        {
            var channels = new byte[ArtNetDmxSender.ChannelCount];
            for (var fader = 0; fader < FaderCount; fader++)
                channels[fader] = (byte)Mathf.Clamp(_values[GetValueIndex(universe, fader)], 0, 255);

            return channels;
        }

        void StartTestSignal()
        {
            _isTestSignalSending = true;
            _outputMode = ArtNetSendTestOutputMode.LiveDesk;
            _universeEnabled[_testUniverse] = true;
            _nextSendTime = 0;
            ApplyTestSignalToFaders();
            SendUniverse(_testUniverse);
            _status = "Test signal started: universe " + _testUniverse + " to " + CurrentAddress + ":" + _port;
        }

        void StopTestSignal(string status)
        {
            if (!_isTestSignalSending) return;

            _isTestSignalSending = false;
            _nextSendTime = 0;
            if (!string.IsNullOrEmpty(status))
                _status = status;
        }

        void ApplyTestSignalToFaders()
        {
            SetUniverseFaders(_testUniverse, 0, false);
            var time = (float)EditorApplication.timeSinceStartup * _testSpeed;
            for (var fixture = 0; fixture < _testFixtureCount; fixture++)
            {
                var baseIndex = _testStartAddress - 1 + fixture * TestFixtureFootprint;
                if (baseIndex < 0 || baseIndex + TestFixtureFootprint > FaderCount) break;

                var phase = time + fixture * 0.18f;
                GetFixtureTestValues(phase, out var pan, out var tilt, out var dimmer, out var color);
                _values[GetValueIndex(_testUniverse, baseIndex)] = ToByte(pan);
                _values[GetValueIndex(_testUniverse, baseIndex + 1)] = ToByte(tilt);
                _values[GetValueIndex(_testUniverse, baseIndex + 2)] = ToByte(dimmer);
                _values[GetValueIndex(_testUniverse, baseIndex + 3)] = ToByte(color.r);
                _values[GetValueIndex(_testUniverse, baseIndex + 4)] = ToByte(color.g);
                _values[GetValueIndex(_testUniverse, baseIndex + 5)] = ToByte(color.b);
            }
        }

        void GetFixtureTestValues(float phase, out float pan, out float tilt, out float dimmer, out Color color)
        {
            if (_panTiltMode == FixturePanTiltTestMode.Circle)
            {
                pan = 0.5f + Mathf.Sin(phase * Mathf.PI * 2f) * 0.45f;
                tilt = 0.5f + Mathf.Cos(phase * Mathf.PI * 2f) * 0.45f;
            }
            else
            {
                pan = 0.5f;
                tilt = 0.5f;
            }

            switch (_dimmerMode)
            {
                case FixtureDimmerTestMode.Sine:
                    dimmer = 0.5f + Mathf.Sin(phase * Mathf.PI * 2f) * 0.5f;
                    break;
                case FixtureDimmerTestMode.FlashSmoothFade:
                    dimmer = Mathf.Exp(-Mathf.Repeat(phase, 1f) * 5f);
                    break;
                default:
                    dimmer = 1f;
                    break;
            }

            color = _colorMode == FixtureColorTestMode.CmyLoop
                ? EvaluateCmyLoop(Mathf.Repeat(phase * 0.2f, 1f))
                : Color.white;
        }

        void SendBlackoutTestUniverse()
        {
            if (_sender == null)
                _sender = new ArtNetDmxSender();

            try
            {
                _sender.SendDmx(CurrentAddress, _port, _testUniverse, new byte[ArtNetDmxSender.ChannelCount], _targetMode);
                SetUniverseFaders(_testUniverse, 0, false);
                StopTestSignal(null);
                _status = "Sent fixture test blackout universe " + _testUniverse + " to " + CurrentAddress + ":" + _port;
            }
            catch (Exception exception)
            {
                _status = "Send failed: " + exception.Message;
            }
        }

        void SetAllFaders(int value, bool send)
        {
            for (var i = 0; i < _values.Length; i++)
                _values[i] = value;

            if (send) SendAllEnabledUniverses();
        }

        void SetUniverseFaders(int universe, int value, bool send)
        {
            for (var fader = 0; fader < FaderCount; fader++)
                _values[GetValueIndex(universe, fader)] = value;

            if (send) SendUniverse(universe);
        }

        void EnsureState()
        {
            if (_values == null || _values.Length != UniverseCount * FaderCount)
                _values = new int[UniverseCount * FaderCount];

            if (_universeEnabled == null || _universeEnabled.Length != UniverseCount)
            {
                _universeEnabled = new bool[UniverseCount];
                for (var i = 0; i < _universeEnabled.Length; i++)
                    _universeEnabled[i] = true;
            }

            if (_universeFoldouts == null || _universeFoldouts.Length != UniverseCount)
            {
                _universeFoldouts = new bool[UniverseCount];
                if (_universeFoldouts.Length > 0)
                    _universeFoldouts[0] = true;
            }
        }

        int GetValueIndex(int universe, int fader)
        {
            return universe * FaderCount + fader;
        }

        string GetOutputStateLabel()
        {
            if (_outputMode == ArtNetSendTestOutputMode.Once)
                return "Once";

            if (_isTestSignalSending)
                return "Test Signal sending";

            return _isLiveSending ? "Live Desk sending" : "Live Desk stopped";
        }

        string GetPrimaryStateLabel()
        {
            if (_isTestSignalSending)
                return "SENDING TEST SIGNAL";

            if (_isLiveSending)
                return "SENDING LIVE DESK";

            if (_outputMode == ArtNetSendTestOutputMode.Once)
                return "READY TO SEND ONCE";

            return "STOPPED";
        }

        string GetCompactStateLabel()
        {
            if (_isTestSignalSending)
                return "TEST SIGNAL";

            if (_isLiveSending)
                return "SENDING";

            if (_outputMode == ArtNetSendTestOutputMode.Once)
                return "ONCE";

            return "STOPPED";
        }

        string GetPrimaryStateDetail()
        {
            if (_isTestSignalSending)
                return "Universe " + _testUniverse + " test pattern at " + FormatRate() + " fps -> " + CurrentAddress + ":" + _port;

            if (_isLiveSending)
                return GetEnabledUniverseCount() + " enabled universe(s) at " + FormatRate() + " fps -> " + CurrentAddress + ":" + _port;

            if (_outputMode == ArtNetSendTestOutputMode.Once)
                return "Continuous output is stopped. Send Once will transmit enabled universes to " + CurrentAddress + ":" + _port;

            return "No continuous Art-Net output. Target is " + CurrentAddress + ":" + _port;
        }

        Color GetPrimaryStateColor()
        {
            if (_isTestSignalSending)
                return TestSignalBlue;

            if (_isLiveSending)
                return SendingBlue;

            return _outputMode == ArtNetSendTestOutputMode.Once ? OnceGray : StoppedGray;
        }

        int GetEnabledUniverseCount()
        {
            var count = 0;
            for (var i = 0; i < _universeEnabled.Length; i++)
            {
                if (_universeEnabled[i])
                    count++;
            }

            return count;
        }

        string FormatRate()
        {
            return Mathf.Clamp(_sendRate, 1f, 60f).ToString("0.#");
        }

        static void DrawStatePill(string label, Color color, params GUILayoutOption[] options)
        {
            var rect = GUILayoutUtility.GetRect(1f, 18f, options);
            EditorGUI.DrawRect(rect, color);

            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10,
                normal = { textColor = Color.white }
            };

            GUI.Label(rect, label, style);
        }

        static byte ToByte(float value)
        {
            return (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255f);
        }

        static Color EvaluateCmyLoop(float t)
        {
            if (t < 1f / 3f)
                return Color.Lerp(Color.cyan, Color.magenta, t * 3f);

            if (t < 2f / 3f)
                return Color.Lerp(Color.magenta, Color.yellow, (t - 1f / 3f) * 3f);

            return Color.Lerp(Color.yellow, Color.cyan, (t - 2f / 3f) * 3f);
        }

        string BuildUniverseSummary(int universe)
        {
            var nonZero = 0;
            var previewCount = Mathf.Min(8, FaderCount);
            var preview = "";
            for (var i = 0; i < FaderCount; i++)
            {
                var value = _values[GetValueIndex(universe, i)];
                if (value != 0) nonZero++;
                if (i < previewCount)
                {
                    if (i > 0) preview += "  ";
                    preview += "Ch" + (i + 1) + ":" + value;
                }
            }

            return "Non Zero: " + nonZero + "    " + preview;
        }

        string CurrentAddress
        {
            get
            {
                return _targetMode == ArtNetSendTargetMode.Unicast ? _unicastAddress : _broadcastAddress;
            }
        }
    }
}
