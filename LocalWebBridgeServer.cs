using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace PhonePadBridge.WebBridge;

public sealed class LocalWebBridgeServer : IDisposable
{
    private WebApplication? _app;
    private CancellationTokenSource? _cts;

    public event Action<string>? StatusChanged;
    public event Action<ControllerState>? PacketReceived;

    public int Port { get; private set; } = 49494;

    public async Task StartAsync(int port)
    {
        if (_app != null) return;

        Port = port;
        _cts = new CancellationTokenSource();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(LocalWebBridgeServer).Assembly.FullName,
            ContentRootPath = AppContext.BaseDirectory
        });

        builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
        builder.WebHost.UseKestrel(options =>
        {
            options.ListenAnyIP(port);
        });

        var app = builder.Build();

        app.UseWebSockets(new WebSocketOptions
        {
            KeepAliveInterval = TimeSpan.FromSeconds(10)
        });

        app.Use(async (context, next) =>
        {
            if (context.Request.Path == "/ws")
            {
                if (!context.WebSockets.IsWebSocketRequest)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsync("WebSocket required.");
                    return;
                }

                using var socket = await context.WebSockets.AcceptWebSocketAsync();
                var remote = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                StatusChanged?.Invoke($"Phone connected: {remote}");

                try
                {
                    await ReceiveLoopAsync(socket, _cts?.Token ?? CancellationToken.None);
                }
                finally
                {
                    StatusChanged?.Invoke($"Phone disconnected: {remote}");
                }

                return;
            }

            await next();
        });

        app.MapGet("/ping", () => "PhonePad Bridge OK");

        // Serve the phone webpage from embedded resources.
        // This keeps the published app as one .exe instead of needing a separate web folder.
        app.MapGet("/{*path}", async (HttpContext context) =>
        {
            var requestedPath = context.Request.Path.Value?.TrimStart('/') ?? "";
            if (string.IsNullOrWhiteSpace(requestedPath))
            {
                requestedPath = "index.html";
            }

            if (requestedPath.Contains("..") || requestedPath.Contains('\\'))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            var ok = await ServeEmbeddedWebFileAsync(context, requestedPath);
            if (!ok)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsync("Not found");
            }
        });

        _app = app;

        StatusChanged?.Invoke($"Web server starting on port {port}");
        _ = Task.Run(async () =>
        {
            try
            {
                await app.RunAsync(_cts.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke("Server error: " + ex.Message);
            }
        });

        await Task.Delay(350);
        StatusChanged?.Invoke("Web server running");
    }

    private static async Task<bool> ServeEmbeddedWebFileAsync(HttpContext context, string requestedPath)
    {
        var resourceName = "web/" + requestedPath.Replace('\\', '/');

        var assembly = Assembly.GetExecutingAssembly();
        await using var stream = assembly.GetManifestResourceStream(resourceName);

        if (stream == null)
        {
            return false;
        }

        context.Response.ContentType = GetContentType(requestedPath);
        await stream.CopyToAsync(context.Response.Body);
        return true;
    }

    private static string GetContentType(string path)
    {
        var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();

        return ext switch
        {
            ".html" => "text/html; charset=utf-8",
            ".css" => "text/css; charset=utf-8",
            ".js" => "application/javascript; charset=utf-8",
            ".json" => "application/json; charset=utf-8",
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".svg" => "image/svg+xml",
            ".ico" => "image/x-icon",
            _ => "application/octet-stream"
        };
    }

    private async Task ReceiveLoopAsync(WebSocket socket, CancellationToken token)
    {
        var buffer = new byte[16 * 1024];

        while (!token.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            WebSocketReceiveResult result;
            int count;

            try
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                count = result.Count;
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (result.MessageType == WebSocketMessageType.Close)
            {
                break;
            }

            if (result.MessageType != WebSocketMessageType.Text)
            {
                continue;
            }

            while (!result.EndOfMessage)
            {
                if (count >= buffer.Length)
                {
                    StatusChanged?.Invoke("Packet too large; dropped");
                    break;
                }

                result = await socket.ReceiveAsync(
                    new ArraySegment<byte>(buffer, count, buffer.Length - count),
                    token
                );

                count += result.Count;
            }

            var json = Encoding.UTF8.GetString(buffer, 0, count);

            try
            {
                var state = JsonSerializer.Deserialize<ControllerState>(json);
                if (state != null && state.Type == "state")
                {
                    PacketReceived?.Invoke(state);
                }
            }
            catch
            {
                StatusChanged?.Invoke("Bad packet ignored");
            }
        }

        if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
        {
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
        }
    }

    public async Task StopAsync()
    {
        if (_app == null) return;

        try
        {
            _cts?.Cancel();
            await _app.StopAsync(TimeSpan.FromSeconds(1));
            await _app.DisposeAsync();
        }
        catch
        {
            // ignored
        }

        _app = null;
        StatusChanged?.Invoke("Web server stopped");
    }

    public static string GetUrlList(int port)
    {
        var ips = GetLocalIPv4Addresses().ToList();

        if (ips.Count == 0)
        {
            return $"http://localhost:{port}";
        }

        return string.Join(Environment.NewLine, ips.Select(ip => $"http://{ip}:{port}"));
    }

    private static IEnumerable<string> GetLocalIPv4Addresses()
    {
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up)
                continue;

            if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;

            var props = networkInterface.GetIPProperties();

            foreach (var address in props.UnicastAddresses)
            {
                if (address.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    yield return address.Address.ToString();
                }
            }
        }
    }

    public void Dispose()
    {
        try
        {
            StopAsync().GetAwaiter().GetResult();
        }
        catch
        {
            // ignored
        }

        _cts?.Dispose();
    }
}
