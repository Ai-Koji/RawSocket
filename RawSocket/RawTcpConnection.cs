using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace RawSocket.Common
{
    public class RawTcpConnection : IRawTcpConnection
    {
        private readonly BlockingCollection<byte[]> _receiveQueue = new();
        private readonly Action<byte[]> _sendAction;
        private readonly IPEndPoint _localEndPoint;
        private readonly IPEndPoint _remoteEndPoint;
        private RawTcpState _state = RawTcpState.Connected;
        private bool _disposed;

        public IPEndPoint LocalEndPoint => _localEndPoint;
        public IPEndPoint RemoteEndPoint => _remoteEndPoint;
        public RawTcpState State => _state;

        public RawTcpConnection(
            IPEndPoint localEndPoint,
            IPEndPoint remoteEndPoint,
            Action<byte[]> sendAction)
        {
            _localEndPoint = localEndPoint ?? throw new ArgumentNullException(nameof(localEndPoint));
            _remoteEndPoint = remoteEndPoint ?? throw new ArgumentNullException(nameof(remoteEndPoint));
            _sendAction = sendAction ?? throw new ArgumentNullException(nameof(sendAction));
        }

        public void Send(byte[] data)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(RawTcpConnection));
            if (_state != RawTcpState.Connected) throw new InvalidOperationException("Connection is not active");

            _sendAction(data);
        }

        public byte[] Receive()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(RawTcpConnection));
            if (_state != RawTcpState.Connected) throw new InvalidOperationException("Connection is not active");

            return _receiveQueue.Take();
        }

        public async Task<byte[]> ReceiveAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(RawTcpConnection));
            if (_state != RawTcpState.Connected) throw new InvalidOperationException("Connection is not active");

            return await Task.Run(() => _receiveQueue.Take(cancellationToken), cancellationToken);
        }

        public void Close()
        {
            if (_state == RawTcpState.Closed) return;

            _state = RawTcpState.Closed;
            _receiveQueue.CompleteAdding();
        }

        public void EnqueueReceivedData(byte[] data)
        {
            if (!_receiveQueue.IsAddingCompleted)
            {
                _receiveQueue.Add(data);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            Close();
            _receiveQueue.Dispose();
            _disposed = true;
        }
    }
}
