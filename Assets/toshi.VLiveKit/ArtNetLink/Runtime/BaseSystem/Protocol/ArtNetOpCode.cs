// VLiveKit is all Unlicense.
// unlicense: https://unlicense.org/
// this comment & namespace can be removed.
// last update: 2024/11/25

using System;
using UnityEngine;
using toshi.VLiveKit.Lighting;

namespace toshi.VLiveKit.Lighting
{
    // とりいそぎ受信に必要なもの
    // OpDmxがDMXが入ったパケットの合図
    public enum ArtNetOpCode
    {
        // OpPoll = 0x2000,
        OpDmx = 0x5000
    }
}