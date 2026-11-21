using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Tests
{
    public class CarNameplateTests
    {
        private static readonly float[] DAMAGES = { 0f, 0.3f, 0.6f, 0.8f, 1f, 2f };
        private GameObject _root;

        [SetUp]
        public void SetUp() => _root = new GameObject("NameplateTests", typeof(RectTransform));

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void EnemyPercent_CopiesTheColoursOfThePlayersOwnPercent()
        {
            DamagePercentText hud = HudPercent();
            var blueToGreen = new Gradient();
            blueToGreen.SetKeys(
                new[] { new GradientColorKey(Color.blue, 0f), new GradientColorKey(Color.green, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            typeof(DamagePercentText).GetField("damageColors", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hud, blueToGreen);

            DamagePercentText enemy = CarNameplate.CreateDamagePercent(Plate(), hud, null);

            Assert.AreNotSame(hud, enemy);
            Assert.AreEqual(Color.blue, enemy.ColorFor(0f), "The designer's colours, not the script defaults.");
            foreach (float damage in DAMAGES)
                Assert.AreEqual(hud.ColorFor(damage), enemy.ColorFor(damage), "damage " + damage);
        }

        [Test]
        public void EnemyPercent_KeepsTheHudFontAndIsLaidOutCentredOnThePlate()
        {
            DamagePercentText hud = HudPercent();
            var hudRect = (RectTransform)hud.transform;
            hudRect.anchorMin = hudRect.anchorMax = Vector2.one;
            hudRect.anchoredPosition = new Vector2(2f, 6f);
            hudRect.localScale = Vector3.one * 1.3f;
            hud.GetComponent<TMP_Text>().fontSize = 54f;

            DamagePercentText enemy = CarNameplate.CreateDamagePercent(Plate(), hud, null);

            var rect = (RectTransform)enemy.transform;
            var text = enemy.GetComponent<TMP_Text>();
            Assert.AreEqual(54f, text.fontSize, 0.001f);
            Assert.AreEqual(TextAlignmentOptions.Center, text.alignment);
            Assert.AreEqual(Vector2.zero, rect.anchoredPosition, "The shake is measured from here.");
            Assert.AreEqual(Vector3.one, rect.localScale);
            Assert.IsTrue(enemy.gameObject.activeInHierarchy);
        }

        [Test]
        public void EnemyPercent_IsStillShownWhenThereIsNoHudToCopy()
        {
            DamagePercentText enemy = CarNameplate.CreateDamagePercent(Plate(), null, null);

            Assert.IsNotNull(enemy);
            Assert.IsNotNull(enemy.GetComponent<TMP_Text>());
            Assert.AreEqual(Color.white, enemy.ColorFor(0f));
        }

        [Test]
        public void EnemyPercent_ReadsTheSameScaleAsTheHud()
        {
            Assert.AreEqual(0f, CarNameplate.Damage01(0f), 0.0001f);
            Assert.AreEqual(0.875f, CarNameplate.Damage01(87.5f), 0.0001f);
            Assert.AreEqual(2.5f, CarNameplate.Damage01(250f), 0.0001f, "Damage past 100% keeps counting, as on the HUD.");
            Assert.AreEqual(0f, CarNameplate.Damage01(-5f), 0.0001f);
        }

        [Test]
        public void EnemyPercent_ShrinksWithDistance()
        {
            float near = CarNameplate.PercentScale(0f, 54f);
            float far = CarNameplate.PercentScale(1f, 54f);

            Assert.Greater(near, far);
            Assert.Greater(far, 0.3f, "Still readable at the far end.");
            Assert.LessOrEqual(near, 1f, "Never bigger than the player's own.");
        }

        [Test]
        public void OwnCarInSinglePlayer_IsRecognisedThroughItsHierarchy()
        {
            var car = new GameObject("Car").transform;
            car.SetParent(_root.transform);
            var body = new GameObject("Body").transform;
            body.SetParent(car);
            var other = new GameObject("Other").transform;
            other.SetParent(_root.transform);

            Assert.IsTrue(CarNameplate.IsSameCar(car, car));
            Assert.IsTrue(CarNameplate.IsSameCar(body, car));
            Assert.IsTrue(CarNameplate.IsSameCar(car, body));
            Assert.IsFalse(CarNameplate.IsSameCar(car, other));
            Assert.IsFalse(CarNameplate.IsSameCar(car, null));
        }

        private RectTransform Plate()
        {
            var plate = new GameObject("Plate", typeof(RectTransform));
            plate.transform.SetParent(_root.transform, false);
            return (RectTransform)plate.transform;
        }

        private DamagePercentText HudPercent()
        {
            var go = new GameObject("DamagePercent_TMP", typeof(RectTransform));
            go.transform.SetParent(_root.transform, false);
            go.AddComponent<TextMeshProUGUI>();
            return go.AddComponent<DamagePercentText>();
        }
    }
}
