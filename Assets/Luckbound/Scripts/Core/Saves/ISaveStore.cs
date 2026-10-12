using System.Threading.Tasks;

namespace Luckbound.Core.Saves
{
    public enum SaveDocument
    {
        Profile = 0,
        Settings = 1
    }

    public sealed class SaveStorageSnapshot
    {
        public SaveStorageSnapshot(string primaryJson, string backupJson)
        {
            PrimaryJson = primaryJson;
            BackupJson = backupJson;
        }

        public string PrimaryJson { get; }
        public string BackupJson { get; }
    }

    /// <summary>Async persistence boundary. A null last-known-good document preserves the current backup.</summary>
    public interface ISaveStore
    {
        Task<SaveStorageSnapshot> ReadAsync(SaveDocument document);
        Task WriteAsync(SaveDocument document, string json, string lastKnownGoodPrimaryJson);
    }
}
