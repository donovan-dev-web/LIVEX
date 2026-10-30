// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text;

namespace Setup.Core.Journal;

/// <summary>
/// Journal texte UTF-8, une ligne par événement.
/// Le fichier n'est créé qu'au premier événement : construire un journal
/// n'a aucun effet de bord (indispensable au mode simulation).
/// </summary>
public sealed class Journal : IJournal
{
    private readonly string _logDirectory;
    private readonly string? _explicitPath;
    private readonly object _lock = new();
    private string? _logPath;

    public Journal(string logDirectory, string? explicitPath = null)
    {
        _logDirectory = logDirectory;
        _explicitPath = explicitPath;
    }

    public string LogPath => _logPath ??= _explicitPath ?? Path.Combine(_logDirectory, $"installer-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log");

    public void Write(JournalLevel level, string step, string operation, string result, string? code = null, string? path = null)
    {
        var line = Format(level, step, operation, result, code, path);
        lock (_lock)
        {
            // Création paresseuse du répertoire et du fichier.
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, line, new UTF8Encoding(false));
        }
    }

    internal static string Format(JournalLevel level, string step, string operation, string result, string? code, string? path)
    {
        var sb = new StringBuilder();
        sb.Append(DateTime.UtcNow.ToString("O"));
        sb.Append(' ');
        sb.Append(level.ToString().ToUpperInvariant());
        sb.Append(" step=").Append(step);
        sb.Append(" op=").Append(operation);
        sb.Append(" result=").Append(result);
        if (!string.IsNullOrEmpty(code)) sb.Append(" code=").Append(code);
        if (!string.IsNullOrEmpty(path)) sb.Append(" path=").Append(path);
        sb.AppendLine();
        return sb.ToString();
    }
}
