using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using RawSocket.Common;
using System.Net.NetworkInformation;

namespace RawSocket.Client;
public class RawTcpClient : IDisposable
{
    private readonly LibPcapLiveDevice _device;
    private readonly IPAddress _localIp;
    private readonly PhysicalAddress _localMac;
    private IRawTcpConnection? _connection;
    private Thread? _captureThread;
    private TaskCompletionSource<IRawTcpConnection>? _handshakeTcs;

    private uint _localSeq;
    private uint _remoteSeq;

    public IPEndPoint LocalEndPoint { get; }
    public IPEndPoint RemoteEndPoint { get; private set; } = null!;

    public RawTcpClient(IPEndPoint localEndPoint)
    {
        LocalEndPoint = localEndPoint;
        _localIp = localEndPoint.Address;

        _localMac = GetMacAddress(_localIp)
            ?? throw new Exception("MAC-адрес не найден для указанного локального IP.");

        _device = LibPcapLiveDeviceList.Instance
            .FirstOrDefault(d => d.Interface.Addresses.Any(a =>
                Equals(a.Addr?.ipAddress, _localIp)))
            ?? throw new Exception("Сетевое устройство не найдено для указанного локального IP.");
    }

    public IRawTcpConnection Connect(IPAddress remoteIp, int remotePort)
    {
        return ConnectAsync(remoteIp, remotePort).GetAwaiter().GetResult();
    }

    public async Task<IRawTcpConnection> ConnectAsync(IPAddress remoteIp, int remotePort, CancellationToken cancellationToken = default)
    {
        RemoteEndPoint = new IPEndPoint(remoteIp, remotePort);
        _handshakeTcs = new TaskCompletionSource<IRawTcpConnection>();

        _device.Open(DeviceModes.Promiscuous, 1000);
        _device.Filter = $"tcp and src host {remoteIp} and dst host {_localIp}";
        _device.OnPacketArrival += HandlePacket;

        _localSeq = (uint)Random.Shared.Next(1000, 5000);

        var synPacket = CreateBuilder()
            .WithSyn()
            .WithSequence(_localSeq)
            .Build();

        SendPacket(synPacket.Bytes);

        _captureThread = new Thread(() => _device.StartCapture());
        _captureThread.Start();

        using var reg = cancellationToken.Register(() =>
            _handshakeTcs.TrySetCanceled(cancellationToken));

        return await _handshakeTcs.Task.ConfigureAwait(false);
    }

    private void HandlePacket(object sender, PacketCapture packetCapture)
    {
        var packet = packetCapture.GetPacket();
        var ipPacket = packet.GetPacket().Extract<IPv4Packet>();
        var tcpPacket = packet.GetPacket().Extract<TcpPacket>();

        if (ipPacket is null || tcpPacket is null)
            return;

        if (tcpPacket.Synchronize && tcpPacket.Acknowledgment &&
            tcpPacket.DestinationPort == LocalEndPoint.Port &&
            tcpPacket.SourcePort == RemoteEndPoint.Port)
        {
            _remoteSeq = tcpPacket.SequenceNumber;

            var ackPacket = CreateBuilder()
                .WithAckOnly()
                .WithSequence(_localSeq + 1)
                .WithAck(_remoteSeq + 1)
                .Build();

            SendPacket(ackPacket.Bytes);

            _connection = new RawTcpConnection(
                localEndPoint: LocalEndPoint,
                remoteEndPoint: RemoteEndPoint,
                sendAction: SendPacket
            );

            _handshakeTcs?.TrySetResult(_connection);
        }
    }

    private TcpHeaderBuilder CreateBuilder() =>
        new TcpHeaderBuilder(_localMac)
            .From(LocalEndPoint.Address, LocalEndPoint.Port)
            .To(RemoteEndPoint.Address, RemoteEndPoint.Port);

    private void SendPacket(byte[] data) =>
        _device.SendPacket(data);

    private PhysicalAddress? GetMacAddress(IPAddress ip) =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(nic => nic.GetIPProperties().UnicastAddresses.Any(a => a.Address.Equals(ip)))
            .Select(nic => nic.GetPhysicalAddress())
            .FirstOrDefault();

    public void Dispose()
    {
        if (_device.Started)
        {
            _device.StopCapture();
            _device.Close();
        }

        _captureThread?.Join();
    }
}