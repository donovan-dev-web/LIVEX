namespace Setup.Core.Preferences;

/// <summary>
/// Lecture/écriture atomique de settings.json, préservation des clés inconnues.
/// </summary>
public sealed class WorkspaceSettings
{
    public string? WorkspacePath { get; set; }
    public Dictionary<string, object?> Extra { get; } = new(StringComparer.Ordinal);

    public static WorkspaceSettings Read(string path)
    {
        var settings = new WorkspaceSettings();
        if (!File.Exists(path)) return settings;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.NameEquals("workspace"))
                {
                    settings.WorkspacePath = prop.Value.GetString();
                }
                else
                {
                    settings.Extra[prop.Name] = prop.Value.Clone();
                }
            }
        }
        catch
        {
            // Fichier invalide : archivage géré par l'appelant.
        }
        return settings;
    }

    public void Write(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        var obj = new Dictionary<string, object?>(Extra, StringComparer.Ordinal);
        if (WorkspacePath is not null) obj["workspace"] = WorkspacePath;
        var json = System.Text.Json.JsonSerializer.Serialize(obj, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(tmp, json, new System.Text.UTF8Encoding(false));
        File.Move(tmp, path, overwrite: true);
    }
}