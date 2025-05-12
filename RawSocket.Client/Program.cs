using System;
using System.Net;
using System.Threading.Tasks;
using RawSocket.Client;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            // Локальный IP и порт (должен быть реальный IP вашего интерфейса)
            var localIp = IPAddress.Parse("192.168.78.74"); // Замените на ваш локальный IP
            var localPort = 54321; // Произвольный незанятый порт

            // Удаленный IP и порт (например, веб-сервер)
            var remoteIp = IPAddress.Parse("192.168.78.74"); // example.com
            var remotePort = 8080; // HTTP порт

            var localEndPoint = new IPEndPoint(localIp, localPort);

            Console.WriteLine($"Попытка подключения к {remoteIp}:{remotePort}...");

            using var client = new RawTcpClient(localEndPoint);

            // Устанавливаем соединение
            var connection = await client.ConnectAsync(remoteIp, remotePort);

            Console.WriteLine("TCP-соединение установлено!");
            Console.WriteLine($"Локальная точка: {connection.LocalEndPoint}");
            Console.WriteLine($"Удаленная точка: {connection.RemoteEndPoint}");

            // Здесь можно добавить отправку/прием данных через connection
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка: {ex.Message}");
        }
    }
}