using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Prism.Contracts;
using Prism.Host.Persistence;

namespace Prism.Host.Hosting;

public static class PrismHostServer
{
    private const int MaximumMessageBytes = 4 * 1024 * 1024;

    public static WebApplication Build(
        int port,
        PrismHostRuntime runtime,
        string? sessionToken,
        int? readPort = null)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port));
        }

        if (readPort is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(readPort));
        }

        var builder = WebApplication.CreateBuilder();
        var urls = readPort is { } streamPort && streamPort != port
            ? new[] { $"http://127.0.0.1:{port}", $"http://127.0.0.1:{streamPort}" }
            : [$"http://127.0.0.1:{port}"];
        builder.WebHost.UseUrls(urls);
        builder.Services.AddSingleton(runtime);
        var app = builder.Build();
        app.UseWebSockets();

        app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
        app.MapGet("/health/ready", () => runtime.IsReady
            ? Results.Ok(new { status = "ready" })
            : Results.Json(new { error = "not_ready" }, statusCode: StatusCodes.Status503ServiceUnavailable));
        app.MapGet("/health/details", () => Results.Ok(runtime.Status));
        app.MapGet("/info", () => Results.Ok(new
        {
            id = "prism-host",
            version = "2.0.0-preview.1",
            protocolVersion = ProtocolVersion.Current.Major,
        }));
        app.MapGet("/metrics", () =>
        {
            var lines = new List<string>
            {
                $"prism_host_last_completed_tick {runtime.Status.LastCompletedTick}",
            };
            if (runtime.CurrentMetrics is { } metrics)
            {
                lines.AddRange(metrics.Stocks.Select(stock =>
                    $"prism_item_stock{{item=\"{stock.ItemTypeId}\",source=\"agents\"}} {stock.Agents}"));
                lines.AddRange(metrics.Stocks.Select(stock =>
                    $"prism_item_stock{{item=\"{stock.ItemTypeId}\",source=\"containers\"}} {stock.Containers}"));
                lines.AddRange(metrics.Stocks.Select(stock =>
                    $"prism_item_stock{{item=\"{stock.ItemTypeId}\",source=\"ground\"}} {stock.GroundItems}"));
                lines.AddRange(metrics.Matter.Select(entry =>
                    $"prism_matter_created_total{{item=\"{entry.ItemTypeId}\",rule=\"{entry.RuleId}\"}} {entry.Created}"));
                lines.AddRange(metrics.Matter.Select(entry =>
                    $"prism_matter_destroyed_total{{item=\"{entry.ItemTypeId}\",rule=\"{entry.RuleId}\"}} {entry.Destroyed}"));
                lines.AddRange(metrics.Failures.Select(failure =>
                    $"prism_command_failures_total{{reason=\"{failure.Reason}\"}} {failure.Count}"));
                lines.Add($"prism_reserved_stations {metrics.ReservedStations}");
                lines.Add($"prism_active_structure_effects {metrics.ActiveStructureEffects}");
            }

            return Results.Text(
                string.Join('\n', lines) + "\n",
                "text/plain; version=0.0.4");
        });
        app.MapGet("/control/status", () => Results.Ok(runtime.Status));

        app.MapPost("/control/prepare", async (HttpContext context) =>
        {
            if (!IsAuthorized(context, sessionToken)) return Unauthorized();
            var request = await ReadJsonAsync<PrepareWorldRequest>(context);
            runtime.Prepare(request);
            return Results.Ok(runtime.Status);
        });
        app.MapPost("/control/start", async (HttpContext context) =>
        {
            if (!IsAuthorized(context, sessionToken)) return Unauthorized();
            runtime.Start(await ReadJsonAsync<StartWorldRequest>(context));
            return Results.Ok(runtime.Status);
        });
        app.MapPost("/control/pause", (HttpContext context) =>
        {
            if (!IsAuthorized(context, sessionToken)) return Unauthorized();
            runtime.Pause();
            return Results.Ok(runtime.Status);
        });
        app.MapPost("/control/resume", (HttpContext context) =>
        {
            if (!IsAuthorized(context, sessionToken)) return Unauthorized();
            runtime.Resume();
            return Results.Ok(runtime.Status);
        });
        app.MapPost("/control/stop", (HttpContext context) =>
        {
            if (!IsAuthorized(context, sessionToken)) return Unauthorized();
            runtime.Stop();
            return Results.Ok(runtime.Status);
        });
        app.MapPost("/control/checkpoint", async (HttpContext context) =>
        {
            if (!IsAuthorized(context, sessionToken)) return Unauthorized();
            var request = await ReadJsonAsync<CheckpointWorldRequest>(context);
            runtime.SaveCheckpoint(request.CheckpointId);
            return Results.Ok(runtime.Status);
        });
        app.MapPost("/control/restore", async (HttpContext context) =>
        {
            if (!IsAuthorized(context, sessionToken)) return Unauthorized();
            var request = await ReadJsonAsync<RestoreWorldRequest>(context);
            runtime.RestoreCheckpoint(request.CheckpointId, request.ContentHash);
            return Results.Ok(runtime.Status);
        });
        app.MapPost("/control/shutdown", (HttpContext context) =>
        {
            if (!IsAuthorized(context, sessionToken)) return Unauthorized();
            context.RequestServices.GetRequiredService<IHostApplicationLifetime>().StopApplication();
            return Results.Accepted();
        });
        app.MapPost("/data/tick", async (HttpContext context) =>
        {
            if (!IsAuthorized(context, sessionToken)) return Unauthorized();
            return Results.Ok(runtime.Step(await ReadJsonAsync<TickRequest>(context)));
        });
        app.Map("/data/lockstep", async (HttpContext context) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            if (!IsAuthorized(context, sessionToken))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            await HandleLockstepAsync(socket, runtime, context.RequestAborted);
        });
        app.Map("/data/read", async (HttpContext context) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            await HandleReadStreamAsync(socket, runtime, context.RequestAborted);
        });

        app.Use(async (context, next) =>
        {
            try
            {
                await next();
            }
            catch (HostLifecycleException exception)
            {
                context.Response.StatusCode = StatusCodes.Status409Conflict;
                await context.Response.WriteAsJsonAsync(new { error = exception.ErrorCode, detail = exception.Message });
            }
            catch (UnsupportedProtocolVersionException exception)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new { error = exception.ErrorCode, detail = exception.Message });
            }
            catch (JsonException exception)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new { error = "invalid_request", detail = exception.Message });
            }
            catch (ArgumentException exception)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new { error = "invalid_request", detail = exception.Message });
            }
            catch (CheckpointContentMismatchException exception)
            {
                context.Response.StatusCode = StatusCodes.Status409Conflict;
                await context.Response.WriteAsJsonAsync(new { error = "content_hash_mismatch", detail = exception.Message });
            }
            catch (KeyNotFoundException exception)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsJsonAsync(new { error = "checkpoint_not_found", detail = exception.Message });
            }
        });

        return app;
    }

    private static async Task HandleLockstepAsync(
        WebSocket socket,
        PrismHostRuntime runtime,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var message = await ReceiveTextMessageAsync(socket, buffer, cancellationToken);
            if (message is null) return;
            try
            {
                var request = ProtocolJson.Deserialize<TickRequest>(message);
                var result = runtime.Step(request);
                await SendTextAsync(socket, ProtocolJson.Serialize(result), cancellationToken);
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException)
            {
                var error = JsonSerializer.Serialize(new
                {
                    error = exception switch
                    {
                        HostLifecycleException lifecycle => lifecycle.ErrorCode,
                        UnsupportedProtocolVersionException unsupported => unsupported.ErrorCode,
                        _ => "invalid_request",
                    },
                    detail = exception.Message,
                }, ProtocolJson.Options);
                await SendTextAsync(socket, error, cancellationToken);
            }
        }
    }

    private static async Task HandleReadStreamAsync(
        WebSocket socket,
        PrismHostRuntime runtime,
        CancellationToken cancellationToken)
    {
        await using var subscription = runtime.SubscribeReadStream();
        using var sending = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var sender = SendReadStreamAsync(socket, subscription, sending.Token);
        try
        {
            var buffer = new byte[1024];
            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var result = await socket.ReceiveAsync(buffer.AsMemory(), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "closed", cancellationToken);
                    return;
                }

                await socket.CloseAsync(
                    WebSocketCloseStatus.PolicyViolation,
                    "the read stream does not accept client messages",
                    cancellationToken);
                return;
            }
        }
        finally
        {
            sending.Cancel();
            await sender;
        }
    }

    private static async Task SendReadStreamAsync(
        WebSocket socket,
        ReadStreamSubscription subscription,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var message in subscription.ReadAllAsync(cancellationToken))
            {
                if (socket.State != WebSocketState.Open)
                {
                    return;
                }

                await SendTextAsync(
                    socket,
                    JsonSerializer.Serialize(message, ProtocolJson.Options),
                    cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private static async Task<string?> ReceiveTextMessageAsync(
        WebSocket socket,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        using var message = new MemoryStream();
        ValueWebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer.AsMemory(), cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "closed", cancellationToken);
                return null;
            }

            if (result.MessageType != WebSocketMessageType.Text)
            {
                await socket.CloseAsync(WebSocketCloseStatus.InvalidMessageType, "text frames required", cancellationToken);
                return null;
            }

            if (message.Length + result.Count > MaximumMessageBytes)
            {
                await socket.CloseAsync(WebSocketCloseStatus.MessageTooBig, "message too large", cancellationToken);
                return null;
            }

            message.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        return Encoding.UTF8.GetString(message.GetBuffer(), 0, checked((int)message.Length));
    }

    private static Task SendTextAsync(WebSocket socket, string message, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        return socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
    }

    private static async Task<T> ReadJsonAsync<T>(HttpContext context)
    {
        if (context.Request.ContentLength is > MaximumMessageBytes)
        {
            throw new JsonException("Request body exceeds the maximum permitted size.");
        }

        using var body = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await context.Request.Body.ReadAsync(buffer, context.RequestAborted)) > 0)
        {
            if (body.Length + read > MaximumMessageBytes)
            {
                throw new JsonException("Request body exceeds the maximum permitted size.");
            }

            body.Write(buffer, 0, read);
        }

        var value = JsonSerializer.Deserialize<T>(body.GetBuffer().AsSpan(0, checked((int)body.Length)), ProtocolJson.Options);
        return value ?? throw new JsonException($"Request body must contain a {typeof(T).Name}.");
    }

    private static bool IsAuthorized(HttpContext context, string? expectedToken)
    {
        if (string.IsNullOrEmpty(expectedToken))
        {
            return false;
        }

        var supplied = context.Request.Headers["X-LIVEX-Session-Token"].ToString();
        if (string.IsNullOrEmpty(supplied) &&
            context.Request.Headers.Authorization.ToString() is { } authorization &&
            authorization.StartsWith("Bearer ", StringComparison.Ordinal))
        {
            supplied = authorization["Bearer ".Length..];
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expectedToken);
        var suppliedBytes = Encoding.UTF8.GetBytes(supplied);
        return expectedBytes.Length == suppliedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
    }

    private static IResult Unauthorized() =>
        Results.Json(new { error = "unauthorized" }, statusCode: StatusCodes.Status401Unauthorized);
}
