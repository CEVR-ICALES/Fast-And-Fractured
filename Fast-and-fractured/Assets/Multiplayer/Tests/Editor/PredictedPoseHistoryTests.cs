using NUnit.Framework;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Tests
{
    public class PredictedPoseHistoryTests
    {
        [Test]
        public void ServerStateIsComparedWithThePredictionForItsOwnTick()
        {
            var history = new PredictedPoseHistory(8);
            for (uint tick = 1; tick <= 5; tick++)
                history.Record(tick, new Vector3(tick, 0f, 0f), Quaternion.identity, Vector3.right, Vector3.zero);

            Assert.IsTrue(history.TryGet(3, out var sample));
            Assert.AreEqual(3f, sample.Position.x);
        }

        [Test]
        public void OverwrittenTickIsNotFound()
        {
            var history = new PredictedPoseHistory(4);
            for (uint tick = 1; tick <= 9; tick++)
                history.Record(tick, Vector3.zero, Quaternion.identity, Vector3.zero, Vector3.zero);

            Assert.IsFalse(history.TryGet(2, out _));
            Assert.IsTrue(history.TryGet(9, out _));
            Assert.IsFalse(history.TryGet(0, out _));
        }

        [Test]
        public void CorrectionIsNotAppliedTwice()
        {
            var history = new PredictedPoseHistory(8);
            for (uint tick = 1; tick <= 5; tick++)
                history.Record(tick, new Vector3(tick, 0f, 0f), Quaternion.identity, Vector3.zero, Vector3.zero);

            history.ShiftAfter(3, new Vector3(0f, 0f, 2f), Quaternion.identity, Vector3.one, Vector3.zero);

            history.TryGet(3, out var corrected);
            history.TryGet(4, out var later);
            Assert.AreEqual(0f, corrected.Position.z, "the tick that was corrected keeps its prediction");
            Assert.AreEqual(2f, later.Position.z, "later predictions already include the correction");
            Assert.AreEqual(Vector3.one, later.Velocity);
        }
    }
}
