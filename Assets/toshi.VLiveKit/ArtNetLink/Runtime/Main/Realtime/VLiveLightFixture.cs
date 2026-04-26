// VLiveKit is all Unlicense.
// unlicense: https://unlicense.org/
// last update: 2024/11/26

using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace toshi.VLiveKit.Lighting
{
    public enum MovingRotationAxis
    {
        X, Y, Z
    }

    public class VLiveLightFixture : MonoBehaviour
    {
        /* =========================
         * Basic
         * ========================= */
        [SerializeField] private Color _color = Color.black;

        [Header("[Settings to use RECData]")]
        [SerializeField] public bool useArtNetREC = false;
        [SerializeField] public DmxChannels artNetChannels = null;

        [Header("[ArtNet Receiver]")]
        [SerializeField] public VLiveArtNetReceiver receiver;

        [Header("[Controll Target Light]")]
        [SerializeField] private HDAdditionalLightData light;

        [Header("[Controll Target Renderer]")]
        // 複数レンダラーを選べるようにする
        [SerializeField] private Renderer[] targetRenderers;

        [Header("[Target Shader Parameters]")]
        [SerializeField] private string _RGBColor = "_RGBColor";

        [Header("[Pan & Tilt Parts]")]
        [SerializeField] private Transform _Panpart;
        [SerializeField] private Transform _Tiltpart;

        /* =========================
         * Patch
         * ========================= */
        [Header("[Patch Fixture]")]
        [SerializeField] [Range(0, 15)] public int universe = 0;
        [SerializeField] public int startAdress = 1;

        [Header("[DMX Channels (relative, 1-based)]")]
        [SerializeField] private int panChannel = 1;
        [SerializeField] private int panFineChannel = 2;
        [SerializeField] private int tiltChannel = 3;
        [SerializeField] private int tiltFineChannel = 4;

        [SerializeField] private int dimmerChannel = 5;

        [SerializeField] private int redChannel = 6;
        [SerializeField] private int greenChannel = 7;
        [SerializeField] private int blueChannel = 8;
        [SerializeField] private int whiteChannel = 9;

        [SerializeField] private int zoomChannel = 10;
        [SerializeField] private int goboChannel = 11;

        /* =========================
         * Options
         * ========================= */
        [Header("[White Channel]")]
        [SerializeField] private bool useWhiteChannel = true;

        [Header("[Zoom]")]
        [SerializeField] private bool useZoom = true;

        [Tooltip("ON: フェーダーを上げると細くなる（実機/MagicQ寄り）")]
        [SerializeField] private bool invertZoom = true;

        [SerializeField] private float minZoomAngle = 7f;   // Narrow
        [SerializeField] private float maxZoomAngle = 45f;  // Wide

        [Header("[Spot Sharpness]")]
        [Tooltip("HDRPの Inner Angle(%) を作るための比率(0-1)。大きいほど芯が硬い")]
        [SerializeField, Range(0.01f, 0.99f)]
        private float spotInnerRatio = 0.80f; // 0.8 = 80%

        [Header("[Gobo]")]
        [SerializeField] private bool useGobo = false;
        [SerializeField] private Texture2D[] goboTextures;

        /* =========================
         * Light Params
         * ========================= */
        [Header("[Light Parameters]")]
        [SerializeField] private float maxLumen = 8000f;
        [SerializeField] private float maxRendererIntensity = 10f;
        [SerializeField] private float colorTemperature = 6500f;

        /* =========================
         * Pan / Tilt Params
         * ========================= */
        [Header("[Pan & Tilt Smoothing]")]
        [Tooltip("追従の速さ(秒)。0=即時、値が大きいほどゆっくり（目安: 0.05〜0.5）")]
        [SerializeField] private float panTiltSmoothTime = 0.15f;

        [Tooltip("パンだけ別設定にしたい場合（-1で共通値を使う）")]
        [SerializeField] private float panSmoothTimeOverride = -1f;

        [Tooltip("チルトだけ別設定にしたい場合（-1で共通値を使う）")]
        [SerializeField] private float tiltSmoothTimeOverride = -1f;

        [Header("[Pan & Tilt Parameters]")]
        [SerializeField] private MovingRotationAxis panRotationAxis = MovingRotationAxis.Y;
        [SerializeField] private MovingRotationAxis tiltRotationAxis = MovingRotationAxis.X;

        [SerializeField] private float MinPanAngle = 270f;
        [SerializeField] private float MaxPanAngle = -270f;

        [SerializeField] private float MinTiltAngle = 135f;
        [SerializeField] private float MaxTiltAngle = -135f;

        /* =========================
         * Runtime Values
         * ========================= */
        [Header("[Runtime Values]")]
        [SerializeField] [Range(0f, 1f)] private float _p;
        [SerializeField] [Range(0f, 1f)] private float _t;
        [SerializeField] [Range(0f, 1f)] private float _i;
        [SerializeField] [Range(0f, 1f)] private float _r;
        [SerializeField] [Range(0f, 1f)] private float _g;
        [SerializeField] [Range(0f, 1f)] private float _b;
        [SerializeField] [Range(0f, 1f)] private float _w;
        [SerializeField] [Range(0f, 1f)] private float _z;

        private int _goboRaw;

        private Quaternion panAverage = Quaternion.identity;
        private Quaternion tiltAverage = Quaternion.identity;
        private MaterialPropertyBlock mpb;

        /* =========================
         * Init
         * ========================= */
        private void Start()
        {
            if (receiver != null)
            {
                receiver._universeToUse = universe;
                receiver.OnDataReceived += data =>
                {
                    if (data == null || data.Length <= 18) return;

                    int[] channels = new int[data.Length - 18];
                    for (int i = 18; i < data.Length; i++)
                        channels[i - 18] = data[i];

                    UpdateFixture(channels);
                };
            }
        }

        /* =========================
         * Update Loop
         * ========================= */
        void Update()
        {
            if (useArtNetREC && artNetChannels != null)
            {
                int[] values = new int[512];
                for (int i = 1; i <= 512; i++)
                    values[i - 1] = artNetChannels.GetChannelValue(i);

                UpdateFixture(values);
            }

            UpdateColor();
            UpdateLight();
            UpdateRenderer();
            UpdatePan();
            UpdateTilt();
            UpdateGobo();
        }

        /* =========================
         * DMX Decode
         * ========================= */
        public void UpdateFixture(int[] data)
        {
            int baseIndex = startAdress - 1;

            _p = Normalize16(data, baseIndex + panChannel - 1, baseIndex + panFineChannel - 1);
            _t = Normalize16(data, baseIndex + tiltChannel - 1, baseIndex + tiltFineChannel - 1);

            _i = Get8(data, baseIndex + dimmerChannel - 1);
            _r = Get8(data, baseIndex + redChannel - 1);
            _g = Get8(data, baseIndex + greenChannel - 1);
            _b = Get8(data, baseIndex + blueChannel - 1);

            _w = useWhiteChannel ? Get8(data, baseIndex + whiteChannel - 1) : 0f;
            _z = useZoom ? Get8(data, baseIndex + zoomChannel - 1) : 0f;

            if (useGobo) _goboRaw = GetRaw(data, baseIndex + goboChannel - 1);
        }

        /* =========================
         * Light / Renderer
         * ========================= */
        void UpdateLight()
        {
            if (light == null) return;

            #if UNITY_2021_2_OR_NEWER
                light.SetIntensity(_i * maxLumen, LightUnit.Lumen);
            #else
                light.intensity = _i * maxLumen;
            #endif
            light.SetColor(new Color(_r, _g, _b, _w), colorTemperature);

            if (useZoom)
            {
                float z = invertZoom ? 1f - _z : _z;

                // HDRP: SetSpotAngle(angle, innerSpotPercent)
                // innerSpotPercent は 0〜100 の「％」
                float outerAngle = Mathf.Lerp(minZoomAngle, maxZoomAngle, z);

                float innerSpotPercent = Mathf.Clamp(spotInnerRatio, 0.01f, 0.99f) * 100f;

                light.SetSpotAngle(outerAngle, innerSpotPercent);
            }
        }

        void UpdateColor()
        {
            _color = new Color(_r + _w, _g + _w, _b + _w, 1f);
        }

        void UpdateRenderer()
        {
            if (targetRenderers == null || targetRenderers.Length == 0) return;

            if (mpb == null) mpb = new MaterialPropertyBlock();

            foreach (var renderer in   targetRenderers)
            {
                if (renderer == null) continue;
                
                renderer.GetPropertyBlock(mpb);
                mpb.SetColor(_RGBColor, _color * (_i * maxRendererIntensity));
                renderer.SetPropertyBlock(mpb);
            }
        }

        /* =========================
         * Pan / Tilt
         * ========================= */
        void UpdatePan()
        {
            if (_Panpart == null) return;

            float angle = Mathf.Lerp(MinPanAngle, MaxPanAngle, _p);
            Quaternion rot = Quaternion.Euler(GetAxisVector(panRotationAxis, angle));

            float smoothTime = (panSmoothTimeOverride >= 0f) ? panSmoothTimeOverride : panTiltSmoothTime;
            float k = SmoothFactor(smoothTime, Time.deltaTime);

            panAverage = Quaternion.Slerp(panAverage, rot, k);
            _Panpart.localRotation = panAverage;
        }

        void UpdateTilt()
        {
            if (_Tiltpart == null) return;

            float angle = Mathf.Lerp(MinTiltAngle, MaxTiltAngle, _t);
            Quaternion rot = Quaternion.Euler(GetAxisVector(tiltRotationAxis, angle));

            float smoothTime = (tiltSmoothTimeOverride >= 0f) ? tiltSmoothTimeOverride : panTiltSmoothTime;
            float k = SmoothFactor(smoothTime, Time.deltaTime);

            tiltAverage = Quaternion.Slerp(tiltAverage, rot, k);
            _Tiltpart.localRotation = tiltAverage;
        }

        /* =========================
         * Gobo
         * ========================= */
        void UpdateGobo()
        {
            if (!useGobo || goboTextures == null || goboTextures.Length == 0) return;

            var unityLight = light.GetComponent<Light>();
            if (unityLight == null) return;

            int index = Mathf.FloorToInt((_goboRaw / 255f) * goboTextures.Length);
            index = Mathf.Clamp(index, 0, goboTextures.Length - 1);

            unityLight.cookie = goboTextures[index];
        }

        /* =========================
         * Utils
         * ========================= */
        static float Get8(int[] d, int i)
        {
            if (i < 0 || i >= d.Length) return 0f;
            return d[i] / 255f;
        }

        static int GetRaw(int[] d, int i)
        {
            if (i < 0 || i >= d.Length) return 0;
            return d[i];
        }

        static float Normalize16(int[] d, int hi, int lo)
        {
            int h = (hi >= 0 && hi < d.Length) ? d[hi] : 0;
            int l = (lo >= 0 && lo < d.Length) ? d[lo] : 0;
            int v = (h << 8) | l;
            return v / 65535f;
        }

        static Vector3 GetAxisVector(MovingRotationAxis axis, float angle)
        {
            return axis switch
            {
                MovingRotationAxis.X => new Vector3(angle, 0, 0),
                MovingRotationAxis.Y => new Vector3(0, angle, 0),
                MovingRotationAxis.Z => new Vector3(0, 0, angle),
                _ => Vector3.zero
            };
        }

        /// <summary>
        /// smoothTime(秒) と dt から、Slerp/Lerpの係数(0-1)を作る。
        /// FPSが変わっても体感を揃えるための変換。
        /// smoothTime=0 なら即時(1)。
        /// </summary>
        static float SmoothFactor(float smoothTime, float dt)
        {
            if (smoothTime <= 0f) return 1f;
            if (dt <= 0f) return 0f;
            return 1f - Mathf.Exp(-dt / smoothTime);
        }
    }
}
