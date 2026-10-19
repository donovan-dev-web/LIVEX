// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : SimulationLoop - partie Books.

namespace Simulation.Core.Loop;

using System.Diagnostics;
using Simulation.Core.Performance;
using Simulation.Core.Prng;

public sealed partial class SimulationLoop
{

    /// <summary>Vide la file des mutations de livres (drain d'observabilité).</summary>
    public void ClearBookChanges() => _bookChanges.Clear();


    /// <summary>
    /// Écrit un livre (SYNE-121) : l'auteur
    /// (cognition requise) paie le **coût d'écriture posé en principe** —
    /// énergie <c>world.books.writeCostEnergy</c> **débitée** de ses besoins
    /// corporels (<see cref="Simulation.Core.Cognition.MindState.Needs"/> ;
    /// durée de rédaction et pénalité non modélisées en V0.1) ; le livre est poinçonné
    /// (<c>writtenTick</c> = tick courant) et ajouté au monde. Mutation
    /// **tracée** dans <see cref="LastBookChanges"/>. API de la boucle —
    /// **0 tirage PRNG** (déterministe).
    /// </summary>
    public void WriteBook(Simulation.Core.World.Book book)
    {
        ArgumentNullException.ThrowIfNull(book);
        if (!BooksEnabled)
        {
            throw new InvalidOperationException("Les livres sont désactivés (world.books.enabled = false).");
        }

        if (!Cognition.HasMind(book.AuthorId))
        {
            throw new InvalidOperationException($"L'auteur {book.AuthorId} n'a pas de cognition.");
        }

        if (World.Books.Any(existing => existing.Id == book.Id))
        {
            throw new ArgumentException($"Un livre portant l'identifiant « {book.Id} » existe déjà.", nameof(book));
        }

        double cost = _options.World.Books.WriteCostEnergy;
        book.WrittenTick = CurrentTick;
        World.AddBook(book);
        Cognition.MindOf(book.AuthorId).Needs.ExertEnergy(cost);
        _bookChanges.Add(new Simulation.Core.World.BookChange(
            Simulation.Core.World.BookChangeKind.Written, book, -cost, null));
    }


    /// <summary>
    /// Lit un livre (SYNE-121) : le lecteur
    /// (cognition requise) tire le **bénéfice posé en principe**
    /// (<c>world.books.read_benefit</c>, chiffrage savoir/confiance reporté au
    /// moteur de mémoire) et la lecture est **tracée** (lecteurs distincts,
    /// ordre de première consultation) dans <see cref="LastBookChanges"/>.
    /// API de la boucle — **0 tirage PRNG** (déterministe).
    /// </summary>
    public void ReadBook(Simulation.Core.World.Book book, ulong readerId)
    {
        ArgumentNullException.ThrowIfNull(book);
        if (!BooksEnabled)
        {
            throw new InvalidOperationException("Les livres sont désactivés (world.books.enabled = false).");
        }

        if (!Cognition.HasMind(readerId))
        {
            throw new InvalidOperationException($"Le lecteur {readerId} n'a pas de cognition.");
        }

        if (!World.Books.Any(existing => ReferenceEquals(existing, book)))
        {
            throw new ArgumentException("Le livre ne fait pas partie de ce monde.", nameof(book));
        }

        double benefit = _options.World.Books.ReadBenefit;
        book.MarkReadBy(readerId);
        _bookChanges.Add(new Simulation.Core.World.BookChange(
            Simulation.Core.World.BookChangeKind.Read, book, benefit, readerId));
    }

    /// <summary>
    /// Avance d'un tick (1 minute simulée) puis exécute le
    /// pipeline cognitif BDI (U1, SYNE-010) dans l'ordre causal strict. Le PRNG
    /// n'avance que d'un tirage par tick (contrat).
    /// </summary>
    public void AdvanceOneTick()
    {
        CurrentTick += 1;
        _rng = _rng.NextUInt64(out _);
        if (_budget is null)
        {
            _cognition.Step(CurrentTick);
            AdvanceResources();
        }
        else
        {
            long start = Stopwatch.GetTimestamp();
            _cognition.Step(CurrentTick);
            using (TickPhaseScope resourcesScope = _budget.Begin(TickPhase.EventsGroupsPopulation))
            {
                AdvanceResources();
            }

            double elapsedMs = (Stopwatch.GetTimestamp() - start) * (1000.0 / Stopwatch.Frequency);
            _budget.RecordPipelineTick(elapsedMs);
        }

        if (_autosaveHandler is not null && CurrentTick % (ulong)_autoSaveEveryNTicks == 0)
        {
            _autosaveHandler(this);
        }

        TrackTerritoryMembership();
    }
}
