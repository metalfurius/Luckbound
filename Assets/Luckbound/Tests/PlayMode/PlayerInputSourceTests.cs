using Luckbound.Core.Identifiers;
using Luckbound.Core.Input;
using Luckbound.Gameplay.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Luckbound.Tests.PlayMode
{
    public sealed class PlayerInputSourceTests : InputTestFixture
    {
        [Test]
        public void KeyboardAndGamepadProduceTheSameGameplayActions()
        {
            PlayerCommand[] keyboardCommands;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            using (PlayerInputSource source = PlayerInputSource.CreateDefault(ActorId.New()))
            {
                Press(keyboard.spaceKey);
                PlayerCommand jump = source.Consume(0);
                Release(keyboard.spaceKey);
                Press(keyboard.jKey);
                PlayerCommand attack = source.Consume(1);
                Press(keyboard.dKey);
                PlayerCommand move = source.Consume(2);
                keyboardCommands = new[] { jump, attack, move };
            }

            var gamepad = InputSystem.AddDevice<Gamepad>();
            using (PlayerInputSource source = PlayerInputSource.CreateDefault(ActorId.New()))
            {
                Press(gamepad.buttonSouth);
                PlayerCommand jump = source.Consume(0);
                Release(gamepad.buttonSouth);
                Press(gamepad.buttonWest);
                PlayerCommand attack = source.Consume(1);
                Set(gamepad.leftStick, new Vector2(0.9f, 0f));
                PlayerCommand move = source.Consume(2);

                Assert.That(jump.WasPressed(PlayerAction.Jump), Is.True);
                Assert.That(attack.WasPressed(PlayerAction.Attack), Is.True);
                Assert.That(move.MoveX, Is.GreaterThan(0.5f));
                Assert.That(keyboardCommands[0].Pressed, Is.EqualTo(jump.Pressed));
                Assert.That(keyboardCommands[1].Pressed, Is.EqualTo(attack.Pressed));
                Assert.That(keyboardCommands[2].MoveX, Is.EqualTo(1f).Within(0.0001f));
            }
        }

        [Test]
        public void ButtonTapBetweenTicksKeepsPressAndReleaseEdges()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            using (PlayerInputSource source = PlayerInputSource.CreateDefault(ActorId.New()))
            {
                Press(keyboard.spaceKey, queueEventOnly: true);
                Release(keyboard.spaceKey, queueEventOnly: true);
                InputSystem.Update();

                PlayerCommand command = source.Consume(0);

                Assert.That(command.WasPressed(PlayerAction.Jump), Is.True);
                Assert.That(command.WasReleased(PlayerAction.Jump), Is.True);
                Assert.That(command.IsHeld(PlayerAction.Jump), Is.False);
                Assert.That(source.Consume(1).Pressed, Is.EqualTo(PlayerAction.None));
            }
        }
    }
}
