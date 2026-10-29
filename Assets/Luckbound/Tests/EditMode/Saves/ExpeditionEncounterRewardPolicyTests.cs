using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using NUnit.Framework;

namespace Luckbound.Tests.App
{
    public sealed class ExpeditionEncounterRewardPolicyTests
    {
        private static readonly RunId TestRunId = new RunId(Guid.Parse("9db52316-c72b-4747-961a-89b79f4d0c6a"));
        private const uint TestSeed = 0x13579BDFu;

        [Test]
        public void SamePersistedContextReconstructsTheSameRoomAndEnemyGrants()
        {
            ExpeditionEncounterRewardRequest firstRequest = CombatRequest("sample.patio.combat",
                "room.patio.c1", "patio", new[]
                {
                    new ExpeditionEncounterRewardSpawn("wave.slimes/0", ExpeditionEncounterRewardEnemyKind.Normal),
                    new ExpeditionEncounterRewardSpawn("wave.slimes/1", ExpeditionEncounterRewardEnemyKind.Elite)
                });
            ExpeditionEncounterRewardResolution first = ExpeditionEncounterRewardPolicy.Resolve(firstRequest);
            ExpeditionEncounterRewardResolution restored = ExpeditionEncounterRewardPolicy.Resolve(CombatRequest(
                "sample.patio.combat", "room.patio.c1", "patio", new[]
                {
                    new ExpeditionEncounterRewardSpawn("wave.slimes/0", ExpeditionEncounterRewardEnemyKind.Normal),
                    new ExpeditionEncounterRewardSpawn("wave.slimes/1", ExpeditionEncounterRewardEnemyKind.Elite)
                }));

            Assert.That(restored.ResolutionId, Is.EqualTo(first.ResolutionId));
            Assert.That(restored.RoomPayout.GoldGrantId, Is.EqualTo(first.RoomPayout.GoldGrantId));
            Assert.That(restored.RoomPayout.RenownGrantId, Is.EqualTo(first.RoomPayout.RenownGrantId));
            Assert.That(restored.RoomPayout.Gold, Is.EqualTo(first.RoomPayout.Gold));
            Assert.That(restored.RoomPayout.Renown, Is.EqualTo(first.RoomPayout.Renown));
            Assert.That(restored.EnemyOutcomes.Select(EnemyFingerprint), Is.EqualTo(first.EnemyOutcomes.Select(EnemyFingerprint)));
        }

