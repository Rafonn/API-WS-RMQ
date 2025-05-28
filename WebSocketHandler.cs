using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;

public class WebSocketHandler
{
    private readonly WebSocket _socket;
    private readonly HttpContext _context;
    private readonly SqlService _db;

    public WebSocketHandler(HttpContext context, WebSocket socket, SqlService db)
    {
        _context = context;
        _socket = socket;
        _db = db;
    }

    public async Task HandleAsync()
    {
        var query = _context.Request.Query;
        var userId = query["userId"].ToString();
        var lastTimestamp = query["lastTimestamp"].ToString();

        var lastTs = string.IsNullOrEmpty(lastTimestamp)
            ? DateTime.UtcNow
            : DateTime.Parse(lastTimestamp);

        while (_socket.State == WebSocketState.Open)
        {
            var result = await FetchNewMessageAsync(userId, lastTs);
            if (result != null)
            {
                string botMessage = result.Value.botMessage;
                DateTime botTimeStamp = result.Value.botTimeStamp;

                lastTs = botTimeStamp;

                var json =
                    $"{{\"botMessage\":\"{EscapeForJson(botMessage)}\",\"botTimeStamp\":\"{botTimeStamp:o}\"}}";

                var buffer = Encoding.UTF8.GetBytes(json);
                await _socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
            }

            await Task.Delay(1000);
        }
    }

    /// <summary>
    /// Busca, no banco, a próxima linha em bot_logs para userId cujo botTimeStamp seja maior que lastTs.
    /// </summary>
    private async Task<(string botMessage, DateTime botTimeStamp)?> FetchNewMessageAsync(string userId, DateTime lastTs)
    {
        var sql = @"
            SELECT TOP 1 botMessage, botTimeStamp
            FROM bot_logs
            WHERE userId = @userId
              AND botTimeStamp > @lastTs
            ORDER BY botTimeStamp DESC";

        var parameters = new[]
        {
            new SqlParameter("@userId", System.Data.SqlDbType.NVarChar, 50) { Value = userId },
            new SqlParameter("@lastTs", System.Data.SqlDbType.DateTime2) { Value = lastTs }
        };

        var registro = await _db.QuerySingleAsync<(string botMessage, DateTime botTimeStamp)>(
            sql,
            parameters,
            reader =>
            {
                string mensagem = reader.GetString(0);
                DateTime timestamp = reader.GetDateTime(1);
                return (mensagem, timestamp);
            });

        return registro;
    }

    /// <summary>
    /// Escapa caracteres especiais para JSON simples (aspas, barras, etc.).
    /// </summary>
    private static string EscapeForJson(string? s)
    {
        if (string.IsNullOrEmpty(s))
            return "";

        return s
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\b", "\\b")
            .Replace("\f", "\\f")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }
}