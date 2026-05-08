// VLiveKit is all Unlicense.
// unlicense: https://unlicense.org/
// this comment & namespace can be removed.
// last update: 2026/05/08

using System;
using System.Net;
using System.Net.Sockets;

namespace toshi.VLiveKit.Lighting
{
    public enum ArtNetSendTargetMode
    {
        Unicast,
        Broadcast
    }

    public sealed class ArtNetDmxSender : IDisposable
    {
        public const int DefaultPort = 6454;
        public const int ChannelCount = 512;

        const int HeaderSize = 18;
        const int MinDmxLength = 2;
        const int MaxUniverse = 32767;

        readonly UdpClient _udpClient = new UdpClient();

        int _sequence = 1;
        bool _disposed;

        public void SendDmx(string address, int port, int universe, byte[] channels, ArtNetSendTargetMode targetMode)
        {
            if (string.IsNullOrWhiteSpace(address))
                throw new ArgumentException("Address is empty.", nameof(address));

            if (!IPAddress.TryParse(address, out var ipAddress))
                throw new ArgumentException("Address must be an IPv4 address.", nameof(address));

            if (ipAddress.AddressFamily != AddressFamily.InterNetwork)
                throw new ArgumentException("Address must be an IPv4 address.", nameof(address));

            if (port < IPEndPoint.MinPort || port > IPEndPoint.MaxPort)
                throw new ArgumentOutOfRangeException(nameof(port), "Port is out of range.");

            SendDmx(new IPEndPoint(ipAddress, port), universe, channels, targetMode);
        }

        public void SendDmx(IPEndPoint endpoint, int universe, byte[] channels, ArtNetSendTargetMode targetMode)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ArtNetDmxSender));

            if (endpoint == null)
                throw new ArgumentNullException(nameof(endpoint));

            if (endpoint.Port < IPEndPoint.MinPort || endpoint.Port > IPEndPoint.MaxPort)
                throw new ArgumentOutOfRangeException(nameof(endpoint), "Port is out of range.");

            if (universe < 0 || universe > MaxUniverse)
                throw new ArgumentOutOfRangeException(nameof(universe), "Universe must be between 0 and 32767.");

            var packet = BuildDmxPacket(universe, channels, NextSequence());
            _udpClient.EnableBroadcast = targetMode == ArtNetSendTargetMode.Broadcast;
            _udpClient.Send(packet, packet.Length, endpoint);
        }

        public static byte[] BuildDmxPacket(int universe, byte[] channels, byte sequence = 0, byte physical = 0)
        {
            if (universe < 0 || universe > MaxUniverse)
                throw new ArgumentOutOfRangeException(nameof(universe), "Universe must be between 0 and 32767.");

            var dmxLength = channels == null ? MinDmxLength : Math.Min(channels.Length, ChannelCount);
            dmxLength = Math.Max(dmxLength, MinDmxLength);
            if ((dmxLength & 1) == 1) dmxLength++;

            var packet = new byte[HeaderSize + dmxLength];

            packet[0] = (byte)'A';
            packet[1] = (byte)'r';
            packet[2] = (byte)'t';
            packet[3] = (byte)'-';
            packet[4] = (byte)'N';
            packet[5] = (byte)'e';
            packet[6] = (byte)'t';
            packet[7] = 0x00;

            packet[8] = 0x00;
            packet[9] = 0x50;

            packet[10] = 0x00;
            packet[11] = 0x0e;
            packet[12] = sequence;
            packet[13] = physical;

            packet[14] = (byte)(universe & 0xff);
            packet[15] = (byte)((universe >> 8) & 0xff);

            packet[16] = (byte)((dmxLength >> 8) & 0xff);
            packet[17] = (byte)(dmxLength & 0xff);

            if (channels != null)
                Buffer.BlockCopy(channels, 0, packet, HeaderSize, Math.Min(channels.Length, dmxLength));

            return packet;
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            _udpClient.Close();
        }

        byte NextSequence()
        {
            var current = (byte)_sequence;
            _sequence++;
            if (_sequence > 255) _sequence = 1;
            return current;
        }
    }
}
