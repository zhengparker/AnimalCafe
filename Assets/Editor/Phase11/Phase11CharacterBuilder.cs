using System;
using System.IO;
using AnimalCafe.Navigation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

namespace AnimalCafe.EditorTools.Phase11
{
    public static class Phase11CharacterBuilder
    {
        public const string CharacterRoot = "Assets/Art/Phase11/Characters";

        // Command-line entry point; the public Build method also works from Editor tools.
        public static void BuildFromCommandLine()
        {
            var sourceRoot = Environment.GetEnvironmentVariable("ANIMALCAFE_SOURCE_PROJECT_ROOT");
            if (string.IsNullOrWhiteSpace(sourceRoot))
                throw new InvalidOperationException("Set ANIMALCAFE_SOURCE_PROJECT_ROOT before building characters.");
            Build(sourceRoot);
        }

        public static void Build(string sourceProjectRoot)
        {
            if (string.IsNullOrWhiteSpace(sourceProjectRoot) || !Directory.Exists(sourceProjectRoot))
                throw new ArgumentException("A valid source project root is required.", nameof(sourceProjectRoot));
            BuildOne("Shiba", 30, sourceProjectRoot);
            BuildOne("Westie", 24, sourceProjectRoot);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            var issues = Phase11NavigationValidator.ValidateCharacters();
            if (issues.Count != 0)
                throw new InvalidOperationException("Character validation failed: " + string.Join(" | ", issues));
            Debug.Log("AC_PHASE11_CHARACTERS_BUILT Shiba Westie");
        }

        private static void BuildOne(string name, int fps, string sourceRoot)
        {
            var source = Path.Combine(sourceRoot, "Blender Model Item", name,
                "AnimalCafe_" + name + "_Walk_Default_v01.blend");
            if (!File.Exists(source)) throw new FileNotFoundException("Original walk source is missing", source);
            var folder = CharacterRoot + "/" + name;
            var modelPath = folder + "/SM_" + name + "_Walk_Default.fbx";
            var texturePath = folder + "/T_" + name + "_BaseColor.png";
            if (!File.Exists(modelPath) || !File.Exists(texturePath))
                throw new FileNotFoundException("Run ExportNavigationCharacters.py first: " + folder);
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceSynchronousImport);
            ConfigureImporter(modelPath);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (texture == null || model == null) throw new InvalidOperationException(name + " import failed");
            var walk = FindWalk(modelPath);
            if (walk == null) throw new InvalidOperationException(name + " has no imported Walk clip");
            var material = BuildMaterial(folder, name, texture);
            var controller = BuildController(folder, name, walk, fps);
            BuildPrefab(folder, name, model, material, controller, walk);
            Debug.Log("AC_PHASE11_CHARACTER " + name + " clip=" + walk.length.ToString("F6") +
                " expected=" + (20f / fps).ToString("F6"));
        }

        private static void ConfigureImporter(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Missing ModelImporter: " + path);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            // Preserve FBX meter size; BakeMesh measurement compensates renderer scale once.
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            var sourceClips = importer.defaultClipAnimations;
            if (sourceClips.Length == 0) throw new InvalidOperationException("No walk in " + path);
            var clip = sourceClips[0];
            clip.name = "Walk";
            clip.firstFrame = 1f;
            clip.lastFrame = 21f;
            clip.loopTime = true;
            clip.loopPose = true;
            importer.clipAnimations = new[] { clip };
            importer.SaveAndReimport();
        }

