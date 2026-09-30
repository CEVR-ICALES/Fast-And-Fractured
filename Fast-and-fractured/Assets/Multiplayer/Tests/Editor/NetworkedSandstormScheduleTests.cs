using NUnit.Framework;

namespace FastAndFractured.Multiplayer.Tests
{
    /// <summary>
    /// The multiplayer storm schedule: the server publishes the tick the storm starts at, every peer's
    /// HUD counts down to it, and each death brings it forward (GameLoopManager.OnCharacterDied in
    /// singleplayer). Until 2026-09-30 nothing started the storm in multiplayer at all and the HUD
    /// storm timer sat at 00:00.
    /// </summary>
    public class NetworkedSandstormScheduleTests
    {
        [TestCase(190f, ExpectedResult = "03:10")]
        [TestCase(59.1f, ExpectedResult = "01:00")]
        [TestCase(0.2f, ExpectedResult = "00:01")]
        [TestCase(0f, ExpectedResult = "00:00")]
        [TestCase(-3f, ExpectedResult = "00:00")]
        public string Countdown_shows_minutes_and_seconds_rounded_up(float remainingSeconds)
        {
            return NetworkedSandstormController.FormatStormCountdown(remainingSeconds);
        }

        [Test]
        public void A_death_brings_the_storm_forward()
        {
            Assert.AreEqual(9000u, NetworkedSandstormController.AdvancedStormStart(10000u, 5000u, 1000u));
        }

        [Test]
        public void A_death_never_moves_the_storm_into_the_past()
        {
            Assert.AreEqual(5000u, NetworkedSandstormController.AdvancedStormStart(5500u, 5000u, 1000u),
                "the storm starts now, not before now");
            Assert.AreEqual(5000u, NetworkedSandstormController.AdvancedStormStart(5000u, 5000u, 1000u));
        }
    }
}
