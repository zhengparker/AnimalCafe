using System.Collections;
using System.Linq;
using AnimalCafe.Content;
using AnimalCafe.Layout;
using AnimalCafe.Tests.PlayMode;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AnimalCafe.Tests.PlayMode.Phase4
{
    public sealed class Phase4EnvironmentIntegrationTests : IPrebuildSetup, IPostBuildCleanup
    {
        private const string BuildSettingsScopeType =
            "AnimalCafe.EditorTools.Phase4.Phase4BuildSettingsScope";
        private const string ScenePath =
            "Assets/Scenes/Validation/Phase4CoreArchitecture.unity";
        private const string FallbackSceneName =
            "Phase4EnvironmentIntegrationTests_ColliderFreeFallback";
        public void Setup()
        {
            EditorPrebuildScopeBridge.Setup(BuildSettingsScopeType);
        }

        public void Cleanup()
        {
            EditorPrebuildScopeBridge.Cleanup(BuildSettingsScopeType);
        }

        [TearDown]
        public void RemovePhase4SceneResidue()
        {
            var phase4Scene = SceneManager.GetActiveScene();
            var fallback = phase4Scene.IsValid()
                && phase4Scene.isLoaded
                && phase4Scene.name == FallbackSceneName
                    ? phase4Scene
                    : SceneManager.CreateScene(FallbackSceneName);
            if (SceneManager.GetActiveScene() != fallback)
            {
                Assert.That(SceneManager.SetActiveScene(fallback), Is.True);
            }

            foreach (var root in fallback.GetRootGameObjects())
            {
                Object.DestroyImmediate(root);
            }

            Assert.That(fallback.GetRootGameObjects(), Is.Empty);
            if (phase4Scene.IsValid()
                && phase4Scene.isLoaded
                && phase4Scene != fallback)
            {
                foreach (var root in phase4Scene.GetRootGameObjects())
                {
                    Object.DestroyImmediate(root);
                }

                Assert.That(phase4Scene.GetRootGameObjects(), Is.Empty);
                Assert.That(
                    SceneManager.UnloadSceneAsync(phase4Scene),
                    Is.Not.Null);
            }
        }

        [UnityTest]
        public IEnumerator Phase4Scene_LoadsWithExactStableEnvironment()
        {
            yield return LoadPhase4ValidationScene();

            Assert.That(FindAll("P4_Floor_8x8"), Has.Length.EqualTo(1));
            Assert.That(FindAll("P4_Entrance"), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<WallSurfaceAuthoring>(
                FindObjectsInactive.Include), Has.Length.EqualTo(2));
            Assert.That(Object.FindObjectsByType<UnityEngine.Camera>(FindObjectsInactive.Include),
                Has.Length.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Phase4Scene_EntranceVisualDoesNotBlockTwoByTwoClearance()
        {
            yield return LoadPhase4ValidationScene();

            var entrance = GameObject.Find("P4_Entrance");
            var portal = entrance.GetComponent<EntrancePortalAuthoring>();
            var reservation = portal.CreateReservation();

            Assert.That(reservation.Type,
                Is.EqualTo(LayoutReservationType.EntranceClearance));
            Assert.That(reservation.Size, Is.EqualTo(new GridSize(2, 2)));
            Assert.That(entrance.transform.Find("EntranceClearance_2x2"), Is.Not.Null);
            Assert.That(entrance.GetComponentsInChildren<Collider>(true), Is.Empty,
                "Entrance line/outline colliders would block its 2x2 clearance.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Phase4Scene_FloorRemainsOnSelectableRaycastLayer()
        {
            yield return LoadPhase4ValidationScene();

            var floor = GameObject.Find("P4_Floor_8x8");
            var floorVisual = floor.transform.Find("FloorVisual").gameObject;
            var floorCollider = floorVisual.GetComponent<Collider>();
            Assert.That(floorCollider, Is.Not.Null);
            Assert.That(floorVisual.layer, Is.EqualTo(LayerMask.NameToLayer("Default")));
            Assert.That(Physics.DefaultRaycastLayers & (1 << floorVisual.layer), Is.Not.Zero);

            Physics.SyncTransforms();
            Assert.That(Physics.Raycast(
                new Vector3(0f, 5f, 0f),
                Vector3.down,
                out var hit,
                10f,
                Physics.DefaultRaycastLayers), Is.True);
            Assert.That(hit.collider, Is.SameAs(floorCollider));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Phase4Scene_HasNoMissingScriptsOrUnexpectedLogs()
        {
            yield return LoadPhase4ValidationScene();

            var scene = SceneManager.GetActiveScene();
            var behaviours = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include)
                .Where(transform => transform.gameObject.scene == scene)
                .SelectMany(transform => transform.gameObject.GetComponents<MonoBehaviour>())
                .ToArray();
            Assert.That(behaviours, Has.None.Null);
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator LoadPhase4ValidationScene()
        {
            var operation = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            Assert.That(operation, Is.Not.Null);
            yield return new WaitUntil(() => operation.isDone);
            yield return null;
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(ScenePath));
        }

        private static GameObject[] FindAll(string name)
        {
            return Object.FindObjectsByType<Transform>(FindObjectsInactive.Include)
                .Where(transform => transform.gameObject.scene == SceneManager.GetActiveScene())
                .Where(transform => transform.name == name)
                .Select(transform => transform.gameObject)
                .ToArray();
        }
    }
}
