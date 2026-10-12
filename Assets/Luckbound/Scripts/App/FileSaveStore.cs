using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Luckbound.Core.Saves;

namespace Luckbound.App
{
    public interface IPersistentFileSystemSyncer
    {
        Task PrepareForReadAsync();
        Task FlushAsync();
    }

    /// <summary>Stores profile/settings independently using a flushed temp, a known-good backup, then replacement.</summary>
    public sealed class FileSaveStore : ISaveStore
    {
        public const string ProfileFileName = "luckbound-profile.json";
        public const string SettingsFileName = "luckbound-settings.json";

        private readonly string _directory;
        private readonly IPersistentFileSystemSyncer _syncer;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private bool _prepared;

        public FileSaveStore(string directory, IPersistentFileSystemSyncer syncer)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("A save directory is required.", nameof(directory));
            _directory = Path.GetFullPath(directory);
            _syncer = syncer ?? new ImmediateFileSystemSyncer();
        }

        public string GetPrimaryPath(SaveDocument document) => Path.Combine(_directory, FileName(document));
        public string GetBackupPath(SaveDocument document) => GetPrimaryPath(document) + ".bak";

        public async Task<SaveStorageSnapshot> ReadAsync(SaveDocument document)
        {
            await _gate.WaitAsync();
            try
            {
                await PrepareUnderLockAsync();
                string primaryPath = GetPrimaryPath(document);
                string backupPath = GetBackupPath(document);
                string primary = File.Exists(primaryPath) ? File.ReadAllText(primaryPath, Encoding.UTF8) : null;
                string backup = File.Exists(backupPath) ? File.ReadAllText(backupPath, Encoding.UTF8) : null;
                return new SaveStorageSnapshot(primary, backup);
            }
            finally { _gate.Release(); }
        }

        public async Task WriteAsync(SaveDocument document, string json, string lastKnownGoodPrimaryJson)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("A save document cannot be empty.", nameof(json));
            await _gate.WaitAsync();
            string primaryTemp = null;
            string backupTemp = null;
            try
            {
                await PrepareUnderLockAsync();
                Directory.CreateDirectory(_directory);
                string primaryPath = GetPrimaryPath(document);
                string backupPath = GetBackupPath(document);
                primaryTemp = primaryPath + ".tmp";
                WriteFlushed(primaryTemp, json);

                if (!string.IsNullOrWhiteSpace(lastKnownGoodPrimaryJson))
                {
                    backupTemp = backupPath + ".tmp";
                    WriteFlushed(backupTemp, lastKnownGoodPrimaryJson);
                    ReplaceFile(backupTemp, backupPath);
                    backupTemp = null;
                }

                ReplaceFile(primaryTemp, primaryPath);
                primaryTemp = null;
                await _syncer.FlushAsync();
            }
            finally
            {
                if (primaryTemp != null && File.Exists(primaryTemp)) File.Delete(primaryTemp);
                if (backupTemp != null && File.Exists(backupTemp)) File.Delete(backupTemp);
                _gate.Release();
            }
        }

        private async Task PrepareUnderLockAsync()
        {
            if (_prepared) return;
            await _syncer.PrepareForReadAsync();
            _prepared = true;
        }

        private static string FileName(SaveDocument document)
        {
            switch (document)
            {
                case SaveDocument.Profile: return ProfileFileName;
                case SaveDocument.Settings: return SettingsFileName;
                default: throw new ArgumentOutOfRangeException(nameof(document));
            }
        }

        private static void WriteFlushed(string path, string contents)
        {
            byte[] bytes = new UTF8Encoding(false).GetBytes(contents);
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096,
                FileOptions.SequentialScan))
            {
                stream.Write(bytes, 0, bytes.Length);
#if UNITY_WEBGL && !UNITY_EDITOR
                stream.Flush();
#else
                stream.Flush(true);
#endif
            }
        }

        private static void ReplaceFile(string temporaryPath, string destinationPath)
        {
            if (File.Exists(destinationPath))
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                File.Delete(destinationPath);
                File.Move(temporaryPath, destinationPath);
#else
                File.Replace(temporaryPath, destinationPath, null, ignoreMetadataErrors: true);
#endif
            }
            else
            {
                File.Move(temporaryPath, destinationPath);
            }
        }

        private sealed class ImmediateFileSystemSyncer : IPersistentFileSystemSyncer
        {
            public Task PrepareForReadAsync() => Task.CompletedTask;
            public Task FlushAsync() => Task.CompletedTask;
        }
    }
}
