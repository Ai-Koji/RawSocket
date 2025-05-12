using System.Net;
using System.Text;
using SharpPcap;
using SharpPcap.LibPcap;
using RawSocket.Common;
using SimpleTcpSniffer;

namespace RawSocket.Server;

class Program
{
    static void Main(string[] args)
    {
        string ip = "192.168.78.74";
        int port = 8080; 
        IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Parse(ip), port: port);
        IRawTcpServer server = new RawTcpServer(localEndPoint);
        server.Start();
        while (true)
        {
            var connection = server.AcceptRawTcpConnection();
            Task.Run(() => HandleClient(connection));
        }
    }
    static void HandleClient(IRawTcpConnection connection)
    {
        Console.WriteLine($"Соединение установлено с {connection.RemoteEndPoint}");
        connection.Send(Encoding.UTF8.GetBytes("Hello from RawTcpServer!"));

        while (connection.State == RawTcpState.Connected)
        {
            var data = connection.Receive();
            if (data.Length == 0) break;
            var message = Encoding.UTF8.GetString(data);
            Console.WriteLine(message);
            Console.WriteLine($"Получено сообщение от {connection.RemoteEndPoint}: {message}");
            connection.Send(Encoding.UTF8.GetBytes($"Echo: {message}"));
        }
        Console.WriteLine($"Соединение закрыто с {connection.RemoteEndPoint}");
        connection.Close();
    }
}