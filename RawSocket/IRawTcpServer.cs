using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawSocket.Common
{
    public interface IRawTcpServer : IDisposable
    {
        void Start();
        void Stop();
        IRawTcpConnection AcceptRawTcpConnection();
        Task<IRawTcpConnection> AcceptRawTcpConnectionAsync(CancellationToken
        cancellationToken = default);
    }
}
