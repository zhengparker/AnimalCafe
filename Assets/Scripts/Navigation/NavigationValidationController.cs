using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using System.Linq;
using AnimalCafe.Content;
using AnimalCafe.Decoration;
using AnimalCafe.Layout;
using AnimalCafe.Core.Time;
using AnimalCafe.UI;
using AnimalCafe.UI.P8R;
using UnityEngine.UI;

namespace AnimalCafe.Navigation
{
    // Fixture requests only. Task 5/6 supplies confirmed-layout navigation and decoration integration.
    public sealed class NavigationValidationController : MonoBehaviour
    {
        [SerializeField] private NavigationWorld world;
        [SerializeField] private NavigationActor[] actors;
        [SerializeField] private BoxCollider floor;
        [SerializeField] private BoxCollider[] solids;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private DecorationModeController decorationController;
        [SerializeField] private CafeLayoutRuntime businessRuntime;
        [SerializeField] private FurnitureContentCatalog businessCatalog;
        [SerializeField] private NavigationDecorationBridge businessBridge;
        [SerializeField] private GameTimeService gameTimeService;
        [SerializeField] private UnityEngine.Camera fixtureCamera;
        [SerializeField] private AnimalCafe.Camera.CafeCameraController cameraController;
        [SerializeField] private AnimalCafe.Camera.CameraSettings cameraSettings;
        private AnimalCafe.Camera.CameraSettings ownedCameraSettings;
        private IDisposable startupBlock;
        private GameSpeed startupSpeed;
        private int startupChoiceVersion;
        private RectTransform validationDock;
        private RectTransform businessTimePanel;
        private AnimalCafe.UI.Feedback.ValidationMessageView businessReadiness;
        private Button[] fixtureButtons;
        private TMP_Text[] fixtureLabels;
        private static readonly string[] HudScenarios = {"straight","detour","crossing","narrow-wait","same-target","recovery","crowd8","blocked85"};
        private static readonly string[] HudActions = {"Pause","1x","2x","Cancel requests"};
        private readonly Vector3[] panelCorners = new Vector3[4];

