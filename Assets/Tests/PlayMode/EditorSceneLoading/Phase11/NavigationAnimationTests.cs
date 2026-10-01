using System.Collections;
using System.Collections.Generic;
using AnimalCafe.Navigation;
using AnimalCafe.Core.Time;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AnimalCafe.Tests.PlayMode.Phase11
{
    public sealed class NavigationAnimationTests
    {
        private GameObject root;
        private NavigationWorld world;
        private GameTimeService time;
        private NavMeshData data;
        private NavMeshDataInstance instance;
        private float oldCapture;
        [SetUp] public void Setup()
        {
            oldCapture = Time.captureDeltaTime; Time.captureDeltaTime = 0;
            root = new GameObject("AnimationFixture");
            time = root.AddComponent<GameTimeService>(); time.SetNormal();
            world = root.AddComponent<NavigationWorld>();
            var settings = NavMesh.GetSettingsByIndex(0);
            settings.agentRadius = .45f; settings.agentHeight = 1.3f; settings.agentClimb = .1f;
            settings.overrideVoxelSize = true; settings.voxelSize = .025f;
            settings.overrideTileSize = true; settings.tileSize = 128; settings.minRegionArea = .01f;
            data = NavMeshBuilder.BuildNavMeshData(settings, new List<NavMeshBuildSource> {
                new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, size = new Vector3(26,.2f,12),
                    transform = Matrix4x4.TRS(new Vector3(0,-.1f,0),Quaternion.identity,Vector3.one) }
            }, new Bounds(Vector3.zero, new Vector3(30,6,16)), Vector3.zero, Quaternion.identity);
            instance = NavMesh.AddNavMeshData(data); world.SetGeometry(new Collider[0], 1);
        }
        [TearDown] public void Cleanup()
        {
            Object.DestroyImmediate(root); instance.Remove(); Object.DestroyImmediate(data);
            Time.captureDeltaTime = oldCapture; Time.timeScale = 1;
        }
        private NavigationActor Actor(string species, Vector3 position)
        {
#if UNITY_EDITOR
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Phase11/Characters/"+species+"/PF_"+species+"_Navigation.prefab");
            var go = Object.Instantiate(prefab, position, Quaternion.identity, root.transform);
            var actor = go.GetComponent<NavigationActor>(); Assert.That(world.Register(actor), Is.True);
            Assert.That(go.GetComponent("NavigationWalkPresenter"), Is.Not.Null, "Prefab must drive Walk from approved displacement");
            return actor;
#else
            throw new System.NotSupportedException();
#endif
        }
        private IEnumerator Until(System.Func<bool> condition, float seconds=25)
        {
            var end = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(condition(), Is.True, "Real callback/state timed out");
        }
        [UnityTest] public IEnumerator InactiveActorStopsWithoutAnimatorWarning()
        {
            var actor = Actor("Shiba", Vector3.left * 3);
            world.Service.MoveTo(actor.ActorId, new NavigationTarget(Vector3.right * 3), _ => {});
            yield return Until(() => actor.ModelAnimator.GetCurrentAnimatorStateInfo(0).IsName("Walk"));
            var warnings = new List<string>();
            Application.LogCallback capture = (message, stack, type) => {
                if (type == LogType.Warning && message.IndexOf("animator", System.StringComparison.OrdinalIgnoreCase) >= 0) warnings.Add(message);
            };
            Application.logMessageReceived += capture;
            try
            {
                actor.gameObject.SetActive(false);
                actor.GetComponent<NavigationWalkPresenter>().SetMotion(0);
                yield return null;
                Assert.That(warnings, Is.Empty, "Inactive actor teardown must not call Animator.Play");
            }
            finally { Application.logMessageReceived -= capture; }
        }

        [UnityTest] public IEnumerator WalkStopsWhenGuardBlocks()
        {
            var actor = Actor("Shiba", Vector3.left*3); MovementResult? result=null;
            world.Service.MoveTo(actor.ActorId,new NavigationTarget(Vector3.right*3),r=>result=r);
            yield return Until(()=>actor.ModelAnimator.GetCurrentAnimatorStateInfo(0).IsName("Walk"));
            // Add a guard-only solid after baking: Agent still intends to walk into it.
            var wall = new GameObject("GuardOnlyWall"); wall.transform.SetParent(root.transform);
            wall.transform.position=new Vector3(0,.7f,0); var collider=wall.AddComponent<BoxCollider>(); collider.size=new Vector3(.2f,1.4f,12);
            world.SetGeometry(new[]{collider},1);
            yield return Until(()=>actor.transform.position.x > -.59f);
            yield return new WaitForSeconds(.3f);
            Assert.That(actor.ModelAnimator.GetCurrentAnimatorStateInfo(0).IsName("Hold"),Is.True);
            var pose=actor.ModelRoot.GetComponentsInChildren<Transform>(); var rotations=new Quaternion[pose.Length];
            for(var i=0;i<pose.Length;i++) rotations[i]=pose[i].localRotation;
            yield return new WaitForSeconds(.2f);
            for(var i=0;i<pose.Length;i++) Assert.That(Quaternion.Angle(rotations[i],pose[i].localRotation),Is.LessThan(.01f));
            Assert.That(result.HasValue,Is.False,"Guard wait must not immediately terminate");
        }
        [UnityTest] public IEnumerator PauseFreezesPoseAndTimeout()
        {
            var actor=Actor("Westie",Vector3.left*5); MovementResult? result=null;
            world.Service.MoveTo(actor.ActorId,new NavigationTarget(Vector3.right*5),r=>result=r);
            yield return Until(()=>actor.ModelAnimator.GetCurrentAnimatorStateInfo(0).IsName("Walk"));
            time.SetPaused(); yield return null;
            var position=actor.transform.position; var state=actor.ModelAnimator.GetCurrentAnimatorStateInfo(0);
            var transforms=actor.ModelRoot.GetComponentsInChildren<Transform>(); var rotations=new Quaternion[transforms.Length];
            for(var i=0;i<transforms.Length;i++) rotations[i]=transforms[i].localRotation;
            yield return new WaitForSecondsRealtime(3.3f);
            Assert.That(Vector3.Distance(position,actor.transform.position),Is.LessThanOrEqualTo(.001f));
            Assert.That(actor.ModelAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime,Is.EqualTo(state.normalizedTime).Within(.001f));
            for(var i=0;i<transforms.Length;i++) Assert.That(Quaternion.Angle(rotations[i],transforms[i].localRotation),Is.LessThan(.01f));
            Assert.That(result.HasValue,Is.False);
            time.SetFast(); yield return Until(()=>result.HasValue);
            Assert.That(result.Value.Status,Is.EqualTo(MovementStatus.Arrived)); Assert.That(result.Value.RetryCount,Is.Zero);
        }
        [UnityTest] public IEnumerator FastIsTwiceNotFourTimes()
        {
            var actor=Actor("Shiba",Vector3.left*6); MovementResult? result=null;
            var start=Time.realtimeSinceStartup;
            world.Service.MoveTo(actor.ActorId,new NavigationTarget(Vector3.right*6),r=>result=r);
            yield return Until(()=>result.HasValue); var normal=Time.realtimeSinceStartup-start;
            Assert.That(result.Value.Status,Is.EqualTo(MovementStatus.Arrived));
            // Walk back to the same authored endpoint before measuring the same direction.
            time.SetFast(); result=null;
            world.Service.MoveTo(actor.ActorId,new NavigationTarget(Vector3.left*6),r=>result=r);
            yield return Until(()=>result.HasValue); Assert.That(result.Value.Status,Is.EqualTo(MovementStatus.Arrived));
            result=null; start=Time.realtimeSinceStartup;
            world.Service.MoveTo(actor.ActorId,new NavigationTarget(Vector3.right*6),r=>result=r);
            yield return Until(()=>result.HasValue); var fast=Time.realtimeSinceStartup-start;
            Assert.That(result.Value.Status,Is.EqualTo(MovementStatus.Arrived));
            Debug.Log("P11_TIMING normal="+normal+" fast="+fast+" ratio="+(fast/normal));
            Assert.That(fast/normal,Is.InRange(.4f,.6f),"normal="+normal+" fast="+fast);
        }
    }
}
