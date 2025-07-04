using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System;
using System.Threading.Tasks;
using RabbitMQ.Client;
using System.Text.Json;

[ApiController]
[Route("logs")]
public class LogsController : ControllerBase
{
    private readonly SqlService _db;
    private readonly RabbitMqService _rabbitMqService;

    public LogsController(SqlService db, RabbitMqService rabbitMqService)
    {
        _db = db;
        _rabbitMqService = rabbitMqService;
    }

    // POST /logs/user
    [HttpPost("user")]
    public async Task<IActionResult> PostUserLog([FromBody] LogDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Log) || string.IsNullOrWhiteSpace(dto.UserId))
            return BadRequest("Campos 'log' e 'userId' são obrigatórios.");

        var userMessageTimestamp = DateTime.Now;
        var userSql = @"
            INSERT INTO user_logs (userMessage, userId, userTimeStamp)
            VALUES (@log, @userId, @timestamp)";
        await _db.ExecuteAsync(userSql, new[]
        {
            new SqlParameter("@log", dto.Log),
            new SqlParameter("@userId", dto.UserId),
            new SqlParameter("@timestamp", userMessageTimestamp)
        });

        _ = Task.Run(() => WaitForAndPublishBotResponse(dto.UserId, userMessageTimestamp));

        return Ok(new { message = "Log do usuário recebido. Aguardando resposta do bot em segundo plano." });
    }

    private async Task WaitForAndPublishBotResponse(string userId, DateTime userTimestamp)
    {
        const int maxWaitSeconds = 60;
        string? botResponse = null;

        for (int i = 0; i < maxWaitSeconds; i++)
        {
            await Task.Delay(1000);

            var botSql = @"
                SELECT TOP 1 botMessage
                FROM bot_logs
                WHERE userId = @userId AND botTimeStamp > @userTimestamp
                ORDER BY botTimeStamp DESC";

            var foundResponse = await _db.QuerySingleAsync(
                botSql,
                new[] {
                    new SqlParameter("@userId", userId),
                    new SqlParameter("@userTimestamp", userTimestamp)
                },
                reader => reader.GetString(0)
            );

            if (foundResponse != null)
            {
                botResponse = foundResponse;
                break;
            }
        }

        if (botResponse != null)
        {
            using (var channel = _rabbitMqService.Connection.CreateModel())
            {
                const string exchangeName = "bot_log_exchange";
                channel.ExchangeDeclare(exchangeName, ExchangeType.Direct);

                var payload = new { lastLog = botResponse };
                var jsonPayload = JsonSerializer.Serialize(payload);
                var body = System.Text.Encoding.UTF8.GetBytes(jsonPayload);

                channel.BasicPublish(
                    exchange: exchangeName,
                    routingKey: userId,
                    basicProperties: null,
                    body: body
                );
            }
        }
        else
        {
            Console.WriteLine($"[BACKGROUND TASK] Tempo de espera esgotado. Nenhuma resposta do bot encontrada para '{userId}'.");
        }
    }

    [HttpGet("bot/{userId}")]
    public async Task<IActionResult> GetLastBotLog(string userId)
    {
        var sql = @"
            SELECT TOP 1 botMessage
            FROM bot_logs
            WHERE userId = @userId
            ORDER BY botTimeStamp DESC";

        var log = await _db.QuerySingleAsync(
            sql,
            new[] { new SqlParameter("@userId", userId) },
            reader => reader.GetString(0)
        );

        return log != null
            ? Ok(new { lastLog = log })
            : NotFound(new { message = "Nenhum log do bot encontrado para este usuário." });
    }
}

public class LogDto
{
    public string? Log { get; set; }
    public string? UserId { get; set; }
}
public class ToggleDto
{
    public bool? Toggle { get; set; }
    public string? UserId { get; set; }
}
