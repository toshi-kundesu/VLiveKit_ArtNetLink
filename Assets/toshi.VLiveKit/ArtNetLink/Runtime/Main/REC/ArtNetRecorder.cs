
// VLiveKit is all Unlicense.
// unlicense: https://unlicense.org/
// this comment & namespace can be removed.
// last update: 2024/11/28

using System.IO;
using System.Linq;
using UnityEngine;

namespace toshi.VLiveKit.Lighting
{
    public class ArtNetRecorder : MonoBehaviour
    {
        #region serialize field

        [SerializeField] private VLiveArtNetReceiver artNetClient;

        [SerializeField] private string directoryPath = "Record";

        [SerializeField] private string clipName = "NewArtNetClip";
        [SerializeField] private KeyCode recordStartKey = KeyCode.R;
        [SerializeField] private KeyCode recordStopKey = KeyCode.S;
        #endregion

        #region private field

        private AnimationCurve[] _curves;
        private float _time;
        private const int ChannelCount = 512;
        private bool _isRecoding;

        #endregion

        #region private Method

        private void Update()
        {
            if (_isRecoding) _time += Time.deltaTime;

            if (Input.GetKeyDown(recordStartKey)) RecordStart();
            if (Input.GetKeyDown(recordStopKey)) RecordStop();
        }

        private void RecordStart()
        {
            if (artNetClient == null)
            {
                Debug.LogWarning("ArtNetRecorder requires a VLiveArtNetReceiver before recording.");
                return;
            }

            _curves = new AnimationCurve[ChannelCount];
            for (int i = 0; i < ChannelCount; i++)
                _curves[i] = new AnimationCurve();

            _time = 0f;
            _isRecoding = true;
            artNetClient.OnDataReceived += RecordingEventHandler;
        }


        private void RecordingEventHandler(byte[] data)
        {
            var artnetData = new ArtNetDataHandle();
            artnetData.Scan(data);
            if (!artnetData.IsArtNet())
            {
                return;
            }
            if (artnetData.OpCode != ArtNetOpCode.OpDmx)
            {
                return;
            }
            if (data.Length < 18)
            {
                return;
            }

            for (int i = 18; i < ChannelCount + 18 && i < data.Length; i++)
            {
                if (_curves[i - 18].keys.Length > 2)
                {
                    var secondLast = _curves[i - 18].keys.Length - 2;
                    var last = _curves[i - 18].keys.Length - 1;
                    var secondLastKey = _curves[i - 18].keys[secondLast];
                    var lastKey = _curves[i - 18].keys[last];

                    if (secondLastKey.value == lastKey.value && lastKey.value == data[i])
                    {
                        _curves[i - 18].RemoveKey(secondLast);
                    }
                }

                var key = new Keyframe(_time, data[i]);
                _curves[i - 18].AddKey(key); 
            }

            Debug.Log($"RecordingTime:{this._time}");
        }

        public void OnApplicationQuit()
        {
            this.RecordStop();
        }

        private void RecordStop()
        {
            if (!_isRecoding) return;

            AnimationClip clip = new AnimationClip();
            for (int i = 0; i < _curves.Length; i++) clip.SetCurve("", typeof(DmxChannels), $"Ch{i + 1}", _curves[i]);
            artNetClient.OnDataReceived -= RecordingEventHandler;
            _curves = null;
            _isRecoding = false;
            
            if (!ArtNetLinkEditorBridge.CanCreateAssets)
            {
                Debug.Log("ArtNet recording finished, but creating an AnimationClip asset is only available through the editor asset pipeline.");
                return;
            }

            var path = $"{Application.dataPath}/{directoryPath}";
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            var p = $"Assets/{directoryPath}/{clipName}.asset";
            while (File.Exists(p)) p = p.Split('.').First() + "_1.asset";

            if (!ArtNetLinkEditorBridge.CreateAsset(clip, p))
            {
                Debug.Log("ArtNet recording finished, but creating an AnimationClip asset is only available through the editor asset pipeline.");
                return;
            }

            ArtNetLinkEditorBridge.RefreshAssets();
            Debug.Log("Record Finish");
        }

        #endregion
    }
}
