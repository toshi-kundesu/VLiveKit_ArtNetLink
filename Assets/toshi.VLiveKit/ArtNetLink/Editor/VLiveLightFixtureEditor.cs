// VLiveKit is all Unlicense.
// unlicense: https://unlicense.org/

using toshi.VLiveKit.Lighting;
using UnityEditor;
using UnityEngine;

namespace toshi.VLiveKit.ArtNetLink.Editor
{
    [CustomEditor(typeof(VLiveLightFixture))]
    public sealed class VLiveLightFixtureEditor : UnityEditor.Editor
    {
        SerializedProperty _color;
        SerializedProperty _useArtNetREC;
        SerializedProperty _artNetChannels;
        SerializedProperty _receiver;
        SerializedProperty _light;
        SerializedProperty _targetRenderers;
        SerializedProperty _panPart;
        SerializedProperty _tiltPart;
        SerializedProperty _universe;
        SerializedProperty _startAdress;
        SerializedProperty _panChannel;
        SerializedProperty _panFineChannel;
        SerializedProperty _tiltChannel;
        SerializedProperty _tiltFineChannel;
        SerializedProperty _dimmerChannel;
        SerializedProperty _redChannel;
        SerializedProperty _greenChannel;
        SerializedProperty _blueChannel;
        SerializedProperty _whiteChannel;
        SerializedProperty _zoomChannel;
        SerializedProperty _goboChannel;
        SerializedProperty _useWhiteChannel;
        SerializedProperty _usePanFineChannel;
        SerializedProperty _useTiltFineChannel;
        SerializedProperty _useZoom;
        SerializedProperty _invertZoom;
        SerializedProperty _minZoomAngle;
        SerializedProperty _maxZoomAngle;
        SerializedProperty _spotInnerRatio;
        SerializedProperty _useGobo;
        SerializedProperty _goboTextures;
        SerializedProperty _parLightPreset;
        SerializedProperty _applyParLightPreset;
        SerializedProperty _maxLumen;
        SerializedProperty _colorTemperature;
        SerializedProperty _emissiveSurfaceArea;
        SerializedProperty _rendererLumenScale;
        SerializedProperty _panTiltSmoothTime;
        SerializedProperty _panSmoothTimeOverride;
        SerializedProperty _tiltSmoothTimeOverride;
        SerializedProperty _panRotationAxis;
        SerializedProperty _tiltRotationAxis;
        SerializedProperty _minPanAngle;
        SerializedProperty _maxPanAngle;
        SerializedProperty _minTiltAngle;
        SerializedProperty _maxTiltAngle;
        SerializedProperty _p;
        SerializedProperty _t;
        SerializedProperty _i;
        SerializedProperty _r;
        SerializedProperty _g;
        SerializedProperty _b;
        SerializedProperty _w;
        SerializedProperty _z;

        bool _showChannels = true;
        bool _showPanTilt = true;
        bool _showLight = true;
        bool _showRuntime;

