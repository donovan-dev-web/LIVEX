// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : profilage opt-in du tick couplé (P9/G5) — décompose le coût d'un tick
//        entre décision (cognition + write-back), appel PRISM (réseau + tick
//        hôte) et application des observations, et relève la taille du flux
//        reçu. Sans ce découpage, « le tick couplé coûte 6 s » ne dit pas où.

namespace Simulation.Console.Integration;

using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

/// <summary>
/// Relevé par tick du coût couplé, activé par <c>SYNE_COUPLED_PROFILE=1</c>.
/// </summary>
/// <remarks>
/// <para>
/// Le profilage ne touche ni la trajectoire ni le déterminisme : il lit des
/// horloges murales et la taille des enveloppes reçues, il ne décide rien. Il
/// reste donc activable sur un run de référence sans en changer l'empreinte.
/// </para>
/// <para>
/// Trois phases, dans l'ordre de la boucle : <b>décision</b> (délibération SYNE
/// et traduction en commandes), <b>appel PRISM</b> (requête HTTP, tick hôte,
/// réponse sérialisée) et <b>application</b> (monde observé, inventaire,
/// croyances, mémoire spatiale). Le nombre d'observations et de changements
/// reçus est relevé à part : un flux qui grossit avec la durée du run explique
/// à lui seul une dégradation, sans qu'aucun calcul ne soit en cause.
/// </para>
/// <para>
/// <c>SYNE_COUPLED_PROFILE=2</c> imprime en plus une ligne par tick.
/// </para>
/// </remarks>
public sealed class PrismCoupledProfile
{
    /// <summary>
    /// Chemin JSONL/JSON du relevé machine (PRISM-106) : quand cette variable
    /// désigne un fichier, le run y dépose ses ticks à la fin, sans passer par la
    /// lecture des lignes de console.
    /// </summary>
    public const string JsonEnvironmentVariable = "SYNE_COUPLED_PROFILE_JSON";

    /// <summary>Schéma des relevés par tick écrits par <see cref="WriteJson"/>.</summary>
    public const string JsonSchema = "livex.syne.coupled-tick.v1";

    private readonly bool _verbose;
    private readonly List<Sample> _samples = [];
    private long _decideStarted;
    private long _stepStarted;
    private long _applyStarted;

    private PrismCoupledProfile(bool verbose)
    {
        _verbose = verbose;
    }

    /// <summary>Relevé actif pour ce run, ou <see langword="null"/> si l'environnement ne le demande pas.</summary>
    public static PrismCoupledProfile? FromEnvironment()
    {
        string? raw = Environment.GetEnvironmentVariable("SYNE_COUPLED_PROFILE");
        if (string.IsNullOrWhiteSpace(raw) || raw is "0" or "false")
        {
            return null;
        }

        return new PrismCoupledProfile(verbose: raw is "2" or "verbose");
    }

    public bool Enabled => true;

    public void BeginDecide(long tick) => _decideStarted = Stopwatch.GetTimestamp();

    public void EndDecide(long tick) => _samples.Add(new Sample(tick, Ms(_decideStarted), 0, 0, 0, 0));

    public void BeginStep() => _stepStarted = Stopwatch.GetTimestamp();

    public void EndStep()
    {
        if (_samples.Count > 0)
        {
            _samples[^1] = _samples[^1] with { StepMs = Ms(_stepStarted) };
        }
    }

    public void BeginApply() => _applyStarted = Stopwatch.GetTimestamp();

