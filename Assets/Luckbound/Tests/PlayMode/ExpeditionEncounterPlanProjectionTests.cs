using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Luckbound.App;
using Luckbound.Content;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using NUnit.Framework;

namespace Luckbound.Tests.PlayMode
{
    public sealed class ExpeditionEncounterPlanProjectionTests
    {
        private const string CatalogId = "f09.plan-projection.synthetic";
        private const string NodeId = "sample.patio.combat";

        [Test]
        public void StrictJsonExactRevisionResolutionProjectsCompleteOrderedRosterInPreparing()
        {
            string[][] revisionOneInputSpawns =
            {
                new[] { "spawn.r1.wave1.zeta", "spawn.r1.wave1.alpha" },
                new[] { "spawn.r1.wave2.beta" }
            };
            string[][] revisionOneCanonicalSpawns =
            {
                new[] { "spawn.r1.wave1.alpha", "spawn.r1.wave1.zeta" },
                new[] { "spawn.r1.wave2.beta" }
            };
            string[][] revisionTwoInputSpawns =
            {
                new[] { "spawn.r2.wave1.zeta", "spawn.r2.wave1.alpha" },
                new[] { "spawn.r2.wave2.zulu", "spawn.r2.wave2.beta" }
            };
            string[][] revisionTwoCanonicalSpawns =
            {
                new[] { "spawn.r2.wave1.alpha", "spawn.r2.wave1.zeta" },
                new[] { "spawn.r2.wave2.beta", "spawn.r2.wave2.zulu" }
            };

            // Each JSON wave deliberately lists zeta before alpha (and zulu before beta) to prove that
            // the resolved snapshot's ordinal SpawnId ordering flows through the projection unchanged.
            EncounterCatalogManifest loadedRevisionOne = LoadEncounterManifest(
                1, "variant.r1", revisionOneInputSpawns);
            EncounterCatalogManifest loadedRevisionTwo = LoadEncounterManifest(
                2, "variant.r2", revisionTwoInputSpawns);
            Assert.That(EncounterCatalogRegistryV1.TryCreate(
                new[] { loadedRevisionTwo, loadedRevisionOne }, out EncounterCatalogRegistryV1 registry,
                out EncounterCatalogError createError), Is.True, createError.Message);

            string revisionOneDigest = EncounterCatalogCanonicalV1.ComputeManifestSha256(loadedRevisionOne);
            string revisionTwoDigest = EncounterCatalogCanonicalV1.ComputeManifestSha256(loadedRevisionTwo);
            Assert.That(revisionOneDigest, Is.Not.EqualTo(revisionTwoDigest));
            Assert.That(registry.TryGetExact(CatalogId, 1, revisionOneDigest,
                out EncounterCatalogManifest exactRevisionOne, out EncounterCatalogError lookupError), Is.True,
                lookupError.Message);
            Assert.That(exactRevisionOne, Is.SameAs(loadedRevisionOne));
            Assert.That(exactRevisionOne.CatalogRevision, Is.EqualTo(1));
            Assert.That(registry.TryGetExact(CatalogId, 2, revisionTwoDigest,
                out EncounterCatalogManifest exactRevisionTwo, out lookupError), Is.True, lookupError.Message);
            Assert.That(exactRevisionTwo, Is.SameAs(loadedRevisionTwo));
            Assert.That(exactRevisionTwo.CatalogRevision, Is.EqualTo(2));
            Assert.That(registry.TryGetExact(CatalogId, 1, revisionTwoDigest,
                out _, out EncounterCatalogError wrongRevisionDigest), Is.False);
            Assert.That(wrongRevisionDigest.Code, Is.EqualTo(EncounterCatalogErrorCode.CatalogRevisionUnavailable));

            ExpeditionRunConfiguration run = CreateSampleRun(seed: 0x51f09u);
            Assert.That(EncounterPlanResolverV1.TryResolve(exactRevisionOne, run, NodeId,
                Array.Empty<string>(), out ResolvedEncounterPlanV1 planOne, out _), Is.True);
            Assert.That(EncounterPlanResolverV1.TryResolve(exactRevisionTwo, run, NodeId,
                Array.Empty<string>(), out ResolvedEncounterPlanV1 planTwo, out _), Is.True);

            ExpeditionEncounterPlanProjection projectionOne = ExpeditionEncounterPlanProjection.Create(planOne);
            ExpeditionEncounterPlanProjection projectionTwo = ExpeditionEncounterPlanProjection.Create(planTwo);
            Assert.That(planOne.CatalogDigest, Is.EqualTo(revisionOneDigest));
            Assert.That(planTwo.CatalogDigest, Is.EqualTo(revisionTwoDigest));
            Assert.That(planOne.SelectedVariant.VariantId, Is.EqualTo("variant.r1"));
            Assert.That(planTwo.SelectedVariant.VariantId, Is.EqualTo("variant.r2"));
            Assert.That(projectionOne.Mode, Is.EqualTo(EncounterCatalogBindingMode.Encounter));
            Assert.That(projectionOne.PlanSha256,
                Is.EqualTo(ResolvedEncounterPlanCanonicalV1.ComputeSha256(planOne)));
            AssertRosterMatches(planOne, projectionOne, revisionOneCanonicalSpawns);
            AssertRosterMatches(planTwo, projectionTwo, revisionTwoCanonicalSpawns);

            var state = new ExpeditionEncounterRosterState(projectionOne.Roster);
            Assert.That(state.Phase, Is.EqualTo(ExpeditionEncounterRosterPhase.Preparing));
            Assert.That(state.IsCleared, Is.False);
            Assert.That(state.CompletedWaveCount, Is.Zero);
            Assert.That(state.RegisteredSpawnCount, Is.Zero);
            Assert.That(state.DefeatedSpawnCount, Is.Zero);
            Assert.That(state.CanArmCurrentWave, Is.False);
        }

