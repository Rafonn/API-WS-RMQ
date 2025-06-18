using Microsoft.OpenApi.Models;
using System.Net.WebSockets;

var builder = WebApplication.CreateBuilder(args);

// Registra CORS:
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:3000",
                "https://interface-proj-2025-49pg.vercel.app"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddControllers();
builder.Services.AddSingleton<SqlService>();
builder.Services.AddSingleton<RabbitMqService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseWebSockets();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Minha API V1");
});

app.UseCors("AllowFrontend");

app.Map("/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var ws = await context.WebSockets.AcceptWebSocketAsync();
        
        // Obtém o serviço do RabbitMQ do container de injeção de dependência
        var rabbitMqService = context.RequestServices.GetRequiredService<RabbitMqService>();
        
        // Cria o handler com a nova dependência
        var handler = new WebSocketHandler(context, ws, rabbitMqService);
        await handler.HandleAsync();
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});

app.MapControllers();
app.Run();
