// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : SimulationOptionsValidator - helpers.

namespace Simulation.Core.Configuration;

public static partial class SimulationOptionsValidator
{
    private static IReadOnlyList<string> FindNonFiniteNumbers(object root)
    {
        var errors = new List<string>();
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        Walk(root, string.Empty, errors, visited, depth: 0);
        return errors;
    }

    private static void Walk(
        object node,
        string path,
        List<string> errors,
        HashSet<object> visited,
        int depth)
    {
        // Garde-fou : un graphe cyclique ne doit pas faire boucler la validation.
        if (node is null || depth > 12 || !visited.Add(node))
        {
            return;
        }

        Type type = node.GetType();
        if (type.IsPrimitive || node is string or decimal)
        {
            return;
        }

        if (node is System.Collections.IDictionary dictionary)
        {
            foreach (System.Collections.DictionaryEntry entry in dictionary)
            {
                string key = entry.Key?.ToString() ?? "?";
                if (entry.Value is double mapValue)
                {
                    ReportNonFinite(mapValue, Combine(path, key), errors);
                }
                else if (entry.Value is not null)
                {
                    Walk(entry.Value, Combine(path, key), errors, visited, depth + 1);
                }
            }

            return;
        }

        if (node is System.Collections.IEnumerable sequence)
        {
            int index = 0;
            foreach (object? item in sequence)
            {
                Walk(item, Combine(path, $"[{index}]"), errors, visited, depth + 1);
                index++;
            }

            return;
        }

        foreach (System.Reflection.PropertyInfo property in type.GetProperties())
        {
            if (property.GetIndexParameters().Length > 0 || !property.CanRead)
            {
                continue;
            }

            object? value;
            try
            {
                value = property.GetValue(node);
            }
            catch (System.Reflection.TargetInvocationException)
            {
                // Propriété calculée qui lève : hors périmètre de la validation.
                continue;
            }

            // Les chemins suivent la casse du contrat de configuration (camelCase
            // comme dans config.json), et non les noms de propriétés C#.
            string child = Combine(path, ToCamelCase(property.Name));
            if (value is double doubleValue)
            {
                ReportNonFinite(doubleValue, child, errors);
            }
            else if (value is float floatValue)
            {
                ReportNonFinite(floatValue, child, errors);
            }
            else if (value is not null)
            {
                Walk(value, child, errors, visited, depth + 1);
            }
        }
    }

    private static string Combine(string path, string segment)
    {
        if (string.IsNullOrEmpty(path))
        {
            return segment;
        }

        // Un indice de collection se colle au chemin (« zones[0] ») sans point.
        return segment.StartsWith('[') ? $"{path}{segment}" : $"{path}.{segment}";
    }

    private static string ToCamelCase(string name) =>
        string.IsNullOrEmpty(name) || char.IsLower(name[0]) ? name : char.ToLowerInvariant(name[0]) + name[1..];

    private static void ReportNonFinite(double value, string path, List<string> errors)
    {
        if (double.IsFinite(value))
        {
            return;
        }

        string rendered = double.IsNaN(value) ? "NaN" : value > 0 ? "Infinity" : "-Infinity";
        errors.Add($"{path} doit être un nombre fini (reçu : {rendered}).");
    }
}
