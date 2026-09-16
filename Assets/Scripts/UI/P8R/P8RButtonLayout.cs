using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AnimalCafe.UI.P8R
{
    /// <summary>Local presentation only. Bounds measured from the approved 256px PNGs at alpha >=16.
    /// 只布局当前P8R控件；使用固定素材可见墨迹边界，不在runtime读取texture。</summary>
    public static class P8RButtonLayout
    {
        private static Rect InkBounds(Sprite sprite)
        {
            if (IsTrimmedArtwork(sprite)) return new Rect(Vector2.zero, sprite.rect.size);
            var key = sprite != null ? sprite.name.Replace("_cocoa", "").Replace("_muted", "").Replace("_ivory", "") : "";
            return key switch
            {
                "catalogue" => new Rect(46, 59, 137, 131), "floor" => new Rect(45, 51, 138, 136),
                "furniture" => new Rect(62, 55, 137, 121), "pickup" => new Rect(54, 54, 170, 150),
                "wall" => new Rect(80, 42, 143, 123), "wall_decor" => new Rect(53, 76, 121, 131),
                "whole_room" => new Rect(52, 83, 138, 122), "single_grid" => new Rect(81, 88, 102, 104),
                "decorate" => new Rect(32, 61, 158, 161), "clock" => new Rect(32, 32, 192, 191),
                "pause" => new Rect(47, 32, 160, 192), "resume" => new Rect(48, 32, 162, 192),
                "fast_forward" => new Rect(32, 67, 193, 122), "lock" => new Rect(48, 31, 160, 194),
                "store" => new Rect(32, 34, 192, 188), "cancel" => new Rect(32, 34, 192, 189),
                "confirm" => new Rect(32, 54, 193, 149), "rotate" => new Rect(31, 34, 193, 189),
                "back" => new Rect(32, 71, 193, 114), "exit" => new Rect(32, 35, 192, 187),
                "status_info" => new Rect(32, 31, 192, 193), "status_warning" => new Rect(32, 40, 192, 176),
                "status_error" => new Rect(31, 32, 194, 192), "status_success" => new Rect(32, 54, 193, 149),
                _ => new Rect(32, 32, 192, 192)
            };
        }

        public static void TextButton(Button button, float fontSize = 14f, float faceHeight = 34f, float sidePadding = 8f)
        {
            if (button == null) return;
            var metrics = P8RMobileMetrics.For(button);
            var icon = button.transform.Find("Icon")?.GetComponent<Image>();
            var label = button.transform.Find("Label")?.GetComponent<TMP_Text>();
            if (label != null) label.fontSize = metrics.Units(fontSize);
            if (label == null) return;
            if (icon == null || !icon.gameObject.activeSelf || !label.gameObject.activeSelf)
            {
                ApplyAdaptiveTextFace(button, label.GetPreferredValues(label.text).x, metrics, faceHeight, sidePadding);
                return;
            }
            var rect = (RectTransform)button.transform;
            var preferred = label.GetPreferredValues(label.text);
            var textRect = label.rectTransform;
            textRect.anchorMin = textRect.anchorMax = textRect.pivot = Vector2.one * .5f;
            textRect.sizeDelta = new Vector2(preferred.x + 2f, Mathf.Max(preferred.y + 2f, rect.rect.height - 8f));
            textRect.anchoredPosition = Vector2.zero;
            label.alignment = TextAlignmentOptions.Center;
            label.ForceMeshUpdate(true, true);
            var textBounds = label.textBounds;
            // Hidden prefab contents may have no generated mesh. Never serialize TMP's sentinel bounds.
            // prefab尚未进入可见Canvas时，用真实字体的preferred尺寸；Show后再按mesh墨迹精确居中。
            // Bold glyph bearings can exceed preferred advance width; finite generated bounds are authoritative.
            // 粗体字形边缘可能超出 preferred advance；不要误退回中心为零的估算。
            if (!(textBounds.size.x > 0f && !float.IsInfinity(textBounds.size.x)
                && textBounds.size.y >= 0f && !float.IsInfinity(textBounds.size.y)
                && Mathf.Abs(textBounds.center.x) <= textRect.rect.width
                && Mathf.Abs(textBounds.center.y) <= textRect.rect.height))
                textBounds = new Bounds(Vector3.zero, new Vector3(preferred.x, preferred.y, 0));
            var gap = metrics.Units(4);
            var extent = metrics.Units(20);
            var ink = InkBounds(icon.sprite);
            var factor = extent / Mathf.Max(ink.width, ink.height);
            var groupWidth = ink.width * factor + gap + textBounds.size.x;
            PlaceIcon(icon, factor, new Vector2(-groupWidth * .5f + ink.width * factor * .5f, 0), ink);
            textRect.anchoredPosition = StablePoint(new Vector2(groupWidth * .5f - textBounds.size.x * .5f - textBounds.center.x, -textBounds.center.y));
            ApplyAdaptiveTextFace(button, groupWidth, metrics, faceHeight, sidePadding);
        }

        /// <summary>Floor footer only: reduce visible padding without reducing its 48-unit touch root.
        /// 仅地板工具使用更小文字与紧凑底板；其他按钮保留原尺寸。</summary>
        public static void SurfaceButton(Button button, bool iconOnly = false)
        {
            if (button == null) return;
            if (!iconOnly) { TextButton(button, 12f, 32f, 6f); return; }
            IconButton(button, 20f, 0);
            var metrics = P8RMobileMetrics.For(button);
            SetFaceSize(button, new Vector2(metrics.Units(44), metrics.Units(32)));
        }

        public static void StatusIcon(Image icon, float extent = 20f)
        {
            if (icon == null) return;
            var metrics = P8RMobileMetrics.For(icon);
            extent = metrics.Units(extent);
            var ink = InkBounds(icon.sprite);
            var rect = icon.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            PlaceIcon(icon, extent / Mathf.Max(ink.width, ink.height), new Vector2(metrics.Units(10) + extent * .5f, -metrics.Units(10) - extent * .5f), ink);
        }

        public static void StackedButton(Button button, float iconExtent = 40f)
        {
            if (button == null) return;
            var icon = button.transform.Find("Icon")?.GetComponent<Image>();
            var label = button.transform.Find("Label")?.GetComponent<TMP_Text>();
            if (icon == null || label == null) return;
            var rect = (RectTransform)button.transform;
            var metrics = P8RMobileMetrics.For(button);
            if (IsColoredTab(icon.sprite))
            {
                // Only the four colored catalogue tabs are icon-only; keep their existing hit areas.
                // 彩色分类图标放大并居中；Floor 等比补偿，窄屏仍保留每侧 8 unit 留白。
                label.gameObject.SetActive(false);
                var tabInk = InkBounds(icon.sprite);
                // The flat floor artwork has less visual mass; compensate without stretching it.
                // 地板图案较扁，等比补偿 15%，其余图标和点击范围不变。
                var extent = metrics.Units(icon.sprite.name == "tab_floor_color" ? 23 : 20);
                var factor = Mathf.Min(extent / Mathf.Max(tabInk.width, tabInk.height),
                    Mathf.Max(1f, rect.rect.width - 16f) / tabInk.width,
                    Mathf.Max(1f, rect.rect.height - 16f) / tabInk.height);
                PlaceIcon(icon, factor, Vector2.zero, tabInk);
                // Fill each equal-width tab cell even when its layout group resizes later.
                // 底板横向跟随等宽Tab占位，避免四个小方块被摊开；高度继续保持紧凑。
                var faceImage = EnsureFace(button);
                if (faceImage != null)
                {
                    var face = faceImage.rectTransform;
                    face.anchorMin = new Vector2(0, .5f);
                    face.anchorMax = new Vector2(1, .5f);
                    face.pivot = Vector2.one * .5f;
                    face.anchoredPosition = Vector2.zero;
                    face.sizeDelta = new Vector2(0, metrics.Units(34));
                }
                return;
            }
            var text = label.rectTransform;
            text.anchorMin = text.anchorMax = text.pivot = Vector2.one * .5f;
            text.sizeDelta = new Vector2(Mathf.Max(1, rect.rect.width - 12), rect.rect.height > 120 ? 64 : 40);
            text.anchoredPosition = new Vector2(0, -rect.rect.height * (rect.rect.height > 120 ? .15f : .25f));
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            var ink = InkBounds(icon.sprite);
            PlaceIcon(icon, Mathf.Min(iconExtent, rect.rect.height <= 80 ? 32 : iconExtent) / Mathf.Max(ink.width, ink.height), new Vector2(0, rect.rect.height * (rect.rect.height > 120 ? .27f : .2f)), ink);
        }

        public static void TextOnly(Button button)
        {
            var label = button.transform.Find("Label")?.GetComponent<TMP_Text>();
            if (label == null) return;
            var metrics = P8RMobileMetrics.For(button);
            label.fontSize = metrics.Units(14);
            var rect = label.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(12, 4); rect.offsetMax = new Vector2(-12, -4);
            label.alignment = TextAlignmentOptions.Center;
            ApplyAdaptiveTextFace(button, label.GetPreferredValues(label.text).x, metrics);
        }

        public static void Modal(RectTransform card, TMP_Text title, TMP_Text body, Button first, Button second)
        {
            if (card == null || title == null || body == null || first == null || second == null) return;
            var metrics = P8RMobileMetrics.For(card);
            var parent = card.parent as RectTransform;
            var width = Mathf.Min(metrics.Units(480), (parent != null ? parent.rect.width : metrics.Units(360)) - metrics.Units(32));
            var padding = metrics.Units(12); var gap = metrics.Units(8); var targetHeight = metrics.Units(48);
            var contentWidth = width - padding * 2;
            var buttons = new[] { first, second }; var neededWidth = gap;
            foreach (var button in buttons)
            {
                var label = button.transform.Find("Label")?.GetComponent<TMP_Text>();
                if (label == null) continue;
                label.fontSize = metrics.Units(14);
                var iconVisible = button.transform.Find("Icon")?.gameObject.activeSelf ?? false;
                neededWidth += label.GetPreferredValues(label.text).x + metrics.Units(iconVisible ? 46 : 20);
            }
            var stacked = neededWidth > contentWidth;
            var buttonsHeight = stacked ? targetHeight * 2 + gap : targetHeight;
            title.fontSize = metrics.Units(16); body.fontSize = metrics.Units(14);
            title.textWrappingMode = body.textWrappingMode = TextWrappingModes.Normal;
            var titleHeight = title.GetPreferredValues(title.text, contentWidth, Mathf.Infinity).y + metrics.Units(2);
            var bodyHeight = body.GetPreferredValues(body.text, contentWidth, Mathf.Infinity).y + metrics.Units(2);
            var height = padding * 2 + titleHeight + bodyHeight + gap * 2 + buttonsHeight;
            card.localScale = Vector3.one;
            card.anchorMin = card.anchorMax = card.pivot = Vector2.one * .5f;
            card.anchoredPosition = Vector2.zero; card.sizeDelta = new Vector2(width, height);
            foreach (var label in new[] { title, body })
            {
                var rect = label.rectTransform;
                rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1);
                var top = label == title ? padding : padding + titleHeight + gap;
                var textHeight = label == title ? titleHeight : bodyHeight;
                rect.offsetMin = new Vector2(padding, -top - textHeight);
                rect.offsetMax = new Vector2(-padding, -top);
            }
            var buttonWidth = stacked ? contentWidth : (contentWidth - gap) * .5f;
            for (var i = 0; i < buttons.Length; i++)
            {
                var rect = (RectTransform)buttons[i].transform;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0); rect.pivot = Vector2.one * .5f;
                rect.sizeDelta = new Vector2(buttonWidth, targetHeight);
                rect.anchoredPosition = new Vector2(stacked ? 0 : (i == 0 ? -1 : 1) * (buttonWidth + gap) * .5f,
                    padding + targetHeight * .5f + (stacked && i == 0 ? targetHeight + gap : 0));
                if (buttons[i].transform.Find("Icon")?.gameObject.activeSelf ?? false) TextButton(buttons[i]);
                else TextOnly(buttons[i]);
            }
        }

        /// <summary>Centre the visible ink, keeping a smaller face inside its touch target.
        /// 图标按实际墨迹居中；可见底板与触控范围分开。</summary>
        public static void IconButton(Button button, float iconExtent = 20f, float faceExtent = 34f)
        {
            if (button == null) return;
            var metrics = P8RMobileMetrics.For(button);
            iconExtent = metrics.Units(iconExtent);
            faceExtent = metrics.Units(faceExtent);
            var icon = button.transform.Find("Icon")?.GetComponent<Image>();
            if (icon != null)
            {
                var ink = InkBounds(icon.sprite);
                PlaceIcon(icon, iconExtent / Mathf.Max(ink.width, ink.height), Vector2.zero, ink);
            }
            if (faceExtent > 0) SetFaceSize(button, Vector2.one * faceExtent);
        }

        /// <summary>Keep presentation on a child Image while the root Graphic owns the full touch target.
        /// 可见底板和点击根节点分离；复用节点，不改 Button 的 public contract。</summary>
        private static Image EnsureFace(Button button)
        {
            if (button == null || button.image == null) return null;
            if (button.image.transform != button.transform)
            {
                var rootHit = button.GetComponent<Image>();
                if (rootHit != null && rootHit != button.image)
                {
                    rootHit.color = Color.clear;
                    rootHit.canvasRenderer.SetAlpha(0f);
                    rootHit.raycastTarget = true;
                }
                button.image.raycastTarget = false;
                return button.image;
            }

            var hitImage = button.image;
            var faceTransform = button.transform.Find("P8RFace");
            Image face;
            if (faceTransform == null)
            {
                var faceObject = new GameObject("P8RFace", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                faceObject.transform.SetParent(button.transform, false);
                faceObject.transform.SetAsFirstSibling();
                face = faceObject.GetComponent<Image>();
            }
            else
            {
                face = faceTransform.GetComponent<Image>() ?? faceTransform.gameObject.AddComponent<Image>();
            }

            face.sprite = hitImage.sprite;
            face.overrideSprite = hitImage.overrideSprite;
            face.type = hitImage.type;
            face.preserveAspect = hitImage.preserveAspect;
            face.fillCenter = hitImage.fillCenter;
            face.fillMethod = hitImage.fillMethod;
            face.fillAmount = hitImage.fillAmount;
            face.fillClockwise = hitImage.fillClockwise;
            face.fillOrigin = hitImage.fillOrigin;
            face.pixelsPerUnitMultiplier = hitImage.pixelsPerUnitMultiplier;
            face.material = hitImage.material;
            face.color = hitImage.color;
            face.maskable = hitImage.maskable;
            face.raycastTarget = false;

            hitImage.color = Color.clear;
            hitImage.canvasRenderer.SetAlpha(0f);
            hitImage.raycastTarget = true;
            button.targetGraphic = face;
            return face;
        }

        private static void SetFaceSize(Button button, Vector2 size)
        {
            var faceImage = EnsureFace(button);
            if (faceImage == null) return;
            var face = faceImage.rectTransform;
            face.anchorMin = face.anchorMax = face.pivot = Vector2.one * .5f;
            face.anchoredPosition = Vector2.zero;
            face.sizeDelta = size;
        }

        private static void ApplyAdaptiveTextFace(Button button, float contentWidth, P8RMobileMetrics metrics,
            float faceHeight = 34f, float sidePadding = 8f)
        {
            if (button == null || button.transform is not RectTransform root) return;
            var desiredWidth = Mathf.Max(metrics.Units(34), contentWidth + metrics.Units(sidePadding * 2));
            var rootWidth = Mathf.Max(root.rect.width, root.sizeDelta.x);
            var width = rootWidth > 0 ? Mathf.Min(rootWidth, desiredWidth) : desiredWidth;
            SetFaceSize(button, new Vector2(width, metrics.Units(faceHeight)));
        }

        private static void PlaceIcon(Image icon, float factor, Vector2 inkCenter, Rect ink)
        {
            var rect = icon.rectTransform;
            if (!icon.name.StartsWith("P8RStatus")) rect.anchorMin = rect.anchorMax = Vector2.one * .5f;
            rect.pivot = Vector2.one * .5f;
            // Colored artwork is already trimmed by its Sprite rect; mono art keeps its 256px canvas.
            // 彩色图用导入时的可见边界，原单色图的尺寸、偏移算法不变。
            var canvasSize = IsTrimmedArtwork(icon.sprite) ? icon.sprite.rect.size : Vector2.one * 256f;
            rect.sizeDelta = canvasSize * factor;
            rect.anchoredPosition = StablePoint(inkCenter - (ink.center - canvasSize * .5f) * factor);
            icon.raycastTarget = false;
        }

        private static bool IsColoredTab(Sprite sprite) => sprite != null &&
            (sprite.name == "tab_furniture_color" || sprite.name == "tab_floor_color"
                || sprite.name == "tab_wall_color" || sprite.name == "tab_wall_decor_color");

        private static bool IsColoredAction(Sprite sprite) => sprite != null &&
            (sprite.name == "action_decorate_color" || sprite.name == "action_exit_color"
                || sprite.name == "action_pickup_color");

        private static bool IsTrimmedArtwork(Sprite sprite) => IsColoredTab(sprite) || IsColoredAction(sprite)
            || (sprite != null && (sprite.name == "range_whole_room_color" || sprite.name == "range_single_grid_color"));

        // Binary subpixel coordinates survive RectTransform's parent-relative float round-trip.
        // 只规范本helper计算的位置（最多0.0078 logical unit），不修改native driven布局或点击区域。
        private static Vector2 StablePoint(Vector2 value) => new Vector2(Mathf.Round(value.x * 64f), Mathf.Round(value.y * 64f)) / 64f;

        public static void ActionFace(Button button, bool floating, float horizontalOffset = 0f)
        {
            if (button == null || button.image == null) return;
            var faceImage = EnsureFace(button);
            if (faceImage == null) return;
            var face = faceImage.rectTransform;
            if (floating)
            {
                var metrics = P8RMobileMetrics.For(button);
                SetFaceSize(button, Vector2.one * metrics.Units(30));
                // Offset visible ink and face together; the button's touch root never moves.
                // 外观和墨迹一起移动，触控根节点保持不变；每次赋值避免累积偏移。
                face.anchoredPosition = StablePoint(new Vector2(horizontalOffset, 0));
                var icon = button.transform.Find("Icon")?.GetComponent<Image>();
                if (icon != null)
                {
                    var ink = InkBounds(icon.sprite);
                    PlaceIcon(icon, metrics.Units(18) / Mathf.Max(ink.width, ink.height), face.anchoredPosition, ink);
                }
            }
            else
            {
                face.anchorMin = Vector2.zero;
                face.anchorMax = Vector2.one;
                face.offsetMin = face.offsetMax = Vector2.zero;
            }
        }
    }
}
