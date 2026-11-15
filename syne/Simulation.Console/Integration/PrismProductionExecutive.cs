// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Exécutif de production couplé (SYNE-223) : plan de recettes → prochaine commande PRISM.

using Simulation.Core.Cognition;

namespace Simulation.Console.Integration;

/// <summary>Commande de production à traduire en commande PRISM.</summary>
public abstract record ProductionCommand;

/// <summary>Prélever une matière première par la macro-action d'acquisition de l'hôte.</summary>
public sealed record GatherProductionCommand(string Item, int Quantity) : ProductionCommand;

/// <summary>Fabriquer une recette ; <paramref name="StationItem"/> est nulle à mains nues.</summary>
public sealed record CraftProductionCommand(string RecipeId, string? StationItem) : ProductionCommand;

/// <summary>Poser la station fabriquée pour que la recette suivante l'utilise.</summary>
public sealed record PlaceProductionCommand(string StationItem) : ProductionCommand;

/// <summary>
/// Exécutif du <b>plan de production</b> (SYNE-223, étape 4) : il avance dans un
/// <see cref="ProductionPlan"/> dérivé du graphe de recettes et de l'inventaire
/// observé, et rend la prochaine commande à émettre. Il ne contient aucune chaîne
/// d'étapes écrite : le plan vient de <see cref="ProductionPlanner"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Avancement par les faits, pas par une horloge.</b> Une étape n'est franchie
/// que lorsque l'hôte publie <c>action_completed</c> pour la commande émise (le
/// programme appelant lui transmet l'identifiant). Tant qu'une commande est en
/// vol, aucune autre n'est émise : une macro-action dure des dizaines de ticks et
/// la redemander à chaque fenêtre ne ferait que produire des refus.
/// </para>
/// <para>
/// <b>Échec borné, et prospecté.</b> Un refus arme un délai de réessai ; au-delà de
/// <see cref="MaxStepFailures"/> refus sur la même étape, le plan est abandonné
/// plutôt que de boucler indéfiniment sur une matière absente. Le refus
/// <c>source_not_found</c> est traité à part : il ne dit pas « non » mais « je ne
/// perçois rien ». Réessayer depuis la même case ne peut pas mieux finir, donc
/// l'exécutif expose la matière manquante (<see cref="TrySearch"/>) pour que
/// l'appelant conduise l'agent vers un gisement connu de cette famille.
/// </para>
/// <para>
/// <b>Inventaire observé.</b> Ce qui est déjà porté n'est ni prélevé ni fabriqué
/// à nouveau : le plan est construit avec l'inventaire du moment, et un
/// prélèvement déjà couvert est sauté.
/// </para>
/// </remarks>
public sealed class PrismProductionExecutive
{
    /// <summary>Nombre de refus tolérés sur une même étape avant abandon du plan.</summary>
    public const int MaxStepFailures = 5;

    /// <summary>
    /// Durée d'une prospection, en ticks : le temps d'aller vers un gisement
    /// connu et d'y percevoir la matière.
    /// </summary>
    public const int SearchTicks = 240;

    private readonly IReadOnlyList<ProductionRecipe> _recipes;
    private readonly int _maxPlanNodes;
    private readonly int _retryTicks;
    private readonly Dictionary<ulong, ProductionRun> _runs = [];

    public PrismProductionExecutive(
        IReadOnlyList<ProductionRecipe> recipes,
        int maxPlanNodes = 128,
        int retryTicks = 30)
    {
        ArgumentNullException.ThrowIfNull(recipes);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxPlanNodes, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(retryTicks, 0);
        _recipes = recipes;
        _maxPlanNodes = maxPlanNodes;
        _retryTicks = retryTicks;
    }

    /// <summary>Plans menés jusqu'au bout (objet final observé dans l'inventaire).</summary>
    public int CompletedRuns { get; private set; }

    /// <summary>Plans abandonnés (matière introuvable, étape refusée trop souvent).</summary>
    public int AbandonedRuns { get; private set; }

    /// <summary>Objectifs pour lesquels le pack ne fournit aucune recette.</summary>
    public SortedSet<string> UnsupportedGoals { get; } = new(StringComparer.Ordinal);

    /// <summary>Objectifs accomplis au moins une fois (détail du bilan de run).</summary>
    public SortedSet<string> CompletedGoals { get; } = new(StringComparer.Ordinal);

