using FastAndFractured.Progression;
using NUnit.Framework;

namespace FastAndFractured.Multiplayer.Tests
{
    /// <summary>The level each player sends to the room (shown next to their name).</summary>
    public class RoomPlayerLevelTests
    {
        [TestCase(0, 1)]
        [TestCase(-5, 1)]
        [TestCase(7, 7)]
        [TestCase(999, ProgressionMath.MAX_LEVEL)]
        public void ClientLevel_IsKeptInRange(int sent, int stored) => Assert.AreEqual(stored, NetworkedLobbyManager.ClampLevel(sent));
    }
}
