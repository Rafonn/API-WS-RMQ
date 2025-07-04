# Real-Time Q&A API

This project is a .NET API designed for real-time question and answer interactions, supporting multiple users simultaneously. It leverages RabbitMQ for robust, scalable message handling and WebSockets for real-time communication.

## Features

- **Real-time Q&A:** Users can interact with the chatbot and receive instant responses.
- **Multi-user Support:** Handles multiple simultaneous connections efficiently.
- **RabbitMQ Integration:** Decouples message processing for scalability and reliability.
- **WebSocket Communication:** Enables low-latency, bidirectional communication.

## Technologies Used

- ASP.NET Core
- RabbitMQ ([`RabbitMqService`](Services/RabbitMqService.cs))
- WebSockets
- SQL Server

## Getting Started

1. **Clone the repository:**
   ```sh
   git clone https://github.com/yourusername/your-repo.git
   cd your-repo
   ```

2. **Configure RabbitMQ:**
   - Set your RabbitMQ connection details in `appsettings.json`:
     ```json
     "RabbitMq": {
       "HostName": "localhost",
       "UserName": "guest",
       "Password": "guest"
     }
     ```

3. **Build and run the API:**
   ```sh
   dotnet build
   dotnet run
   ```

4. **Test the WebSocket endpoint:**
   - Connect using a WebSocket client to interact with the chatbot in real time.

## Project Structure

- [`Services/RabbitMqService.cs`](Services/RabbitMqService.cs): Handles RabbitMQ connections.
- `WebSocketHandler.cs`: Manages WebSocket connections and messaging.
- `Controllers/`: API controllers for various endpoints.