        [Test]
        public void RewardIdsChangeWithRunSeedVersionNodeTemplateSectorKindAndSpawnKey()
        {
            ExpeditionEncounterRewardResolution baseline = ExpeditionEncounterRewardPolicy.Resolve(CombatRequest(
                "sample.patio.combat", "room.patio.c1", "patio",
                new[] { new ExpeditionEncounterRewardSpawn("wave.slimes/0", ExpeditionEncounterRewardEnemyKind.Normal) }));
            string roomGoldId = baseline.RoomPayout.GoldGrantId;
            string enemyGoldId = baseline.EnemyOutcomes[0].GoldGrantId;

            Assert.That(Resolve(CombatRequest("sample.patio.combat", "room.patio.c1", "patio",
                new[] { new ExpeditionEncounterRewardSpawn("wave.slimes/0", ExpeditionEncounterRewardEnemyKind.Normal) },
                runId: new RunId(Guid.Parse("9db52316-c72b-4747-961a-89b79f4d0c6b")))).RoomPayout.GoldGrantId, Is.Not.EqualTo(roomGoldId));
            Assert.That(Resolve(CombatRequest("sample.patio.combat", "room.patio.c1", "patio",
                new[] { new ExpeditionEncounterRewardSpawn("wave.slimes/0", ExpeditionEncounterRewardEnemyKind.Normal) }, seed: TestSeed + 1u)).RoomPayout.GoldGrantId,
                Is.Not.EqualTo(roomGoldId));
            Assert.That(Resolve(CombatRequest("sample.patio.combat", "room.patio.c1", "patio",
                new[] { new ExpeditionEncounterRewardSpawn("wave.slimes/0", ExpeditionEncounterRewardEnemyKind.Normal) }, generationVersion: 2)).RoomPayout.GoldGrantId,
                Is.Not.EqualTo(roomGoldId));
            Assert.That(Resolve(CombatRequest("sample.patio.combat.changed", "room.patio.c1", "patio",
                new[] { new ExpeditionEncounterRewardSpawn("wave.slimes/0", ExpeditionEncounterRewardEnemyKind.Normal) })).RoomPayout.GoldGrantId,
                Is.Not.EqualTo(roomGoldId));
            Assert.That(Resolve(CombatRequest("sample.patio.combat", "room.patio.c2", "patio",
                new[] { new ExpeditionEncounterRewardSpawn("wave.slimes/0", ExpeditionEncounterRewardEnemyKind.Normal) })).RoomPayout.GoldGrantId,
                Is.Not.EqualTo(roomGoldId));
            Assert.That(Resolve(CombatRequest("sector.archivo.combat", "room.archivo.c1", "archivo",
                new[] { new ExpeditionEncounterRewardSpawn("wave.slimes/0", ExpeditionEncounterRewardEnemyKind.Normal) })).RoomPayout.GoldGrantId,
                Is.Not.EqualTo(roomGoldId));
            Assert.That(Resolve(new ExpeditionEncounterRewardRequest(TestRunId, TestSeed, 1, "sample.patio.combat",
                "room.patio.c1", "patio", ExpeditionRouteNodeKind.OptionalCombat,
                new[] { new ExpeditionEncounterRewardSpawn("wave.slimes/0", ExpeditionEncounterRewardEnemyKind.Normal) })).RoomPayout.GoldGrantId,
                Is.Not.EqualTo(roomGoldId));
            Assert.That(Resolve(CombatRequest("sample.patio.combat", "room.patio.c1", "patio",
                new[] { new ExpeditionEncounterRewardSpawn("wave.slimes/1", ExpeditionEncounterRewardEnemyKind.Normal) })).EnemyOutcomes[0].GoldGrantId,
                Is.Not.EqualTo(enemyGoldId));
        }

        [Test]
        public void SampleExplorationGetsRenownOnClearAndItsPhysicalCacheHasSeparatePickupGold()
        {
            ExpeditionEncounterRewardResolution sample = Resolve(new ExpeditionEncounterRewardRequest(TestRunId,
                TestSeed, 2, "sample.patio.exploration", "room.patio.e1", "patio",
                ExpeditionRouteNodeKind.Exploration, Array.Empty<ExpeditionEncounterRewardSpawn>(),
                hasExplorationCache: true, explorationCacheKey: "sample.patio.exploration.cache"));

            Assert.That(sample.RoomPayout.Gold, Is.Zero);
            Assert.That(sample.RoomPayout.GoldGrantId, Is.Null);
            Assert.That(sample.RoomPayout.Renown, Is.EqualTo(3));
            Assert.That(sample.ExplorationCacheReward, Is.Not.Null);
            Assert.That(sample.ExplorationCacheReward.Gold, Is.EqualTo(15));
            Assert.That(sample.ExplorationCacheReward.CacheKey, Is.EqualTo("sample.patio.exploration.cache"));
            Assert.That(sample.EnemyOutcomes, Is.Empty);
            Assert.That(sample.OfferRequirement, Is.Null);

            Assert.Throws<InvalidOperationException>(() => Resolve(new ExpeditionEncounterRewardRequest(TestRunId,
                TestSeed, 2, "sample.patio.exploration", "room.patio.e1", "patio",
                ExpeditionRouteNodeKind.Exploration, Array.Empty<ExpeditionEncounterRewardSpawn>())));
            Assert.Throws<InvalidOperationException>(() => Resolve(new ExpeditionEncounterRewardRequest(TestRunId,
                TestSeed, 2, "sector.archivo.exploration", "room.archivo.e2", "archivo",
                ExpeditionRouteNodeKind.Exploration, Array.Empty<ExpeditionEncounterRewardSpawn>())));
        }

