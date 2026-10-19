// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : ControlServer - partie IO.

using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Simulation.Console.Observability;
using Simulation.Core.Configuration;

namespace Simulation.Console.Control;

using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Simulation.Console.Observability;
using Simulation.Core.Configuration;

public sealed partial class ControlServer : IAsyncDisposable
{

    /// <summary>
    /// Traduit une erreur de déserialisation en message utile sans exposer la
    /// pile d'appels ni un numéro de ligne interne.
    /// </summary>
    private static string DescribeJsonError(JsonException exception)
    {
        string message = exception.Message;
        int marker = message.IndexOf(" Path: ", StringComparison.Ordinal);
        if (marker > 0)
        {
            message = message[..marker].Trim();
        }

        return string.IsNullOrWhiteSpace(message)
            ? "La configuration contient des valeurs ou des types invalides."
            : message;
    }    private async Task<(int Status, string Body)> ResetAsync(JsonElement body)
    {
        ulong? seed = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("seed", out JsonElement seedElement) ? seedElement.GetUInt64() : null;
        // Même règle que start/prepare : la surcouche reste en JSON brut et un
        // type non objet est un 400 explicite, jamais un repli silencieux.
        if (body.ValueKind == JsonValueKind.Object && body.TryGetProperty("config", out JsonElement resetConfig) && resetConfig.ValueKind != JsonValueKind.Object)
            return (400, ToJson(ErrorJson("invalid_config", "config doit etre un objet JSON.")));
        string? config = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("config", out JsonElement resetConfigRaw)
            ? resetConfigRaw.GetRawText()
            : null;
        int? maxTicks = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("maxTicks", out JsonElement maxTicksElement)
            ? maxTicksElement.GetInt32()
            : null;

        try
        {
            string runId = await _controller.ResetAsync(seed, maxTicks, config);
            return (200, ToJson(OkJson("reset", runId, _controller.Status())));
        }
        catch (JsonException exception)
        {
            return (400, ToJson(ErrorJson("invalid_config", DescribeJsonError(exception))));
        }
        catch (InvalidOperationException exception)
        {
            return (409, ToJson(ErrorJson("run_active", exception.Message)));
        }
    }

    /// <summary>
    /// Limite de taille du corps de requête. Sans elle, un
    /// <c>Content-Length</c> (ou un transfert par blocs) arbitrairement grand
    /// était lu entièrement en mémoire avant tout rejet.
    /// </summary>
    private const int MaxBodyBytes = 1 << 20;

    /// <summary>
    /// Plafond de vidage (16 Mio) avant de répondre 413 sur un
    /// <c>Content-Length</c> déclaré trop grand : fermer la socket alors que des
    /// octets du corps sont encore non lus émet un RST et le client reçoit
    /// « broken pipe » au lieu du 413 (course observée en parallélisme).
    /// </summary>
    private const int DrainCapBytes = 16 * MaxBodyBytes;


