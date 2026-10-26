using System;
using AnimalCafe.CafeLoop;
using AnimalCafe.Customers;
using AnimalCafe.Employees;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace AnimalCafe.Tests.EditMode.Phase14
{
    public sealed class CafeLoopDriverTests
    {
        private GameObject root;
        private CafeLoopController owner;
        private CustomerFlowController customers;
        private EmployeeFlowController employees;
        [SetUp] public void Setup()
        {
            root=new GameObject("P14 driver"); customers=root.AddComponent<CustomerFlowController>();
            employees=root.AddComponent<EmployeeFlowController>(); owner=root.AddComponent<CafeLoopController>();
            owner.Configure(customers,employees,null,null,null,null);
        }
        [TearDown] public void Cleanup() => Object.DestroyImmediate(root);
        [TestCase(-1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidDeltaCannotDriveEitherSubsystem(float delta)
            => Assert.Throws<ArgumentOutOfRangeException>(()=>owner.Step(delta));
        [Test] public void ConfigureTwicePreservesBusinessSessionAndSerializedOwnership()
        {
            var first=owner.Snapshot; owner.Configure(customers,employees,null,null,null,null);
            Assert.That(owner.Snapshot.SessionId,Is.EqualTo(first.SessionId));
            Assert.That(first.SessionId,Is.Not.EqualTo("unconfigured"));
            Assert.That(new SerializedObject(customers).FindProperty("integratedMode").boolValue,Is.True);
            Assert.That(new SerializedObject(employees).FindProperty("integratedMode").boolValue,Is.True);
        }
        [Test] public void SnapshotListenerCannotReenterDriverStep()
        {
            var calls=0;
            owner.SnapshotChanged+=_=>{ calls++; owner.Step(.1f); };
            owner.Step(.1f); Assert.That(calls,Is.EqualTo(1));
            owner.Step(.1f); Assert.That(calls,Is.EqualTo(2));
        }
        [Test] public void DisabledOwnerDoesNotReleaseLegacyMode()
        {
            owner.enabled=false;
            Assert.That(new SerializedObject(customers).FindProperty("integratedMode").boolValue,Is.True);
            Assert.That(new SerializedObject(employees).FindProperty("integratedMode").boolValue,Is.True);
            Assert.That(customers.AutoSpawn,Is.True,"Hold must not overwrite the caller's spawn preference");
        }
    }
}
