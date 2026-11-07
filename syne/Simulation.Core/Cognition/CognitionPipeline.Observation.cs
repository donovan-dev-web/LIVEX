// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : CognitionPipeline - partie Observation.

namespace Simulation.Core.Cognition;

using System.Globalization;
using Simulation.Core.Actions;
using Simulation.Core.Communication;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Perception;
using Simulation.Core.Performance;
using Simulation.Core.Population;
using Simulation.Core.Social;

public sealed partial class CognitionPipeline
{
    /// <summary>
    /// Applique des observations externes (par exemple perçues par PRISM) aux
    /// croyances et à la mémoire d'une entité, sans passer par la perception
    /// locale de SYNE. Utilisé par la boucle couplée SYNE ↔ PRISM (R2).
    /// </summary>
    public void ApplyExternalObservations(
        Entity self,
        IReadOnlyList<Observation> observations,
        ulong currentTick,
        string source = "prism")
    {
        ArgumentNullException.ThrowIfNull(self);
        ArgumentNullException.ThrowIfNull(observations);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        MindState mind = GetOrCreate(self.Id.Value, self);
        mind.Factors ??= new AgentFactors(self.Traits);
        foreach (Observation observation in observations)
        {
            StoreObservation(mind, self, observation, currentTick, source);
        }
    }

    /// <summary>
    /// Applique des <b>résumés de région</b> (SYNE-221) à la mémoire spatiale
    /// d'une entité : un masque de tags par chunk, avec sa confiance et son
    /// horodatage. Utilisé par la boucle couplée (PRISM publie ses résumés de
    /// région dans les observations différentielles) ; aucun effet sur les
    /// croyances ni sur la mémoire épisodique — un résumé n'est pas un fait
    /// ponctuel.
    /// </summary>
    public void ApplyExternalSpatialObservations(
        Entity self,
        IReadOnlyList<SpatialChunkObservation> chunks,
        ulong currentTick)
    {
        ArgumentNullException.ThrowIfNull(self);
        ArgumentNullException.ThrowIfNull(chunks);
        if (chunks.Count == 0)
        {
            return;
        }

        MindState mind = GetOrCreate(self.Id.Value, self);
        foreach (SpatialChunkObservation chunk in chunks)
        {
            if (chunk.TagMask == 0)
            {
                // Aucun tag exposé : rien à mémoriser. Un chunk déjà cru n'est pas
                // effacé ici — c'est la décroissance temporelle qui l'oublie.
                continue;
            }

            mind.Spatial.Observe(chunk.ChunkX, chunk.ChunkY, chunk.TagMask, _options.Agents.Beliefs, currentTick);
        }
    }

    /// <summary>
    /// Apprend les recettes déclarées dans un livre lu (SYNE-224) : le savoir
    /// enseigné devient durable et daté, exactement comme la connaissance
    /// initiale — mais sa source dit <b>de quel livre</b> il vient.
    /// </summary>
    /// <returns>Nombre de recettes apprises (0 si le livre n'en déclare aucune).</returns>
    public int LearnFromBook(Simulation.Core.World.Book book, ulong readerId, ulong currentTick)
    {
        ArgumentNullException.ThrowIfNull(book);
        if (!_options.Agents.Knowledge.LearnFromBooks || !HasMind(readerId))
        {
            return 0;
        }

        return KnowledgeBuilder.LearnRecipeKnowledge(
            MindOf(readerId).Beliefs,
            string.Concat("book:", book.Id),
            book.Content,
            currentTick);
    }

