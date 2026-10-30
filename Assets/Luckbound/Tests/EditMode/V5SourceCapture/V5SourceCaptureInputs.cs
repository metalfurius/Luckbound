using System;
using System.Collections.Generic;
using System.Linq;
using Luckbound.App;
using Luckbound.Content;
using Luckbound.Core.Combat;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Fortune;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using Luckbound.Core.Progression;
using Luckbound.Core.Saves;
using UnityEngine;

namespace Luckbound.Tests.V5SourceCapture
{
    /// <summary>Deterministic, source-backed input recipes for the historical V5 writer.</summary>
    internal static class V5SourceCaptureInputs
    {
        internal const uint Seed = 17320508u;
        internal const int SchemaVersion = 1;
        internal const int ContentVersion = 5;
        internal const int Generation = 1;
        internal const long Sequence = 11L;
        internal const string SavedAtUtc = "2000-01-01T00:00:00Z";

        internal const string ProfileGuidN = "10000000000000000000000000000001";
        internal const string ActiveRunGuidN = "10000000000000000000000000000002";
        internal const string SettlementGuidN = "10000000000000000000000000000003";
        internal const string ProgressionPurchaseGuidN = "10000000000000000000000000000004";
        internal const string InventoryTransactionGuidN = "10000000000000000000000000000005";
        internal const string FortuneOfferGuidN = "10000000000000000000000000000006";
        internal const string FortuneHistoryGuidN = "10000000000000000000000000000007";
        internal const string FortuneRerollGuidN = "10000000000000000000000000000008";
        internal const string FortuneBrewGuidN = "10000000000000000000000000000009";

        internal const string ClassId = "class.bellator";
        internal const string RaceId = "race.human";
        internal const string ContractId = "contract.ct02";
        internal const string ResultJson = "{\"outcome\":\"completed\",\"note\":\"línea 1\\n\\\"daga\\\" — ñ\",\"rune\":\"\\u00E9\"}";

        private const string WeaponInstanceIdN = "20000000000000000000000000000001";
        private const string ArmorInstanceIdN = "20000000000000000000000000000002";
        private const string PotionInstanceIdN = "20000000000000000000000000000003";
        private const string WeaponCollectTransactionIdN = "30000000000000000000000000000001";
        private const string WeaponEquipTransactionIdN = "30000000000000000000000000000002";
        private const string ArmorCollectTransactionIdN = "30000000000000000000000000000003";
        private const string ArmorEquipTransactionIdN = "30000000000000000000000000000004";
        private const string PotionCollectTransactionIdN = "30000000000000000000000000000005";
        private const string PotionQuickSlotTransactionIdN = "30000000000000000000000000000006";
        private const string SecondRerollTransactionIdN = "40000000000000000000000000000002";
        private const string PurchaseTransactionIdHealthOneN = "50000000000000000000000000000001";
        private const string PurchaseTransactionIdHealthTwoN = "50000000000000000000000000000002";
        private const string PurchaseTransactionIdEnergyOneN = "50000000000000000000000000000003";
        private const string PurchaseTransactionIdEnergyTwoN = "50000000000000000000000000000004";
        private const string PurchaseTransactionIdPotionN = "50000000000000000000000000000005";

        private static readonly string[] SelectedSkillIds =
        {
            "skill.bellator.circular_cut",
            "skill.bellator.guard"
        };

        private static readonly string[] FortuneOptionIds =
        {
            "relic.cinder",
            "relic.iron",
            "relic.mirror",
            "relic.ember",
            "relic.ivory",
            "relic.ash",
            "relic.moon",
            "relic.root"
        };

        internal static ProfileSaveData BuildProfileInput(string caseId)
        {
            switch (caseId)
            {
                case "P0-no-active":
                case "B0-arrays-null":
                case "B1-arrays-empty":
                case "M0-false-marker-payload":
                case "M1-true-marker-null":
                    return new ProfileSaveData();

                case "P1-known-zero":
                case "P2-unknown-null":
                case "P2-unknown-inline":
                    return NewUnlockedProfile();

                case "P1-known-nontrivial":
                    return NewUnlockedProfile(availableRenown: 33, healthLevel: 2, energyLevel: 2, potionLevel: 1);

                case "P3-receipts-completed":
                case "N0-tampered":
                    return BuildCompletedProfile();

                default:
                    throw new ArgumentOutOfRangeException(nameof(caseId), caseId, "Case does not use a profile input.");
            }
        }

