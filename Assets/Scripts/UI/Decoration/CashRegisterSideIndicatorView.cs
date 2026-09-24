using System;
using AnimalCafe.Content;
using AnimalCafe.Decoration;
using AnimalCafe.Layout;
using AnimalCafe.UI.P8R;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AnimalCafe.UI.Decoration
{
    /// <summary>Preview-only role arrows on the ground, with upright icons above them.
    /// 锚定真实相邻站位；受阻只叠加禁止标志，不改变 layout、Save 或 pointer ownership。</summary>
    public sealed class CashRegisterSideIndicatorView : MonoBehaviour
    {
        private sealed class Marker
        {
            public RectTransform Icon;
            public GameObject InvalidOverlay;
            public MeshRenderer Arrow;
            public Material Material;
        }

        private RectTransform root, actionPanel;
        private CanvasGroup group;
        private UnityEngine.Camera worldCamera, uiCamera;
        private Func<Rect> safeArea;
        private Func<Rect?> uiObstacle;
        private Transform ghost, floor;
        private Renderer[] ghostRenderers;
        private ResolvedStationAnchors anchors = ResolvedStationAnchors.Empty;
        private DecorationGridSpace gridSpace;
        private CashRegisterSides sides;
        private Marker employee, customer;
        private GameObject worldRoot;
        private Mesh arrowMesh;
        private float cellSize = 1;
        private bool covered;
        private Button[] actionButtons;
        private readonly Vector3[] corners = new Vector3[4];
        private Rect presentationBounds, presentationArea;
        private bool hasPresentationBounds;

        // Reserve the complete bob envelope, not this frame's animated icon position.
        // 操作栏只跟随稳定布局变化，不跟着 icon 的轻微浮动上下晃动。
        public event Action PresentationChanged;
        public bool TryGetPresentationBounds(out Rect bounds)
        {
            bounds = presentationBounds;
            return hasPresentationBounds;
        }

        public void Configure(P8RAppearance appearance, UnityEngine.Camera camera,
            Func<Rect> safeAreaProvider, RectTransform actions, Material footprintMaterial,
            Transform ground, DecorationGridSpace space, Func<Rect?> obstacleProvider = null)
        {
            worldCamera = camera;
            safeArea = safeAreaProvider;
            uiObstacle = obstacleProvider;
            actionPanel = actions;
            if (actions != null) actionButtons = actions.GetComponentsInChildren<Button>(true);
            floor = ground;
            gridSpace = space;
            cellSize = space.Settings.CellSize;
            if (root != null) return;
            root = (RectTransform)transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            group = gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = group.interactable = false;
            group.alpha = 0;
            var canvas = GetComponentInParent<Canvas>().rootCanvas;
            uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            // World geometry must not inherit the Overlay Canvas's pixel scale.
            // 地面 mesh 独立于 Canvas，避免随 UI 缩放漂移；由本 View 负责回收。
            worldRoot = new GameObject("CashRegisterRoleGround");
            SceneManager.MoveGameObjectToScene(worldRoot, gameObject.scene);
            worldRoot.SetActive(false);
            arrowMesh = CreateArrowMesh();
            employee = CreateMarker("Employee", "employee-apron", footprintMaterial, new Color(.65f, .73f, .48f));
            customer = CreateMarker("Customer", "customer-bag", footprintMaterial, new Color(.96f, .65f, .38f));
        }

        public void Show(GameObject preview, ResolvedStationAnchors resolvedAnchors,
            bool employeeInvalid, bool customerInvalid)
        {
            if (preview == null) { Hide(); return; }
            if (ghost != preview.transform)
            {
                // The ghost already includes Counter and equipment rotation; never rotate twice.
                sides = CashRegisterSideMarker.ReadSidesFrom(preview);
                ghost = preview.transform;
                ghostRenderers = preview.GetComponentsInChildren<Renderer>();
            }
            anchors = resolvedAnchors ?? ResolvedStationAnchors.Empty;
            employee.InvalidOverlay.SetActive(employeeInvalid);
            customer.InvalidOverlay.SetActive(customerInvalid);
            RefreshGeometry();
        }

        public void Hide()
        {
            // Scene unload may destroy icon children before the controller clears this view.
            // 场景卸载时图标可能先被销毁；Unity-null 检查使清理可重复调用。
            if (this == null) return;
            ghost = null;
            ghostRenderers = null;
            anchors = ResolvedStationAnchors.Empty;
            if (employee?.InvalidOverlay != null) employee.InvalidOverlay.SetActive(false);
            if (customer?.InvalidOverlay != null) customer.InvalidOverlay.SetActive(false);
            ClearPresentation();
        }

        public void SetCovered(bool value)
        {
            covered = value;
            if (value) ClearPresentation();
        }

        private void LateUpdate() => RefreshGeometry();
        private void OnDisable() => ClearPresentation();
        private void OnDestroy()
        {
            if (worldRoot != null) Destroy(worldRoot);
            if (arrowMesh != null) Destroy(arrowMesh);
            if (employee?.Material != null) Destroy(employee.Material);
            if (customer?.Material != null) Destroy(customer.Material);
        }

        private void SetVisible(bool visible)
        {
            if (group != null) group.alpha = visible ? 1 : 0;
            if (worldRoot != null && worldRoot.activeSelf != visible) worldRoot.SetActive(visible);
        }

        private void ClearPresentation()
        {
            hasPresentationBounds = false;
            SetVisible(false);
        }

        private void RefreshGeometry()
        {
            if (group == null) return;
            if (!isActiveAndEnabled || covered || ghost == null || !ghost.gameObject.activeInHierarchy
                || floor == null || worldCamera == null || employee.Material == null || customer.Material == null
                || !TryBounds(ghostRenderers, out var device))
            { ClearPresentation(); return; }
            var center = device.center;
            if (worldCamera.WorldToScreenPoint(center).z <= 0) { ClearPresentation(); return; }
            var metrics = P8RMobileMetrics.For(this);
            var pixels = metrics.PixelsPerLogicalUnit;
            var area = Inset(safeArea != null ? safeArea() : worldCamera.pixelRect, 3 * pixels);
            var first = PlaceArrow(employee, InteractionRole.Employee, sides.EmployeeSide, center);
            var second = PlaceArrow(customer, InteractionRole.Customer, sides.CustomerSide, center);
            if (first.z <= 0 || second.z <= 0) { ClearPresentation(); return; }

            var anchor = employee.Arrow.transform.position;
            var worldHeight = Vector3.Distance(first,
                worldCamera.WorldToScreenPoint(anchor + worldCamera.transform.up * (.5f * cellSize)));
            // Reserve the prohibition ring even while valid, so a state change does not jump.
            // 始终预留红圈外沿；valid/invalid 切换不改变浮动高度或操作栏位置。
            var iconPixels = Mathf.Min(30 * pixels, worldHeight) * 1.2f;
            var amplitude = 1.2f * pixels;
            var bob = Mathf.Sin(Time.unscaledTime * (2 * Mathf.PI / 2.8f)) * amplitude;
            var arrowTop = Mathf.Max(Project(employee.Arrow.bounds).yMax - first.y,
                Project(customer.Arrow.bounds).yMax - second.y);
            var minimumHover = arrowTop + 4 * pixels + iconPixels * .5f;
            // One shared offset keeps both roles at the same height above their own arrows.
            // 共享相对高度与浮动，不把有前后关系的箭头强行拉到屏幕同一水平线。
            var minimumOffset = Mathf.Max(minimumHover + amplitude,
                area.yMin + iconPixels * .5f + pixels + amplitude - Mathf.Min(first.y, second.y));
            var maximumOffset = area.yMax - iconPixels * .5f - pixels - amplitude - Mathf.Max(first.y, second.y);
            // Only a role actually below the local checklist needs its upper limit reduced.
            // 清单左侧保留完整高度；两角色仍共享相对于地面箭头的高度。
            var obstacle = uiObstacle?.Invoke();
            if (obstacle.HasValue)
            {
                var card = Inset(obstacle.Value, -4 * pixels);
                foreach (var point in new[] { first, second })
                    if (point.x + iconPixels * .5f > card.xMin && point.x - iconPixels * .5f < card.xMax)
                        maximumOffset = Mathf.Min(maximumOffset,
                            card.yMin - iconPixels * .5f - amplitude - point.y);
            }
            if (maximumOffset < minimumOffset) { ClearPresentation(); return; }
            // Match the confirmed Pickup sign's visible center at the authored camera angle.
            // 对齐常驻取餐牌的视觉高度（台面 + lift + billboard 中心），不包含拖动额外抬高。
            var naturalHeight = worldCamera.WorldToScreenPoint(anchor + floor.up * (1.4f * cellSize)).y - first.y;
            var offset = Mathf.Clamp(naturalHeight, minimumOffset, maximumOffset);
            var a = IconRect(first, offset, iconPixels);
            var b = IconRect(second, offset, iconPixels);
            var envelope = Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin) - amplitude,
                Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax) + amplitude);

            // Layout the action row first; it already keeps a pressed target under the pointer.
            // 先让整条操作栏避让。回退位移不计入此区域，防止双方反复互相推开。
            var changed = !hasPresentationBounds || (envelope.min - presentationBounds.min).sqrMagnitude > .0625f
                || (envelope.max - presentationBounds.max).sqrMagnitude > .0625f || area != presentationArea;
            presentationBounds = envelope;
            presentationArea = area;
            hasPresentationBounds = true;
            if (changed) PresentationChanged?.Invoke();

            var actions = ActionFaceRect();
            var paddedActions = Inset(actions, -3 * pixels);
            if (actions.width > 0 && (BobEnvelope(a, amplitude).Overlaps(paddedActions)
                || BobEnvelope(b, amplitude).Overlaps(paddedActions)))
            {
                // Crowded-screen fallback: move the pair together, never just one role.
                var above = paddedActions.yMax + pixels + amplitude - Mathf.Min(a.yMin, b.yMin);
                var below = paddedActions.yMin - pixels - amplitude - Mathf.Max(a.yMax, b.yMax);
                var canAbove = offset + above <= maximumOffset && offset + above >= minimumOffset;
                var canBelow = offset + below >= minimumOffset && offset + below <= maximumOffset;
                if (!canAbove && !canBelow) { SetVisible(false); return; }
                var shift = canAbove && (!canBelow || Mathf.Abs(above) <= Mathf.Abs(below)) ? above : below;
                a.y += shift;
                b.y += shift;
            }
            a.y += bob;
            b.y += bob;
            LayoutIcon(employee, a, metrics);
            LayoutIcon(customer, b, metrics);
            SetVisible(area.Contains(a.min) && area.Contains(a.max) && area.Contains(b.min) && area.Contains(b.max)
                && !a.Overlaps(b));
        }

        private static Rect IconRect(Vector3 point, float offset, float size) =>
            new Rect(new Vector2(point.x - size * .5f, point.y + offset - size * .5f), Vector2.one * size);

        private static Rect BobEnvelope(Rect rect, float amplitude) =>
            Rect.MinMaxRect(rect.xMin, rect.yMin - amplitude, rect.xMax, rect.yMax + amplitude);

        private void LayoutIcon(Marker marker, Rect rect, P8RMobileMetrics metrics)
        {
            marker.Icon.sizeDelta = Vector2.one * metrics.Units(rect.height / metrics.PixelsPerLogicalUnit / 1.2f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, rect.center, uiCamera, out var position);
            marker.Icon.anchoredPosition = position;
        }

        private Vector3 PlaceArrow(Marker marker, InteractionRole role, CardinalDirection side, Vector3 center)
        {
            if (anchors.TryGetAnchor(role, out var resolved))
            {
                // Domain position already includes both support and equipment rotation.
                // 真实站位即使被占用也不移位；图示朝向 CR，不寻找空闲位置。
                var position = floor.TransformPoint(gridSpace.GetCellCenterLocal(resolved.Position,
                    GridHighlightView.FootprintHeight));
                var facing = floor.TransformDirection(Direction(resolved.Facing));
                marker.Arrow.transform.SetPositionAndRotation(position, Quaternion.LookRotation(facing, floor.up));
                marker.Arrow.transform.localScale = new Vector3(.38f, 1, .44f) * cellSize;
                return worldCamera.WorldToScreenPoint(position);
            }
            // No valid surface slot: keep the existing ghost-side fallback, marked invalid.
            // 没有可解析支撑时只说明当前朝向，不伪造有效站位。
            var local = Direction(side);
            var normal = floor.up;
            var direction = Vector3.ProjectOnPlane(ghost.TransformDirection(local), normal).normalized;
            var anchor = center + direction * cellSize;
            var plane = new Plane(normal, floor.position);
            anchor = plane.ClosestPointOnPlane(anchor) + normal * GridHighlightView.FootprintHeight;
            marker.Arrow.transform.SetPositionAndRotation(anchor, Quaternion.LookRotation(-direction, normal));
            marker.Arrow.transform.localScale = new Vector3(.38f, 1, .44f) * cellSize;
            return worldCamera.WorldToScreenPoint(anchor);
        }

        private static Vector3 Direction(CardinalDirection side) => side == CardinalDirection.North ? Vector3.forward
            : side == CardinalDirection.East ? Vector3.right
            : side == CardinalDirection.South ? Vector3.back : Vector3.left;

        private Marker CreateMarker(string role, string iconName, Material source, Color tint)
        {
            var rect = new GameObject(role + "Badge", typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(root, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            var image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            image.sprite = Resources.Load<Sprite>("UI/P8R/RoleIcons/" + iconName);
            image.preserveAspect = true;
            var invalid = new GameObject("InvalidOverlay", typeof(RectTransform), typeof(P8RInvalidRoleGraphic));
            var invalidRect = (RectTransform)invalid.transform;
            invalidRect.SetParent(rect, false);
            invalidRect.anchorMin = new Vector2(-.1f, -.1f);
            invalidRect.anchorMax = new Vector2(1.1f, 1.1f);
            invalidRect.offsetMin = invalidRect.offsetMax = Vector2.zero;
            invalid.GetComponent<P8RInvalidRoleGraphic>().raycastTarget = false;
            invalid.GetComponent<P8RInvalidRoleGraphic>().color = new Color32(185, 86, 64, 255);
            invalid.SetActive(false);
            var arrow = new GameObject(role + "RoleArrow", typeof(MeshFilter), typeof(MeshRenderer));
            arrow.transform.SetParent(worldRoot.transform, false);
            arrow.GetComponent<MeshFilter>().sharedMesh = arrowMesh;
            var renderer = arrow.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Material material = null;
            if (source != null && source.HasProperty("_FootprintOpacity"))
            {
                // Clone only: keep existing validity footprints and their shared Material unchanged.
                // 角色色不表示 valid/invalid；不修改共用 footprint 材质。
                material = new Material(source) { name = role + "RoleFootprint" };
                material.SetColor("_BaseColor", tint);
                material.SetFloat("_TintBrightness", 1);
                material.SetFloat("_TintSaturation", 1);
                material.SetFloat("_LightIntensity", 1.3f);
                material.SetFloat("_FootprintOpacity", .65f);
                material.SetFloat("_EdgeSoftness", .45f);
                renderer.sharedMaterial = material;
            }
            return new Marker { Icon = rect, InvalidOverlay = invalid, Arrow = renderer, Material = material };
        }

        private static Mesh CreateArrowMesh()
        {
            // Concave seven-point arrow: filled center + inward feathered rim.
            // 用现有 footprint shader 的 UV 柔边，细箭头不依赖低分辨率 PNG。
            var outline = new[] { new Vector2(0, .5f), new Vector2(-.5f, .02f),
                new Vector2(-.22f, .02f), new Vector2(-.22f, -.5f),
                new Vector2(.22f, -.5f), new Vector2(.22f, .02f), new Vector2(.5f, .02f) };
            var vertices = new Vector3[14];
            var uv = new Vector2[14];
            for (var i = 0; i < 7; i++)
            {
                vertices[i] = new Vector3(outline[i].x, 0, outline[i].y) * .92f;
                vertices[i + 7] = new Vector3(outline[i].x, 0, outline[i].y);
                uv[i] = new Vector2(.5f, .5f);
                uv[i + 7] = new Vector2(0, .5f);
            }
            var triangles = new int[51];
            var fill = new[] { 0, 1, 6, 2, 3, 4, 2, 4, 5 };
            Array.Copy(fill, triangles, fill.Length);
            for (var i = 0; i < 7; i++)
            {
                var next = (i + 1) % 7;
                var index = 9 + i * 6;
                triangles[index] = i; triangles[index + 1] = i + 7; triangles[index + 2] = next + 7;
                triangles[index + 3] = i; triangles[index + 4] = next + 7; triangles[index + 5] = next;
            }
            var mesh = new Mesh { name = "CashRoleArrowSoftMesh", vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static bool TryBounds(Renderer[] renderers, out Bounds result)
        {
            result = default;
            var found = false;
            if (renderers == null) return false;
            foreach (var renderer in renderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (!found) result = renderer.bounds;
                else result.Encapsulate(renderer.bounds);
                found = true;
            }
            return found;
        }

        private Rect Project(Bounds bounds)
        {
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (var i = 0; i < 8; i++)
            {
                var point = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var screen = (Vector2)worldCamera.WorldToScreenPoint(point);
                min = Vector2.Min(min, screen);
                max = Vector2.Max(max, screen);
            }
            return UnityEngine.Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private Rect ScreenRect(RectTransform rect)
        {
            rect.GetWorldCorners(corners);
            var min = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]);
            var max = min;
            for (var i = 1; i < 4; i++)
            {
                var point = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[i]);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            return UnityEngine.Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private Rect ActionFaceRect()
        {
            if (actionPanel == null || !actionPanel.gameObject.activeInHierarchy) return default;
            var result = default(Rect);
            var found = false;
            if (actionButtons != null)
                foreach (var button in actionButtons)
                {
                    if (button == null || !button.gameObject.activeInHierarchy || button.image == null) continue;
                    var face = button.image.rectTransform;
                    var rect = ScreenRect(face);
                    result = !found ? rect : UnityEngine.Rect.MinMaxRect(Mathf.Min(result.xMin, rect.xMin),
                        Mathf.Min(result.yMin, rect.yMin), Mathf.Max(result.xMax, rect.xMax), Mathf.Max(result.yMax, rect.yMax));
                    found = true;
                }
            // Invisible hit padding stays fully clickable; it need not push decorative art away.
            // 避让实际按钮底板，不把透明点击留白当成视觉障碍。
            return found ? result : ScreenRect(actionPanel);
        }

        private static Rect Inset(Rect rect, float padding) => UnityEngine.Rect.MinMaxRect(
            rect.xMin + padding, rect.yMin + padding, rect.xMax - padding, rect.yMax - padding);
    }
}
