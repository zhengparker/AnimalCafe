using System;
using System.Linq;
using AnimalCafe.Content;
using AnimalCafe.Decoration;
using AnimalCafe.EditorTools.Phase6;
using AnimalCafe.UI.Decoration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.IO;
using System.Text.RegularExpressions;

namespace AnimalCafe.EditorTools.Phase7
{
    public static class Phase7DecorationSceneSetup
    {
        [MenuItem("AnimalCafe/Phase 7/Configure Validation Scene")]
        public static void ConfigureValidationScene()
        {
            PreserveDependencyAssets(Phase6DecorationSceneSetup.ConfigureValidationScene);
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(Phase7AssetPaths.ValidationScenePath)==null)
            {
                const string source="Assets/Scenes/Validation/Phase6DecorationMode.unity";
                var sourceScene=EditorSceneManager.OpenScene(source,OpenSceneMode.Additive);
                try{EditorSceneManager.SaveScene(sourceScene,Phase7AssetPaths.ValidationScenePath,true);}
                finally{if(sourceScene.IsValid()&&sourceScene.isLoaded)EditorSceneManager.CloseScene(sourceScene,true);}
            }
            ConfigureScene(Phase7AssetPaths.ValidationScenePath);
            ExcludeValidationSceneFromBuild();
        }

        [MenuItem("AnimalCafe/Phase 7/Migrate MainCafe")]
        public static void MigrateMainCafe()
        {
            if(!SceneContains<DecorationModeController>(Phase7AssetPaths.MainCafeScenePath))
                Phase6DecorationSceneSetup.ConfigureMainCafe();
            ConfigureScene(Phase7AssetPaths.MainCafeScenePath);
            EnsureMainCafeBuildEntry();
        }

        private static void ConfigureScene(string path)
        {
            var existing=Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt)
                .FirstOrDefault(scene=>scene.path==path);
            var opened=!existing.IsValid();
            var scene=opened?EditorSceneManager.OpenScene(path,OpenSceneMode.Additive):existing;
            try
            {
                var controller=FindAll<DecorationModeController>(scene).Single();
                var authoring=FindAll<WallSurfaceAuthoring>(scene).OrderBy(x=>x.SurfaceId,StringComparer.Ordinal).ToArray();
                if(authoring.Length!=2)throw new InvalidOperationException("Phase 7 requires exactly two canonical Wall Surface authoring components.");
                foreach(var stale in controller.GetComponents<WallSurfaceRegistry>())UnityEngine.Object.DestroyImmediate(stale);
                foreach(var stale in controller.GetComponents<WallMountedSceneRegistry>())UnityEngine.Object.DestroyImmediate(stale);
                foreach(var stale in controller.GetComponents<FloorSurfaceGridView>())UnityEngine.Object.DestroyImmediate(stale);
                foreach(var stale in controller.GetComponents<WallMountedPreviewView>())UnityEngine.Object.DestroyImmediate(stale);
                foreach(var wall in authoring)
                    foreach(var stale in wall.GetComponents<WallSurfaceView>())UnityEngine.Object.DestroyImmediate(stale);
                var roots=scene.GetRootGameObjects().Where(root=>root.name=="Phase7_InteriorRuntime").ToArray();
                var host=roots.FirstOrDefault();
                if(host==null)host=new GameObject("Phase7_InteriorRuntime");
                SceneManager.MoveGameObjectToScene(host,scene);
                for(var i=1;i<roots.Length;i++)UnityEngine.Object.DestroyImmediate(roots[i]);
                host.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);host.transform.localScale=Vector3.one;
                var wallRegistry=EnsureSingle<WallSurfaceRegistry>(host);
                var mountedRegistry=EnsureSingle<WallMountedSceneRegistry>(host);
                var floorView=EnsureSingle<FloorSurfaceGridView>(host);
                var projection=EnsureSingle<WallMountedPreviewView>(host);
                var fade=EnsureSingle<WallOcclusionFadeView>(host);

