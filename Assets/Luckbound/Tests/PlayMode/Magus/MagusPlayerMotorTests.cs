using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Luckbound.App;
using Luckbound.Content;
using Luckbound.Core.Combat;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Input;
using Luckbound.Gameplay.Combat;
using Luckbound.Gameplay.Combat.Magus;
using Luckbound.Gameplay.Enemies;
using Luckbound.Gameplay.Movement;
using Luckbound.Gameplay.Rooms;
using Luckbound.Presentation.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using EntityId = Luckbound.Core.Identifiers.EntityId;

namespace Luckbound.Tests.PlayMode.Magus
{
    public sealed class MagusPlayerMotorTests
    {
        private readonly List<MotorFixture> _fixtures = new List<MotorFixture>();
        private readonly List<GameObject> _temporaryAudioObjects = new List<GameObject>();
        private bool _previousAudioListenerPause;
        private Vector2 _previousGravity;
        private float _previousTimeScale;

        [SetUp]
        public void SetUp()
        {
            _previousGravity = Physics2D.gravity;
            _previousTimeScale = Time.timeScale;
            _previousAudioListenerPause = AudioListener.pause;
            Physics2D.gravity = new Vector2(0f, -9.81f);
            Time.timeScale = 1f;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int index = _temporaryAudioObjects.Count - 1; index >= 0; index--)
            {
                GameObject temporaryObject = _temporaryAudioObjects[index];
                if (temporaryObject != null) UnityEngine.Object.DestroyImmediate(temporaryObject);
            }
            _temporaryAudioObjects.Clear();

            for (int index = _fixtures.Count - 1; index >= 0; index--)
            {
                _fixtures[index].Dispose();
            }

            _fixtures.Clear();
            Physics2D.gravity = _previousGravity;
            Time.timeScale = _previousTimeScale;
            AudioListener.pause = _previousAudioListenerPause;
            yield return null;
        }

