using System;
using System.Reflection;
using AnimalCafe.Core.Time;
using AnimalCafe.Decoration;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace AnimalCafe.Tests.EditMode.Phase14
{
    public sealed class CafeDecorationLifecycleTests
    {
        private GameObject root;
        private GameTimeService time;
        [SetUp] public void Setup()
        {
            root=new GameObject("P14 decoration lifecycle"); time=root.AddComponent<GameTimeService>();
            typeof(GameTimeService).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(time,null);
        }
        [TearDown] public void Cleanup() { Object.DestroyImmediate(root); Time.timeScale=1; }
        [Test] public void SameSpeedExplicitPauseIsRecordedWithoutChangingAutomaticRules()
        {
            time.TrySetAutomaticSpeed(GameSpeed.Paused); var before=time.ExplicitChoiceVersion;
            Assert.That(time.LastExplicitSpeed,Is.Null);
            Assert.That(time.TrySetSpeed(GameSpeed.Paused),Is.True);
            Assert.That(time.LastExplicitSpeed,Is.EqualTo(GameSpeed.Paused));
            Assert.That(time.ExplicitChoiceVersion,Is.EqualTo(before+1));
            time.TrySetAutomaticSpeed(GameSpeed.Normal);
            Assert.That(time.LastExplicitSpeed,Is.EqualTo(GameSpeed.Paused));
        }
        [Test] public void SameNormalThenAutomaticPauseDoesNotInventExplicitPause()
        {
            Assert.That(time.TrySetSpeed(GameSpeed.Normal),Is.True);
            using(time.AcquireResumeBlock(this,"setup"))
            {
                Assert.That(time.LastExplicitSpeed,Is.EqualTo(GameSpeed.Normal));
                var version=time.ExplicitChoiceVersion;
                Assert.That(time.TrySetSpeed(GameSpeed.Fast),Is.False);
                Assert.That(time.LastExplicitSpeed,Is.EqualTo(GameSpeed.Normal));
                Assert.That(time.ExplicitChoiceVersion,Is.EqualTo(version));
            }
        }
        [Test] public void PreparingHookRunsBeforeVersionPublicationWhilePauseRemainsHeld()
        {
            var decor=root.AddComponent<DecorationModeController>(); Arm(decor,true); time.TrySetAutomaticSpeed(GameSpeed.Paused);
            var calls=0; decor.ChangedLayoutExitPreparing+=version=>
            { calls++; Assert.That(version,Is.EqualTo(1)); Assert.That(decor.CompletedLayoutChangeVersion,Is.Zero);
                Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Paused)); };
            Close(decor,true);
            Assert.That(calls,Is.EqualTo(1)); Assert.That(decor.CompletedLayoutChangeVersion,Is.EqualTo(1));
            Close(decor,true); Assert.That(calls,Is.EqualTo(1));
        }
        [TestCase(false,true)] [TestCase(true,false)]
        public void NoChangeAndShutdownDoNotTriggerEditedDone(bool changed,bool normal)
        {
            var decor=root.AddComponent<DecorationModeController>(); Arm(decor,changed); var calls=0;
            decor.ChangedLayoutExitPreparing+=_=>calls++; Close(decor,normal);
            Assert.That(calls,Is.Zero); Assert.That(decor.CompletedLayoutChangeVersion,Is.Zero);
        }
        private static void Arm(DecorationModeController decor,bool changed)
        { Set(decor,"isOpen",true); Set(decor,"cleanupRequired",true); Set(decor,"hasConfirmedLayoutChanges",changed); }
        private static void Set(object o,string name,object value) => o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,value);
        private static void Close(DecorationModeController decor,bool normal) => typeof(DecorationModeController)
            .GetMethod("CleanupDecorationMode",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(decor,new object[]{normal});
    }
}
