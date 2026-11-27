using System;
using System.IO;
using System.Reflection;
using Luckbound.App;
using Luckbound.Content;
using Luckbound.Core.Saves;
using Luckbound.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Luckbound.Tests.Editor
{
    /// <summary>Negative safety coverage for the staged Editor-only opening package.</summary>
    public sealed class F10V7DiagnosticOpeningSafetyTests
    {
        private const string ValidRevision = "20261120T1234567890123Z-00000000000000000000000000000000";

        [Test]
        public void BundleRevisionRejectsTraversalAndMalformedIds()
        {
            Type policy = EditorType("F10V7DiagnosticOpeningPathPolicy");
            MethodInfo method = policy.GetMethod("ResolveBundleDirectory", BindingFlags.Static | BindingFlags.NonPublic);
            TargetInvocationException thrown = Assert.Throws<TargetInvocationException>(() =>
                method.Invoke(null, new object[] { Path.GetTempPath(), "..\\persistent" }));
            Assert.That(thrown.InnerException, Is.TypeOf<InvalidDataException>());
        }

        [Test]
        public void StorePathRejectsPersistentDataOverlap()
        {
            Type policy = EditorType("F10V7DiagnosticOpeningPathPolicy");
            MethodInfo method = policy.GetMethod("EnsureContainedAndDisjoint", BindingFlags.Static | BindingFlags.NonPublic);
            string root = Path.Combine(Path.GetTempPath(), "Luckbound-F10-V7-path-test-" + Guid.NewGuid().ToString("N"));
            string candidate = Path.Combine(root, "Logs", "technical-store");
            string persistent = Path.Combine(candidate, "player-data");
            TargetInvocationException thrown = Assert.Throws<TargetInvocationException>(() =>
                method.Invoke(null, new object[] { candidate, root, persistent }));
            Assert.That(thrown.InnerException, Is.TypeOf<InvalidDataException>());
        }

        [Test]
        public void MissingMaterializedPrefabRootIsRejected()
        {
            Type policy = EditorType("F10V7DiagnosticOpeningPathPolicy");
            MethodInfo method = policy.GetMethod("EnsureRequiredRoots", BindingFlags.Static | BindingFlags.NonPublic);
            TargetInvocationException thrown = Assert.Throws<TargetInvocationException>(() =>
                method.Invoke(null, new object[] { Array.Empty<string>(), new[] { "Assets/Luckbound/Editor/Diagnostic/F10/F10TutorialDiagnostic.prefab" } }));
            Assert.That(thrown.InnerException, Is.TypeOf<FileNotFoundException>());
        }

        [Test]
        public void ForeignProjectRootIsRejectedBeforeAssetDatabaseAccess()
        {
            Type builder = EditorType("F10V7DiagnosticCatalogBuilder");
            MethodInfo method = builder.GetMethod("LoadAndVerifyPhysicalInputs", BindingFlags.Static | BindingFlags.NonPublic);
            string projectRoot = Path.Combine(Path.GetTempPath(), "Luckbound-F10-V7-bundle-test-" + Guid.NewGuid().ToString("N"));
            Assert.That(Directory.Exists(projectRoot), Is.False, "Use only a unique, nonexistent test path.");
            Assert.That(File.Exists(projectRoot), Is.False, "Use only a unique, nonexistent test path.");
            TargetInvocationException thrown = Assert.Throws<TargetInvocationException>(() =>
                method.Invoke(null, new object[] { projectRoot, ValidRevision, new string('a', 64) }));
            Assert.That(thrown.InnerException, Is.TypeOf<InvalidOperationException>());
            Assert.That(thrown.InnerException.Message, Is.EqualTo(
                "Physical verification must use the exact Unity editor project containing the loaded AssetDatabase."));
        }

        [Test]
        public void RealC08MapsFourSpawnsToTheTwoC1RoomAnchors()
        {
            TextAsset c1Asset = AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Resources/Luckbound/Data/Rooms/F10PatioCombatC1Room.json");
            TextAsset c08Asset = AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Resources/Luckbound/Data/Encounters/C08Arena.json");
            Assert.That(c1Asset, Is.Not.Null, "The test must load the real C1 room JSON.");
            Assert.That(c08Asset, Is.Not.Null, "The test must load the real C08 encounter JSON.");

            RoomTemplateData c1 = RoomTemplateJsonLoader.Load(c1Asset.text);
            CombatEncounterData c08 = CombatEncounterJsonLoader.Load(c08Asset.text);
            Type builder = EditorType("F10V7DiagnosticCatalogBuilder");
            MethodInfo method = builder.GetMethod("BuildC1SpawnAnchorMapping", BindingFlags.Static | BindingFlags.NonPublic);
            var mapping = method.Invoke(null, new object[] { c1, c08 }) as System.Collections.Generic.IDictionary<string, string>;

            Assert.That(mapping, Is.Not.Null);
            Assert.That(mapping.Count, Is.EqualTo(4));
            Assert.That(mapping["spawn.slime.01"], Is.EqualTo("spawn.slime.01"));
            Assert.That(mapping["spawn.huesoguardia.01"], Is.EqualTo("spawn.slime.01"));
            Assert.That(mapping["spawn.slime.02"], Is.EqualTo("spawn.slime.02"));
            Assert.That(mapping["spawn.huesoguardia.02"], Is.EqualTo("spawn.slime.02"));
        }

        [Test]
        public void CompositeDescriptorFingerprintGateRejectsMissingVerifiedInputs()
        {
            Type builder = EditorType("F10V7DiagnosticCatalogBuilder");
            MethodInfo method = builder.GetMethod("RequireApprovedCompositeDescriptorFingerprintContract",
                BindingFlags.Static | BindingFlags.NonPublic);
            TargetInvocationException thrown = Assert.Throws<TargetInvocationException>(() =>
                method.Invoke(null, new object[] { null }));
            Assert.That(thrown.InnerException, Is.TypeOf<InvalidDataException>());
        }

        [Test]
        public void AppV6BootstrapAcceptsOnlyAnEmptyLoadedStoreAndExactOwner()
        {
            MethodInfo method = typeof(ApplicationBootstrapper).GetMethod(
                "IsEmptyF10V7DiagnosticV6BootstrapState", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            var profile = new SaveStorageSnapshot(null, null);
            var settings = new SaveStorageSnapshot(null, null);
            object[] args =
            {
                SaveLoadStatus.Missing, SaveLoadStatus.Missing, SaveDataAccess.ReadWrite,
                profile, settings, null, null, false, 1, true, true
            };
            Assert.That(method.Invoke(null, args), Is.True);

            args[3] = new SaveStorageSnapshot("primary-v6", null);
            Assert.That(method.Invoke(null, args), Is.False, "A V6 primary must reject the diagnostic bootstrap.");
            args[3] = profile;
            args[4] = new SaveStorageSnapshot(null, "backup-settings");
            Assert.That(method.Invoke(null, args), Is.False, "A settings backup must reject the diagnostic bootstrap.");
            args[4] = settings;
            args[8] = 2;
            Assert.That(method.Invoke(null, args), Is.False, "The Editor store factory must have exactly one owner invocation.");
            args[8] = 1;
            args[10] = false;
            Assert.That(method.Invoke(null, args), Is.False, "Unexpected files reject the diagnostic store.");
        }

        [Test]
        public void AppRequiresAnArmedV6FenceBeforeReadyAndRejectsWritableState()
        {
            MethodInfo method = typeof(ApplicationBootstrapper).GetMethod(
                "IsF10V7DiagnosticV6WriteFenceState", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            Assert.That(method.Invoke(null, new object[]
                { true, true, SaveLoadStatus.Incompatible, false }), Is.True);
            Assert.That(method.Invoke(null, new object[]
                { true, false, SaveLoadStatus.Incompatible, false }), Is.False);
            Assert.That(method.Invoke(null, new object[]
                { true, true, SaveLoadStatus.Missing, false }), Is.False);
            Assert.That(method.Invoke(null, new object[]
                { true, true, SaveLoadStatus.Incompatible, true }), Is.False);
        }

        [Test]
        public void WriterReadbackAndAppReceiptRequireOrdinalPrimaryJsonEquality()
        {
            MethodInfo method = typeof(ApplicationBootstrapper).GetMethod(
                "IsExactF10V7PrimarySnapshot", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            Assert.That(method.Invoke(null, new object[] { "{\"run\":1}", "{\"run\":1}" }), Is.True);
            Assert.That(method.Invoke(null, new object[] { "{\"run\":1}", "{\"run\":2}" }), Is.False);
            Assert.That(method.Invoke(null, new object[] { "{\"run\":1}", "{\"RUN\":1}" }), Is.False);
            Assert.That(method.Invoke(null, new object[] { null, "{\"run\":1}" }), Is.False);
        }

        private static Type EditorType(string name)
        {
            Type type = typeof(F10TechnicalPhysicalDescriptorExporter).Assembly.GetType("Luckbound.Editor." + name);
            Assert.That(type, Is.Not.Null, "Staged Editor type must be present when the candidate is integrated.");
            return type;
        }
    }
}