    /// <summary>Clôt l'application des observations et relève la taille du flux reçu.</summary>
    public void EndApply(int observations, int changes)
    {
        if (_samples.Count == 0)
        {
            return;
        }

        Sample sample = _samples[^1] with
        {
            ApplyMs = Ms(_applyStarted),
            Observations = observations,
            Changes = changes,
        };
        _samples[^1] = sample;

        if (_verbose)
        {
            System.Console.Error.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"[couplé] tick {sample.Tick,5} décision {sample.DecideMs,8:F1} ms | PRISM {sample.StepMs,8:F1} ms | " +
                $"application {sample.ApplyMs,7:F1} ms | {sample.Observations,4} obs, {sample.Changes,6} changements"));
        }
    }

    /// <summary>
    /// Écrit le relevé par tick dans un fichier JSON, pour le harnais
    /// <c>scripts/prism-coupled-benchmark.py</c>. Le fichier est réécrit à chaque
    /// run : il ne s'agit pas d'un journal appendé mais du relevé d'un run.
    /// </summary>
    public void WriteJson(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var payload = new
        {
            schema = JsonSchema,
            ticks = _samples
                .Select(sample => new
                {
                    tick = sample.Tick,
                    decideMs = Math.Round(sample.DecideMs, 3),
                    prismMs = Math.Round(sample.StepMs, 3),
                    applyMs = Math.Round(sample.ApplyMs, 3),
                    observations = sample.Observations,
                    changes = sample.Changes,
                })
                .ToArray(),
        };

        File.WriteAllText(path, JsonSerializer.Serialize(payload));
    }

    /// <summary>Bilan du run : moyennes globales et fenêtres de début et de fin.</summary>
    public void Report(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (_samples.Count == 0)
        {
            writer.WriteLine("SYNE — profil couplé : aucun tick mesuré.");
            return;
        }

        int window = Math.Min(10, _samples.Count);
        List<Sample> head = _samples.Take(window).ToList();
        List<Sample> tail = _samples.Skip(_samples.Count - window).ToList();
        Sample mean = Mean(_samples);

        writer.WriteLine(
            "SYNE — profil couplé (ms/tick, moyenne) : " +
            $"décision {mean.DecideMs:F1} | PRISM {mean.StepMs:F1} | application {mean.ApplyMs:F1} | " +
            $"total {mean.TotalMs:F1} | {mean.Changes:F0} changements reçus");
        writer.WriteLine(
            $"SYNE — profil couplé, début (ticks {head[0].Tick}–{head[^1].Tick}) : " +
            $"décision {Mean(head).DecideMs:F1} | PRISM {Mean(head).StepMs:F1} | " +
            $"application {Mean(head).ApplyMs:F1} | {Mean(head).Changes:F0} changements");
        writer.WriteLine(
            $"SYNE — profil couplé, fin (ticks {tail[0].Tick}–{tail[^1].Tick}) : " +
            $"décision {Mean(tail).DecideMs:F1} | PRISM {Mean(tail).StepMs:F1} | " +
            $"application {Mean(tail).ApplyMs:F1} | {Mean(tail).Changes:F0} changements");

        writer.WriteLine(
            "SYNE — profil couplé (ms/tick, détail) : tick | décision | PRISM | application | changements");
        foreach (Sample sample in _samples)
        {
            writer.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"  {sample.Tick,5} | {sample.DecideMs,8:F1} | {sample.StepMs,8:F1} | {sample.ApplyMs,7:F1} | {sample.Changes,7}"));
        }
    }

    private static Sample Mean(IReadOnlyList<Sample> samples)
    {
        double decide = 0, step = 0, apply = 0, changes = 0;
        foreach (Sample sample in samples)
        {
            decide += sample.DecideMs;
            step += sample.StepMs;
            apply += sample.ApplyMs;
            changes += sample.Changes;
        }

        double count = samples.Count;
        return new Sample(
            samples[^1].Tick,
            decide / count,
            step / count,
            apply / count,
            (int)(samples.Sum(s => s.Observations) / count),
            (int)(changes / count));
    }

    private static double Ms(long started) =>
        (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;

    private readonly record struct Sample(
        long Tick,
        double DecideMs,
        double StepMs,
        double ApplyMs,
        int Observations,
        int Changes)
    {
        public double TotalMs => DecideMs + StepMs + ApplyMs;
    }
}
