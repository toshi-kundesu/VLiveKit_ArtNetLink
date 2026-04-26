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

        public ArtNetMessageDispatcher MessageDispatcher {
            get { return _dispatcher; }
        }

        public ArtNetServer(string host, int listenPort)
        {
            _dispatcher = new ArtNetMessageDispatcher();
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);


            _socket.ReceiveTimeout = 100;

            _socket.Bind(new IPEndPoint(IPAddress.Parse(host), listenPort));
            // memo: Anyにすると、SocketExceptionが発生する

            _thread = new Thread(ServerLoop);
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

            if (disposing)
            {
                if (_socket != null)
                {
                    _socket.Close();
                    _socket = null;
                }

                if (_thread != null)
                {
                    _thread.Join();
                    _thread = null;
                }

                _dispatcher = null;
            }
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
                    // data length
                    int dataRead = _socket.Receive(buffer);
                    if (!_disposed && dataRead > 0)
                        parser.Parse(buffer); 
                }
                catch (SocketException)
                {
                    // It might exited by timeout. Nothing to do.
                }
                catch (ThreadAbortException)
                {
                    // Abort silently.
                }
                catch (Exception e)
                {
                #if UNITY_EDITOR || UNITY_STANDALONE
                    if (!_disposed) UnityEngine.Debug.Log(e);
                #else
                    if (!_disposed) System.Console.WriteLine(e);
                #endif
                    break;
                }
            }
        }

    }
}
