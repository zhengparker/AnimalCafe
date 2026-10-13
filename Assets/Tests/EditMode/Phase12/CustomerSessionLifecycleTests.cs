using System;
using System.Collections;
using System.Linq;
using AnimalCafe.Customers;
using AnimalCafe.Navigation;
using AnimalCafe.Core.Time;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace AnimalCafe.Tests.EditMode.Phase12
{
    public sealed class CustomerSessionLifecycleTests
    {
        [UnityTest] public IEnumerator TwoPlaySessionsWithoutDomainReloadReleaseCustomerOwners()
        {
            var enabled=EditorSettings.enterPlayModeOptionsEnabled; var options=EditorSettings.enterPlayModeOptions;
            var setup=EditorSceneManager.GetSceneManagerSetup(); var capture=Time.captureDeltaTime;
            Exception failure=null; string previousId=null;
            EditorSettings.enterPlayModeOptionsEnabled=true;
            EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
            try
            {
                EditorSceneManager.OpenScene("Assets/Scenes/Validation/Phase12CustomerQueue.unity",OpenSceneMode.Single);
                for(var run=0;run<2;run++)
                {
                    yield return new EnterPlayMode(false); Time.captureDeltaTime=1f/60;
                    CustomerFlowController flow=null; AnimalCafe.Capacity.CapacityService capacity=null;
                    for(var frame=0;frame<900;frame++)
                    {
                        flow=UnityEngine.Object.FindFirstObjectByType<CustomerFlowController>();
                        if(flow!=null && flow.Snapshot.Count>0) break;
                        yield return null;
                    }
                    try
                    {
                        Assert.That(flow,Is.Not.Null); Assert.That(flow.Snapshot.Count,Is.GreaterThan(0));
                        var time=UnityEngine.Object.FindFirstObjectByType<GameTimeService>();
                        Assert.That(time.IsResumeBlocked,Is.False);
                        Assert.That(UnityEngine.Object.FindObjectsByType<CustomerFlowController>(FindObjectsSortMode.None).Length,Is.EqualTo(1));
                        var id=flow.Snapshot[0].VisitId; Assert.That(id,Is.Not.EqualTo(previousId)); previousId=id;
                        capacity=flow.Capacity; flow.AutoSpawn=false;
                    }
                    catch(Exception error) { failure=error; }
                    yield return new ExitPlayMode();
                    if(capacity!=null) foreach(var ledger in capacity.GetCapacities()) Assert.That(ledger.Used,Is.Zero,"Old session releases its own tokens");
                    Assert.That(Resources.FindObjectsOfTypeAll<NavigationActor>().Any(a=>a.ActorId==previousId),Is.False,"No old runtime actor");
                    if(failure!=null) break;
                }
            }
            finally
            {
                Time.captureDeltaTime=capture;
                EditorSettings.enterPlayModeOptions=options; EditorSettings.enterPlayModeOptionsEnabled=enabled;
                if(setup.Length>0) EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            }
            if(failure!=null) throw failure;
        }
    }
}