    /// <summary>
    /// Stockage d'une observation : <b>mémoire</b> (récit canonique, source) puis
    /// <b>croyance</b> révisée par la formule du chapitre XI
    /// (<see cref="BeliefSet.ApplyObservation"/>) — croyance de <b>gisement</b>
    /// (<c>node-&lt;label&gt;</c> → « x,y,amount ») ou de <b>position d'entité</b>
    /// (<c>entity-&lt;id&gt;</c>). C'est la chaîne
    /// <c>perception → mémoire → croyance</c> rendue effective (S-02/S-07/S-08).
    /// </summary>
    private void StoreObservation(MindState mind, Entity self, Observation observation, ulong currentTick, string source)
    {
        // Le récit canonique n'est <b>formaté qu'à l'écriture effective</b> : la
        // déduplication ci-dessous écarte la plupart des réobservations d'un
        // voisinage stable, et formater d'abord payait une chaîne par observation
        // perçue pour un contenu aussitôt jeté (le formatage est pur : le déplacer
        // ne change ni la mémoire ni les croyances).
        bool storeInMemory = true;

        if (observation.EntityType == "entity")
        {
            Fact position = new(EntitySubject(observation.EntityId), KnowledgeBuilder.EntityPositionPredicate, CachedPosition(observation));

            // Déduplication par la croyance (même règle que les gisements) : une
            // position <b>déjà crue et non expirée</b> n'est pas réécrite en mémoire.
            // Sans cela, un voisinage stable réobservé à chaque rotation écrirait un
            // souvenir par entité et par passage — remplissait la mémoire et payait
            // une éviction de capacité à chaque écriture — pour une information déjà
            // connue. La croyance est, elle, rafraîchie à chaque passage (confiance,
            // âge) : c'est le fait qui fait foi.
            storeInMemory = !(mind.Beliefs.TryGet(position, out Belief? known) && !known.IsExpired(currentTick));
            mind.Beliefs.ApplyObservation(position, observation.Confidence, source, _options.Agents.Beliefs, currentTick);
        }
        else if (observation.EntityType == "node")
        {
            string label = observation.Attributes.TryGetValue("label", out string? resolved)
                ? resolved
                : observation.EntityId.ToString(CultureInfo.InvariantCulture);
            string amount = observation.Attributes.TryGetValue("amount", out string? quantity) ? quantity : "0";
            Fact state = new(
                $"node-{label}",
                KnowledgeBuilder.NodeStatePredicate,
                string.Concat(CanonicalPosition(observation.Position), ",", amount));

            // Déduplication par la croyance (O(1), sans état caché : la croyance est
            // persistée) : un gisement dont l'état est <b>déjà cru et non expiré</b>
            // n'est pas réécrit en mémoire. Sans cela, un repère statique réobservé à
            // chaque passage écrit un souvenir par agent et par rotation, remplit la
            // mémoire et paie une éviction de capacité — pour une information déjà
            // connue. La croyance, elle, est rafraîchie à chaque passage (confiance, âge).
            storeInMemory = !(mind.Beliefs.TryGet(state, out Belief? known) && !known.IsExpired(currentTick));
            mind.Beliefs.ApplyObservation(state, observation.Confidence, source, _options.Agents.Beliefs, currentTick);
        }
        else if (observation.EntityType == "site")
        {
            // Chantier (SYNE-222) : deux croyances distinctes sur le même sujet —
            // où il en est (position + état) et combien de travail reste. Un chantier
            // n'est pas un gisement : il ne doit pas entrer dans les cibles de
            // ressource, donc il a son propre sujet `site-<id>`.
            string label = observation.Attributes.TryGetValue("label", out string? siteLabel) && siteLabel.Length > 0
                ? siteLabel
                : observation.EntityId.ToString(CultureInfo.InvariantCulture);
            string state = observation.Attributes.TryGetValue("state", out string? siteState) ? siteState : "0";
            Fact stateFact = new(
                $"site-{label}",
                KnowledgeBuilder.SiteStatePredicate,
                string.Concat(CanonicalPosition(observation.Position), ",", state));

            string completed = observation.Attributes.TryGetValue("amount", out string? workDone) ? workDone : "0";
            string required = observation.Attributes.TryGetValue("required", out string? workNeeded) ? workNeeded : "0";
            Fact progressFact = new(
                $"site-{label}",
                KnowledgeBuilder.SiteProgressPredicate,
                string.Concat(completed, ",", required));

            storeInMemory = !(mind.Beliefs.TryGet(stateFact, out Belief? knownSite) && !knownSite.IsExpired(currentTick));
            mind.Beliefs.ApplyObservation(stateFact, observation.Confidence, source, _options.Agents.Beliefs, currentTick);
            mind.Beliefs.ApplyObservation(progressFact, observation.Confidence, source, _options.Agents.Beliefs, currentTick);
        }
        else if (observation.EntityType == "structure")
        {
            // Effet d'abri éprouvé (SYNE-222) : l'agent s'est trouvé dans la
            // structure, il en connaît le bonus. Sert de fait justificatif au
            // planificateur (SYNE-223), pas encore d'objectif.
            string label = observation.Attributes.TryGetValue("label", out string? structureLabel) && structureLabel.Length > 0
                ? structureLabel
                : observation.EntityId.ToString(CultureInfo.InvariantCulture);
            string bonus = observation.Attributes.TryGetValue("shelterBonusPermille", out string? permille)
                ? permille
                : "0";
            Fact shelter = new($"structure-{label}", KnowledgeBuilder.StructureShelterPredicate, bonus);
            mind.Beliefs.ApplyObservation(shelter, observation.Confidence, source, _options.Agents.Beliefs, currentTick);
        }
        else if (observation.EntityType == "obstacle")
        {
            // Le rayon n'est pas toujours fourni (une observation PRISM décrit un
            // bloc par son matériau et son altitude) : un index manquant levait une
            // KeyNotFoundException qui faisait échouer tout le tick couplé.
            string radius = observation.Attributes.TryGetValue("radius", out string? knownRadius)
                ? knownRadius
                : "0";
            Fact obstacle = new($"obstacle-{observation.EntityId}", "radius", radius);
            mind.Beliefs.ApplyObservation(obstacle, observation.Confidence, source, _options.Agents.Beliefs, currentTick);
        }

        if (storeInMemory)
        {
            mind.Memory.Store(MemoryCategory.Observation, EntitySubject(self.Id.Value), FormatContent(observation), observation.Confidence, currentTick);
        }
    }


