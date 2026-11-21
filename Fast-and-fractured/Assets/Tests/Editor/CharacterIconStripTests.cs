using NUnit.Framework;

namespace FastAndFractured.Tests
{
    public sealed class CharacterIconStripTests
    {
        [Test]
        public void OnlyTheCarsOfTheMatchGetAnIcon()
        {
            Assert.AreEqual(8, IngameEventsManager.UsedIconSlots(slots: 12, characters: 8, nameCodes: 8), "Twelve slots, eight cars: four stay hidden.");
            Assert.AreEqual(3, IngameEventsManager.UsedIconSlots(12, 3, 8));
            Assert.AreEqual(8, IngameEventsManager.UsedIconSlots(8, 12, 12), "Never more icons than slots.");
            Assert.AreEqual(0, IngameEventsManager.UsedIconSlots(12, 0, 0));
        }
    }
}
