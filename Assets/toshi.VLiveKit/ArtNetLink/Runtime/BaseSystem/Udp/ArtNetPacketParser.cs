// VLiveKit is all Unlicense.
// unlicense: https://unlicense.org/
// this comment & namespace can be removed.
// last update: 2024/11/28

using System;
using System.Collections.Generic;
using UnityEngine;

namespace toshi.VLiveKit.Lighting
{
    /// <summary>
    /// Converts raw UDP Art-Net packet bytes into an ArtNetDataHandle before handing it to the lighting message flow.
    /// </summary>
    internal sealed class ArtNetPacketParser
    {
        public ArtNetPacketParser(ArtNetMessageDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public void Parse(Byte[] buffer)
        {
            ScanMessage(buffer);
        }

        ArtNetMessageDispatcher _dispatcher;
        ArtNetDataHandle _dataHandle = new ArtNetDataHandle();

        void ScanMessage(Byte[] buffer)
        {
            _dataHandle.Scan(buffer);
            // コールバックの整理
            _dispatcher.Dispatch(_dataHandle);
        }
    }
}
