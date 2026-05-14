// VLiveKit is all Unlicense.
// unlicense: https://unlicense.org/

using System;
using UnityEngine;

namespace toshi.VLiveKit.Lighting
{
    public static class ArtNetLinkEditorBridge
    {
        public static Func<GameObject, GameObject> InstantiatePrefabHook { private get; set; }
        public static Action<UnityEngine.Object, string> RegisterCreatedObjectUndoHook { private get; set; }
        public static Action<UnityEngine.Object, string> RecordObjectHook { private get; set; }
        public static Func<GameObject, Type, Component> AddComponentHook { private get; set; }
        public static Action<UnityEngine.Object> DestroyObjectImmediateHook { private get; set; }
        public static Action<UnityEngine.Object, string> CreateAssetHook { private get; set; }
        public static Action RefreshAssetsHook { private get; set; }

        public static bool CanCreateAssets => CreateAssetHook != null;
        public static bool CanRefreshAssets => RefreshAssetsHook != null;

        public static GameObject InstantiatePrefab(GameObject prefab)
        {
            return InstantiatePrefabHook?.Invoke(prefab);
        }

        public static void RegisterCreatedObjectUndo(UnityEngine.Object target, string undoName)
        {
            if (Application.isPlaying || target == null) return;

            RegisterCreatedObjectUndoHook?.Invoke(target, undoName);
        }

        public static void RecordObject(UnityEngine.Object target, string undoName)
        {
            if (Application.isPlaying || target == null) return;

            RecordObjectHook?.Invoke(target, undoName);
        }

        public static T AddComponent<T>(GameObject target) where T : Component
        {
            return AddComponent(target, typeof(T)) as T;
        }

        public static Component AddComponent(GameObject target, Type componentType)
        {
            if (target == null || componentType == null) return null;

            if (!Application.isPlaying && AddComponentHook != null)
            {
                return AddComponentHook(target, componentType);
            }

            return target.AddComponent(componentType);
        }

        public static void DestroyObject(UnityEngine.Object target)
        {
            if (target == null) return;

            if (!Application.isPlaying)
            {
                if (DestroyObjectImmediateHook != null)
                {
                    DestroyObjectImmediateHook(target);
                    return;
                }

                UnityEngine.Object.DestroyImmediate(target);
                return;
            }

            UnityEngine.Object.Destroy(target);
        }

        public static bool CreateAsset(UnityEngine.Object asset, string assetPath)
        {
            if (asset == null || string.IsNullOrEmpty(assetPath) || CreateAssetHook == null)
            {
                return false;
            }

            CreateAssetHook(asset, assetPath);
            return true;
        }

        public static void RefreshAssets()
        {
            RefreshAssetsHook?.Invoke();
        }
    }
}