        [Test]
        public void ProjectionKeepsEncounterRosterCollectionsDeeplyReadOnly()
        {
            string[][] spawns =
            {
                new[] { "spawn.immutable.a", "spawn.immutable.b" }
            };
            EncounterCatalogManifest manifest = LoadEncounterManifest(1, "variant.immutable", spawns);
            Assert.That(EncounterPlanResolverV1.TryResolve(manifest, CreateSampleRun(seed: 0x53f09u), NodeId,
                Array.Empty<string>(), out ResolvedEncounterPlanV1 plan, out _), Is.True);

            ExpeditionEncounterPlanProjection projection = ExpeditionEncounterPlanProjection.Create(plan);
            Assert.That(((IList<ExpeditionEncounterWavePlan>)projection.Roster.Waves).IsReadOnly, Is.True);
            Assert.That(((IList<string>)projection.Roster.Waves[0].SpawnKeys).IsReadOnly, Is.True);
            Assert.Throws<NotSupportedException>(() =>
                ((IList<ExpeditionEncounterWavePlan>)projection.Roster.Waves).Clear());
            Assert.Throws<NotSupportedException>(() =>
                ((IList<string>)projection.Roster.Waves[0].SpawnKeys)[0] = "spawn.tampered");
        }

        [Test]
        public void StrictJsonNoneBindingProjectsNullRosterAndCannotRepresentClear()
        {
            Assert.That(EncounterCatalogJsonLoaderV1.TryLoad(Utf8(NoneManifestJson()),
                out EncounterCatalogManifest loaded, out EncounterCatalogError loadError), Is.True,
                loadError.Message);
            string digest = EncounterCatalogCanonicalV1.ComputeManifestSha256(loaded);
            Assert.That(EncounterCatalogRegistryV1.TryCreate(new[] { loaded },
                out EncounterCatalogRegistryV1 registry, out EncounterCatalogError createError), Is.True,
                createError.Message);
            Assert.That(registry.TryGetExact(CatalogId, 1, digest,
                out EncounterCatalogManifest exact, out EncounterCatalogError lookupError), Is.True,
                lookupError.Message);
            Assert.That(EncounterPlanResolverV1.TryResolve(exact, CreateSampleRun(seed: 0x55f09u), NodeId,
                Array.Empty<string>(), out ResolvedEncounterPlanV1 plan, out _), Is.True);

            ExpeditionEncounterPlanProjection projection = ExpeditionEncounterPlanProjection.Create(plan);
            Assert.That(projection.Mode, Is.EqualTo(EncounterCatalogBindingMode.None));
            Assert.That(plan.SelectedVariant, Is.Null);
            Assert.That(projection.PlanSha256,
                Is.EqualTo(ResolvedEncounterPlanCanonicalV1.ComputeSha256(plan)));
            Assert.That(projection.Roster, Is.Null);
        }

