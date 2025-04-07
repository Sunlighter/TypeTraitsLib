using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Sunlighter.OptionLib;

namespace Sunlighter.TypeTraitsLib.Networking
{
    public static class SocketExtensions
    {
        public static Task<Socket> AcceptAsync(this Socket s)
        {
            TaskCompletionSource<Socket> k = new TaskCompletionSource<Socket>();
            AsyncCallback a = delegate (IAsyncResult result)
            {
                try
                {
                    Socket sc = s.EndAccept(result);
                    k.SetResult(sc);
                }
                catch (Exception exc)
                {
                    k.SetException(exc);
                }
            };
            try
            {
                s.BeginAccept(a, null);
            }
            catch (Exception exc)
            {
                k.SetException(exc);
            }
            return k.Task;
        }

#if NETSTANDARD2_0
        public static async Task<Socket> AcceptAsync(this Socket s, CancellationToken cToken)
        {
            using (CancellationTokenRegistration ctr = cToken.Register(() => s.Close()))
            {
                return await s.AcceptAsync();
            }
        }
#endif

        public static Task ConnectAsync(this Socket s, EndPoint remoteEndPoint)
        {
            TaskCompletionSource<bool> k = new TaskCompletionSource<bool>();
            AsyncCallback a = delegate (IAsyncResult result)
            {
                try
                {
                    s.EndConnect(result);
                    k.SetResult(true);
                }
                catch (Exception exc)
                {
                    k.SetException(exc);
                }
            };
            try
            {
                s.BeginConnect(remoteEndPoint, a, null);
            }
            catch (Exception exc)
            {
                k.SetException(exc);
            }
            return k.Task;
        }

        public static async Task ConnectAsync(this Socket s, EndPoint remoteEndPoint, CancellationToken cToken)
        {
            using (CancellationTokenRegistration ctr = cToken.Register(() => s.Close()))
            {
                await s.ConnectAsync(remoteEndPoint);
            }
        }

        public static Task<int> ReceiveAsync(this Socket s, byte[] buffer, int offset, int size, SocketFlags flags = SocketFlags.None)
        {
            TaskCompletionSource<int> k = new TaskCompletionSource<int>();
            AsyncCallback a = delegate (IAsyncResult result)
            {
                try
                {
                    int count = s.EndReceive(result);
                    k.SetResult(count);
                }
                catch (Exception exc)
                {
                    k.SetException(exc);
                }
            };
            try
            {
                s.BeginReceive(buffer, offset, size, flags, a, null);
            }
            catch (Exception exc)
            {
                k.SetException(exc);
            }
            return k.Task;
        }

        public static async Task<int> ReceiveAsync(this Socket s, byte[] buffer, int offset, int size, SocketFlags flags, CancellationToken cToken)
        {
            using (CancellationTokenRegistration ctr = cToken.Register(() => s.Close()))
            {
                return await s.ReceiveAsync(buffer, offset, size, flags);
            }
        }

        public static Task<int> SendAsync(this Socket s, byte[] buffer, int offset, int size, SocketFlags flags = SocketFlags.None)
        {
            TaskCompletionSource<int> k = new TaskCompletionSource<int>();
            AsyncCallback a = delegate (IAsyncResult result)
            {
                try
                {
                    int count = s.EndSend(result);
                    k.SetResult(count);
                }
                catch (Exception exc)
                {
                    k.SetException(exc);
                }
            };
            try
            {
                s.BeginSend(buffer, offset, size, flags, a, null);
            }
            catch (Exception exc)
            {
                k.SetException(exc);
            }
            return k.Task;
        }

        public static async Task<int> SendAsync(this Socket s, byte[] buffer, int offset, int size, SocketFlags flags, CancellationToken cToken)
        {
            using (CancellationTokenRegistration ctr = cToken.Register(() => s.Close()))
            {
                return await s.SendAsync(buffer, offset, size, flags);
            }
        }

        public static async Task<int> ReceiveFullyAsync(this Socket s, byte[] buffer, int offset, int size, SocketFlags flags = SocketFlags.None)
        {
            int totalBytesRead = 0;
            while (size > 0)
            {
                int bytesRead = await s.ReceiveAsync(buffer, offset, size, flags);
                if (bytesRead == 0) break;
                size -= bytesRead;
                offset += bytesRead;
                totalBytesRead += bytesRead;
            }
            return totalBytesRead;
        }

