using System.Collections.Generic;
using AnimalCafe.Navigation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AnimalCafe.EditorTools.Phase11
{
    public static class Phase11NavigationValidator
    {
        // Empty list means the generated character assets have no detected configuration errors.
        public static IReadOnlyList<string> ValidateCharacters()
        {
            var issues = new List<string>();
            ValidateOne("Shiba", 30f, issues);
            ValidateOne("Westie", 24f, issues);
            return issues;
        }

        private static void ValidateOne(string name, float fps, List<string> issues)
        {
            var folder = Phase11CharacterBuilder.CharacterRoot + "/" + name + "/";
            var fbxPath = folder + "SM_" + name + "_Walk_Default.fbx";
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "T_" + name + "_BaseColor.png");
            var material = AssetDatabase.LoadAssetAtPath<Material>(folder + "M_" + name + ".mat");
            var controllerPath = folder + "AC_" + name + "_Navigation.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "PF_" + name + "_Navigation.prefab");
            if (importer == null) issues.Add(name + ": missing FBX importer");
            if (texture == null) issues.Add(name + ": missing BaseColor texture");
            if (material == null) issues.Add(name + ": missing URP material");
            if (controller == null) issues.Add(name + ": missing AnimatorController");
            if (prefab == null) issues.Add(name + ": missing Navigation prefab");
            if (importer == null || texture == null || material == null || controller == null || prefab == null)
                return;

            if (importer.animationType != ModelImporterAnimationType.Generic || !importer.importAnimation)
                issues.Add(name + ": walk must import as Generic animation");
            if (importer.clipAnimations.Length != 1 ||
                Mathf.Abs(importer.clipAnimations[0].firstFrame - 1f) > 0.01f ||
                Mathf.Abs(importer.clipAnimations[0].lastFrame - 21f) > 0.01f ||
                !importer.clipAnimations[0].loopTime)
                issues.Add(name + ": Walk clip must loop frames 1–21");
            AnimationClip walk = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
                if (asset is AnimationClip clip && clip.name == "Walk") walk = clip;
            if (walk == null || Mathf.Abs(walk.length - 20f / fps) > 0.04f)
                issues.Add(name + ": Walk clip duration does not match source fps");
            if (material.shader == null || material.shader.name != "Universal Render Pipeline/Lit" ||
                material.GetTexture("_BaseMap") != texture)
                issues.Add(name + ": URP material texture reference is invalid");
            AnimationClip hold = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(controllerPath))
                if (asset is AnimationClip clip && clip.name == "Hold") hold = clip;
            var layers = controller.layers;
            var states = layers.Length > 0 && layers[0].stateMachine != null
                ? layers[0].stateMachine.states : System.Array.Empty<ChildAnimatorState>();
            if (hold == null || states.Length != 2)
                issues.Add(name + ": Animator needs Walk and Hold");
            else
            {
                var hasWalk = false;
                var hasHold = false;
                foreach (var state in states)
                {
                    if (state.state.name == "Walk" && state.state.motion == walk) hasWalk = true;
                    if (state.state.name == "Hold" && state.state.motion == hold) hasHold = true;
                }
                if (!hasWalk || !hasHold) issues.Add(name + ": Animator motion references are invalid");
            }

            var actor = prefab.GetComponent<NavigationActor>();
            if (actor == null || actor.ActorId != name || actor.Agent == null || actor.Proxy == null ||
                actor.ModelAnimator == null || actor.ModelRoot == null)
            {
                issues.Add(name + ": NavigationActor references are incomplete");
                return;
            }
            if (prefab.transform.localScale != Vector3.one || actor.ModelRoot.localScale != Vector3.one ||
                Mathf.Abs(actor.Proxy.radius - 0.45f) > 0.001f ||
                Mathf.Abs(actor.Proxy.height - 1.30f) > 0.001f ||
                Mathf.Abs(actor.Agent.radius - 0.45f) > 0.001f ||
                Mathf.Abs(actor.Agent.height - 1.30f) > 0.001f)
                issues.Add(name + ": model scale or collision dimensions are invalid");
            // updatePosition/updateRotation are runtime-only; NavigationActor.Awake applies them.
            if (actor.Agent.enabled || actor.Agent.autoRepath || actor.Agent.autoTraverseOffMeshLink ||
                actor.ModelAnimator.applyRootMotion)
                issues.Add(name + ": automatic transform or root motion is enabled");
            var renderer = actor.ModelRoot.GetComponentInChildren<SkinnedMeshRenderer>();
            if (renderer == null || renderer.sharedMaterial != material)
                issues.Add(name + ": body renderer material is invalid");
            if (renderer != null && walk != null)
            {
                var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                try
                {
                    var instanceActor = instance.GetComponent<NavigationActor>();
                    walk.SampleAnimation(instanceActor.ModelAnimator.gameObject, 0f);
                    var bounds = BakedBounds(instanceActor.ModelRoot.GetComponentInChildren<SkinnedMeshRenderer>());
                    if (bounds.size.y < 1.27f || bounds.size.y > 1.34f || Mathf.Abs(bounds.min.y) > 0.02f)
                        issues.Add(name + ": baked body height/feet differ from original ~1.30 m (height=" +
                            bounds.size.y.ToString("F3") + ", foot=" + bounds.min.y.ToString("F3") + ")");
                }
                finally { Object.DestroyImmediate(instance); }
            }
        }

        private static Bounds BakedBounds(SkinnedMeshRenderer renderer)
        {
            var mesh = new Mesh();
            try
            {
                renderer.BakeMesh(mesh, true); // Compensate renderer scale before localToWorldMatrix.
                var vertices = mesh.vertices;
                if (vertices.Length == 0) throw new System.InvalidOperationException("Empty body Mesh");
                var matrix = renderer.localToWorldMatrix;
                var result = new Bounds(matrix.MultiplyPoint3x4(vertices[0]), Vector3.zero);
                for (var i = 1; i < vertices.Length; i++)
                    result.Encapsulate(matrix.MultiplyPoint3x4(vertices[i]));
                return result;
            }
            finally { Object.DestroyImmediate(mesh); }
        }
    }
}
