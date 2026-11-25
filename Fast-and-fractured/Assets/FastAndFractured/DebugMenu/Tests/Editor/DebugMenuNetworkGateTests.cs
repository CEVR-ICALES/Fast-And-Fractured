using FastAndFractured.Multiplayer.Testing;
using NUnit.Framework;

namespace FastAndFractured.Multiplayer.Tests
{
    public sealed class DebugMenuNetworkGateTests
    {
        [TestCase(false, false, false, false)]
        [TestCase(true, false, false, false)]
        [TestCase(true, true, false, true)]
        [TestCase(true, false, true, true)]
        [TestCase(true, true, true, true)]
        public void OnlyAStartedServerOrClientMakesASession(bool hasManager, bool serverStarted, bool clientStarted, bool expected)
        {
            Assert.AreEqual(expected, DebugMenuNetworkGate.IsSession(hasManager, serverStarted, clientStarted));
        }

        [Test]
        public void WithNoNetworkRunningTheCheatsStayAvailable()
        {
            Assert.IsFalse(DebugMenuNetworkGate.IsNetworkSession());
        }
    }
}
