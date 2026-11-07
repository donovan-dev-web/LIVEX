// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

using Simulation.Core.World;

namespace Simulation.Console.Integration;

/// <summary>
/// Convention spatiale partagée SYNE ↔ PRISM (ADR-013) : <b>1 unité de monde =
/// 1 bloc PRISM = 1 mètre</b>. Aucune échelle de monde (ni <c>k</c>, ni uu, ni
/// facteur de conversion) n'intervient entre les deux composants.
/// </summary>
/// <remarks>
/// Le <b>milli-bloc</b> est l'encodage à virgule fixe de PRISM pour sa précision
/// infra-bloc, publié par <c>self.position</c> : 1 000 milli-blocs = 1 bloc, et
/// le centre d'une case vaut <c>x * 1 000 + 500</c>. Ce n'est pas une unité de
/// monde : la seule arithmétique admise au bord du contrat est cette échelle
/// fixe, isolée ici et appliquée une fois à la lecture.
/// <para>
/// Les commandes PRISM (<c>Incarner</c>, <c>Aller</c>, …) attendent à l'inverse
/// des coordonnées de bloc entières : <see cref="BlockOf"/> est cette
/// projection, volontairement unique.
/// </para>
/// </remarks>
public static class WorldSpace
{
    /// <summary>Milli-blocs par bloc — échelle fixe de l'encodage PRISM.</summary>
    public const int MilliBlocksPerBlock = 1_000;

    /// <summary>Demi-bloc : PRISM incarne un agent au centre de sa case.</summary>
    public const double HalfBlock = 0.5;

    /// <summary>
    /// Convertit une position PRISM en milli-blocs vers une position SYNE en
    /// unités de monde (blocs continus). Aucun arrondi : le centre de case
    /// <c>128 500</c> devient <c>128,5</c> blocs, exactement.
    /// </summary>
    public static Position FromMilliBlocks(double milliBlocksX, double milliBlocksY) =>
        new(milliBlocksX / MilliBlocksPerBlock, milliBlocksY / MilliBlocksPerBlock);

    /// <summary>
    /// Bloc contenant une position SYNE (partie entière basse, cohérente avec
    /// <c>floor</c> côté PRISM pour les coordonnées négatives).
    /// </summary>
    public static int BlockOf(double worldUnits) => (int)Math.Floor(worldUnits);

    /// <summary>
    /// Position SYNE du centre de la case de bloc donnée — la position que PRISM
    /// attribue à un agent incarné (<c>AgentPosition.AtCell</c>).
    /// </summary>
    public static Position CellCenter(int blockX, int blockY) =>
        new(blockX + HalfBlock, blockY + HalfBlock);
}