        internal static GameSettingsSaveData BuildSettingsInput()
        {
            return new GameSettingsSaveData
            {
                languageId = "es",
                masterVolume = 0.83f,
                musicVolume = 0.57f,
                effectsVolume = 0.31f,
                ambienceVolume = 0.19f
            };
        }

        internal static ExpeditionSaveData BuildExpeditionInput(string caseId)
        {
            bool completed = string.Equals(caseId, "P3-receipts-completed", StringComparison.Ordinal);
            bool unknownMarker = caseId == "P2-unknown-null" || caseId == "P2-unknown-inline";
            bool nontrivial = caseId == "P1-known-nontrivial" || completed;

            if (!completed && !unknownMarker && caseId != "P1-known-zero")
                throw new ArgumentOutOfRangeException(nameof(caseId), caseId, "Case does not use an active expedition input.");

            ValidateHistoricalSkills();
            ItemCatalog catalog = LoadHistoricalItemCatalog();
            ExpeditionRoutePlan route = ExpeditionRouteGenerator.Generate(Seed, BuildFixtureRouteCatalog());
            int healthLevel = nontrivial ? 2 : 0;
            int energyLevel = nontrivial ? 2 : 0;
            int potionLevel = nontrivial ? 1 : 0;
            bool includeReceipts = completed;

            ExpeditionInventory inventory = BuildInventory(catalog, ClassId, potionLevel, includeReceipts);
            FortuneOfferBookSnapshot fortune = BuildFortuneSnapshot(route, includeReceipts);
            string currentNodeId = route.MandatoryNodeIds[0];
            bool unknownInline = caseId == "P2-unknown-inline";

            var state = new ExpeditionSaveData
            {
                runId = RunId.Parse(ActiveRunGuidN).ToString(),
                seed = Seed,
                generationVersion = route.GenerationVersion,
                contentVersion = ContentVersion,
                classId = ClassId,
                raceId = RaceId,
                selectedSkillIds = (string[])SelectedSkillIds.Clone(),
                startingEquipmentInstanceIds = new[] { WeaponInstanceIdN, ArmorInstanceIdN },
                routeSnapshotJson = route.CanonicalForm,
                currentNodeId = currentNodeId,
                visitedNodeIds = new[] { currentNodeId },
                completedRoomIds = completed ? new[] { currentNodeId } : Array.Empty<string>(),
                completedRoomResults = completed
                    ? new[]
                    {
                        new ExpeditionRoomCompletionSaveData
                        {
                            roomNodeId = currentNodeId,
                            resultId = "result.v5.capture.completed.01",
                            resultJson = ResultJson
                        }
                    }
                    : Array.Empty<ExpeditionRoomCompletionSaveData>(),
                inventorySnapshotJson = JsonUtility.ToJson(inventory.CreateSnapshot()),
                maximumHealth = PermanentUpgradeRules.ApplyFivePercentPerLevel(100, healthLevel),
                currentHealth = nontrivial ? 73 : 91,
                maximumEnergy = PermanentUpgradeRules.ApplyFivePercentPerLevel(50, energyLevel),
                currentEnergy = nontrivial ? 41 : 36,
                healthPotionCharges = 2 + potionLevel,
                hasFrozenPermanentUpgrades = !unknownMarker,
                frozenPermanentUpgrades = unknownMarker
                    ? (unknownInline ? new ExpeditionPermanentUpgradeSnapshotSaveData() : null)
                    : new ExpeditionPermanentUpgradeSnapshotSaveData
                    {
                        healthUpgradeLevel = healthLevel,
                        energyUpgradeLevel = energyLevel,
                        additionalPotionUpgradeLevel = potionLevel
                    },
                accumulatedRenown = completed ? 33 : nontrivial ? 19 : 0,
                pactIds = nontrivial ? new[] { "pact.guardian.ward" } : Array.Empty<string>(),
                fortuneOfferBookSnapshot = fortune,
                collectedItemIds = nontrivial ? new[] { "relic.cinder" } : Array.Empty<string>(),
                claimedRewardIds = completed ? new[] { "reward.v5.capture.completed.01" } : Array.Empty<string>()
            };

            state.roomEntryCheckpointJson = completed
                ? string.Empty
                : ExpeditionSaveMapper.SerializeCheckpoint(ExpeditionSaveMapper.CreateCheckpoint(state));
            return state;
        }

