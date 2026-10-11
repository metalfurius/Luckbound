using Luckbound.Core;
using NUnit.Framework;

namespace Luckbound.Tests.Core
{
    public sealed class CoreAssemblyTests
    {
        [Test]
        public void CoreIdentifiesTheProductWithoutAnEngineDependency()
        {
            Assert.That(CoreAssemblyMarker.ProductName, Is.EqualTo("Luckbound"));
        }
    }
}
