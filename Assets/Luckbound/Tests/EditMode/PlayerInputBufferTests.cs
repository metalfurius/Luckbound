using System;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Input;
using NUnit.Framework;

namespace Luckbound.Tests.Core
{
    public sealed class PlayerInputBufferTests
    {
        [Test]
        public void PressAndReleaseBetweenTicksAreBothPreservedForOneCommand()
        {
            var buffer = new PlayerInputBuffer(ActorId.New());
            buffer.SetMoveX(-0.4f);
            buffer.Press(PlayerAction.Jump);
            buffer.Release(PlayerAction.Jump);

            PlayerCommand first = buffer.Consume(0);
            PlayerCommand second = buffer.Consume(1);

            Assert.That(first.WasPressed(PlayerAction.Jump), Is.True);
            Assert.That(first.WasReleased(PlayerAction.Jump), Is.True);
            Assert.That(first.IsHeld(PlayerAction.Jump), Is.False);
            Assert.That(first.MoveX, Is.EqualTo(-0.4f).Within(0.0001f));
            Assert.That(second.WasPressed(PlayerAction.Jump), Is.False);
            Assert.That(second.WasReleased(PlayerAction.Jump), Is.False);
            Assert.That(second.MoveX, Is.EqualTo(-0.4f).Within(0.0001f));
            Assert.That(first.Tick, Is.EqualTo(0));
            Assert.That(first.ActorId, Is.EqualTo(buffer.ActorId));
        }

        [Test]
        public void HeldStatePersistsAcrossTicksAndReleaseIsOneShot()
        {
            var buffer = new PlayerInputBuffer(ActorId.New());
            buffer.Press(PlayerAction.Attack);

            PlayerCommand first = buffer.Consume(0);
            PlayerCommand second = buffer.Consume(1);
            buffer.Release(PlayerAction.Attack);
            PlayerCommand third = buffer.Consume(2);
            PlayerCommand fourth = buffer.Consume(3);

            Assert.That(first.WasPressed(PlayerAction.Attack), Is.True);
            Assert.That(first.IsHeld(PlayerAction.Attack), Is.True);
            Assert.That(second.WasPressed(PlayerAction.Attack), Is.False);
            Assert.That(second.IsHeld(PlayerAction.Attack), Is.True);
            Assert.That(third.WasReleased(PlayerAction.Attack), Is.True);
            Assert.That(third.IsHeld(PlayerAction.Attack), Is.False);
            Assert.That(fourth.WasReleased(PlayerAction.Attack), Is.False);
        }

        [Test]
        public void MovementIsClampedAndTicksMustBeConsumedOnceInOrder()
        {
            var buffer = new PlayerInputBuffer(ActorId.New());
            buffer.SetMoveX(4f);
            Assert.That(buffer.Consume(0).MoveX, Is.EqualTo(1f));

            buffer.SetMoveX(-4f);
            Assert.That(buffer.Consume(1).MoveX, Is.EqualTo(-1f));
            Assert.That(() => buffer.Consume(1), Throws.InvalidOperationException);
            Assert.That(() => buffer.Consume(-1), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => buffer.SetMoveX(float.NaN), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => buffer.Press(PlayerAction.Jump | PlayerAction.Attack), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void ReleasingAllClearsHeldInputAndMovement()
        {
            var buffer = new PlayerInputBuffer(ActorId.New());
            buffer.SetMoveX(1f);
            buffer.Press(PlayerAction.Dodge);

            buffer.ReleaseAll();
            PlayerCommand command = buffer.Consume(0);

            Assert.That(command.MoveX, Is.Zero);
            Assert.That(command.IsHeld(PlayerAction.Dodge), Is.False);
            Assert.That(command.WasReleased(PlayerAction.Dodge), Is.True);
        }
    }
}