        internal static byte[] ProjectProfilePreimage(ProfileSaveEnvelope source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            ExpeditionSaveData canonicalExpedition = source.hasActiveExpedition && source.activeExpedition != null
                ? source.activeExpedition.Clone()
                : null;
            var canonical = new ProfileSaveEnvelope
            {
                schemaVersion = source.schemaVersion,
                contentVersion = source.contentVersion,
                generation = source.generation,
                sequence = source.sequence,
                savedAtUtc = source.savedAtUtc,
                checksum = string.Empty,
                profile = source.profile,
                hasActiveExpedition = source.hasActiveExpedition,
                activeExpedition = source.hasActiveExpedition ? canonicalExpedition : null
            };
            return Utf8(JsonUtility.ToJson(canonical, false));
        }

        internal static byte[] ProjectSettingsPreimage(SettingsSaveEnvelope source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var canonical = new SettingsSaveEnvelope
            {
                schemaVersion = source.schemaVersion,
                contentVersion = source.contentVersion,
                generation = source.generation,
                sequence = source.sequence,
                savedAtUtc = source.savedAtUtc,
                checksum = string.Empty,
                settings = source.settings
            };
            return Utf8(JsonUtility.ToJson(canonical, false));
        }

        internal static byte[] Utf8(string value) => new System.Text.UTF8Encoding(false).GetBytes(value ?? string.Empty);

        internal static string[] GetSelectedSkillIds() => (string[])SelectedSkillIds.Clone();

        private static ProfileSaveData NewUnlockedProfile(int availableRenown = 0, int healthLevel = 0,
            int energyLevel = 0, int potionLevel = 0)
        {
            ProfileSaveData profile = ProfileSaveData.CreateNewProfile();
            profile.availableRenown = availableRenown;
            profile.healthUpgradeLevel = healthLevel;
            profile.energyUpgradeLevel = energyLevel;
            profile.additionalPotionUpgradeLevel = potionLevel;
            profile.contractCounters = Array.Empty<ContractCounterSaveData>();
            profile.progressionPurchaseTransactions = Array.Empty<PermanentUpgradePurchaseSaveData>();
            profile.processedSettlementRunIds = Array.Empty<string>();
            return profile;
        }

        private static ProfileSaveData BuildCompletedProfile()
        {
            var snapshot = new PermanentUpgradeSnapshot(543, 0, 0, 0);
            PurchaseSpec[] purchases =
            {
                new PurchaseSpec(PermanentUpgradeKind.MaximumHealth, ProgressionPurchaseGuidN),
                new PurchaseSpec(PermanentUpgradeKind.MaximumHealth, PurchaseTransactionIdHealthTwoN),
                new PurchaseSpec(PermanentUpgradeKind.MaximumEnergy, PurchaseTransactionIdEnergyOneN),
                new PurchaseSpec(PermanentUpgradeKind.MaximumEnergy, PurchaseTransactionIdEnergyTwoN),
                new PurchaseSpec(PermanentUpgradeKind.AdditionalHealthPotion, PurchaseTransactionIdPotionN)
            };

            foreach (PurchaseSpec purchase in purchases)
            {
                PermanentUpgradePurchaseResult result = PermanentUpgradeRules.PreparePurchase(snapshot,
                    purchase.Kind, TransactionId.Parse(purchase.TransactionIdN), isInTavern: true);
                if (!result.Succeeded || !result.ChangesProfile)
                    throw new InvalidOperationException("Historical purchase rules did not commit the fixed P3 receipt.");
                snapshot = result.Snapshot;
            }

            if (snapshot.AvailableRenown != 33 || snapshot.HealthUpgradeLevel != 2 ||
                snapshot.EnergyUpgradeLevel != 2 || snapshot.AdditionalPotionUpgradeLevel != 1 ||
                snapshot.PurchaseRecords.Count != 5)
                throw new InvalidOperationException("The fixed P3 purchase recipe did not reach its declared state.");

            return new ProfileSaveData
            {
                availableRenown = snapshot.AvailableRenown,
                healthUpgradeLevel = snapshot.HealthUpgradeLevel,
                energyUpgradeLevel = snapshot.EnergyUpgradeLevel,
                additionalPotionUpgradeLevel = snapshot.AdditionalPotionUpgradeLevel,
                unlockedRaceIds = new[] { RaceId },
                unlockedStartingKitIds = ProfileSaveData.CreateNewProfile().unlockedStartingKitIds,
                acceptedContractIds = new[] { ContractId },
                claimedContractIds = Array.Empty<string>(),
                readStoryFragmentIds = new[] { "story.v5.capture.read.01" },
                contractCounters = new[] { new ContractCounterSaveData { contractId = ContractId, progress = 2 } },
                processedSettlementRunIds = new[]
                {
                    RunId.Parse(SettlementGuidN).ToString(),
                    RunId.Parse(ProfileGuidN).ToString()
                },
                progressionPurchaseTransactions = snapshot.PurchaseRecords.Select(record =>
                    new PermanentUpgradePurchaseSaveData
                    {
                        transactionId = record.TransactionId.ToString(),
                        kind = (int)record.Kind,
                        cost = record.Cost
                    }).ToArray()
            };
        }

