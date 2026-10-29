// SPDX-License-Identifier: GPL-3.0-or-later
namespace Setup.Core.Journal;

/// <summary>
/// Journal mémoire : conserve les événements sans écrire sur le disque.
/// Utilisé par le mode simulation, les tests, et l'écran de résultat.
/// </summary>
public sealed class InMemoryJournal : IJournal
{
    private readonly List<string> _lines = new();

    public string LogPath => "(simulation)";

    public IReadOnlyList<string> Lines
    {
        get { lock (_lines) { return _lines.ToArray(); } }
    }

    public void Write(JournalLevel level, string step, string operation, string result, string? code = null, string? path = null)
    {
        var line = Journal.Format(level, step, operation, result, code, path).TrimEnd();
        lock (_lines) { _lines.Add(line); }
    }
}

/// <summary>
/// Journal qui n'enregistre rien. Aucun fichier, aucun répertoire.
/// </summary>
public sealed class NullJournal : IJournal
{
    public static readonly NullJournal Instance = new();

    public string LogPath => "(aucun journal)";

    public void Write(JournalLevel level, string step, string operation, string result, string? code = null, string? path = null)
    {
        // volontairement vide
    }
}
