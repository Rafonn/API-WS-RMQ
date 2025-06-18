using RabbitMQ.Client;
using System;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client.Exceptions;

public class RabbitMqService : IDisposable
{
    public IConnection Connection { get; private set; }

    public RabbitMqService(IConfiguration configuration)
    {
        var factory = new ConnectionFactory()
        {
            HostName = configuration["RabbitMq:HostName"],
            UserName = configuration["RabbitMq:UserName"],
            Password = configuration["RabbitMq:Password"],
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
        };

        try
        {
            Connection = factory.CreateConnection();
        }
        catch (BrokerUnreachableException ex)
        {
            Console.WriteLine($"--> Erro ao conectar ao RabbitMQ: {ex.Message}");

            throw;
        }
    }

    public void Dispose()
    {
        if (Connection?.IsOpen ?? false)
        {
            Connection.Close();
        }
    }
}