        [Test]
        public void PhysicalExplorationCacheGoldIsASeparatePickupReward()
        {
            AssertExplorationCache("patio", "sample.patio.exploration", "room.patio.e1", 15, false);
            AssertExplorationCache("archivo", "sector.archivo.exploration", "room.archivo.e1", 25, true);
            AssertExplorationCache("forjas", "sector.forjas.exploration", "room.forjas.e2", 35, false);

            Assert.Throws<ArgumentException>(() => new ExpeditionEncounterRewardRequest(TestRunId, TestSeed, 1,
                "sector.archivo.exploration", "room.archivo.e1", "archivo", ExpeditionRouteNodeKind.Exploration,
                Array.Empty<ExpeditionEncounterRewardSpawn>(), hasExplorationCache: true));
        }

        [Test]
        public void TreasureAndPactRoomsReturnStableOfferRequirementsWithoutOfferPayloads()
        {
            var treasureRequest = new ExpeditionEncounterRewardRequest(TestRunId, TestSeed, 1,
                "patio.special.treasure", "room.patio.t", "patio", ExpeditionRouteNodeKind.Treasure,
                Array.Empty<ExpeditionEncounterRewardSpawn>());
            var pactRequest = new ExpeditionEncounterRewardRequest(TestRunId, TestSeed, 1,
                "patio.special.pact", "room.patio.p", "patio", ExpeditionRouteNodeKind.Pact,
                Array.Empty<ExpeditionEncounterRewardSpawn>());

            ExpeditionEncounterRewardResolution treasure = Resolve(treasureRequest);
            ExpeditionEncounterRewardResolution pact = Resolve(pactRequest);

            Assert.That(treasure.OfferRequirement.Kind, Is.EqualTo(ExpeditionRewardOfferKind.TreasureEquipmentOrRelic));
            Assert.That(treasure.OfferRequirement.OfferId, Is.EqualTo(Resolve(treasureRequest).OfferRequirement.OfferId));
            Assert.That(treasure.OfferRequirement.OfferId, Is.Not.Empty);
            Assert.That(treasure.RoomPayout.Renown, Is.EqualTo(3));
            Assert.That(treasure.RoomPayout.Gold, Is.Zero);
            Assert.That(pact.OfferRequirement.Kind, Is.EqualTo(ExpeditionRewardOfferKind.Pact));
            Assert.That(pact.OfferRequirement.OfferId, Is.Not.Empty);
            Assert.That(pact.OfferRequirement.OfferId, Is.EqualTo(Resolve(pactRequest).OfferRequirement.OfferId));
            Assert.That(pact.OfferRequirement.OfferId, Is.Not.EqualTo(treasure.OfferRequirement.OfferId));
            Assert.That(pact.RoomPayout.Renown, Is.EqualTo(3));
            Assert.That(pact.RoomPayout.Gold, Is.Zero);
        }

        [Test]
        public void TutorialAndSafeExtractionProduceNoRewards()
        {
            ExpeditionEncounterRewardResolution tutorial = Resolve(new ExpeditionEncounterRewardRequest(TestRunId,
                TestSeed, 2, "sample.tutorial", "room.sample.tutorial", "patio",
                ExpeditionRouteNodeKind.Tutorial, Array.Empty<ExpeditionEncounterRewardSpawn>()));
            ExpeditionEncounterRewardResolution extraction = Resolve(new ExpeditionEncounterRewardRequest(TestRunId,
                TestSeed, 2, "sample.safe_extraction", "room.sample.extraction", "patio",
                ExpeditionRouteNodeKind.SafeExtraction, Array.Empty<ExpeditionEncounterRewardSpawn>()));

            Assert.That(tutorial.RoomPayout.HasReward, Is.False);
            Assert.That(tutorial.RoomPayout.Gold, Is.Zero);
            Assert.That(tutorial.RoomPayout.Renown, Is.Zero);
            Assert.That(extraction.RoomPayout.HasReward, Is.False);
            Assert.That(extraction.RoomPayout.Gold, Is.Zero);
            Assert.That(extraction.RoomPayout.Renown, Is.Zero);
            Assert.That(tutorial.EnemyOutcomes, Is.Empty);
            Assert.That(extraction.EnemyOutcomes, Is.Empty);
        }

