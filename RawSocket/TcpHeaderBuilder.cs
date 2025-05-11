using System.Net;
using System.Net.NetworkInformation;
using PacketDotNet;

public class TcpHeaderBuilder
{
    private bool _syn;
    private bool _ack;
    private bool _fin;
    private bool _rst;
    private uint _seqNum;
    private uint _ackNum;
    private IPAddress _srcIp = IPAddress.Any;
    private int _srcPort;
    private IPAddress _destIp = IPAddress.None;
    private int _destPort;
    private byte[] _payload = [];
    private readonly PhysicalAddress _localMac;
    private PhysicalAddress _remoteMac = PhysicalAddress.Parse("FF-FF-FF-FF-FF-FF");

    public TcpHeaderBuilder(PhysicalAddress localMac)
    {
        _localMac = localMac;
    }

    public TcpHeaderBuilder WithFlags(bool syn = false, bool ack = false, bool fin = false, bool rst = false)
    {
        _syn = syn;
        _ack = ack;
        _fin = fin;
        _rst = rst;
        return this;
    }

    public TcpHeaderBuilder WithSequence(uint seq)
        => Set(ref _seqNum, seq);
    
    public TcpHeaderBuilder WithAck(uint ack)
        => Set(ref _ackNum, ack);

    public TcpHeaderBuilder From(IPAddress ip, int port) 
        => Set(ref _srcIp, ip).Set(ref _srcPort, port);

    public TcpHeaderBuilder To(IPAddress ip, int port) 
        => Set(ref _destIp, ip).Set(ref _destPort, port);

    public TcpHeaderBuilder WithPayload(byte[] payload) 
        => Set(ref _payload, payload);

    public TcpHeaderBuilder WithRemoteMac(PhysicalAddress mac)
    {
        _remoteMac = mac;
        return this;
    }

    public Packet Build()
    {
        var tcpPacket = new TcpPacket((ushort)_srcPort, (ushort)_destPort)
        {
            SequenceNumber = _seqNum,
            AcknowledgmentNumber = _ackNum,
            Synchronize = _syn,
            Acknowledgment = _ack,
            Finished = _fin,
            Reset = _rst,
            WindowSize = 8192,
            PayloadData = _payload
        };

        var ipPacket = new IPv4Packet(_srcIp, _destIp)
        {
            TimeToLive = 128,
            PayloadPacket = tcpPacket
        };
        
        tcpPacket.ParentPacket = ipPacket;

        var ethPacket = new EthernetPacket(_localMac, _remoteMac, EthernetType.IPv4)
        {
            PayloadPacket = ipPacket
        };
        
        ipPacket.ParentPacket = ethPacket;

        tcpPacket.UpdateTcpChecksum();
        ipPacket.UpdateIPChecksum();

        return ethPacket;
    }
    
    public TcpHeaderBuilder WithSynAck()
        => WithFlags(syn: true, ack: true);

    public TcpHeaderBuilder WithFinAck()
        => WithFlags(fin: true, ack: true);

    public TcpHeaderBuilder WithRst()
        => WithFlags(rst: true);

    public TcpHeaderBuilder WithSyn()
        => WithFlags(syn: true);

    public TcpHeaderBuilder WithAckOnly()
        => WithFlags(ack: true);

    private TcpHeaderBuilder Set<T>(ref T field, T value)
    {
        field = value;
        return this;
    }
}