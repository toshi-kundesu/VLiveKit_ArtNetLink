// VLiveKit is all Unlicense.
// unlicense: https://unlicense.org/
// this comment & namespace can be removed.
// last update: 2026/01/03

using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;

namespace toshi.VLiveKit.Lighting
{
    public class LightingCSVReader : MonoBehaviour
    {
        [Tooltip(".csvとつけなくていい＋Resourcesフォルダーの下にないと動作しない")]
        [SerializeField] private string csvName = "test";
        [SerializeField] private bool loadOnStart = false;

        [Header("Load Source")]
        [SerializeField] private bool loadFromWeb = false;
        [SerializeField] private string csvUrl = "";

        [Header("Spawn Options")]
        [SerializeField] private bool useREC = false;

        [Header("ArtNet Channels (REC)")]
        [SerializeField] private DmxChannels universe00;
        [SerializeField] private DmxChannels universe01;
        [SerializeField] private DmxChannels universe02;
        [SerializeField] private DmxChannels universe03;

        [Header("Prefabs")]
        [SerializeField] private List<PrefabMapping> prefabMappings;
        [SerializeField] private Transform parentObject;

        [Header("Assign Universe/Address To Spawned Prefab")]
        [SerializeField] private bool assignUniverseAddress = true;

        [Tooltip("例: toshi.VLiveKit.Lighting.VLiveLightFixture  または  VLiveLightFixture")]
        [SerializeField] private string assignComponentTypeName = "toshi.VLiveKit.Lighting.VLiveLightFixture";
        [Tooltip("例: universe（フィールド/プロパティ名）")]
        [SerializeField] private string universeMemberName = "universe";
        [Tooltip("例: startAdress（フィールド/プロパティ名）")]
        [SerializeField] private string addressMemberName = "startAdress";
        [Tooltip("生成したPrefabに指定コンポーネントが無ければ追加する（MonoBehaviour型のみ）")]
        [SerializeField] private bool addComponentIfMissing = false;

        [Header("Universe Parent Receiver (ArtNet)")]
        [Tooltip("Universe00.. に VLiveArtNetReceiver を自動で付けて、各灯体の Fixture.receiver に流し込む")]
        [SerializeField] private bool useUniverseParentReceiver = true;

        [Tooltip("Universe親に receiver が無い場合に追加する（通常ON推奨）")]
        [SerializeField] private bool addReceiverToUniverseParentIfMissing = true;

        [Tooltip("灯体側の receiver をこの親receiverに差し替える（useREC=false のときのみ有効）")]
        [SerializeField] private bool overrideFixtureReceiverWithUniverseParent = true;

        private Dictionary<string, GameObject> prefabDictionary;
        private Dictionary<int, Transform> universeParents; // Universeごとの親オブジェクト
        private Dictionary<int, VLiveArtNetReceiver> universeReceivers; // ★UniverseごとのReceiver

        // reflection cache (assignUniverseAddress)
        private Type _assignComponentType;
        private FieldInfo _universeField;
        private PropertyInfo _universeProp;
        private FieldInfo _addressField;
        private PropertyInfo _addressProp;

        // reflection cache (VLiveArtNetReceiver._universeToUse)
        private FieldInfo _artnetUniverseField;

        public void Start()
        {
            if (loadOnStart)
            {
                Execute();
                Debug.Log("loadOnStartがtrueなので、実行されました。");
            }
            else
            {
                Debug.Log("loadOnStartがfalseなので、実行されません。");
            }
        }

