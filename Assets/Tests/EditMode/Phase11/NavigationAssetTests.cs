using System;
using System.Collections;
using AnimalCafe.Navigation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.TestTools;

namespace AnimalCafe.Tests.EditMode.Phase11
{
    public sealed class NavigationAssetTests
    {
        private const string Base = "Assets/Art/Phase11/Characters/";

        private static readonly (string name, float fps)[] Characters =
        {
            ("Shiba", 30f), ("Westie", 24f)
        };

        [Test]
        public void CharacterUsesOriginalScaleAndWalk()
        {
            foreach (var character in Characters)
            {
                var folder = Base + character.name + "/";
                var prefab = Require<GameObject>(folder + "PF_" + character.name + "_Navigation.prefab");
                var model = Require<GameObject>(folder + "SM_" + character.name + "_Walk_Default.fbx");
                var material = Require<Material>(folder + "M_" + character.name + ".mat");
                var texture = Require<Texture2D>(folder + "T_" + character.name + "_BaseColor.png");
                var actor = prefab.GetComponent<NavigationActor>();
                Assert.That(actor, Is.Not.Null, character.name + " NavigationActor");
                Assert.That(actor.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(actor.ActorId, Is.EqualTo(character.name));
                Assert.That(actor.ModelRoot, Is.Not.Null);
                Assert.That(actor.ModelRoot.localScale, Is.EqualTo(Vector3.one));
                Assert.That(actor.Proxy, Is.Not.Null);
                Assert.That(actor.Proxy.radius, Is.EqualTo(0.45f).Within(0.001f));
                Assert.That(actor.Proxy.height, Is.EqualTo(1.30f).Within(0.001f));
                Assert.That(actor.Agent.radius, Is.EqualTo(actor.Proxy.radius).Within(0.001f));
                Assert.That(actor.Agent.height, Is.EqualTo(actor.Proxy.height).Within(0.001f));
                Assert.That(actor.Settings.AgentRadius, Is.EqualTo(0.45f));
                Assert.That(actor.Settings.CapsuleHeight, Is.EqualTo(1.30f));
                Assert.That(actor.ModelRoot.GetComponentInChildren<SkinnedMeshRenderer>(), Is.Not.Null);
                Assert.That(actor.ModelRoot.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterial,
                    Is.EqualTo(material));
                Assert.That(material.GetTexture("_BaseMap"), Is.EqualTo(texture));
                Assert.That(model.GetComponentInChildren<SkinnedMeshRenderer>(), Is.Not.Null);
                var walk = FindWalk(folder + "SM_" + character.name + "_Walk_Default.fbx");
                Assert.That(walk, Is.Not.Null, character.name + " Walk clip");
                var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                try
                {
                    var instanceActor = instance.GetComponent<NavigationActor>();
                    walk.SampleAnimation(instanceActor.ModelAnimator.gameObject, 0f);
                    var bounds = BakedBounds(instanceActor.ModelRoot.GetComponentInChildren<SkinnedMeshRenderer>());
                    Assert.That(bounds.size.y, Is.InRange(1.27f, 1.34f), character.name + " original height");
                    Assert.That(bounds.min.y, Is.InRange(-0.02f, 0.02f), character.name + " feet at pivot");
                }
                finally { UnityEngine.Object.DestroyImmediate(instance); }
            }
        }

        [Test]
        public void NoRootMotionOrAutomaticAgentTransform()
        {
            foreach (var character in Characters)
            {
                var prefab = Require<GameObject>(Base + character.name + "/PF_" + character.name + "_Navigation.prefab");
                var actor = prefab.GetComponent<NavigationActor>();
                Assert.That(actor, Is.Not.Null, character.name + " NavigationActor");
                Assert.That(actor.Agent, Is.Not.Null);
                Assert.That(actor.ModelAnimator, Is.Not.Null);
                Assert.That(actor.Agent.enabled, Is.False);
                Assert.That(actor.Agent.autoRepath, Is.False);
                Assert.That(actor.Agent.autoTraverseOffMeshLink, Is.False);
                Assert.That(actor.ModelAnimator.applyRootMotion, Is.False);
                Assert.That(actor.Agent.speed, Is.EqualTo(1.2f).Within(0.001f));
                Assert.That(actor.Agent.angularSpeed, Is.EqualTo(360f).Within(0.001f));
            }
        }

        [UnityTest]
        public IEnumerator RuntimeActorDisablesAutomaticAgentTransformBeforeBinding()
        {
            yield return new EnterPlayMode();
            Exception failure = null;
            try
            {
                foreach (var character in Characters)
                {
                    var prefab = Require<GameObject>(Base + character.name + "/PF_" + character.name + "_Navigation.prefab");
                    var position = new Vector3(4f, 0f, 3f);
                    var instance = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);
                    try
                    {
                        var actor = instance.GetComponent<NavigationActor>();
                        Assert.That(actor.Agent.enabled, Is.False);
                        Assert.That(actor.Agent.updatePosition, Is.False);
                        Assert.That(actor.Agent.updateRotation, Is.False);
                        Assert.That(instance.transform.position, Is.EqualTo(position));
                    }
                    finally { UnityEngine.Object.Destroy(instance); }
                }
            }
            catch (Exception error) { failure = error; }
            yield return new ExitPlayMode();
            if (failure != null) throw failure;
        }

