using System.Linq;
using System.Reflection;
using AnimalCafe.Capacity;
using AnimalCafe.EditorTools.Phase10;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnimalCafe.Tests.EditMode.Phase10
{
    public sealed class CapacityDebugWindowTests
    {
        private CapacityDebugWindow window;

        [TearDown]
        public void CloseOwnedWindow()
        {
            if (window != null) window.Close();
            window = null;
        }

        [Test]
        public void D001_InputFloorStaysSeparateUntilApply()
        {
            window = NewWindow();
            Assert.That(window.FloorCells, Is.EqualTo(64));
            Assert.That(window.OwnerId, Is.EqualTo("V1"));
            Assert.That(window.TokenId, Is.EqualTo(1));
            Assert.That(new[] { window.IncludeTotal, window.IncludeCounter, window.IncludePickUp },
                Is.All.True);
            Assert.That(window.Session.Service.Limits.FloorCellCount, Is.EqualTo(64));
            Assert.That(window.Capacities.Select(x => x.Limit), Is.EqualTo(new[] { 16, 8, 16 }));
            window.FloorCells = 8;
            Assert.That(window.Session.Service.Limits.FloorCellCount, Is.EqualTo(64));
            Assert.That(window.RunApplyFloor().Succeeded, Is.True);
            Assert.That(window.Session.Service.Limits.FloorCellCount, Is.EqualTo(8));
            Assert.That(window.Capacities.Select(x => x.Limit), Is.EqualTo(new[] { 2, 1, 2 }));
        }

        [Test]
        public void D002_CommandsReturnDomainResultsIncludingInvalidInputs()
        {
            window = NewWindow();
            window.IncludeTotal = false;
            window.IncludeCounter = false;
            window.IncludePickUp = false;
            Assert.That(window.RunReserveSelected().FailureReason, Is.EqualTo(CapacityFailureReason.InvalidKinds));
            window.FloorCells = -1;
            Assert.That(window.RunApplyFloor().FailureReason, Is.EqualTo(CapacityFailureReason.InvalidFloorCellCount));
            Assert.That(window.Session.Service.Limits.FloorCellCount, Is.EqualTo(64));
            window.TokenId = 999;
            Assert.That(window.RunOccupy().FailureReason, Is.EqualTo(CapacityFailureReason.InvalidToken));
            Assert.That(window.LastResult.FailureReason, Is.EqualTo(CapacityFailureReason.InvalidToken));
            Assert.That(window.RunRelease().FailureReason, Is.EqualTo(CapacityFailureReason.InvalidToken));
            Assert.That(window.RunReserveAdmission().Succeeded, Is.True);
            window.TokenId = 1;
            Assert.That(window.RunOccupy().Succeeded, Is.True);
            Assert.That(window.RunRelease().Succeeded, Is.True);
            window.IncludePickUp = true;
            window.OwnerId = "V2";
            Assert.That(window.RunReserveSelected().Succeeded, Is.True);
            Assert.That(window.Reservations.Last().Kind, Is.EqualTo(CapacityKind.PickUp));
        }

        [Test]
        public void D003_ResetReplacesServiceAndAllWindowState()
        {
            window = NewWindow();
            var token = window.RunReserveAdmission().Reservations[0].Token;
            var oldService = window.Session.Service;
            window.FloorCells = 8;
            window.OwnerId = "Other";
            window.TokenId = 42;
            window.IncludeTotal = false;
            window.IncludeCounter = false;
            window.IncludePickUp = false;
            window.ScrollPosition = new Vector2(17, 23);
            window.ResetSession();
            Assert.That(window.Session.Service, Is.Not.SameAs(oldService));
            Assert.That(window.FloorCells, Is.EqualTo(64));
            Assert.That(window.OwnerId, Is.EqualTo("V1"));
            Assert.That(window.TokenId, Is.EqualTo(1));
            Assert.That(new[] { window.IncludeTotal, window.IncludeCounter, window.IncludePickUp },
                Is.All.True);
            Assert.That(window.ScrollPosition, Is.EqualTo(Vector2.zero));
            Assert.That(window.LastResult, Is.Null);
            Assert.That(window.Reservations, Is.Empty);
            Assert.That(window.Session.Service.Occupy(token, "V1").FailureReason,
                Is.EqualTo(CapacityFailureReason.InvalidToken));
        }

        [Test]
        public void D004_CloseReopenAndPlayBoundaryResetOwnedSession()
        {
            window = NewWindow();
            window.RunReserveAdmission();
            var first = window.Session.Service;
            var callback = typeof(CapacityDebugWindow).GetMethod("OnPlayModeStateChanged",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(callback, Is.Not.Null);
            callback.Invoke(window, new object[] { PlayModeStateChange.ExitingEditMode });
            Assert.That(window.Session.Service, Is.Not.SameAs(first));
            Assert.That(window.Reservations, Is.Empty);
            window.RunReserveAdmission();
            var second = window.Session.Service;
            callback.Invoke(window, new object[] { PlayModeStateChange.ExitingPlayMode });
            Assert.That(window.Session.Service, Is.Not.SameAs(second));
            var third = window.Session.Service;
            window.Close();
            window = null;
            window = NewWindow();
            Assert.That(window.Session.Service, Is.Not.SameAs(third));
            Assert.That(window.Reservations, Is.Empty);
            Assert.That(window.RunReserveAdmission().Reservations[0].Token.Id, Is.EqualTo(1));
        }

        [Test]
        public void D005_OwnedWindowDoesNotChangeSceneSelectionOrTime()
        {
            var activeScene = SceneManager.GetActiveScene();
            var selected = Selection.objects.ToArray();
            var timeScale = Time.timeScale;
            var scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
            var dirty = scenes.Select(scene => scene.isDirty).ToArray();
            var roots = scenes.Select(scene => scene.GetRootGameObjects()
                .Select(root => root.GetEntityId()).ToArray()).ToArray();
            window = NewWindow();
            window.RunReserveAdmission();
            window.RunOccupy();
            window.RunRelease();
            Assert.That(window.Capacities.Count, Is.EqualTo(3));
            window.ResetSession();
            window.Close();
            window = null;
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(activeScene));
            Assert.That(Selection.objects, Is.EqualTo(selected));
            Assert.That(Time.timeScale, Is.EqualTo(timeScale));
            Assert.That(SceneManager.sceneCount, Is.EqualTo(scenes.Length));
            for (var i = 0; i < scenes.Length; i++)
            {
                Assert.That(SceneManager.GetSceneAt(i), Is.EqualTo(scenes[i]));
                Assert.That(scenes[i].isDirty, Is.EqualTo(dirty[i]));
                Assert.That(scenes[i].GetRootGameObjects().Select(root => root.GetEntityId()),
                    Is.EqualTo(roots[i]));
            }
        }

        [Test]
        public void D006_CurrentTableIncludesReleasedHistoryAndFreshLimits()
        {
            window = NewWindow();
            var first = window.RunReserveAdmission();
            Assert.That(first.Reservations.Count, Is.EqualTo(3));
            window.TokenId = 2;
            Assert.That(window.RunRelease().Succeeded, Is.True);
            Assert.That(window.RunReserveAdmission().FailureReason,
                Is.EqualTo(CapacityFailureReason.OwnerAlreadyReserved));
            Assert.That(window.Reservations.Count, Is.EqualTo(3));
            Assert.That(window.Reservations[1].State, Is.EqualTo(ReservationState.Released));
            Assert.That(window.Capacities[1].Used, Is.EqualTo(0));
            window.FloorCells = 4;
            window.RunApplyFloor();
            Assert.That(window.Capacities.Select(x => x.Limit), Is.EqualTo(new[] { 1, 1, 1 }));
            Assert.That(window.Capacities[0].OverCapacity, Is.EqualTo(0));
            Assert.That(window.LastResult.Succeeded, Is.True);
        }

        private static CapacityDebugWindow NewWindow()
        {
            var owned = ScriptableObject.CreateInstance<CapacityDebugWindow>();
            owned.Show();
            return owned;
        }
    }
}
