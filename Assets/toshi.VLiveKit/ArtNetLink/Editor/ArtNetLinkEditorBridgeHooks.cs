// VLiveKit is all Unlicense.
// unlicense: https://unlicense.org/

using UnityEditor;
using UnityEngine;

namespace toshi.VLiveKit.Lighting
{
    [InitializeOnLoad]
    public static class ArtNetLinkEditorBridgeHooks
    {
        static ArtNetLinkEditorBridgeHooks()
        {
            ArtNetLinkEditorBridge.InstantiatePrefabHook = prefab => PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            ArtNetLinkEditorBridge.RegisterCreatedObjectUndoHook = (target, undoName) => Undo.RegisterCreatedObjectUndo(target, undoName);
            ArtNetLinkEditorBridge.RecordObjectHook = (target, undoName) => Undo.RecordObject(target, undoName);
            ArtNetLinkEditorBridge.AddComponentHook = (target, componentType) => Undo.AddComponent(target, componentType);
            ArtNetLinkEditorBridge.DestroyObjectImmediateHook = target => Undo.DestroyObjectImmediate(target);
            ArtNetLinkEditorBridge.CreateAssetHook = (asset, assetPath) => AssetDatabase.CreateAsset(asset, assetPath);
            ArtNetLinkEditorBridge.RefreshAssetsHook = () => AssetDatabase.Refresh();
        }
    }
}