        private static void AssertRosterMatches(ResolvedEncounterPlanV1 plan,
            ExpeditionEncounterPlanProjection projection, string[][] expectedSpawnIdsByWave)
        {
            Assert.That(projection.Roster, Is.Not.Null);
            Assert.That(plan.SelectedVariant.Waves.Count, Is.EqualTo(expectedSpawnIdsByWave.Length));
            Assert.That(projection.Roster.Waves.Count, Is.EqualTo(expectedSpawnIdsByWave.Length));

            var expectedKeys = new List<string>();
            var actualKeys = new List<string>();
            for (int waveIndex = 0; waveIndex < expectedSpawnIdsByWave.Length; waveIndex++)
            {
                Assert.That(plan.SelectedVariant.Waves[waveIndex].Spawns.Count,
                    Is.EqualTo(expectedSpawnIdsByWave[waveIndex].Length));
                ExpeditionEncounterWavePlan rosterWave = projection.Roster.Waves[waveIndex];
                Assert.That(rosterWave.WaveNumber, Is.EqualTo(waveIndex + 1));
                Assert.That(rosterWave.SpawnKeys.Count, Is.EqualTo(expectedSpawnIdsByWave[waveIndex].Length));

                for (int spawnIndex = 0; spawnIndex < expectedSpawnIdsByWave[waveIndex].Length; spawnIndex++)
                {
                    string expectedSpawnId = expectedSpawnIdsByWave[waveIndex][spawnIndex];
                    Assert.That(plan.SelectedVariant.Waves[waveIndex].Spawns[spawnIndex].SpawnId,
                        Is.EqualTo(expectedSpawnId));
                    string expectedKey = plan.GetSpawnIdentity(expectedSpawnId).Key;
                    expectedKeys.Add(expectedKey);
                    actualKeys.Add(rosterWave.SpawnKeys[spawnIndex]);
                }
            }

            CollectionAssert.AreEqual(expectedKeys, actualKeys);
            Assert.That(actualKeys.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(expectedKeys.Count));
        }

        private static EncounterCatalogManifest LoadEncounterManifest(int revision, string variantId,
            string[][] spawnIdsByWave)
        {
            Assert.That(EncounterCatalogJsonLoaderV1.TryLoad(
                Utf8(EncounterManifestJson(revision, variantId, spawnIdsByWave)),
                out EncounterCatalogManifest manifest, out EncounterCatalogError error), Is.True, error.Message);
            return manifest;
        }

        private static string EncounterManifestJson(int revision, string variantId, string[][] spawnIdsByWave)
        {
            var waves = new StringBuilder();
            for (int waveIndex = 0; waveIndex < spawnIdsByWave.Length; waveIndex++)
            {
                if (waveIndex > 0) waves.Append(',');
                waves.Append("{\"waveId\":\"wave.synthetic.")
                    .Append((waveIndex + 1).ToString(CultureInfo.InvariantCulture))
                    .Append("\",\"spawns\":[");

                for (int spawnIndex = 0; spawnIndex < spawnIdsByWave[waveIndex].Length; spawnIndex++)
                {
                    if (spawnIndex > 0) waves.Append(',');
                    string spawnId = spawnIdsByWave[waveIndex][spawnIndex];
                    waves.Append("{\"spawnId\":\"").Append(spawnId)
                        .Append("\",\"enemyId\":\"enemy.synthetic\",")
                        .Append("\"enemyDescriptorRef\":{\"descriptorId\":\"enemy.synthetic\",\"revision\":1},")
                        .Append("\"spawnAnchorId\":\"anchor.").Append(spawnId)
                        .Append("\",\"rewardKind\":\"normal\",\"vinculoSequenceOrdinal\":0}");
                }

                waves.Append("]}");
            }

            string safeRevision = revision.ToString(CultureInfo.InvariantCulture);
            return "{"
                + "\"schemaVersion\":1,"
                + "\"catalogId\":\"" + CatalogId + "\","
                + "\"catalogRevision\":" + safeRevision + ","
                + "\"identityCodec\":\"encounter-spawn.v1\","
                + "\"resolverId\":\"encounter-resolver.v1\","
                + "\"resolverRevision\":1,"
                + "\"supportedRouteGenerationVersions\":[2],"
                + "\"descriptorRefs\":["
                    + "{\"descriptorId\":\"room.synthetic\",\"revision\":1,\"kind\":\"room\",\"sha256\":\"" + new string('a', 64) + "\"},"
                    + "{\"descriptorId\":\"enemy.synthetic\",\"revision\":1,\"kind\":\"enemy\",\"sha256\":\"" + new string('b', 64) + "\"}],"
                + "\"bindings\":[{\"generationVersion\":2,\"templateId\":\"room.patio.c1\","
                    + "\"nodeKind\":\"combat\",\"mode\":\"encounter\",\"encounterId\":\"encounter.synthetic\",\"definitionRevision\":1}],"
                + "\"definitions\":[{\"encounterId\":\"encounter.synthetic\",\"definitionRevision\":1,\"variants\":[{"
                    + "\"variantId\":\"" + variantId + "\",\"requiredPactIds\":[],\"forbiddenPactIds\":[],"
                    + "\"scheduledVinculoOrdinal\":0,\"roomDescriptorRef\":{\"descriptorId\":\"room.synthetic\",\"revision\":1},"
                    + "\"waves\":[" + waves + "],\"cache\":null}]}]}";
        }

