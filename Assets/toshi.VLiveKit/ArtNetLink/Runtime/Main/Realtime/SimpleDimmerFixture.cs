// // VLiveKit is all Unlicense.
// // unlicense: https://unlicense.org/

// using UnityEngine;
// using UnityEngine.Rendering.HighDefinition;

// namespace toshi.VLiveKit.Lighting
// {
//     [DisallowMultipleComponent]
//     [RequireComponent(typeof(VLiveArtNetReceiver))]
//     [AddComponentMenu("toshi.VLiveKit.Lighting/Fixtures/Simple Dimmer Fixture")]
//     public sealed class SimpleDimmerFixture : MonoBehaviour
//     {
//         const int ArtDmxPayloadOffset = 18;

//         [Header("[ArtNet Receiver]")]
//         [SerializeField] private VLiveArtNetReceiver receiver;
//         [SerializeField, Range(0, 64)] private int universe = 0;
//         [SerializeField, Min(1)] private int dimmerAddress = 1;
//         [SerializeField] private bool forceLocalhostReceiver = true;

//         [Header("[Light]")]
//         [SerializeField] private Light targetLight;
//         [SerializeField] private HDAdditionalLightData targetHdLight;
//         [SerializeField] private Color lightColor = Color.white;
//         [SerializeField] private float maxLumen = 1000f;

//         [Header("[Renderer Emission]")]
//         [SerializeField] private Renderer[] targetRenderers;
//         [SerializeField] private float rendererEmissionIntensity = 1f;

//         [Header("[Runtime Values]")]
//         [SerializeField, Range(0f, 1f)] private float dimmer;
//         [SerializeField] private int rawDimmer;

//         private MaterialPropertyBlock materialPropertyBlock;
//         private VLiveArtNetReceiver subscribedReceiver;

//         static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
//         static readonly int ColorId = Shader.PropertyToID("_Color");
//         static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
//         static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");
//         static readonly int EmissiveColorLdrId = Shader.PropertyToID("_EmissiveColorLDR");
//         static readonly int EmissiveIntensityId = Shader.PropertyToID("_EmissiveIntensity");
//         static readonly int EmissiveIntensityUnitId = Shader.PropertyToID("_EmissiveIntensityUnit");
//         static readonly int UseEmissiveIntensityId = Shader.PropertyToID("_UseEmissiveIntensity");

//         public float Dimmer => dimmer;
//         public int RawDimmer => rawDimmer;

//         void Reset()
//         {
//             CacheDefaultTargets();
//             EnsureReceiver(true);
//             ApplyDimmer();
//         }

//         void OnValidate()
//         {
//             universe = Mathf.Clamp(universe, 0, 64);
//             dimmerAddress = Mathf.Clamp(dimmerAddress, 1, 512);
//             maxLumen = Mathf.Max(0f, maxLumen);
//             rendererEmissionIntensity = Mathf.Max(0f, rendererEmissionIntensity);

//             var existingReceiver = GetComponent<VLiveArtNetReceiver>();
//             if (existingReceiver != null)
//             {
//                 receiver = existingReceiver;
//                 ConfigureReceiver(receiver);
//             }
//         }

//         void OnEnable()
//         {
//             EnsureReceiver(true);
//             SubscribeReceiver();
//             ApplyDimmer();
//         }

//         void OnDisable()
//         {
//             UnsubscribeReceiver();
//         }

//         void OnDataReceived(byte[] data)
//         {
//             var payloadIndex = ArtDmxPayloadOffset + dimmerAddress - 1;
//             rawDimmer = payloadIndex >= 0 && payloadIndex < data.Length ? data[payloadIndex] : 0;
//             dimmer = rawDimmer / 255f;
//             ApplyDimmer();
//         }

//         void CacheDefaultTargets()
//         {
//             if (targetLight == null)
//             {
//                 targetLight = GetComponent<Light>();
//                 if (targetLight == null)
//                 {
//                     targetLight = GetComponentInChildren<Light>();
//                 }
//             }

