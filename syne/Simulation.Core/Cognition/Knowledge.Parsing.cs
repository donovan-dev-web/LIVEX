// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// RÃ´le : Knowledge - partie Parsing.

using System.Globalization;
using Simulation.Core.Configuration;
using Simulation.Core.Prng;
using Simulation.Core.World;

namespace Simulation.Core.Cognition;

public static partial class KnowledgeBuilder
{

    /// <summary>Â« x.xx,y.yy,amount Â» â†’ position + quantitÃ© courante du gisement.</summary>
    public static bool TryParseNodeState(string value, out Position position, out double amount)
    {
        position = default;
        amount = 0.0;
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        string[] parts = value.Split(',');
        if (parts.Length < 3 ||
            !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double x) ||
            !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double y) ||
            !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out amount))
        {
            return false;
        }

        position = new Position(x, y);
        return true;
    }


    /// <summary>Â« x.xx,y.yy Â» â†’ position.</summary>
    public static bool TryParsePosition(string value, out Position position)
    {
        position = default;
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        string[] parts = value.Split(',');
        if (parts.Length < 2 ||
            !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double x) ||
            !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double y))
        {
            return false;
        }

        position = new Position(x, y);
        return true;
    }


    /// <summary>
    /// Contenu canonique d'une observation de gisement â€” mÃ©moire, souvenir et
    /// <b>message</b> partagent ce format : <c>node|label|x.xx|y.yy|amount.0|conf</c>.
    /// Un format unique est ce qui permet Ã  un message reÃ§u de devenir une croyance
    /// de plein droit (D-13).
    /// </summary>
    public static bool TryParseNodeMemory(
        string content,
        out string label,
        out Position position,
        out double amount,
        out double confidence)
    {
        label = string.Empty;
        position = default;
        amount = 0.0;
        confidence = 0.0;
        if (string.IsNullOrEmpty(content))
        {
            return false;
        }

        string[] parts = content.Split('|');
        if (parts.Length < 6 || !string.Equals(parts[0], "node", StringComparison.Ordinal))
        {
            return false;
        }

        if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double x) ||
            !double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double y) ||
            !double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out amount) ||
            !double.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out confidence))
        {
            return false;
        }

        label = parts[1];
        position = new Position(x, y);
        return true;
    }


    /// <summary>
    /// Convertit le contenu d'un <b>message compris</b> en fait observable :
    /// Â« entitÃ© en (x, y) Â» ou Â« gisement en (x, y), quantitÃ© a Â». Renvoie faux
    /// pour un contenu non interprÃ©table (le message reste un message, il ne
    /// produit pas de croyance).
    /// </summary>
    public static bool TryParseSharedObservation(string content, out Fact fact, out double payloadConfidence)
    {
        fact = default;
        payloadConfidence = 0.0;
        if (string.IsNullOrEmpty(content))
        {
            return false;
        }

        if (TryParseNodeMemory(content, out string label, out Position nodePosition, out double amount, out double nodeConfidence))
        {
            fact = new Fact(
                $"node-{label}",
                NodeStatePredicate,
                string.Concat(
                    nodePosition.X.ToString("0.00", CultureInfo.InvariantCulture),
                    ",",
                    nodePosition.Y.ToString("0.00", CultureInfo.InvariantCulture),
                    ",",
                    amount.ToString("0.###", CultureInfo.InvariantCulture)));
            payloadConfidence = nodeConfidence;
            return true;
        }

        string[] parts = content.Split('|');
        if (parts.Length >= 5 &&
            string.Equals(parts[0], "entity", StringComparison.Ordinal) &&
            double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double x) &&
            double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double y) &&
            double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out double confidence))
        {
            fact = new Fact(
                $"entity-{parts[1]}",
                EntityPositionPredicate,
                string.Concat(
                    x.ToString("0.00", CultureInfo.InvariantCulture),
                    ",",
                    y.ToString("0.00", CultureInfo.InvariantCulture)));
            payloadConfidence = confidence;
            return true;
        }

        return false;
    }


    /// <summary>Type de gisement dÃ©duit du libellÃ© canonique (Â« resource-water-2 Â» â†’ Water).</summary>
    public static ResourceKind? KindFromLabel(string label)
    {
        const string prefix = "resource-";
        if (!label.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        string rest = label[prefix.Length..];
        int dash = rest.LastIndexOf('-');
        string kindName = dash > 0 ? rest[..dash] : rest;
        foreach (ResourceKind kind in Enum.GetValues<ResourceKind>())
        {
            if (string.Equals(ResourceNodePlacement.KindName(kind), kindName, StringComparison.Ordinal))
            {
                return kind;
            }
        }

        return null;
    }


    private static ulong ParseEntityId(string subject)
    {
        const string prefix = "entity-";
        return subject.StartsWith(prefix, StringComparison.Ordinal) &&
               ulong.TryParse(subject[prefix.Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong id)
            ? id
            : 0UL;
    }
}