        private static AnimationClip FindWalk(string path)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is AnimationClip clip && clip.name == "Walk") return clip;
            return null;
        }

        private static Material BuildMaterial(string folder, string name, Texture2D texture)
        {
            var path = folder + "/M_" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("URP Lit shader is unavailable");
                material = new Material(shader) { name = "M_" + name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static AnimatorController BuildController(string folder, string name,
            AnimationClip walk, int fps)
        {
            var path = folder + "/AC_" + name + "_Navigation.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var hold = FindHold(path);
            if (hold == null)
            {
                hold = new AnimationClip { name = "Hold", frameRate = fps };
                AssetDatabase.AddObjectToAsset(hold, controller);
            }
            // Copy the walk's first sampled pose into a short, stable Hold clip.
            foreach (var binding in AnimationUtility.GetCurveBindings(hold))
                AnimationUtility.SetEditorCurve(hold, binding, null);
            foreach (var binding in AnimationUtility.GetCurveBindings(walk))
            {
                var curve = AnimationUtility.GetEditorCurve(walk, binding);
                var value = curve.Evaluate(0f);
                AnimationUtility.SetEditorCurve(hold, binding,
                    AnimationCurve.Constant(0f, 1f / fps, value));
            }
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(walk))
            {
                var keys = AnimationUtility.GetObjectReferenceCurve(walk, binding);
                if (keys.Length == 0) continue;
                var first = new ObjectReferenceKeyframe { time = 0f, value = keys[0].value };
                AnimationUtility.SetObjectReferenceCurve(hold, binding, new[] { first });
            }
            var stateMachine = controller.layers[0].stateMachine;
            foreach (var state in stateMachine.states)
                stateMachine.RemoveState(state.state);
            var holdState = stateMachine.AddState("Hold");
            holdState.motion = hold;
            var walkState = stateMachine.AddState("Walk");
            walkState.motion = walk;
            stateMachine.defaultState = holdState;
            EditorUtility.SetDirty(hold);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static AnimationClip FindHold(string controllerPath)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(controllerPath))
                if (asset is AnimationClip clip && clip.name == "Hold") return clip;
            return null;
        }

        private static void BuildPrefab(string folder, string name, GameObject model,
            Material material, RuntimeAnimatorController controller, AnimationClip walk)
        {
            var root = new GameObject("PF_" + name + "_Navigation");
            try
            {
                var settings = new NavigationSettings();
                var agent = root.AddComponent<NavMeshAgent>();
                agent.radius = settings.AgentRadius;
                agent.height = settings.CapsuleHeight;
                agent.speed = settings.MaxSpeed;
                agent.angularSpeed = settings.TurnSpeedDegrees;
                agent.stoppingDistance = settings.ArrivalDistance;
                agent.updatePosition = false;
                agent.updateRotation = false;
                agent.autoRepath = false;
                agent.autoTraverseOffMeshLink = false;
                // Task 3 World enables Agent only after validating its authored start point.
                agent.enabled = false;
                var proxy = root.AddComponent<CapsuleCollider>();
                proxy.radius = settings.AgentRadius;
                proxy.height = settings.CapsuleHeight;
                proxy.center = Vector3.up * settings.CapsuleHeight * 0.5f;
                var modelRoot = new GameObject("ModelRoot").transform;
                modelRoot.SetParent(root.transform, false);
                // Source visual front is -Z; align it with NavigationActor +Z without changing source bones.
                modelRoot.localRotation = Quaternion.Euler(0, 180, 0);
                var instance = PrefabUtility.InstantiatePrefab(model) as GameObject;
                if (instance == null) throw new InvalidOperationException("Could not instantiate " + name);
                instance.transform.SetParent(modelRoot, false);
                foreach (var bodyRenderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
                    bodyRenderer.sharedMaterial = material;
                var animator = instance.GetComponent<Animator>() ?? instance.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.updateMode = AnimatorUpdateMode.Normal;
                // Calibrate the child once in the prefab; runtime actor position stays untouched.
                walk.SampleAnimation(instance, 0f);
                var renderer = instance.GetComponentInChildren<SkinnedMeshRenderer>();
                if (renderer == null) throw new InvalidOperationException(name + " has no body Renderer");
                var bounds = BakedBounds(renderer);
                modelRoot.localPosition = Vector3.up * -bounds.min.y;
                var actor = root.AddComponent<NavigationActor>();
                actor.Configure(name, agent, proxy, animator, modelRoot);
                root.AddComponent<NavigationWalkPresenter>();
                var path = folder + "/PF_" + name + "_Navigation.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, path, out var saved);
                if (!saved) throw new InvalidOperationException("Could not save " + path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Bounds BakedBounds(SkinnedMeshRenderer renderer)
        {
            var mesh = new Mesh();
            try
            {
                renderer.BakeMesh(mesh, true); // Compensate renderer scale before localToWorldMatrix.
                var vertices = mesh.vertices;
                if (vertices.Length == 0) throw new InvalidOperationException("Empty skinned body Mesh");
                var matrix = renderer.localToWorldMatrix;
                var result = new Bounds(matrix.MultiplyPoint3x4(vertices[0]), Vector3.zero);
                for (var i = 1; i < vertices.Length; i++)
                    result.Encapsulate(matrix.MultiplyPoint3x4(vertices[i]));
                return result;
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }
    }
}