                var uiRoots=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<Transform>(true))
                    .Where(transform=>transform.name=="Phase7_UIRuntime").Select(transform=>transform.gameObject).ToArray();
                var uiRoot=uiRoots.FirstOrDefault();
                if(uiRoot==null)uiRoot=new GameObject("Phase7_UIRuntime",typeof(RectTransform));
                for(var i=1;i<uiRoots.Length;i++)UnityEngine.Object.DestroyImmediate(uiRoots[i]);
                foreach(var extra in uiRoot.GetComponents<GraphicRaycaster>())UnityEngine.Object.DestroyImmediate(extra);
                foreach(var extra in uiRoot.GetComponents<CanvasScaler>())UnityEngine.Object.DestroyImmediate(extra);
                foreach(var extra in uiRoot.GetComponents<Canvas>())UnityEngine.Object.DestroyImmediate(extra);
                var screenCanvas=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<Transform>(true))
                    .Single(transform=>transform.name=="Screen Canvas");
                var phase7Catalogue=EnsurePhase7UiPrefab<DecorationCatalogueView>(scene,controller,screenCanvas,Phase7AssetPaths.CataloguePrefabPath,"catalogueView","PF_UI_DecorationCatalogue");
                var phase7ActionBar=EnsurePhase7UiPrefab<DecorationActionBarView>(scene,controller,screenCanvas,Phase7AssetPaths.ActionBarPrefabPath,"actionBarView","PF_UI_DecorationActionBar");
                if(phase7Catalogue.SurfaceFooterHost==null)
                    throw new InvalidOperationException("Phase7 Catalogue prefab requires SurfaceFooterHost.");
                uiRoot.transform.SetParent(screenCanvas,false);
                uiRoot.transform.localScale=Vector3.one;
                var uiRect=(RectTransform)uiRoot.transform;uiRect.anchorMin=new Vector2(0f,0f);uiRect.anchorMax=new Vector2(1f,0f);uiRect.pivot=new Vector2(.5f,0f);uiRect.anchoredPosition=Vector2.zero;uiRect.sizeDelta=new Vector2(0f,190f);
                foreach(var staleTabs in uiRoot.GetComponentsInChildren<DecorationModeTabsView>(true))
                    UnityEngine.Object.DestroyImmediate(staleTabs.gameObject);
                var tabs=EnsureTabs(phase7Catalogue.transform);
                var range=EnsureFloorRange(scene,phase7Catalogue.SurfaceFooterHost);
                var exit=EnsureExitModal(scene,screenCanvas);
                LayoutButtons(range.transform,new[]{"WholeRoomButton","SingleGridButton"},-70f,140f,96f);
                StyleFloorRangeButtons(range);
                Phase7SurfaceAssetBuilder.LayoutExitModal(exit);
                if(path==Phase7AssetPaths.ValidationScenePath){EnsureValidationFixtures(host.transform);EnsureFormalGallery(host.transform);}
                else RemoveValidationFixtures(host.transform);
                var so=new SerializedObject(controller);
                SetRef(so,"floorStyleCatalogue",Phase7AssetPaths.FloorCataloguePath);
                SetRef(so,"wallpaperStyleCatalogue",Phase7AssetPaths.WallpaperCataloguePath);
                SetRef(so,"paintStyleCatalogue",Phase7AssetPaths.PaintCataloguePath);
                SetRef(so,"wainscotingStyleCatalogue",Phase7AssetPaths.WainscotingCataloguePath);
                SetRef(so,"wallDecorCatalogue",Phase7AssetPaths.WallMountedProductionCataloguePath);
                SetRef(so,"windowCatalogue",Phase7AssetPaths.WindowCataloguePath);
                so.FindProperty("catalogueView").objectReferenceValue=phase7Catalogue;
                so.FindProperty("actionBarView").objectReferenceValue=phase7ActionBar;
                so.FindProperty("wallSurfaceRegistry").objectReferenceValue=wallRegistry;
                so.FindProperty("wallMountedSceneRegistry").objectReferenceValue=mountedRegistry;
                so.FindProperty("floorSurfaceGridView").objectReferenceValue=floorView;
                so.FindProperty("wallMountedProjectionView").objectReferenceValue=projection;
                so.FindProperty("wallOcclusionFadeView").objectReferenceValue=fade;
                so.FindProperty("projectionValidMaterial").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Material>(Phase7AssetPaths.ProjectionValidMaterialPath);
                so.FindProperty("projectionInvalidMaterial").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Material>(Phase7AssetPaths.ProjectionInvalidMaterialPath);
                so.FindProperty("modeTabsView").objectReferenceValue=tabs;
                so.FindProperty("floorRangeView").objectReferenceValue=range;
                so.FindProperty("exitModalView").objectReferenceValue=exit;
                var walls=so.FindProperty("phase7WallAuthoring");walls.arraySize=2;
                for(var i=0;i<2;i++)walls.GetArrayElementAtIndex(i).objectReferenceValue=authoring[i];
                var seedObject=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<Transform>(true))
                    .Single(transform=>transform.name=="P4_Window_BackRight_C3_R0").gameObject;
                foreach(var staleSeed in seedObject.GetComponents<WallMountedSeedAuthoring>())
                    UnityEngine.Object.DestroyImmediate(staleSeed);
                // Keep the completed Phase 4 prefab instance as a reversible scene dependency,
                // but do not present it as a pre-placed Phase 7 decoration in MainCafe.
                if(path==Phase7AssetPaths.MainCafeScenePath)seedObject.SetActive(false);
                var seeds=so.FindProperty("phase7MountedSeeds");seeds.arraySize=0;
                so.ApplyModifiedPropertiesWithoutUndo();
                var fadeSo=new SerializedObject(fade);
                fadeSo.FindProperty("viewCamera").objectReferenceValue=FindAll<UnityEngine.Camera>(scene).Single(camera=>camera.CompareTag("MainCamera"));
                fadeSo.FindProperty("fadeMaterialTemplate").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Material>(Phase7AssetPaths.OcclusionFadeMaterialPath);
                fadeSo.FindProperty("fadeOpacity").floatValue=.35f;
                fadeSo.ApplyModifiedPropertiesWithoutUndo();
                // Canonical Phase 4 Wall prefab instances remain component-exact.
                // Wall blockers may opt into OcclusionFadeRepresentationRoot on their own
                // representation roots; the selected Wall target resolves to its Renderer.
                foreach(var wall in authoring)
                    foreach(var staleMarker in wall.GetComponents<OcclusionFadeRepresentationRoot>())
                        UnityEngine.Object.DestroyImmediate(staleMarker);
                EnsureDimensionalWallVisuals(authoring, host.transform);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene,path);
            }
            finally{if(opened&&scene.IsValid()&&scene.isLoaded)EditorSceneManager.CloseScene(scene,true);}
            NormalizeSerializedWhitespace(path);
        }

        private static void SetRef(SerializedObject so,string property,string path)
        { var p=so.FindProperty(property)??throw new InvalidOperationException(property);p.objectReferenceValue=AssetDatabase.LoadMainAssetAtPath(path)??throw new InvalidOperationException("Missing asset: "+path); }
        private static T EnsureSingle<T>(GameObject root) where T:Component
        { var all=root.GetComponents<T>();for(var i=1;i<all.Length;i++)UnityEngine.Object.DestroyImmediate(all[i]);return all.FirstOrDefault()??root.AddComponent<T>(); }
        private static Transform FindChild(Transform root,string name)=>root.GetComponentsInChildren<Transform>(true).FirstOrDefault(x=>x.name==name);
        private static DecorationModeTabsView EnsureTabs(Transform parent)
        {
            var view=parent.GetComponentInChildren<DecorationModeTabsView>(true);
            if(view==null){var root=new GameObject("ModeTabs",typeof(RectTransform),typeof(DecorationModeTabsView));root.transform.SetParent(parent,false);view=root.GetComponent<DecorationModeTabsView>();}
            Phase7SurfaceAssetBuilder.LayoutModeTabs(view);return view;
        }
        private static DecorationFloorRangeView EnsureFloorRange(Scene scene,Transform parent)
        {
            var views=FindAll<DecorationFloorRangeView>(scene);var view=views.FirstOrDefault();
            for(var i=1;i<views.Length;i++)UnityEngine.Object.DestroyImmediate(views[i].gameObject);
            if(view==null){var root=new GameObject("FloorRange",typeof(RectTransform),typeof(DecorationFloorRangeView));root.transform.SetParent(parent,false);view=root.GetComponent<DecorationFloorRangeView>();}
            else if(view.transform.parent!=parent)view.transform.SetParent(parent,false);
            var whole=FindChild(view.transform,"WholeRoomButton")?.GetComponent<Button>()??Phase7SurfaceAssetBuilder.CreateButton(view.transform,"WholeRoomButton");
            var single=FindChild(view.transform,"SingleGridButton")?.GetComponent<Button>()??Phase7SurfaceAssetBuilder.CreateButton(view.transform,"SingleGridButton");view.Configure(whole,single);return view;
        }
        private static DecorationExitModalView EnsureExitModal(Scene scene,Transform parent)
        {
            var views=FindAll<DecorationExitModalView>(scene).ToArray();var view=views.FirstOrDefault();
            for(var i=1;i<views.Length;i++)UnityEngine.Object.DestroyImmediate(views[i].gameObject);
            if(view==null)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Phase7AssetPaths.ExitModalPrefabPath)
                    ??throw new InvalidOperationException("Missing Phase 7 Exit Modal prefab.");
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
                view=instance.GetComponent<DecorationExitModalView>();
            }
            if(view.transform.parent!=parent)view.transform.SetParent(parent,false);
            view.transform.SetAsLastSibling();
            return view;
        }
        private static void LayoutButtons(Transform root,string[] names,float startX,float step,float y)
        {
            var rootRect=(RectTransform)root;rootRect.anchorMin=Vector2.zero;rootRect.anchorMax=Vector2.one;rootRect.offsetMin=Vector2.zero;rootRect.offsetMax=Vector2.zero;rootRect.localScale=Vector3.one;
            for(var i=0;i<names.Length;i++)
            {
                var button=FindChild(root,names[i])?.GetComponent<Button>();if(button==null)continue;
                Phase7SurfaceAssetBuilder.EnsureButtonLabel(button);
                var rect=(RectTransform)button.transform;rect.anchorMin=new Vector2(.5f,0f);rect.anchorMax=new Vector2(.5f,0f);rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=new Vector2(startX+i*step,y);rect.sizeDelta=new Vector2(132f,52f);rect.localScale=Vector3.one;
                var shadow=button.GetComponent<Shadow>()??button.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(.12f,.10f,.08f,.28f);shadow.effectDistance=new Vector2(0f,-4f);shadow.useGraphicAlpha=true;
            }
        }
        private static void StyleFloorRangeButtons(DecorationFloorRangeView range)
        {
            var roundedSprite=AssetDatabase.LoadAssetAtPath<Sprite>(Phase7AssetPaths.RoundedCatalogueCardSpritePath)
                ??throw new InvalidOperationException("Missing Phase 7 rounded button sprite.");
            foreach(var button in range.GetComponentsInChildren<Button>(true))
            {
                Phase7SurfaceAssetBuilder.EnsureButtonLabel(button);
                button.image.sprite=roundedSprite;
                button.image.type=Image.Type.Sliced;
                button.image.color=Color.white;
                var colors=button.colors;
                colors.normalColor=new Color(1f,.91f,.72f,1f);
                colors.highlightedColor=new Color(1f,.94f,.80f,1f);
                colors.pressedColor=new Color(.91f,.80f,.61f,1f);
                // EventSystem focus is not the accepted floor range. Keep focus warm;
                // DecorationFloorRangeView uses the disabled colour for the true active range.
                colors.selectedColor=colors.highlightedColor;
                colors.disabledColor=new Color(.28f,.43f,.31f,1f);
                colors.colorMultiplier=1f;
                colors.fadeDuration=.1f;
                button.colors=colors;
            }
        }
        private static T EnsurePhase7UiPrefab<T>(Scene scene,DecorationModeController controller,Transform fallbackParent,string prefabPath,string controllerProperty,string instanceName) where T:Component
        {
            var property=new SerializedObject(controller).FindProperty(controllerProperty);var current=property.objectReferenceValue as T;
            if(current!=null&&AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(current))==prefabPath){current.gameObject.SetActive(true);return current;}
            var parent=current!=null?current.transform.parent:fallbackParent;var sibling=current!=null?current.transform.GetSiblingIndex():parent.childCount;
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)??throw new InvalidOperationException("Missing Phase7 UI prefab: "+prefabPath);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);instance.name=instanceName;instance.transform.SetParent(parent,false);instance.transform.SetSiblingIndex(sibling);
            if(current!=null){instance.SetActive(current.gameObject.activeSelf);CopyRect(current.transform as RectTransform,instance.transform as RectTransform);}
            var result=instance.GetComponentInChildren<T>(true)??throw new InvalidOperationException(prefabPath+" missing "+typeof(T).Name);
            if(current!=null)UnityEngine.Object.DestroyImmediate(current.gameObject);return result;
        }
        private static void CopyRect(RectTransform source,RectTransform destination)
        {if(source==null||destination==null)return;destination.anchorMin=source.anchorMin;destination.anchorMax=source.anchorMax;destination.pivot=source.pivot;destination.anchoredPosition3D=source.anchoredPosition3D;destination.sizeDelta=source.sizeDelta;destination.localRotation=source.localRotation;destination.localScale=source.localScale;}
        private static void EnsureValidationFixtures(Transform host)
        {
            EnsureFixture(host,"TEST_ONLY_WallFixture_2x2",new Vector3(2f,2f,.08f),new Vector3(-3f,1f,1f));
            EnsureFixture(host,"TEST_ONLY_WallFixture_3x2",new Vector3(3f,2f,.08f),new Vector3(3f,1f,1f));
        }

        private static void EnsureDimensionalWallVisuals(
            WallSurfaceAuthoring[] walls,
            Transform phase7Host)
        {
            var bodyMaterial = AssetDatabase.LoadAssetAtPath<Material>(Phase7AssetPaths.WallBodyMaterialPath)
                ?? throw new InvalidOperationException("Missing dimensional Wall body Material.");
            var cornerMaterial = AssetDatabase.LoadAssetAtPath<Material>(Phase7AssetPaths.WallCornerMaterialPath)
                ?? throw new InvalidOperationException("Missing dimensional Wall corner Material.");
            foreach (var wall in walls)
            {
                var body = wall.transform.Find("WallVisual")
                    ?? throw new InvalidOperationException(wall.SurfaceId + " missing canonical WallVisual.");
                var bodyRenderer = body.GetComponent<Renderer>()
                    ?? throw new InvalidOperationException(wall.SurfaceId + " WallVisual missing Renderer.");
                bodyRenderer.sharedMaterial = bodyMaterial;
                ConfigureWallRenderer(bodyRenderer, true);

                const float finishDepth = .012f;
                const float wainscotingDepth = .014f;
                var front = body.localPosition.z - body.localScale.z * .5f;
                var finish = EnsureRenderOnlyCube(
                    wall.transform,
                    "Phase7_WallFinish",
                    new Vector3(body.localPosition.x, body.localPosition.y,
                        front - finishDepth * .5f - .003f),
                    new Vector3(body.localScale.x, body.localScale.y, finishDepth),
                    bodyMaterial);
                ConfigureWallRenderer(finish.GetComponent<Renderer>(), false);
                var waistHeight = CharacterScaleReference.SharedCharacterWaistHeightMeters;
                var bottom = body.localPosition.y - body.localScale.y * .5f;
                var wainscoting = EnsureRenderOnlyCube(
                    wall.transform,
                    "Phase7_WainscotingFinish",
                    new Vector3(body.localPosition.x, bottom + waistHeight * .5f,
                        finish.localPosition.z - finishDepth * .5f - wainscotingDepth * .5f - .004f),
                    new Vector3(body.localScale.x, waistHeight, wainscotingDepth),
                    bodyMaterial);
                ConfigureWallRenderer(wainscoting.GetComponent<Renderer>(), false);
                const float railDepth = .04f;
                const float baseboardDepth = .032f;
                var rail = EnsureRenderOnlyCube(
                    wall.transform,
                    "Phase7_WainscotingRailLip",
                    new Vector3(body.localPosition.x, bottom + waistHeight - .005f,
                        wainscoting.localPosition.z - wainscotingDepth * .5f - railDepth * .5f - .003f),
                    new Vector3(body.localScale.x, .055f, railDepth),
                    bodyMaterial);
                var baseboard = EnsureRenderOnlyCube(
                    wall.transform,
                    "Phase7_WainscotingBaseboardLip",
                    new Vector3(body.localPosition.x, bottom + .045f,
                        wainscoting.localPosition.z - wainscotingDepth * .5f - baseboardDepth * .5f - .003f),
                    new Vector3(body.localScale.x, .09f, baseboardDepth),
                    bodyMaterial);
                ConfigureWallRenderer(rail.GetComponent<Renderer>(), false);
                ConfigureWallRenderer(baseboard.GetComponent<Renderer>(), false);
                rail.GetComponent<Renderer>().enabled = false;
                baseboard.GetComponent<Renderer>().enabled = false;
                EditorUtility.SetDirty(bodyRenderer);
            }

            var backLeft = walls.Single(wall => wall.SurfaceId == "wall.back-left");
            var backRight = walls.Single(wall => wall.SurfaceId == "wall.back-right");
            var leftBody = backLeft.transform.Find("WallVisual").GetComponent<Renderer>();
            var rightBody = backRight.transform.Find("WallVisual").GetComponent<Renderer>();
            var lower = Mathf.Max(leftBody.bounds.min.y, rightBody.bounds.min.y);
            var upper = Mathf.Min(leftBody.bounds.max.y, rightBody.bounds.max.y);
            var outward = (-backLeft.transform.forward - backRight.transform.forward).normalized;
            var intersection = new Vector3(
                rightBody.bounds.center.x,
                (lower + upper) * .5f,
                leftBody.bounds.center.z);
            var corner = EnsureRenderOnlyCube(
                phase7Host,
                "Phase7_InteriorCornerDepth",
                phase7Host.InverseTransformPoint(intersection + outward * .078f),
                new Vector3(.055f, upper - lower, .055f),
                cornerMaterial);
            corner.localRotation = Quaternion.identity;
            ConfigureWallRenderer(corner.GetComponent<Renderer>(), true);
            EnsureWallFillLight(phase7Host);
        }

        private static void ConfigureWallRenderer(Renderer renderer, bool castsShadow)
        {
            renderer.shadowCastingMode = castsShadow
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.renderingLayerMask = 3u;
            EditorUtility.SetDirty(renderer);
        }

        private static void EnsureWallFillLight(Transform parent)
        {
            var root = parent.Find("Phase7_WallFillLight")?.gameObject;
            if (root == null)
            {
                root = new GameObject("Phase7_WallFillLight", typeof(Light));
                root.transform.SetParent(parent, false);
            }

            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.Euler(20f, 45f, 0f);
            root.transform.localScale = Vector3.one;
            var light = root.GetComponent<Light>() ?? root.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, .93f, .82f, 1f);
            light.intensity = .45f;
            light.shadows = LightShadows.None;
            light.renderingLayerMask = 2;
            EditorUtility.SetDirty(root);
            EditorUtility.SetDirty(light);
        }

        private static Transform EnsureRenderOnlyCube(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            var visual = parent.Find(name)?.gameObject;
            if (visual == null)
            {
                visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.name = name;
                visual.transform.SetParent(parent, false);
            }

            visual.transform.localPosition = localPosition;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = localScale;
            foreach (var collider in visual.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(collider);
            foreach (var obstacle in visual.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true))
                UnityEngine.Object.DestroyImmediate(obstacle);
            foreach (var body in visual.GetComponentsInChildren<Rigidbody>(true))
                UnityEngine.Object.DestroyImmediate(body);
            foreach (var handler in visual.GetComponentsInChildren<MonoBehaviour>(true)
                         .Where(component => component is IEventSystemHandler))
                UnityEngine.Object.DestroyImmediate(handler);
            var renderer = visual.GetComponent<Renderer>()
                ?? throw new InvalidOperationException(name + " missing Renderer.");
            renderer.sharedMaterial = material;
            renderer.receiveShadows = true;
            EditorUtility.SetDirty(visual);
            return visual.transform;
        }
        private static void EnsureFixture(Transform host,string name,Vector3 scale,Vector3 position)
        {
            var existing=FindChild(host,name)?.gameObject;
            if(existing==null){existing=GameObject.CreatePrimitive(PrimitiveType.Cube);existing.name=name;existing.transform.SetParent(host,false);}
            existing.transform.localPosition=position;existing.transform.localRotation=Quaternion.identity;existing.transform.localScale=scale;
            var collider=existing.GetComponent<Collider>();if(collider!=null)collider.isTrigger=true;
        }
        private static void EnsureFormalGallery(Transform host)
        {
            foreach(var stale in host.GetComponentsInChildren<Transform>(true).Where(x=>x!=host&&x.name.StartsWith("TEST_ONLY_Placeholder_",StringComparison.Ordinal)).ToArray())UnityEngine.Object.DestroyImmediate(stale.gameObject);
            var entries=new[]{Phase7AssetPaths.WallMountedProductionCataloguePath,Phase7AssetPaths.WindowCataloguePath}.SelectMany(path=>AssetDatabase.LoadAssetAtPath<WallMountedCatalogueAsset>(path).Entries).ToArray();
            for(var i=0;i<entries.Length;i++)
            {
                var entry=entries[i];var name="TECH_REVIEW_Formal_"+entry.DefinitionId;
                var item=FindChild(host,name)?.gameObject;
                if(item==null){item=(GameObject)PrefabUtility.InstantiatePrefab(entry.Prefab);item.name=name;item.transform.SetParent(host,false);}
                item.transform.localPosition=new Vector3(-4f+i*2f,.15f,-2f);item.transform.localRotation=Quaternion.Euler(0f,180f,0f);item.transform.localScale=Vector3.one;
            }
        }
        private static void RemoveValidationFixtures(Transform host)
        {
            foreach(var name in new[]{"TEST_ONLY_WallFixture_2x2","TEST_ONLY_WallFixture_3x2"}){var fixture=FindChild(host,name);if(fixture!=null)UnityEngine.Object.DestroyImmediate(fixture.gameObject);}
        }
        private static T[] FindAll<T>(Scene scene) where T:Component=>scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<T>(true)).ToArray();
        private static bool SceneContains<T>(string path) where T:Component
        { var existing=Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).FirstOrDefault(x=>x.path==path);var opened=!existing.IsValid();var scene=opened?EditorSceneManager.OpenScene(path,OpenSceneMode.Additive):existing;try{return FindAll<T>(scene).Length>0;}finally{if(opened&&scene.IsValid()&&scene.isLoaded)EditorSceneManager.CloseScene(scene,true);} }
        private static void ExcludeValidationSceneFromBuild()=>EditorBuildSettings.scenes=EditorBuildSettings.scenes.Where(x=>x.path!=Phase7AssetPaths.ValidationScenePath).ToArray();
        private static void EnsureMainCafeBuildEntry()
        { var list=EditorBuildSettings.scenes.Where(x=>x.path!=Phase7AssetPaths.MainCafeScenePath&&x.path!=Phase7AssetPaths.ValidationScenePath).ToList();list.Insert(0,new EditorBuildSettingsScene(Phase7AssetPaths.MainCafeScenePath,true));EditorBuildSettings.scenes=list.ToArray(); }
        private static void NormalizeSerializedWhitespace(string path)
        {
            var original=File.ReadAllText(path);var normalized=Regex.Replace(original,@"[ \t]+(?=\r?$)",string.Empty,RegexOptions.Multiline);
            if(normalized==original)return;File.WriteAllText(path,normalized,new System.Text.UTF8Encoding(false));AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        }
        private static void PreserveDependencyAssets(Action operation)
        {
            var paths=new[]{"Assets/Settings/UniversalRenderPipelineGlobalSettings.asset","Assets/Art/Phase4/Environment/Materials/M_Environment_Entrance_01.mat"};var snapshots=paths.ToDictionary(path=>path,path=>File.ReadAllBytes(path),StringComparer.Ordinal);
            try{operation();}
            finally{foreach(var path in paths)if(!File.ReadAllBytes(path).SequenceEqual(snapshots[path])){File.WriteAllBytes(path,snapshots[path]);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);}}
        }
    }
}