        [Test]
        public void CombatAndNormalEnemyGoldUseTheExactSectorTables()
        {
            AssertCombatAmounts("patio", "sample.patio.combat", "room.patio.c1", 12, 3);
            AssertCombatAmounts("archivo", "sector.archivo.combat", "room.archivo.c1", 18, 5);
            AssertCombatAmounts("forjas", "sector.forjas.combat", "room.forjas.c1", 24, 7);
        }

        [Test]
        public void AvariceRoundsTheRoomAndEnemyGoldSourcesIndependently()
        {
            ExpeditionEncounterRewardResolution result = Resolve(CombatRequest("sector.archivo.combat",
                "room.archivo.c1", "archivo",
                new[] { new ExpeditionEncounterRewardSpawn("wave.archive/0", ExpeditionEncounterRewardEnemyKind.Normal) },
                avaricePactActive: true));

            Assert.That(result.RoomPayout.Gold, Is.EqualTo(24));
            Assert.That(result.EnemyOutcomes[0].TotalGold, Is.EqualTo(6));
            Assert.That(result.RoomPayout.GoldGrantId, Is.Not.EqualTo(result.EnemyOutcomes[0].GoldGrantId));
        }

        [Test]
        public void NormalDropChanceAndMaterialDistributionsAreReproducibleBySector()
        {
            foreach (string sector in new[] { "patio", "archivo", "forjas" })
            {
                int drops = 0;
                bool foundRepeatedMaterialKind = false;
                var materialCounts = new Dictionary<MaterialKind, int>();
                foreach (MaterialKind material in Enum.GetValues(typeof(MaterialKind))) materialCounts[material] = 0;

                for (int index = 0; index < 128; index++)
                {
                    string spawnKey = "distribution." + index.ToString("D3", CultureInfo.InvariantCulture);
                    ExpeditionEncounterRewardResolution normal = Resolve(SectorCombatRequest(sector, spawnKey,
                        ExpeditionEncounterRewardEnemyKind.Normal));
                    if (normal.EnemyOutcomes[0].BaseMaterialGrants.Count == 1) drops++;

                    ExpeditionEncounterRewardResolution elite = Resolve(SectorCombatRequest(sector, spawnKey,
                        ExpeditionEncounterRewardEnemyKind.Elite));
                    Assert.That(elite.EnemyOutcomes[0].BaseMaterialGrants, Has.Count.EqualTo(2));
                    Assert.That(elite.EnemyOutcomes[0].BaseMaterialGrants.All(grant => grant.Quantity == 1), Is.True);
                    if (elite.EnemyOutcomes[0].BaseMaterialGrants[0].Material ==
                        elite.EnemyOutcomes[0].BaseMaterialGrants[1].Material)
                    {
                        foundRepeatedMaterialKind = true;
                    }
                    foreach (ExpeditionMaterialRewardGrant grant in elite.EnemyOutcomes[0].BaseMaterialGrants)
                        materialCounts[grant.Material]++;
                }

                Assert.That(drops, Is.EqualTo(ExpectedNormalDrops(sector)), "sector=" + sector);
                Assert.That(foundRepeatedMaterialKind, Is.True, "Selections are with replacement; sector=" + sector);
                CollectionAssert.AreEqual(ExpectedEliteMaterialCounts(sector), AllowedMaterialCounts(sector, materialCounts),
                    "sector=" + sector);
            }
        }

        [Test]
        public void EliteAndVinculoBonusesAreSeparateAndHonorAvariceAndOfferOrdinal()
        {
            var elite = new ExpeditionEncounterRewardSpawn("bond.elite.03", ExpeditionEncounterRewardEnemyKind.Elite,
                vinculoSequenceOrdinal: 3);
            ExpeditionEncounterRewardResolution result = Resolve(CombatRequest("patio.combat.03", "room.patio.c3",
                "patio", new[] { elite }, avaricePactActive: true, vinculoPactActive: true));
            ExpeditionEnemyRewardOutcome outcome = result.EnemyOutcomes[0];

            Assert.That(result.RoomPayout.Gold, Is.EqualTo(16));
            Assert.That(outcome.BaseGold, Is.EqualTo(9));
            Assert.That(outcome.VinculoBonusGold, Is.EqualTo(15));
            Assert.That(outcome.TotalGold, Is.EqualTo(32));
            Assert.That(outcome.GoldGrantId, Is.Not.Null);
            Assert.That(outcome.BaseMaterialGrants, Has.Count.EqualTo(2));
            Assert.That(outcome.VinculoMaterialGrants, Has.Count.EqualTo(2));
            Assert.That(outcome.BaseMaterialGrants.All(grant => grant.Quantity == 1), Is.True);
            Assert.That(outcome.VinculoMaterialGrants.All(grant => grant.Quantity == 1), Is.True);
            Assert.That(outcome.VinculoMaterialGrants.Select(grant => grant.GrantId).Distinct(), Has.Count.EqualTo(2));
            Assert.That(outcome.RelicOfferRequired, Is.True);
            Assert.That(outcome.RelicOfferId, Is.Not.Empty);
        }

