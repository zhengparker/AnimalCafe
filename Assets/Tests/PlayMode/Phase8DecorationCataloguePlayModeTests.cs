using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AnimalCafe.Content;
using AnimalCafe.Decoration;
using AnimalCafe.Layout;
using AnimalCafe.UI.Decoration;
using AnimalCafe.UI.Foundation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AnimalCafe.Tests.PlayMode
{
    public sealed class Phase8DecorationCataloguePlayModeTests
    {
#if UNITY_EDITOR
        private const string CataloguePrefabPath =
            "Assets/UI/Phase8/Prefabs/PF_UI_Phase8DecorationCatalogue.prefab";
        private const string ActionBarPrefabPath =
            "Assets/UI/Phase8/Prefabs/PF_UI_Phase8DecorationActionBar.prefab";
#endif

        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (var index = owned.Count - 1; index >= 0; index--)
            {
                if (owned[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(owned[index]);
                }
            }

            owned.Clear();
        }

#if UNITY_EDITOR
        [Test]
        public void ProductionPrefabs_LoadWithRealHierarchyComponentsAndSerializedReferences()
        {
            var cataloguePrefab = LoadAsset<GameObject>(CataloguePrefabPath);
            var actionBarPrefab = LoadAsset<GameObject>(ActionBarPrefabPath);

            Assert.That(cataloguePrefab, Is.Not.Null);
            Assert.That(actionBarPrefab, Is.Not.Null);
            var catalogue = cataloguePrefab.GetComponent<DecorationCatalogueView>();
            var actionBar = actionBarPrefab.GetComponent<DecorationActionBarView>();
            Assert.That(catalogue, Is.Not.Null);
            Assert.That(actionBar, Is.Not.Null);

            var verticalScroll = Reference<ScrollRect>(catalogue, "verticalScroll");
            var categoryContent = Reference<RectTransform>(catalogue, "categoryContent");
            var rowTemplate = Reference<GameObject>(catalogue, "categoryRowTemplate");
            var tileTemplate = Reference<DecorationCatalogueTileView>(
                catalogue, "categoryTileTemplate");
            var pickUpButton = Reference<Button>(catalogue, "pickUpPointButton");
            Assert.That(verticalScroll.viewport, Is.Not.Null);
            Assert.That(verticalScroll.content, Is.SameAs(categoryContent));
            Assert.That(rowTemplate.GetComponent<ScrollRect>()?.content
                ?.GetComponent<HorizontalLayoutGroup>(), Is.Not.Null);
            Assert.That(tileTemplate.GetComponent<Button>(), Is.Not.Null);
            Assert.That(cataloguePrefab
                .GetComponentsInChildren<DecorationCatalogueTileView>(true),
                Is.EqualTo(new[] { tileTemplate }),
                "Phase 8 must reuse the single card template rather than add a second framework.");
            Assert.That(cataloguePrefab.GetComponentsInChildren<Button>(true)
                .Count(button => button.name == "PickUpPointButton"), Is.EqualTo(1));
            Assert.That(pickUpButton.GetComponent<DecorationPointerBoundaryEventHook>(), Is.Not.Null);
            Assert.That(pickUpButton.GetComponentInChildren<TMP_Text>(true)?.text,
                Is.EqualTo("Pick-up Point"));

            foreach (var field in new[]
                     {
                         "storeButton", "rotateButton", "cancelButton", "confirmButton"
                     })
            {
                var button = Reference<Button>(actionBar, field);
                Assert.That(button.GetComponent<DecorationPointerBoundaryEventHook>(), Is.Not.Null,
                    field + " must retain the shared pointer-ownership adapter.");
            }
        }

        [Test]
        public void ProductionCataloguePrefab_BindsThreeStableRowsAndKeepsEmptyRows()
        {
            var root = InstantiatePrefab(CataloguePrefabPath);
            var view = root.GetComponent<DecorationCatalogueView>();
            var categoryContent = Reference<RectTransform>(view, "categoryContent");
            var catalogue = CreateCatalogue(CreateDefinition(
                "furniture.counter.phase8", "Phase 8 Counter",
                PlacementSurfaceType.Floor, FurnitureFunctionType.None));
            var selected = new List<DecorationCatalogueItemModel>();

            view.Configure(new UiPointerBoundary(), new UiTransitionRunner(() => true));
            view.BindCategories(BuildFurnitureTab(catalogue), selected.Add);
            view.ShowCatalogue();
            view.SetSheetState(DecorationSheetState.Expanded, hasActivePreview: false);
            Canvas.ForceUpdateCanvases();

            var rows = categoryContent.Cast<Transform>().ToArray();
            Assert.That(rows.Select(row => row.name), Is.EqualTo(new[]
            {
                "CategoryRow_furniture",
                "CategoryRow_cash-register",
                "CategoryRow_coffee-machine"
            }));
            Assert.That(rows.Select(RowLabel), Is.EqualTo(new[]
            {
                "Furniture", "Cash Register", "Coffee Machine"
            }));
            Assert.That(rows.Select(row => row.GetComponent<ScrollRect>().content
                .GetComponent<HorizontalLayoutGroup>().spacing), Is.All.EqualTo(8f));
            Assert.That(rows[1].GetComponent<ScrollRect>().content.childCount, Is.Zero);
            Assert.That(rows[2].GetComponent<ScrollRect>().content.childCount, Is.Zero);
            var furnitureTile = rows[0].GetComponent<ScrollRect>().content
                .GetComponentInChildren<DecorationCatalogueTileView>(true);
            Assert.That(furnitureTile, Is.Not.Null);
            furnitureTile.GetComponent<Button>().onClick.Invoke();
            Assert.That(selected, Has.Count.EqualTo(1));
            Assert.That(selected[0].FurnitureDefinition.DefinitionId,
                Is.EqualTo("furniture.counter.phase8"));
            Assert.That(root.GetComponentsInChildren<Button>(true)
                .Count(button => button.name == "PickUpPointButton"), Is.EqualTo(1));
            Assert.That(rows.Any(row => row.name.IndexOf(
                "pick", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
        }

        [Test]
        public void ProductionCataloguePrefab_CashRegisterAndCoffeeMachineTilesUseSharedTypedSelectionCallback()
        {
            var root = InstantiatePrefab(CataloguePrefabPath);
            var view = root.GetComponent<DecorationCatalogueView>();
            var categoryContent = Reference<RectTransform>(view, "categoryContent");
            var tileTemplate = Reference<DecorationCatalogueTileView>(
                view, "categoryTileTemplate");
            var catalogue = CreateCatalogue(
                CreateDefinition("equipment.cash-register.phase8", "Phase 8 Register",
                    PlacementSurfaceType.FurnitureSurface,
                    FurnitureFunctionType.CashRegister),
                CreateDefinition("equipment.coffee-machine.phase8", "Phase 8 Machine",
                    PlacementSurfaceType.FurnitureSurface,
                    FurnitureFunctionType.CoffeeMachine));
            var selected = new List<DecorationCatalogueItemModel>();

            view.Configure(new UiPointerBoundary(), new UiTransitionRunner(() => true));
            view.BindCategories(BuildFurnitureTab(catalogue), selected.Add);
            view.ShowCatalogue();
            view.SetSheetState(DecorationSheetState.Expanded, hasActivePreview: false);

            var cashTile = FindOnlyTile(categoryContent, "cash-register");
            var coffeeTile = FindOnlyTile(categoryContent, "coffee-machine");
            Assert.That(cashTile.GetType(), Is.EqualTo(tileTemplate.GetType()));
            Assert.That(coffeeTile.GetType(), Is.EqualTo(tileTemplate.GetType()));

            cashTile.GetComponent<Button>().onClick.Invoke();
            coffeeTile.GetComponent<Button>().onClick.Invoke();

            Assert.That(selected.Select(item => item.Kind), Is.EqualTo(new[]
            {
                ParseKind("CashRegister"), ParseKind("CoffeeMachine")
            }));
            Assert.That(selected.Select(item => item.ItemId), Is.EqualTo(new[]
            {
                "equipment.cash-register.phase8", "equipment.coffee-machine.phase8"
            }));
        }

        [UnityTest]
        public IEnumerator RepeatedConfigureAndRebind_UsesOnlyCurrentOwnershipAndCallbacks()
        {
            var root = InstantiatePrefab(CataloguePrefabPath);
            var view = root.GetComponent<DecorationCatalogueView>();
            var categoryContent = Reference<RectTransform>(view, "categoryContent");
            var pickUpButton = Reference<Button>(view, "pickUpPointButton");
            var pickUpHook = pickUpButton.GetComponent<DecorationPointerBoundaryEventHook>();
            var oldBoundary = new UiPointerBoundary();
            var currentBoundary = new UiPointerBoundary();
            var oldSelected = new List<DecorationCatalogueItemModel>();
            var currentSelected = new List<DecorationCatalogueItemModel>();
            var pickUpRequests = 0;
            SubscribePickUp(view, () => pickUpRequests++);

            view.Configure(oldBoundary, new UiTransitionRunner(() => true));
            view.BindCategories(BuildFurnitureTab(CreateCatalogue(CreateDefinition(
                "equipment.cash-register.old", "Old Register",
                PlacementSurfaceType.FurnitureSurface,
                FurnitureFunctionType.CashRegister))), oldSelected.Add);

            view.Configure(currentBoundary, new UiTransitionRunner(() => true));
            view.BindCategories(BuildFurnitureTab(CreateCatalogue(CreateDefinition(
                "equipment.cash-register.current", "Current Register",
                PlacementSurfaceType.FurnitureSurface,
                FurnitureFunctionType.CashRegister))), currentSelected.Add);
            view.ShowCatalogue();
            view.SetSheetState(DecorationSheetState.Expanded, hasActivePreview: false);
            yield return null;

            FindOnlyTile(categoryContent, "cash-register")
                .GetComponent<Button>().onClick.Invoke();
            Assert.That(oldSelected, Is.Empty,
                "The old model callback must not survive the current binding.");
            Assert.That(currentSelected.Select(item => item.ItemId),
                Is.EqualTo(new[] { "equipment.cash-register.current" }));

            var pointer = new PointerEventData(null) { pointerId = 901 };
            pickUpHook.OnPointerDown(pointer);
            Assert.That(oldBoundary.CanProcessScenePointer(901), Is.True,
                "Repeated Configure must replace the old pointer registrar.");
            Assert.That(currentBoundary.CanProcessScenePointer(901), Is.False);
            pickUpHook.OnPointerUp(pointer);
            pickUpButton.onClick.Invoke();
            Assert.That(pickUpRequests, Is.EqualTo(1),
                "Repeated Configure must retain exactly one owned Pick-up listener.");
        }

        [Test]
        public void ProductionActionBarPrefab_PickUpOmitsRotateWhileMountedEquipmentRetainsIt()
        {
            var root = InstantiatePrefab(ActionBarPrefabPath);
            var view = root.GetComponent<DecorationActionBarView>();
            var rotate = Reference<Button>(view, "rotateButton");
            var store = Reference<Button>(view, "storeButton");

            SetCatalogueItemActions(view, "PickUpPoint", existing: true);
            view.Show(canStore: true, canConfirm: true, PlacementFeedbackKey.None);
            Assert.That(view.VisibleActionLabels,
                Is.EqualTo(new[] { "Store", "Cancel", "Confirm" }));
            Assert.That(rotate.gameObject.activeSelf, Is.False);
            Assert.That(store.gameObject.activeSelf, Is.True);

            SetCatalogueItemActions(view, "CashRegister", existing: true);
            view.Show(canStore: true, canConfirm: true, PlacementFeedbackKey.None);
            Assert.That(view.VisibleActionLabels,
                Is.EqualTo(new[] { "Store", "Cancel", "Rotate", "Confirm" }));
            Assert.That(rotate.gameObject.activeSelf, Is.True);

            SetCatalogueItemActions(view, "CoffeeMachine", existing: false);
            view.Show(canStore: false, canConfirm: true, PlacementFeedbackKey.None);
            Assert.That(view.VisibleActionLabels,
                Is.EqualTo(new[] { "Cancel", "Rotate", "Confirm" }));
            Assert.That(rotate.gameObject.activeSelf, Is.True);
            Assert.That(store.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void PickUpButton_UsesSharedPointerOwnershipAndRaisesOncePerEligibleClick()
        {
            var root = InstantiatePrefab(CataloguePrefabPath);
            var view = root.GetComponent<DecorationCatalogueView>();
            var button = Reference<Button>(view, "pickUpPointButton");
            var hook = button.GetComponent<DecorationPointerBoundaryEventHook>();
            var boundary = new UiPointerBoundary();
            var requests = 0;
            SubscribePickUp(view, () => requests++);
            view.Configure(boundary, new UiTransitionRunner(() => true));
            view.BindCategories(BuildFurnitureTab(CreateCatalogue()), _ => { });
            view.ShowCatalogue();
            view.SetSheetState(DecorationSheetState.Expanded, hasActivePreview: false);

            Click(button, hook, boundary, 701);
            Assert.That(requests, Is.EqualTo(1));
            Click(button, hook, boundary, 702);
            Assert.That(requests, Is.EqualTo(2));
        }

        [Test]
        public void PickUpButton_DragCancelAndNonInteractiveStatesDoNotRaiseRequests()
        {
            var root = InstantiatePrefab(CataloguePrefabPath);
            var view = root.GetComponent<DecorationCatalogueView>();
            var button = Reference<Button>(view, "pickUpPointButton");
            var hook = button.GetComponent<DecorationPointerBoundaryEventHook>();
            var boundary = new UiPointerBoundary();
            var requests = 0;
            SubscribePickUp(view, () => requests++);
            view.Configure(boundary, new UiTransitionRunner(() => true));
            view.BindCategories(BuildFurnitureTab(CreateCatalogue()), _ => { });
            view.ShowCatalogue();
            view.SetSheetState(DecorationSheetState.Expanded, hasActivePreview: false);

            var drag = new PointerEventData(null) { pointerId = 801, dragging = true };
            hook.OnPointerDown(drag);
            Assert.That(boundary.CanProcessScenePointer(801), Is.False);
            hook.OnPointerUp(drag);
            Assert.That(boundary.CanProcessScenePointer(801), Is.True);
            Assert.That(requests, Is.Zero,
                "A drag without PointerClick must not request a Pick-up Preview.");

            var canceled = new PointerEventData(null) { pointerId = 802 };
            hook.OnPointerDown(canceled);
            view.Hide();
            hook.OnPointerUp(canceled);
            button.onClick.Invoke();
            Assert.That(requests, Is.Zero,
                "A release after the catalogue is hidden must not invoke the action.");

            view.ShowCatalogue();
            view.SetSheetState(DecorationSheetState.Expanded, hasActivePreview: false);
            button.interactable = false;
            button.onClick.Invoke();
            Assert.That(requests, Is.Zero,
                "Direct callbacks must still respect the non-interactive state.");
        }


#endif

        [Test]
        public void RuntimeCatalogue_BindsStableFurnitureCashRegisterAndCoffeeMachineRowsIncludingEmptyRows()
        {
            var fixture = CreateRuntimeCatalogueFixture();
            var catalogue = CreateCatalogue(CreateDefinition(
                "furniture.counter.phase8", "Phase 8 Counter",
                PlacementSurfaceType.Floor, FurnitureFunctionType.None));
            var selected = new List<DecorationCatalogueItemModel>();

            fixture.View.Configure(
                new UiPointerBoundary(), new UiTransitionRunner(() => true));
            fixture.View.BindCategories(BuildFurnitureTab(catalogue), selected.Add);
            fixture.View.ShowCatalogue();
            fixture.View.SetSheetState(
                DecorationSheetState.Expanded, hasActivePreview: false);
            Canvas.ForceUpdateCanvases();

            var rows = fixture.CategoryContent.Cast<Transform>().ToArray();
            Assert.That(rows.Select(row => row.name), Is.EqualTo(new[]
            {
                "CategoryRow_furniture",
                "CategoryRow_cash-register",
                "CategoryRow_coffee-machine"
            }));
            Assert.That(rows.Select(RowLabel), Is.EqualTo(new[]
            {
                "Furniture", "Cash Register", "Coffee Machine"
            }));
            Assert.That(rows.Select(row => row.GetComponent<ScrollRect>().content
                .GetComponent<HorizontalLayoutGroup>().spacing), Is.All.EqualTo(0f));
            Assert.That(rows[1].GetComponent<ScrollRect>().content.childCount, Is.Zero);
            Assert.That(rows[2].GetComponent<ScrollRect>().content.childCount, Is.Zero);
            var furnitureTile = rows[0].GetComponent<ScrollRect>().content
                .GetComponentInChildren<DecorationCatalogueTileView>(true);
            Assert.That(furnitureTile, Is.Not.Null);
            furnitureTile.GetComponent<Button>().onClick.Invoke();
            Assert.That(selected, Has.Count.EqualTo(1));
            Assert.That(selected[0].FurnitureDefinition.DefinitionId,
                Is.EqualTo("furniture.counter.phase8"));
            Assert.That(fixture.Root.GetComponentsInChildren<Button>(true)
                .Count(button => button.name == "PickUpPointButton"), Is.EqualTo(1));
            Assert.That(rows.Any(row => row.name.IndexOf(
                "pick", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
        }

        [Test]
        public void RuntimeCatalogue_CashRegisterAndCoffeeMachineTilesUseSharedTypedSelectionCallback()
        {
            var fixture = CreateRuntimeCatalogueFixture();
            var catalogue = CreateCatalogue(
                CreateDefinition("equipment.cash-register.phase8", "Phase 8 Register",
                    PlacementSurfaceType.FurnitureSurface,
                    FurnitureFunctionType.CashRegister),
                CreateDefinition("equipment.coffee-machine.phase8", "Phase 8 Machine",
                    PlacementSurfaceType.FurnitureSurface,
                    FurnitureFunctionType.CoffeeMachine));
            var selected = new List<DecorationCatalogueItemModel>();

            fixture.View.Configure(
                new UiPointerBoundary(), new UiTransitionRunner(() => true));
            fixture.View.BindCategories(BuildFurnitureTab(catalogue), selected.Add);
            fixture.View.ShowCatalogue();
            fixture.View.SetSheetState(
                DecorationSheetState.Expanded, hasActivePreview: false);

            FindOnlyTile(fixture.CategoryContent, "cash-register")
                .GetComponent<Button>().onClick.Invoke();
            FindOnlyTile(fixture.CategoryContent, "coffee-machine")
                .GetComponent<Button>().onClick.Invoke();

            Assert.That(selected.Select(item => item.Kind), Is.EqualTo(new[]
            {
                ParseKind("CashRegister"), ParseKind("CoffeeMachine")
            }));
            Assert.That(selected.Select(item => item.ItemId), Is.EqualTo(new[]
            {
                "equipment.cash-register.phase8", "equipment.coffee-machine.phase8"
            }));
        }

        [UnityTest]
        public IEnumerator RuntimeCatalogue_RepeatedConfigureAndRebindUsesOnlyCurrentOwnershipAndCallbacks()
        {
            var fixture = CreateRuntimeCatalogueFixture();
            var oldBoundary = new UiPointerBoundary();
            var currentBoundary = new UiPointerBoundary();
            var oldSelected = new List<DecorationCatalogueItemModel>();
            var currentSelected = new List<DecorationCatalogueItemModel>();
            var pickUpRequests = 0;
            SubscribePickUp(fixture.View, () => pickUpRequests++);

            fixture.View.Configure(oldBoundary, new UiTransitionRunner(() => true));
            fixture.View.BindCategories(BuildFurnitureTab(CreateCatalogue(CreateDefinition(
                "equipment.cash-register.old", "Old Register",
                PlacementSurfaceType.FurnitureSurface,
                FurnitureFunctionType.CashRegister))), oldSelected.Add);

            fixture.View.Configure(currentBoundary, new UiTransitionRunner(() => true));
            fixture.View.BindCategories(BuildFurnitureTab(CreateCatalogue(CreateDefinition(
                "equipment.cash-register.current", "Current Register",
                PlacementSurfaceType.FurnitureSurface,
                FurnitureFunctionType.CashRegister))), currentSelected.Add);
            fixture.View.ShowCatalogue();
            fixture.View.SetSheetState(
                DecorationSheetState.Expanded, hasActivePreview: false);
            yield return null;

            FindOnlyTile(fixture.CategoryContent, "cash-register")
                .GetComponent<Button>().onClick.Invoke();
            Assert.That(oldSelected, Is.Empty,
                "The old model callback must not survive the current binding.");
            Assert.That(currentSelected.Select(item => item.ItemId),
                Is.EqualTo(new[] { "equipment.cash-register.current" }));

            var pointer = new PointerEventData(null) { pointerId = 901 };
            fixture.PickUpHook.OnPointerDown(pointer);
            Assert.That(oldBoundary.CanProcessScenePointer(901), Is.True,
                "Repeated Configure must replace the old pointer registrar.");
            Assert.That(currentBoundary.CanProcessScenePointer(901), Is.False);
            fixture.PickUpHook.OnPointerUp(pointer);
            fixture.PickUpButton.onClick.Invoke();
            Assert.That(pickUpRequests, Is.EqualTo(1),
                "Repeated Configure must retain exactly one owned Pick-up listener.");
        }

        [Test]
        public void RuntimeActionBar_PickUpOmitsRotateWhileMountedEquipmentRetainsIt()
        {
            var fixture = CreateRuntimeActionBarFixture();

            SetCatalogueItemActions(fixture.View, "PickUpPoint", existing: true);
            fixture.View.Show(canStore: true, canConfirm: true, PlacementFeedbackKey.None);
            Assert.That(fixture.View.VisibleActionLabels,
                Is.EqualTo(new[] { "Store", "Cancel", "Confirm" }));
            Assert.That(fixture.Rotate.gameObject.activeSelf, Is.False);
            Assert.That(fixture.Store.gameObject.activeSelf, Is.True);

            SetCatalogueItemActions(fixture.View, "CashRegister", existing: true);
            fixture.View.Show(canStore: true, canConfirm: true, PlacementFeedbackKey.None);
            Assert.That(fixture.View.VisibleActionLabels,
                Is.EqualTo(new[] { "Store", "Cancel", "Rotate", "Confirm" }));
            Assert.That(fixture.Rotate.gameObject.activeSelf, Is.True);

            SetCatalogueItemActions(fixture.View, "CoffeeMachine", existing: false);
            fixture.View.Show(canStore: false, canConfirm: true, PlacementFeedbackKey.None);
            Assert.That(fixture.View.VisibleActionLabels,
                Is.EqualTo(new[] { "Cancel", "Rotate", "Confirm" }));
            Assert.That(fixture.Rotate.gameObject.activeSelf, Is.True);
            Assert.That(fixture.Store.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void RuntimePickUpButton_UsesSharedPointerOwnershipAndRaisesExactlyOncePerEligibleClick()
        {
            var fixture = CreateRuntimeCatalogueFixture();
            var boundary = new UiPointerBoundary();
            var requests = 0;
            SubscribePickUp(fixture.View, () => requests++);
            fixture.View.Configure(boundary, new UiTransitionRunner(() => true));
            fixture.View.BindCategories(BuildFurnitureTab(CreateCatalogue()), _ => { });
            fixture.View.ShowCatalogue();
            fixture.View.SetSheetState(
                DecorationSheetState.Expanded, hasActivePreview: false);

            Click(fixture.PickUpButton, fixture.PickUpHook, boundary, 701);
            Assert.That(requests, Is.EqualTo(1));
            Click(fixture.PickUpButton, fixture.PickUpHook, boundary, 702);
            Assert.That(requests, Is.EqualTo(2));
        }

        [Test]
        public void RuntimePickUpButton_DragCancelAndNonInteractiveStatesDoNotRaiseRequests()
        {
            var fixture = CreateRuntimeCatalogueFixture();
            var boundary = new UiPointerBoundary();
            var requests = 0;
            SubscribePickUp(fixture.View, () => requests++);
            fixture.View.Configure(boundary, new UiTransitionRunner(() => true));
            fixture.View.BindCategories(BuildFurnitureTab(CreateCatalogue()), _ => { });
            fixture.View.ShowCatalogue();
            fixture.View.SetSheetState(
                DecorationSheetState.Expanded, hasActivePreview: false);

            var drag = new PointerEventData(null) { pointerId = 801, dragging = true };
            fixture.PickUpHook.OnPointerDown(drag);
            Assert.That(boundary.CanProcessScenePointer(801), Is.False);
            fixture.PickUpHook.OnPointerUp(drag);
            Assert.That(boundary.CanProcessScenePointer(801), Is.True);
            Assert.That(requests, Is.Zero,
                "A drag without PointerClick must not request a Pick-up Preview.");

            var canceled = new PointerEventData(null) { pointerId = 802 };
            fixture.PickUpHook.OnPointerDown(canceled);
            fixture.View.Hide();
            fixture.PickUpHook.OnPointerUp(canceled);
            fixture.PickUpButton.onClick.Invoke();
            Assert.That(requests, Is.Zero,
                "A release after the catalogue is hidden must not invoke the action.");

            fixture.View.ShowCatalogue();
            fixture.View.SetSheetState(
                DecorationSheetState.Expanded, hasActivePreview: false);
            fixture.PickUpButton.interactable = false;
            fixture.PickUpButton.onClick.Invoke();
            Assert.That(requests, Is.Zero,
                "Direct callbacks must still respect the non-interactive state.");
        }

        private static void Click(
            Button button,
            DecorationPointerBoundaryEventHook hook,
            UiPointerBoundary boundary,
            int pointerId)
        {
            var data = new PointerEventData(null) { pointerId = pointerId };
            hook.OnPointerDown(data);
            Assert.That(boundary.CanProcessScenePointer(pointerId), Is.False);
            hook.OnPointerUp(data);
            Assert.That(boundary.CanProcessScenePointer(pointerId), Is.True);
            button.onClick.Invoke();
        }

        [Test]
        public void RuntimeReturnToEditing_RebindsOwnershipOnce_AndRestoresViewportWhenPreviewEnds()
        {
            var fixture = CreateRuntimeCatalogueFixture();
            var scrollObject = RuntimeUiObject("VerticalScroll", fixture.Root.transform);
            var scroll = scrollObject.AddComponent<ScrollRect>();
            var scrollRect = (RectTransform)scrollObject.transform;
            var originalOffset = new Vector2(-17, -29);
            scrollRect.offsetMax = originalOffset;
            SetField(fixture.View, "verticalScroll", scroll);
            var oldBoundary = new UiPointerBoundary();
            var boundary = new UiPointerBoundary();
            var requests = 0;
            fixture.View.ReturnToEditingRequested += () => requests++;
            fixture.View.Configure(oldBoundary, new UiTransitionRunner(() => true));
            fixture.View.SetEditingContext("正在编辑：取餐点 · 尚未确认\n位置有效，可以确认", true);
            fixture.View.Configure(boundary, new UiTransitionRunner(() => true));
            fixture.View.Configure(boundary, new UiTransitionRunner(() => true));
            fixture.View.ShowCatalogue();
            var button = fixture.View.GetComponentsInChildren<Button>(true).Single(item => item.name == "ReturnToEditing");
            var hook = button.GetComponent<DecorationPointerBoundaryEventHook>();
            var pointer = new PointerEventData(null) { pointerId = 803 };
            hook.OnPointerDown(pointer);
            Assert.That(oldBoundary.CanProcessScenePointer(803), Is.True);
            Assert.That(boundary.CanProcessScenePointer(803), Is.False);
            hook.OnPointerUp(pointer);
            Click(button, hook, boundary, 804);
            Assert.That(requests, Is.EqualTo(1));
            Assert.That(scrollRect.offsetMax.y, Is.LessThan(originalOffset.y));
            fixture.View.SetEditingContext("正在编辑：地板 · 尚未确认\n请选择样式", false);
            Assert.That(button.gameObject.activeSelf, Is.False, "Surface style browsing remains inline, with no forced Return.");
            fixture.View.SetEditingContext(null, false);
            Assert.That(scrollRect.offsetMax, Is.EqualTo(originalOffset));
            button.onClick.Invoke();
            Assert.That(requests, Is.EqualTo(1), "Cleared context must not react to a stale callback.");
        }

        [UnityTest]
        public IEnumerator BrowsingMemory_RestoresEachTabAndCategoryAfterRowsChangeOrder()
        {
            var fixture = CreateScrollingCatalogueFixture();
            BindBrowsingTab(fixture.View, "Furniture", BrowsingCategories("chairs", "tables"));
            SetBrowsingGeometry(fixture.View);
            yield return null;
            fixture.View.VerticalScroll.verticalNormalizedPosition = .3f;
            BrowsingRow(fixture.View, "chairs").horizontalNormalizedPosition = .7f;
            BrowsingRow(fixture.View, "tables").horizontalNormalizedPosition = .2f;
            BindBrowsingTab(fixture.View, "Wall", BrowsingCategories("chairs", "paint"));
            SetBrowsingGeometry(fixture.View);
            yield return null;
            fixture.View.VerticalScroll.verticalNormalizedPosition = .8f;
            BrowsingRow(fixture.View, "chairs").horizontalNormalizedPosition = .4f;

            BindBrowsingTab(fixture.View, "Furniture", BrowsingCategories("tables", "chairs"));
            SetBrowsingGeometry(fixture.View);
            yield return null;
            Assert.That(fixture.View.VerticalScroll.verticalNormalizedPosition, Is.EqualTo(.3f).Within(.002f));
            Assert.That(BrowsingRow(fixture.View, "chairs").horizontalNormalizedPosition, Is.EqualTo(.7f).Within(.002f),
                "The saved row belongs to CategoryId, not its current list index.");
            Assert.That(BrowsingRow(fixture.View, "tables").horizontalNormalizedPosition, Is.EqualTo(.2f).Within(.002f));
            BindBrowsingTab(fixture.View, "Wall", BrowsingCategories("paint", "chairs"));
            SetBrowsingGeometry(fixture.View);
            yield return null;
            Assert.That(fixture.View.VerticalScroll.verticalNormalizedPosition, Is.EqualTo(.8f).Within(.002f));
            Assert.That(BrowsingRow(fixture.View, "chairs").horizontalNormalizedPosition, Is.EqualTo(.4f).Within(.002f),
                "Matching CategoryIds in different tabs must not overwrite each other.");
        }

        [UnityTest]
        public IEnumerator BrowsingMemory_ClampsElasticOverscrollAfterReplacementLayoutSettles()
        {
            var fixture = CreateScrollingCatalogueFixture();
            BindBrowsingTab(fixture.View, "Furniture", BrowsingCategories("chairs"));
            SetBrowsingGeometry(fixture.View);
            yield return null;
            fixture.View.VerticalScroll.verticalNormalizedPosition = -.2f;
            BrowsingRow(fixture.View, "chairs").horizontalNormalizedPosition = 1.25f;
            BindBrowsingTab(fixture.View, "Wall", BrowsingCategories("paint"));
            SetBrowsingGeometry(fixture.View);
            yield return null;
            BindBrowsingTab(fixture.View, "Furniture", BrowsingCategories("chairs"));
            // Simulate smaller content from the next Canvas layout pass; clamp elastic overscroll.
            // 重建后内容变短；只恢复合法范围，不把弹性越界带回新列表。
            SetBrowsingGeometry(fixture.View, 600f, 650f);
            yield return null;
            Assert.That(fixture.View.VerticalScroll.verticalNormalizedPosition, Is.EqualTo(0f).Within(.002f));
            Assert.That(BrowsingRow(fixture.View, "chairs").horizontalNormalizedPosition, Is.EqualTo(1f).Within(.002f));
            Assert.That(fixture.View.VerticalScroll.velocity, Is.EqualTo(Vector2.zero));
            Assert.That(BrowsingRow(fixture.View, "chairs").velocity, Is.EqualTo(Vector2.zero));
        }

        [UnityTest]
        public IEnumerator BrowsingMemory_NewSessionResetsCurrentPositionAndCannotRecapturePreviousTab()
        {
            var fixture = CreateScrollingCatalogueFixture();
            BindBrowsingTab(fixture.View, "Furniture", BrowsingCategories("chairs"));
            SetBrowsingGeometry(fixture.View);
            yield return null;
            fixture.View.VerticalScroll.verticalNormalizedPosition = .25f;
            BrowsingRow(fixture.View, "chairs").horizontalNormalizedPosition = .65f;
            ResetBrowsingSession(fixture.View);
            Assert.That(fixture.View.VerticalScroll.verticalNormalizedPosition, Is.EqualTo(1f).Within(.002f));
            Assert.That(BrowsingRow(fixture.View, "chairs").horizontalNormalizedPosition, Is.EqualTo(0f).Within(.002f));
            BindBrowsingTab(fixture.View, "Wall", BrowsingCategories("paint"));
            SetBrowsingGeometry(fixture.View);
            yield return null;
            BindBrowsingTab(fixture.View, "Furniture", BrowsingCategories("chairs"));
            SetBrowsingGeometry(fixture.View);
            yield return null;
            Assert.That(fixture.View.VerticalScroll.verticalNormalizedPosition, Is.EqualTo(1f).Within(.002f));
            Assert.That(BrowsingRow(fixture.View, "chairs").horizontalNormalizedPosition, Is.EqualTo(0f).Within(.002f));
        }

        [Test]
        public void BrowsingMemory_SwitchingTabEndsNestedDragAndStopsOutgoingScrollInertia()
        {
            var fixture = CreateScrollingCatalogueFixture();
            BindBrowsingTab(fixture.View, "Furniture", BrowsingCategories("chairs"));
            SetBrowsingGeometry(fixture.View);
            var outgoingRow = BrowsingRow(fixture.View, "chairs");
            fixture.View.BeginNestedDrag(outgoingRow);
            Assert.That(fixture.View.UpdateNestedDrag(new Vector2(2f, 30f)), Is.EqualTo("Vertical"));
            outgoingRow.velocity = new Vector2(120f, 0f);
            fixture.View.VerticalScroll.velocity = new Vector2(0f, 90f);
            BindBrowsingTab(fixture.View, "Wall", BrowsingCategories("paint"));
            Assert.That(fixture.View.NestedDragOwner, Is.Null);
            Assert.That(fixture.View.IsSceneDragBlocked, Is.False);
            Assert.That(outgoingRow.horizontal, Is.True);
            Assert.That(outgoingRow.velocity, Is.EqualTo(Vector2.zero), "A replaced row must not retain a fling.");
            Assert.That(fixture.View.VerticalScroll.velocity, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void CollapsedHandle_OffersContinueAddingOnlyWhenPreviewHasEndedAndExpandsOnlyOnClick()
        {
            var fixture = CreateRuntimeCatalogueFixture();
            var collapsed = Reference<GameObject>(fixture.View, "collapsedRoot");
            var handle = CreateRuntimeButton("CatalogueHandle", collapsed.transform);
            var label = RuntimeUiObject("Label", handle.transform).AddComponent<TextMeshProUGUI>();
            label.text = "Catalogue";
            var icon = RuntimeUiObject("Icon", handle.transform).AddComponent<Image>();
            icon.sprite = CreateSprite("ExistingHandleIcon");
            var originalIcon = icon.sprite;
            var originalSize = ((RectTransform)handle.transform).sizeDelta;
            SetField(fixture.View, "collapsedHandleButton", handle);
            fixture.View.Configure(new UiPointerBoundary(), new UiTransitionRunner(() => true));
            fixture.View.ShowCatalogue();
            fixture.View.SetEditingContext("正在编辑：椅子 · 尚未确认", true);
            // Match the Controller Preview-begin path: collapse the Catalogue state, then its Sheet presentation.
            // Controller 先收起 Catalogue，再切换 Sheet；SetSheetState 本身不更新 Catalogue State。
            fixture.View.ShowCollapsedHandle();
            fixture.View.SetSheetState(DecorationSheetState.CompactPreview, hasActivePreview: true);
            Assert.That(label.text, Is.EqualTo("Catalogue"));
            fixture.View.SetEditingContext(null, false);
            fixture.View.SetSheetState(DecorationSheetState.CompactPreview, hasActivePreview: false);
            Assert.That(label.text, Is.EqualTo("继续添加"));
            Assert.That(fixture.View.State, Is.EqualTo(DecorationCatalogueState.Collapsed),
                "Finishing a Preview must not open the Catalogue automatically.");
            Assert.That(icon.sprite, Is.SameAs(originalIcon));
            Assert.That(((RectTransform)handle.transform).sizeDelta, Is.EqualTo(originalSize));
            handle.onClick.Invoke();
            Assert.That(fixture.View.State, Is.EqualTo(DecorationCatalogueState.Expanded));
            fixture.View.SetEditingContext("正在编辑：椅子 · 尚未确认", true);
            fixture.View.ShowCollapsedHandle();
            Assert.That(label.text, Is.EqualTo("Catalogue"), "A later Preview must restore the browsing label.");
        }

        private RuntimeCatalogueFixture CreateScrollingCatalogueFixture()
        {
            var fixture = CreateRuntimeCatalogueFixture();
            var expanded = Reference<GameObject>(fixture.View, "expandedRoot");
            var viewport = RuntimeUiObject("BrowsingViewport", expanded.transform).GetComponent<RectTransform>();
            SetTopLeftRect(viewport, 300f, 320f);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = fixture.CategoryContent;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            fixture.CategoryContent.SetParent(viewport, false);
            SetField(fixture.View, "verticalScroll", scroll);
            fixture.View.Configure(new UiPointerBoundary(), new UiTransitionRunner(() => true));
            fixture.View.ShowCatalogue();
            return fixture;
        }

        private static DecorationCategoryModel[] BrowsingCategories(params string[] categoryIds)
        {
            return categoryIds.Select(id => new DecorationCategoryModel(
                id, id, Array.Empty<DecorationCatalogueItemModel>())).ToArray();
        }

        private static ScrollRect BrowsingRow(DecorationCatalogueView view, string categoryId)
        {
            return view.CategoryRows.Single(row => row.HorizontalScroll.name == "CategoryRow_" + categoryId).HorizontalScroll;
        }

        private static void SetBrowsingGeometry(DecorationCatalogueView view, float contentHeight = 900f, float rowWidth = 1200f)
        {
            SetTopLeftRect(view.VerticalScroll.content, 300f, contentHeight);
            foreach (var row in view.CategoryRows)
            {
                var scroll = row.HorizontalScroll;
                SetTopLeftRect((RectTransform)scroll.transform, 300f, 128f);
                SetTopLeftRect(scroll.viewport, 300f, 128f);
                // Fixture geometry is input; real ScrollRects calculate bounds and normalized positions.
                scroll.content.GetComponent<HorizontalLayoutGroup>().enabled = false;
                SetTopLeftRect(scroll.content, rowWidth, 128f);
            }
        }

        private static void SetTopLeftRect(RectTransform rect, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = Vector2.zero;
        }

        private static void BindBrowsingTab(DecorationCatalogueView view, string key, IReadOnlyList<DecorationCategoryModel> categories)
        {
            view.BindCategories(key, categories, _ => { });
        }

        private static void ResetBrowsingSession(DecorationCatalogueView view)
        {
            view.ResetBrowsingMemory();
        }

        private static string RowLabel(Transform row)
        {
            return row.Find("CategoryLabel")?.GetComponent<TMP_Text>()?.text;
        }

        private static DecorationCatalogueTileView FindOnlyTile(
            RectTransform categoryContent,
            string categoryId)
        {
            var row = categoryContent.Cast<Transform>().Single(item =>
                item.gameObject.activeSelf
                && item.name == "CategoryRow_" + categoryId);
            var tiles = row.GetComponent<ScrollRect>().content
                .GetComponentsInChildren<DecorationCatalogueTileView>(true);
            Assert.That(tiles, Has.Length.EqualTo(1));
            return tiles[0];
        }

        private static DecorationCatalogueItemKind ParseKind(string name)
        {
            Assert.That(Enum.TryParse(name, out DecorationCatalogueItemKind result), Is.True);
            return result;
        }

        private static void SubscribePickUp(DecorationCatalogueView view, Action handler)
        {
            var eventInfo = typeof(DecorationCatalogueView).GetEvent(
                "PickUpPointRequested", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(eventInfo, Is.Not.Null,
                "Task 7 requires DecorationCatalogueView.PickUpPointRequested.");
            eventInfo.AddEventHandler(view, handler);
        }

        private static void SetCatalogueItemActions(
            DecorationActionBarView view,
            string kindName,
            bool existing)
        {
            Assert.That(Enum.TryParse(kindName, out DecorationCatalogueItemKind kind), Is.True);
            var method = typeof(DecorationActionBarView).GetMethod(
                "SetCatalogueItemActions",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(DecorationCatalogueItemKind), typeof(bool) },
                null);
            Assert.That(method, Is.Not.Null,
                "Task 7 requires an action-bar presentation API for catalogue item kinds.");
            method.Invoke(view, new object[] { kind, existing });
        }

        private static IReadOnlyList<DecorationCategoryModel> BuildFurnitureTab(
            DecorationCatalogueAsset catalogue)
        {
            var method = typeof(DecorationCatalogueModelBuilder).GetMethod(
                "BuildFurnitureTab",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(DecorationCatalogueAsset) },
                null);
            Assert.That(method, Is.Not.Null,
                "Task 7 requires the additive Furniture Tab model-builder entry point.");
            return (IReadOnlyList<DecorationCategoryModel>)method.Invoke(
                null, new object[] { catalogue });
        }

#if UNITY_EDITOR
        private GameObject InstantiatePrefab(string path)
        {
            var prefab = LoadAsset<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            var root = Track(UnityEngine.Object.Instantiate(prefab));
            root.SetActive(true);
            return root;
        }

#endif

        private DecorationCatalogueAsset CreateCatalogue(
            params FurnitureDefinitionAsset[] definitions)
        {
            var catalogue = Track(ScriptableObject.CreateInstance<DecorationCatalogueAsset>());
            var entries = new List<DecorationCatalogueEntry>();
            foreach (var definition in definitions)
            {
                var entry = new DecorationCatalogueEntry();
                SetField(entry, "definition", definition);
                SetField(entry, "thumbnail", CreateSprite("S_" + definition.DefinitionId));
                entries.Add(entry);
            }

            SetField(catalogue, "entries", entries);
            return catalogue;
        }

        private FurnitureDefinitionAsset CreateDefinition(
            string id,
            string displayName,
            PlacementSurfaceType surfaces,
            FurnitureFunctionType functionType)
        {
            var definition = Track(ScriptableObject.CreateInstance<FurnitureDefinitionAsset>());
            var prefab = Track(new GameObject("PF_" + id));
            SetField(definition, "definitionId", id);
            SetField(definition, "displayName", displayName);
            SetField(definition, "footprintWidth", 1);
            SetField(definition, "footprintDepth", 1);
            SetField(definition, "allowedPlacementSurfaces", surfaces);
            SetField(definition, "functionType", functionType);
            SetField(definition, "prefab", prefab);
            return definition;
        }

        private Sprite CreateSprite(string name)
        {
            var texture = Track(new Texture2D(2, 2) { name = "T_" + name });
            var sprite = Track(Sprite.Create(
                texture, new Rect(0f, 0f, 2f, 2f), Vector2.one * 0.5f));
            sprite.name = name;
            return sprite;
        }

        private static T Reference<T>(object target, string fieldName)
            where T : UnityEngine.Object
        {
            Assert.That(target, Is.Not.Null, "Cannot read a serialized field from a null target.");
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null,
                $"Missing serialized field '{target.GetType().Name}.{fieldName}'.");
            var value = field.GetValue(target);
            Assert.That(value, Is.Not.Null,
                $"Missing serialized reference '{target.GetType().Name}.{fieldName}'.");
            Assert.That(value, Is.InstanceOf<T>(),
                $"Serialized field '{target.GetType().Name}.{fieldName}' must be " +
                $"'{typeof(T).FullName}', but was '{value.GetType().FullName}'.");
            return (T)value;
        }

#if UNITY_EDITOR
        private static T LoadAsset<T>(string path) where T : UnityEngine.Object
        {
            var assetDatabase = Type.GetType("UnityEditor.AssetDatabase, UnityEditor.CoreModule");
            Assert.That(assetDatabase, Is.Not.Null,
                "Production prefab tests require the Unity Editor PlayMode runner.");
            var load = assetDatabase.GetMethod(
                "LoadAssetAtPath",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string), typeof(Type) },
                null);
            Assert.That(load, Is.Not.Null,
                "UnityEditor.AssetDatabase.LoadAssetAtPath(string, Type) was not found.");
            var asset = load.Invoke(null, new object[] { path, typeof(T) });
            Assert.That(asset, Is.InstanceOf<T>(),
                $"Asset '{path}' must be '{typeof(T).FullName}', but was " +
                $"'{asset?.GetType().FullName ?? "null"}'.");
            return (T)asset;
        }

#endif


        private RuntimeCatalogueFixture CreateRuntimeCatalogueFixture()
        {
            var root = Track(new GameObject(
                "RuntimeCatalogue",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(DecorationCatalogueView)));
            var expanded = RuntimeUiObject("ExpandedSheet", root.transform);
            var collapsed = RuntimeUiObject("CollapsedHandle", root.transform);
            var categoryContent = RuntimeUiObject(
                "CategoryContent", expanded.transform).GetComponent<RectTransform>();
            var pickUpButton = CreateRuntimeButton("PickUpPointButton", expanded.transform);
            var pickUpHook = pickUpButton.gameObject
                .AddComponent<DecorationPointerBoundaryEventHook>();
            var view = root.GetComponent<DecorationCatalogueView>();
            SetField(view, "canvasGroup", root.GetComponent<CanvasGroup>());
            SetField(view, "expandedRoot", expanded);
            SetField(view, "collapsedRoot", collapsed);
            SetField(view, "categoryContent", categoryContent);
            SetField(view, "pickUpPointButton", pickUpButton);
            collapsed.SetActive(false);
            return new RuntimeCatalogueFixture(
                root, view, categoryContent, pickUpButton, pickUpHook);
        }

        private RuntimeActionBarFixture CreateRuntimeActionBarFixture()
        {
            var root = Track(new GameObject(
                "RuntimeActionBar",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(DecorationActionBarView)));
            var store = CreateRuntimeButton("StoreButton", root.transform);
            var rotate = CreateRuntimeButton("RotateButton", root.transform);
            var cancel = CreateRuntimeButton("CancelButton", root.transform);
            var confirm = CreateRuntimeButton("ConfirmButton", root.transform);
            var view = root.GetComponent<DecorationActionBarView>();
            SetField(view, "canvasGroup", root.GetComponent<CanvasGroup>());
            SetField(view, "presentationRoot", root.GetComponent<RectTransform>());
            SetField(view, "storeButton", store);
            SetField(view, "rotateButton", rotate);
            SetField(view, "cancelButton", cancel);
            SetField(view, "confirmButton", confirm);
            return new RuntimeActionBarFixture(view, store, rotate);
        }

        private static GameObject RuntimeUiObject(string name, Transform parent)
        {
            var result = new GameObject(name, typeof(RectTransform));
            result.transform.SetParent(parent, false);
            return result;
        }

        private static Button CreateRuntimeButton(string name, Transform parent)
        {
            var result = new GameObject(
                name, typeof(RectTransform), typeof(Image), typeof(Button));
            result.transform.SetParent(parent, false);
            return result.GetComponent<Button>();
        }

        private sealed class RuntimeCatalogueFixture
        {
            public RuntimeCatalogueFixture(
                GameObject root,
                DecorationCatalogueView view,
                RectTransform categoryContent,
                Button pickUpButton,
                DecorationPointerBoundaryEventHook pickUpHook)
            {
                Root = root;
                View = view;
                CategoryContent = categoryContent;
                PickUpButton = pickUpButton;
                PickUpHook = pickUpHook;
            }

            public GameObject Root { get; }
            public DecorationCatalogueView View { get; }
            public RectTransform CategoryContent { get; }
            public Button PickUpButton { get; }
            public DecorationPointerBoundaryEventHook PickUpHook { get; }
        }

        private sealed class RuntimeActionBarFixture
        {
            public RuntimeActionBarFixture(
                DecorationActionBarView view,
                Button store,
                Button rotate)
            {
                View = view;
                Store = store;
                Rotate = rotate;
            }

            public DecorationActionBarView View { get; }
            public Button Store { get; }
            public Button Rotate { get; }
        }

        private static void SetField(object target, string fieldName, object value)
        {
            Assert.That(target, Is.Not.Null, "Cannot set a serialized field on a null target.");
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null,
                $"Missing serialized field '{target.GetType().Name}.{fieldName}'.");
            Assert.That(
                value == null
                    ? !field.FieldType.IsValueType || Nullable.GetUnderlyingType(field.FieldType) != null
                    : field.FieldType.IsInstanceOfType(value),
                Is.True,
                $"Serialized field '{target.GetType().Name}.{fieldName}' expects " +
                $"'{field.FieldType.FullName}', but received " +
                $"'{value?.GetType().FullName ?? "null"}'.");
            field.SetValue(target, value);
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            owned.Add(value);
            return value;
        }
    }
}
