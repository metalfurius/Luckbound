using System;
using System.IO;
using System.Linq;
using Luckbound.App;
using Luckbound.Core.Combat;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Saves;
using NUnit.Framework;
using UnityEngine;

namespace Luckbound.Tests.PlayMode
{
    public sealed class ExpeditionSampleRunTests
    {
        [Test]
        public void SampleCreationIsExplicitAndDoesNotReplaceCampaignOrShareInventory()
        {
            string[] skills = { BellatorSkillLoadout.ChargeId, BellatorSkillLoadout.CircularCutId };
            ExpeditionRunConfiguration sample = ExpeditionRunConfigurationFactory.CreateSample(
                BellatorSkillLoadout.ClassId, ExpeditionRunConfiguration.HumanRaceId, skills, 2, 2, 1);
            ExpeditionRunConfiguration campaign = ExpeditionRunConfigurationFactory.Create(
                BellatorSkillLoadout.ClassId, ExpeditionRunConfiguration.HumanRaceId, skills);
            Assert.That(sample.Route.GenerationVersion, Is.EqualTo(2));
            Assert.That(sample.Route.Nodes.Count, Is.EqualTo(5));
            Assert.That(sample.Route.Validate(), Is.Empty);
            Assert.That(campaign.Route.GenerationVersion, Is.EqualTo(1));
            Assert.That(campaign.Route.Nodes.Count, Is.EqualTo(24));
            Assert.That(sample.RunId, Is.Not.EqualTo(campaign.RunId));
            Assert.That(sample.Inventory, Is.Not.SameAs(campaign.Inventory));
            Assert.That(sample.Inventory.Weapon.InstanceId, Is.Not.EqualTo(campaign.Inventory.Weapon.InstanceId));
            Assert.That(sample.StartingHealth, Is.EqualTo(110));
            Assert.That(sample.StartingEnergy, Is.EqualTo(55));
            Assert.That(sample.StartingHealthPotions, Is.EqualTo(3));
        }

        [Test]
        public void EverySampleCheckpointSurvivesRealRepositoryRestartWithItsVersionAndHistory()
        {
            string directory = Path.Combine(Path.GetTempPath(), "LuckboundSampleSaves", Guid.NewGuid().ToString("N"));
            try
            {
                ExpeditionRunConfiguration run = ExpeditionRunConfigurationFactory.CreateSample(
                    BellatorSkillLoadout.ClassId, ExpeditionRunConfiguration.HumanRaceId,
                    new[] { BellatorSkillLoadout.ChargeId, BellatorSkillLoadout.CircularCutId }, 2, 2, 1);
                var repository = new SaveRepository(new FileSaveStore(directory, null));
                for (int index = 0; index < run.Route.MandatoryNodeIds.Count; index++)
                {
                    ExpeditionSaveData state = ExpeditionSaveMapper.Create(run);
                    state.currentNodeId = run.Route.MandatoryNodeIds[index];
                    state.visitedNodeIds = run.Route.MandatoryNodeIds.Take(index + 1).ToArray();
                    state.currentHealth = 61;
                    state.currentEnergy = 19;
                    state.roomEntryCheckpointJson = JsonUtility.ToJson(ExpeditionSaveMapper.CreateCheckpoint(state));
                    SaveWriteResult write = repository.SaveProfileAsync(ProfileSaveData.CreateNewProfile(), state)
                        .GetAwaiter().GetResult();
                    Assert.That(write.Succeeded, Is.True, write.Message);

                    var restarted = new SaveRepository(new FileSaveStore(directory, null));
                    SaveLoadResult<ProfileSaveEnvelope> loaded = restarted.LoadProfileAsync().GetAwaiter().GetResult();
                    Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.LoadedPrimary), loaded.Message);
                    ExpeditionRunConfiguration restored = ExpeditionSaveMapper.Restore(loaded.Data.activeExpedition,
                        out ExpeditionSaveData resumed);
                    Assert.That(restored.RunId, Is.EqualTo(run.RunId));
                    Assert.That(restored.Route.GenerationVersion, Is.EqualTo(2));
                    Assert.That(restored.Route.CanonicalForm, Is.EqualTo(run.Route.CanonicalForm));
                    Assert.That(resumed.currentNodeId, Is.EqualTo(state.currentNodeId));
                    Assert.That(resumed.visitedNodeIds, Is.EqualTo(state.visitedNodeIds));
                    Assert.That(resumed.currentHealth, Is.EqualTo(61));
                    Assert.That(resumed.currentEnergy, Is.EqualTo(19));
                    Assert.That(restored.HealthUpgradeLevel, Is.EqualTo(2));
                    Assert.That(restored.EnergyUpgradeLevel, Is.EqualTo(2));
                    Assert.That(restored.AdditionalPotionUpgradeLevel, Is.EqualTo(1));
                    repository = restarted;
                }
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }
    }
}
