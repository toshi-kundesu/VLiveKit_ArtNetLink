// VLiveKit is all Unlicense.
// unlicense: https://unlicense.org/
// this comment & namespace can be removed.
// last update: 2024/11/28

using System;
using System.Collections.Generic;

namespace toshi.VLiveKit.Lighting
{
    public sealed class ArtNetMessageDispatcher
    {
        public delegate void MessageCallback(ArtNetDataHandle data);

        private MessageCallback _singleCallback;

        public void AddCallback(MessageCallback callback)
        {
            lock (this)
            {
                _singleCallback += callback;
            }
        }

        public void RemoveCallback(MessageCallback callback)
        {
            lock (this)
            {
                _singleCallback -= callback;
            }
        }

        internal void Dispatch(ArtNetDataHandle data)
        {
            lock (_singleCallback)
            {
                _singleCallback(data);
            }
        }
    }
}