        // Validation-only layout: keep diagnostics outside the playable viewport.
        // 复用现有控件；装修时隐藏诊断，保留 P8 的编辑界面。
        private void LateUpdate()
        {
            if (statusLabel == null || fixtureCamera == null || decorationController == null) return;
            if (validationDock == null)
            {
                validationDock = statusLabel.transform.parent as RectTransform;
                fixtureButtons = validationDock.GetComponentsInChildren<Button>(true);
                fixtureLabels = validationDock.GetComponentsInChildren<TMP_Text>(true)
                    .Where(t => t.transform.parent == validationDock && t != statusLabel && t.name != "NavigationResumeBlockReason").ToArray();
                businessTimePanel = FindObjectsByType<TimeControlPanel>(FindObjectsSortMode.None)
                    .Where(p => p.transform != validationDock).Select(p => p.transform as RectTransform).FirstOrDefault();
                businessReadiness = FindFirstObjectByType<AnimalCafe.UI.Feedback.ValidationMessageView>();
            }
            var decorating = decorationController.IsOpen;
            validationDock.gameObject.SetActive(!decorating);
            var canvas = validationDock.GetComponentInParent<Canvas>();
            var scale = canvas.scaleFactor;
            var width = ((RectTransform)canvas.transform).rect.width;
            var height = Mathf.Min(((RectTransform)canvas.transform).rect.height * .36f, 480);
            validationDock.anchorMin = validationDock.anchorMax = new Vector2(.5f, 0);
            validationDock.pivot = new Vector2(.5f, 0); validationDock.anchoredPosition = Vector2.zero;
            validationDock.sizeDelta = new Vector2(width, height);
            var column = (width - 30) / 4;
            for (var i = 0; i < fixtureButtons.Length; i++)
            {
                var button = fixtureButtons[i];
                var index = Array.IndexOf(HudScenarios, button.name);
                if (index >= 0) PlaceHud((RectTransform)button.transform, 10 + index % 4 * column, 42 + index / 4 * 44, column - 6, 38);
                else
                {
                    var timeIndex = Array.IndexOf(HudActions, button.name);
                    if (timeIndex >= 0) PlaceHud((RectTransform)button.transform, 10 + timeIndex * column, 132, column - 6, 38);
                }
                var label = button.GetComponentInChildren<TMP_Text>();
                if (label != null) { var rect = label.rectTransform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; label.fontSize = 19; }
            }
            if (fixtureLabels.Length > 0)
            { fixtureLabels[0].text = "Phase 11 Navigation"; PlaceHud(fixtureLabels[0].rectTransform, 10, 6, width - 20, 30); }
            if (fixtureLabels.Length > 1)
            { fixtureLabels[1].text = "Drag / zoom camera to follow crowd8. Decoration hides this panel."; PlaceHud(fixtureLabels[1].rectTransform, 10, 176, width - 20, 24); }
            PlaceHud(statusLabel.rectTransform, 10, 208, width - 20, height - 218);
            statusLabel.enableAutoSizing = true; statusLabel.fontSizeMin = 12; statusLabel.fontSizeMax = 18;
            var duplicate = validationDock.Find("NavigationResumeBlockReason");
            if (duplicate != null) duplicate.gameObject.SetActive(false);
            var topPixels = 8f;
            if (businessTimePanel != null)
            {
                businessTimePanel.GetWorldCorners(panelCorners);
                var timeBottom = panelCorners[0].y;
                var contentBottom = timeBottom;
                if (businessReadiness != null && businessReadiness.IsVisible)
                {
                    ((RectTransform)businessReadiness.transform).GetWorldCorners(panelCorners);
                    contentBottom = Mathf.Min(contentBottom, panelCorners[0].y);
                }
                topPixels = Screen.height - contentBottom + 8;
                var reason = businessTimePanel.Find("NavigationResumeBlockReason")?.GetComponent<TMP_Text>();
                if (reason != null && reason.gameObject.activeSelf)
                {
                    var metrics = P8RMobileMetrics.For(reason);
                    reason.fontSize = metrics.Units(14);
                    var rect = reason.rectTransform;
                    rect.anchoredPosition = new Vector2(0, -(timeBottom - contentBottom) / reason.canvas.scaleFactor - metrics.Units(4));
                    rect.sizeDelta = new Vector2(0, metrics.Units(36));
                    topPixels += metrics.Units(40) * reason.canvas.scaleFactor;
                }
            }
            var bottom = decorating ? 0 : (height * scale + 2) / Screen.height;
            fixtureCamera.rect = new Rect(0, bottom, 1, Mathf.Max(.1f, 1 - bottom - topPixels / Screen.height));
        }
        private static void PlaceHud(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
        }
        private void Awake()
        {
            if(fixtureCamera==null || cameraController==null || cameraSettings==null) return;
            ownedCameraSettings=Instantiate(cameraSettings);
            ownedCameraSettings.PositionMin=new Vector2(-40,-40); ownedCameraSettings.PositionMax=new Vector2(40,40);
            ownedCameraSettings.MaxOrthographicSize=20;
            cameraController.Configure(fixtureCamera,ownedCameraSettings,FindFirstObjectByType<AnimalCafe.Input.MouseCameraInput>());
        }
        private NavMeshData ownedData;
        private NavMeshDataInstance ownedDataInstance;
        private readonly List<long> active = new List<long>();
        private readonly List<MovementResult> results = new List<MovementResult>();
        private readonly Dictionary<string,string> rows = new Dictionary<string,string>();
        private Coroutine scenario;
        private string headline = "Ready";
        public NavigationWorld World => world;
        public IReadOnlyList<NavigationActor> Actors => actors;
        public IReadOnlyList<MovementResult> Results => results;
        public int SubmittedCount { get; private set; }
        private bool ready;
        private bool businessInitialized;
        public bool Ready
        {
            get => world!=null && world.isActiveAndEnabled && world.LayoutAvailable && (businessBridge!=null
                ? businessInitialized && gameTimeService!=null && businessBridge.Adapter!=null && businessBridge.Adapter.CurrentReadiness.CanResume
                : ready);
            private set => ready=value;
        }
        public bool Busy { get; private set; }
        public string StatusText => statusLabel != null ? statusLabel.text : headline;
        public BoxCollider Floor => floor;
        public IReadOnlyList<BoxCollider> Solids => solids;