        [ContextMenu("Execute")]
        public void Execute()
        {
            // Prefab辞書
            prefabDictionary = new Dictionary<string, GameObject>();
            foreach (var mapping in prefabMappings)
            {
                if (mapping == null) continue;
                if (string.IsNullOrEmpty(mapping.prefabNameInCSV)) continue;
                if (mapping.prefab == null) continue;
                prefabDictionary[mapping.prefabNameInCSV] = mapping.prefab;
            }

            // Assign先の反射キャッシュ準備
            BuildAssignReflectionCache();

            // Universe親を作る + Universe親Receiverを準備
            universeParents = new Dictionary<int, Transform>();
            universeReceivers = new Dictionary<int, VLiveArtNetReceiver>();

            CacheArtNetUniverseField();

            for (int i = 0; i <= 8; i++)
            {
                string universeName = $"Universe0{i}";
                Transform universeParent = parentObject != null ? parentObject.Find(universeName) : null;

                if (universeParent == null)
                {
                    GameObject universeObject = new GameObject(universeName);
                    ArtNetLinkEditorBridge.RegisterCreatedObjectUndo(universeObject, "Create Universe Parent");
                    if (parentObject != null) universeObject.transform.SetParent(parentObject, false);
                    universeObject.transform.localPosition = Vector3.zero;
                    universeParent = universeObject.transform;
                }

                universeParents[i] = universeParent;

                // ★Universe親にReceiverを用意
                if (useUniverseParentReceiver && universeParent != null)
                {
                    var r = EnsureUniverseParentReceiver(universeParent.gameObject, i);
                    if (r != null) universeReceivers[i] = r;
                }
            }

            // Load
            if (loadFromWeb)
            {
                if (string.IsNullOrEmpty(csvUrl))
                {
                    Debug.LogError("CSV URL is not set.");
                    return;
                }

                StartCoroutine(LoadCSVFromWeb());
                Debug.Log("CSVをウェブからロードします。");
            }
            else
            {
                LoadCSVFromResources();
                Debug.Log("CSVをリソースからロードします。");
            }
        }

        private void BuildAssignReflectionCache()
        {
            _assignComponentType = null;
            _universeField = null;
            _universeProp = null;
            _addressField = null;
            _addressProp = null;

            if (!assignUniverseAddress) return;

            _assignComponentType = FindTypeByName(assignComponentTypeName);
            if (_assignComponentType == null)
            {
                Debug.LogWarning($"[LightingCSVReader] assignComponentTypeName '{assignComponentTypeName}' が見つかりません。Universe/Addressの代入はスキップされます。");
                return;
            }

            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            if (!string.IsNullOrEmpty(universeMemberName))
            {
                _universeField = _assignComponentType.GetField(universeMemberName, flags);
                _universeProp = (_universeField == null) ? _assignComponentType.GetProperty(universeMemberName, flags) : null;
            }

            if (!string.IsNullOrEmpty(addressMemberName))
            {
                _addressField = _assignComponentType.GetField(addressMemberName, flags);
                _addressProp = (_addressField == null) ? _assignComponentType.GetProperty(addressMemberName, flags) : null;
            }

            if (_universeField == null && (_universeProp == null || !_universeProp.CanWrite))
                Debug.LogWarning($"[LightingCSVReader] Universeの代入先 '{universeMemberName}' が見つからない/書き込み不可です（type={_assignComponentType.FullName}）。");

            if (_addressField == null && (_addressProp == null || !_addressProp.CanWrite))
                Debug.LogWarning($"[LightingCSVReader] Addressの代入先 '{addressMemberName}' が見つからない/書き込み不可です（type={_assignComponentType.FullName}）。");
        }

        private void CacheArtNetUniverseField()
        {
            _artnetUniverseField = null;
            var t = typeof(VLiveArtNetReceiver);
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            // 既存コードから receiver._universeToUse を触ってる前提
            _artnetUniverseField = t.GetField("_universeToUse", flags);
        }

        private VLiveArtNetReceiver EnsureUniverseParentReceiver(GameObject universeObj, int universe)
        {
            if (universeObj == null) return null;

            var receiver = universeObj.GetComponent<VLiveArtNetReceiver>();
            if (receiver == null && addReceiverToUniverseParentIfMissing)
            {
                receiver = ArtNetLinkEditorBridge.AddComponent<VLiveArtNetReceiver>(universeObj);
            }

            if (receiver != null)
            {
                TrySetArtNetReceiverUniverse(receiver, universe);
            }

            return receiver;
        }

        private void TrySetArtNetReceiverUniverse(VLiveArtNetReceiver receiver, int universe)
        {
            if (receiver == null) return;

            // publicなら直接でも良いけど、非publicの可能性があるので反射で安全に
            if (_artnetUniverseField != null)
            {
                ArtNetLinkEditorBridge.RecordObject(receiver, "Set ArtNet Receiver Universe");
                try { _artnetUniverseField.SetValue(receiver, universe); } catch { }
            }
            else
            {
                // 最悪ここは何もしない（receiver側が別の方法でuniverse決めてるケースもある）
            }
        }

