using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;

public static class ProductionProtocolCheck
{
    private static void Require(bool value) { if (!value) throw new Exception("Synthetic factory input rejected"); }
    private static string Hex(byte[] value) => BitConverter.ToString(value).Replace("-", "").ToLowerInvariant();
    public static void Main()
    {
        Require(EncounterCatalogFactoryV1.TryCreateManifest(1, "cat-é", 7, "encounter-spawn.v1", "resolver-a", 2,
            Array.Empty<int>(), Array.Empty<EncounterCatalogDescriptor>(), Array.Empty<EncounterCatalogBinding>(),
            Array.Empty<EncounterCatalogDefinition>(), out var empty, out var err));
        Require(EncounterCatalogFactoryV1.TryCreateDefinition("e-1", 1, Array.Empty<EncounterCatalogVariant>(), out var emptyDef, out err));
        Require(EncounterCatalogFactoryV1.TryCreateDescriptor("room", 1, EncounterCatalogDescriptorKind.Room, new string('0', 64), out var room, out err));
        Require(EncounterCatalogFactoryV1.TryCreateDescriptor("enemy", 1, EncounterCatalogDescriptorKind.Enemy, new string('1', 64), out var enemy, out err));
        Require(EncounterCatalogFactoryV1.TryCreateDescriptor("cache", 1, EncounterCatalogDescriptorKind.Cache, new string('0', 64), out var cache, out err));
        Require(EncounterCatalogFactoryV1.TryCreateDescriptorRef("room", 1, out var roomRef, out err));
        Require(EncounterCatalogFactoryV1.TryCreateDescriptorRef("enemy", 1, out var enemyRef, out err));
        Require(EncounterCatalogFactoryV1.TryCreateDescriptorRef("cache", 1, out var cacheRef, out err));
        Require(EncounterCatalogFactoryV1.TryCreateSpawn("s", "enemy", enemyRef, "anchor", ExpeditionEncounterRewardEnemyKind.Normal, 0, out var spawn, out err));
        Require(EncounterCatalogFactoryV1.TryCreateWave("w", new[] {spawn}, out var wave, out err));
        Require(EncounterCatalogFactoryV1.TryCreateCache("cache-one", cacheRef, "chest", out var cacheRecord, out err));
        Require(EncounterCatalogFactoryV1.TryCreateVariant("v", new[] {"a"}, Array.Empty<string>(), 0, roomRef, new[] {wave}, cacheRecord, out var variant, out err));
        Require(EncounterCatalogFactoryV1.TryCreateDefinition("enc", 1, new[] {variant}, out var definition, out err));
        Require(EncounterCatalogFactoryV1.TryCreateBinding(1, "t", ExpeditionRouteNodeKind.Combat, EncounterCatalogBindingMode.Encounter, "enc", 1, out var binding, out err));
        Require(EncounterCatalogFactoryV1.TryCreateManifest(1, "c", 1, "encounter-spawn.v1", "r", 1, new[] {1},
            new[] {cache, enemy, room}, new[] {binding}, new[] {definition}, out var rich, out err));
        var vectors = new Dictionary<string, object> {
            ["manifestEmpty"] = new {hex = Hex(EncounterCatalogCanonicalV1.EncodeManifest(empty)), sha256 = EncounterCatalogCanonicalV1.ComputeManifestSha256(empty)},
            ["manifestRich"] = new {hex = Hex(EncounterCatalogCanonicalV1.EncodeManifest(rich)), sha256 = EncounterCatalogCanonicalV1.ComputeManifestSha256(rich)},
            ["definitionEmpty"] = new {hex = Hex(EncounterCatalogCanonicalV1.EncodeDefinition(emptyDef)), sha256 = EncounterCatalogCanonicalV1.ComputeDefinitionSha256(emptyDef)},
            ["definitionRich"] = new {hex = Hex(EncounterCatalogCanonicalV1.EncodeDefinition(definition)), sha256 = EncounterCatalogCanonicalV1.ComputeDefinitionSha256(definition)}
        };
        var rewards = new Dictionary<string, object>();
        foreach (string sector in new[] {"patio", "archivo", "forjas"})
        {
            var counts = new Dictionary<MaterialKind, int>();
            foreach (MaterialKind kind in Enum.GetValues(typeof(MaterialKind))) counts[kind] = 0;
            int drops = 0;
            for (int index = 0; index < 128; index++)
            {
                string key = "distribution." + index.ToString("D3", System.Globalization.CultureInfo.InvariantCulture);
                foreach (var kind in new[] {ExpeditionEncounterRewardEnemyKind.Normal, ExpeditionEncounterRewardEnemyKind.Elite})
                {
                    var request = new ExpeditionEncounterRewardRequest(new RunId(Guid.Parse("9db52316-c72b-4747-961a-89b79f4d0c6a")),
                        0x13579BDFu, 1, sector + ".combat.01", "room." + sector + ".c1", sector,
                        ExpeditionRouteNodeKind.Combat, new[] {new ExpeditionEncounterRewardSpawn(key, kind)});
                    var outcome = ExpeditionEncounterRewardPolicy.Resolve(request).EnemyOutcomes[0];
                    if (kind == ExpeditionEncounterRewardEnemyKind.Normal) { if (outcome.BaseMaterialGrants.Count == 1) drops++; }
                    else foreach (var grant in outcome.BaseMaterialGrants) counts[grant.Material]++;
                }
            }
            var materials = sector == "patio" ? new[] {MaterialKind.UmbraHerb, MaterialKind.ShadowWood, MaterialKind.DarkResin}
                : sector == "archivo" ? new[] {MaterialKind.Ore, MaterialKind.ArcaneDust, MaterialKind.UmbraHerb}
                : new[] {MaterialKind.Ore, MaterialKind.VitalEmber, MaterialKind.DarkResin};
            rewards[sector] = new {normalDrops = drops, eliteMaterialCounts = materials.Select(m => counts[m]).ToArray()};
        }
        Console.WriteLine(JsonSerializer.Serialize(new {vectors, rewards, unityExecuted = false, jsonUtilityExecuted = false}));
    }
}
