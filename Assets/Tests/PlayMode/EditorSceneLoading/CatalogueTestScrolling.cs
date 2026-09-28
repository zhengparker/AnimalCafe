#if UNITY_EDITOR
using AnimalCafe.UI.Decoration;
using UnityEngine;
using UnityEngine.UI;

namespace AnimalCafe.Tests.PlayMode
{
    internal static class CatalogueTestScrolling
    {
        // Reveal the requested item before sending real pointer input.
        // 目录分组后先滚动到目标，后续仍验证真实 raycast 和点击。
        internal static void Reveal(Button button)
        {
            if (button.GetComponentInParent<DecorationCatalogueView>() == null) return;
            Canvas.ForceUpdateCanvases();
            var target = (RectTransform)button.transform;
            foreach (var scroll in button.GetComponentsInParent<ScrollRect>())
            {
                if (scroll.content == null || scroll.viewport == null) continue;
                scroll.StopMovement();
                var center = (Vector2)scroll.viewport.InverseTransformPoint(target.TransformPoint(target.rect.center));
                var delta = scroll.viewport.rect.center - center;
                scroll.content.anchoredPosition += new Vector2(scroll.horizontal ? delta.x : 0f,
                    scroll.vertical ? delta.y : 0f);
                Canvas.ForceUpdateCanvases();
            }
        }
    }
}
#endif
