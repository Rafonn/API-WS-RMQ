using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

public class WebSocketHandler
{
    private readonly HttpContext _context;
    private readonly WebSocket _socket;
    private readonly RabbitMqService _rabbitMqService;
    private IModel? _channel;

    public WebSocketHandler(HttpContext context, WebSocket socket, RabbitMqService rabbitMqService)
    {
        _context = context;
        _socket = socket;
        _rabbitMqService = rabbitMqService;
    }

    public async Task HandleAsync()
    {
        var userId = _context.Request.Query["userId"].ToString();

        if (string.IsNullOrEmpty(userId))
        {
            Console.WriteLine("ERRO: userId está vazio. Fechando conexão.");
            await _socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "userId é obrigatório", CancellationToken.None);
            return;
        }

        try
        {
            _channel = _rabbitMqService.Connection.CreateModel();

            const string exchangeName = "bot_log_exchange";
            _channel.ExchangeDeclare(exchange: exchangeName, type: ExchangeType.Direct);

            var queueName = _channel.QueueDeclare().QueueName;

            _channel.QueueBind(queue: queueName, exchange: exchangeName, routingKey: userId);

            var consumer = new EventingBasicConsumer(_channel);

            consumer.Received += async (model, ea) =>
            {

                var body = ea.Body.ToArray();
                var jsonMessage = Encoding.UTF8.GetString(body);

                if (_socket.State == WebSocketState.Open)
                {
                    try
                    {
                        var buffer = Encoding.UTF8.GetBytes(jsonMessage);
                        await _socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                    catch (Exception sendEx)
                    {
                        Console.WriteLine($"ERRO ao enviar mensagem via WebSocket: {sendEx.Message}");
                    }
                }
                else
                {
                    Console.WriteLine($"AVISO: Socket não estava no estado 'Open'. Estado atual: {_socket.State}. Mensagem descartada.");
                }
            };

            _channel.BasicConsume(queue: queueName, autoAck: true, consumer: consumer);

            await KeepAliveUntilSocketIsClosed();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);

            if (_socket.State == WebSocketState.Open || _socket.State == WebSocketState.Connecting)
            {
                await _socket.CloseAsync(WebSocketCloseStatus.InternalServerError, "Erro interno no servidor", CancellationToken.None);
            }
        }
        finally
        {
            _channel?.Close();
            _channel?.Dispose();
        }
    }

    private async Task KeepAliveUntilSocketIsClosed()
    {
        var buffer = new byte[1024 * 4];
        WebSocketReceiveResult result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

        while (!result.CloseStatus.HasValue)
        {
            result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        }
    }
}
