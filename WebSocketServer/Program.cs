using System.Net;
using System.Net.WebSockets;
using System.Text;

/**
 * Esempio di un server WebSocket in C# con .NET 8
 * Il server si occupa di ricevere messaggi da un client e inviarli in broadcast a tutti gli altri client connessi
 */

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:6969");

var app = builder.Build();

/*
var webSocketOptions = new WebSocketOptions()
{
    //KeepAliveInterval = TimeSpan.FromSeconds(120), //Tempo di attesa tra due ping consecutivi
    //ReceiveBufferSize = 4 * 1024 //Dimensione del buffer di ricezione
    //AllowedOrigins = { "http://localhost:5000" } //Origini consentite
};
*/

//app.UseWebSockets(webSocketOptions); //Aggiungo il middleware per gestire le richieste WebSocket

app.UseWebSockets(); //Aggiungo il middleware per gestire le richieste WebSocket

var connections = new List<WebSocket>();

app.Map("/ws", async context => {
    if (context.WebSockets.IsWebSocketRequest)
    {
        var curName = context.Request.Query["name"];

        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        connections.Add(webSocket);        

        await Broadcast($"User [{curName}] joined the chat");
        await Broadcast($"Users online: {connections.Count}");

        await ReceiveMessage(webSocket, async (result, buffer) =>
        {
            if (result.MessageType == WebSocketMessageType.Text)
            {
                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                await Broadcast($"[{curName}]: {message}");
            }
            else if (result.MessageType == WebSocketMessageType.Close || webSocket.State == WebSocketState.Aborted)
            {
                connections.Remove(webSocket);
                await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
                await Broadcast($"User [{curName}] left the chat");
                await Broadcast($"Users online: {connections.Count}");
            }
        });
    }
    else
    {
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
    }
});

async Task ReceiveMessage(WebSocket webSocket, Action<WebSocketReceiveResult, byte[]> handleMessage)
{
    var buffer = new byte[1024 * 4];
    while (webSocket.State == WebSocketState.Open)
    {
        var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        handleMessage(result, buffer);
    }
}

async Task Broadcast(string message)
{
    var bytes = Encoding.UTF8.GetBytes(message);
    foreach (var connection in connections)
    {
        var arraySegment = new ArraySegment<byte>(bytes, 0, bytes.Length);
        if (connection.State == WebSocketState.Open)
        {
            await connection.SendAsync(arraySegment, WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}

await app.RunAsync();