    /// <summary>
    /// Partage social (SYNE-050, V0.1) : une entité sociable (trait ≥ 0.5) qui
    /// perçoit une entité vivante ou un gisement émet une pulsation
    /// <b>Information</b> publique — une au plus par tick (bien sous
    /// <c>maxSendsPerTick</c>). La décision de partage ne consomme aucun tirage du
    /// PRNG global. Le partage automatique est une <b>loi assumée</b> (arbitrage
    /// Phase 0) : son contenu est un témoignage interprétable par le récepteur, qui
    /// en fait une croyance dégradée (<see cref="ApplyReceivedMessages"/>).
    /// </summary>
    private static bool WantsToShare(AgentFactors factors, Observation observation)
    {
        if (observation.EntityType is not ("entity" or "node"))
        {
            return false;
        }

        // Facteurs déjà résolus pour l'entité (aucune recherche de trait par clé
        // textuelle par observation perçue).
        return factors.Sociability >= 0.5;
    }


    private void EnqueueSharePulse(Entity entity, Observation observation, MindState mind, ulong currentTick)
    {
        // Le payload porte le contenu canonique de l'observation : le récepteur
        // peut en faire une croyance de plein droit (provenance = émetteur).
        Message share = CommunicationSystem.CreateMessage(
            entity.Id.Value,
            targetId: null,
            MessageType.Information,
            FormatContent(observation),
            currentTick,
            sequence: 1);

        // File sortante bornée : au-delà de maxSendsPerTick en attente, la pulsance
        // la plus ancienne est abandonnée au profit de la plus récente (une
        // perception périmée n'a plus d'intérêt et la file ne doit pas croître).
        mind.Communication.Enqueue(share, _options.Communication.MaxSendsPerTick);
    }


    /// <summary>
    /// Récit canonique d'une observation, invariant à la culture — partagé par la
    /// mémoire et les messages : <c>entity|id|x.xx|y.yy|conf</c> ou
    /// <c>node|label|x.xx|y.yy|amount.0|conf</c>.
    /// </summary>
    private static string FormatContent(Observation observation)
    {
        // Interpolation à gestionnaire (une seule allocation, format invariant à la
        // culture) au lieu d'un tableau de fragments + 
        // <c>string.Join</c> : ce chemin est parcouru par observation perçue.
        if (observation.EntityType == "node")
        {
            string label = observation.Attributes.TryGetValue("label", out string? resolved) ? resolved : observation.EntityId.ToString(CultureInfo.InvariantCulture);
            string amount = observation.Attributes.TryGetValue("amount", out string? quantity) ? quantity : "0";
            return string.Create(
                CultureInfo.InvariantCulture,
                $"node|{label}|{observation.Position.X:0.00}|{observation.Position.Y:0.00}|{amount}|{observation.Confidence:0.000}");
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{observation.EntityType}|{observation.EntityId}|{observation.Position.X:0.00}|{observation.Position.Y:0.00}|{observation.Confidence:0.000}");
    }


    /// <summary>Position canonique d'un fait : « X,Y » invariant à la culture.</summary>
    private static string CanonicalPosition(Simulation.Core.World.Position position) =>
        string.Create(CultureInfo.InvariantCulture, $"{position.X:0.00},{position.Y:0.00}");


    /// <summary>Sujet canonique « entity-N » d'une observation d'entité (mémoïsé par tick).</summary>
    private string EntitySubject(ulong entityId)
    {
        if (_entitySubjectCache.TryGetValue(entityId, out string? cached))
        {
            return cached;
        }

        string subject = string.Create(CultureInfo.InvariantCulture, $"entity-{entityId}");
        _entitySubjectCache[entityId] = subject;
        return subject;
    }


    /// <summary>Position canonique « X,Y » d'une observation d'entité (mémoïsée par tick).</summary>
    private string CachedPosition(Observation observation)
    {
        if (_positionCache.TryGetValue(observation.EntityId, out string? cached))
        {
            return cached;
        }

        string position = CanonicalPosition(observation.Position);
        _positionCache[observation.EntityId] = position;
        return position;
    }

}
