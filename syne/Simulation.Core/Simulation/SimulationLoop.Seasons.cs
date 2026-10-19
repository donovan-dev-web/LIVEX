// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : SimulationLoop - partie Seasons.

namespace Simulation.Core.Loop;

using System.Diagnostics;
using Simulation.Core.Performance;
using Simulation.Core.Prng;

public sealed partial class SimulationLoop
{

    /// <summary>Vide la file des changements de saison (drain d'observabilité).</summary>
    public void ClearSeasonChanges() => _seasonChanges.Clear();

    /// <summary>
    /// Saison courante du monde au tick courant (fonction pure du tick, 0 PRNG) —
    /// saison effective en ticks (durée configurée exprimée en minutes simulées,
    /// identique au défaut, 360 ticks à <c>dt = 1</c>).
    /// </summary>
    public World.Season CurrentSeason => _effectiveSeasons.At(CurrentTick);

    /// <summary>
    /// Cycle de saisons activé (<c>world.seasons.enabled</c>) — modifieurs de
    /// régénération et événement <c>world.season_changed</c> actifs.
    /// </summary>
    public bool SeasonsEnabled => _options.World.Seasons.Enabled;

    /// <summary>
    /// Suivi du territoire activé (<c>world.territories.enabled</c>, SYNE-073) —
    /// appartenance des entités aux zones, événements <c>world.territory_membership_changed</c>
    /// et champ snapshot <c>territories[]</c> actifs.
    /// </summary>
    public bool TerritoriesEnabled => _options.World.Territories.Enabled;

    /// <summary>
    /// Livres activés (SYNE-121) —
    /// <c>world.books.enabled</c> : écriture payée en énergie par l'auteur
    /// (coût <c>world.books.writeCostEnergy</c>), lecture à bénéfice posé en
    /// principe (n°19, <c>world.books.read_benefit</c>). Désactivés par défaut
    /// ⇒ trajectoire du scénario de référence inchangée, checksums dorés
    /// ré-épinglés inchangés (pin contractuel).
    /// </summary>
    public bool BooksEnabled => _options.World.Books.Enabled;

    /// <summary>
    /// Changements d'appartenance aux zones de territoire survenus depuis la
    /// dernière collecte (SYNE-073) : vidés (<see cref="ClearTerritoryChanges"/>)
    /// par la couche d'observabilité après réémission — même protocole que les
    /// modifications d'environnement et les changements de saison.
    /// </summary>
    public IReadOnlyList<World.TerritoryMembershipChange> LastTerritoryChanges => _territoryChanges;


    /// <summary>
    /// Facteurs de régénération saisonniers du tick courant (SYNE-072) + trace du
    /// changement de saison quand le cycle est actif. <c>null</c> sans cycle actif
    /// (régénération nominale ×1) ; pendant un changement, un <see cref="Configuration.SeasonChange"/>
    /// est ajouté à <see cref="LastSeasonChanges"/>. 0 tirage PRNG.
    /// </summary>
    private Simulation.Core.Configuration.SeasonFactors? SeasonFactorsForTick()
    {
        Simulation.Core.Configuration.SeasonSettings seasons = _effectiveSeasons;
        if (!seasons.Enabled)
        {
            return null;
        }

        World.Season current = seasons.At(CurrentTick);
        World.Season previous = seasons.At(CurrentTick - 1);
        if (current != previous)
        {
            _seasonChanges.Add(new Simulation.Core.Configuration.SeasonChange(previous, current));
        }

        return seasons.Factors(CurrentTick);
    }

    /// <summary>Exécute la boucle jusqu'au tick n° <paramref name="maxTicks"/> inclus.</summary>
    public void Run(int maxTicks)
    {
        if (maxTicks <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxTicks), "maxTicks doit être &gt; 0.");
        }

        while (CurrentTick < (ulong)maxTicks)
        {
            AdvanceOneTick();
        }
    }

    /// <summary>
    /// Restauration bit-à-bit (SYNE-111) : pointe la boucle sur
    /// le tick <paramref name="tick"/> et remplace l'état du PRNG par les 4 × 64 bits
    /// sauvegardés. Aucun tirage supplémentaire — la suite est identique à une
    /// exécution ininterrompue.
    /// </summary>
    internal void RestoreState(ulong tick, Xoshiro256StarStar rng)
    {
        CurrentTick = tick;
        _rng = rng;
    }
}

/// <summary>
/// Facade historique de correspondance tick ⇄ temps simulé.
/// Depuis, l'horloge est <b>instanciée</b> par run à partir de la
/// configuration (<see cref="Configuration.SimulationClock"/>, exposée par
/// <see cref="SimulationLoop.Clock"/>) : cette facade fixe l'échelle du profil
/// par défaut (1 tick = 1 minute) et n'est conservée que pour les chemins qui
/// n'ont pas de run sous la main (affichage CLI, tests historiques).
/// </summary>
public static class SimulationTime
{
    /// <summary>Échelle du profil par défaut — utiliser <see cref="Configuration.SimulationClock"/> pour un run configuré.</summary>
    public static readonly int TicksPerSimulationMinute = 1;

    public static long ToSimulatedMinutes(ulong tick) => Simulation.Core.Configuration.SimulationClock.Default.ToSimulatedMinutes(tick);

    /// <summary>Format horloge simulée « T+h:mm » depuis le tick 0.</summary>
    public static string FormatClock(ulong tick) => Simulation.Core.Configuration.SimulationClock.Default.FormatClock(tick);
}
