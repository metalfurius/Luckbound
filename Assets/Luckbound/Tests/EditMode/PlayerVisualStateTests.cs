using Luckbound.Core.Presentation;
using NUnit.Framework;

namespace Luckbound.Tests.Core
{
    public sealed class PlayerVisualStateTests
    {
        [Test]
        public void VisualStateIdsRemainStableForAnimatorAssets()
        {
            Assert.That((int)PlayerVisualState.Idle, Is.EqualTo(0));
            Assert.That((int)PlayerVisualState.Moving, Is.EqualTo(1));
            Assert.That((int)PlayerVisualState.Airborne, Is.EqualTo(2));
            Assert.That((int)PlayerVisualState.Attacking, Is.EqualTo(3));
            Assert.That((int)PlayerVisualState.Dodging, Is.EqualTo(4));
            Assert.That((int)PlayerVisualState.Stunned, Is.EqualTo(5));
            Assert.That((int)PlayerVisualState.Dead, Is.EqualTo(6));
        }

        [Test]
        public void ResolverAppliesTheProductPriorityOrder()
        {
            Assert.That(Resolve(true, true, true, true, false, 6f), Is.EqualTo(PlayerVisualState.Dead));
            Assert.That(Resolve(false, true, true, true, false, 6f), Is.EqualTo(PlayerVisualState.Stunned));
            Assert.That(Resolve(false, false, true, true, true, 6f), Is.EqualTo(PlayerVisualState.Dodging));
            Assert.That(Resolve(false, false, false, true, false, 6f), Is.EqualTo(PlayerVisualState.Attacking));
            Assert.That(Resolve(false, false, false, false, false, 6f), Is.EqualTo(PlayerVisualState.Airborne));
            Assert.That(Resolve(false, false, false, false, true, 0.051f), Is.EqualTo(PlayerVisualState.Moving));
            Assert.That(Resolve(false, false, false, false, true, 0.05f), Is.EqualTo(PlayerVisualState.Idle));
        }

        private static PlayerVisualState Resolve(
            bool isDead,
            bool isStunned,
            bool isDodging,
            bool isAttacking,
            bool isGrounded,
            float velocityX)
        {
            return PlayerVisualStateResolver.Resolve(
                isDead, isStunned, isDodging, isAttacking, isGrounded, velocityX);
        }
    }
}