//             if (targetHdLight == null)
//             {
//                 targetHdLight = GetComponent<HDAdditionalLightData>();
//                 if (targetHdLight == null)
//                 {
//                     targetHdLight = GetComponentInChildren<HDAdditionalLightData>();
//                 }
//             }

//             if (targetRenderers == null || targetRenderers.Length == 0)
//             {
//                 targetRenderers = GetComponentsInChildren<Renderer>();
//             }
//         }

//         void EnsureReceiver(bool allowCreate)
//         {
//             if (receiver == null)
//             {
//                 receiver = GetComponent<VLiveArtNetReceiver>();
//             }

//             if (receiver == null && allowCreate)
//             {
//                 receiver = gameObject.AddComponent<VLiveArtNetReceiver>();
//             }

//             if (receiver != null)
//             {
//                 ConfigureReceiver(receiver);
//             }
//         }

//         void ConfigureReceiver(VLiveArtNetReceiver targetReceiver)
//         {
//             if (forceLocalhostReceiver)
//             {
//                 targetReceiver.UseLocalhost(universe);
//             }
//             else
//             {
//                 targetReceiver._universeToUse = universe;
//             }
//         }

//         void SubscribeReceiver()
//         {
//             if (receiver == subscribedReceiver)
//             {
//                 return;
//             }

//             UnsubscribeReceiver();

//             if (receiver == null)
//             {
//                 return;
//             }

//             receiver.OnDataReceived += OnDataReceived;
//             subscribedReceiver = receiver;
//         }

//         void UnsubscribeReceiver()
//         {
//             if (subscribedReceiver == null)
//             {
//                 return;
//             }

//             subscribedReceiver.OnDataReceived -= OnDataReceived;
//             subscribedReceiver = null;
//         }

//         void ApplyDimmer()
//         {
//             var intensity = dimmer * maxLumen;

//             if (targetHdLight != null)
//             {
//                 SetLightIntensity(targetHdLight, intensity);
//                 targetHdLight.SetColor(lightColor);
//             }
//             else if (targetLight != null)
//             {
//                 targetLight.intensity = intensity;
//                 targetLight.color = lightColor;
//             }

//             ApplyRendererEmission();
//         }

//         void ApplyRendererEmission()
//         {
//             if (targetRenderers == null || targetRenderers.Length == 0)
//             {
//                 return;
//             }

//             if (materialPropertyBlock == null)
//             {
//                 materialPropertyBlock = new MaterialPropertyBlock();
//             }

//             var nits = dimmer * rendererEmissionIntensity;
//             var emissionColor = lightColor * nits;
//             emissionColor.a = nits;

//             foreach (var targetRenderer in targetRenderers)
//             {
//                 if (targetRenderer == null)
//                 {
//                     continue;
//                 }

//                 targetRenderer.GetPropertyBlock(materialPropertyBlock);
//                 materialPropertyBlock.SetColor(BaseColorId, lightColor);
//                 materialPropertyBlock.SetColor(ColorId, lightColor);
//                 materialPropertyBlock.SetColor(EmissionColorId, lightColor);
//                 materialPropertyBlock.SetColor(EmissiveColorLdrId, lightColor);
//                 materialPropertyBlock.SetColor(EmissiveColorId, emissionColor);
//                 materialPropertyBlock.SetFloat(EmissiveIntensityId, nits);
//                 materialPropertyBlock.SetFloat(EmissiveIntensityUnitId, 0f);
//                 materialPropertyBlock.SetFloat(UseEmissiveIntensityId, 1f);
//                 targetRenderer.SetPropertyBlock(materialPropertyBlock);
//             }
//         }

//         static void SetLightIntensity(HDAdditionalLightData hdLight, float intensity)
//         {
// #if UNITY_2023_3_OR_NEWER
//             var unityLight = hdLight.GetComponent<Light>();
//             if (unityLight == null)
//             {
//                 return;
//             }

//             unityLight.intensity = LightUnitUtils.ConvertIntensity(
//                 unityLight,
//                 intensity,
//                 unityLight.lightUnit,
//                 LightUnitUtils.GetNativeLightUnit(unityLight.type));
// #else
//             hdLight.SetIntensity(intensity);
// #endif
//         }
//     }
// }