        void OnEnable()
        {
            _color = serializedObject.FindProperty("_color");
            _useArtNetREC = serializedObject.FindProperty("useArtNetREC");
            _artNetChannels = serializedObject.FindProperty("artNetChannels");
            _receiver = serializedObject.FindProperty("receiver");
            _light = serializedObject.FindProperty("light");
            _targetRenderers = serializedObject.FindProperty("targetRenderers");
            _panPart = serializedObject.FindProperty("_Panpart");
            _tiltPart = serializedObject.FindProperty("_Tiltpart");
            _universe = serializedObject.FindProperty("universe");
            _startAdress = serializedObject.FindProperty("startAdress");
            _panChannel = serializedObject.FindProperty("panChannel");
            _panFineChannel = serializedObject.FindProperty("panFineChannel");
            _tiltChannel = serializedObject.FindProperty("tiltChannel");
            _tiltFineChannel = serializedObject.FindProperty("tiltFineChannel");
            _dimmerChannel = serializedObject.FindProperty("dimmerChannel");
            _redChannel = serializedObject.FindProperty("redChannel");
            _greenChannel = serializedObject.FindProperty("greenChannel");
            _blueChannel = serializedObject.FindProperty("blueChannel");
            _whiteChannel = serializedObject.FindProperty("whiteChannel");
            _zoomChannel = serializedObject.FindProperty("zoomChannel");
            _goboChannel = serializedObject.FindProperty("goboChannel");
            _useWhiteChannel = serializedObject.FindProperty("useWhiteChannel");
            _usePanFineChannel = serializedObject.FindProperty("usePanFineChannel");
            _useTiltFineChannel = serializedObject.FindProperty("useTiltFineChannel");
            _useZoom = serializedObject.FindProperty("useZoom");
            _invertZoom = serializedObject.FindProperty("invertZoom");
            _minZoomAngle = serializedObject.FindProperty("minZoomAngle");
            _maxZoomAngle = serializedObject.FindProperty("maxZoomAngle");
            _spotInnerRatio = serializedObject.FindProperty("spotInnerRatio");
            _useGobo = serializedObject.FindProperty("useGobo");
            _goboTextures = serializedObject.FindProperty("goboTextures");
            _parLightPreset = serializedObject.FindProperty("parLightPreset");
            _applyParLightPreset = serializedObject.FindProperty("applyParLightPreset");
            _maxLumen = serializedObject.FindProperty("maxLumen");
            _colorTemperature = serializedObject.FindProperty("colorTemperature");
            _emissiveSurfaceArea = serializedObject.FindProperty("emissiveSurfaceArea");
            _rendererLumenScale = serializedObject.FindProperty("rendererLumenScale");
            _panTiltSmoothTime = serializedObject.FindProperty("panTiltSmoothTime");
            _panSmoothTimeOverride = serializedObject.FindProperty("panSmoothTimeOverride");
            _tiltSmoothTimeOverride = serializedObject.FindProperty("tiltSmoothTimeOverride");
            _panRotationAxis = serializedObject.FindProperty("panRotationAxis");
            _tiltRotationAxis = serializedObject.FindProperty("tiltRotationAxis");
            _minPanAngle = serializedObject.FindProperty("MinPanAngle");
            _maxPanAngle = serializedObject.FindProperty("MaxPanAngle");
            _minTiltAngle = serializedObject.FindProperty("MinTiltAngle");
            _maxTiltAngle = serializedObject.FindProperty("MaxTiltAngle");
            _p = serializedObject.FindProperty("_p");
            _t = serializedObject.FindProperty("_t");
            _i = serializedObject.FindProperty("_i");
            _r = serializedObject.FindProperty("_r");
            _g = serializedObject.FindProperty("_g");
            _b = serializedObject.FindProperty("_b");
            _w = serializedObject.FindProperty("_w");
            _z = serializedObject.FindProperty("_z");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("VLive Light Fixture", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Patch values are Art-Net 0-based Universe and DMX 1-based Start Address. For basic light output, use toshi/VLiveKit/Lighting/SimpleLightConsole.", MessageType.None);

            DrawSourceAndTargets();
            DrawPatch();
            DrawChannels();
            DrawPanTilt();
            DrawLightAndColor();
            DrawRuntime();

            serializedObject.ApplyModifiedProperties();
        }

        void DrawSourceAndTargets()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Input / Targets", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_color, new GUIContent("Current Color"));
                EditorGUILayout.PropertyField(_useArtNetREC, new GUIContent("Use REC Data"));
                using (new EditorGUI.DisabledScope(!_useArtNetREC.boolValue))
                EditorGUILayout.PropertyField(_artNetChannels, new GUIContent("REC Channels"));
                EditorGUILayout.PropertyField(_receiver, new GUIContent("Art-Net Receiver"));
                EditorGUILayout.PropertyField(_light, new GUIContent("HDRP Light"));
                using (new EditorGUI.DisabledScope(_light.objectReferenceValue == null))
                {
                    if (GUILayout.Button("Select HDRP Light Inspector"))
                        Selection.activeObject = _light.objectReferenceValue;
                }
                EditorGUILayout.PropertyField(_targetRenderers, new GUIContent("Emission Renderers"), true);
                EditorGUILayout.PropertyField(_panPart, new GUIContent("Pan Part"));
                EditorGUILayout.PropertyField(_tiltPart, new GUIContent("Tilt Part"));
            }
        }