        public static async Task<int> ReceiveFullyAsync(this Socket s, byte[] buffer, int offset, int size, SocketFlags flags, CancellationToken cToken)
        {
            using (CancellationTokenRegistration ctr = cToken.Register(() => s.Close()))
            {
                return await s.ReceiveFullyAsync(buffer, offset, size, flags);
            }
        }

        public static async Task<int> SendFullyAsync(this Socket s, byte[] buffer, int offset, int size, SocketFlags flags = SocketFlags.None)
        {
            int totalBytesWritten = 0;
            while (size > 0)
            {
                int bytesWritten = await s.SendAsync(buffer, offset, size, flags);
                size -= bytesWritten;
                offset += bytesWritten;
                totalBytesWritten += bytesWritten;
            }
            return totalBytesWritten;
        }

        public static async Task<int> SendFullyAsync(this Socket s, byte[] buffer, int offset, int size, SocketFlags flags, CancellationToken cToken)
        {
            using (CancellationTokenRegistration ctr = cToken.Register(() => s.Close()))
            {
                return await s.SendFullyAsync(buffer, offset, size, flags);
            }
        }

        public static async Task SendBlock(this Socket s, byte[] packet)
        {
            int len = packet.Length;
            byte[] lenBytes = BitConverter.GetBytes(len);
            await s.SendFullyAsync(lenBytes, 0, 4);
            await s.SendFullyAsync(packet, 0, len);
        }

        public static async Task SendBlock(this Socket s, byte[] packet, CancellationToken cToken)
        {
            int len = packet.Length;
            byte[] lenBytes = BitConverter.GetBytes(len);
            await s.SendFullyAsync(lenBytes, 0, 4, SocketFlags.None, cToken);
            await s.SendFullyAsync(packet, 0, len, SocketFlags.None, cToken);
        }

        public static async Task<Option<byte[]>> ReceiveBlock(this Socket s)
        {
            byte[] lenBytes = new byte[4];
            int lenBytesReceived = await s.ReceiveFullyAsync(lenBytes, 0, 4);
            if (lenBytesReceived == 0)
            {
                return Option<byte[]>.None;
            }
            else
            {
                int len = BitConverter.ToInt32(lenBytes, 0);
                byte[] packet = new byte[len];
                await s.ReceiveFullyAsync(packet, 0, len);
                return Option<byte[]>.Some(packet);
            }
        }

        public static async Task<Option<byte[]>> ReceiveBlock(this Socket s, CancellationToken cToken)
        {
            byte[] lenBytes = new byte[4];
            int lenBytesReceived = await s.ReceiveFullyAsync(lenBytes, 0, 4, SocketFlags.None, cToken);
            if (lenBytesReceived == 0)
            {
                return Option<byte[]>.None;
            }
            else
            {
                int len = BitConverter.ToInt32(lenBytes, 0);
                byte[] packet = new byte[len];
                await s.ReceiveFullyAsync(packet, 0, len, SocketFlags.None, cToken);
                return Option<byte[]>.Some(packet);
            }
        }

        public static async Task SendAsync<T>(this ITypeTraits<T> typeTraits, Socket s, T value)
        {
            byte[] block = typeTraits.SerializeToBytes(value);
            await s.SendBlock(block);
        }

        public static async Task SendAsync<T>(this ITypeTraits<T> typeTraits, Socket s, T value, CancellationToken cToken)
        {
            byte[] block = typeTraits.SerializeToBytes(value);
            await s.SendBlock(block, cToken);
        }

        public static async Task<Option<T>> ReceiveAsync<T>(this ITypeTraits<T> typeTraits, Socket s)
        {
            Option<byte[]> block = await s.ReceiveBlock();
            if (block.HasValue)
                return Option<T>.Some(typeTraits.DeserializeFromBytes(block.Value));
            else
                return Option<T>.None;
        }

        public static async Task<Option<T>> ReceiveAsync<T>(this ITypeTraits<T> typeTraits, Socket s,  CancellationToken cToken)
        {
            Option<byte[]> block = await s.ReceiveBlock(cToken);
            if (block.HasValue)
                return Option<T>.Some(typeTraits.DeserializeFromBytes(block.Value));
            else
                return Option<T>.None;
        }
    }
}
