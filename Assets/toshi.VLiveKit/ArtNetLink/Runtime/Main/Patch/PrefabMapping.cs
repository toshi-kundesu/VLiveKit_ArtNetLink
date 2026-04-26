using UnityEngine;

namespace toshi.VLiveKit.Lighting
{
    [CreateAssetMenu(fileName = "PrefabMapping", menuName = "ScriptableObjects/PrefabMapping", order = 1)]
    public class PrefabMapping : ScriptableObject
    {
        public string prefabNameInCSV;
        public GameObject prefab;
    }
}