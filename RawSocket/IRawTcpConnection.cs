using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace RawSocket.Common
{
    public interface IRawTcpConnection : IDisposable
    {
        IPEndPoint LocalEndPoint { get; }
        IPEndPoint RemoteEndPoint { get; }
        RawTcpState State { get; }
        void Send(byte[] data);
        byte[] Receive();
        Task<byte[]> ReceiveAsync(CancellationToken cancellationToken = default);
        void Close();
        void EnqueueReceivedData(byte[] data);
    }
}