        [Test]
        public void GuardianPayoutIncludesSectorGoldRenownAndOneStableFortuneGrant()
        {
            AssertGuardian("patio", 40, 20);
            AssertGuardian("archivo", 70, 30);
            AssertGuardian("forjas", 100, 40);
        }

        [Test]
        public void VinculoRequiresAnExplicitOrdinalAndOnlyTheThirdMarksARelicOfferRequirement()
        {
            for (int ordinal = 1; ordinal <= 3; ordinal++)
            {
                var spawn = new ExpeditionEncounterRewardSpawn("bond.elite." + ordinal.ToString(CultureInfo.InvariantCulture),
                    ExpeditionEncounterRewardEnemyKind.Elite, ordinal);
                ExpeditionEncounterRewardResolution result = Resolve(CombatRequest("bond.combat." + ordinal,
                    "room.patio.c" + ordinal.ToString(CultureInfo.InvariantCulture), "patio", new[] { spawn },
                    vinculoPactActive: true));
                ExpeditionEncounterRewardResolution restored = Resolve(CombatRequest("bond.combat." + ordinal,
                    "room.patio.c" + ordinal.ToString(CultureInfo.InvariantCulture), "patio", new[] { spawn },
                    vinculoPactActive: true));
                Assert.That(result.EnemyOutcomes[0].VinculoSequenceOrdinal, Is.EqualTo(ordinal));
                Assert.That(result.EnemyOutcomes[0].RelicOfferRequired, Is.EqualTo(ordinal == 3));
                Assert.That(result.EnemyOutcomes[0].RelicOfferId != null, Is.EqualTo(ordinal == 3));
                Assert.That(restored.EnemyOutcomes[0].RelicOfferId, Is.EqualTo(result.EnemyOutcomes[0].RelicOfferId));
            }
        }

        [Test]
        public void RejectsIncompleteInconsistentAndDuplicateRewardInputs()
        {
            Assert.Throws<ArgumentException>(() => new ExpeditionEncounterRewardRequest(default, TestSeed, 1,
                "node", "room.patio.c1", "patio", ExpeditionRouteNodeKind.Combat,
                new[] { new ExpeditionEncounterRewardSpawn("spawn.0", ExpeditionEncounterRewardEnemyKind.Normal) }));
            Assert.Throws<ArgumentOutOfRangeException>(() => CombatRequest("node", "room.patio.c1", "patio",
                new[] { new ExpeditionEncounterRewardSpawn("spawn.0", ExpeditionEncounterRewardEnemyKind.Normal) }, generationVersion: 0));
            Assert.Throws<ArgumentException>(() => new ExpeditionEncounterRewardSpawn("spawn with spaces",
                ExpeditionEncounterRewardEnemyKind.Normal));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExpeditionEncounterRewardSpawn("bond.elite.04",
                ExpeditionEncounterRewardEnemyKind.Elite, vinculoSequenceOrdinal: 4));
            Assert.Throws<InvalidOperationException>(() => Resolve(CombatRequest("node", "room.archivo.c1", "patio",
                new[] { new ExpeditionEncounterRewardSpawn("spawn.0", ExpeditionEncounterRewardEnemyKind.Normal) })));
            Assert.Throws<InvalidOperationException>(() => Resolve(CombatRequest("node", "room.patio.c1", "patio",
                Array.Empty<ExpeditionEncounterRewardSpawn>())));
            Assert.Throws<InvalidOperationException>(() => Resolve(CombatRequest("node", "room.patio.c1", "patio",
                new[]
                {
                    new ExpeditionEncounterRewardSpawn("spawn.0", ExpeditionEncounterRewardEnemyKind.Normal),
                    new ExpeditionEncounterRewardSpawn("spawn.0", ExpeditionEncounterRewardEnemyKind.Normal)
                })));
            Assert.Throws<InvalidOperationException>(() => Resolve(new ExpeditionEncounterRewardRequest(TestRunId,
                TestSeed, 1, "guardian", "room.patio.guardian", "patio", ExpeditionRouteNodeKind.Guardian,
                Array.Empty<ExpeditionEncounterRewardSpawn>())));
            Assert.Throws<InvalidOperationException>(() => Resolve(new ExpeditionEncounterRewardRequest(TestRunId,
                TestSeed, 1, "sample.patio.exploration", "room.patio.e1", "patio",
                ExpeditionRouteNodeKind.Exploration,
                new[] { new ExpeditionEncounterRewardSpawn("spawn.0", ExpeditionEncounterRewardEnemyKind.Normal) })));
            Assert.Throws<InvalidOperationException>(() => Resolve(CombatRequest("node", "room.patio.c1", "patio",
                new[] { new ExpeditionEncounterRewardSpawn("bond.elite.01", ExpeditionEncounterRewardEnemyKind.Elite,
                    vinculoSequenceOrdinal: 1) })));
        }