        void DrawPatch()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Patch", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Many desks display Universe 1 while sending Art-Net Universe 0. Use ArtNet Monitor if values do not move.", MessageType.Info);
                EditorGUILayout.PropertyField(_universe, new GUIContent("Universe (0-based)"));
                EditorGUILayout.PropertyField(_startAdress, new GUIContent("Start Address (1-based)"));
            }
        }

        void DrawChannels()
        {
            _showChannels = EditorGUILayout.Foldout(_showChannels, "DMX Channels", true);
            if (!_showChannels) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (GUILayout.Button("Apply 6ch Test Map (Pan, Tilt, Dimmer, R, G, B)"))
                    ApplySixChannelTestMap();

                EditorGUILayout.Space(2f);
                EditorGUILayout.LabelField("Relative DMX Channels (1-based)", EditorStyles.boldLabel);
                DrawChannel(_panChannel, "Pan");
                DrawOptionalChannel(_usePanFineChannel, _panFineChannel, "Pan Fine");
                DrawChannel(_tiltChannel, "Tilt");
                DrawOptionalChannel(_useTiltFineChannel, _tiltFineChannel, "Tilt Fine");
                DrawChannel(_dimmerChannel, "Dimmer");
                DrawChannel(_redChannel, "Red");
                DrawChannel(_greenChannel, "Green");
                DrawChannel(_blueChannel, "Blue");
                DrawOptionalChannel(_useWhiteChannel, _whiteChannel, "White");
                DrawOptionalChannel(_useZoom, _zoomChannel, "Zoom");
                DrawOptionalChannel(_useGobo, _goboChannel, "Gobo");
            }
        }

        void DrawPanTilt()
        {
            _showPanTilt = EditorGUILayout.Foldout(_showPanTilt, "Pan / Tilt", true);
            if (!_showPanTilt) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawEnum<MovingRotationAxis>(_panRotationAxis, "Pan Rotation Axis");
                DrawEnum<MovingRotationAxis>(_tiltRotationAxis, "Tilt Rotation Axis");
                DrawFloat(_minPanAngle, "Min Pan Angle", "deg");
                DrawFloat(_maxPanAngle, "Max Pan Angle", "deg");
                DrawFloat(_minTiltAngle, "Min Tilt Angle", "deg");
                DrawFloat(_maxTiltAngle, "Max Tilt Angle", "deg");
                EditorGUILayout.Space(2f);
                DrawFloat(_panTiltSmoothTime, "Smooth Time", "s");
                DrawFloat(_panSmoothTimeOverride, "Pan Smooth Override", "s");
                DrawFloat(_tiltSmoothTimeOverride, "Tilt Smooth Override", "s");
            }
        }

        void DrawLightAndColor()
        {
            _showLight = EditorGUILayout.Foldout(_showLight, "Light / Color", true);
            if (!_showLight) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawEnum<ParLightPreset>(_parLightPreset, "PAR Preset");
                DrawToggle(_applyParLightPreset, "Apply Preset");
                DrawFloat(_maxLumen, "Max Intensity", "lm");
                DrawFloat(_colorTemperature, "White Color Temperature", "K");
                DrawFloat(_emissiveSurfaceArea, "Emitter Area", "m2");
                DrawFloat(_rendererLumenScale, "Renderer Lumen Scale", null);
                EditorGUILayout.Space(2f);
                DrawToggle(_useZoom, "Use Zoom");
                using (new EditorGUI.DisabledScope(!_useZoom.boolValue))
                {
                    DrawToggle(_invertZoom, "Invert Zoom");
                    DrawFloat(_minZoomAngle, "Min Zoom Angle", "deg");
                    DrawFloat(_maxZoomAngle, "Max Zoom Angle", "deg");
                    DrawFloat(_spotInnerRatio, "Spot Inner Ratio", null);
                }
                DrawToggle(_useGobo, "Use Gobo");
                using (new EditorGUI.DisabledScope(!_useGobo.boolValue))
                    EditorGUILayout.PropertyField(_goboTextures, new GUIContent("Gobo Textures"), true);
            }
        }

        void DrawRuntime()
        {
            _showRuntime = EditorGUILayout.Foldout(_showRuntime, "Runtime Values", true);
            if (!_showRuntime) return;

            using (new EditorGUI.DisabledScope(true))
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(_p, new GUIContent("Pan"));
                EditorGUILayout.PropertyField(_t, new GUIContent("Tilt"));
                EditorGUILayout.PropertyField(_i, new GUIContent("Dimmer"));
                EditorGUILayout.PropertyField(_r, new GUIContent("Red"));
                EditorGUILayout.PropertyField(_g, new GUIContent("Green"));
                EditorGUILayout.PropertyField(_b, new GUIContent("Blue"));
                EditorGUILayout.PropertyField(_w, new GUIContent("White"));
                EditorGUILayout.PropertyField(_z, new GUIContent("Zoom"));
            }
        }

        static void DrawChannel(SerializedProperty property, string label)
        {
            property.intValue = Mathf.Clamp(EditorGUILayout.IntField(label, property.intValue), 1, 512);
        }

        static void DrawOptionalChannel(SerializedProperty toggle, SerializedProperty channel, string label)
        {
            var rect = EditorGUILayout.GetControlRect();
            var toggleRect = new Rect(rect.x, rect.y, EditorGUIUtility.labelWidth, rect.height);
            var fieldRect = new Rect(rect.x + EditorGUIUtility.labelWidth, rect.y, rect.width - EditorGUIUtility.labelWidth, rect.height);

            toggle.boolValue = EditorGUI.ToggleLeft(toggleRect, label, toggle.boolValue);
            using (new EditorGUI.DisabledScope(!toggle.boolValue))
            {
                channel.intValue = Mathf.Clamp(EditorGUI.IntField(fieldRect, channel.intValue), 1, 512);
            }
        }

        static void DrawToggle(SerializedProperty property, string label)
        {
            property.boolValue = EditorGUILayout.Toggle(label, property.boolValue);
        }

        static void DrawFloat(SerializedProperty property, string label, string unit)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                property.floatValue = EditorGUILayout.FloatField(label, property.floatValue);
                if (!string.IsNullOrEmpty(unit))
                    GUILayout.Label(unit, EditorStyles.miniLabel, GUILayout.Width(32f));
            }
        }

        static void DrawEnum<T>(SerializedProperty property, string label) where T : System.Enum
        {
            var value = (T)System.Enum.ToObject(typeof(T), property.enumValueIndex);
            var next = (T)EditorGUILayout.EnumPopup(label, value);
            property.enumValueIndex = System.Convert.ToInt32(next);
        }

        void ApplySixChannelTestMap()
        {
            _panChannel.intValue = 1;
            _usePanFineChannel.boolValue = false;
            _panFineChannel.intValue = 2;
            _tiltChannel.intValue = 2;
            _useTiltFineChannel.boolValue = false;
            _tiltFineChannel.intValue = 3;
            _dimmerChannel.intValue = 3;
            _redChannel.intValue = 4;
            _greenChannel.intValue = 5;
            _blueChannel.intValue = 6;
            _useWhiteChannel.boolValue = false;
            _whiteChannel.intValue = 7;
            _useZoom.boolValue = false;
            _zoomChannel.intValue = 8;
            _useGobo.boolValue = false;
            _goboChannel.intValue = 9;
        }
    }
}