    /// <summary>Vrai si un plan est en cours pour cet agent.</summary>
    public bool IsRunning(ulong agentId) => _runs.ContainsKey(agentId);

    /// <summary>Index de l'étape courante (pour l'identifiant de commande), ou -1.</summary>
    public int CurrentStepIndex(ulong agentId) =>
        _runs.TryGetValue(agentId, out ProductionRun? run) ? run.Index : -1;

    /// <summary>
    /// Prochaine commande à émettre pour <paramref name="goal"/>, ou <c>false</c>
    /// s'il faut attendre (commande en vol, délai de réessai, plan terminé).
    /// </summary>
    public bool TryNext(
        ulong agentId,
        string goal,
        IReadOnlyDictionary<string, int> carried,
        long tick,
        out ProductionCommand command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(goal);
        ArgumentNullException.ThrowIfNull(carried);
        command = null!;

        if (carried.TryGetValue(goal, out int goalQuantity) && goalQuantity > 0)
        {
            if (_runs.Remove(agentId))
            {
                CompletedRuns++;
                CompletedGoals.Add(goal);
            }

            return false;
        }

        if (!_runs.TryGetValue(agentId, out ProductionRun? run) ||
            !string.Equals(run.Goal, goal, StringComparison.Ordinal))
        {
            if (run is not null)
            {
                _runs.Remove(agentId);
            }

            // Un objectif de production doit être produit par une recette : sans
            // cela, « fabriquer une pioche » deviendrait « ramasser une pioche »,
            // qui n'existe pas dans le monde. Le pack le dit ; on ne l'invente pas.
            if (!_recipes.Any(recipe => recipe.OutputQuantity(goal) > 0))
            {
                UnsupportedGoals.Add(goal);
                return false;
            }

            ProductionPlan plan = ProductionPlanner.Build(goal, 1, _recipes, carried, _maxPlanNodes);
            if (plan.IsEmpty)
            {
                // Aucune recette ne produit l'objet et il n'est pas porté : le
                // pack ne permet pas cet objectif. Le dire vaut mieux que de
                // laisser l'agent tenter de « prélever » un objet fabriqué.
                UnsupportedGoals.Add(goal);
                return false;
            }

            run = new ProductionRun(goal, plan);
            _runs[agentId] = run;
        }

        if (run.PendingCommandId is not null || tick < run.RetryAtTick)
        {
            return false;
        }

        while (run.Index < run.Plan.Steps.Count)
        {
            ProductionStep step = run.Plan.Steps[run.Index];
            if (step is GatherStep gather &&
                carried.GetValueOrDefault(gather.Item) >= gather.Quantity)
            {
                run.Index++;
                continue;
            }

            if (step is PlaceStep && run.StationPlaced)
            {
                run.Index++;
                continue;
            }

            command = step switch
            {
                GatherStep missing => new GatherProductionCommand(
                    missing.Item,
                    Math.Max(1, missing.Quantity - carried.GetValueOrDefault(missing.Item))),
                CraftStep craft => new CraftProductionCommand(craft.RecipeId, craft.Station),
                PlaceStep place => new PlaceProductionCommand(place.StationItem),
                _ => throw new InvalidOperationException($"Étape de production inconnue : {step.GetType().Name}."),
            };
            return true;
        }

        // Plan épuisé sans que l'objet final soit observé : la dernière étape a
        // peut-être été perdue, mais insister n'a plus de sens.
        _runs.Remove(agentId);
        AbandonedRuns++;
        return false;
    }

    /// <summary>Enregistre la commande émise : aucune autre ne partira avant son issue.</summary>
    public void MarkIssued(ulong agentId, string commandId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
        if (_runs.TryGetValue(agentId, out ProductionRun? run))
        {
            run.PendingCommandId = commandId;
        }
    }