        private static void AssertCombatAmounts(string sector, string nodeId, string templateId,
            int expectedRoomGold, int expectedEnemyGold)
        {
            ExpeditionEncounterRewardResolution result = Resolve(CombatRequest(nodeId, templateId, sector,
                new[] { new ExpeditionEncounterRewardSpawn("wave.0/enemy.0", ExpeditionEncounterRewardEnemyKind.Normal) }));
            Assert.That(result.RoomPayout.Gold, Is.EqualTo(expectedRoomGold), "sector=" + sector);
            Assert.That(result.RoomPayout.Renown, Is.EqualTo(5), "sector=" + sector);
            Assert.That(result.EnemyOutcomes[0].BaseGold, Is.EqualTo(expectedEnemyGold), "sector=" + sector);
        }

        private static void AssertGuardian(string sector, int expectedGold, int expectedRenown)
        {
            var spawn = new ExpeditionEncounterRewardSpawn("guardian.main", ExpeditionEncounterRewardEnemyKind.Guardian);
            ExpeditionEncounterRewardResolution result = Resolve(new ExpeditionEncounterRewardRequest(TestRunId,
                TestSeed, 2, sector + ".guardian", "room." + sector + ".guardian", sector,
                ExpeditionRouteNodeKind.Guardian, new[] { spawn }));

            Assert.That(result.RoomPayout.Gold, Is.Zero);
            Assert.That(result.RoomPayout.Renown, Is.EqualTo(expectedRenown));
            Assert.That(result.EnemyOutcomes[0].BaseGold, Is.EqualTo(expectedGold));
            Assert.That(result.EnemyOutcomes[0].FortuneCharges, Is.EqualTo(1));
            Assert.That(result.EnemyOutcomes[0].FortuneChargeGrantId, Is.Not.Empty);
            Assert.That(result.EnemyOutcomes[0].BaseMaterialGrants, Is.Empty);
        }

        private static void AssertExplorationCache(string sector, string nodeId, string templateId,
            int expectedCacheGold, bool avariceActive)
        {
            ExpeditionEncounterRewardResolution result = Resolve(new ExpeditionEncounterRewardRequest(TestRunId,
                TestSeed, 2, nodeId, templateId, sector, ExpeditionRouteNodeKind.Exploration,
                Array.Empty<ExpeditionEncounterRewardSpawn>(), avaricePactActive: avariceActive,
                hasExplorationCache: true, explorationCacheKey: "cache.world.0"));

            Assert.That(result.RoomPayout.Gold, Is.Zero);
            Assert.That(result.RoomPayout.GoldGrantId, Is.Null);
            Assert.That(result.RoomPayout.Renown, Is.EqualTo(3));
            Assert.That(result.ExplorationCacheReward, Is.Not.Null);
            Assert.That(result.ExplorationCacheReward.CacheKey, Is.EqualTo("cache.world.0"));
            int expectedAdjustedCacheGold = avariceActive ? expectedCacheGold * 135 / 100 : expectedCacheGold;
            Assert.That(result.ExplorationCacheReward.Gold, Is.EqualTo(expectedAdjustedCacheGold));
            Assert.That(result.ExplorationCacheReward.GoldGrantId, Is.Not.Empty);
        }