    private static async Task<JsonElement> ReadBodyAsync(HttpListenerRequest request)
    {
        if (request.ContentLength64 == 0)
        {
            return new JsonElement();
        }

        if (request.ContentLength64 > MaxBodyBytes)
        {
            // Vidage best effort, borné en taille, dans un tampon jetable de
            // 8 Kio (le corps n'est jamais accumulé en mémoire — la garantie
            // « not buffered » tient) : le client termine son envoi et peut
            // lire la réponse 413 au lieu d'être coupé en pleine écriture.
            await DiscardAsync(request.InputStream,
                Math.Min(request.ContentLength64, DrainCapBytes)).ConfigureAwait(false);
            throw new RequestException(413, "payload_too_large",
                $"Le corps de la requête dépasse la limite de {MaxBodyBytes} octets.");
        }

        // ContentLength64 est -1 pour un transfert par blocs : la lecture reste
        // donc bornée par ReadBoundedAsync, qui compte les octets reçus.
        byte[] raw = await ReadBoundedAsync(request.InputStream, MaxBodyBytes).ConfigureAwait(false);
        if (raw.Length == 0)
        {
            return new JsonElement();
        }

        string text;
        try
        {
            text = Encoding.UTF8.GetString(raw);
        }
        catch (ArgumentException)
        {
            throw new RequestException(400, "invalid_json", "Le corps de la requête n'est pas de l'UTF-8 valide.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return new JsonElement();
        }

        try
        {
            // Le RootElement dépend du JsonDocument : sans Clone, il devient
            // invalide à la dépose et libère la mémoire native gérée
            // alors que les propriétés sont encore lues par le routeur.
            using JsonDocument document = JsonDocument.Parse(text);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            throw new RequestException(400, "invalid_json", "Le corps de la requête n'est pas un JSON valide.");
        }
    }


    /// <summary>
    /// Vide jusqu'à <paramref name="maxBytes"/> octets sans les conserver.
    /// Best effort : toute erreur (client déconnecté) est ignorée, la réponse
    /// 413 part de toute façon.
    /// </summary>
    private static async Task DiscardAsync(Stream stream, long maxBytes)
    {
        byte[] scratch = new byte[8192];
        try
        {
            long discarded = 0;
            while (discarded < maxBytes)
            {
                int read = await stream.ReadAsync(scratch).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                discarded += read;
            }
        }
        catch (Exception)
        {
            // best effort : aucune requête à servir au-delà du rejet
        }
    }


    /// <summary>
    /// Lit au plus <paramref name="limit"/> octets et détrègue le dépassement,
    /// même lorsque la longueur déclarée est inconnue (transfert par blocs).
    /// </summary>
    private static async Task<byte[]> ReadBoundedAsync(Stream stream, int limit)
    {
        using MemoryStream buffer = new(capacity: Math.Min(limit, 8192));
        byte[] chunk = new byte[8192];
        while (true)
        {
            int read = await stream.ReadAsync(chunk).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            if (buffer.Length + read > limit)
            {
                throw new RequestException(413, "payload_too_large",
                    $"Le corps de la requête dépasse la limite de {limit} octets.");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    /// <summary>Erreur de requète associée à un code HTTP (4xx) plutôt qu'à une 500.</summary>
    private sealed class RequestException(int status, string code, string detail) : Exception(detail)
    {
        public int Status { get; } = status;

        public string Code { get; } = code;

        public string Detail { get; } = detail;
    }


    private static async Task WriteErrorAsync(HttpListenerContext context, int status, string code, string detail)
    {
        byte[] payload = Encoding.UTF8.GetBytes(ToJson(ErrorJson(code, detail)));
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = payload.Length;
        await context.Response.OutputStream.WriteAsync(payload);
    }

    private static string ToJson<T>(T value) where T : notnull
    {
        return JsonSerializer.Serialize(value, JsonOptions);
    }


    private static Dictionary<string, object?> OkJson(string action) => new()
    {
        ["ok"] = true,
        ["action"] = action,
    };


    private static Dictionary<string, object?> OkJson(string action, string runId, SimulationStatusSnapshot status) => new()
    {
        ["ok"] = true,
        ["action"] = action,
        ["runId"] = runId,
        ["state"] = status.State.ToString().ToLowerInvariant(),
        ["tick"] = status.Tick,
        ["aliveCount"] = status.AliveCount,
        ["seed"] = status.Seed,
    };


    private static Dictionary<string, object?> ErrorJson(string code, string detail) => new()
    {
        ["ok"] = false,
        ["error"] = code,
        ["detail"] = detail,
    };


    private static Dictionary<string, object?> StatusJson(
        SimulationStatusSnapshot status,
        int? ticksPerSecond,
        int? simulatedSecondsPerTick) => new()
    {
        ["state"] = status.State.ToString().ToLowerInvariant(),
        ["runId"] = status.RunId,
        ["tick"] = status.Tick,
        ["aliveCount"] = status.AliveCount,
        ["seed"] = status.Seed,
        ["worldPrepared"] = status.WorldPrepared,
        ["worldVersion"] = status.WorldVersion,
        ["worldReadyAcknowledged"] = status.WorldReadyAcknowledged,
        ["maxTicks"] = status.MaxTicks,
        ["ticksPerSecond"] = ticksPerSecond,
        // Échelle temporelle du run (contrat 0.4.0) : secondes simulées
        // par tick — null tant qu'aucun monde n'est préparé (même sémantique
        // que ticksPerSecond).
        ["simulatedSecondsPerTick"] = simulatedSecondsPerTick,
    };
}
