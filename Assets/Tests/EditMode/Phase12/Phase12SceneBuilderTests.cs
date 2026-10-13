using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using AnimalCafe.Customers;
using AnimalCafe.Navigation;

namespace AnimalCafe.Tests.EditMode.Phase12
{
    public class Phase12SceneBuilderTests
    {
        [Test] public void BuilderCanRunTwiceAndSavedSceneHasSingleOwners()
        {
            var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("AnimalCafe.EditorTools.Phase12.Phase12CustomerQueueSceneSetup")).First(t=>t!=null);
            var p11=System.IO.File.ReadAllBytes("Assets/Scenes/Validation/Phase11Navigation.unity");
            var method=type.GetMethod("CreateValidationScene");
            method.Invoke(null,null); method.Invoke(null,null);
            Assert.That(System.IO.File.ReadAllBytes("Assets/Scenes/Validation/Phase11Navigation.unity"),Is.EqualTo(p11),"Builder preserves P11 scene");
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/Validation/Phase12CustomerQueue.unity",OpenSceneMode.Additive);
            try
            {
                var roots=scene.GetRootGameObjects();
                Assert.That(roots.SelectMany(r=>r.GetComponentsInChildren<CustomerFlowController>(true)).Count(),Is.EqualTo(1));
                Assert.That(roots.SelectMany(r=>r.GetComponentsInChildren<NavigationWorld>(true)).Count(),Is.EqualTo(1));
                Assert.That(roots.SelectMany(r=>r.GetComponentsInChildren<NavigationLayoutAdapter>(true)).Count(),Is.EqualTo(1));
                Assert.That(roots.SelectMany(r=>r.GetComponentsInChildren<NavigationDecorationBridge>(true)).Count(),Is.EqualTo(1));
            }
            finally { EditorSceneManager.CloseScene(scene,true); }
        }
    }
}

