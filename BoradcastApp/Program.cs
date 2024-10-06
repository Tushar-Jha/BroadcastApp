using System.Net;
using System.Net.WebSockets;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:6969");
var app = builder.Build();

var connections = new List<WebSocket>();

app.UseWebSockets();
app.Map("/ws", async context => {
    if (context.WebSockets.IsWebSocketRequest)
    {
        var curName = context.Request.Query["name"];
        using var ws = await context.WebSockets.AcceptWebSocketAsync();
        connections.Add(ws);

        await Broadcast($"{curName} joined the room.");
        await Broadcast($"{connections.Count} users connected.");
        await ReceiveMessage(ws, 
            async(result, buffer) =>
            {
                if(result.MessageType == WebSocketMessageType.Text)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0 , result.Count);
                    await Broadcast(curName + ": " + message);
                }
                else if(result.MessageType == WebSocketMessageType.Close || ws.State == WebSocketState.Aborted) {
                    connections.Remove(ws);
                    await Broadcast($"{curName} left.");
                    await Broadcast($"{connections.Count} users connected.");
                    await ws.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
                }
            });
    }
    else
    {
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
    }
});

async Task ReceiveMessage(WebSocket socket, Action<WebSocketReceiveResult, byte[]> handleMessage)
{
    var buffer = new byte[1024 * 4];
    while (socket.State == WebSocketState.Open)
    {
        var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        handleMessage(result, buffer);
    }
}

async Task Broadcast(string message)
{
    var bytes = Encoding.UTF8.GetBytes(message);
    var arraySegment = new ArraySegment<byte>(bytes);
    foreach(var client in connections)
    {
        if (client.State == WebSocketState.Open)
        {
            await client.SendAsync(arraySegment, WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}
await app.RunAsync();