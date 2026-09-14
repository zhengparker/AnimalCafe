using System.Collections;
using System.Reflection;
using AnimalCafe.UI.P8R;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AnimalCafe.Tests.PlayMode
{
    public sealed class P8RPickUpFloatPlayModeTests
    {
        private GameObject root, cameraObject;
        private Transform visual, footprint;
        private P8RPickUpSignBillboard sign;
        private float originalTimeScale;

        [SetUp]
        public void SetUp()
        {
            originalTimeScale = Time.timeScale;
            root = new GameObject("FloatTestSupport");
            root.transform.SetPositionAndRotation(new Vector3(3, .72f, 4), Quaternion.Euler(0, 90, 0));
            footprint = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            footprint.SetParent(root.transform, false);
            footprint.localScale = new Vector3(.6f, .01f, .6f);
            visual = GameObject.CreatePrimitive(PrimitiveType.Quad).transform;
            visual.SetParent(root.transform, false);
            visual.localScale = new Vector3(.56f, .56f, 1);
            cameraObject = new GameObject("FloatTestCamera");
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<UnityEngine.Camera>().transform.rotation = Quaternion.Euler(38, 45, 0);
            sign = visual.gameObject.AddComponent<P8RPickUpSignBillboard>();
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = originalTimeScale;
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(cameraObject);
        }

        // Catches missing lift/bob or using scaled Time in Decoration's paused state.
        // 防止暂停装修时动画停止，或动画错误地移动真实 Slot/Footprint。
        [UnityTest]
        public IEnumerator PickupFloat_LiftsAndMovesWhilePausedWithoutMovingSlotOrFootprint()
        {
            Time.timeScale = 0;
            var anchor = root.transform.position;
            var footprintPosition = footprint.position;
            var min = float.PositiveInfinity;
            var max = float.NegativeInfinity;
            for (var index = 0; index < 12; index++)
            {
                yield return new WaitForSecondsRealtime(.1f);
                var lift = visual.position.y - anchor.y;
                Assert.That(lift, Is.InRange(.0839f, .1401f));
                min = Mathf.Min(min, lift); max = Mathf.Max(max, lift);
                Assert.That(root.transform.position, Is.EqualTo(anchor));
                Assert.That(footprint.position, Is.EqualTo(footprintPosition));
                Assert.That(Quaternion.Angle(visual.rotation, cameraObject.transform.rotation), Is.LessThan(.01f));
            }
            Assert.That(max - min, Is.GreaterThan(.005f), "The raised sign must gently move, not stay static.");
        }

        [Test]
        public void PickupFloat_RefreshesFacingImmediatelyAfterParentIsPositionedOrRotated()
        {
            // Instantiate enables the visual before CreateIndicator sets the support pose.
            // 覆盖真实创建顺序：OnEnable 后才设置柜台旋转，不能等下一帧纠正。
            root.transform.rotation = Quaternion.Euler(0, 180, 0);
            sign.SetHoverOffset(new Vector3(0, .1f, 0));
            Assert.That(Quaternion.Angle(visual.rotation, cameraObject.transform.rotation), Is.LessThan(.01f));
            root.transform.rotation = Quaternion.Euler(0, 270, 0);
            var bounds = sign.GetPresentationBounds(visual.GetComponent<Renderer>());
            Assert.That(Quaternion.Angle(visual.rotation, cameraObject.transform.rotation), Is.LessThan(.01f));
            Assert.That(bounds.Contains(visual.GetComponent<Renderer>().bounds.center), Is.True);
        }

        // Stable layout bounds prevent the action bar from following every bob frame.
        [UnityTest]
        public IEnumerator PickupFloat_ReservesStableEnvelopeAndRetainsHoverAfterSupportMoves()
        {
            var setHover = typeof(P8RPickUpSignBillboard).GetMethod("SetHoverOffset");
            var envelope = typeof(P8RPickUpSignBillboard).GetMethod("GetPresentationBounds");
            Assert.That(setHover, Is.Not.Null, "Preview lift must remain separately owned.");
            Assert.That(envelope, Is.Not.Null, "Floating graphics need stable action-bar bounds.");
            var hover = new Vector3(.08f, .1f, -.06f);
            setHover.Invoke(sign, new object[] { hover });
            root.transform.position += new Vector3(1, .2f, -1);
            root.transform.rotation = Quaternion.Euler(0, 180, 0);
            yield return null;
            var renderer = visual.GetComponent<Renderer>();
            var baseline = (Bounds)envelope.Invoke(sign, new object[] { renderer });
            for (var index = 0; index < 9; index++)
            {
                yield return new WaitForSecondsRealtime(.1f);
                var now = (Bounds)envelope.Invoke(sign, new object[] { renderer });
                Assert.That(Vector3.Distance(now.center, baseline.center), Is.LessThan(.0002f));
                Assert.That(Vector3.Distance(now.size, baseline.size), Is.LessThan(.0002f));
                Assert.That(now.min.y, Is.LessThanOrEqualTo(renderer.bounds.min.y + .0001f));
                Assert.That(now.max.y, Is.GreaterThanOrEqualTo(renderer.bounds.max.y - .0001f));
                var relative = visual.position - root.transform.position - hover;
                Assert.That(relative.x, Is.EqualTo(0).Within(.0001f));
                Assert.That(relative.z, Is.EqualTo(0).Within(.0001f));
                Assert.That(relative.y, Is.InRange(.0839f, .1401f));
            }
            for (var index = 0; index < 3; index++)
            {
                visual.gameObject.SetActive(false); visual.gameObject.SetActive(true);
                yield return null;
                Assert.That((visual.position - root.transform.position - hover).y, Is.InRange(.0839f, .1401f),
                    "Re-enabling must not accumulate lift or lose the preview hover.");
            }
        }
    }
}
