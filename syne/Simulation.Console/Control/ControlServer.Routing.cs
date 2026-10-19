// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : ControlServer - partie Routing.

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

    private async Task<(int Status, string Body)> RouteAsync(HttpListenerRequest request)
    {
        string path = request.Url?.AbsolutePath ?? string.Empty;
        string method = request.HttpMethod;

        if (method == "GET" && path is "/health/live" or "/health/ready" or "/health/details")
        {
            if (path == "/health/ready" && !_ready)
            {
                return (503, ToJson(ErrorJson("not_ready", "Le service n'a pas terminé sa préparation.")));
            }

            if (path == "/health/details" && !_ready)
            {
                return (503, ToJson(new { status = "NotReady", readiness = false }));
            }

            return (200, ToJson(new { status = "Healthy" }));
        }

        if (method == "GET" && path == "/info")
        {
            return (200, ToJson(new { id = "syne", version = _version, protocolVersion = 1, instanceId = _instanceId }));
        }

        if (path == "/control/shutdown" && method == "POST")
        {
            if (!IsAuthorized(request))
            {
                return (401, ToJson(ErrorJson("unauthorized", "Un jeton de session valide est requis.")));
            }

            return (200, ToJson(OkJson("shutdown")));
        }

        if (path == "/api/control/status" && method == "GET")
        {
            return (200, ToJson(StatusJson(
                _controller.Status(),
                _controller.WorldDescription?.TicksPerSecond,
                _controller.WorldDescription?.SimulatedSecondsPerTick)));
        }

        if (path == "/api/world" && method == "GET")
        {
            return _controller.WorldDescription is { } world
                ? (200, ToJson(world))
                : (409, ToJson(ErrorJson("world_not_prepared", "Préparez le monde avant de le consulter.")));
        }

        if (!path.StartsWith("/api/control/", StringComparison.Ordinal) || method != "POST")
        {
            return (404, ToJson(ErrorJson("not_found", "Endpoint inconnu.")));
        }

        if (_sessionToken is not null && !IsAuthorized(request))
        {
            return (401, ToJson(ErrorJson("unauthorized", "Un jeton de session valide est requis.")));
        }

        string action = path["/api/control/".Length..];
        JsonElement body = await ReadBodyAsync(request);

        switch (action)
        {
            case "prepare":
                return await PrepareAsync(body);
            case "ready":
                string? version = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("worldVersion", out var v)
                    ? v.GetString() : null;
                return _controller.AcknowledgeReady(version)
                    ? (200, ToJson(OkJson("ready")))
                    : (409, ToJson(ErrorJson("world_not_ready", "Le monde n'est pas préparé ou sa version est incorrecte.")));
            case "start":
                return await StartAsync(body);
            case "pause":
                _controller.Pause();
                return (200, ToJson(OkJson("paused")));
            case "resume":
                _controller.Resume();
                return (200, ToJson(OkJson("resumed")));
            case "stop":
                // On attend la fin réelle de la boucle : répondre 200 avant
                // l'arrêt effectif laisserait au client l'impression que le run
                // est terminé alors qu'il peut encore avancer d'un tick.
                await _controller.StopAsync();
                return (200, ToJson(OkJson("stopped")));
            case "reset":
                return await ResetAsync(body);
            default:
                return (404, ToJson(ErrorJson("not_found", $"Action de contrôle inconnue : {action}.")));
        }

    }


    private bool IsAuthorized(HttpListenerRequest request)
    {
        if (string.IsNullOrEmpty(_sessionToken))
        {
            return false;
        }

        string? header = request.Headers["Authorization"];
        string[] authorization = header?.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries) ?? [];
        if (authorization.Length != 2 || !authorization[0].Equals("Bearer", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        byte[] supplied = Encoding.UTF8.GetBytes(authorization[1]);
        byte[] expected = Encoding.UTF8.GetBytes(_sessionToken);
        return supplied.Length == expected.Length && CryptographicOperations.FixedTimeEquals(supplied, expected);
    }


    private async Task<(int Status, string Body)> PrepareAsync(JsonElement body)
    {
            ulong? seed = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("seed", out var s) ? s.GetUInt64() : null;
            if (body.ValueKind == JsonValueKind.Object && body.TryGetProperty("config", out var c) && c.ValueKind != JsonValueKind.Object)
                return (400, ToJson(ErrorJson("invalid_config", "config doit etre un objet JSON.")));
            string? config = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("config", out var c2)
                ? c2.GetRawText()
                : null;
            int? ticksPerSecond = null;
            if (body.ValueKind == JsonValueKind.Object && body.TryGetProperty("ticksPerSecond", out var tps))
            {
                if (tps.ValueKind != JsonValueKind.Number || !tps.TryGetInt32(out int parsedTicksPerSecond) || parsedTicksPerSecond <= 0)
                    return (400, ToJson(ErrorJson("invalid_ticks_per_second", "ticksPerSecond doit être un entier strictement positif.")));
                ticksPerSecond = parsedTicksPerSecond;
            }
            try
            {
                var world = await _controller.PrepareAsync(seed, config ?? SimulationProfiles.ReferenceJson(), ticksPerSecond);
                return (200, ToJson(new { ok = true, action = "prepared", ticksPerSecond = world.TicksPerSecond, world }));
            }
            catch (JsonException exception)
            {
                // JSON bien formé mais valeurs incompatibles avec le modèle
                // (mauvais types, nombres hors domaine) : c'est une requête
                // invalide, pas une erreur interne du serveur.
                return (400, ToJson(ErrorJson("invalid_config", DescribeJsonError(exception))));
            }
    }


    private async Task<(int Status, string Body)> StartAsync(JsonElement body)
    {
        ulong? seed = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("seed", out JsonElement seedElement)
            ? seedElement.GetUInt64()
            : null;
        if (body.ValueKind == JsonValueKind.Object && body.TryGetProperty("config", out JsonElement configRaw) && configRaw.ValueKind != JsonValueKind.Object)
            return (400, ToJson(ErrorJson("invalid_config", "config doit être un objet JSON.")));
        // La surcouche est conservée en JSON brut : désérialiser la surcouche
        // en SimulationOptions la rendrait complète et la fusion
        // réinitialiserait le profil (cf. ConfigLoader.MergeJson).
        string? config = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("config", out JsonElement configElement)
            ? configElement.GetRawText()
            : null;
        int? maxTicks = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("maxTicks", out JsonElement maxTicksElement)
            ? maxTicksElement.GetInt32()
            : null;

        // Une commande start sans options ne remplace pas un run actif.
        if (config is null && seed is null && maxTicks is null && _controller.HasRun)        {
            return (409, ToJson(ErrorJson("run_active", "Un run est déjà en cours — utilisez /stop ou /reset avant de redémarrer.")));
        }

        // Un run « finished » a atteint son nombre de ticks cible : le monde est
        // prêt mais le run n'avancera plus. Le 409 world_not_ready générique
        // orientait le client vers /prepare alors que le remède est /reset.
        if (_controller.State == SimulationControlState.Finished)
        {
            return (409, ToJson(ErrorJson("run_finished", "Run terminé — appelez /api/control/reset avant de redémarrer.")));
        }

        // Un run manuel/UI utilise le profil de référence revu, sauf si
        // l'appelant fournit une surcouche de configuration explicite.
        try
        {
            string? effectiveConfig = _controller.WorldPrepared && config is null
                ? null
                : config ?? SimulationProfiles.ReferenceJson();
            string runId = await _controller.StartAsync(seed, effectiveConfig, maxTicks);
            return (200, ToJson(OkJson("started", runId, _controller.Status())));
        }
        catch (PreparedWorldMismatchException exception)
        {
            return (409, ToJson(ErrorJson(exception.Code, exception.Message)));
        }
        catch (JsonException exception)
        {
            return (400, ToJson(ErrorJson("invalid_config", DescribeJsonError(exception))));
        }
        catch (InvalidOperationException exception)
        {
            return (409, ToJson(ErrorJson("world_not_ready", exception.Message)));
        }
    }
}
