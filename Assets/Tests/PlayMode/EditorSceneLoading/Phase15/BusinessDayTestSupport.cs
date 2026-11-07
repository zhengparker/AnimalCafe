#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using AnimalCafe.CafeLoop;
using AnimalCafe.Customers;
using AnimalCafe.Employees;
using AnimalCafe.Navigation;
using AnimalCafe.Tests.PlayMode.EditorSceneLoading;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AnimalCafe.Tests.PlayMode.Phase15
{
    public abstract class BusinessDayTestSupport
    {
        protected Scene scene;
        protected CafeLoopController loop;
        protected CustomerFlowController customers;
        protected NavigationWorld world;
        private float capture, scale;
        private UnityEngine.InputSystem.InputActionAsset[] inputs;
        protected sealed class FakeClock { public double Now; public double Read() => Now; }
        protected virtual string RuntimeScenePath => "Assets/Scenes/Validation/Phase14CafeLoop.unity";

        [UnitySetUp] public IEnumerator LoadRuntime()
        {
            capture=Time.captureDeltaTime; scale=Time.timeScale; Time.captureDeltaTime=1f/60;
            scene=EditorSceneManager.LoadSceneInPlayMode(RuntimeScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            loop=Find<CafeLoopController>(); customers=Find<CustomerFlowController>(); world=Find<NavigationWorld>();
            customers.AutoSpawn=false; inputs=Phase8SceneInputTestCleanup.CaptureAssets(scene);
            var employees=Find<EmployeeFlowController>();
            for(var frame=0;frame<600 && (!loop.CanProgress || employees.Snapshot.Any(e=>e.State!=EmployeeState.Idle));frame++)
                yield return null;
            Assert.That(loop.CanProgress,Is.True,"P14 runtime must really be ready before admission checks");
            Assert.That(employees.Snapshot.All(e=>e.State==EmployeeState.Idle),Is.True);
            // 员工到站后的下一次唯一driver才会发布admission gate。
            loop.Step(0);
            Assert.That(customers.Snapshot,Is.Empty);
        }

        protected T Find<T>() where T:Component => scene.GetRootGameObjects()
            .SelectMany(root=>root.GetComponentsInChildren<T>(true)).Single();

        // Reuse the actual unborn-only proof; older real work may continue normally.
        // 仅在原positive前准备同一请求；不做全局Setup prime，不冻结旧业务/Native。
        protected IEnumerator PrepareUnbornAdmission()
            => AnimalCafe.Tests.PlayMode.Phase14.CafeLoopRuntimeProof.PreparePositiveAdmission(loop,customers,world,600);

        protected void RequirePolicy(bool required=true)
        {
            var serialized=new SerializedObject(customers);
            var property=serialized.FindProperty("requireAdmissionPolicy");
            Assert.That(property,Is.Not.Null,"required policy must be serialized for load-time protection");
            property.boolValue=required; serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        protected void AssertRejectedWithoutAllocation()
        {
            var actors=world.RegisteredActors.ToArray();
            var reservations=customers.Capacity.GetReservations().Count;
            var orders=loop.Snapshot.Orders.Count;
            Assert.That(customers.TrySpawn(),Is.False,"Admission must reject before reserve/create: "+customers.Status);
            CollectionAssert.AreEquivalent(actors,world.RegisteredActors);
            Assert.That(customers.Capacity.GetReservations().Count,Is.EqualTo(reservations));
            Assert.That(loop.Snapshot.Orders.Count,Is.EqualTo(orders));
            Assert.That(customers.Snapshot,Is.Empty);
        }

        [UnityTearDown] public IEnumerator UnloadRuntime()
        {
            if(scene.IsValid() && scene.isLoaded)
            {
                SceneManager.SetActiveScene(SceneManager.CreateScene("P15 admission cleanup"));
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            Phase8SceneInputTestCleanup.DisposeReleasedAssets(inputs ?? Array.Empty<UnityEngine.InputSystem.InputActionAsset>());
            Time.captureDeltaTime=capture; Time.timeScale=scale;
        }
    }
}
#endif
