using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace tetsapi.Controllers;
public class Consumer
{
    private ushort id;
    public ushort ID
    {
        get { return id; }
        set { id = value; }
    }
    private volatile bool keeprun = true;
    private readonly object padlock = new object();
    private IConnection _connection;
    private IModel _channel;
    private ManualResetEvent _resetEvent = new ManualResetEvent(false);

    public event EventHandler<ReceiveEventArgs> OnReceiveEvent;

    public class ReceiveEventArgs : EventArgs
    {
        public ushort id { get; set; }
        public string data { get; set; }
    }

    private void CallResponse(string response)
    {
        //if (client == null) return;            
        var handler = OnReceiveEvent;
        if (handler != null)
            handler(this, new ReceiveEventArgs() { id = id, data = response });
    }

    public void Send(string message)
    {
        var factory = new ConnectionFactory { Uri = new Uri("amqp://admin:maam2013@192.168.1.58:5672") };
        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        _channel.QueueDeclare(queue: "hello2",
                             durable: false,
                             exclusive: false,
                             autoDelete: false,
                             arguments: null);
        var body = Encoding.UTF8.GetBytes(message);

        _channel.BasicPublish(exchange: string.Empty,
                     routingKey: "hello2",
                     basicProperties: null,
                     body: body);
        Console.WriteLine($" [x] Sent {message}");

    }

    public void Run()
    {
        var factory = new ConnectionFactory { Uri = new Uri("amqp://admin:maam2013@192.168.1.58:5672") };
        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        _channel.QueueDeclare(queue: "hello",
                             durable: false,
                             exclusive: false,
                             autoDelete: false,
                             arguments: null);

        Console.WriteLine(" [*] Waiting for messages.");

        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += (model, ea) =>
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    Console.WriteLine($"{message}");
                    CallResponse(message);
                };
        _channel.BasicConsume(queue: "hello",
                             autoAck: true,
                             consumer: consumer);

        keeprun = true;
    // Wait for the reset event and clean up when it triggers
    // _resetEvent.WaitOne();
    loop:
        if (keeprun)
        {
            lock (padlock)
            {
                Monitor.Wait(padlock, TimeSpan.FromSeconds(10));
            }
            goto loop;
        }
    }

    public void Close()
    {
        keeprun = false;
        lock (padlock)
        {
            Monitor.Pulse(padlock);
        }
        _channel?.Close();
        _channel = null;
        _connection?.Close();
        _connection = null;
        Console.WriteLine("Close consumer");
    }
}