        private static ExpeditionEncounterRewardRequest SectorCombatRequest(string sector, string spawnKey,
            ExpeditionEncounterRewardEnemyKind kind)
        {
            return CombatRequest(sector + ".combat.01", "room." + sector + ".c1", sector,
                new[] { new ExpeditionEncounterRewardSpawn(spawnKey, kind) });
        }

        private static ExpeditionEncounterRewardRequest CombatRequest(string nodeId, string templateId,
            string sector, IEnumerable<ExpeditionEncounterRewardSpawn> spawns, RunId? runId = null,
            uint? seed = null, int generationVersion = 1, bool avaricePactActive = false,
            bool vinculoPactActive = false)
        {
            return new ExpeditionEncounterRewardRequest(runId ?? TestRunId, seed ?? TestSeed, generationVersion,
                nodeId, templateId, sector, ExpeditionRouteNodeKind.Combat, spawns,
                avaricePactActive, vinculoPactActive);
        }

        private static ExpeditionEncounterRewardResolution Resolve(ExpeditionEncounterRewardRequest request)
        {
            return ExpeditionEncounterRewardPolicy.Resolve(request);
        }

        private static string EnemyFingerprint(ExpeditionEnemyRewardOutcome outcome)
        {
            string baseMaterials = string.Join(",", outcome.BaseMaterialGrants.Select(grant =>
                grant.GrantId + "=" + grant.Material));
            string vinculoMaterials = string.Join(",", outcome.VinculoMaterialGrants.Select(grant =>
                grant.GrantId + "=" + grant.Material));
            return string.Join("|", new[]
            {
                outcome.SpawnKey,
                outcome.EnemyKind.ToString(),
                outcome.BaseGold.ToString(CultureInfo.InvariantCulture),
                outcome.VinculoBonusGold.ToString(CultureInfo.InvariantCulture),
                outcome.TotalGold.ToString(CultureInfo.InvariantCulture),
                outcome.GoldGrantId,
                baseMaterials,
                vinculoMaterials,
                outcome.FortuneCharges.ToString(CultureInfo.InvariantCulture),
                outcome.FortuneChargeGrantId,
                outcome.RelicOfferRequired.ToString(),
                outcome.RelicOfferId
            });
        }

        private static int ExpectedNormalDrops(string sector)
        {
            switch (sector)
            {
                case "patio": return 62;
                case "archivo": return 62;
                case "forjas": return 67;
                default: throw new ArgumentOutOfRangeException(nameof(sector));
            }
        }

        private static int[] ExpectedEliteMaterialCounts(string sector)
        {
            switch (sector)
            {
                case "patio": return new[] { 113, 83, 60 };
                case "archivo": return new[] { 91, 108, 57 };
                case "forjas": return new[] { 92, 98, 66 };
                default: throw new ArgumentOutOfRangeException(nameof(sector));
            }
        }

        private static int[] AllowedMaterialCounts(string sector, IDictionary<MaterialKind, int> counts)
        {
            switch (sector)
            {
                case "patio": return new[] { counts[MaterialKind.UmbraHerb], counts[MaterialKind.ShadowWood], counts[MaterialKind.DarkResin] };
                case "archivo": return new[] { counts[MaterialKind.Ore], counts[MaterialKind.ArcaneDust], counts[MaterialKind.UmbraHerb] };
                case "forjas": return new[] { counts[MaterialKind.Ore], counts[MaterialKind.VitalEmber], counts[MaterialKind.DarkResin] };
                default: throw new ArgumentOutOfRangeException(nameof(sector));
            }
        }
    }
}
