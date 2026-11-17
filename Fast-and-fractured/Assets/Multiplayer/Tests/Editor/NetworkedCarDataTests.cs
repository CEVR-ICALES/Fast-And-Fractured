using NUnit.Framework;
using UnityEditor;

namespace FastAndFractured.Multiplayer.Tests
{
    /// <summary>
    /// The host's and the AIs' cars read the networked car data, a remote player's car the singleplayer one: any gap
    /// between them makes the same hit push harder or softer depending on who drives.
    /// </summary>
    public class NetworkedCarDataTests
    {
        private static readonly string[][] Pairs =
        {
            new[] { "Pepe", "NPepeCarData" },
            new[] { "Josefino", "NJosefinoCarDat" },
            new[] { "Carme", "NCarmeCarData" },
            new[] { "MariaAntonia", "NMariaAntoniaCarData" },
        };

        private static readonly string[] SharedValues =
            { "BaseForce", "PushShootFORCE", "Weight", "MaxSpeedAscend", "MaxSpeedDescend", "NormalShootAngle" };

        [Test]
        public void NetworkedCarData_MatchesSingleplayerGameplayValues()
        {
            foreach (var pair in Pairs)
            {
                var singleplayer = AssetDatabase.LoadAssetAtPath<CharacterData>(
                    $"Assets/FastAndFractured/Characters/{pair[0]}/ScriptableObjects/{pair[0]}CarData.asset");
                var networked = AssetDatabase.LoadAssetAtPath<CharacterData>($"Assets/Multiplayer/Vehicles/CarData/{pair[1]}.asset");
                Assert.IsNotNull(singleplayer, pair[0]);
                Assert.IsNotNull(networked, pair[1]);

                var sp = new SerializedObject(singleplayer);
                var mp = new SerializedObject(networked);
                foreach (string value in SharedValues)
                    Assert.AreEqual(sp.FindProperty(value).floatValue, mp.FindProperty(value).floatValue, $"{pair[1]}.{value}");
            }
        }
    }
}