        private void LoadCSVFromResources()
        {
            TextAsset csvFile = Resources.Load(csvName) as TextAsset;
            if (csvFile != null) ParseCSV(csvFile.text);
            else Debug.LogError($"CSVファイルがリソースから見つかりませんでした。 name={csvName}");
        }

        private IEnumerator LoadCSVFromWeb()
        {
            using (UnityWebRequest req = UnityWebRequest.Get(csvUrl))
            {
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.ProtocolError &&
                    req.result != UnityWebRequest.Result.ConnectionError)
                {
                    string csvData = req.downloadHandler.text;
                    ParseCSV(csvData);
                    SaveCSVAsTextAsset(csvData, "DownloadedCSV");
                }
                else
                {
                    Debug.LogError($"ウェブからCSVをロードする際にエラーが発生しました: {req.error}");
                }
            }
        }

        // --------------------------
        // CSV PARSE (header-based)
        // --------------------------

        private void ParseCSV(string csvData)
        {
            if (string.IsNullOrEmpty(csvData))
            {
                Debug.LogError("CSVが空です。");
                return;
            }

            using (StringReader reader = new StringReader(csvData))
            {
                int lineNumber = 0;

                // --- header ---
                string headerLine = reader.ReadLine();
                lineNumber++;

                if (string.IsNullOrEmpty(headerLine))
                {
                    Debug.LogError("CSVヘッダー行が空です。");
                    return;
                }

                var header = SplitCsvLineSimple(headerLine);
                var col = BuildColumnIndex(header);

                if (!TryResolveRequiredColumns(col, out string missing))
                {
                    Debug.LogError($"CSVの必須列が不足しています: {missing}\nHeader: {headerLine}");
                    return;
                }

                // --- rows ---
                while (reader.Peek() != -1)
                {
                    string line = reader.ReadLine();
                    lineNumber++;

                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var data = SplitCsvLineSimple(line);

                    try
                    {
                        string headNumber = GetString(data, col, "Head", "HeadNumber", "ID", "Index");
                        string name = GetString(data, col, "Name", "PrefabName", "Fixture", "Type");

                        int universe = GetInt(data, col, 0, "Universe", "Uni");
                        int address = GetInt(data, col, 0, "Address", "Addr", "StartAddress", "StartAddr");

                        Vector3 pos = Vector3.zero;
                        Vector3 rot = Vector3.zero;
                        Vector3 scl = Vector3.one;

                        // Position
                        if (HasAny(col, "X Pos", "XPos", "PosX") ||
                            HasAny(col, "Y Pos", "YPos", "PosY") ||
                            HasAny(col, "Z Pos", "ZPos", "PosZ"))
                        {
                            pos.x = GetFloat(data, col, 0f, "X Pos", "XPos", "PosX");
                            pos.y = GetFloat(data, col, 0f, "Y Pos", "YPos", "PosY");
                            pos.z = GetFloat(data, col, 0f, "Z Pos", "ZPos", "PosZ");
                        }

                        // Rotation
                        if (HasAny(col, "X Rot", "XRot", "RotX") ||
                            HasAny(col, "Y Rot", "YRot", "RotY") ||
                            HasAny(col, "Z Rot", "ZRot", "RotZ"))
                        {
                            rot.x = GetFloat(data, col, 0f, "X Rot", "XRot", "RotX");
                            rot.y = GetFloat(data, col, 0f, "Y Rot", "YRot", "RotY");
                            rot.z = GetFloat(data, col, 0f, "Z Rot", "ZRot", "RotZ");
                        }

                        // Scale
                        if (HasAny(col, "X Scl", "XScl", "SclX", "X Scale", "XScale") ||
                            HasAny(col, "Y Scl", "YScl", "SclY", "Y Scale", "YScale") ||
                            HasAny(col, "Z Scl", "ZScl", "SclZ", "Z Scale", "ZScale"))
                        {
                            scl.x = GetFloat(data, col, 1f, "X Scl", "XScl", "SclX", "X Scale", "XScale");
                            scl.y = GetFloat(data, col, 1f, "Y Scl", "YScl", "SclY", "Y Scale", "YScale");
                            scl.z = GetFloat(data, col, 1f, "Z Scl", "ZScl", "SclZ", "Z Scale", "ZScale");
                        }

                        // Prefab決定
                        if (!prefabDictionary.TryGetValue(name, out GameObject selectedPrefab) || selectedPrefab == null)
                        {
                            Debug.LogWarning($"行 {lineNumber}: 未知の名前 '{name}'。Prefabは配置されません。");
                            continue;
                        }

                        // Universe親
                        if (!universeParents.TryGetValue(universe, out Transform universeParent) || universeParent == null)
                        {
                            Debug.LogWarning($"行 {lineNumber}: Universe{universe} の親が見つかりません。Prefabは配置されません。");
                            continue;
                        }

                        // ★Prefabのまま置く（Editor非再生では prefab link を維持）
                        GameObject instance = SpawnPrefabInstance(selectedPrefab, universeParent);
                        if (instance == null)
                        {
                            Debug.LogWarning($"行 {lineNumber}: Prefabの生成に失敗しました。 name='{name}'");
                            continue;
                        }

                        // Transform反映（ローカル）
                        ArtNetLinkEditorBridge.RecordObject(instance.transform, "Apply Transform From CSV");
                        instance.transform.localPosition = pos;
                        instance.transform.localRotation = Quaternion.Euler(rot);
                        instance.transform.localScale = scl;

                        // Universe/Address の代入（選択制）
                        if (assignUniverseAddress)
                        {
                            ApplyUniverseAddress(instance, universe, address, lineNumber);
                        }

                        // ★ArtNet Receiver を Universe親から Fixture.receiver へ差し込む（RECでなければ）
                        if (useUniverseParentReceiver && overrideFixtureReceiverWithUniverseParent && !useREC)
                        {
                            ApplyUniverseParentReceiverToFixture(instance, universe, lineNumber);
                        }

                        // useREC 設定（従来通り）
                        if (useREC)
                        {
                            ApplyRECIfPossible(instance, universe, lineNumber);
                        }

                        // 名前
                        ArtNetLinkEditorBridge.RecordObject(instance, "Rename Spawned Prefab");
                        instance.name = selectedPrefab.name + "_" + headNumber;
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"行 {lineNumber}: パース中にエラー: {e.Message}\nLine: {line}");
                    }
                }
            }
        }

        // --------------------------
        // Prefab spawn (keep prefab link in editor)
        // --------------------------

        private GameObject SpawnPrefabInstance(GameObject prefab, Transform parent)
        {
            if (prefab == null) return null;

            if (!Application.isPlaying)
            {
                GameObject instance = ArtNetLinkEditorBridge.InstantiatePrefab(prefab);
                if (instance != null)
                {
                    ArtNetLinkEditorBridge.RegisterCreatedObjectUndo(instance, "Spawn Light From CSV");

                    if (parent != null)
                        instance.transform.SetParent(parent, false);

                    return instance;
                }
            }

            return Instantiate(prefab, parent);
        }

        // --------------------------
        // Universe Parent Receiver -> Fixture.receiver
        // --------------------------

        private void ApplyUniverseParentReceiverToFixture(GameObject instance, int universe, int lineNumber)
        {
            if (instance == null) return;

            if (!universeReceivers.TryGetValue(universe, out var parentReceiver) || parentReceiver == null)
            {
                Debug.LogWarning($"行 {lineNumber}: Universe{universe} の親Receiverが見つかりません。Fixture.receiver を設定できません。");
                return;
            }

            var fixture = instance.GetComponent<VLiveLightFixture>();
            if (fixture == null)
            {
                Debug.LogWarning($"行 {lineNumber}: VLiveLightFixture が見つかりません。Fixture.receiver を設定できません。");
                return;
            }

            ArtNetLinkEditorBridge.RecordObject(fixture, "Assign Universe Parent Receiver");
            fixture.receiver = parentReceiver;

            // 念のため receiver側のuniverseも合わせる
            TrySetArtNetReceiverUniverse(parentReceiver, universe);
        }

        // --------------------------
        // Assign Universe/Address (reflection)
        // --------------------------

        private void ApplyUniverseAddress(GameObject instance, int universe, int address, int lineNumber)
        {
            if (instance == null) return;
            if (_assignComponentType == null) return;

            Component comp = instance.GetComponent(_assignComponentType);

            if (comp == null && addComponentIfMissing)
            {
                if (typeof(MonoBehaviour).IsAssignableFrom(_assignComponentType))
                {
                    comp = ArtNetLinkEditorBridge.AddComponent(instance, _assignComponentType);
                }
                else
                {
                    Debug.LogWarning($"行 {lineNumber}: addComponentIfMissing=true ですが、type={_assignComponentType.FullName} はAddComponent不可です。");
                }
            }

            if (comp == null)
            {
                Debug.LogWarning($"行 {lineNumber}: 代入対象コンポーネントが見つかりません。 type={_assignComponentType.FullName}");
                return;
            }

            if (_universeField != null) TrySetIntToField(_universeField, comp, universe, lineNumber, "Universe");
            else if (_universeProp != null && _universeProp.CanWrite) TrySetIntToProperty(_universeProp, comp, universe, lineNumber, "Universe");

            if (_addressField != null) TrySetIntToField(_addressField, comp, address, lineNumber, "Address");
            else if (_addressProp != null && _addressProp.CanWrite) TrySetIntToProperty(_addressProp, comp, address, lineNumber, "Address");
        }

        private void ApplyRECIfPossible(GameObject instance, int universe, int lineNumber)
        {
            if (instance == null) return;

            VLiveLightFixture receiver = instance.GetComponent<VLiveLightFixture>();
            if (receiver == null)
            {
                Debug.LogWarning($"行 {lineNumber}: useREC=true ですが VLiveLightFixture が見つかりません。REC設定をスキップします。");
                return;
            }

            ArtNetLinkEditorBridge.RecordObject(receiver, "Apply REC Settings");

            // Fixture側のネットワーク受信は使わない
            receiver.receiver = null;

            // instance側のArtNetReceiverがもし付いてたら消す（Undo対応）
            var artnet = instance.GetComponent<VLiveArtNetReceiver>();
            if (artnet != null)
            {
                ArtNetLinkEditorBridge.DestroyObject(artnet);
            }

            receiver.useArtNetREC = true;

            switch (universe)
            {
                case 0: receiver.artNetChannels = universe00; break;
                case 1: receiver.artNetChannels = universe01; break;
                case 2: receiver.artNetChannels = universe02; break;
                case 3: receiver.artNetChannels = universe03; break;
                default:
                    Debug.LogWarning($"行 {lineNumber}: Universe{universe} はRECチャンネル割当対象外です。");
                    break;
            }
        }

        private void TrySetIntToField(FieldInfo fi, Component comp, int value, int lineNumber, string label)
        {
            try
            {
                ArtNetLinkEditorBridge.RecordObject(comp, $"Set {label}");
                object boxed = ConvertToMemberType(value, fi.FieldType);
                fi.SetValue(comp, boxed);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"行 {lineNumber}: {label} のフィールド代入に失敗: {fi.Name} (type={fi.FieldType.Name}) msg={e.Message}");
            }
        }

        private void TrySetIntToProperty(PropertyInfo pi, Component comp, int value, int lineNumber, string label)
        {
            try
            {
                ArtNetLinkEditorBridge.RecordObject(comp, $"Set {label}");
                object boxed = ConvertToMemberType(value, pi.PropertyType);
                pi.SetValue(comp, boxed);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"行 {lineNumber}: {label} のプロパティ代入に失敗: {pi.Name} (type={pi.PropertyType.Name}) msg={e.Message}");
            }
        }

        private object ConvertToMemberType(int v, Type targetType)
        {
            if (targetType == typeof(int)) return v;
            if (targetType == typeof(uint)) return (uint)Mathf.Max(0, v);
            if (targetType == typeof(short)) return (short)v;
            if (targetType == typeof(ushort)) return (ushort)Mathf.Max(0, v);
            if (targetType == typeof(byte)) return (byte)Mathf.Clamp(v, 0, 255);
            if (targetType == typeof(sbyte)) return (sbyte)Mathf.Clamp(v, -128, 127);

            if (targetType.IsEnum)
                return Enum.ToObject(targetType, v);

            return Convert.ChangeType(v, targetType, CultureInfo.InvariantCulture);
        }

        // --------------------------
        // CSV helper
        // --------------------------

        private bool HasAny(Dictionary<string, int> col, params string[] keys)
        {
            foreach (var k in keys)
                if (col.ContainsKey(Norm(k))) return true;
            return false;
        }

        private bool TryResolveRequiredColumns(Dictionary<string, int> col, out string missing)
        {
            var req = new[]
            {
                new[] { "Name", "PrefabName", "Fixture", "Type" },
                new[] { "Universe", "Uni" },
                new[] { "Address", "Addr", "StartAddress", "StartAddr" },
            };

            var miss = new List<string>();
            foreach (var group in req)
            {
                bool ok = group.Any(k => col.ContainsKey(Norm(k)));
                if (!ok) miss.Add(group[0]);
            }

            missing = string.Join(", ", miss);
            return miss.Count == 0;
        }

        private Dictionary<string, int> BuildColumnIndex(string[] headerCells)
        {
            var dict = new Dictionary<string, int>();
            for (int i = 0; i < headerCells.Length; i++)
            {
                var key = Norm(headerCells[i]);
                if (string.IsNullOrEmpty(key)) continue;
                if (!dict.ContainsKey(key)) dict.Add(key, i);
            }
            return dict;
        }

        private string Norm(string s)
        {
            if (s == null) return "";
            return s.Trim().Replace("\t", " ").ToLowerInvariant();
        }

        private string[] SplitCsvLineSimple(string line)
        {
            // Exporterがquoteを使わない前提
            return line.Split(',');
        }

        private string GetString(string[] data, Dictionary<string, int> col, params string[] keys)
        {
            foreach (var k in keys)
            {
                if (col.TryGetValue(Norm(k), out int idx))
                {
                    if (0 <= idx && idx < data.Length)
                        return data[idx].Trim();
                }
            }
            return "";
        }

        private int GetInt(string[] data, Dictionary<string, int> col, int defaultValue, params string[] keys)
        {
            foreach (var k in keys)
            {
                if (col.TryGetValue(Norm(k), out int idx))
                {
                    if (0 <= idx && idx < data.Length)
                    {
                        string s = data[idx].Trim();
                        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
                            return v;
                        if (int.TryParse(s, out v))
                            return v;
                    }
                }
            }
            return defaultValue;
        }

        private float GetFloat(string[] data, Dictionary<string, int> col, float defaultValue, params string[] keys)
        {
            foreach (var k in keys)
            {
                if (col.TryGetValue(Norm(k), out int idx))
                {
                    if (0 <= idx && idx < data.Length)
                    {
                        string s = data[idx].Trim();
                        if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                            return v;
                        if (float.TryParse(s, out v))
                            return v;
                    }
                }
            }
            return defaultValue;
        }

        private void SaveCSVAsTextAsset(string csvData, string fileName)
        {
            if (!ArtNetLinkEditorBridge.CanRefreshAssets)
            {
                Debug.Log("CSVデータをロードしました。Editorのasset pipelineがないためTextAsset保存はスキップされました。");
                return;
            }

            string path = Path.Combine(Application.dataPath, $"{fileName}.txt");
            File.WriteAllText(path, csvData);
            Debug.Log($"CSVデータが {path} に保存されました。");
            ArtNetLinkEditorBridge.RefreshAssets();
        }

        // Type解決（Unity用）
        private Type FindTypeByName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return null;

            var t = Type.GetType(typeName);
            if (t != null) return t;

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var asm in assemblies)
            {
                t = asm.GetType(typeName);
                if (t != null) return t;
            }

            string shortName = typeName.Contains(".")
                ? typeName.Substring(typeName.LastIndexOf('.') + 1)
                : typeName;

            foreach (var asm in assemblies)
            {
                try
                {
                    var types = asm.GetTypes();
                    for (int i = 0; i < types.Length; i++)
                    {
                        if (types[i].Name == shortName)
                            return types[i];
                    }
                }
                catch { }
            }

            return null;
        }
    }
}
