using System.Collections.Generic;
using System.Linq;
using Luckbound.App;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using NUnit.Framework;

namespace Luckbound.Tests.PlayMode
{
    public sealed class ExpeditionSettlementFlowTests
    {
        [Test]
        public void D07_ExtractionConvertsARealRunInventoryAndRepeatedResultCannotDuplicatePayout()
        {
            using (var evidence = new ScenarioEvidenceRecorder("D07", "D07_ExtractionSettlementIsIdempotent", 16180339))
            {
                evidence.RecordInput(0, "PrepareRun", "Bellator with catalog skills and the data-defined starting kit");
                ExpeditionRunConfiguration run = ExpeditionRunConfigurationFactory.Create(
                    "class.bellator", ExpeditionRunConfiguration.HumanRaceId,
                    new[] { "skill.bellator.circular_cut", "skill.bellator.guard" });
                ExpeditionInventory inventory = run.Inventory;

                bool kitCaptured = run.StartingEquipmentInstanceIds.Count == 2 &&
                    run.StartingEquipmentInstanceIds.Contains(inventory.Weapon.InstanceId) &&
                    run.StartingEquipmentInstanceIds.Contains(inventory.Armor.InstanceId);
                evidence.RecordAssertion("The new run records the exact two starter equipment instances",
                    kitCaptured, "equipped weapon and armor instance IDs", run.StartingEquipmentInstanceIds.Count + " starter IDs");

                var added = new List<string>();
                Add("weapon.bellator.rusted_sword", ItemRarity.Rare);
                Add("armor.magus.traveler_robe", ItemRarity.Epic);
                Add("relic.marked_coin", ItemRarity.Common);
                Assert.That(inventory.TryAddMaterial(MaterialKind.ShadowWood, 5, TransactionId.New()).Succeeded, Is.True);
                Assert.That(inventory.TryAddMaterial(MaterialKind.Ore, 4, TransactionId.New()).Succeeded, Is.True);
                Assert.That(inventory.TryAddGold(39, TransactionId.New()).Succeeded, Is.True);

                var ledger = new ExpeditionSettlementLedger(availableRenown: 12);
                ExpeditionSettlementReceipt first = ledger.Settle(run.RunId, ExpeditionOutcome.Extracted,
                    213, inventory, run.StartingEquipmentInstanceIds);
                ExpeditionSettlementReceipt retry = ledger.Settle(run.RunId, ExpeditionOutcome.Extracted,
                    213, inventory, run.StartingEquipmentInstanceIds);

                bool exactPayout = first.LootRenown == 27 && first.RenownAwarded == 240 && first.BalanceAfter == 252 &&
                    first.AlreadyProcessed == false;
                evidence.RecordAssertion("Extraction converts each eligible loot category and grants all Renown",
                    exactPayout, "loot 27; award 213 + 27 = 240; balance 252 from 12", "loot " + first.LootRenown +
                    "; award " + first.RenownAwarded + "; balance " + first.BalanceAfter);

                bool retrySafe = retry.AlreadyProcessed && retry.RenownAwarded == 0 &&
                    retry.BalanceAfter == first.BalanceAfter && ledger.ProcessedRunIds.Count == 1;
                evidence.RecordAssertion("A repeated result for the same RunId does not duplicate its payout",
                    retrySafe, "second award 0; one processed RunId; unchanged balance", "second award " + retry.RenownAwarded +
                    "; processed " + ledger.ProcessedRunIds.Count + "; balance " + ledger.AvailableRenown);
                evidence.RecordEvent("run=" + run.RunId + "; outcome=Extracted; eligibleItems=rare weapon, epic armor, relic; " +
                    "starter kit and consumables excluded; repeated result ignored");

                int exitCode = evidence.Complete();
                Assert.That(added, Is.EqualTo(new[] { "weapon.bellator.rusted_sword", "armor.magus.traveler_robe", "relic.marked_coin" }));
                Assert.That(kitCaptured, Is.True);
                Assert.That(exactPayout, Is.True, "Unexpected settlement payout. Evidence: " + evidence.ReportPath);
                Assert.That(retrySafe, Is.True, "Repeated settlement changed the profile. Evidence: " + evidence.ReportPath);
                Assert.That(exitCode, Is.Zero, "Scenario evidence contains a failure. Evidence: " + evidence.ReportPath);

                void Add(string itemId, ItemRarity rarity)
                {
                    InventoryOperationOutcome result = inventory.TryCollectItem(
                        new InventoryItemStack(ItemInstanceId.New(), itemId, rarity, 0, 1), TransactionId.New());
                    Assert.That(result.Succeeded, Is.True, itemId + " must be accepted by the real item catalog.");
                    added.Add(itemId);
                }
            }
        }
    }
}
