// VLiveKit is all Unlicense.
// unlicense: https://unlicense.org/
// this comment & namespace can be removed.
// last update: 2024/11/25

using System;
using System.Collections.Generic;

namespace toshi.VLiveKit.Lighting
{
    public sealed class ArtNetDataHandle
    {
        // GetSharedBuffer
        public Byte[] GetSharedBuffer()
        {
            return _sharedBuffer;
        }

        // get universe
        public int GetUniverse()
        {
            return Universe;
        }

        // get opCode
        public ArtNetOpCode GetOpCode()
        {
            return OpCode;
        }

        // get sequence
        public int GetSequence()
        {
            return Sequence;
        }

        // get physical
        public int GetPhysical()
        {
            return Physical;
        }

        // get lengthHi
        public int GetLengthHi()
        {
            return LengthHi;
        }
        
        // get lengthLo
        public int GetLengthLo()
        {
            return LengthLo;
        }

        // get channels
        public byte[] GetArtNetChannels()
        {
            return Channels;
        }

        // get protocolVersionHi
        public int GetProtocolVersionHi()
        {
            return ProtocolVersionHi;
        }

        // get protocolVersionLo
        public int GetProtocolVersionLo()
        {
            return ProtocolVersionLo;
        }

        // check isArtNet
        public bool IsArtNet()
        {
            // check opCode
            if (OpCode == ArtNetOpCode.OpDmx)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        internal void Scan(Byte[] buf)
        {
            _sharedBuffer = buf;

            var buffer = new byte[530];
            buffer = buf;
            Buffer.BlockCopy(buf, 0, buffer, 0, buf.Length);
            OpCode = (ArtNetOpCode)BitConverter.ToInt16(new byte[2] { buffer[8], buffer[9] }, 0);
            ProtocolVersionHi = buffer[10];
            ProtocolVersionLo = buffer[11];
            Sequence = buffer[12];
            Physical = buffer[13];
            Universe = BitConverter.ToInt16(new byte[2] { buffer[14], buffer[15] }, 0);
            LengthHi = buffer[16];
            LengthLo = buffer[17];
            Channels = new byte[512];
            for (int i = 18; i < buffer.Length; i++)
            {
                Channels[i - 18] = buffer[i];
            }            
        }

        Byte[] _sharedBuffer;
        public ArtNetOpCode OpCode { get; private set; }
        int Sequence, Physical, Universe;
        byte[] Channels;
        int ProtocolVersionHi, ProtocolVersionLo, LengthHi, LengthLo;
    }
}
