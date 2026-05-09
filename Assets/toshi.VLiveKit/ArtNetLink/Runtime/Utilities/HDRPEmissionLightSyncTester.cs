// VLiveKit is all Unlicense.
// unlicense: https://unlicense.org/

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace toshi.VLiveKit.Lighting
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class HDRPEmissionLightSyncTester : MonoBehaviour
    {
        public enum EmissionInputUnit
        {
            Nits,
            EV100
        }

        public enum LightOutputMode
        {
            AutoNitsThenLumen,
            ForceNits,
            ForceLumen
        }

        [Header("Targets")]
        [SerializeField] private Renderer[] targetRenderers;
        [SerializeField, Min(0)] private int materialIndex;
        [SerializeField] private Light targetLight;
        [SerializeField] private bool autoAddHDAdditionalLightData = true;
        [SerializeField] private bool writeToSharedMaterials = false;

        [Header("HDRP Lit Material Properties")]
        [SerializeField] private bool syncHDRPLitProperties = true;
        [SerializeField] private string hdrpLitEmissiveColorProperty = "_EmissiveColor";
        [SerializeField] private string hdrpLitEmissiveColorLdrProperty = "_EmissiveColorLDR";
        [SerializeField] private string hdrpLitEmissiveIntensityProperty = "_EmissiveIntensity";
        [SerializeField] private string hdrpLitEmissiveIntensityUnitProperty = "_EmissiveIntensityUnit";
        [SerializeField] private string hdrpLitUseEmissiveIntensityProperty = "_UseEmissiveIntensity";
        [SerializeField] private string hdrpLitEmissiveExposureWeightProperty = "_EmissiveExposureWeight";
        [SerializeField] private string hdrpLitGILegacyEmissionColorProperty = "_EmissionColor";

        [Header("Shader Graph Emission Node Properties")]
        [SerializeField] private bool syncEmissionNodeProperties = true;
        [SerializeField] private string emissionColorProperty = "_EmissionColor";
        [SerializeField] private string emissionIntensityProperty = "_EmissionIntensity";
        [SerializeField] private string exposureWeightProperty = "_EmissionExposureWeight";

        [Header("RGB/Brightness Shader Properties")]
        [SerializeField] private bool syncRgbBrightnessProperties = true;
        [SerializeField] private string rgbColorProperty = "_RGBColor";
        [SerializeField] private string brightnessProperty = "_Brightness";
        [SerializeField] private bool multiplyRgbColorByBrightness = false;
        [SerializeField, Min(0f)] private float brightnessPropertyScale = 1f;

        [Header("Emission Master")]
        [SerializeField] private EmissionInputUnit emissionInputUnit = EmissionInputUnit.Nits;
        [SerializeField, Min(0f)] private float emissionNits = 1000f;
        [SerializeField] private float emissionEV100 = 9f;
        [SerializeField, Range(0f, 1f)] private float exposureWeight = 1f;
        [SerializeField] private Color tint = Color.white;
        [SerializeField] private bool useColorTemperature = true;
        [SerializeField, Range(1000f, 40000f)] private float colorTemperatureKelvin = 6500f;

        [Header("Light Sync")]
        [SerializeField] private LightOutputMode lightOutputMode = LightOutputMode.AutoNitsThenLumen;
        [SerializeField] private bool syncLightColor = true;
        [SerializeField] private bool syncLightIntensity = true;
        [SerializeField] private bool forceRectangleAreaLight = false;
        [SerializeField] private bool syncRectangleAreaSize = true;
        [SerializeField] private Vector2 sourceSizeMeters = Vector2.one;

        [Header("Test Animation")]
        [SerializeField] private bool animateIntensity = false;
        [SerializeField, Min(0f)] private float pulseHz = 0.5f;
        [SerializeField] private Vector2 pulseMultiplier = new Vector2(0.25f, 1f);

        private MaterialPropertyBlock propertyBlock;
        private bool warnedUnsupportedUnit;

        private void Reset()
        {
            var renderer = GetComponent<Renderer>();
            if (renderer != null)
            {
                targetRenderers = new[] { renderer };
            }

            targetLight = GetComponent<Light>();
            if (targetLight == null)
            {
                targetLight = GetComponentInChildren<Light>();
            }
        }

        private void OnEnable()
        {
            EnsurePropertyBlock();
            Apply();
        }

        private void OnValidate()
        {
            materialIndex = Mathf.Max(0, materialIndex);
            colorTemperatureKelvin = Mathf.Clamp(colorTemperatureKelvin, 1000f, 40000f);
            sourceSizeMeters = new Vector2(
                Mathf.Max(0.001f, Mathf.Abs(sourceSizeMeters.x)),
                Mathf.Max(0.001f, Mathf.Abs(sourceSizeMeters.y))
            );
            pulseMultiplier.x = Mathf.Max(0f, pulseMultiplier.x);
            pulseMultiplier.y = Mathf.Max(pulseMultiplier.x, pulseMultiplier.y);
            brightnessPropertyScale = Mathf.Max(0f, brightnessPropertyScale);

            EnsurePropertyBlock();
            Apply();
        }

        private void Update()
        {
            if (animateIntensity || Application.isPlaying)
            {
                Apply();
            }
        }

        [ContextMenu("Apply Sync Now")]
        public void Apply()
        {
            EnsurePropertyBlock();

            float currentNits = GetCurrentNits();
            Color finalEmissionColor = GetFinalEmissionColor();

            ApplyRenderers(currentNits, finalEmissionColor);
            ApplyLight(currentNits);
        }

        private void EnsurePropertyBlock()
        {
            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }
        }

        private float GetCurrentNits()
        {
            float baseNits = emissionInputUnit == EmissionInputUnit.Nits
                ? emissionNits
                : EV100ToNits(emissionEV100);

            if (animateIntensity && pulseHz > 0f)
            {
                float phase = Time.realtimeSinceStartup * pulseHz * Mathf.PI * 2f;
                float t = (Mathf.Sin(phase) + 1f) * 0.5f;
                baseNits *= Mathf.Lerp(pulseMultiplier.x, pulseMultiplier.y, t);
            }

            return Mathf.Max(0f, baseNits);
        }

        private float GetGraphIntensity(float currentNits)
        {
            return emissionInputUnit == EmissionInputUnit.Nits
                ? currentNits
                : NitsToEV100(Mathf.Max(currentNits, 1e-6f));
        }

        private Color GetFinalEmissionColor()
        {
            Color color = tint;

            if (useColorTemperature)
            {
                Color kelvin = Mathf.CorrelatedColorTemperatureToRGB(colorTemperatureKelvin);
                color = new Color(
                    color.r * kelvin.r,
                    color.g * kelvin.g,
                    color.b * kelvin.b,
                    color.a
                );
            }

            return color;
        }

        private void ApplyRenderers(float currentNits, Color finalEmissionColor)
        {
            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                return;
            }

            float brightness = currentNits * brightnessPropertyScale;
            Color rgbColor = multiplyRgbColorByBrightness
                ? finalEmissionColor * brightness
                : finalEmissionColor;
            Color hdrpLitEmissiveColor = MultiplyColorRgb(finalEmissionColor, currentNits);

            foreach (Renderer renderer in targetRenderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                Material[] sharedMaterials = renderer.sharedMaterials;
                if (sharedMaterials == null || sharedMaterials.Length == 0)
                {
                    continue;
                }

                int safeIndex = Mathf.Clamp(materialIndex, 0, sharedMaterials.Length - 1);
                Material material = sharedMaterials[safeIndex];
                if (material == null)
                {
                    continue;
                }

                if (writeToSharedMaterials)
                {
                    ApplyMaterialProperties(material, currentNits, finalEmissionColor, hdrpLitEmissiveColor, brightness, rgbColor);
                }
                else
                {
                    renderer.GetPropertyBlock(propertyBlock, safeIndex);
                    ApplyPropertyBlockProperties(material, currentNits, finalEmissionColor, hdrpLitEmissiveColor, brightness, rgbColor);
                    renderer.SetPropertyBlock(propertyBlock, safeIndex);
                }
            }
        }

        private void ApplyPropertyBlockProperties(
            Material material,
            float currentNits,
            Color finalEmissionColor,
            Color hdrpLitEmissiveColor,
            float brightness,
            Color rgbColor
        )
        {
            if (syncHDRPLitProperties && HasNamedProperty(material, hdrpLitEmissiveColorProperty))
            {
                SetColorIfPresent(material, propertyBlock, hdrpLitEmissiveColorProperty, hdrpLitEmissiveColor);
                SetColorIfPresent(material, propertyBlock, hdrpLitEmissiveColorLdrProperty, finalEmissionColor);
                SetFloatIfPresent(material, propertyBlock, hdrpLitUseEmissiveIntensityProperty, 1f);
                SetFloatIfPresent(material, propertyBlock, hdrpLitEmissiveIntensityProperty, currentNits);
                SetFloatIfPresent(material, propertyBlock, hdrpLitEmissiveIntensityUnitProperty, GetHDRPEmissionIntensityUnitValue());
                SetFloatIfPresent(material, propertyBlock, hdrpLitEmissiveExposureWeightProperty, exposureWeight);
                SetColorIfPresent(material, propertyBlock, hdrpLitGILegacyEmissionColorProperty, hdrpLitEmissiveColor);
            }

            if (syncEmissionNodeProperties && HasNamedProperty(material, emissionIntensityProperty))
            {
                SetColorIfPresent(material, propertyBlock, emissionColorProperty, finalEmissionColor);
                SetFloatIfPresent(material, propertyBlock, emissionIntensityProperty, GetGraphIntensity(currentNits));
                SetFloatIfPresent(material, propertyBlock, exposureWeightProperty, exposureWeight);
            }

            if (syncRgbBrightnessProperties)
            {
                SetColorIfPresent(material, propertyBlock, rgbColorProperty, rgbColor);
                SetFloatIfPresent(material, propertyBlock, brightnessProperty, brightness);
            }
        }

        private void ApplyMaterialProperties(
            Material material,
            float currentNits,
            Color finalEmissionColor,
            Color hdrpLitEmissiveColor,
            float brightness,
            Color rgbColor
        )
        {
            if (syncHDRPLitProperties && HasNamedProperty(material, hdrpLitEmissiveColorProperty))
            {
                SetColorIfPresent(material, hdrpLitEmissiveColorProperty, hdrpLitEmissiveColor);
                SetColorIfPresent(material, hdrpLitEmissiveColorLdrProperty, finalEmissionColor);
                SetFloatIfPresent(material, hdrpLitUseEmissiveIntensityProperty, 1f);
                SetFloatIfPresent(material, hdrpLitEmissiveIntensityProperty, currentNits);
                SetFloatIfPresent(material, hdrpLitEmissiveIntensityUnitProperty, GetHDRPEmissionIntensityUnitValue());
                SetFloatIfPresent(material, hdrpLitEmissiveExposureWeightProperty, exposureWeight);
                SetColorIfPresent(material, hdrpLitGILegacyEmissionColorProperty, hdrpLitEmissiveColor);
            }

            if (syncEmissionNodeProperties && HasNamedProperty(material, emissionIntensityProperty))
            {
                SetColorIfPresent(material, emissionColorProperty, finalEmissionColor);
                SetFloatIfPresent(material, emissionIntensityProperty, GetGraphIntensity(currentNits));
                SetFloatIfPresent(material, exposureWeightProperty, exposureWeight);
            }

            if (syncRgbBrightnessProperties)
            {
                SetColorIfPresent(material, rgbColorProperty, rgbColor);
                SetFloatIfPresent(material, brightnessProperty, brightness);
            }
        }

        private void ApplyLight(float currentNits)
        {
            if (targetLight == null)
            {
                return;
            }

            HDAdditionalLightData hdLight = GetOrAddHDAdditionalLightData(targetLight);
            if (hdLight == null)
            {
                return;
            }

            if (forceRectangleAreaLight)
            {
#if UNITY_2023_2_OR_NEWER
                targetLight.type = LightType.Rectangle;
#else
                hdLight.SetLightTypeAndShape(HDLightTypeAndShape.RectangleArea);
#endif
            }

#if UNITY_6000_3_OR_NEWER
            if (syncRectangleAreaSize && targetLight.type == LightType.Rectangle)
            {
                targetLight.areaSize = sourceSizeMeters;
            }
#else
#pragma warning disable CS0618
            if (syncRectangleAreaSize && hdLight.type == HDLightType.Area && hdLight.areaLightShape == AreaLightShape.Rectangle)
            {
                hdLight.SetAreaLightSize(sourceSizeMeters);
            }
#pragma warning restore CS0618
#endif

            if (syncLightColor)
            {
                if (useColorTemperature)
                {
                    hdLight.SetColor(tint, colorTemperatureKelvin);
                }
                else
                {
                    hdLight.SetColor(tint);
                }

                hdLight.EnableColorTemperature(useColorTemperature);
            }

            if (syncLightIntensity)
            {
                ApplyLightIntensity(hdLight, currentNits);
            }
        }

        private HDAdditionalLightData GetOrAddHDAdditionalLightData(Light lightComponent)
        {
            if (lightComponent == null)
            {
                return null;
            }

            if (lightComponent.TryGetComponent(out HDAdditionalLightData hdLight))
            {
                return hdLight;
            }

            return autoAddHDAdditionalLightData
                ? lightComponent.gameObject.AddComponent<HDAdditionalLightData>()
                : null;
        }

        private void ApplyLightIntensity(HDAdditionalLightData hdLight, float currentNits)
        {
            LightUnit unit;
            float intensity;

            switch (lightOutputMode)
            {
                case LightOutputMode.ForceNits:
                    if (!IsLightUnitSupported(hdLight, LightUnit.Nits))
                    {
                        WarnUnsupportedUnit(hdLight, LightUnit.Nits);
                        return;
                    }

                    unit = LightUnit.Nits;
                    intensity = currentNits;
                    break;

                case LightOutputMode.ForceLumen:
                    if (!IsLightUnitSupported(hdLight, LightUnit.Lumen))
                    {
                        WarnUnsupportedUnit(hdLight, LightUnit.Lumen);
                        return;
                    }

                    unit = LightUnit.Lumen;
                    intensity = NitsToRectLumen(currentNits, sourceSizeMeters);
                    break;

                default:
                    if (IsLightUnitSupported(hdLight, LightUnit.Nits))
                    {
                        unit = LightUnit.Nits;
                        intensity = currentNits;
                    }
                    else if (IsLightUnitSupported(hdLight, LightUnit.Lumen))
                    {
                        unit = LightUnit.Lumen;
                        intensity = NitsToRectLumen(currentNits, sourceSizeMeters);
                    }
                    else
                    {
                        WarnUnsupportedUnit(hdLight, LightUnit.Nits);
                        return;
                    }

                    break;
            }

            SetLightIntensity(hdLight, intensity, unit);
        }

        private void WarnUnsupportedUnit(HDAdditionalLightData hdLight, LightUnit unit)
        {
            if (warnedUnsupportedUnit)
            {
                return;
            }

            warnedUnsupportedUnit = true;
            Debug.LogWarning(
                $"{nameof(HDRPEmissionLightSyncTester)}: {GetLightTypeName(hdLight)} light does not support {unit}. " +
                "Use an HDRP Rectangle Area Light for strict Nits sync, or use Lumen sync for Point/Spot lights.",
                this
            );
        }

        private static bool IsLightUnitSupported(HDAdditionalLightData hdLight, LightUnit unit)
        {
            if (hdLight == null)
            {
                return false;
            }

            Light lightComponent = GetLightComponent(hdLight);
            if (lightComponent == null)
            {
                return false;
            }

#if UNITY_2023_3_OR_NEWER
            return LightUnitUtils.IsLightUnitSupported(lightComponent.type, unit);
#else
            LightUnit[] supportedUnits = hdLight.GetSupportedLightUnits();
            for (int i = 0; i < supportedUnits.Length; i++)
            {
                if (supportedUnits[i] == unit)
                {
                    return true;
                }
            }

            return false;
#endif
        }

        private static void SetLightIntensity(HDAdditionalLightData hdLight, float intensity, LightUnit unit)
        {
#if UNITY_2023_3_OR_NEWER
            Light lightComponent = GetLightComponent(hdLight);
            if (lightComponent == null)
            {
                return;
            }

            lightComponent.intensity = LightUnitUtils.ConvertIntensity(
                lightComponent,
                intensity,
                unit,
                LightUnitUtils.GetNativeLightUnit(lightComponent.type));
            lightComponent.lightUnit = unit;
#else
            hdLight.SetIntensity(intensity, unit);
#endif
        }

        private static string GetLightTypeName(HDAdditionalLightData hdLight)
        {
            Light lightComponent = GetLightComponent(hdLight);
            if (lightComponent != null)
            {
                return lightComponent.type.ToString();
            }

#if UNITY_2023_2_OR_NEWER
            return "Unknown";
#else
            return hdLight.type.ToString();
#endif
        }

        private static Light GetLightComponent(HDAdditionalLightData hdLight)
        {
            return hdLight != null ? hdLight.GetComponent<Light>() : null;
        }

        private float GetHDRPEmissionIntensityUnitValue()
        {
            return emissionInputUnit == EmissionInputUnit.EV100 ? 1f : 0f;
        }

        private static bool HasNamedProperty(Material material, string propertyName)
        {
            return material != null && !string.IsNullOrEmpty(propertyName) && material.HasProperty(propertyName);
        }

        private static void SetColorIfPresent(Material material, MaterialPropertyBlock block, string propertyName, Color value)
        {
            if (HasNamedProperty(material, propertyName))
            {
                block.SetColor(propertyName, value);
            }
        }

        private static void SetFloatIfPresent(Material material, MaterialPropertyBlock block, string propertyName, float value)
        {
            if (HasNamedProperty(material, propertyName))
            {
                block.SetFloat(propertyName, value);
            }
        }

        private static void SetColorIfPresent(Material material, string propertyName, Color value)
        {
            if (HasNamedProperty(material, propertyName))
            {
                material.SetColor(propertyName, value);
            }
        }

        private static void SetFloatIfPresent(Material material, string propertyName, float value)
        {
            if (HasNamedProperty(material, propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }

        private static Color MultiplyColorRgb(Color color, float multiplier)
        {
            return new Color(color.r * multiplier, color.g * multiplier, color.b * multiplier, color.a);
        }

        private static float NitsToRectLumen(float nits, Vector2 sizeMeters)
        {
            float area = Mathf.Max(0.001f, Mathf.Abs(sizeMeters.x)) * Mathf.Max(0.001f, Mathf.Abs(sizeMeters.y));
            return Mathf.Max(0f, nits) * area * Mathf.PI;
        }

        private static float EV100ToNits(float ev100)
        {
            return Mathf.Pow(2f, ev100) * ColorUtils.s_LightMeterCalibrationConstant / 100f;
        }

        private static float NitsToEV100(float nits)
        {
            return Mathf.Log(Mathf.Max(nits, 1e-6f) * 100f / ColorUtils.s_LightMeterCalibrationConstant, 2f);
        }
    }
}