        private static ExpeditionInventory BuildInventory(ItemCatalog catalog, string classId, int potionUpgradeLevel,
            bool includeCompletedReceipts)
        {
            StartingLoadout loadout = catalog.GetStartingLoadout(classId);
            var inventory = new ExpeditionInventory(catalog, classId, initialGold: 27);
            var weapon = new InventoryItemStack(ItemInstanceId.Parse(WeaponInstanceIdN), loadout.WeaponItemId,
                ItemRarity.Common, upgradeLevel: 0, quantity: 1);
            RequireSuccess(inventory.TryCollectItem(weapon, TransactionId.Parse(WeaponCollectTransactionIdN)), "collect fixed weapon");
            RequireSuccess(inventory.EquipBackpackItem(FindSlot(inventory, weapon.InstanceId), EquipmentSlot.Weapon,
                TransactionId.Parse(WeaponEquipTransactionIdN)), "equip fixed weapon");

            var armor = new InventoryItemStack(ItemInstanceId.Parse(ArmorInstanceIdN), loadout.ArmorItemId,
                ItemRarity.Common, upgradeLevel: 0, quantity: 1);
            RequireSuccess(inventory.TryCollectItem(armor, TransactionId.Parse(ArmorCollectTransactionIdN)), "collect fixed armor");
            RequireSuccess(inventory.EquipBackpackItem(FindSlot(inventory, armor.InstanceId), EquipmentSlot.Armor,
                TransactionId.Parse(ArmorEquipTransactionIdN)), "equip fixed armor");

            int potionCount = 2 + potionUpgradeLevel;
            var potions = new InventoryItemStack(ItemInstanceId.Parse(PotionInstanceIdN), "consumable.health_potion",
                ItemRarity.Common, upgradeLevel: 0, quantity: potionCount);
            RequireSuccess(inventory.TryCollectItem(potions, TransactionId.Parse(PotionCollectTransactionIdN)), "collect fixed potion stack");
            RequireSuccess(inventory.SetQuickConsumable(FindSlot(inventory, potions.InstanceId),
                TransactionId.Parse(PotionQuickSlotTransactionIdN)), "set fixed quick consumable");

            if (includeCompletedReceipts)
            {
                RequireSuccess(inventory.TryAddMaterial(MaterialKind.Ore, 7,
                    TransactionId.Parse(InventoryTransactionGuidN)), "add fixed historical Core material transaction");
            }
            return inventory;
        }

        private static FortuneOfferBookSnapshot BuildFortuneSnapshot(ExpeditionRoutePlan route, bool includeReceipts)
        {
            var book = new FortuneOfferBook(route.GenerationVersion, route.Seed, RaceId);
            if (!includeReceipts) return book.CreateSnapshot();

            FortuneOfferResult firstOffer = book.CreateOffer(FortuneOfferGuidN, "context.guardian", FortuneOptionIds);
            if (firstOffer.Status != FortuneOperationStatus.Succeeded)
                throw new InvalidOperationException("Could not create the fixed historical Fortune offer.");
            FortuneRerollResult firstReroll = book.Reroll(FortuneOfferGuidN, TransactionId.Parse(FortuneRerollGuidN));
            if (firstReroll.Status != FortuneOperationStatus.Succeeded)
                throw new InvalidOperationException("Could not create the fixed historical Fortune reroll receipt.");
            FortuneOffer currentOffer = book.GetOffer(FortuneOfferGuidN);
            if (currentOffer == null || currentOffer.OptionIds.Count == 0 ||
                !book.Choose(FortuneOfferGuidN, currentOffer.OptionIds[0]).Succeeded || !book.Claim(FortuneOfferGuidN).Succeeded)
                throw new InvalidOperationException("Could not complete the fixed historical Fortune offer.");

            string[] secondOptions = FortuneOptionIds.Select(optionId => optionId + ".second").ToArray();
            FortuneOfferResult secondOffer = book.CreateOffer(FortuneHistoryGuidN, "context.treasure", secondOptions);
            if (secondOffer.Status != FortuneOperationStatus.Succeeded)
                throw new InvalidOperationException("Could not create the second fixed historical Fortune offer.");
            FortuneRerollResult secondReroll = book.Reroll(FortuneHistoryGuidN,
                TransactionId.Parse(SecondRerollTransactionIdN));
            if (secondReroll.Status != FortuneOperationStatus.Succeeded)
                throw new InvalidOperationException("Could not create the second fixed historical Fortune reroll receipt.");

            FortuneChargeResult brew = book.GrantFortuneBrewCharge(ItemInstanceId.Parse(PotionInstanceIdN),
                TransactionId.Parse(FortuneBrewGuidN));
            if (brew.Status != FortuneOperationStatus.Succeeded)
                throw new InvalidOperationException("Could not create the fixed historical Fortune Brew receipt.");

            FortuneOfferBookSnapshot snapshot = book.CreateSnapshot();
            if (snapshot.offers.Length == 0 || snapshot.offers.Any(offer => offer.history == null || offer.history.Length == 0) ||
                snapshot.rerollReceipts.Length == 0 || snapshot.fortuneBrewReceipts.Length == 0)
                throw new InvalidOperationException("The P3 Fortune recipe must retain offer, history, reroll and brew receipts.");
            return snapshot;
        }

