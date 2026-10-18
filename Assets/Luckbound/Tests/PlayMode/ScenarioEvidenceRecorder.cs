using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Luckbound.Tests.PlayMode
{
    /// <summary>
    /// Writes a reproducible scenario report and append-only log for local or CI test runs.
    /// </summary>
    public sealed class ScenarioEvidenceRecorder : IDisposable
    {
        private string[] _sourceFiles;
        private static readonly Dictionary<string, SourceSnapshot> SourceSnapshots =
            new Dictionary<string, SourceSnapshot>(StringComparer.Ordinal);
        private SourceSnapshot _sourceSnapshot;
        private readonly string _projectRoot;
        private bool _completed;

        public ScenarioEvidenceRecorder(string taskId, string scenarioId, int seed)
        {
            _projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string configuredRoot = Environment.GetEnvironmentVariable("LUCKBOUND_EVIDENCE_DIR");
            string evidenceRoot = string.IsNullOrWhiteSpace(configuredRoot)
                ? Path.Combine(_projectRoot, "Logs", "Scenarios")
                : Path.GetFullPath(configuredRoot);

            string runId = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture) +
                "_" + Sanitize(scenarioId) + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            EvidenceDirectory = Path.Combine(evidenceRoot, Sanitize(taskId), runId);
            Directory.CreateDirectory(EvidenceDirectory);

            Report = new ScenarioEvidenceReport
            {
                taskId = taskId,
                scenarioId = scenarioId,
                seed = seed,
                sourceCommit = Environment.GetEnvironmentVariable("GITHUB_SHA") ??
                    Environment.GetEnvironmentVariable("LUCKBOUND_SOURCE_COMMIT") ?? string.Empty,
                sourceHash = ComputeSourceHash(),
                sourceHashVersion = 2,
                sourceFileCount = _sourceFiles.Length,
                sourceSnapshotCapturedUtc = _sourceSnapshot.CapturedUtc,
                sourceWorkingTree = Environment.GetEnvironmentVariable("LUCKBOUND_SOURCE_STATE") ?? "unspecified",
                unityVersion = Application.unityVersion,
                platform = Application.platform.ToString(),
                graphicsDevice = SystemInfo.graphicsDeviceType.ToString(),
                startedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                evidenceDirectory = ToProjectRelativePath(EvidenceDirectory),
                screenshotStatus = "Not attempted",
                inputs = new List<ScenarioInputRecord>(),
                events = new List<string>(),
                assertions = new List<ScenarioAssertionRecord>(),
                errors = new List<string>(),
                artifacts = new List<string> { "source-manifest.sha256" }
            };

            AppendLog("START", "scenario=" + scenarioId + " task=" + taskId + " seed=" + seed);
        }

        public ScenarioEvidenceReport Report { get; }

        public string EvidenceDirectory { get; }

        public string ReportPath => Path.Combine(EvidenceDirectory, "scenario-report.json");

        public string LogPath => Path.Combine(EvidenceDirectory, "scenario.log");

        public string ScreenshotPath => Path.Combine(EvidenceDirectory, "screen.png");

        public string ScreenshotCapturePath => ToProjectRelativePath(ScreenshotPath).Replace('\\', '/');

        public void RecordInput(int frame, string action, string value)
        {
            Report.inputs.Add(new ScenarioInputRecord { frame = frame, action = action, value = value });
            AppendLog("INPUT", "frame=" + frame + " action=" + action + " value=" + value);
        }

        public void RecordEvent(string message)
        {
            Report.events.Add(message);
            AppendLog("EVENT", message);
        }

        public void RecordAssertion(string name, bool passed, string expected, string actual)
        {
            Report.assertions.Add(new ScenarioAssertionRecord
            {
                name = name,
                passed = passed,
                expected = expected,
                actual = actual
            });

            if (!passed)
            {
                string error = name + ": expected '" + expected + "', actual '" + actual + "'.";
                Report.errors.Add(error);
                AppendLog("FAIL", error);
            }
            else
            {
                AppendLog("PASS", name + ": " + actual);
            }
        }

        public void RecordFailure(string message)
        {
            Report.errors.Add(message);
            AppendLog("FAIL", message);
        }

        public void RecordScreenshotStatus(string status)
        {
            Report.screenshotStatus = status;
            AppendLog("CAPTURE", status);
            if (File.Exists(ScreenshotPath) && new FileInfo(ScreenshotPath).Length > 0)
            {
                string relativePath = ToEvidenceRelativePath(ScreenshotPath);
                if (!Report.artifacts.Contains(relativePath))
                {
                    Report.artifacts.Add(relativePath);
                }
            }
        }

        public int Complete()
        {
            if (_completed)
            {
                return Report.exitCode;
            }

            _completed = true;
            Report.passed = Report.errors.Count == 0 && Report.assertions.All(assertion => assertion.passed);
            Report.exitCode = Report.passed ? 0 : 1;
            Report.completedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            Report.artifacts.Add("scenario.log");
            Report.artifacts.Add("scenario-report.json");
            AppendLog("RESULT", Report.passed ? "PASS exitCode=0" : "FAIL exitCode=1");
            File.WriteAllText(ReportPath, JsonUtility.ToJson(Report, true), new UTF8Encoding(false));
            return Report.exitCode;
        }

        public void Dispose()
        {
            if (!_completed)
            {
                RecordFailure("Scenario ended before Complete() was called.");
                Complete();
            }
        }

        private void AppendLog(string level, string message)
        {
            string line = "[" + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) + "] [" + level + "] " +
                message.Replace("\r", " ").Replace("\n", " ") + Environment.NewLine;
            File.AppendAllText(LogPath, line, new UTF8Encoding(false));
        }

        private string ComputeSourceHash()
        {
            // The coordinator freezes the checkout during this Editor execution; the runner verifies
            // the manifest again when tests end. Reuse that snapshot across scenarios in this domain.
            lock (SourceSnapshots)
            {
                if (!SourceSnapshots.TryGetValue(_projectRoot, out _sourceSnapshot))
                {
                    _sourceSnapshot = CaptureSourceSnapshot();
                    SourceSnapshots.Add(_projectRoot, _sourceSnapshot);
                }
            }
            _sourceFiles = _sourceSnapshot.Files;
            File.WriteAllText(Path.Combine(EvidenceDirectory, "source-manifest.sha256"),
                _sourceSnapshot.Manifest, new UTF8Encoding(false));
            return _sourceSnapshot.Hash;
        }

        private SourceSnapshot CaptureSourceSnapshot()
        {
            string capturedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            string[] files = new[] { "Assets/Luckbound", "Assets/Resources/Luckbound", "Assets/Settings", "Packages", "ProjectSettings" }
                .SelectMany(root => Directory.Exists(Path.Combine(_projectRoot, root))
                    ? Directory.EnumerateFiles(Path.Combine(_projectRoot, root), "*", SearchOption.AllDirectories)
                    : Enumerable.Empty<string>())
                .Select(path => path.Substring(_projectRoot.Length + 1).Replace('\\', '/'))
                .OrderBy(path => path, StringComparer.Ordinal).ToArray();
            string material = string.Join("\n", files.Select(path =>
            {
                string absolutePath = Path.Combine(_projectRoot, path.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(absolutePath))
                {
                    return path + ":missing";
                }

                using (SHA256 sha256 = SHA256.Create())
                {
                    string hash = string.Concat(sha256.ComputeHash(File.ReadAllBytes(absolutePath))
                        .Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
                    return path + ":" + hash;
                }
            }));

            using (SHA256 sha256 = SHA256.Create())
            {
                string hash = string.Concat(sha256.ComputeHash(Encoding.UTF8.GetBytes(material))
                    .Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
                return new SourceSnapshot(files, material, hash, capturedUtc);
            }
        }

        private sealed class SourceSnapshot
        {
            public SourceSnapshot(string[] files, string manifest, string hash, string capturedUtc)
            {
                Files = files;
                Manifest = manifest;
                Hash = hash;
                CapturedUtc = capturedUtc;
            }
            public string[] Files { get; }
            public string Manifest { get; }
            public string Hash { get; }
            public string CapturedUtc { get; }
        }

        private string ToProjectRelativePath(string absolutePath)
        {
            Uri rootUri = new Uri(_projectRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
            Uri fileUri = new Uri(absolutePath);
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(fileUri).ToString()).Replace('/', Path.DirectorySeparatorChar);
        }

        private string ToEvidenceRelativePath(string absolutePath)
        {
            return Path.GetFileName(absolutePath);
        }

        private static string Sanitize(string value)
        {
            string sanitized = value ?? string.Empty;
            foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
            {
                sanitized = sanitized.Replace(invalidCharacter, '_');
            }
            return sanitized.Replace(' ', '-');
        }
    }

    [Serializable]
    public sealed class ScenarioEvidenceReport
    {
        public string taskId;
        public string scenarioId;
        public int seed;
        public string sourceCommit;
        public string sourceHash;
        public int sourceHashVersion;
        public int sourceFileCount;
        public string sourceSnapshotCapturedUtc;
        public string sourceWorkingTree;
        public string unityVersion;
        public string platform;
        public string graphicsDevice;
        public string startedUtc;
        public string completedUtc;
        public string evidenceDirectory;
        public string screenshotStatus;
        public bool passed;
        public int exitCode;
        public List<ScenarioInputRecord> inputs;
        public List<string> events;
        public List<ScenarioAssertionRecord> assertions;
        public List<string> errors;
        public List<string> artifacts;
    }

    [Serializable]
    public sealed class ScenarioInputRecord
    {
        public int frame;
        public string action;
        public string value;
    }

    [Serializable]
    public sealed class ScenarioAssertionRecord
    {
        public string name;
        public bool passed;
        public string expected;
        public string actual;
    }
}
