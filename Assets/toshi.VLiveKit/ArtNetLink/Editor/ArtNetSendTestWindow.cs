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

    public sealed class ArtNetSendTestWindow : EditorWindow
    {
        const int UniverseCount = 10;
        const int FaderCount = 10;
        const int ButtonWidth = 64;

        [SerializeField] ArtNetSendTargetMode _targetMode = ArtNetSendTargetMode.Broadcast;
        [SerializeField] ArtNetSendTestOutputMode _outputMode = ArtNetSendTestOutputMode.LiveDesk;
        [SerializeField] string _unicastAddress = "127.0.0.1";
        [SerializeField] string _broadcastAddress = "255.255.255.255";
        [SerializeField] int _port = ArtNetDmxSender.DefaultPort;
        [SerializeField] bool _isLiveSending;
        [SerializeField] float _sendRate = 30f;
        [SerializeField] int[] _values;
        [SerializeField] bool[] _universeEnabled;

        ArtNetDmxSender _sender;
        Vector2 _scrollPosition;
        double _nextSendTime;
        string _status = "Idle";

        [MenuItem("toshi/VLiveKit/Lighting/ArtNet Send Test")]
        public static void Open()
        {
            GetWindow<ArtNetSendTestWindow>("ArtNet Send Test");
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

            DrawDestination();
            EditorGUILayout.Space(6f);
            DrawTransport();
            EditorGUILayout.Space(6f);
            DrawFaders();
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(_status, EditorStyles.miniLabel);
        }

        void DrawDestination()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Destination", EditorStyles.boldLabel);

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
                EditorGUI.BeginChangeCheck();
                var outputMode = (ArtNetSendTestOutputMode)EditorGUILayout.EnumPopup("Output Mode", _outputMode);
                if (EditorGUI.EndChangeCheck())
                {
                    StopLiveSending("Stopped");
                    _outputMode = outputMode;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (_outputMode == ArtNetSendTestOutputMode.LiveDesk)
                    {
                        if (!_isLiveSending)
                        {
                            if (GUILayout.Button("Send Start", EditorStyles.miniButtonLeft, GUILayout.Width(100f)))
                                StartLiveSending();
                        }
                        else
                        {
                            if (GUILayout.Button("Send Stop", EditorStyles.miniButtonLeft, GUILayout.Width(100f)))
                                StopLiveSending("Stopped");
                        }
                    }
                    else
                    {
                        if (GUILayout.Button("Send Once", EditorStyles.miniButtonLeft, GUILayout.Width(100f)))
                            SendAllEnabledUniverses();
                    }

                    if (GUILayout.Button("Blackout", EditorStyles.miniButtonMid, GUILayout.Width(80f)))
                        SetAllFaders(0, true);

                    if (GUILayout.Button("Full", EditorStyles.miniButtonRight, GUILayout.Width(64f)))
                        SetAllFaders(255, true);
                }

                using (new EditorGUI.DisabledScope(_outputMode != ArtNetSendTestOutputMode.LiveDesk))
                {
                    _sendRate = EditorGUILayout.Slider("Rate (fps)", _sendRate, 1f, 60f);
                }

                EditorGUILayout.LabelField("State", GetOutputStateLabel(), EditorStyles.miniLabel);
            }
        }

        void DrawFaders()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            for (var universe = 0; universe < UniverseCount; universe++)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _universeEnabled[universe] = EditorGUILayout.ToggleLeft(
                            "Universe " + universe,
                            _universeEnabled[universe],
                            EditorStyles.boldLabel);

                        if (GUILayout.Button("0", EditorStyles.miniButtonLeft, GUILayout.Width(ButtonWidth)))
                            SetUniverseFaders(universe, 0, true);

                        if (GUILayout.Button("255", EditorStyles.miniButtonMid, GUILayout.Width(ButtonWidth)))
                            SetUniverseFaders(universe, 255, true);

                        if (GUILayout.Button("Send", EditorStyles.miniButtonRight, GUILayout.Width(ButtonWidth)))
                            SendUniverse(universe);
                    }

                    using (new EditorGUI.DisabledScope(!_universeEnabled[universe]))
                    {
                        for (var fader = 0; fader < FaderCount; fader++)
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
            if (_outputMode != ArtNetSendTestOutputMode.LiveDesk || !_isLiveSending) return;

            var now = EditorApplication.timeSinceStartup;
            if (now < _nextSendTime) return;

            SendAllEnabledUniverses();
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
            if (!_isLiveSending) return;

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
        }

        int GetValueIndex(int universe, int fader)
        {
            return universe * FaderCount + fader;
        }

        string GetOutputStateLabel()
        {
            if (_outputMode == ArtNetSendTestOutputMode.Once)
                return "Once";

            return _isLiveSending ? "Live Desk sending" : "Live Desk stopped";
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