        private static ItemCatalog LoadHistoricalItemCatalog()
        {
            TextAsset asset = Resources.Load<TextAsset>("Luckbound/Data/Items/Items");
            if (asset == null) throw new InvalidOperationException("Historical item catalog resource is missing.");
            return ItemCatalogJsonLoader.Load(asset.text);
        }

        private static void ValidateHistoricalSkills()
        {
            TextAsset asset = Resources.Load<TextAsset>("Luckbound/Data/Skills");
            if (asset == null) throw new InvalidOperationException("Historical skill catalog resource is missing.");
            SkillCatalog catalog = SkillCatalogJsonLoader.Load(asset.text);
            foreach (string skillId in SelectedSkillIds)
            {
                if (!catalog.TryGet(skillId, out SkillDefinition skill) ||
                    !string.Equals(skill.ClassId, ClassId, StringComparison.Ordinal))
                    throw new InvalidOperationException("Fixed selected skill is not present in the historical class catalog: " + skillId);
            }
        }

        private static ExpeditionRouteCatalog BuildFixtureRouteCatalog()
        {
            var catalog = new ExpeditionRouteCatalog(new[]
            {
                new ExpeditionRouteSectorCatalog("patio",
                    new[] { "room.patio.c1", "room.patio.c2", "room.patio.c3", "room.patio.c4", "room.patio.c5" },
                    new[] { "room.patio.e1", "room.patio.e2" },
                    "room.patio.t", "room.patio.p", "room.patio.n", "room.patio.guardian"),
                new ExpeditionRouteSectorCatalog("archivo",
                    new[] { "room.archivo.c1", "room.archivo.c2", "room.archivo.c3", "room.archivo.c4", "room.archivo.c5" },
                    new[] { "room.archivo.e1", "room.archivo.e2" },
                    "room.archivo.t", "room.archivo.p", "room.archivo.n", "room.archivo.guardian"),
                new ExpeditionRouteSectorCatalog("forjas",
                    new[] { "room.forjas.c1", "room.forjas.c2", "room.forjas.c3", "room.forjas.c4", "room.forjas.c5" },
                    new[] { "room.forjas.e1", "room.forjas.e2" },
                    "room.forjas.t", "room.forjas.p", "room.forjas.n", "room.forjas.guardian")
            }, "room.hub.rest", "room.final.arena");

            IReadOnlyList<string> errors = catalog.Validate();
            if (errors.Count != 0)
                throw new InvalidOperationException("The fixed V5 route catalog is invalid: " + string.Join("; ", errors));
            return catalog;
        }

        private static int FindSlot(ExpeditionInventory inventory, ItemInstanceId instanceId)
        {
            for (int index = 0; index < ExpeditionInventory.BackpackCapacity; index++)
            {
                InventoryItemStack stack = inventory.GetBackpackSlot(index);
                if (stack != null && stack.InstanceId == instanceId) return index;
            }
            throw new InvalidOperationException("Fixed inventory item was not placed in a backpack slot.");
        }

        private static void RequireSuccess(InventoryOperationOutcome result, string operation)
        {
            if (result == null || !result.Succeeded)
                throw new InvalidOperationException("Historical Core inventory operation failed: " + operation + ".");
        }

        private sealed class PurchaseSpec
        {
            internal PurchaseSpec(PermanentUpgradeKind kind, string transactionIdN)
            {
                Kind = kind;
                TransactionIdN = transactionIdN;
            }

            internal PermanentUpgradeKind Kind { get; }
            internal string TransactionIdN { get; }
        }
    }
}
