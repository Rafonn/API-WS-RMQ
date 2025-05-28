using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System;

[ApiController]
[Route("logs")]
public class LogsController : ControllerBase
{
    private readonly SqlService _db;

    public LogsController(SqlService db) => _db = db;

    // POST /logs/user
    [HttpPost("user")]
    public async Task<IActionResult> PostUserLog([FromBody] LogDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Log) || string.IsNullOrWhiteSpace(dto.UserId))
            return BadRequest("Campos 'log' e 'userId' são obrigatórios.");

        var sql = @"
            INSERT INTO user_logs (userMessage, userId, userTimeStamp)
            VALUES (@log, @userId, @timestamp)";
        await _db.ExecuteAsync(sql, new[]
        {
            new SqlParameter("@log", dto.Log),
            new SqlParameter("@userId", dto.UserId),
            new SqlParameter("@timestamp", DateTimeOffset.UtcNow)
        });

        return Ok(new { message = "Log do usuário inserido com sucesso." });
    }

    // GET /logs/bot/{userId}
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
            new[]
            {
                new SqlParameter("@userId", userId)
            },
            reader => reader.GetString(0)
        );

        return log != null
            ? Ok(new { lastLog = log })
            : NotFound(new { message = "Nenhum log do bot encontrado para este usuário." });
    }

    // GET /logs/user/{userId}
    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetLastUserLog(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest("Parâmetro 'userId' é obrigatório.");

        var sql = @"
            SELECT TOP 1 userMessage
            FROM user_logs
            WHERE userId = @userId
            ORDER BY userTimeStamp DESC";

        var log = await _db.QuerySingleAsync(
            sql,
            new[]
            {
                new SqlParameter("@userId", userId)
            },
            reader => reader.GetString(0)
        );

        return log != null
            ? Ok(new { lastLog = log })
            : NotFound(new { message = "Nenhum log do usuário encontrado para este usuário." });
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

// DTO para POST /logs/user
public class LogDto
{
    public string? Log { get; set; }
    public string? UserId { get; set; }
}

// DTO para POST /logs/toggle
public class ToggleDto
{
    public bool? Toggle { get; set; }
    public string? UserId { get; set; }
}