        public void Configure(NavigationWorld navigationWorld, NavigationActor[] characters,
            BoxCollider fixtureFloor, BoxCollider[] fixtureSolids, TMP_Text label)
        { world=navigationWorld; actors=characters; floor=fixtureFloor; solids=fixtureSolids; statusLabel=label; }

        private IEnumerator Start()
        {
            if(businessBridge!=null)
            {
                startupSpeed=gameTimeService.CurrentSpeed;
                startupChoiceVersion=gameTimeService.ExplicitChoiceVersion;
                startupBlock=gameTimeService.AcquireResumeBlock(this,NavigationDecorationBridge.BlockedMessage);
                world.RequireStartupActors(actors);
            }
            var sources = new List<NavMeshBuildSource>();
            AddSource(sources,floor); foreach(var solid in solids) AddSource(sources,solid);
            var settings=NavMesh.GetSettingsByIndex(0);
            settings.agentRadius=.45f; settings.agentHeight=1.30f; settings.agentClimb=.1f;
            settings.overrideVoxelSize=true; settings.voxelSize=.025f;
            settings.overrideTileSize=true; settings.tileSize=128; settings.minRegionArea=.01f;
            ownedData=NavMeshBuilder.BuildNavMeshData(settings,sources,new Bounds(Vector3.zero,new Vector3(30,8,24)),Vector3.zero,Quaternion.identity);
            if(ownedData==null)
            {
                headline="Fixture bake failed";
                if(businessBridge!=null) world.HoldUnavailableResumeBlock(gameTimeService);
                startupBlock?.Dispose(); startupBlock=null;
                Refresh(); yield break;
            }
            ownedDataInstance=NavMesh.AddNavMeshData(ownedData);
            world.SetGeometry(solids,1); Ready=true;
            foreach(var actor in actors)
                if(!world.Register(actor)) { Ready=false; headline="Invalid authored start: "+actor.ActorId; }
            Refresh();
            if(businessBridge != null)
            {
                Ready=false;
                yield return null; // Existing Decoration Start initializes its formal registries first.
                InitializeBusinessNavigation();
            }
        }
        private void InitializeBusinessNavigation()
        {
            var enteringSpeed=startupSpeed;
            try
            {
            ReleaseFixtureNavigation();
            businessRuntime.Initialize();
            var supportDefinition=businessRuntime.Layout.FurnitureInstances.Single().DefinitionId;
            var slot=businessCatalog.BuildSurfaceSlotCatalog(businessRuntime.Layout.GridSettings).GetForSupport(supportDefinition).First().SlotId;
            var first=businessRuntime.Layout.FurnitureInstances.Single().InstanceId;
            Require(businessRuntime.Layout.PlaceFurniture(FurnitureInstance.Restore("11000000000000000000000000000002",supportDefinition,new GridPosition(4,3),FurnitureRotation.Degrees0)).Succeeded);
            Require(businessRuntime.Layout.PlaceFurniture(FurnitureInstance.Restore("11000000000000000000000000000003",supportDefinition,new GridPosition(6,3),FurnitureRotation.Degrees0)).Succeeded);
            Require(businessRuntime.FunctionalSurfaceLayout.PlaceMounted(new SurfaceMountedInstance("11000000000000000000000000000004","equipment.cash-register.01",new SurfaceSlotAddress(first,slot),FurnitureRotation.Degrees0)).Succeeded);
            Require(businessRuntime.FunctionalSurfaceLayout.PlaceMounted(new SurfaceMountedInstance("11000000000000000000000000000005","equipment.coffee-machine.01",new SurfaceSlotAddress("11000000000000000000000000000002",slot),FurnitureRotation.Degrees0)).Succeeded);
            Require(businessRuntime.FunctionalSurfaceLayout.PlacePickUp(new PickUpPointInstance("11000000000000000000000000000006",new SurfaceSlotAddress("11000000000000000000000000000003",slot))).Succeeded);
            decorationController.RefreshConfirmedNavigationPresentation();
            businessInitialized=true;
            businessBridge.Configure(businessBridge.Adapter);
            businessBridge.ConfigureTime(gameTimeService);
            // Enable the coordinator while the startup pause still prevents movement.
            // 先启用移动协调器，再验证营业恢复；此时 startup pause 仍有效。
            world.enabled=true;
            businessBridge.PrepareExit();
            if(!businessBridge.Adapter.CurrentReadiness.CanResume) world.HoldUnavailableResumeBlock(gameTimeService);
            startupBlock?.Dispose(); startupBlock=null;
            Ready=businessBridge.ResumeAfterRepair();
            headline=Ready ? "Ready: P8 business and Navigation validated" : businessBridge.Adapter.CurrentReadiness.Reason;
            if(Ready && startupChoiceVersion==gameTimeService.ExplicitChoiceVersion)
                gameTimeService.TrySetAutomaticSpeed(enteringSpeed);
            }
            catch(Exception error) { Ready=false; headline="Business navigation unavailable: "+error.Message; }
            finally
            {
                if(!Ready) world.HoldUnavailableResumeBlock(gameTimeService);
                startupBlock?.Dispose(); startupBlock=null;
                Refresh();
            }
        }
        private static void Require(bool valid)
        { if(!valid) throw new InvalidOperationException("P11 business seed rejected by confirmed layout domain"); }
        private static void AddSource(List<NavMeshBuildSource> sources,BoxCollider box)
        {
            sources.Add(new NavMeshBuildSource { shape=NavMeshBuildSourceShape.Box,
                transform=box.transform.localToWorldMatrix*Matrix4x4.Translate(box.center), size=box.size, area=0 });
        }
        // Explicit ownership handoff: call before a confirmed-layout adapter adds its own data.
        public void ReleaseFixtureNavigation()
        {
            CancelAll(); Ready=false; if(world!=null) world.enabled=false;
            if(ownedDataInstance.valid) ownedDataInstance.Remove();
            if(ownedData!=null) Destroy(ownedData); ownedData=null;
        }
        public void RunScenario(string scenarioId)
        {
            if(!Ready) { headline="Fixture unavailable; leave Play and reopen scene."; Refresh(); return; }
            if(Busy) { headline="Request running. Cancel or wait before another scenario."; Refresh(); return; }
            if(Array.IndexOf(new[]{"straight","detour","crossing","narrow-wait","same-target","recovery","crowd8","blocked85"},scenarioId)<0)
            { headline="Unknown scenario: "+scenarioId; Refresh(); return; }
            results.Clear(); SubmittedCount=0; rows.Clear(); Busy=true; headline=scenarioId+": walking to staging positions"; Refresh();
            scenario=StartCoroutine(Execute(scenarioId));
        }
        private IEnumerator Execute(string id)
        {
            if(id=="crowd8")
            {
                for(var i=0;i<actors.Length;i++) Submit(i,new NavigationTarget(new Vector3(9-i*2.6f,0,6)),null);
            }
            else
            {
                var a=new Vector3(-4,0,-4); var b=new Vector3(4,0,-4);
                if(id=="detour") a=new Vector3(-3,0,0);
                if(id=="crossing") { a=new Vector3(0,0,-4); b=new Vector3(2,0,-6); }
                if(id=="narrow-wait") { a=new Vector3(-8,0,-4); b=new Vector3(-8,0,4); }
                if(id=="blocked85") a=new Vector3(-5,0,-4);
                if(id=="detour") { yield return Stage(0,new Vector3(-3,0,-4)); if(!Busy) yield break; }
                yield return Stage(0,a); if(!Busy) yield break;
                if(id=="narrow-wait") { yield return Stage(1,new Vector3(-10,0,-4)); if(!Busy) yield break; yield return Stage(1,new Vector3(-10,0,4)); if(!Busy) yield break; }
                if(id=="crossing" || id=="same-target" || id=="narrow-wait" || id=="recovery")
                { yield return Stage(1,b); if(!Busy) yield break; }
                headline=id+": running";
                switch(id)
                {
                    case "straight": Submit(0,new NavigationTarget(new Vector3(7,0,-4)),null); break;
                    case "detour": Submit(0,new NavigationTarget(new Vector3(3,0,0)),null); break;
                    case "crossing": Submit(0,new NavigationTarget(new Vector3(4,0,-4)),null); Submit(1,new NavigationTarget(new Vector3(2,0,-2)),null); break;
                    case "narrow-wait": Submit(0,new NavigationTarget(b),null); Submit(1,new NavigationTarget(a),null); break;
                    case "same-target": Submit(0,new NavigationTarget(new Vector3(0,0,-4)),null); Submit(1,new NavigationTarget(new Vector3(0,0,-4)),null); break;
                    case "blocked85": Submit(0,new NavigationTarget(new Vector3(-5,0,0)),null); break;
                    case "recovery": Submit(0,new NavigationTarget(b),new NavigationTarget(new Vector3(3,0,-2))); break;
                }
            }
            Refresh();
            while(active.Count>0) yield return null;
            Busy=false; headline=id+": complete. Results below; actors stay here."; scenario=null; Refresh();
        }
        private IEnumerator Stage(int index,Vector3 position)
        {
            MovementResult? result=null;
            Submit(index,new NavigationTarget(position),null,r=>result=r);
            while(!result.HasValue && active.Count>0) yield return null;
            if(!result.HasValue || result.Value.Status!=MovementStatus.Arrived)
            { Busy=false; headline="Staging blocked. Cancel, try another path, or leave Play and reopen fixture."; Refresh(); }
        }
        private void Submit(int index,NavigationTarget target,NavigationTarget? recovery,Action<MovementResult> completed=null)
        {
            var actor=actors[index]; var finished=false;
            var start=world.Service.MoveTo(actor.ActorId,target,result=> {
                finished=true; active.Remove(result.RequestId); results.Add(result);
                rows[result.ActorId]="#"+result.RequestId+" "+result.Status+" retry="+result.RetryCount+" reason="+result.Reason+
                    (result.OriginalFailure!=NavigationFailure.None ? " original="+result.OriginalFailure : "");
                completed?.Invoke(result); Refresh();
            },recovery);
            if(start.Accepted)
            { SubmittedCount++; if(!finished) { active.Add(start.RequestId); rows[actor.ActorId]="#"+start.RequestId+" running"; } }
            else { rows[actor.ActorId]="Rejected: "+start.Reason; }
            Refresh();
        }
        public void CancelAll()
        {
            if(scenario!=null) StopCoroutine(scenario); scenario=null;
            foreach(var id in active.ToArray()) if(world!=null) world.Service.Cancel(id);
            active.Clear(); Busy=false; headline="Cancelled. Actors remain at current positions."; Refresh();
        }
        private void Refresh()
        {
            if(statusLabel==null) return;
            var text=new StringBuilder(headline+"\n");
            foreach(var row in rows) text.Append(row.Key).Append(": ").AppendLine(row.Value);
            statusLabel.text=text.ToString();
        }
        private void OnDisable() => CancelAll();
        private void OnDestroy()
        { ReleaseFixtureNavigation(); startupBlock?.Dispose(); startupBlock=null; if(ownedCameraSettings!=null) Destroy(ownedCameraSettings); }
    }
}
