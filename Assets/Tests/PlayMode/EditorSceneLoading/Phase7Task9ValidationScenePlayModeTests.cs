#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using AnimalCafe.Decoration;
using AnimalCafe.Content;
using AnimalCafe.Layout;
using AnimalCafe.UI.Decoration;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AnimalCafe.Tests.PlayMode.EditorSceneLoading
{
    public sealed class Phase7Task9ValidationScenePlayModeTests : InputTestFixture
    {
        private const string ScenePath="Assets/Scenes/Validation/Phase7InteriorWalls.unity";
        private const string MainCafePath="Assets/Scenes/MainCafe.unity";
        private const string CanonicalWindowId="wall-mounted.main.window.canonical.01";

        [UnityTearDown]
        public IEnumerator RestoreCleanSceneAndTime()
        {
            Time.timeScale = 1f;
            var active = SceneManager.GetActiveScene();
            var cleanup = SceneManager.CreateScene("Phase7Task11ValidationCleanup");
            SceneManager.SetActiveScene(cleanup);
            if (active.IsValid() && active.isLoaded && active != cleanup)
            {
                var unload = SceneManager.UnloadSceneAsync(active);
                while (unload != null && !unload.isDone)
                    yield return null;
            }
            Assert.That(Object.FindObjectsByType<AnimalCafe.Decoration.Input.InputSystemDecorationTouchSource>(
                FindObjectsInactive.Include, FindObjectsSortMode.None), Is.Empty);
            Assert.That(UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.enabled, Is.False);
        }

        [UnityTest]
        public IEnumerator MainCafe_WindowCatalogueConfirmsSessionItemAndReloadStartsWithoutPlacedWindow()
        {
            EditorSceneManager.LoadSceneInPlayMode(MainCafePath,new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;yield return null;
            var controller=Object.FindFirstObjectByType<DecorationModeController>();
            controller.EnterDecorationMode();yield return null;
            Assert.That(Object.FindFirstObjectByType<DecorationModeTabsView>().RequestMode(DecorationModeKind.WallDecor),Is.True);
            yield return null;
            var runtime=Object.FindFirstObjectByType<CafeLayoutRuntime>();
            var registry=Object.FindFirstObjectByType<WallMountedSceneRegistry>();
            Assert.That(runtime.WallMountedLayout.CaptureSnapshot().Instances,Is.Empty);
            Assert.That(registry.TryGet(CanonicalWindowId,out _),Is.False);
            var authored=Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(item=>item.name=="P4_Window_BackRight_C3_R0");
            Assert.That(authored.gameObject.activeSelf,Is.False);
            var tiles=Object.FindFirstObjectByType<DecorationCatalogueView>().GetComponentsInChildren<DecorationCatalogueTileView>(true)
                .Where(tile=>tile.ItemId!=null&&tile.ItemId.StartsWith("window.")).ToArray();
            Assert.That(tiles,Has.Length.EqualTo(2));
            tiles.Single(tile=>tile.ItemId=="window.canonical.phase4").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;
            Assert.That(controller.TryConfirmPhase7Preview(),Is.True);
            Assert.That(runtime.WallMountedLayout.CaptureSnapshot().Instances.Count(x=>x.DefinitionId=="window.canonical.phase4"),Is.EqualTo(1));
            EditorSceneManager.LoadSceneInPlayMode(MainCafePath,new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;yield return null;
            runtime=Object.FindFirstObjectByType<CafeLayoutRuntime>();
            registry=Object.FindFirstObjectByType<WallMountedSceneRegistry>();
            Assert.That(runtime.WallMountedLayout.CaptureSnapshot().Instances,Is.Empty);
            Assert.That(registry.TryGet(CanonicalWindowId,out _),Is.False);
        }
        [UnityTest]
        public IEnumerator ValidationScene_EntersPhase7AndCapturesTechnicalScreenshot()
        {
            EditorSceneManager.LoadSceneInPlayMode(ScenePath,new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;yield return null;
            var controller=Object.FindFirstObjectByType<DecorationModeController>();
            Assert.That(controller,Is.Not.Null);
            Assert.That(Object.FindObjectsByType<WallSurfaceRegistry>(FindObjectsSortMode.None),Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<FloorSurfaceGridView>(FindObjectsSortMode.None),Has.Length.EqualTo(1));
            var allTabs=Object.FindObjectsByType<DecorationModeTabsView>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            Assert.That(allTabs,Has.Length.EqualTo(1));
            Assert.That(allTabs[0].gameObject.activeInHierarchy,Is.False,
                "Phase 7 chrome must stay hidden before Decoration Mode is entered.");
            Assert.That(GameObject.Find("TEST_ONLY_WallFixture_2x2"),Is.Not.Null);
            Assert.That(GameObject.Find("TEST_ONLY_WallFixture_3x2"),Is.Not.Null);
            Assert.That(Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(item=>item.name.StartsWith("TECH_REVIEW_Formal_")),Is.EqualTo(5));
            controller.EnterDecorationMode();yield return null;yield return null;
            Assert.That(controller.IsOpen,Is.True);
            Assert.That(allTabs[0].gameObject.activeInHierarchy,Is.True,
                "Mode Tabs must become visible after Decoration Mode is entered.");
            var productionCatalogue=Object.FindObjectsByType<DecorationCatalogueView>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single();
            Assert.That(productionCatalogue.CategoryRows,Has.Count.EqualTo(1));
            Assert.That(productionCatalogue.CategoryRows[0].HorizontalScroll.content.childCount,Is.EqualTo(4));
            Assert.That(productionCatalogue.VerticalScroll.vertical,Is.True);
            Assert.That(productionCatalogue.AreCategoryRowsVisible,Is.True);
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(productionCatalogue.GetComponent<CanvasGroup>().alpha,Is.GreaterThan(.9f));
            var rowRect=(RectTransform)productionCatalogue.CategoryRows[0].HorizontalScroll.transform;
            Assert.That(rowRect.rect.width,Is.GreaterThan(48f),"production row width; catalogue="+productionCatalogue.GetComponent<RectTransform>().rect+", vertical="+productionCatalogue.VerticalScroll.GetComponent<RectTransform>().rect+", row="+rowRect.rect);
            Assert.That(rowRect.rect.height,Is.GreaterThan(48f));
            var productionGraphics=productionCatalogue.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).Where(graphic=>graphic.gameObject.activeInHierarchy).ToArray();
            Assert.That(productionGraphics.Length,Is.GreaterThan(4));Assert.That(productionGraphics.Any(graphic=>!graphic.canvasRenderer.cull),Is.True);
            var tabs=Object.FindFirstObjectByType<DecorationModeTabsView>();Assert.That(tabs.RequestMode(DecorationModeKind.Floor),Is.True);yield return null;
            Assert.That(productionCatalogue.CategoryRows,Has.Count.EqualTo(1));Assert.That(productionCatalogue.CategoryRows[0].HorizontalScroll.content.childCount,Is.EqualTo(3));
            Assert.That(tabs.RequestMode(DecorationModeKind.Wall),Is.True);yield return null;
            Assert.That(productionCatalogue.CategoryRows,Has.Count.EqualTo(3));Assert.That(productionCatalogue.CategoryRows.Select(row=>row.HorizontalScroll.content.childCount),Is.EqualTo(new[]{2,3,3}));
            Assert.That(productionCatalogue.GetComponentsInChildren<DecorationCatalogueTileView>(true).All(tile=>tile.transform.Find("UsingCheck")!=null&&tile.transform.Find("PreviewOutline")!=null),Is.True);
            var tabButtons=tabs.GetComponentsInChildren<UnityEngine.UI.Button>(true).Where(button=>button.transform.parent==tabs.transform).ToArray();
            var wallTab=tabButtons.Single(button=>button.name=="wallButton");var wallBaseline=tabButtons.Where(button=>button!=wallTab).Max(button=>((RectTransform)button.transform).anchoredPosition.y);
            Assert.That(((RectTransform)wallTab.transform).anchoredPosition.y,Is.GreaterThan(wallBaseline));Assert.That(wallTab.transform.GetSiblingIndex(),Is.EqualTo(tabs.transform.childCount-1));
            Assert.That(tabButtons.All(button=>button.image!=null&&button.image.raycastTarget&&button.interactable),Is.True);
            Assert.That(tabs.RequestMode(DecorationModeKind.Floor),Is.True);yield return null;var floorTab=tabButtons.Single(button=>button.name=="floorButton");
            Assert.That(((RectTransform)floorTab.transform).anchoredPosition.y,Is.GreaterThan(tabButtons.Where(button=>button!=floorTab).Max(button=>((RectTransform)button.transform).anchoredPosition.y)));Assert.That(((RectTransform)wallTab.transform).anchoredPosition.y,Is.EqualTo(wallBaseline));
            Assert.That(tabs.RequestMode(DecorationModeKind.Wall),Is.True);yield return null;
            var layoutRuntime=Object.FindFirstObjectByType<CafeLayoutRuntime>();
            layoutRuntime.RoomSurfaceLayout.ReplaceWall(new WallAppearance("wall.back-left","wallpaper.cream-floral","wainscoting.warm-white-rail"));
            layoutRuntime.RoomSurfaceLayout.ReplaceWall(new WallAppearance("wall.back-right","paint.sage",null));
            foreach(var view in Object.FindObjectsByType<WallSurfaceView>(FindObjectsSortMode.None))view.RenderConfirmed(layoutRuntime.RoomSurfaceLayout);
            var wallViews=Object.FindObjectsByType<WallSurfaceView>(FindObjectsSortMode.None).OrderBy(view=>view.SurfaceId).ToArray();
            Assert.That(wallViews,Has.Length.EqualTo(2));
            Assert.That(wallViews[0].transform.Find("Phase7_WainscotingFinish").GetComponent<Renderer>().enabled,Is.True);
            Assert.That(wallViews[0].transform.Find("Phase7_WainscotingRailLip").GetComponent<Renderer>().enabled,Is.True);
            Assert.That(wallViews[0].transform.Find("Phase7_WainscotingBaseboardLip").GetComponent<Renderer>().enabled,Is.True);
            Assert.That(wallViews[1].transform.Find("Phase7_WainscotingFinish").GetComponent<Renderer>().enabled,Is.False);
            Assert.That(wallViews[1].transform.Find("Phase7_WainscotingRailLip").GetComponent<Renderer>().enabled,Is.False);
            Assert.That(wallViews[1].transform.Find("Phase7_WainscotingBaseboardLip").GetComponent<Renderer>().enabled,Is.False);

            Assert.That(tabs.RequestMode(DecorationModeKind.WallDecor),Is.True);
            yield return null;
            Assert.That(productionCatalogue.CategoryRows.Select(row=>row.HorizontalScroll.content.childCount),
                Is.EqualTo(new[]{3,2}));
            var mountedTiles=productionCatalogue.CategoryRows
                .SelectMany(row=>row.HorizontalScroll.content.GetComponentsInChildren<DecorationCatalogueTileView>(false))
                .ToArray();
            Assert.That(mountedTiles,Has.Length.EqualTo(5));
            Assert.That(mountedTiles.All(tile=>
                ((UnityEngine.UI.Image)new SerializedObject(tile).FindProperty("thumbnailImage").objectReferenceValue).sprite!=null),Is.True);
            Assert.That(mountedTiles.Select(tile=>
                ((TMPro.TMP_Text)new SerializedObject(tile).FindProperty("nameLabel").objectReferenceValue).text),
                Is.EqualTo(new[]{"Monitor","Shiba Painting","Wood Shelf","Tall Glass Window","Tall Glass Window 1x2"}));
            foreach(var tile in mountedTiles)
            {
                var so=new SerializedObject(tile);
                var thumbnail=(RectTransform)((UnityEngine.UI.Image)so.FindProperty("thumbnailImage").objectReferenceValue).transform;
                var label=(RectTransform)((TMPro.TMP_Text)so.FindProperty("nameLabel").objectReferenceValue).transform;
                Assert.That(thumbnail.anchorMin.y,Is.GreaterThanOrEqualTo(.2f));
                Assert.That(label.anchorMax.y,Is.LessThanOrEqualTo(.25f));
            }

            var camera=Object.FindObjectsByType<UnityEngine.Camera>(FindObjectsSortMode.None)
                .First(candidate=>candidate.enabled);
            var evidenceRoot=new GameObject("Task10IsolatedFormalGallery");var evidenceEntries=new[]{"Assets/Art/Phase7/Catalogues/WMC_Phase7Production.asset","Assets/Art/Phase7/Catalogues/WMC_Phase7Windows.asset"}.SelectMany(path=>AssetDatabase.LoadAssetAtPath<WallMountedCatalogueAsset>(path).Entries).ToArray();for(var i=0;i<evidenceEntries.Length;i++){var instance=Object.Instantiate(evidenceEntries[i].Prefab,evidenceRoot.transform);instance.name=evidenceEntries[i].DefinitionId;instance.transform.position=new Vector3(-3.6f+i*1.8f,0f,0f);instance.transform.rotation=Quaternion.Euler(0f,180f,0f);foreach(var child in instance.GetComponentsInChildren<Transform>(true))child.gameObject.layer=31;}
            var evidenceCameraObject=new GameObject("Task10EvidenceCamera");var evidenceCamera=evidenceCameraObject.AddComponent<UnityEngine.Camera>();evidenceCamera.cullingMask=1<<31;evidenceCamera.orthographic=true;evidenceCamera.orthographicSize=2.4f;evidenceCamera.clearFlags=CameraClearFlags.SolidColor;evidenceCamera.backgroundColor=new Color(.16f,.18f,.2f,1f);evidenceCamera.transform.position=new Vector3(0f,1f,-10f);evidenceCamera.transform.LookAt(new Vector3(0f,1f,0f));
            var target=new RenderTexture(640,360,24,RenderTextureFormat.ARGB32);
            var pixels=new Texture2D(640,360,TextureFormat.RGBA32,false);
            evidenceCamera.targetTexture=target;evidenceCamera.Render();RenderTexture.active=target;
            pixels.ReadPixels(new Rect(0,0,640,360),0,0);pixels.Apply();
            var output="outputs/phase7-task10/Phase7FormalMounted_SideBySide_Technical.png";
            Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllBytes(output,pixels.EncodeToPNG());
            evidenceCamera.targetTexture=null;RenderTexture.active=null;Object.Destroy(target);Object.Destroy(pixels);Object.Destroy(evidenceCameraObject);Object.Destroy(evidenceRoot);
            var screenCanvas=GameObject.Find("Screen Canvas").GetComponent<Canvas>();
            screenCanvas.renderMode=RenderMode.ScreenSpaceCamera;screenCanvas.worldCamera=camera;screenCanvas.planeDistance=1f;
            var catalogueRect=productionCatalogue.GetComponent<RectTransform>();catalogueRect.SetAsLastSibling();catalogueRect.anchorMin=new Vector2(0f,0f);catalogueRect.anchorMax=new Vector2(1f,0f);catalogueRect.pivot=new Vector2(.5f,0f);catalogueRect.anchoredPosition=Vector2.zero;catalogueRect.sizeDelta=new Vector2(0f,520f);
            var expandedRect=productionCatalogue.GetComponentsInChildren<RectTransform>(true).Single(item=>item.name=="ExpandedSheet");expandedRect.anchorMin=Vector2.zero;expandedRect.anchorMax=Vector2.one;expandedRect.offsetMin=Vector2.zero;expandedRect.offsetMax=Vector2.zero;
            var evidenceCanvas=productionCatalogue.gameObject.AddComponent<Canvas>();evidenceCanvas.overrideSorting=true;evidenceCanvas.sortingOrder=100;evidenceCanvas.renderMode=RenderMode.ScreenSpaceCamera;evidenceCanvas.worldCamera=camera;evidenceCanvas.planeDistance=.5f;
            Canvas.ForceUpdateCanvases();
            target=new RenderTexture(540,960,24,RenderTextureFormat.ARGB32);pixels=new Texture2D(540,960,TextureFormat.RGBA32,false);
            camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,540,960),0,0);pixels.Apply();
            var uiOutput="outputs/phase7-task10/Phase7FormalMounted_Catalogue_Technical.png";
            File.WriteAllBytes(uiOutput,pixels.EncodeToPNG());
            camera.targetTexture=null;RenderTexture.active=null;Object.Destroy(target);Object.Destroy(pixels);
            Assert.That(new FileInfo(output).Length,Is.GreaterThan(1024));
            Assert.That(new FileInfo(uiOutput).Length,Is.GreaterThan(1024));
            controller.ExitDecorationMode();yield return null;
        }
    }
}
#endif
