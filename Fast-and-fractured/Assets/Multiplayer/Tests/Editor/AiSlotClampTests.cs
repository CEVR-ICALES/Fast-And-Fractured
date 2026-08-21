using NUnit.Framework;
using FastAndFractured.Multiplayer;

namespace FastAndFractured.Multiplayer.Tests
{
    /// <summary>
    /// Tests for AI slot clamping logic without FishNet runtime (pure logic extracted into a helper).
    /// The actual FishNet SyncVar behaviour is verified at runtime; these tests cover the clamping math.
    /// </summary>
    [TestFixture]
    public class AiSlotClampTests
    {
        private const int MAX_PLAYERS = NetworkedLobbyManager.MAX_PLAYERS;

        private static int ClampAiSlots(int requestedAi, int humanCount) =>
            UnityEngine.Mathf.Clamp(requestedAi, 0, UnityEngine.Mathf.Max(0, MAX_PLAYERS - humanCount));

        [Test]
        public void NegativeRequest_ClampsToZero()
        {
            Assert.AreEqual(0, ClampAiSlots(-5, 2));
        }

        [Test]
        public void ExceedingMax_ClampsToFreeSlots()
        {
            // 3 humans → max AI = 5
            Assert.AreEqual(5, ClampAiSlots(10, 3));
        }

        [Test]
        public void FullHumanLobby_ClampsToZeroAi()
        {
            Assert.AreEqual(0, ClampAiSlots(3, MAX_PLAYERS));
        }

        [Test]
        public void NoHumans_AllSlotsAvailableForAi()
        {
            Assert.AreEqual(MAX_PLAYERS, ClampAiSlots(MAX_PLAYERS, 0));
        }

        [Test]
        public void ExactLimit_Accepted()
        {
            // 2 humans + 6 AI = 8 total
            Assert.AreEqual(6, ClampAiSlots(6, 2));
        }

        [Test]
        public void MaxPlayersConstant_IsEight()
        {
            Assert.AreEqual(8, MAX_PLAYERS);
        }

        [Test]
        public void TotalSlotCount_NeverExceedsMax()
        {
            for (int humans = 0; humans <= MAX_PLAYERS; humans++)
            {
                int ai = ClampAiSlots(MAX_PLAYERS, humans);
                Assert.LessOrEqual(humans + ai, MAX_PLAYERS,
                    $"humans={humans} ai={ai} exceeds MAX_PLAYERS={MAX_PLAYERS}");
            }
        }
    }
}