        [Test]
        public void ValidatorReportsEmptyAnimatorLayersWithoutThrowing()
        {
            var controller = Require<AnimatorController>(Base + "Shiba/AC_Shiba_Navigation.controller");
            var layers = controller.layers;
            var dirty = EditorUtility.IsDirty(controller);
            try
            {
                controller.layers = Array.Empty<AnimatorControllerLayer>();
                System.Collections.Generic.IReadOnlyList<string> issues = null;
                Assert.DoesNotThrow(() => issues = AnimalCafe.EditorTools.Phase11.Phase11NavigationValidator.ValidateCharacters());
                Assert.That(issues, Has.Some.Contains("Shiba: Animator needs Walk and Hold"));
            }
            finally
            {
                controller.layers = layers;
                if (!dirty) EditorUtility.ClearDirty(controller);
            }
        }

        [Test]
        public void FinalCharacterValidatorHasNoIssues()
        {
            Assert.That(AnimalCafe.EditorTools.Phase11.Phase11NavigationValidator.ValidateCharacters(), Is.Empty);
        }

        [Test]
        public void BothClipsIncludeActionLastFrame()
        {
            foreach (var character in Characters)
            {
                var folder = Base + character.name + "/";
                var modelPath = folder + "SM_" + character.name + "_Walk_Default.fbx";
                var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
                Assert.That(importer, Is.Not.Null, "Missing imported FBX: " + modelPath);
                Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.Generic));
                Assert.That(importer.importAnimation, Is.True);
                var clips = importer.clipAnimations;
                Assert.That(clips, Has.Length.EqualTo(1), character.name + " walk-only import");
                Assert.That(clips[0].firstFrame, Is.EqualTo(1f).Within(0.01f));
                Assert.That(clips[0].lastFrame, Is.EqualTo(21f).Within(0.01f));
                Assert.That(clips[0].loopTime, Is.True);
                var walk = System.Array.Find(AssetDatabase.LoadAllAssetsAtPath(modelPath),
                    asset => asset is AnimationClip && asset.name == "Walk") as AnimationClip;
                Assert.That(walk, Is.Not.Null, character.name + " Walk clip");
                Assert.That(walk.length, Is.EqualTo(20f / character.fps).Within(0.04f));
                var bindings = AnimationUtility.GetCurveBindings(walk);
                Assert.That(bindings, Is.Not.Empty);
                var lastKey = 0f;
                foreach (var binding in bindings)
                {
                    var curve = AnimationUtility.GetEditorCurve(walk, binding);
                    Assert.That(curve.length, Is.GreaterThan(0));
                    lastKey = Mathf.Max(lastKey, curve.keys[curve.length - 1].time);
                    Assert.That(curve.Evaluate(walk.length), Is.EqualTo(curve.Evaluate(0)).Within(.001f),
                        character.name + " loop seam: " + binding.path + "/" + binding.propertyName);
                }
                Assert.That(lastKey, Is.EqualTo(20f / character.fps).Within(.001f), character.name + " actual last imported key");
                var controllerPath = folder + "AC_" + character.name + "_Navigation.controller";
                var controller = Require<AnimatorController>(controllerPath);
                var states = controller.layers[0].stateMachine.states;
                Assert.That(states, Has.Length.EqualTo(2));
                Assert.That(System.Array.Exists(states, state => state.state.name == "Walk" && state.state.motion == walk), Is.True);
                var hold = System.Array.Find(AssetDatabase.LoadAllAssetsAtPath(controllerPath),
                    asset => asset is AnimationClip && asset.name == "Hold") as AnimationClip;
                Assert.That(hold, Is.Not.Null, character.name + " Hold subasset");
                Assert.That(System.Array.Exists(states, state => state.state.name == "Hold" && state.state.motion == hold), Is.True);
                var holdBindings = AnimationUtility.GetCurveBindings(hold);
                Assert.That(holdBindings, Is.Not.Empty, character.name + " Hold pose curves");
                foreach (var binding in holdBindings)
                {
                    var holdCurve = AnimationUtility.GetEditorCurve(hold, binding);
                    var walkCurve = AnimationUtility.GetEditorCurve(walk, binding);
                    Assert.That(walkCurve, Is.Not.Null, character.name + " Hold comes from Walk");
                    Assert.That(holdCurve.Evaluate(0f), Is.EqualTo(walkCurve.Evaluate(0f)).Within(0.0001f));
                    Assert.That(holdCurve.Evaluate(1f / character.fps), Is.EqualTo(holdCurve.Evaluate(0f)).Within(0.0001f));
                }
            }
        }

        [TestCase("Shiba", 30f)]
        [TestCase("Westie", 24f)]
        public void ImportedWalkHasAmplifiedLimbSwing(string character, float fps)
        {
            var modelPath = Base + character + "/SM_" + character + "_Walk_Default.fbx";
            var model = Require<GameObject>(modelPath);
            var walk = FindWalk(modelPath);
            Assert.That(walk, Is.Not.Null, character + " Walk clip");

            // 原始 X 轴摆动幅度 ×1.5；检查导入后的实际骨骼姿势，而非源曲线名称。
            // Original X-axis swing ×1.5; sample the imported bone poses, not source curve names.
            var expectedRanges = new (string bone, float degrees)[]
            {
                ("upper_arm.L", 24f), ("upper_arm.R", 24f),
                ("thigh.L", 30f), ("thigh.R", 30f),
                ("shin.L", 18f), ("shin.R", 18f),
                ("foot.L", 18.375f), ("foot.R", 18.375f)
            };
            var instance = PrefabUtility.InstantiatePrefab(model) as GameObject;
            Assert.That(instance, Is.Not.Null, character + " model instance");
            try
            {
                var transforms = instance.GetComponentsInChildren<Transform>(true);
                var bones = new Transform[expectedRanges.Length];
                for (var i = 0; i < bones.Length; i++)
                {
                    var boneName = expectedRanges[i].bone;
                    bones[i] = Array.Find(transforms, transform => transform.name == boneName);
                    Assert.That(bones[i], Is.Not.Null, character + " missing bone " + boneName);
                }

                // 21 帧覆盖完整 walk cycle，包括与首帧相接的末帧。
                // Sample all 21 frames, including the final loop-seam frame.
                var poses = new Quaternion[bones.Length, 21];
                for (var frame = 0; frame <= 20; frame++)
                {
                    walk.SampleAnimation(instance, frame / fps);
                    for (var bone = 0; bone < bones.Length; bone++)
                        poses[bone, frame] = bones[bone].localRotation;
                }

                var angularRanges = new float[bones.Length];
                for (var bone = 0; bone < bones.Length; bone++)
                {
                    for (var first = 0; first <= 20; first++)
                        for (var second = first + 1; second <= 20; second++)
                            angularRanges[bone] = Mathf.Max(angularRanges[bone],
                                Quaternion.Angle(poses[bone, first], poses[bone, second]));
                }

                // 源曲线精确放大 1.5 倍；Unity FBX 重采样/压缩可能略改导入后的峰值。
                // Source curves are exactly 1.5x; Unity FBX resampling/compression can shift imported peaks slightly.
                var errors = new System.Collections.Generic.List<string>();
                for (var bone = 0; bone < bones.Length; bone++)
                {
                    if (Mathf.Abs(angularRanges[bone] - expectedRanges[bone].degrees) > 1f)
                        errors.Add(character + "/" + expectedRanges[bone].bone + " expected " +
                            expectedRanges[bone].degrees.ToString("F3") + " deg, actual " +
                            angularRanges[bone].ToString("F3") + " deg");
                }
                Assert.That(errors, Is.Empty, string.Join(" | ", errors));
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        private static T Require<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, "Missing imported asset: " + path);
            return asset;
        }

        private static AnimationClip FindWalk(string modelPath)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(modelPath))
                if (asset is AnimationClip clip && clip.name == "Walk") return clip;
            return null;
        }

        private static Bounds BakedBounds(SkinnedMeshRenderer renderer)
        {
            var mesh = new Mesh();
            try
            {
                renderer.BakeMesh(mesh, true); // Compensate renderer scale before localToWorldMatrix.
                var vertices = mesh.vertices;
                Assert.That(vertices, Is.Not.Empty);
                var matrix = renderer.localToWorldMatrix;
                var bounds = new Bounds(matrix.MultiplyPoint3x4(vertices[0]), Vector3.zero);
                for (var i = 1; i < vertices.Length; i++)
                    bounds.Encapsulate(matrix.MultiplyPoint3x4(vertices[i]));
                return bounds;
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }
    }
}
