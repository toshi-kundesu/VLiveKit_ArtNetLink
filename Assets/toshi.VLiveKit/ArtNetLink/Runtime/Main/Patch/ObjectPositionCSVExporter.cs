// VLiveKit is all Unlicense.
// unlicense: https://unlicense.org/
// this comment & namespace can be removed. you can use this code freely.

using System.IO;
using UnityEngine;

namespace toshi.VLiveKit.Lighting
{
    public class ObjectPositionCSVExporter : MonoBehaviour
    {
        [SerializeField] private string csvFileName = "ObjectPositions.csv";
        [SerializeField] private GameObject[] movingLights; // ムービングライトのオブジェクト配列
        [SerializeField] private GameObject[] parLights; // パーライトのオブジェクト配列

        [ContextMenu("ExportPositionsToCSV")]
        public void ExportPositionsToCSV()
        {
            string filePath = Path.Combine(Application.dataPath, csvFileName);
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                // ヘッダー行を書き込む
                writer.WriteLine("Head,Name,Category,Universe,Address,X Pos,Y Pos,Z Pos,X Rot,Y Rot,Z Rot");

                int headNumber = 1; // Head番号を初期化

                // ムービングライトをエクスポート
                foreach (var obj in movingLights)
                {
                    if (obj != null)
                    {
                        Vector3 position = obj.transform.position;
                        // CSV行を書き込む
                        writer.WriteLine($"{headNumber},moving_light,Moving,0,0,{position.x},{position.y},{position.z},0,0,0");
                        headNumber++;
                    }
                }

                // パーライトをエクスポート
                foreach (var obj in parLights)
                {
                    if (obj != null)
                    {
                        Vector3 position = obj.transform.position;
                        // CSV行を書き込む
                        writer.WriteLine($"{headNumber},parlight,Par,0,0,{position.x},{position.y},{position.z},0,0,0");
                        headNumber++;
                    }
                }
            }
            // 更新
            ArtNetLinkEditorBridge.RefreshAssets();

            Debug.Log($"オブジェクトの位置情報が {filePath} に保存されました。");
        }
    }
}
