using System.Net.NetworkInformation;
using NUnit.Framework;

namespace FastAndFractured.Multiplayer.Tests
{
    /// <summary>Wi-Fi / cable detection and the ping → signal bars scale shared by every view.</summary>
    public class NetworkLinkTests
    {
        [TestCase(NetworkInterfaceType.Wireless80211, LinkKind.Wifi)]
        [TestCase(NetworkInterfaceType.Ethernet, LinkKind.Ethernet)]
        [TestCase(NetworkInterfaceType.GigabitEthernet, LinkKind.Ethernet)]
        [TestCase(NetworkInterfaceType.FastEthernetT, LinkKind.Ethernet)]
        [TestCase(NetworkInterfaceType.Loopback, LinkKind.Unknown)]
        [TestCase(NetworkInterfaceType.Tunnel, LinkKind.Unknown)]
        [TestCase(NetworkInterfaceType.Ppp, LinkKind.Unknown)]
        public void Classify_MapsAdapterTypes(NetworkInterfaceType type, LinkKind expected) =>
            Assert.AreEqual(expected, NetworkLink.Classify(type));

        [TestCase(-1, 0)]
        [TestCase(0, 4)]
        [TestCase(40, 4)]
        [TestCase(41, 3)]
        [TestCase(80, 3)]
        [TestCase(150, 2)]
        [TestCase(151, 1)]
        [TestCase(900, 1)]
        public void Bars_FallWithPing(int pingMs, int bars) =>
            Assert.AreEqual(bars, NetworkLink.Bars(pingMs));

        [TestCase("1", LinkKind.Ethernet)]
        [TestCase("2", LinkKind.Wifi)]
        [TestCase("0", LinkKind.Unknown)]
        [TestCase("7", LinkKind.Unknown)]      // a future value an old build does not know
        [TestCase("", LinkKind.Unknown)]
        [TestCase(null, LinkKind.Unknown)]     // older hosts do not advertise it
        public void Parse_AdvertisedLink(string value, LinkKind expected) =>
            Assert.AreEqual(expected, NetworkLink.Parse(value));

        [Test]
        public void Detect_DoesNotThrow() =>
            Assert.DoesNotThrow(() => NetworkLink.Detect());
    }
}
