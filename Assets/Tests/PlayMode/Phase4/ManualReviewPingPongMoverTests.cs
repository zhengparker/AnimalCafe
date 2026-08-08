using System.Collections;
using System.Reflection;
using AnimalCafe.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AnimalCafe.Tests.PlayMode.Phase4
{
    public sealed class ManualReviewPingPongMoverTests
    {
        [Test]
        public void DefaultsExposeTheManualReviewPathAndSpeed()
        {
            var gameObject = new GameObject("ManualReviewPingPongMoverDefaultsTest");

            try
            {
                var mover = gameObject.AddComponent<ManualReviewPingPongMover>();

                Assert.That(mover.LocalPointA, Is.EqualTo(new Vector3(-2f, 0.5f, -1f)));
                Assert.That(mover.LocalPointB, Is.EqualTo(new Vector3(2f, 0.5f, -1f)));
                Assert.That(mover.UnitsPerSecond, Is.EqualTo(1f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [TestCase(-1f)]
        [TestCase(0f)]
        public void ConfigureClampsSpeedToTheMinimum(float configuredSpeed)
        {
            var gameObject = new GameObject("ManualReviewPingPongMoverSpeedTest");

            try
            {
                var mover = gameObject.AddComponent<ManualReviewPingPongMover>();
                mover.Configure(Vector3.zero, Vector3.right, configuredSpeed);

                Assert.That(mover.UnitsPerSecond, Is.EqualTo(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ConfigurationFieldsAreSerializedForScenePersistence()
        {
            Assert.That(GetField("localPointA").IsDefined(typeof(SerializeField), false), Is.True);
            Assert.That(GetField("localPointB").IsDefined(typeof(SerializeField), false), Is.True);
            Assert.That(GetField("unitsPerSecond").IsDefined(typeof(SerializeField), false), Is.True);
            Assert.That(GetField("unitsPerSecond").GetCustomAttribute<MinAttribute>(), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator MovementUsesScaledTimeForPauseNormalAndFast()
        {
            var originalTimeScale = Time.timeScale;
            var gameObject = new GameObject("ManualReviewPingPongMoverTest");

            try
            {
                var mover = gameObject.AddComponent<ManualReviewPingPongMover>();
                mover.Configure(Vector3.zero, Vector3.right * 10f, 1f);

                Time.timeScale = 0f;
                yield return new WaitForSecondsRealtime(0.15f);
                Assert.That(gameObject.transform.localPosition, Is.EqualTo(Vector3.zero));

                mover.ResetToStart();
                Time.timeScale = 1f;
                yield return new WaitForSecondsRealtime(0.2f);
                var normalDistance = gameObject.transform.localPosition.magnitude;

                mover.ResetToStart();
                Time.timeScale = 2f;
                yield return new WaitForSecondsRealtime(0.2f);
                var fastDistance = gameObject.transform.localPosition.magnitude;

                Assert.That(normalDistance, Is.GreaterThan(0.05f));
                Assert.That(fastDistance, Is.GreaterThan(normalDistance * 1.7f));
            }
            finally
            {
                Time.timeScale = originalTimeScale;
                Object.Destroy(gameObject);
            }
        }

        [UnityTest]
        public IEnumerator MovementReversesAfterReachingPointB()
        {
            var originalTimeScale = Time.timeScale;
            var gameObject = new GameObject("ManualReviewPingPongMoverReversalTest");

            try
            {
                var mover = gameObject.AddComponent<ManualReviewPingPongMover>();
                mover.Configure(Vector3.zero, Vector3.right, 1f);
                gameObject.transform.localPosition = Vector3.right;

                Time.timeScale = 1f;
                yield return null;
                yield return null;

                Assert.That(gameObject.transform.localPosition.x, Is.LessThan(1f));
            }
            finally
            {
                Time.timeScale = originalTimeScale;
                Object.Destroy(gameObject);
            }
        }

        private static FieldInfo GetField(string fieldName)
        {
            return typeof(ManualReviewPingPongMover).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
        }
    }
}
