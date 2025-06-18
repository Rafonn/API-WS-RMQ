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

    // POST /logs/toggle
    [HttpPost("toggle")]
    public async Task<IActionResult> PostToggle([FromBody] ToggleDto dto)
    {
        if (dto.Toggle == null || string.IsNullOrWhiteSpace(dto.UserId))
            return BadRequest("Campos 'toggle' e 'userId' são obrigatórios.");

        var sql = @"
            IF EXISTS (SELECT 1 FROM andritzButton_logs WHERE userId = @userId)
                UPDATE andritzButton_logs
                SET buttonState = @toggle,
                    updated_at = @updatedAt
                WHERE userId = @userId;
            ELSE
                INSERT INTO andritzButton_logs (userId, buttonState, updated_at)
                VALUES (@userId, @toggle, @updatedAt);";

        await _db.ExecuteAsync(sql, new[]
        {
            new SqlParameter("@userId", dto.UserId),
            new SqlParameter("@toggle", dto.Toggle.Value),
            new SqlParameter("@updatedAt", DateTimeOffset.UtcNow)
        });

        return Ok(new { message = "Toggle salvo ou atualizado com sucesso." });
    }

    // GET /logs/toggle/{userId}
    [HttpGet("toggle/{userId}")]
    public async Task<IActionResult> GetToggle(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest("Parâmetro 'userId' é obrigatório.");

        var sql = @"
            SELECT TOP 1 buttonState
            FROM andritzButton_logs
            WHERE userId = @userId
            ORDER BY updated_at DESC";

        var registro = await _db.QuerySingleAsync<object>(
            sql,
            new[]
            {
                new SqlParameter("@userId", System.Data.SqlDbType.NVarChar, 50) { Value = userId }
            },
            reader =>
            {
                return reader.IsDBNull(0) ? null! : reader.GetValue(0)!;
            }
        );

        if (registro == null)
            return NotFound(new { message = "Nenhum toggle encontrado para este usuário." });

        bool buttonState;

        switch (registro)
        {
            case bool b:
                buttonState = b;
                break;
            case int i:
                buttonState = (i != 0);
                break;
            case long l:
                buttonState = (l != 0L);
                break;
            case string s:
                if (bool.TryParse(s, out var parsedBool))
                {
                    buttonState = parsedBool;
                }
                else if (int.TryParse(s, out var parsedInt))
                {
                    buttonState = (parsedInt != 0);
                }
                else
                {
                    buttonState = false;
                }
                break;
            default:
                // Convertendo via ChangeType
                try
                {
                    buttonState = Convert.ToBoolean(registro);
                }
                catch
                {
                    buttonState = false;
                }
                break;
        }

        return Ok(new { button = buttonState });
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
