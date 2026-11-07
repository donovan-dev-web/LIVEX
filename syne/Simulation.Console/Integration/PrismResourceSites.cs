// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

using Simulation.Core.World;

namespace Simulation.Console.Integration;

/// <summary>
/// Gisements du monde PRISM (P9) : un bloc ressource déclaré par l'instantané de
/// monde, sa <b>case d'approche</b> praticable et un libellé canonique.
/// </summary>
/// <remarks>
/// <para>
/// Pourquoi une case d'approche : un agent PRISM ne peut pas se tenir <b>dans</b>
/// un bloc ressource (<c>MovementGraph.CanStandAt</c> exige un plancher solide et
/// deux blocs libres ; l'eau n'est pas solide). La cible d'un déplacement est
/// donc la case praticable voisine, pas le bloc lui-même.
/// </para>
/// <para>
/// La case d'approche doit être à la <b>même altitude</b> que le bloc ressource :
/// <c>ConsumptionSystem.DrinkFromSource</c> exige <c>IsAdjacent</c>
/// (Manhattan ≤ 1, z inclus). Un voisin praticable au-dessus ou en dessous du
/// bloc — une nappe enterrée sous un plateau — est à distance ≥ 2 et la commande
/// <c>Consommer</c> échouerait en <c>not_adjacent</c>. Un bloc sans rive
/// praticable à son niveau n'est donc <b>pas</b> un gisement : une cible
/// inatteignable n'est pas une cible.
/// </para>
/// <para>
/// Le libellé suit la convention des gisements SYNE
/// (<c>resource-&lt;kind&gt;-&lt;n&gt;</c>) : <c>KnowledgeBuilder.KindFromLabel</c> en
/// déduit le <see cref="ResourceKind"/> sans table supplémentaire, et la
/// croyance correspondante est <c>node-resource-water-3</c>. Seule l'eau porte
/// en plus un <see cref="Site.Source"/> (le bloc buvable visé par
/// <c>Consommer</c>) : le bois et le minerai ne sont que des repères de
/// déplacement (aucun désir moteur ne les consomme encore).
/// </para>
/// </remarks>
public sealed class PrismResourceSites
{
    /// <summary>
    /// Un gisement : libellé, nature, case d'approche et — pour l'eau — le bloc
    /// buvable visé par une commande <c>Consommer</c>.
    /// </summary>
    public readonly record struct Site(
        string Label,
        ResourceKind Kind,
        PrismBlockPosition Approach,
        PrismBlockPosition? Source);

    private static readonly (int Dx, int Dy)[] Neighbourhood =
    [
        (0, -1),
        (-1, 0),
        (1, 0),
        (0, 1),
    ];

    private readonly Dictionary<(int X, int Y, int Z), Site> _byBlock;
    private readonly Dictionary<string, Site> _byLabel;
    private readonly List<ResourceNode> _nodes;

    private PrismResourceSites(
        Dictionary<(int X, int Y, int Z), Site> byBlock,
        Dictionary<string, Site> byLabel,
        List<ResourceNode> nodes)
    {
        _byBlock = byBlock;
        _byLabel = byLabel;
        _nodes = nodes;
        DrinkableCount = nodes.Count(node => node.Kind == ResourceKind.Water);
    }

    /// <summary>Aucun gisement connu (contenu absent ou monde sans ressource atteignable).</summary>
    public static PrismResourceSites Empty { get; } = new([], new(StringComparer.Ordinal), []);

    /// <summary>Gisements exposés au monde SYNE, dans l'ordre canonique.</summary>
    public IReadOnlyList<ResourceNode> Nodes => _nodes;

    public int Count => _nodes.Count;

    /// <summary>Nombre de gisements buvables (seuls à porter une cible <c>Consommer</c>).</summary>
    public int DrinkableCount { get; }

    /// <summary>
    /// Construit les gisements d'un monde : pour chaque bloc ressource, la
    /// première case praticable du voisinage (nord, ouest, est, sud) <b>au même
    /// niveau</b>. Un bloc sans un tel voisin est ignoré ; une colonne ne produit
    /// qu'un gisement par nature (une forêt ne crée pas un gisement par feuille).
    /// </summary>
    public static PrismResourceSites Build(
        PrismWorldGrid grid,
        IReadOnlyDictionary<ushort, ResourceKind> materialKinds)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(materialKinds);
        if (materialKinds.Count == 0)
        {
            return Empty;
        }

        var byBlock = new Dictionary<(int X, int Y, int Z), Site>();
        var byLabel = new Dictionary<string, Site>(StringComparer.Ordinal);
        var nodes = new List<ResourceNode>();
        var claimedColumns = new HashSet<(int X, int Y, ResourceKind)>();
        var countsByKind = new Dictionary<ResourceKind, int>();

        foreach (PrismBlockCell block in grid.Blocks
                     .OrderBy(block => block.Y)
                     .ThenBy(block => block.X)
                     .ThenBy(block => block.Z))
        {
            if (!materialKinds.TryGetValue(block.MaterialId, out ResourceKind kind) ||
                !claimedColumns.Add((block.X, block.Y, kind)))
            {
                continue;
            }

            PrismBlockPosition? approach = null;
            foreach ((int dx, int dy) in Neighbourhood)
            {
                int x = block.X + dx;
                int y = block.Y + dy;
                if (grid.TryFeetZ(x, y, out int feetZ) && feetZ == block.Z)
                {
                    approach = new PrismBlockPosition(x, y, feetZ);
                    break;
                }
            }

            if (approach is not { } cell)
            {
                continue;
            }

            int ordinal = countsByKind.GetValueOrDefault(kind) + 1;
            countsByKind[kind] = ordinal;
            string label = string.Concat(
                "resource-",
                ResourceNodePlacement.KindName(kind),
                "-",
                ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture));
            var source = new PrismBlockPosition(block.X, block.Y, block.Z);
            var site = new Site(label, kind, cell, kind == ResourceKind.Water ? source : null);
            byBlock[(block.X, block.Y, block.Z)] = site;
            byLabel[label] = site;
            nodes.Add(new ResourceNode(
                label,
                kind,
                WorldSpace.CellCenter(cell.X, cell.Y),
                amount: 1.0,
                capacity: 1.0,
                regenerationRate: 0.0));
        }

        return nodes.Count == 0 ? Empty : new PrismResourceSites(byBlock, byLabel, nodes);
    }

    /// <summary>
    /// Décrit un bloc ressource perçu : libellé du gisement et position de sa
    /// case d'approche (la position que la cognition doit viser).
    /// </summary>
    public bool TryDescribe(int x, int y, int z, out string label, out Position approach)
    {
        if (_byBlock.TryGetValue((x, y, z), out Site site))
        {
            label = site.Label;
            approach = WorldSpace.CellCenter(site.Approach.X, site.Approach.Y);
            return true;
        }

        label = string.Empty;
        approach = default;
        return false;
    }

    /// <summary>
    /// Bloc à viser par une commande <c>Consommer</c> pour ce gisement. Faux pour
    /// un gisement non buvable : il n'y a rien à consommer dans une forêt.
    /// </summary>
    public bool TrySource(string label, out PrismBlockPosition source)
    {
        if (!string.IsNullOrEmpty(label) &&
            _byLabel.TryGetValue(label, out Site site) &&
            site.Source is { } block)
        {
            source = block;
            return true;
        }

        source = default;
        return false;
    }
}
