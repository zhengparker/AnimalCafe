using System.Collections;
using AnimalCafe.UI.Feedback;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AnimalCafe.Tests.PlayMode
{
    public sealed class Phase5FeedbackPlayModeTests
    {
        [UnityTest]
        public IEnumerator IT018_ToastView_AdvancesMergedQueueWithUnscaledTimeWhilePaused()
        {
            var originalTimeScale = Time.timeScale;
            var root = CreateUiObject("ToastRoot");
            var background = root.AddComponent<Image>();
            var labelObject = CreateUiObject("ToastLabel", root.transform);
            var label = labelObject.AddComponent<TextMeshProUGUI>();
            var view = root.AddComponent<ToastView>();
            var queue = new ToastQueue(() => Time.unscaledTime);

            try
            {
                view.Configure(queue, label, new Graphic[] { background, label });
                queue.Enqueue(new ToastMessage(ToastType.Info, "Saved", ToastPriority.Normal, 0.05f));
                queue.Enqueue(new ToastMessage(ToastType.Info, "Saved", ToastPriority.Normal, 0.05f));
                queue.Enqueue(new ToastMessage(ToastType.Success, "Ready", ToastPriority.Normal, 0.2f));
                Time.timeScale = 0f;

                yield return null;

                Assert.That(view.IsVisible, Is.True);
                Assert.That(label.text, Does.Contain("Saved"));
                Assert.That(label.text, Does.Contain("2"));
                Assert.That(background.raycastTarget, Is.False);
                Assert.That(label.raycastTarget, Is.False);

                yield return new WaitForSecondsRealtime(0.08f);
                yield return null;

                Assert.That(view.IsVisible, Is.True);
                Assert.That(label.text, Does.Contain("Ready"));
            }
            finally
            {
                Time.timeScale = originalTimeScale;
                Object.DestroyImmediate(root);
            }
        }

        [UnityTest]
        public IEnumerator IT019_TooltipView_RequiresTapAndHasNoHoverDependency()
        {
            var root = CreateUiObject("TooltipInfoAction");
            var content = CreateUiObject("TooltipContent", root.transform);
            var label = content.AddComponent<TextMeshProUGUI>();
            var view = root.AddComponent<TooltipView>();

            try
            {
                view.Configure(label, content);
                view.SetMessage("Tap to learn about syrup slots");

                yield return null;
                yield return null;

                Assert.That(content.activeSelf, Is.False, "Tooltip must not depend on Hover.");
                Assert.That(view, Is.Not.InstanceOf<IPointerEnterHandler>());

                view.OnPointerClick(new PointerEventData(null));
                yield return null;

                Assert.That(content.activeSelf, Is.True);
                Assert.That(label.text, Is.EqualTo("Tap to learn about syrup slots"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [UnityTest]
        public IEnumerator IT020_ValidationMessage_PersistsSpecificReasonUntilInputIsCorrected()
        {
            var root = CreateUiObject("ValidationMessage");
            var label = root.AddComponent<TextMeshProUGUI>();
            var view = root.AddComponent<ValidationMessageView>();

            try
            {
                view.Configure(label);
                view.SetValidationResult(false, "Choose a coffee bean for this machine");

                yield return null;
                yield return null;

                Assert.That(view.IsVisible, Is.True);
                Assert.That(label.text, Is.EqualTo("Choose a coffee bean for this machine"));

                view.SetValidationResult(false, "Choose a coffee bean for this machine");
                yield return null;

                Assert.That(view.IsVisible, Is.True);
                Assert.That(label.text, Is.EqualTo("Choose a coffee bean for this machine"));

                view.SetValidationResult(true, null);
                yield return null;

                Assert.That(view.IsVisible, Is.False);
                Assert.That(label.text, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateUiObject(string name, Transform parent = null)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            if (parent != null)
            {
                gameObject.transform.SetParent(parent, false);
            }

            return gameObject;
        }
    }
}
