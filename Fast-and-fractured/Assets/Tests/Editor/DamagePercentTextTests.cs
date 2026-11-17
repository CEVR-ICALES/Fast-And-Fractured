using FastAndFractured.Multiplayer;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace FastAndFractured.Tests
{
    public sealed class DamagePercentTextTests
    {
        private GameObject _go;
        private DamagePercentText _text;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject(nameof(DamagePercentTextTests), typeof(RectTransform), typeof(TextMeshProUGUI));
            _text = _go.AddComponent<DamagePercentText>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        [Test]
        public void FullEnduranceShowsZeroPercent() => Assert.AreEqual(0, DamagePercentText.PercentOf(0f));

        [Test]
        public void AnyDamageShowsAtLeastOnePercent() => Assert.AreEqual(1, DamagePercentText.PercentOf(0.001f));

        [TestCase(0.42f, 42)]
        [TestCase(0.421f, 43)]
        public void DamageShowsAsWholePercent(float damage, int percent) => Assert.AreEqual(percent, DamagePercentText.PercentOf(damage));

        [Test]
        public void HundredPercentOnlyWhenTheEnduranceIsGone()
        {
            Assert.AreEqual(99, DamagePercentText.PercentOf(0.996f));
            Assert.AreEqual(100, DamagePercentText.PercentOf(1f));
        }

        [TestCase(1.37f, 137)]
        [TestCase(3f, 300)]
        public void PercentKeepsCountingPastHundred(float damage, int percent) =>
            Assert.AreEqual(percent, DamagePercentText.PercentOf(damage));

        [Test]
        public void ColourKeepsDarkeningPastHundred()
        {
            Color hundred = _text.ColorFor(1f);
            Color oneFifty = _text.ColorFor(1.5f);
            Color twoHundred = _text.ColorFor(2f);
            Assert.Greater(hundred.r, oneFifty.r);
            Assert.Greater(oneFifty.r, twoHundred.r);
            Assert.Greater(twoHundred.r, twoHundred.g * 4f);
        }

        [Test]
        public void PercentSignIsSmallerThanTheNumber() =>
            Assert.AreEqual("57<size=55%>%</size>", DamagePercentText.Format(57, 0.55f));

        [Test]
        public void ColourHeatsUpFromWhiteToRed()
        {
            Color none = _text.ColorFor(0f);
            Color half = _text.ColorFor(0.5f);
            Color full = _text.ColorFor(1f);
            Assert.AreEqual(Color.white, none);
            Assert.Greater(none.g, half.g);
            Assert.Greater(half.g, full.g);
            Assert.Greater(full.r, full.g * 4f);
        }

        [Test]
        public void DamageHasNoCeilingButNoNegative()
        {
            _text.SetDamage(1.7f);
            Assert.AreEqual(1.7f, _text.Damage, 1e-5f);
            _text.SetDamage(-0.3f);
            Assert.AreEqual(0f, _text.Damage);
        }

        [Test]
        public void CarWithoutNetworkedStatsIsNeverDead() => Assert.IsFalse(NetworkedStatsController.IsDeadCar(null));
    }
}
