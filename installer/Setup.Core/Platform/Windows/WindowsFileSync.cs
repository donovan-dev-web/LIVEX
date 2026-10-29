namespace Setup.Core.Platform.Windows;

public sealed class WindowsFileSync : IFileSync
{
    public void Flush(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None);
            fs.Flush(true);
        }
        catch
        {
            // best effort
        }
    }
}