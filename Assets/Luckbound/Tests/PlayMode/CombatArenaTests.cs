using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Luckbound.App;
using Luckbound.Content;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Input;
using Luckbound.Core.Saves;
using Luckbound.Gameplay.Combat;
using Luckbound.Gameplay.Enemies;
using Luckbound.Gameplay.Movement;
using Luckbound.Presentation.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Luckbound.Tests.PlayMode
{
    public sealed class CombatArenaTests : LuckboundInputTestFixture
    {
        private Keyboard _keyboard;
        private readonly HashSet<Key> _pressedKeys = new HashSet<Key>();
        private bool _leftHeld;
        private bool _rightHeld;
        private Func<ISaveStore> _previousSaveStoreFactory;
        private string _contractSaveDirectory;
        private bool _restoreSaveStoreFactory;
        private bool _previousAudioListenerPaused;
        private bool _restoreAudioListenerPause;

        [UnityTearDown]
        public IEnumerator RestoreContractTestApplication()
        {
            if (_restoreAudioListenerPause)
            {
                AudioListener.pause = _previousAudioListenerPaused;
                _restoreAudioListenerPause = false;
            }
            if (!_restoreSaveStoreFactory) yield break;
            try
            {
                if (ApplicationBootstrapper.Instance != null)
                    Object.Destroy(ApplicationBootstrapper.Instance.gameObject);
                yield return null;
            }
            finally
            {
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = _previousSaveStoreFactory;
                DeleteContractSaveDirectory(_contractSaveDirectory);
                _restoreSaveStoreFactory = false;
            }
        }

        [Test]
        public void C08EncounterDataDefinesTwoValidWavesAndKeepsEverySpawnClearOfTheEntrance()
        {
            TextAsset json = Resources.Load<TextAsset>("Luckbound/Data/Encounters/C08Arena");
            Assert.That(json, Is.Not.Null);
            CombatEncounterData encounter = CombatEncounterJsonLoader.Load(json.text);

            Assert.That(encounter.schemaVersion, Is.EqualTo(2));
            Assert.That(encounter.encounterId, Is.EqualTo("encounter.c08_arena"));
            Assert.That(encounter.waves, Has.Length.EqualTo(2));
            CollectionAssert.AreEqual(new[] { "wave.slimes", "wave.huesoguardias" },
                encounter.waves.Select(wave => wave.id).ToArray());
            string[] spawnIds = encounter.waves.SelectMany(wave => wave.spawns)
                .Select(spawn => spawn.spawnId).ToArray();
            CollectionAssert.AreEqual(new[]
            {
                "spawn.slime.01", "spawn.slime.02", "spawn.huesoguardia.01", "spawn.huesoguardia.02"
            }, spawnIds);
            Assert.That(spawnIds.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(spawnIds.Length));
            Assert.That(encounter.waves[0].spawns.Count(spawn => spawn.enemyId == "enemy.slime"), Is.EqualTo(2));
            Assert.That(encounter.waves[1].spawns.Count(spawn => spawn.enemyId == "enemy.huesoguardia"), Is.EqualTo(2));
            Assert.That(encounter.waves.SelectMany(wave => wave.spawns).All(spawn =>
                Vector2.Distance(new Vector2(spawn.x, spawn.y), new Vector2(encounter.entranceX, encounter.entranceY)) >= 8f), Is.True);

            string invalid = json.text.Replace("\"x\": 0, \"y\": -3.5", "\"x\": -11.5, \"y\": -3.5");
            Assert.Throws<System.FormatException>(() => CombatEncounterJsonLoader.Load(invalid));

            string wrongSchema = json.text.Replace("\"schemaVersion\": 2", "\"schemaVersion\": 1");
            Assert.Throws<System.FormatException>(() => CombatEncounterJsonLoader.Load(wrongSchema));

            const string firstSpawnIdField = "\"spawnId\": \"spawn.slime.01\"";
            string missingSpawnId = ReplaceFirstOccurrence(json.text, firstSpawnIdField + ", ", string.Empty);
            Assert.Throws<System.FormatException>(() => CombatEncounterJsonLoader.Load(missingSpawnId));

            string nullSpawnId = ReplaceFirstOccurrence(json.text, firstSpawnIdField, "\"spawnId\": null");
            Assert.Throws<System.FormatException>(() => CombatEncounterJsonLoader.Load(nullSpawnId));

            string[] invalidSpawnIds =
            {
                "spawn.sl\u00edme.01", "spawn/slime.01", "spawn\\slime.01", "spawn slime.01",
                " spawn.slime.01", "spawn.slime.01 "
            };
            foreach (string invalidSpawnId in invalidSpawnIds)
            {
                string jsonSpawnId = invalidSpawnId.Replace("\\", "\\\\");
                string invalidSpawnJson = ReplaceFirstOccurrence(json.text, firstSpawnIdField,
                    "\"spawnId\": \"" + jsonSpawnId + "\"");
                if (invalidSpawnId.IndexOf('\\') >= 0)
                {
                    CombatEncounterData deserializedInvalidSpawn = JsonUtility.FromJson<CombatEncounterData>(invalidSpawnJson);
                    Assert.That(deserializedInvalidSpawn, Is.Not.Null);
                    Assert.That(deserializedInvalidSpawn.waves[0].spawns[0].spawnId, Is.EqualTo(invalidSpawnId),
                        "JSON decoding should preserve one literal backslash in spawnId");
                }
                Assert.Throws<System.FormatException>(() => CombatEncounterJsonLoader.Load(invalidSpawnJson),
                    "Invalid spawnId should be rejected: " + invalidSpawnId);
            }

            string duplicateSpawnId = json.text.Replace("spawn.slime.02", "spawn.slime.01");
            Assert.Throws<System.FormatException>(() => CombatEncounterJsonLoader.Load(duplicateSpawnId));

            string duplicateAcrossWaves = ReplaceFirstOccurrence(json.text,
                "\"spawnId\": \"spawn.huesoguardia.01\"", "\"spawnId\": \"spawn.slime.01\"");
            Assert.Throws<System.FormatException>(() => CombatEncounterJsonLoader.Load(duplicateAcrossWaves));
        }

        [Test]
        public void C08SpawnIdUniquenessUsesOrdinalCaseSensitiveComparison()
        {
            TextAsset json = Resources.Load<TextAsset>("Luckbound/Data/Encounters/C08Arena");
            Assert.That(json, Is.Not.Null);

            string caseVariant = ReplaceFirstOccurrence(json.text,
                "\"spawnId\": \"spawn.slime.02\"", "\"spawnId\": \"Spawn.Slime.01\"");
            CombatEncounterData encounter = CombatEncounterJsonLoader.Load(caseVariant);
            string[] spawnIds = encounter.waves.SelectMany(wave => wave.spawns)
                .Select(spawn => spawn.spawnId).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "spawn.slime.01", "Spawn.Slime.01", "spawn.huesoguardia.01", "spawn.huesoguardia.02"
            }, spawnIds);
            Assert.That(StringComparer.OrdinalIgnoreCase.Equals(spawnIds[0], spawnIds[1]), Is.True);
            Assert.That(spawnIds.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(spawnIds.Length));
        }

        [Test]
        public void C08SpawnIdsStayExplicitAcrossSpawnOrderAndPositionChangesWhileWaveOrderIsPreserved()
        {
            TextAsset json = Resources.Load<TextAsset>("Luckbound/Data/Encounters/C08Arena");
            Assert.That(json, Is.Not.Null);

            const string firstSpawn = "{ \"spawnId\": \"spawn.slime.01\", \"enemyId\": \"enemy.slime\", \"x\": 0, \"y\": -3.5 }";
            const string secondSpawn = "{ \"spawnId\": \"spawn.slime.02\", \"enemyId\": \"enemy.slime\", \"x\": 4.5, \"y\": -3.5 }";
            string reordered = ReplaceFirstOccurrence(json.text, firstSpawn,
                "{ \"spawnId\": \"spawn.slime.02\", \"enemyId\": \"enemy.slime\", \"x\": 12, \"y\": -3.5 }");
            reordered = ReplaceFirstOccurrence(reordered, secondSpawn,
                "{ \"spawnId\": \"spawn.slime.01\", \"enemyId\": \"enemy.slime\", \"x\": 9, \"y\": -3.5 }");

            CombatEncounterData encounter = CombatEncounterJsonLoader.Load(reordered);
            CollectionAssert.AreEqual(new[] { "wave.slimes", "wave.huesoguardias" },
                encounter.waves.Select(wave => wave.id).ToArray());
            CollectionAssert.AreEqual(new[] { "spawn.slime.02", "spawn.slime.01" },
                encounter.waves[0].spawns.Select(spawn => spawn.spawnId).ToArray());
            CollectionAssert.AreEqual(new[] { 12f, 9f },
                encounter.waves[0].spawns.Select(spawn => spawn.x).ToArray());
            CollectionAssert.AreEqual(new[] { "spawn.huesoguardia.01", "spawn.huesoguardia.02" },
                encounter.waves[1].spawns.Select(spawn => spawn.spawnId).ToArray());
        }

        private static string ReplaceFirstOccurrence(string source, string search, string replacement)
        {
            int index = source.IndexOf(search, StringComparison.Ordinal);
            if (index < 0) throw new InvalidOperationException("Expected encounter JSON text was not found: " + search);
            return source.Substring(0, index) + replacement + source.Substring(index + search.Length);
        }

        [UnityTest]
        public IEnumerator PlayerCanWinTheGeneratedArenaWithKeyboardCombatAndWithoutForcedEnemyRemoval()
        {
            _previousAudioListenerPaused = AudioListener.pause;
            _restoreAudioListenerPause = true;
            AudioListener.pause = true;
            using (var evidence = new ScenarioEvidenceRecorder("C08", "C08_RealKeyboardTwoWaveCombat", 17320508))
            {
                _contractSaveDirectory = Path.Combine(Path.GetTempPath(), "LuckboundE10Combat", Guid.NewGuid().ToString("N"));
                var saveStore = new FileSaveStore(_contractSaveDirectory, null);
                _previousSaveStoreFactory = ApplicationBootstrapper.EditorSaveStoreFactoryForTests;
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => saveStore;
                _restoreSaveStoreFactory = true;
                if (ApplicationBootstrapper.Instance != null)
                    Object.Destroy(ApplicationBootstrapper.Instance.gameObject);
                yield return null;
                SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
                _keyboard = InputSystem.AddDevice<Keyboard>();
                InputSystem.AddDevice<Mouse>();
                InputSystem.Update();
                for (int frame = 0; frame < 120 && (ApplicationBootstrapper.Instance == null ||
                    !ApplicationBootstrapper.Instance.SaveDataReady || SceneManager.GetActiveScene().name != "Menu"); frame++)
                    yield return null;

                ApplicationBootstrapper app = ApplicationBootstrapper.Instance;
                Assert.That(app, Is.Not.Null);
                Assert.That(app.SaveDataReady, Is.True);
                ContractProgressionCommitResult acceptedRooms = null;
                yield return Complete(app.AcceptContractAsync("CT01"), value => acceptedRooms = value);
                Assert.That(acceptedRooms.Succeeded, Is.True, acceptedRooms.Message);
                ContractProgressionCommitResult acceptedBoneguards = null;
                yield return Complete(app.AcceptContractAsync("CT03"), value => acceptedBoneguards = value);
                Assert.That(acceptedBoneguards.Succeeded, Is.True, acceptedBoneguards.Message);
                _pressedKeys.Clear();
                _leftHeld = false;
                _rightHeld = false;
                Button arenaButton = GameObject.Find("StartCombatArenaButton")?.GetComponent<Button>();
                bool menuExposesArenaButton = arenaButton != null && arenaButton.interactable;
                if (menuExposesArenaButton) arenaButton.onClick.Invoke();
                evidence.RecordInput(0, "MenuButton", "LUCHAR · ARENA 0.2");
                for (int frame = 0; frame < 120 && SceneManager.GetActiveScene().name != "CombatArena"; frame++) yield return null;
                for (int frame = 0; frame < 180 && Object.FindAnyObjectByType<CombatArenaComposition>() == null; frame++) yield return null;

                CombatArenaComposition arena = Object.FindAnyObjectByType<CombatArenaComposition>();
                Assert.That(arena, Is.Not.Null, "The menu route must load the generated combat arena.");
                Assert.That(menuExposesArenaButton, Is.True, "The arena must be reachable through its visible menu button.");
                PlayerMotor2D player = arena.PlayerMotor;
                Assert.That(player, Is.Not.Null);
                Assert.That(arena.CurrentWave, Is.EqualTo(1));
                Assert.That(arena.LivingEnemyCount, Is.EqualTo(2));
                evidence.RecordAssertion("Arena opens from its menu button with two live Slimes",
                    menuExposesArenaButton && arena.CurrentWave == 1 && arena.Slimes.Count == 2 && arena.LivingEnemyCount == 2,
                    "wave 1; two live Slimes; menu button route",
                    "button=" + menuExposesArenaButton + "; wave=" + arena.CurrentWave + "; slimes=" + arena.Slimes.Count +
                    "; alive=" + arena.LivingEnemyCount);

                var firstWaveSlimes = arena.Slimes.ToArray();
                evidence.RecordInput(1, "Keyboard", "A/D move, J attack, Left Shift dodge, F health potion");
                int simulatedTicks = 0;
                int lastAttackTick = -100;
                int lastDodgeTick = -100;
                int groundedWaitTicks = 0;
                int lastPotionTick = -1000;
                bool attackHeld = false;
                bool waveTransitionRecorded = false;
                bool meleeStayedQueuedUntilTheFollowingFixedTick = false;
                bool enemyDamageAudioRouted = false;
                AudioClip enemyDamageClip = Resources.Load<AudioClip>("Luckbound/Audio/Effects/enemy_damage");
                AudioClip landingClip = Resources.Load<AudioClip>("Luckbound/Audio/Effects/landing");
                bool jumpStartedForLandingTest = false;
                bool sawAirborneAfterLandingTestJump = false;
                bool landingAudioRouted = false;
                int jumpCountBeforeGesture = -1;
                int jumpCountAfterGestureTick = -1;
                long jumpTickBeforeGesture = -1;
                long jumpTickAfterGesture = -1;
                bool? lastPumpSpace = null;
                string lastPumpSpacePhase = "NotSampled";
                int lastPumpSpaceFrame = -1;
                Rigidbody2D jumpBody = player.GetComponent<Rigidbody2D>();
                System.Action<string> recordJumpBoundary = phase => evidence.RecordEvent(
                    "C08 jump boundary=" + phase + "; frame=" + Time.frameCount +
                    "; fixedTime=" + Time.fixedTime.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                    "; updateMode=" + InputSystem.settings.updateMode + "; tick=" + player.SimulationTick +
                    "; successfulJumps=" + player.SuccessfulJumpCount +
                    "; spaceLastPump=" + (lastPumpSpace.HasValue ? lastPumpSpace.Value.ToString() : "NotSampled") +
                    "; spaceSamplePhase=" + lastPumpSpacePhase + "; spaceSampleFrame=" + lastPumpSpaceFrame +
                    "; grounded=" + player.IsGrounded +
                    "; jumpBuffer=" + player.JumpBufferRemaining.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                    "; timeScale=" + Time.timeScale.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                    "; dead=" + player.IsDead + "; stunned=" + player.IsStunned + "; dodging=" + player.IsDodging +
                    "; y=" + player.transform.position.y.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                    "; velocityY=" + (jumpBody == null ? "<missing>" :
                        jumpBody.linearVelocity.y.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
                int pendingAudioEffectsBeforeFirstPlayerGesture = app.AudioRouter.PendingEffectRequestCount;
                while (!player.IsGrounded && groundedWaitTicks < 60)
                {
                    yield return new WaitForFixedUpdate();
                    groundedWaitTicks++;
                }
                if (player.IsGrounded)
                {
                    jumpCountBeforeGesture = player.SuccessfulJumpCount;
                    jumpTickBeforeGesture = player.SimulationTick;
                    recordJumpBoundary("before-enqueue");
                    SetKey(Key.Space, true);
                    InputSystem.Update();
                    lastPumpSpace = _keyboard.spaceKey.isPressed;
                    lastPumpSpacePhase = "after-down-pump";
                    lastPumpSpaceFrame = Time.frameCount;
                    recordJumpBoundary("after-down-pump");
                    evidence.RecordInput(simulatedTicks, "Jump", "Space: one real jump to verify the landing cue");
                    yield return new WaitForFixedUpdate();
                    jumpTickAfterGesture = player.SimulationTick;
                    jumpCountAfterGestureTick = player.SuccessfulJumpCount;
                    recordJumpBoundary("after-fixed");
                    jumpStartedForLandingTest = jumpTickAfterGesture > jumpTickBeforeGesture &&
                        jumpCountAfterGestureTick > jumpCountBeforeGesture;
                    SetKey(Key.Space, false);
                    InputSystem.Update();
                    lastPumpSpace = _keyboard.spaceKey.isPressed;
                    lastPumpSpacePhase = "after-release-pump";
                    lastPumpSpaceFrame = Time.frameCount;
                    recordJumpBoundary("after-release-pump");
                }
                while (simulatedTicks < 12000 && arena.Result == CombatArenaResult.Ongoing && !player.IsDead)
                {
                    IEnumerable<Transform> liveTargets = arena.Slimes.Where(enemy => enemy != null && !enemy.IsDead).Select(enemy => enemy.transform)
                        .Concat(arena.Huesoguardias.Where(enemy => enemy != null && !enemy.IsDead).Select(enemy => enemy.transform));
                    Transform target = liveTargets.OrderBy(item => Mathf.Abs(item.position.x - player.transform.position.x)).FirstOrDefault();
                    if (target == null)
                    {
                        SetMove(0);
                        yield return new WaitForFixedUpdate();
                        simulatedTicks++;
                        continue;
                    }

                    float dx = target.position.x - player.transform.position.x;
                    bool imminentAttack = IsImminentAttack(arena, target, dx);
                    if (imminentAttack && player.IsDodgeReady && simulatedTicks - lastDodgeTick >= 48)
                    {
                        SetMove(dx >= 0f ? -1f : 1f);
                        if (!_pressedKeys.Contains(Key.LeftShift)) SetKey(Key.LeftShift, true);
                        lastDodgeTick = simulatedTicks;
                        evidence.RecordInput(simulatedTicks, "Dodge", "Left Shift away from an enemy telegraph or active attack");
                    }
                    else
                    {
                        if (_pressedKeys.Contains(Key.LeftShift)) SetKey(Key.LeftShift, false);
                        if (Mathf.Abs(dx) > 1.35f) SetMove(dx > 0f ? 1f : -1f);
                        else SetMove(0f);

                        if (Mathf.Abs(dx) <= 1.55f && simulatedTicks - lastAttackTick >= 29)
                        {
                            if (!attackHeld)
                            {
                                SetKey(Key.J, true);
                                attackHeld = true;
                                lastAttackTick = simulatedTicks;
                                evidence.RecordInput(simulatedTicks, "Attack", "J sword attack at melee range");
                            }
                        }
                        else if (attackHeld)
                        {
                            SetKey(Key.J, false);
                            attackHeld = false;
                        }
                    }

                    if (player.CurrentHealth <= 55 && player.HealthPotionCharges > 0 &&
                        simulatedTicks - lastPotionTick >= 60 && !player.IsPreparingHealthPotion)
                    {
                        SetKey(Key.F, true);
                        evidence.RecordInput(simulatedTicks, "Consumable", "F potion at health=" + player.CurrentHealth);
                        lastPotionTick = simulatedTicks;
                    }
                    else if (_pressedKeys.Contains(Key.F) && simulatedTicks > lastPotionTick)
                    {
                        SetKey(Key.F, false);
                    }

                    int queuedBeforeTick = player.MeleeImpactsQueuedCount;
                    yield return new WaitForFixedUpdate();
                    simulatedTicks++;
                    if (player.MeleeImpactsQueuedCount > queuedBeforeTick)
                    {
                        ProjectileImpactTarget2D[] targets = Object.FindObjectsByType<ProjectileImpactTarget2D>(FindObjectsSortMode.None);
                        if (targets.Any(target => target.PendingMeleeImpactCount > 0)) meleeStayedQueuedUntilTheFollowingFixedTick = true;
                    }
                    int acceptedEnemyImpacts = arena.Slimes.Sum(enemy => enemy == null ? 0 : enemy.MeleeImpactCount + enemy.ProjectileImpactCount) +
                        arena.Huesoguardias.Sum(enemy => enemy == null ? 0 : enemy.MeleeImpactCount + enemy.ProjectileImpactCount);
                    if (!enemyDamageAudioRouted && acceptedEnemyImpacts > 0)
                    {
                        yield return null;
                        RuntimeAudioDirector audioDirector = app.GetComponent<RuntimeAudioDirector>();
                        enemyDamageAudioRouted = enemyDamageClip != null && audioDirector != null &&
                            audioDirector.GetComponentsInChildren<AudioSource>(true).Any(source => source.clip == enemyDamageClip);
                    }
                    if (jumpStartedForLandingTest && !player.IsGrounded)
                        sawAirborneAfterLandingTestJump = true;
                    if (!landingAudioRouted && sawAirborneAfterLandingTestJump && player.IsGrounded)
                    {
                        yield return null;
                        RuntimeAudioDirector audioDirector = app.GetComponent<RuntimeAudioDirector>();
                        landingAudioRouted = landingClip != null && audioDirector != null &&
                            audioDirector.GetComponentsInChildren<AudioSource>(true).Any(source => source.clip == landingClip);
                    }
                    if (attackHeld && simulatedTicks - lastAttackTick >= 1)
                    {
                        SetKey(Key.J, false);
                        attackHeld = false;
                    }

                    if (!waveTransitionRecorded && arena.CurrentWave == 2 && firstWaveSlimes.All(enemy => enemy.IsDead) && arena.Huesoguardias.Count == 2)
                    {
                        evidence.RecordEvent("Wave 1 Slimes confirmed dead; wave 2 Huesoguardias spawned at tick " + simulatedTicks + ".");
                        waveTransitionRecorded = true;
                    }
                }

                SetMove(0f);
                foreach (Key key in _pressedKeys.ToArray()) SetKey(key, false);

                bool won = arena.Result == CombatArenaResult.Victory;
                bool bothSlimesDied = firstWaveSlimes.Length == 2 && firstWaveSlimes.All(enemy => enemy != null && enemy.IsDead);
                bool bothHuesoguardiasDied = arena.Huesoguardias.Count == 2 && arena.Huesoguardias.All(enemy => enemy != null && enemy.IsDead);
                int meleeImpacts = firstWaveSlimes.Sum(enemy => enemy == null ? 0 : enemy.MeleeImpactCount) +
                    arena.Huesoguardias.Sum(enemy => enemy == null ? 0 : enemy.MeleeImpactCount);
                int queuedImpacts = player.MeleeImpactsQueuedCount;
                evidence.RecordEvent("ticks=" + simulatedTicks + "; result=" + arena.Result + "; playerHealth=" + player.CurrentHealth +
                    "; meleeQueued=" + queuedImpacts + "; slimesDead=" + bothSlimesDied + "; huesoguardiasDead=" + bothHuesoguardiasDied +
                    "; wave=" + arena.CurrentWave);
                evidence.RecordAssertion("The player uses the real melee overlap path against targets",
                    queuedImpacts > 0 && meleeImpacts > 0,
                    "at least one queued and applied melee impact",
                    "queued=" + queuedImpacts + "; applied=" + meleeImpacts);
                evidence.RecordAssertion("Melee contact stays queued until a later 60 Hz simulation tick",
                    meleeStayedQueuedUntilTheFollowingFixedTick,
                    "a collected hit remains pending during its collection tick",
                    "delayedResolutionObserved=" + meleeStayedQueuedUntilTheFollowingFixedTick);
                evidence.RecordAssertion("Accepted physical enemy impacts request the enemy_damage effect while audio output stays paused",
                    enemyDamageAudioRouted,
                    "an applied Slime or Huesoguardia impact assigns the enemy_damage clip to an audio voice",
                    "routed=" + enemyDamageAudioRouted + "; listenerPaused=" + AudioListener.pause +
                    "; clipAvailable=" + (enemyDamageClip != null));
                evidence.RecordAssertion("Spawn contact stays silent and a real jump landing requests the landing effect",
                    pendingAudioEffectsBeforeFirstPlayerGesture == 0 && jumpStartedForLandingTest &&
                        sawAirborneAfterLandingTestJump && landingAudioRouted,
                    "the player leaves the floor, returns to grounded, and assigns the landing clip to an audio voice",
                    "pendingAtSpawn=" + pendingAudioEffectsBeforeFirstPlayerGesture +
                        "; jumpStarted=" + jumpStartedForLandingTest + "; airborne=" + sawAirborneAfterLandingTestJump +
                    "; routed=" + landingAudioRouted + "; listenerPaused=" + AudioListener.pause +
                    "; clipAvailable=" + (landingClip != null) + "; jumpCountBefore=" + jumpCountBeforeGesture +
                    "; jumpCountAfter=" + jumpCountAfterGestureTick + "; jumpTickBefore=" + jumpTickBeforeGesture +
                    "; jumpTickAfter=" + jumpTickAfterGesture);
                evidence.RecordAssertion("Both Slimes die through combat before the next wave",
                    bothSlimesDied && arena.CurrentWave == 2,
                    "two Slimes dead and wave 2 reached",
                    "dead=" + bothSlimesDied + "; wave=" + arena.CurrentWave);
                evidence.RecordAssertion("The two Huesoguardias die through combat without forced removal",
                    bothHuesoguardiasDied,
                    "two Huesoguardias enter Dead after melee impacts",
                    "dead=" + bothHuesoguardiasDied + "; states=" + string.Join(",", arena.Huesoguardias.Select(enemy => enemy == null ? "missing" : enemy.State.ToString())));
                evidence.RecordAssertion("Victory requires clearing both configured encounter waves",
                    won && arena.LivingEnemyCount == 0 && !player.IsDead,
                    "Victory; zero living enemies; player alive",
                    "result=" + arena.Result + "; alive=" + arena.LivingEnemyCount + "; playerHealth=" + player.CurrentHealth);

                int clearedRoomsAfterCombat = app.ContractProgression.GetProgress("CT01");
                int boneguardsAfterCombat = app.ContractProgression.GetProgress("CT03");
                for (int frame = 0; frame < 10; frame++) yield return null;
                bool demoLeftExpeditionContractProgressUnchanged = won && clearedRoomsAfterCombat == 0 && boneguardsAfterCombat == 0 &&
                    app.ContractProgression.GetProgress("CT01") == 0 && app.ContractProgression.GetProgress("CT03") == 0;
                evidence.RecordAssertion("The standalone combat demo does not advance Expedition contracts",
                    demoLeftExpeditionContractProgressUnchanged, "CT01=0 and CT03=0 after a physical demo victory",
                    "won=" + won + "; CT01=" + clearedRoomsAfterCombat + "; CT03=" + boneguardsAfterCombat +
                    "; afterExtraFrames=" + app.ContractProgression.GetProgress("CT01") + "/" +
                    app.ContractProgression.GetProgress("CT03"));

                Object.Destroy(app.gameObject);
                yield return null;
                SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
                for (int frame = 0; frame < 120 && (ApplicationBootstrapper.Instance == null ||
                    !ApplicationBootstrapper.Instance.SaveDataReady || SceneManager.GetActiveScene().name != "Menu"); frame++)
                    yield return null;
                app = ApplicationBootstrapper.Instance;
                bool demoDidNotPersistExpeditionContractProgress = app != null &&
                    app.ContractProgression.GetProgress("CT01") == 0 && app.ContractProgression.GetProgress("CT03") == 0;
                evidence.RecordAssertion("Standalone demo victories leave Expedition contract progress unchanged after restart",
                    demoDidNotPersistExpeditionContractProgress, "reloaded profile CT01=0 and CT03=0",
                    app == null ? "application missing after restart" : "CT01=" +
                    app.ContractProgression.GetProgress("CT01") + "; CT03=" + app.ContractProgression.GetProgress("CT03"));
                int exitCode = evidence.Complete();
                yield return null;

                Assert.That(exitCode, Is.Zero, "C08 real keyboard combat scenario failed. Evidence: " + evidence.ReportPath);
                Assert.That(demoLeftExpeditionContractProgressUnchanged, Is.True);
                Assert.That(demoDidNotPersistExpeditionContractProgress, Is.True);
            }
        }

        private static IEnumerator Complete<T>(Task<T> task, Action<T> completed)
        {
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted) throw task.Exception.Flatten();
            if (task.IsCanceled) throw new OperationCanceledException();
            completed(task.GetAwaiter().GetResult());
        }

        private static void DeleteContractSaveDirectory(string directory)
        {
            if (string.IsNullOrEmpty(directory)) return;
            string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            string fullDirectory = Path.GetFullPath(directory);
            if (!fullDirectory.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Refusing to delete a save directory outside the system temp root.");
            if (Directory.Exists(fullDirectory)) Directory.Delete(fullDirectory, recursive: true);
        }

        private bool IsImminentAttack(CombatArenaComposition arena, Transform target, float dx)
        {
            foreach (Slime2D slime in arena.Slimes)
            {
                if (slime != null && slime.transform == target &&
                    (slime.State == SlimeBehaviorState.Attack ||
                     slime.State == SlimeBehaviorState.Telegraph && slime.TelegraphTicksRemaining <= 3) && Mathf.Abs(dx) < 3.4f)
                    return true;
            }

            foreach (Huesoguardia2D enemy in arena.Huesoguardias)
            {
                if (enemy != null && enemy.transform == target &&
                    (enemy.State == HuesoguardiaBehaviorState.Attack ||
                     enemy.State == HuesoguardiaBehaviorState.Telegraph && enemy.TelegraphTicksRemaining <= 3) && Mathf.Abs(dx) < 3.5f)
                    return true;
            }

            return false;
        }

        private void SetMove(float direction)
        {
            bool left = direction < -0.1f;
            bool right = direction > 0.1f;
            if (_leftHeld != left)
            {
                SetKey(Key.A, left);
                _leftHeld = left;
            }
            if (_rightHeld != right)
            {
                SetKey(Key.D, right);
                _rightHeld = right;
            }
        }

        private void SetKey(Key key, bool pressed)
        {
            if (pressed) _pressedKeys.Add(key);
            else _pressedKeys.Remove(key);
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(_pressedKeys.ToArray()));
        }
    }
}
