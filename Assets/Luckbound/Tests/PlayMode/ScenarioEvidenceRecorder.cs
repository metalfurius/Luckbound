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
        private static readonly string[] SourceFiles =
        {
            "ProjectSettings/ProjectVersion.txt",
            "ProjectSettings/TagManager.asset",
            "ProjectSettings/TimeManager.asset",
            "Assets/Luckbound/Scenes/Bootstrap.unity",
            "Assets/Luckbound/Scenes/Menu.unity",
            "Assets/Luckbound/Scenes/Expedition.unity",
            "Assets/Resources/Luckbound/InputSystem_Actions.inputactions",
            "Assets/Luckbound/Scripts/Core/Luckbound.Core.asmdef",
            "Assets/Luckbound/Scripts/Core/Input/PlayerAction.cs",
            "Assets/Luckbound/Scripts/Core/Input/PlayerCommand.cs",
            "Assets/Luckbound/Scripts/Core/Input/PlayerInputBuffer.cs",
            "Assets/Luckbound/Scripts/Core/Identifiers/HitId.cs",
            "Assets/Luckbound/Scripts/Core/Combat/CombatStats.cs",
            "Assets/Luckbound/Scripts/Core/Combat/DamageCalculationRequest.cs",
            "Assets/Luckbound/Scripts/Core/Combat/DamageResolution.cs",
            "Assets/Luckbound/Scripts/Core/Combat/DamageResolver.cs",
            "Assets/Luckbound/Scripts/Core/Combat/ActorDamageState.cs",
            "Assets/Luckbound/Scripts/Core/Combat/DirectImpactOutcome.cs",
            "Assets/Luckbound/Scripts/Core/Combat/MeleeAttackProfile.cs",
            "Assets/Luckbound/Scripts/Core/Combat/MeleeAttackSnapshot.cs",
            "Assets/Luckbound/Scripts/Core/Combat/MeleeAttackState.cs",
            "Assets/Luckbound/Scripts/Core/Combat/MeleeComboSequence.cs",
            "Assets/Luckbound/Scripts/Core/Combat/MeleeHitWindow.cs",
            "Assets/Luckbound/Scripts/Core/Combat/SkillDefinition.cs",
            "Assets/Luckbound/Scripts/Core/Combat/SkillCatalog.cs",
            "Assets/Luckbound/Scripts/Core/Combat/SkillUseOutcome.cs",
            "Assets/Luckbound/Scripts/Core/Combat/SkillUseState.cs",
            "Assets/Luckbound/Scripts/Core/Combat/ProjectileDefinition.cs",
            "Assets/Luckbound/Scripts/Core/Combat/ProjectileImpact.cs",
            "Assets/Luckbound/Scripts/Core/Presentation/PlayerVisualState.cs",
            "Assets/Luckbound/Scripts/Gameplay/Luckbound.Gameplay.asmdef",
            "Assets/Luckbound/Scripts/Gameplay/Input/PlayerInputSource.cs",
            "Assets/Luckbound/Scripts/Gameplay/Movement/PlayerMotor2D.cs",
            "Assets/Luckbound/Scripts/Gameplay/Enemies/Slime2D.cs",
            "Assets/Luckbound/Scripts/Gameplay/Combat/ProjectileImpactTarget2D.cs",
            "Assets/Luckbound/Scripts/Gameplay/Combat/ProjectilePool2D.cs",
            "Assets/Luckbound/Scripts/Gameplay/Combat/Projectile2D.cs",
            "Assets/Luckbound/Scripts/Gameplay/Combat/RoomProjectileScope.cs",
            "Assets/Luckbound/Scripts/Content/SkillCatalogJsonLoader.cs",
            "Assets/Luckbound/Scripts/App/MenuNavigationController.cs",
            "Assets/Luckbound/Scripts/App/MovementDemoComposition.cs",
            "Assets/Luckbound/Scripts/App/MovementDemoGoalTrigger.cs",
            "Assets/Luckbound/Scripts/Presentation/Luckbound.Presentation.asmdef",
            "Assets/Luckbound/Scripts/Presentation/PlayerVisualStatePresenter.cs",
            "Assets/Luckbound/Scripts/Presentation/RoomCameraRig2D.cs",
            "Assets/Luckbound/Editor/ProjectDevelopmentChecks.cs",
            "Assets/Luckbound/Editor/ProjectScaffoldGenerator.cs",
            "Assets/Luckbound/Editor/MovementDemoScaffoldGenerator.cs",
            "Assets/Luckbound/Prefabs/MovementDemoPlayer.prefab",
            "Assets/Luckbound/Art/Prototypes/MovementDemo/BellatorPrototype.png",
            "Assets/Luckbound/Art/Prototypes/MovementDemo/StonePrototype.png",
            "Assets/Resources/Luckbound/PlayerVisualStates.controller",
            "Assets/Luckbound/Tests/PlayMode/Luckbound.PlayMode.Tests.asmdef",
            "Assets/Luckbound/Tests/PlayMode/ScenarioEvidenceRecorder.cs",
            "Assets/Luckbound/Tests/PlayMode/JumpControlTests.cs",
            "Assets/Luckbound/Tests/PlayMode/PlayerPresentationTests.cs",
            "Assets/Luckbound/Tests/PlayMode/PlayerDodgeTests.cs",
            "Assets/Luckbound/Tests/PlayMode/PlayerMotorTests.cs",
            "Assets/Luckbound/Tests/PlayMode/StartupFlowTests.cs",
            "Assets/Luckbound/Tests/PlayMode/MovementDemoFlowTests.cs",
            "Assets/Luckbound/Tests/PlayMode/MeleeAttackMotorTests.cs",
            "Assets/Luckbound/Tests/EditMode/DamageResolverTests.cs",
            "Assets/Luckbound/Tests/EditMode/ActorDamageStateTests.cs",
            "Assets/Luckbound/Tests/EditMode/MeleeAttackTests.cs",
            "Assets/Luckbound/Tests/EditMode/SkillUseStateTests.cs",
            "Assets/Luckbound/Tests/PlayMode/SkillCatalogContentTests.cs",
            "Assets/Luckbound/Tests/PlayMode/ProjectilePoolTests.cs",
            "Assets/Luckbound/Tests/PlayMode/PlayerDamageTests.cs",
            "Assets/Luckbound/Tests/PlayMode/SlimeTests.cs",
            "Assets/Resources/Luckbound/Data/Skills.json"
        };

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
                sourceCommit = Environment.GetEnvironmentVariable("GITHUB_SHA") ?? string.Empty,
                sourceHash = ComputeSourceHash(),
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
                artifacts = new List<string>()
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
            string material = string.Join("\n", SourceFiles.Select(path =>
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
                return string.Concat(sha256.ComputeHash(Encoding.UTF8.GetBytes(material))
                    .Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
            }
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
