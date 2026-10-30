namespace Setup.Core.Journal;

public enum JournalLevel { Debug, Info, Warn, Error }

public interface IJournal
{
    void Write(JournalLevel level, string step, string operation, string result, string? code = null, string? path = null);
    string LogPath { get; }
}