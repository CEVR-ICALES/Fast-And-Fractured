using NUnit.Framework;

namespace FastAndFractured.Tests
{
    public sealed class StormOverflowTests
    {
        private const float STEP = 0.02f;

        [Test]
        public void AFullBarDoesNotKillInTheStorm()
        {
            Assert.IsFalse(StormOverflow.Explodes(1f));
            Assert.IsFalse(StormOverflow.Explodes(5f));
            Assert.IsTrue(StormOverflow.Explodes(10f), "1000 % is the end.");
        }

        [Test]
        public void TheDamageReachesAThousandPercentInTheTimeGiven()
        {
            float damage = 1f;
            float seconds = 0f;
            while (!StormOverflow.Explodes(damage) && seconds < 60f)
            {
                damage = StormOverflow.Grow(damage, STEP);
                seconds += STEP;
            }

            Assert.AreEqual(StormOverflow.SECONDS_FROM_FULL_BAR, seconds, 2f * STEP);
            Assert.AreEqual(StormOverflow.EXPLODES_AT, damage, 0.001f, "It never shows more than 1000 %.");
        }

        [Test]
        public void EveryHundredComesFasterThanTheLast()
        {
            float previous = float.MaxValue;
            for (int hundred = 1; hundred < 10; hundred++)
            {
                float seconds = StormOverflow.SecondsToExplode(hundred) - StormOverflow.SecondsToExplode(hundred + 1);
                Assert.Less(seconds, previous, hundred + "00 % to " + (hundred + 1) + "00 %");
                previous = seconds;
            }
        }

        [Test]
        public void ACarAlreadyPastAFullBarStartsFromItsOwnDamage()
        {
            Assert.AreEqual(StormOverflow.SECONDS_FROM_FULL_BAR, StormOverflow.SecondsToExplode(0.4f), 0.001f, "The bar has to fill first.");
            Assert.Less(StormOverflow.SecondsToExplode(3f), StormOverflow.SecondsToExplode(1f));
            Assert.AreEqual(0f, StormOverflow.SecondsToExplode(12f), 0.001f);
            Assert.AreEqual(3f, StormOverflow.Grow(3f, 0f), 0.001f);
        }
    }
}