    /// <summary>L'hôte a publié <c>action_completed</c> : l'étape est franchie.</summary>
    public void MarkCompleted(ulong agentId, string commandId, long tick)
    {
        if (!_runs.TryGetValue(agentId, out ProductionRun? run) ||
            !string.Equals(run.PendingCommandId, commandId, StringComparison.Ordinal))
        {
            return;
        }

        run.PendingCommandId = null;
        run.Failures = 0;
        run.RetryAtTick = tick;
        // L'étape a abouti : la source est trouvée, la prospection n'a plus d'objet.
        run.SearchItem = null;
        if (run.Index < run.Plan.Steps.Count &&
            run.Plan.Steps[run.Index] is PlaceStep placed)
        {
            run.StationPlaced = true;
            run.StationItem = placed.StationItem;
        }

        run.Index++;
    }

    /// <summary>L'hôte a publié <c>action_failed</c> : réarmer l'étape, ou abandonner.</summary>
    public void MarkFailed(ulong agentId, string commandId, string reason, long tick)
    {
        if (!_runs.TryGetValue(agentId, out ProductionRun? run) ||
            !string.Equals(run.PendingCommandId, commandId, StringComparison.Ordinal))
        {
            return;
        }

        run.PendingCommandId = null;
        run.Failures++;
        run.RetryAtTick = tick + _retryTicks;

        // `source_not_found` n'est pas un refus de l'hôte : c'est un agent qui ne
        // perçoit aucune source. Réessayer la même commande depuis la même case
        // ne peut pas mieux finir ; il faut aller voir ailleurs. L'étape courante
        // dit quelle matière manque.
        if (string.Equals(reason, "source_not_found", StringComparison.Ordinal) &&
            run.Index < run.Plan.Steps.Count &&
            run.Plan.Steps[run.Index] is GatherStep gather)
        {
            run.SearchItem = gather.Item;
            run.SearchUntilTick = tick + SearchTicks;
        }

        if (run.Failures >= MaxStepFailures)
        {
            _runs.Remove(agentId);
            AbandonedRuns++;
        }
    }

    /// <summary>
    /// Matière à prospecter, si une récolte vient d'échouer faute de source :
    /// l'appelant conduit l'agent vers un gisement de cette famille. La
    /// prospection expire d'elle-même après <see cref="SearchTicks"/>.
    /// </summary>
    public bool TrySearch(ulong agentId, long tick, out string item)
    {
        item = string.Empty;
        if (!_runs.TryGetValue(agentId, out ProductionRun? run) ||
            run.SearchItem is null ||
            tick >= run.SearchUntilTick)
        {
            return false;
        }

        item = run.SearchItem;
        return true;
    }

    /// <summary>
    /// L'inventaire observé porte l'objet final : la chaîne est <b>accomplie</b>. À
    /// appeler à chaque observation, sans attendre une nouvelle décision de
    /// production — l'objet satisfait le désir, donc <see cref="TryNext"/> ne
    /// serait plus rappelé et l'accomplissement passerait inaperçu.
    /// </summary>
    public void ObserveInventory(ulong agentId, IReadOnlyDictionary<string, int> carried)
    {
        ArgumentNullException.ThrowIfNull(carried);
        if (_runs.TryGetValue(agentId, out ProductionRun? run) &&
            carried.GetValueOrDefault(run.Goal) > 0)
        {
            _runs.Remove(agentId);
            CompletedRuns++;
            CompletedGoals.Add(run.Goal);
        }
    }

    /// <summary>Oublie un agent (arrêt, restauration, nouveau run).</summary>
    public void Forget(ulong agentId) => _runs.Remove(agentId);

    /// <summary>Vrai si la station de l'agent est posée (pour choisir la cible du craft).</summary>
    public bool HasStation(ulong agentId) =>
        _runs.TryGetValue(agentId, out ProductionRun? run) && run.StationPlaced;

    /// <summary>Objet-station posé par l'agent, ou <c>null</c>.</summary>
    public string? StationItem(ulong agentId) =>
        _runs.TryGetValue(agentId, out ProductionRun? run) ? run.StationItem : null;

    private sealed class ProductionRun
    {
        public ProductionRun(string goal, ProductionPlan plan)
        {
            Goal = goal;
            Plan = plan;
        }

        public string Goal { get; }

        public ProductionPlan Plan { get; }

        public int Index { get; set; }

        public string? PendingCommandId { get; set; }

        public int Failures { get; set; }

        public long RetryAtTick { get; set; }

        public bool StationPlaced { get; set; }

        public string? StationItem { get; set; }

        public string? SearchItem { get; set; }

        public long SearchUntilTick { get; set; }
    }
}
