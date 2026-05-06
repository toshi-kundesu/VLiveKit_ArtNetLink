// VLiveKit is all Unlicense.
// unlicense: https://unlicense.org/
// this comment & namespace can be removed.
// last update: 2024/11/28

#if UNITY_EDITOR
#define ARTNET_SERVER_LIST
#endif

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

#if ARTNET_SERVER_LIST
using System.Collections.Generic;
using System.Collections.ObjectModel;
#endif

namespace toshi.VLiveKit.Lighting
{
    public sealed class ArtNetServer : IDisposable
    {
        const int ReceiveTimeoutMilliseconds = 100;
        const int DisposeJoinTimeoutMilliseconds = 500;

        public ArtNetMessageDispatcher MessageDispatcher
        {
            get { return _dispatcher; }
        }

        public ArtNetServer(string host, int listenPort)
        {
            _dispatcher = new ArtNetMessageDispatcher();
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _socket.ReceiveTimeout = ReceiveTimeoutMilliseconds;
            _socket.Bind(new IPEndPoint(IPAddress.Parse(host), listenPort));

            _thread = new Thread(ServerLoop);
            _thread.IsBackground = true;
            _thread.Start();

        #if ARTNET_SERVER_LIST
            _servers.Add(this);
        #endif
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);

        #if ARTNET_SERVER_LIST
            if (_servers != null) _servers.Remove(this);
        #endif
        }

        void Dispose(bool disposing)
        {
            if (_disposed) return;
            _disposed = true;

            if (!disposing) return;

            if (_socket != null)
            {
                _socket.Close();
                _socket = null;
            }

            if (_thread != null)
            {
                if (!_thread.Join(DisposeJoinTimeoutMilliseconds))
                {
                #if UNITY_EDITOR || UNITY_STANDALONE
                    UnityEngine.Debug.LogWarning("ArtNetServer receive thread did not stop within timeout.");
                #endif
                }

                _thread = null;
            }

            _dispatcher = null;
        }

        ~ArtNetServer()
        {
            Dispose(false);
        }

    #if ARTNET_SERVER_LIST

        static List<ArtNetServer> _servers = new List<ArtNetServer>(8);
        static ReadOnlyCollection<ArtNetServer> _serversReadOnly;

        internal static ReadOnlyCollection<ArtNetServer> ServerList
        {
            get
            {
                if (_serversReadOnly == null)
                    _serversReadOnly = new ReadOnlyCollection<ArtNetServer>(_servers);
                return _serversReadOnly;
            }
        }

    #endif

        ArtNetMessageDispatcher _dispatcher;
        Socket _socket;
        Thread _thread;
        bool _disposed;

        void ServerLoop()
        {
            var parser = new ArtNetPacketParser(_dispatcher);
            var buffer = new byte[530];

            while (!_disposed)
            {
                try
                {
                    var socket = _socket;
                    if (socket == null) break;

                    int dataRead = socket.Receive(buffer);
                    if (!_disposed && dataRead > 0)
                        parser.Parse(buffer);
                }
                catch (SocketException e)
                {
                    if (_disposed) break;
                    if (e.SocketErrorCode == SocketError.TimedOut) continue;
                    if (e.SocketErrorCode == SocketError.Interrupted) break;
                    if (e.SocketErrorCode == SocketError.NotSocket) break;

                #if UNITY_EDITOR || UNITY_STANDALONE
                    UnityEngine.Debug.Log(e);
                #else
                    Console.WriteLine(e);
                #endif
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (ThreadAbortException)
                {
                    break;
                }
                catch (Exception e)
                {
                #if UNITY_EDITOR || UNITY_STANDALONE
                    if (!_disposed) UnityEngine.Debug.Log(e);
                #else
                    if (!_disposed) Console.WriteLine(e);
                #endif
                    break;
                }
            }
        }
    }
}
