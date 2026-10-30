namespace Setup.Core.Platform;

public interface IFileSync
{
    void Flush(string path);
}