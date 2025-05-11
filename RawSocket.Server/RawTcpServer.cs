using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

using RawSocket.Common;
using System.Collections.Concurrent;
using RawSocket.Server;

namespace SimpleTcpSniffer
{
    public class RawTcpSniffer : IRawTcpServer
    {
        private LibPcapLiveDevice _device;
        private IPEndPoint _endPoint; // хранит ip и port
        public IPEndPoint endPoint
        {
            get
            {
                return _endPoint;
            }
            set
            {
                _endPoint = value;
            }
        }

        private Thread _captureThread;
        private bool _isCapturing = true; // Флаг для отслеживания состояния сниффера

        private BlockingCollection<IRawTcpConnection> _acceptedConnections = new();
        private ConcurrentDictionary<IPEndPoint, IRawTcpConnection> _activeConnections = new();
        private readonly ConcurrentDictionary<IPEndPoint, HandshakeState> _handshakeStates = new();

        private void SendPacket(byte[] bytes) => _device.SendPacket(bytes);
        public IRawTcpConnection AcceptRawTcpConnection()
        {
            return _acceptedConnections.Take();
        }
        public async Task<IRawTcpConnection> AcceptRawTcpConnectionAsync(CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => _acceptedConnections.Take(cancellationToken), cancellationToken);
        }
        public RawTcpSniffer(IPEndPoint localEndPoint)
        {
            if (!_isCapturing) // запущен ли сниффер
            {
                throw new InvalidOperationException("Сниффер уже запущен.");
            }

            endPoint = localEndPoint; // ip и port
            var devices = LibPcapLiveDeviceList.Instance; // все устройства сети
            _device = devices.FirstOrDefault(d => d.Interface.Addresses.Any(a =>
            a.Addr.ipAddress?.ToString() ==
            localEndPoint.Address.ToString())); // находим устройство с указанным ip и port
            if (_device == null)
                throw new Exception("Сетевое устройство не найдено");
        }
        public void Start()
        {
            _device.Open(DeviceModes.Promiscuous, 1000);

            _device.Filter = "tcp dst port 80";
            _device.OnPacketArrival += HandlePacket;

            // создаем поток с выводом
            _captureThread = new Thread(() => _device.StartCapture());
            _captureThread.Start();
        }
        public void Stop()
        {
            if (!_isCapturing) // запущен ли сниффер
            {
                throw new InvalidOperationException("Сниффер не запущен.");
            }

            _device.StopCapture();
            _captureThread?.Join();
            _device.Close();
            _isCapturing = false; // Сбрасываем флаг, что сниффер остановлен
        }
        public void Dispose()
        {
            Stop();
            _device.OnPacketArrival -= HandlePacket;
        }
        private void HandlePacket(object sender, PacketCapture packetCapture)
        {
            var rawPacket = packetCapture.GetPacket();
            var packet = Packet.ParsePacket(rawPacket.LinkLayerType, rawPacket.Data);
            var ipPacket = packet.Extract<IPv4Packet>();
            var tcpPacket = packet.Extract<TcpPacket>();

            if (ipPacket == null || tcpPacket == null)
                return;

            var remoteEndPoint = new IPEndPoint(ipPacket.SourceAddress, tcpPacket.SourcePort);
            var localEndPoint = new IPEndPoint(ipPacket.DestinationAddress, tcpPacket.DestinationPort);

            if (!localEndPoint.Address.Equals(_endPoint.Address))
                return;

            if (tcpPacket.Synchronize && !tcpPacket.Acknowledgment)
            {
                HandleSynPacket(remoteEndPoint, tcpPacket);
            }
            else if (tcpPacket.Acknowledgment && !tcpPacket.Synchronize)
            {
                HandleFinalAck(remoteEndPoint, tcpPacket);
            }
            else if (tcpPacket.PayloadData != null && tcpPacket.PayloadData.Length > 0)
            {
                HandlePayloadPacket(remoteEndPoint, tcpPacket);
            }
        }

        private void HandleSynPacket(IPEndPoint remoteEndPoint, TcpPacket tcpPacket)
        {
            var serverSeq = (uint)new Random().Next(0, int.MaxValue);

            _handshakeStates[remoteEndPoint] = new HandshakeState
            {
                ClientSeq = tcpPacket.SequenceNumber,
                ServerSeq = serverSeq,
                Timestamp = DateTime.Now
            };

            var synAckPacket = new TcpHeaderBuilder(_device.Interface.MacAddress)
                .From(_endPoint.Address, _endPoint.Port)
                .To(remoteEndPoint.Address, remoteEndPoint.Port)
                .WithSynAck()
                .WithSequence(serverSeq)
                .WithAck(tcpPacket.SequenceNumber + 1)
                .Build();

            SendPacket(synAckPacket.Bytes);
        }

        private void HandleFinalAck(IPEndPoint remoteEndPoint, TcpPacket tcpPacket)
        {
            if (!_handshakeStates.TryGetValue(remoteEndPoint, out var handshakeState))
                return;

            if (tcpPacket.AcknowledgmentNumber != handshakeState.ServerSeq + 1)
                return;

            var connection = new RawTcpConnection(
                localEndPoint: new IPEndPoint(_endPoint.Address, _endPoint.Port),
                remoteEndPoint: remoteEndPoint,
                sendAction: SendPacket);

            _activeConnections.TryAdd(remoteEndPoint, connection);

            _acceptedConnections.Add(connection);

            _handshakeStates.TryRemove(remoteEndPoint, out _);
        }

        private void HandlePayloadPacket(IPEndPoint remoteEndPoint, TcpPacket tcpPacket)
        {
            if (_activeConnections.TryGetValue(remoteEndPoint, out var connection))
            {
                connection.EnqueueReceivedData(tcpPacket.PayloadData);

                var ackPacket = new TcpHeaderBuilder(_device.Interface.MacAddress)
                    .From(_endPoint.Address, _endPoint.Port)
                    .To(remoteEndPoint.Address, remoteEndPoint.Port)
                    .WithAckOnly()
                    .WithSequence(tcpPacket.AcknowledgmentNumber)
                    .WithAck(tcpPacket.SequenceNumber + (uint)tcpPacket.PayloadData.Length)
                    .Build();

                SendPacket(ackPacket.Bytes);
            }
        }
    }
}