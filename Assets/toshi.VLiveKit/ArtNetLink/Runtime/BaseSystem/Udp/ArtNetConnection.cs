// VLiveKit is all Unlicense.
// unlicense: https://unlicense.org/
// this comment & namespace can be removed.
// last update: 2024/11/28

using UnityEngine;

namespace toshi.VLiveKit.Lighting
{
    public enum ArtNetConnectionType { Udp }

    [CreateAssetMenu(fileName = "ArtNetConnection",
                     menuName = "toshi.VLiveKit.Lighting/ArtNet Link/Connection")]
    public sealed class ArtNetConnection : ScriptableObject
    {
        public ArtNetConnectionType type = ArtNetConnectionType.Udp;
        public string host = "127.0.0.1";
        public int port = 6454;
    }
} // namespace toshi.VLiveKit.Lighting
