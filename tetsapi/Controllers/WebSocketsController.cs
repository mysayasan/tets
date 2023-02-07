using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace tetsapi.Controllers;

[ApiController]
[Route("[controller]")]
public class WebSocketsController : ControllerBase
{
    private readonly ILogger<WebSocketsController> _logger;
    private static List<Thread> threads = new List<Thread>();

    public WebSocketsController(ILogger<WebSocketsController> logger)
    {
        _logger = logger;
    }

    [HttpGet("/ws")]
    public async Task Get()
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
            _logger.Log(LogLevel.Information, "WebSocket connection established");
            await Echo(webSocket);
        }
        else
        {
            HttpContext.Response.StatusCode = 400;
        }
    }

    private async Task Echo(WebSocket webSocket)
    {
        var buffer = new byte[1024 * 4];
        var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        _logger.Log(LogLevel.Information, $"Message received from Client >>");
        Consumer consumer = new Consumer() { ID = 1 };

        Thread oldthread = threads.FirstOrDefault(i => i.Name == consumer.ID.ToString());
        if (oldthread != null)
        {
            threads.Remove(oldthread);
        }

        // consumer.OnReceiveEvent += new EventHandler<Consumer.ReceiveEventArgs>(consumer_OnReceiveEvent);
        consumer.OnReceiveEvent += (sender, e) =>
        {
            var consumerMsg = Encoding.UTF8.GetBytes($"{e.data}");
            webSocket.SendAsync(new ArraySegment<byte>(consumerMsg, 0, consumerMsg.Length), result.MessageType, result.EndOfMessage, CancellationToken.None);

            Console.WriteLine(string.Format("[{0}] {1} >> {2}", e.id, e.data));
        };


        try
        {
            Thread thread = new Thread(new ThreadStart(() => consumer.Run())) { Name = consumer.ID.ToString(), IsBackground = true };
            thread.Start();
            threads.Add(thread);
            Thread.Sleep(100);
        }
        catch (ThreadStateException ex)
        {
            Console.WriteLine(ex.Message);
        }

        while (!result.CloseStatus.HasValue)
        {
            var serverMsg = Encoding.UTF8.GetBytes($"Server: Hello. You said: {Encoding.UTF8.GetString(buffer)}");
            var newMsg = buffer.TakeWhile((v, index) => buffer.Skip(index).Any(w => w != 0x00)).ToArray();

            _logger.Log(LogLevel.Information, Encoding.UTF8.GetString(newMsg));
            await webSocket.SendAsync(new ArraySegment<byte>(newMsg, 0, newMsg.Length), result.MessageType, result.EndOfMessage, CancellationToken.None);
            _logger.Log(LogLevel.Information, "Message sent to Client");

            result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            var rcvmsg = buffer.TakeWhile((v, index) => buffer.Skip(index).Any(w => w != 0x00)).ToArray();
            string rcvStr = Encoding.UTF8.GetString(rcvmsg);
            if (rcvStr == "1") {
                consumer.Send("on");
            }
            else if (rcvStr == "9") {
                consumer.Send("oi");
            }
            else {
                consumer.Send("of");
            }
            _logger.Log(LogLevel.Information, $"Message received from Client >> {rcvStr}");

        }
        await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
        _logger.Log(LogLevel.Information, "WebSocket connection closed");
        consumer.Close();
    }

    private void consumer_OnReceiveEvent(object sender, Consumer.ReceiveEventArgs e)
    {
        Console.WriteLine(string.Format("[{0}] {1} >> {2}", e.id, e.data));
    }
}