        private static string NoneManifestJson()
        {
            return "{"
                + "\"schemaVersion\":1,"
                + "\"catalogId\":\"" + CatalogId + "\","
                + "\"catalogRevision\":1,"
                + "\"identityCodec\":\"encounter-spawn.v1\","
                + "\"resolverId\":\"encounter-resolver.v1\","
                + "\"resolverRevision\":1,"
                + "\"supportedRouteGenerationVersions\":[2],"
                + "\"descriptorRefs\":[],"
                + "\"bindings\":[{\"generationVersion\":2,\"templateId\":\"room.patio.c1\","
                    + "\"nodeKind\":\"combat\",\"mode\":\"none\"}],"
                + "\"definitions\":[]}";
        }

        private static ExpeditionRunConfiguration CreateSampleRun(uint seed)
        {
            string[] nodeIds =
            {
                "sample.tutorial",
                "sample.patio.combat",
                "sample.patio.exploration",
                "sample.patio.guardian",
                "sample.safe_extraction"
            };
            var nodes = new[]
            {
                new ExpeditionSampleRouteNodeDefinition(nodeIds[0], "patio", "room.sample.tutorial",
                    ExpeditionRouteNodeKind.Tutorial, true, 0, ExpeditionSampleRouteDefinition.EntryPortId,
                    ExpeditionSampleRouteDefinition.ExitPortId),
                new ExpeditionSampleRouteNodeDefinition(nodeIds[1], "patio", "room.patio.c1",
                    ExpeditionRouteNodeKind.Combat, true, 1, ExpeditionSampleRouteDefinition.EntryPortId,
                    ExpeditionSampleRouteDefinition.ExitPortId),
                new ExpeditionSampleRouteNodeDefinition(nodeIds[2], "patio", "room.patio.e1",
                    ExpeditionRouteNodeKind.Exploration, true, 2, ExpeditionSampleRouteDefinition.EntryPortId,
                    ExpeditionSampleRouteDefinition.ExitPortId),
                new ExpeditionSampleRouteNodeDefinition(nodeIds[3], "patio", "room.patio.guardian",
                    ExpeditionRouteNodeKind.Guardian, true, 3, ExpeditionSampleRouteDefinition.EntryPortId,
                    ExpeditionSampleRouteDefinition.ExitPortId),
                new ExpeditionSampleRouteNodeDefinition(nodeIds[4], "patio", "room.sample.extraction",
                    ExpeditionRouteNodeKind.SafeExtraction, true, 4, ExpeditionSampleRouteDefinition.EntryPortId,
                    ExpeditionSampleRouteDefinition.ExitPortId)
            };
            var connections = new ExpeditionSampleRouteConnectionDefinition[nodeIds.Length - 1];
            for (int index = 0; index < connections.Length; index++)
            {
                connections[index] = new ExpeditionSampleRouteConnectionDefinition(nodeIds[index],
                    ExpeditionSampleRouteDefinition.ExitPortId, nodeIds[index + 1],
                    ExpeditionSampleRouteDefinition.EntryPortId, isOptionalBranch: false);
            }

            var definition = new ExpeditionSampleRouteDefinition(
                ExpeditionSampleRouteDefinition.RequiredSchemaVersion,
                ExpeditionSampleRouteDefinition.RequiredGenerationVersion,
                nodes,
                connections,
                nodeIds);
            ExpeditionRoutePlan route = ExpeditionSampleRouteGenerator.Generate(seed, definition);
            var inventory = new ExpeditionInventory(
                new ItemCatalog(Array.Empty<ItemDefinition>()), "class.bellator");
            return new ExpeditionRunConfiguration(
                new RunId(Guid.ParseExact("0123456789abcdef0123456789abcdef", "N")),
                "class.bellator",
                ExpeditionRunConfiguration.HumanRaceId,
                new[] { "skill.synthetic.one", "skill.synthetic.two" },
                seed,
                route,
                inventory);
        }

        private static byte[] Utf8(string value)
        {
            return new UTF8Encoding(false, true).GetBytes(value);
        }
    }
}
