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
        // обрабатывает каждый пакет
        private void HandlePacket(object sender, PacketCapture packetCapture)
        {
            var rawPacket = packetCapture.GetPacket();
            var packet = rawPacket.GetPacket();
            var ipPacket = packet.Extract<IPv4Packet>();
            var tcpPacket = packet.Extract<TcpPacket>();

            Console.WriteLine($"{rawPacket} {packet} {ipPacket} {tcpPacket}");

            if (ipPacket != null && tcpPacket != null)
            {
                // Извлечение необходимых данных
                var ipSource = ipPacket.SourceAddress.ToString();
                var portSource = tcpPacket.SourcePort;
                var ipDest = ipPacket.DestinationAddress.ToString();
                var portDest = tcpPacket.DestinationPort;
                var sequenceNumber = tcpPacket.SequenceNumber;
                var flags = tcpPacket.Flags;

                // Вывод данных
                Console.WriteLine($"IP Source: {ipSource}");
                Console.WriteLine($"Port Source: {portSource}");
                Console.WriteLine($"IP Destination: {ipDest}");
                Console.WriteLine($"Port Destination: {portDest}");
                Console.WriteLine($"Sequence Number: {sequenceNumber}");
                Console.WriteLine($"Flags: {flags}");
                Console.WriteLine();
            }
        }
    }
}