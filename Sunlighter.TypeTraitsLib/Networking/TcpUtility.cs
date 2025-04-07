using Sunlighter.OptionLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sunlighter.TypeTraitsLib.Networking
{
    public interface IObjectStream<TRead, TWrite> : IDisposable
    {
        Task<Option<TRead>> ReadObjectOrEof();

        Task Write(TWrite value);
        void WriteEof();
    }

    public class SocketObjectStream<TRead, TWrite> : IObjectStream<TRead, TWrite>
    {
        private readonly Socket s;
        private ITypeTraits<TRead> readTraits;
        private ITypeTraits<TWrite> writeTraits;

        public SocketObjectStream
        (
            Socket s,
            ITypeTraits<TRead> readTraits,
            ITypeTraits<TWrite> writeTraits
        )
        {
            this.s = s;
            this.readTraits = readTraits;
            this.writeTraits = writeTraits;
        }

        public Task<Option<TRead>> ReadObjectOrEof()
        {
            return readTraits.ReceiveAsync(s);
        }

        public async Task Write(TWrite value)
        {
            await writeTraits.SendAsync(s, value);
        }

        public void WriteEof()
        {
            s.Shutdown(SocketShutdown.Send);
        }

        public void Dispose()
        {
            s.Dispose();
        }
    }

    public static class TcpUtility
    {
        public static async Task RunServer<TRequest, TResponse>
        (
            IPEndPoint localEndPoint,
            ITypeTraits<TRequest> requestTraits,
            ITypeTraits<TResponse> responseTraits,
            Func<IPEndPoint, IObjectStream<TRequest, TResponse>, CancellationToken, Task> handlePeer,
            CancellationToken cToken
        )
        {
            using (Socket s = new Socket(localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
            {
                s.Bind(localEndPoint);
                s.Listen(1);
                while(!cToken.IsCancellationRequested)
                {
                    try
                    {
                        Socket peer = await s.AcceptAsync(cToken);
                        _ = Task.Run
                        (
                            async () =>
                            {
                                if (peer.RemoteEndPoint is IPEndPoint remoteEndPoint)
                                {
                                    SocketObjectStream<TRequest, TResponse> ss = new SocketObjectStream<TRequest, TResponse>(peer, requestTraits, responseTraits);
                                    await handlePeer(remoteEndPoint, ss, cToken);
                                }
                                else
                                {
                                    peer.Dispose();
                                }
                            },
                            cToken
                        );
                    }
                    catch(OperationCanceledException)
                    {
                        // ignore for a clean shutdown
                    }
                }
            }
        }

        public static async Task RunClient<TRequest, TResponse>
        (
            IPEndPoint remoteEndPoint,
            ITypeTraits<TRequest> requestTraits,
            ITypeTraits<TResponse> responseTraits,
            Func<IObjectStream<TResponse, TRequest>, Task> handlePeer
        )
        {
            using (Socket s = new Socket(remoteEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
            {
                await s.ConnectAsync(remoteEndPoint);
                await handlePeer(new SocketObjectStream<TResponse, TRequest>(s, responseTraits, requestTraits));
            }
        }
    }
}
