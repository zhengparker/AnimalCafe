using System;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using AnimalCafe.Navigation;

namespace AnimalCafe.Tests.PlayMode.Phase12
{
    public class NavigationRuntimeIdentityTests
    {
        [TestCase("Shiba")]
        [TestCase("Westie")]
        public void SamePrefabInstancesHaveIndependentImmutableVisitIds(string species)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/Phase11/Characters/" + species + "/PF_" + species + "_Navigation.prefab");
            var a = UnityEngine.Object.Instantiate(prefab);
            var b = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var method = typeof(NavigationActor).GetMethod("TryInitializeRuntimeId");
                Assert.That(method, Is.Not.Null, "Runtime spawn needs an ID initialization API");
                var first = a.GetComponent<NavigationActor>();
                var second = b.GetComponent<NavigationActor>();
                Assert.That(method.Invoke(first, new object[] { "" }), Is.False);
                Assert.That(method.Invoke(first, new object[] { " visit-a" }), Is.False);
                Assert.That(method.Invoke(first, new object[] { "visit-a" }), Is.True);
                Assert.That(method.Invoke(second, new object[] { "visit-b" }), Is.True);
                Assert.That(method.Invoke(first, new object[] { "visit-c" }), Is.False);
                Assert.That(first.ActorId, Is.EqualTo("visit-a"));
                Assert.That(second.ActorId, Is.EqualTo("visit-b"));
            }
            finally { UnityEngine.Object.DestroyImmediate(a); UnityEngine.Object.DestroyImmediate(b); }
        }
    }
}