        [UnityTest]
        public IEnumerator EmberStaffBasicAttackUsesPlayerInputAndRealProjectileWithoutEnergyCost()
        {
            MotorFixture fixture = CreateFixture();
            ProjectileImpactTarget2D target = fixture.CreateTarget("Magus_Basic_Target", new Vector2(2.2f, 2f));
            Physics2D.SyncTransforms();

            fixture.Input.Press(PlayerAction.Attack);
            yield return new WaitForFixedUpdate();
            MagusBasicAttackSnapshot snapshot = fixture.Motor.CurrentMagusBasicAttack;
            fixture.Input.Release(PlayerAction.Attack);

            for (int tick = 0; tick < 40 && target.PendingImpactCount == 0; tick++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(fixture.Motor.IsMagus, Is.True);
            Assert.That(fixture.Motor.MagusBasicAttackActivationCount, Is.EqualTo(1));
            Assert.That(fixture.Motor.MagusBasicAttackProjectileSpawnCount, Is.EqualTo(1));
            Assert.That(fixture.Motor.MeleeAttackActivationCount, Is.Zero, "Magus input must not fall through to Bellator melee.");
            Assert.That(snapshot.WasStartedThisTick, Is.True);
            Assert.That(snapshot.BaseDamage, Is.EqualTo(12m));
            Assert.That(snapshot.ProjectileElement, Is.EqualTo("fire"));
            Assert.That(snapshot.Projectile.SpeedUnitsPerSecond, Is.EqualTo(10m));
            Assert.That(snapshot.Projectile.LifetimeTicks, Is.EqualTo(180));
            Assert.That(snapshot.Projectile.MaximumTargets, Is.EqualTo(1));
            Assert.That(snapshot.MovementSpeedMultiplier, Is.EqualTo(0.65m));
            Assert.That(fixture.Motor.CurrentEnergy, Is.EqualTo(50), "The ember-staff basic attack is free.");
            Assert.That(target.PendingImpactCount, Is.EqualTo(1), "The projectile must hit through Unity 2D physics.");
            Assert.That(target.TryDequeueImpact(out ProjectileImpact impact), Is.True);
            Assert.That(impact.SourceActorId, Is.EqualTo(fixture.ActorId));
            Assert.That(impact.TargetActorId, Is.EqualTo(target.ActorId));
            Assert.That(impact.BaseDamage, Is.EqualTo(12m));

            fixture.Room.Dispose();
            Assert.That(fixture.ProjectileScope.IsDisposed, Is.True);
            Assert.That(fixture.ProjectileScope.ActiveProjectileCount, Is.Zero);
            Assert.That(fixture.Motor.IsAttacking, Is.False, "Closing the room clears the actor's room-bound attack state.");
        }

        [UnityTest]
        public IEnumerator EmberStaffChoosesNearestEligibleTargetInsideAimCone()
        {
            MotorFixture fixture = CreateFixture();
            ProjectileImpactTarget2D nearest = fixture.CreateTarget("Magus_Aim_Nearest", new Vector2(1.5f, 1.7f));
            ProjectileImpactTarget2D farther = fixture.CreateTarget("Magus_Aim_Farther", new Vector2(4f, 1f));
            Physics2D.SyncTransforms();

            fixture.Input.Press(PlayerAction.Attack);
            yield return new WaitForFixedUpdate();
            fixture.Input.Release(PlayerAction.Attack);

            for (int tick = 0; tick < 40 && nearest.PendingImpactCount == 0 && farther.PendingImpactCount == 0; tick++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(nearest.PendingImpactCount, Is.EqualTo(1),
                "The closest visible target inside the 35-degree cone must receive the physical shot.");
            Assert.That(farther.PendingImpactCount, Is.Zero,
                "A farther eligible target must not win while the nearer target is available.");
        }

        [UnityTest]
        public IEnumerator EmberStaffRejectsOutOfConeTargetAndFallsBackHorizontally()
        {
            MotorFixture fixture = CreateFixture();
            ProjectileImpactTarget2D target = fixture.CreateTarget("Magus_Aim_Out_Of_Cone", new Vector2(2.2f, 3f));

            yield return AssertBasicAttackFallsBackHorizontally(fixture, target);
        }

        [UnityTest]
        public IEnumerator EmberStaffRejectsTargetBehindPlayerAndFallsBackHorizontally()
        {
            MotorFixture fixture = CreateFixture();
            ProjectileImpactTarget2D target = fixture.CreateTarget("Magus_Aim_Behind", new Vector2(-2.2f, 1f));

            yield return AssertBasicAttackFallsBackHorizontally(fixture, target);
        }

        [UnityTest]
        public IEnumerator EmberStaffRejectsOccludedTargetAndFallsBackHorizontally()
        {
            MotorFixture fixture = CreateFixture();
            ProjectileImpactTarget2D target = fixture.CreateTarget("Magus_Aim_Occluded", new Vector2(2.2f, 2f));
            fixture.CreateGroundObstacle("Magus_Aim_Occluding_Wall", new Vector2(1.15f, 1.3f), new Vector2(0.16f, 0.7f));

            yield return AssertBasicAttackFallsBackHorizontally(fixture, target);
        }

        [UnityTest]
        public IEnumerator SelectedFireballRoutesOneCueForMultiTargetBurnAndNoAdditionalCueOnBurnTick()
        {
            MotorFixture fixture = CreateFixture();
            ProjectileImpactTarget2D directTarget = fixture.CreateTarget("Magus_Fireball_Audio_Direct", new Vector2(2.2f, 2f));
            ProjectileImpactTarget2D splashTarget = fixture.CreateTarget("Magus_Fireball_Audio_Splash", new Vector2(2.8f, 2f));
            Physics2D.SyncTransforms();

            ApplicationAudioRouter router = CreateBurnAudioBridge(fixture.Motor);
            RuntimeAudioDirector director = router.GetComponent<RuntimeAudioDirector>();
            AudioListener.pause = true;
            Assert.That(router.UserGestureObserved, Is.False);

            yield return ActivateSkill(fixture, PlayerAction.Skill1);

            for (int step = 0; step < 40 && directTarget.PendingImpactCount == 0; step++)
                yield return new WaitForFixedUpdate();

            Assert.That(directTarget.PendingImpactCount, Is.EqualTo(1),
                "The selected Fireball must reach its direct target through the production projectile.");
            Assert.That(splashTarget.PendingImpactCount, Is.EqualTo(1),
                "The same explosion must affect a second eligible actor.");
            MagusStatusEffectTarget2D directStatus = directTarget.GetComponent<MagusStatusEffectTarget2D>();
            MagusStatusEffectTarget2D splashStatus = splashTarget.GetComponent<MagusStatusEffectTarget2D>();
            Assert.That(directStatus, Is.Not.Null);
            Assert.That(splashStatus, Is.Not.Null);

            for (int step = 0; step < 5 && (!directStatus.StatusSnapshot.IsBurning || !splashStatus.StatusSnapshot.IsBurning); step++)
                yield return new WaitForFixedUpdate();

            Assert.That(directStatus.StatusSnapshot.IsBurning, Is.True);
            Assert.That(splashStatus.StatusSnapshot.IsBurning, Is.True);
            Assert.That(router.PendingEffectRequestCount, Is.EqualTo(1),
                "One Fireball impact requests one burn cue even when it burns multiple actors.");
            Assert.That(PendingEffectIdAt(router, 0), Is.EqualTo(ApplicationAudioRouter.BurnEffectId));
            Assert.That(router.UserGestureObserved, Is.False,
                "The cue must remain queued behind the first real-input gate.");
            Assert.That(director.OneShotSourceCount, Is.Zero);
            Assert.That(director.ActiveOneShotSourceCount, Is.Zero);

            for (int step = 0; step < 140 && directStatus.PendingBurnTickCount == 0; step++)
                yield return new WaitForFixedUpdate();

            Assert.That(directStatus.PendingBurnTickCount, Is.GreaterThan(0),
                "The production status clock must reach a later periodic burn tick.");
            Assert.That(router.PendingEffectRequestCount, Is.EqualTo(1),
                "Periodic damage ticks do not request additional burn cues.");
            Assert.That(router.UserGestureObserved, Is.False);
            Assert.That(director.OneShotSourceCount, Is.Zero);
            Assert.That(director.ActiveOneShotSourceCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator FireballCollisionWithoutEligibleTargetLeavesBurnAudioSilent()
        {
            MotorFixture fixture = CreateFixture();
            ApplicationAudioRouter router = CreateBurnAudioBridge(fixture.Motor);
            RuntimeAudioDirector director = router.GetComponent<RuntimeAudioDirector>();
            AudioListener.pause = true;

            var invalidTarget = new GameObject("Magus_Fireball_No_Eligible_Target");
            invalidTarget.layer = LayerMask.NameToLayer("Enemies");
            invalidTarget.transform.position = new Vector2(
                fixture.Motor.transform.position.x + 2.2f,
                fixture.Motor.transform.position.y + 0.25f);
            CircleCollider2D invalidTargetCollider = invalidTarget.AddComponent<CircleCollider2D>();
            invalidTargetCollider.radius = 0.45f;
            _temporaryAudioObjects.Add(invalidTarget);
            Physics2D.SyncTransforms();

            yield return ActivateSkill(fixture, PlayerAction.Skill1);

            for (int step = 0; step < 40 && fixture.ProjectileScope.ActiveProjectileCount > 0; step++)
                yield return new WaitForFixedUpdate();

            Assert.That(fixture.ProjectileScope.ActiveProjectileCount, Is.Zero,
                "The physical Fireball must collide with and return from the collider without an initialized impact target.");
            Assert.That(router.PendingEffectRequestCount, Is.Zero,
                "A collision without an eligible non-own target must not request burn audio.");
            Assert.That(router.UserGestureObserved, Is.False);
            Assert.That(director.OneShotSourceCount, Is.Zero);
            Assert.That(director.ActiveOneShotSourceCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator SelectedFireballUsesPlayerInputPhysicalImpactAndBurnQueue()
        {
            MotorFixture fixture = CreateFixture();
            ProjectileImpactTarget2D target = fixture.CreateTarget("Magus_Fireball_Target", new Vector2(2.2f, 2f));
            Physics2D.SyncTransforms();

            fixture.Input.Press(PlayerAction.Skill1);
            for (int tick = 0; tick < 12 && fixture.Motor.MagusSkillActivationCount == 0; tick++)
            {
                yield return new WaitForFixedUpdate();
            }

            fixture.Input.Release(PlayerAction.Skill1);
            MagusCombatResult activation = fixture.Motor.LastMagusCombatResult;
            Assert.That(fixture.Motor.MagusSkillActivationCount, Is.EqualTo(1));
            Assert.That(activation.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.Activated));
            Assert.That(activation.SkillOutcome.SkillId, Is.EqualTo(MagusSkillLoadout.FireballId));
            Assert.That(activation.SkillOutcome.EnergySpent, Is.EqualTo(8));
            Assert.That(fixture.Motor.CurrentEnergy, Is.EqualTo(42));
            Assert.That(fixture.Motor.IsMagusSkillReady(MagusSkillLoadout.FireballId), Is.False);

            for (int tick = 0; tick < 40 && target.PendingImpactCount == 0; tick++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(target.PendingImpactCount, Is.EqualTo(1), "The selected skill must launch a physical fireball through PlayerMotor2D.");
            Assert.That(target.TryDequeueImpact(out ProjectileImpact directImpact), Is.True);
            Assert.That(directImpact.BaseDamage, Is.EqualTo(24m));
            MagusStatusEffectTarget2D status = target.GetComponent<MagusStatusEffectTarget2D>();
            Assert.That(status, Is.Not.Null);
            for (int tick = 0; tick < 5 && !status.StatusSnapshot.IsBurning; tick++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(status.StatusSnapshot.IsBurning, Is.True);

            for (int tick = 0; tick < 140 && status.PendingBurnTickCount < 2; tick++)
            {
                yield return new WaitForFixedUpdate();
            }

            var burnTicks = new List<MagusBurnTick>();
            while (status.TryDequeueBurnTick(out MagusBurnTick burnTick)) burnTicks.Add(burnTick);
            Assert.That(burnTicks, Has.Count.EqualTo(2));
            Assert.That(burnTicks[0].Damage, Is.EqualTo(3m));
            Assert.That(burnTicks[1].Damage, Is.EqualTo(3m));
            Assert.That(burnTicks[0].HitId, Is.Not.EqualTo(burnTicks[1].HitId));
            Assert.That(burnTicks[0].SourceActorId, Is.EqualTo(fixture.ActorId));
            Assert.That(burnTicks[0].TargetActorId, Is.EqualTo(target.ActorId));
        }

        [UnityTest]
        public IEnumerator SelectedFrostNovaUsesPlayerCommandForPhysicalAreaImpactAndFreeze()
        {
            MotorFixture fixture = CreateFixture(MagusSkillLoadout.FrostNovaId, MagusSkillLoadout.ArcaneSealId);
            ProjectileImpactTarget2D target = fixture.CreateTarget(
                "Magus_PlayerMotor_FrostNova_Target",
                new Vector2(1.8f, 0.75f),
                colliderCount: 3);
            Physics2D.SyncTransforms();

            yield return ActivateSkill(fixture, PlayerAction.Skill1);
            MagusCombatResult activation = fixture.Motor.LastMagusCombatResult;
            long activationTick = fixture.Motor.SimulationTick;

            Assert.That(fixture.Motor.IsMagus, Is.True);
            Assert.That(activation.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.Activated),
                "Skill1 must route through the selected Nova slot on PlayerMotor2D.");
            Assert.That(activation.SkillOutcome.SkillId, Is.EqualTo(MagusSkillLoadout.FrostNovaId));
            Assert.That(activation.SkillOutcome.EnergySpent, Is.EqualTo(12));
            Assert.That(activation.SkillOutcome.ReadyAtTick, Is.EqualTo(activationTick + 360));
            Assert.That(activation.SkillActivation, Is.Not.Null);
            Assert.That(activation.SkillActivation.FrostNova.BaseDamage, Is.EqualTo(18m));
            Assert.That(activation.SkillActivation.FrostNova.Radius, Is.EqualTo(2.5m));
            Assert.That(fixture.Motor.MagusSkillActivationCount, Is.EqualTo(1));
            Assert.That(fixture.Motor.CurrentEnergy, Is.EqualTo(38));
            Assert.That(fixture.Motor.IsMagusSkillReady(MagusSkillLoadout.FrostNovaId), Is.False);
            Assert.That(target.PendingImpactCount, Is.EqualTo(1),
                "The motor-routed nova must use 2D overlap and enqueue one hit for this actor despite three colliders.");
            Assert.That(target.TryDequeueImpact(out ProjectileImpact impact), Is.True);
            Assert.That(impact.SourceActorId, Is.EqualTo(fixture.ActorId));
            Assert.That(impact.TargetActorId, Is.EqualTo(target.ActorId));
            Assert.That(impact.BaseDamage, Is.EqualTo(18m));

            MagusStatusEffectTarget2D status = target.GetComponent<MagusStatusEffectTarget2D>();
            Assert.That(status, Is.Not.Null);
            Assert.That(status.IsFrozen, Is.True, "Nova applies the catalog freeze when the effect activates.");
        }

        [UnityTest]
        public IEnumerator SelectedArcaneSealUsesPlayerCommandAndQueuesFourCatalogPulses()
        {
            MotorFixture fixture = CreateFixture(MagusSkillLoadout.FrostNovaId, MagusSkillLoadout.ArcaneSealId);
            ProjectileImpactTarget2D target = fixture.CreateTarget(
                "Magus_PlayerMotor_ArcaneSeal_Target",
                new Vector2(1.8f, 0.75f),
                colliderCount: 2);
            Physics2D.SyncTransforms();

            yield return ActivateSkill(fixture, PlayerAction.Skill2);
            MagusCombatResult activation = fixture.Motor.LastMagusCombatResult;
            long activationTick = fixture.Motor.SimulationTick;

            Assert.That(activation.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.Activated),
                "Skill2 must route through the selected Arcane Seal slot on PlayerMotor2D.");
            Assert.That(activation.SkillOutcome.SkillId, Is.EqualTo(MagusSkillLoadout.ArcaneSealId));
            Assert.That(activation.SkillOutcome.EnergySpent, Is.EqualTo(16));
            Assert.That(activation.SkillOutcome.ReadyAtTick, Is.EqualTo(activationTick + 480));
            Assert.That(activation.SkillActivation, Is.Not.Null);
            Assert.That(activation.SkillActivation.ArcaneSeal.Radius, Is.EqualTo(2.5m));
            Assert.That(activation.SkillActivation.ArcaneSeal.DurationTicks, Is.EqualTo(240));
            Assert.That(activation.SkillActivation.ArcaneSeal.DamagePerSecond, Is.EqualTo(8m));
            Assert.That(fixture.Motor.CurrentEnergy, Is.EqualTo(34));
            Assert.That(target.PendingImpactCount, Is.EqualTo(1),
                "The seal's first physical overlap pulse is immediate and deduplicates sibling colliders.");

            var impacts = new List<ProjectileImpact>();
            while (target.TryDequeueImpact(out ProjectileImpact pulse)) impacts.Add(pulse);
            Assert.That(impacts, Has.Count.EqualTo(1));
            Assert.That(impacts[0].BaseDamage, Is.EqualTo(8m));

            for (int tick = 0; tick < 59; tick++) yield return new WaitForFixedUpdate();
            Assert.That(target.PendingImpactCount, Is.Zero, "The next 8-damage pulse is due after 60 ticks.");
            yield return new WaitForFixedUpdate();
            Assert.That(target.PendingImpactCount, Is.EqualTo(1));
            while (target.TryDequeueImpact(out ProjectileImpact secondPulse)) impacts.Add(secondPulse);

            for (int tick = 0; tick < 179; tick++) yield return new WaitForFixedUpdate();
            while (target.TryDequeueImpact(out ProjectileImpact nextPulse)) impacts.Add(nextPulse);

            Assert.That(impacts, Has.Count.EqualTo(4),
                "Skills.json schedules the initial pulse and three more at 60-tick intervals before the 240-tick lifetime ends.");
            var hitIds = new HashSet<HitId>();
            foreach (ProjectileImpact impact in impacts)
            {
                Assert.That(impact.BaseDamage, Is.EqualTo(8m));
                Assert.That(hitIds.Add(impact.HitId), Is.True, "Each damage pulse must have its own hit identity.");
            }

            yield return new WaitForFixedUpdate();
            Assert.That(target.PendingImpactCount, Is.Zero, "The room-local seal expires at its catalog duration without a fifth pulse.");
            Assert.That(fixture.Motor.IsMagusSkillReady(MagusSkillLoadout.ArcaneSealId), Is.False);
        }

        [UnityTest]
        public IEnumerator InsufficientEnergyRejectsSelectedArcaneSealWithoutCostOrCooldown()
        {
            MotorFixture fixture = CreateFixture(MagusSkillLoadout.FrostNovaId, MagusSkillLoadout.ArcaneSealId);
            ProjectileImpactTarget2D target = fixture.CreateTarget(
                "Magus_PlayerMotor_InsufficientEnergy_Target",
                new Vector2(1.8f, 0.75f),
                colliderCount: 2);
            Physics2D.SyncTransforms();
            fixture.Motor.SkillUses.SetCurrentEnergy(15);

            fixture.Input.Press(PlayerAction.Skill2);
            yield return new WaitForFixedUpdate();

            SkillUseOutcome rejected = fixture.Motor.LastMagusCombatResult.SkillOutcome;
            Assert.That(rejected.Status, Is.EqualTo(SkillUseStatus.RejectedInsufficientEnergy));
            Assert.That(rejected.SkillId, Is.EqualTo(MagusSkillLoadout.ArcaneSealId));
            Assert.That(rejected.EnergySpent, Is.Zero);
            Assert.That(fixture.Motor.CurrentEnergy, Is.EqualTo(15));
            Assert.That(fixture.Motor.IsMagusSkillReady(MagusSkillLoadout.ArcaneSealId), Is.True,
                "A rejected request must leave the selected skill ready.");
            Assert.That(fixture.Motor.MagusSkillActivationCount, Is.Zero);
            Assert.That(target.PendingImpactCount, Is.Zero, "A rejected seal must not create a physical damage pulse.");
            Assert.That(target.GetComponent<MagusStatusEffectTarget2D>(), Is.Null,
                "A rejected seal must not attach or apply a room status effect.");
        }

        [UnityTest]
        public IEnumerator DodgeAndPhysicalWaveDamageCancelSelectedSkillPreparationBeforeActivation()
        {
            MotorFixture dodged = CreateFixture(MagusSkillLoadout.FrostNovaId, MagusSkillLoadout.ArcaneSealId);
            dodged.Input.Press(PlayerAction.Skill2);
            for (int tick = 0; tick < 5; tick++) yield return new WaitForFixedUpdate();
            Assert.That(dodged.Motor.IsPreparingMagusSkill, Is.True);

            dodged.Input.Release(PlayerAction.Skill2);
            dodged.Input.Press(PlayerAction.Dodge);
            yield return new WaitForFixedUpdate();

            Assert.That(dodged.Motor.LastMagusCombatResult.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.Cancelled));
            Assert.That(dodged.Motor.IsPreparingMagusSkill, Is.False);
            Assert.That(dodged.Motor.DodgeActivationCount, Is.EqualTo(1));
            Assert.That(dodged.Motor.MagusSkillActivationCount, Is.Zero);
            Assert.That(dodged.Motor.CurrentEnergy, Is.EqualTo(50));
            Assert.That(dodged.Motor.IsMagusSkillReady(MagusSkillLoadout.ArcaneSealId), Is.True);
            dodged.Dispose();
            yield return null;

            MotorFixture damaged = CreateFixture(MagusSkillLoadout.FrostNovaId, MagusSkillLoadout.ArcaneSealId);
            damaged.Input.Press(PlayerAction.Skill1);
            for (int tick = 0; tick < 5; tick++) yield return new WaitForFixedUpdate();
            Assert.That(damaged.Motor.IsPreparingMagusSkill, Is.True);

            damaged.CreateDamageWave(5);
            for (int tick = 0; tick < 5 && damaged.Motor.CurrentHealth == 100; tick++) yield return new WaitForFixedUpdate();
            damaged.Input.Release(PlayerAction.Skill1);
            yield return new WaitForFixedUpdate();

            Assert.That(damaged.Motor.CurrentHealth, Is.EqualTo(95), "The production wave must damage the player through its trigger collider.");
            Assert.That(damaged.Motor.LastMagusCombatResult.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.Cancelled));
            Assert.That(damaged.Motor.IsPreparingMagusSkill, Is.False);
            Assert.That(damaged.Motor.MagusSkillActivationCount, Is.Zero);
            Assert.That(damaged.Motor.CurrentEnergy, Is.EqualTo(50));
            Assert.That(damaged.Motor.IsMagusSkillReady(MagusSkillLoadout.FrostNovaId), Is.True);
        }

        [UnityTest]
        public IEnumerator LethalDamageDuringPreparationPreventsMagusActivation()
        {
            MotorFixture fixture = CreateFixture(MagusSkillLoadout.FrostNovaId, MagusSkillLoadout.ArcaneSealId);
            fixture.Input.Press(PlayerAction.Skill2);
            for (int tick = 0; tick < 4; tick++) yield return new WaitForFixedUpdate();
            Assert.That(fixture.Motor.IsPreparingMagusSkill, Is.True);

            fixture.CreateDamageWave(200);
            for (int tick = 0; tick < 5 && !fixture.Motor.IsDead; tick++) yield return new WaitForFixedUpdate();
            fixture.Input.Release(PlayerAction.Skill2);
            yield return new WaitForFixedUpdate();

            Assert.That(fixture.Motor.IsDead, Is.True, "The production wave must deliver the lethal hit through 2D physics.");
            Assert.That(fixture.Motor.CurrentHealth, Is.Zero);
            Assert.That(fixture.Motor.LastMagusCombatResult.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.Cancelled));
            Assert.That(fixture.Motor.IsPreparingMagusSkill, Is.False);
            Assert.That(fixture.Motor.MagusSkillActivationCount, Is.Zero);
            Assert.That(fixture.Motor.CurrentEnergy, Is.EqualTo(50));
            Assert.That(fixture.ProjectileScope.ActiveProjectileCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator MagusCooldownSurvivesRoomRebindingAtItsExactReadyTick()
        {
            MotorFixture fixture = CreateFixture(MagusSkillLoadout.FireballId, MagusSkillLoadout.FrostNovaId);
            yield return ActivateSkill(fixture, PlayerAction.Skill1);

            SkillUseOutcome initialActivation = fixture.Motor.LastMagusCombatResult.SkillOutcome;
            long readyAtTick = initialActivation.ReadyAtTick;
            int activationsBeforeRebind = fixture.Motor.MagusSkillActivationCount;
            Assert.That(initialActivation.SkillId, Is.EqualTo(MagusSkillLoadout.FireballId));
            Assert.That(readyAtTick, Is.EqualTo(fixture.Motor.SimulationTick + 240),
                "Fireball retains its four-second cooldown on the authoritative 60 Hz tick schedule.");

            RoomLifetimeScope disposedRoom = fixture.Room;
            RoomProjectileScope disposedProjectileScope = fixture.ProjectileScope;
            fixture.RebindToFreshRoom();

            Assert.That(disposedRoom.IsDisposed, Is.True);
            Assert.That(disposedProjectileScope.IsDisposed, Is.True);
            Assert.That(fixture.Room, Is.Not.SameAs(disposedRoom));
            Assert.That(fixture.ProjectileScope, Is.Not.SameAs(disposedProjectileScope));
            Assert.That(fixture.Motor.IsMagus, Is.True, "The same player motor retains its Magus loadout across room bindings.");
            Assert.That(fixture.Motor.IsMagusSkillReady(MagusSkillLoadout.FireballId), Is.False,
                "Disposing the previous room must not clear the active skill's cooldown.");

            while (fixture.Motor.SimulationTick < readyAtTick - 2)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(fixture.Motor.SimulationTick, Is.EqualTo(readyAtTick - 2));
            int energyBeforeEarlyRetry = fixture.Motor.CurrentEnergy;
            fixture.Input.Press(PlayerAction.Skill1);
            yield return new WaitForFixedUpdate();

            SkillUseOutcome earlyRetry = fixture.Motor.LastMagusCombatResult.SkillOutcome;
            Assert.That(fixture.Motor.SimulationTick, Is.EqualTo(readyAtTick - 1));
            Assert.That(earlyRetry.Status, Is.EqualTo(SkillUseStatus.RejectedCooldown));
            Assert.That(earlyRetry.SkillId, Is.EqualTo(MagusSkillLoadout.FireballId));
            Assert.That(earlyRetry.ReadyAtTick, Is.EqualTo(readyAtTick));
            Assert.That(earlyRetry.EnergySpent, Is.Zero);
            Assert.That(fixture.Motor.CurrentEnergy, Is.EqualTo(energyBeforeEarlyRetry),
                "A cooldown rejection on the replacement room must not spend energy.");
            Assert.That(fixture.Motor.MagusSkillActivationCount, Is.EqualTo(activationsBeforeRebind));
            Assert.That(fixture.Motor.IsMagusSkillReady(MagusSkillLoadout.FireballId), Is.False);

            // Release and re-press before the next input command so the retry is evaluated exactly at ReadyAtTick.
            fixture.Input.Release(PlayerAction.Skill1);
            fixture.Input.Press(PlayerAction.Skill1);
            yield return new WaitForFixedUpdate();

            SkillUseOutcome boundaryRetry = fixture.Motor.LastMagusCombatResult.SkillOutcome;
            int energyAtBoundary = fixture.Motor.CurrentEnergy;
            Assert.That(fixture.Motor.SimulationTick, Is.EqualTo(readyAtTick));
            Assert.That(fixture.Motor.IsMagusSkillReady(MagusSkillLoadout.FireballId), Is.True);
            Assert.That(boundaryRetry.Status, Is.EqualTo(SkillUseStatus.Preparing),
                "The same skill must begin preparation on its exact ready tick after room rebinding.");
            Assert.That(boundaryRetry.SkillId, Is.EqualTo(MagusSkillLoadout.FireballId));
            Assert.That(boundaryRetry.EnergySpent, Is.Zero);
            Assert.That(fixture.Motor.CurrentEnergy, Is.EqualTo(energyAtBoundary),
                "Energy is spent when preparation completes, not when the ready-tick request starts.");

            fixture.Input.Release(PlayerAction.Skill1);
            for (int tick = 0; tick < 9; tick++) yield return new WaitForFixedUpdate();

            SkillUseOutcome secondActivation = fixture.Motor.LastMagusCombatResult.SkillOutcome;
            Assert.That(secondActivation.Status, Is.EqualTo(SkillUseStatus.Activated));
            Assert.That(secondActivation.SkillId, Is.EqualTo(MagusSkillLoadout.FireballId));
            Assert.That(secondActivation.EnergySpent, Is.EqualTo(8));
            Assert.That(secondActivation.ReadyAtTick, Is.EqualTo(fixture.Motor.SimulationTick + 240),
                "The next cooldown starts on the tick the skill activation completes.");
            Assert.That(fixture.Motor.CurrentEnergy, Is.EqualTo(energyAtBoundary - 8));
            Assert.That(fixture.Motor.MagusSkillActivationCount, Is.EqualTo(activationsBeforeRebind + 1));
        }

        [UnityTest]
        public IEnumerator LeavingRoomClearsMotorBoundSealFreezeAndFlyingProjectile()
        {
            MotorFixture fixture = CreateFixture(MagusSkillLoadout.FrostNovaId, MagusSkillLoadout.ArcaneSealId);
            ProjectileImpactTarget2D target = fixture.CreateTarget(
                "Magus_PlayerMotor_RoomExit_Target",
                new Vector2(1.8f, 0.75f),
                colliderCount: 2,
                keepAfterRoomDisposal: true);
            Physics2D.SyncTransforms();

            yield return ActivateSkill(fixture, PlayerAction.Skill1);
            MagusStatusEffectTarget2D oldStatus = target.GetComponent<MagusStatusEffectTarget2D>();
            Assert.That(oldStatus, Is.Not.Null);
            Assert.That(oldStatus.IsFrozen, Is.True);
            Assert.That(target.PendingImpactCount, Is.EqualTo(1));
            target.ClearPendingImpacts();

            yield return ActivateSkill(fixture, PlayerAction.Skill2);
            Assert.That(fixture.Motor.LastMagusCombatResult.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.Activated));
            Assert.That(target.PendingImpactCount, Is.EqualTo(1), "The active seal has already queued its first pulse.");
            target.ClearPendingImpacts();

            // Face away from the effect target so the basic attack projectile remains in flight at room exit.
            fixture.Input.SetMoveX(-1f);
            yield return new WaitForFixedUpdate();
            Assert.That(fixture.Motor.FacingDirection, Is.EqualTo(-1f));
            fixture.Input.SetMoveX(0f);
            fixture.Input.Press(PlayerAction.Attack);
            yield return new WaitForFixedUpdate();
            fixture.Input.Release(PlayerAction.Attack);

            Projectile2D roomProjectile = fixture.ProjectileScope.GetComponentInChildren<Projectile2D>(includeInactive: true);
            Assert.That(fixture.Motor.MagusBasicAttackProjectileSpawnCount, Is.EqualTo(1));
            Assert.That(roomProjectile, Is.Not.Null);
            Assert.That(roomProjectile.IsPooled, Is.False);
            Assert.That(fixture.ProjectileScope.ActiveProjectileCount, Is.EqualTo(1));

            fixture.Room.Dispose();

            Assert.That(fixture.Room.IsDisposed, Is.True);
            Assert.That(fixture.ProjectileScope.IsDisposed, Is.True);
            Assert.That(fixture.ProjectileScope.ActiveProjectileCount, Is.Zero);
            Assert.That(roomProjectile.IsPooled, Is.True);
            Assert.That(oldStatus.IsFrozen, Is.False);
            Assert.That(oldStatus.StatusSnapshot.IsFrozen, Is.False);
            Assert.That(oldStatus.StatusSnapshot.IsFreezeProtected, Is.False);
            Assert.That(fixture.Motor.IsPreparingMagusSkill, Is.False);
            Assert.That(fixture.Motor.IsAttacking, Is.False);

            for (int tick = 0; tick < 61; tick++) yield return new WaitForFixedUpdate();
            Assert.That(target.PendingImpactCount, Is.Zero, "The disposed room must not emit another Arcane Seal pulse.");

            fixture.Input.Press(PlayerAction.Skill1);
            yield return new WaitForFixedUpdate();
            Assert.That(fixture.Motor.LastMagusCombatResult.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.RejectedInvalid),
                "PlayerMotor2D must stop routing Magus effects after the room binding is disposed.");
            Assert.That(fixture.Motor.MagusSkillActivationCount, Is.EqualTo(2));
            Assert.That(fixture.ProjectileScope.ActiveProjectileCount, Is.Zero);
            Assert.That(target.PendingImpactCount, Is.Zero);
        }

        private static IEnumerator AssertBasicAttackFallsBackHorizontally(
            MotorFixture fixture,
            ProjectileImpactTarget2D target)
        {
            Physics2D.SyncTransforms();
            fixture.Input.Press(PlayerAction.Attack);
            yield return new WaitForFixedUpdate();
            fixture.Input.Release(PlayerAction.Attack);

            Projectile2D projectile = fixture.ProjectileScope.GetComponentInChildren<Projectile2D>(includeInactive: true);
            Assert.That(projectile, Is.Not.Null);
            Assert.That(projectile.IsPooled, Is.False);
            Vector2 velocity = projectile.GetComponent<Rigidbody2D>().linearVelocity;
            Assert.That(velocity.x, Is.GreaterThan(0f), "The fallback shot must retain the player's forward direction.");
            Assert.That(velocity.y, Is.EqualTo(0f).Within(0.0001f), "The fallback shot must remain horizontal.");

            for (int tick = 0; tick < 30 && target.PendingImpactCount == 0; tick++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(target.PendingImpactCount, Is.Zero,
                "The physical horizontal projectile must not hit an invalid or occluded candidate above or behind it.");
        }

        private ApplicationAudioRouter CreateBurnAudioBridge(PlayerMotor2D motor)
        {
            var audioOwner = new GameObject("I06_BurnAudioRouter");
            audioOwner.AddComponent<RuntimeAudioDirector>();
            ApplicationAudioRouter router = audioOwner.AddComponent<ApplicationAudioRouter>();
            router.SetSceneContext("Expedition");
            _temporaryAudioObjects.Add(audioOwner);

            var compositionOwner = new GameObject("I06_BurnAudioComposition");
            ExpeditionGameplayComposition composition = compositionOwner.AddComponent<ExpeditionGameplayComposition>();
            SetPrivateField(composition, "_playerMotor", motor);
            SetPrivateField(composition, "_audioRouter", router);
            MethodInfo bindAudio = typeof(ExpeditionGameplayComposition).GetMethod(
                "BindPlayerMagusAudio",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(bindAudio, Is.Not.Null, "Expedition composition must expose its production player-audio binding.");
            bindAudio.Invoke(composition, null);
            _temporaryAudioObjects.Add(compositionOwner);
            return router;
        }

        private static void SetPrivateField<T>(object instance, string fieldName, T value)
        {
            FieldInfo field = instance.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Missing private composition field: " + fieldName);
            field.SetValue(instance, value);
        }

        private static string PendingEffectIdAt(ApplicationAudioRouter router, int index)
        {
            FieldInfo pendingField = typeof(ApplicationAudioRouter).GetField(
                "_pendingEffectRequests",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(pendingField, Is.Not.Null);
            var requests = (System.Collections.IList)pendingField.GetValue(router);
            object request = requests[index];
            PropertyInfo effectId = request.GetType().GetProperty("EffectId");
            Assert.That(effectId, Is.Not.Null);
            return (string)effectId.GetValue(request);
        }

        private static IEnumerator ActivateSkill(MotorFixture fixture, PlayerAction action)
        {
            int energyBeforePreparation = fixture.Motor.CurrentEnergy;
            int activationsBeforePreparation = fixture.Motor.MagusSkillActivationCount;
            fixture.Input.Press(action);
            yield return new WaitForFixedUpdate();
            Assert.That(fixture.Motor.LastMagusCombatResult.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.Preparing),
                "A selected Magus skill begins its nine-tick preparation from the motor's input command.");
            Assert.That(fixture.Motor.IsPreparingMagusSkill, Is.True);
            Assert.That(fixture.Motor.MagusSkillActivationCount, Is.EqualTo(activationsBeforePreparation),
                "Preparation must not increment the successful activation count.");
            Assert.That(fixture.Motor.CurrentEnergy, Is.EqualTo(energyBeforePreparation),
                "Energy is spent only when the prepared effect begins.");
            long preparationStartTick = fixture.Motor.SimulationTick;

            bool activated = false;
            for (int tick = 0; tick < 11; tick++)
            {
                yield return new WaitForFixedUpdate();
                if (fixture.Motor.LastMagusCombatResult.SkillOutcome.Status != SkillUseStatus.Activated) continue;

                activated = true;
                Assert.That(fixture.Motor.SimulationTick - preparationStartTick, Is.EqualTo(9),
                    "The authoritative catalog requires nine preparation ticks at 60 Hz.");
                break;
            }

            Assert.That(activated, Is.True, "The selected skill must finish preparation through PlayerMotor2D.");
            fixture.Input.Release(action);
        }

        private MotorFixture CreateFixture(params string[] selectedSkillIds)
        {
            if (selectedSkillIds == null || selectedSkillIds.Length == 0)
            {
                selectedSkillIds = new[] { MagusSkillLoadout.FireballId, MagusSkillLoadout.FrostNovaId };
            }
            Assert.That(selectedSkillIds, Has.Length.EqualTo(2), "Every Magus motor fixture must select exactly two skills.");

            int groundLayer = LayerMask.NameToLayer("Ground");
            int enemyLayer = LayerMask.NameToLayer("Enemies");
            Assert.That(groundLayer, Is.GreaterThanOrEqualTo(0), "The Ground layer is required by the player motor.");
            Assert.That(enemyLayer, Is.GreaterThanOrEqualTo(0), "The Enemies layer is required by Magus projectiles.");

            var floor = new GameObject("Magus Player Test Ground");
            floor.layer = groundLayer;
            floor.transform.position = new Vector2(0f, -0.5f);
            BoxCollider2D floorCollider = floor.AddComponent<BoxCollider2D>();
            floorCollider.size = new Vector2(40f, 1f);

            ActorId actorId = ActorId.New();
            var input = new PlayerInputBuffer(actorId);
            var player = new GameObject("Magus Player Motor");
            player.transform.position = new Vector2(0f, 0.75f);
            Rigidbody2D body = player.AddComponent<Rigidbody2D>();
            body.gravityScale = 3f;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            CapsuleCollider2D playerCollider = player.AddComponent<CapsuleCollider2D>();
            playerCollider.direction = CapsuleDirection2D.Vertical;
            playerCollider.size = new Vector2(0.75f, 1.5f);
            PlayerMotor2D motor = player.AddComponent<PlayerMotor2D>();
            LayerMask enemyLayers = 1 << enemyLayer;
            motor.Initialize(actorId, input, 1 << groundLayer, new CombatStats(100, 50, 0), enemyLayers);

            var poolObject = new GameObject("Magus Player Projectile Pool");
            ProjectilePool2D projectilePool = poolObject.AddComponent<ProjectilePool2D>();
            var projectilePrefab = new GameObject("Magus Player Projectile Prefab");
            projectilePrefab.AddComponent<Rigidbody2D>();
            CircleCollider2D projectileCollider = projectilePrefab.AddComponent<CircleCollider2D>();
            projectileCollider.radius = 0.12f;
            projectilePrefab.AddComponent<Projectile2D>();
            projectilePrefab.SetActive(false);
            projectilePool.Initialize(projectilePrefab);

            var roomObject = new GameObject("Magus Player Room Lifetime");
            RoomLifetimeScope room = roomObject.AddComponent<RoomLifetimeScope>();
            room.Initialize(RoomInstanceId.New(), "room.test.magus.player");

            var projectileScopeObject = new GameObject("Magus Player Room Projectiles");
            RoomProjectileScope projectileScope = projectileScopeObject.AddComponent<RoomProjectileScope>();
            projectileScope.Initialize(EntityId.New(), projectilePool);
            room.OwnActor(projectileScopeObject);
            room.OwnResource(projectileScope);

            TextAsset skillsJson = Resources.Load<TextAsset>("Luckbound/Data/Skills");
            Assert.That(skillsJson, Is.Not.Null, "The authoritative Skills.json resource is required.");
            SkillCatalog catalog = SkillCatalogJsonLoader.Load(skillsJson.text);
            motor.ConfigureMagus(
                catalog,
                selectedSkillIds,
                projectilePool,
                projectileScope,
                room,
                enemyLayers);
            Physics2D.SyncTransforms();

            var fixture = new MotorFixture(
                floor,
                player,
                poolObject,
                projectilePrefab,
                room,
                motor,
                input,
                projectileScope,
                catalog,
                selectedSkillIds,
                projectilePool,
                enemyLayers,
                enemyLayer,
                actorId);
            _fixtures.Add(fixture);
            return fixture;
        }

        private sealed class MotorFixture
        {
            private readonly GameObject _floor;
            private readonly GameObject _player;
            private readonly GameObject _poolObject;
            private readonly GameObject _projectilePrefab;
            private readonly int _enemyLayer;
            private readonly SkillCatalog _skillCatalog;
            private readonly string[] _selectedSkillIds;
            private readonly ProjectilePool2D _projectilePool;
            private readonly LayerMask _enemyLayers;
            private readonly List<GameObject> _persistentTargets = new List<GameObject>();
            private bool _disposed;

            public MotorFixture(
                GameObject floor,
                GameObject player,
                GameObject poolObject,
                GameObject projectilePrefab,
                RoomLifetimeScope room,
                PlayerMotor2D motor,
                PlayerInputBuffer input,
                RoomProjectileScope projectileScope,
                SkillCatalog skillCatalog,
                string[] selectedSkillIds,
                ProjectilePool2D projectilePool,
                LayerMask enemyLayers,
                int enemyLayer,
                ActorId actorId)
            {
                _floor = floor;
                _player = player;
                _poolObject = poolObject;
                _projectilePrefab = projectilePrefab;
                Room = room;
                Motor = motor;
                Input = input;
                ProjectileScope = projectileScope;
                _skillCatalog = skillCatalog;
                _selectedSkillIds = (string[])selectedSkillIds.Clone();
                _projectilePool = projectilePool;
                _enemyLayers = enemyLayers;
                _enemyLayer = enemyLayer;
                ActorId = actorId;
            }

            public ActorId ActorId { get; }

            public PlayerMotor2D Motor { get; }

            public PlayerInputBuffer Input { get; }

            public RoomProjectileScope ProjectileScope { get; private set; }

            public RoomLifetimeScope Room { get; private set; }

            public ProjectileImpactTarget2D CreateTarget(string name, Vector2 position, int colliderCount = 1)
            {
                return CreateTarget(name, position, colliderCount, keepAfterRoomDisposal: false);
            }

            public ProjectileImpactTarget2D CreateTarget(
                string name,
                Vector2 position,
                int colliderCount,
                bool keepAfterRoomDisposal)
            {
                Assert.That(colliderCount, Is.GreaterThan(0));
                var targetObject = new GameObject(name);
                targetObject.layer = _enemyLayer;
                targetObject.transform.position = position;
                ProjectileImpactTarget2D target = targetObject.AddComponent<ProjectileImpactTarget2D>();
                target.Initialize(ActorId.New());
                BoxCollider2D collider = targetObject.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(0.8f, 0.8f);
                for (int index = 1; index < colliderCount; index++)
                {
                    var sibling = new GameObject(name + " Collider " + index);
                    sibling.layer = _enemyLayer;
                    sibling.transform.SetParent(targetObject.transform, worldPositionStays: false);
                    sibling.transform.localPosition = new Vector3(index * 0.08f, 0f, 0f);
                    sibling.AddComponent<BoxCollider2D>().size = new Vector2(0.8f, 0.8f);
                }
                if (keepAfterRoomDisposal) _persistentTargets.Add(targetObject);
                else Room.OwnActor(targetObject);
                return target;
            }

            public GameObject CreateGroundObstacle(string name, Vector2 position, Vector2 size)
            {
                var obstacle = new GameObject(name);
                obstacle.layer = LayerMask.NameToLayer("Ground");
                obstacle.transform.position = position;
                obstacle.AddComponent<BoxCollider2D>().size = size;
                Room.OwnActor(obstacle);
                return obstacle;
            }

            public void CreateDamageWave(int damage)
            {
                var waveObject = new GameObject("Magus Player Test Damage Wave");
                waveObject.transform.position = _player.transform.position + Vector3.left;
                waveObject.AddComponent<Rigidbody2D>();
                BoxCollider2D collider = waveObject.AddComponent<BoxCollider2D>();
                collider.size = Vector2.one;
                CustodianWaveHazard2D wave = waveObject.AddComponent<CustodianWaveHazard2D>();
                wave.Initialize(
                    Motor,
                    ActorId.New(),
                    damage,
                    direction: 1f,
                    speed: 12f,
                    lifetimeTicks: 90,
                    minimumX: -10f,
                    maximumX: 10f);
                Room.OwnActor(waveObject);
                Physics2D.SyncTransforms();
            }

            public void RebindToFreshRoom()
            {
                RoomLifetimeScope previousRoom = Room;
                previousRoom.Dispose();
                Object.Destroy(previousRoom.gameObject);

                var roomObject = new GameObject("Magus Player Replacement Room Lifetime");
                Room = roomObject.AddComponent<RoomLifetimeScope>();
                Room.Initialize(RoomInstanceId.New(), "room.test.magus.player.replacement");

                var projectileScopeObject = new GameObject("Magus Player Replacement Room Projectiles");
                ProjectileScope = projectileScopeObject.AddComponent<RoomProjectileScope>();
                ProjectileScope.Initialize(EntityId.New(), _projectilePool);
                Room.OwnActor(projectileScopeObject);
                Room.OwnResource(ProjectileScope);

                Motor.ConfigureMagus(
                    _skillCatalog,
                    _selectedSkillIds,
                    _projectilePool,
                    ProjectileScope,
                    Room,
                    _enemyLayers);
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                Room?.Dispose();
                foreach (GameObject target in _persistentTargets)
                {
                    if (target != null) Object.Destroy(target);
                }
                _persistentTargets.Clear();
                if (_player != null) Object.Destroy(_player);
                if (_floor != null) Object.Destroy(_floor);
                if (_poolObject != null) Object.Destroy(_poolObject);
                if (_projectilePrefab != null) Object.Destroy(_projectilePrefab);
                if (Room != null) Object.Destroy(Room.gameObject);
            }
        }
    }
}
