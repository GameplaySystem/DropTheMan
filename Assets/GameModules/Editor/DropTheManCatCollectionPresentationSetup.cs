using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DropAwayPrototype.Runtime;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace DropAwayPrototype.Editor
{
    /// <summary>
    /// Repeatable editor setup for the runtime cat collection presentation prefab and controller.
    /// </summary>
    internal static class DropTheManCatCollectionPresentationSetup
    {
        private const string ModelPath = "Assets/RuntimeAssets/RuntimeModels/Cat.fbx";
        private const string AnimationPath = ModelPath;
        private const string IdleClipName = "Idle_1";
        private const string PrefabPath =
            "Assets/RuntimeAssets/RuntimePrefabs/CatCollectible.prefab";
        private const string AnimationFolder = "Assets/RuntimeAssets/Animations";
        private const string ControllerPath =
            AnimationFolder + "/CatCollection.controller";
        private const string SkeletonRootName = "Armature";
        private const bool BakeAxisConversion = true;

        private static readonly string[] FallingClipNames =
        {
            "Jump_1", "Jump_2", "Jump_3", "Jump_4", "Jump_5", "Jump_6"
        };

        private static readonly string[] StateNames =
        {
            "Fall_1",
            "Fall_2",
            "Fall_3",
            "Fall_4",
            "Fall_5",
            "Fall_6"
        };

        [MenuItem("Tools/Drop The Man/Configure Cat Collection Presentation")]
        public static void Configure()
        {
            Avatar avatar = ConfigureModelImporters();
            AnimationClip idleClip = LoadAnimationClip(IdleClipName);
            AnimationClip[] clips = LoadFallingClips();
            AnimatorController controller = ConfigureController(idleClip, clips);
            ConfigurePrefab(controller, avatar, clips);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "Configured CatCollectible prefab and CatCollection Animator Controller.");
        }

        [MenuItem("Tools/Drop The Man/Validate Cat Falling Clip Sampling")]
        public static void ValidateFallingClipSampling()
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Animator animator = prefabRoot.GetComponentInChildren<Animator>(includeInactive: true);
                DropTheManCatCollectionPresentation presentation =
                    prefabRoot.GetComponent<DropTheManCatCollectionPresentation>();
                if (animator == null || presentation == null || animator.avatar == null ||
                    AssetDatabase.GetAssetPath(animator.avatar) != ModelPath ||
                    animator.runtimeAnimatorController is not AnimatorController controller)
                {
                    throw new InvalidOperationException(
                        "CatCollectible requires the delivered model's Avatar, controller, and presentation.");
                }

                AnimationClip idle = LoadAnimationClip(IdleClipName);
                AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
                if (!idle.isLooping || stateMachine.defaultState?.motion != idle)
                    throw new InvalidOperationException("Idle must be the default looping state.");

                SerializedProperty clips = new SerializedObject(presentation).FindProperty("fallingClips");
                if (clips.arraySize != FallingClipNames.Length)
                    throw new InvalidOperationException("The prefab falling clip count is out of date.");

                HashSet<Hash128> sequences = new();
                float[] sampleSeconds = { 0f, 0.15f, 0.35f, 0.6f };
                for (int i = 0; i < FallingClipNames.Length; i++)
                {
                    AnimationClip clip = LoadAnimationClip(FallingClipNames[i]);
                    AnimatorState state = stateMachine.states.Select(child => child.state)
                        .Single(candidate => candidate.name == StateNames[i]);
                    if (clips.GetArrayElementAtIndex(i).objectReferenceValue != clip ||
                        state.motion != clip || clip.isLooping || clip.length <= 0f)
                        throw new InvalidOperationException($"Invalid motion mapping for '{StateNames[i]}'.");

                    foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
                    {
                        if (binding.path.Length > 0 && animator.transform.Find(binding.path) == null)
                            throw new InvalidOperationException($"Missing animated path '{binding.path}'.");
                    }

                    StringBuilder sequence = new();
                    foreach (float seconds in sampleSeconds)
                    {
                        animator.Rebind();
                        animator.Update(0f);
                        int hash = Animator.StringToHash($"Base Layer.{StateNames[i]}");
                        animator.Play(hash, 0, Mathf.Min(seconds / clip.length, 0.99f));
                        animator.Update(0f);
                        AnimatorClipInfo[] active = animator.GetCurrentAnimatorClipInfo(0);
                        if (animator.GetCurrentAnimatorStateInfo(0).fullPathHash != hash ||
                            active.Length != 1 || active[0].clip != clip || active[0].weight < 0.999f)
                            throw new InvalidOperationException($"'{StateNames[i]}' did not play '{clip.name}' alone.");
                        sequence.Append(BuildSkinnedMeshSignature(animator.transform));
                    }

                    // This catches identical deformation, not artistic readability. Also review
                    // matched early poses and real collection while the cat is still visible.
                    if (!sequences.Add(Hash128.Compute(sequence.ToString())))
                        throw new InvalidOperationException($"'{clip.name}' duplicates another early mesh sequence.");
                    Debug.Log($"Validated {StateNames[i]} -> {clip.name}, {clip.length:F3}s, early mesh samples differ.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static Hash128 BuildSkinnedMeshSignature(Transform animatorRoot)
        {
            StringBuilder builder = new();
            foreach (SkinnedMeshRenderer renderer in
                     animatorRoot.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true))
            {
                Mesh bakedMesh = new();
                try
                {
                    renderer.BakeMesh(bakedMesh);
                    foreach (Vector3 vertex in bakedMesh.vertices)
                        builder.Append(vertex.x).Append('|').Append(vertex.y).Append('|').Append(vertex.z).Append(';');
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(bakedMesh);
                }
            }

            return Hash128.Compute(builder.ToString());
        }

        private static Avatar ConfigureModelImporters()
        {
            ModelImporter modelImporter = GetModelImporter(ModelPath);
            modelImporter.animationType = ModelImporterAnimationType.Generic;
            modelImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            modelImporter.sourceAvatar = null;
            modelImporter.bakeAxisConversion = BakeAxisConversion;
            modelImporter.importAnimation = true;
            SetGenericRootNode(modelImporter, SkeletonRootName);
            ConfigureClipImports(modelImporter);
            modelImporter.SaveAndReimport();

            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath)
                .OfType<Avatar>()
                .FirstOrDefault();
            if (avatar == null || !avatar.isValid)
            {
                throw new InvalidOperationException(
                    $"'{ModelPath}' did not generate a valid Generic Avatar.");
            }

            return avatar;
        }

        private static void ConfigureClipImports(ModelImporter importer)
        {
            // Keep authored trims for existing takes, but never retain entries for renamed or
            // removed takes: Unity silently imports no clip when takeName no longer exists.
            ModelImporterClipAnimation[] defaults = importer.defaultClipAnimations;
            ModelImporterClipAnimation[] authored = importer.clipAnimations;
            foreach (string required in FallingClipNames.Prepend(IdleClipName))
                if (!defaults.Any(clip => clip.takeName == required))
                    throw new InvalidOperationException($"'{AnimationPath}' is missing take '{required}'.");

            List<ModelImporterClipAnimation> clips = authored.Where(clip =>
                defaults.Any(take => take.takeName == clip.takeName)).ToList();
            foreach (ModelImporterClipAnimation take in defaults)
            {
                if (!clips.Any(clip => clip.takeName == take.takeName && clip.name == take.name))
                    clips.Add(take);
            }

            foreach (ModelImporterClipAnimation clip in clips)
            {
                clip.loopTime = clip.takeName.StartsWith("Idle_", StringComparison.Ordinal);
                clip.loopPose = clip.loopTime;
            }
            importer.clipAnimations = clips.ToArray();
        }

        private static ModelImporter GetModelImporter(string path)
        {
            if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
            {
                throw new InvalidOperationException(
                    $"A model importer is required at '{path}'.");
            }

            return importer;
        }

        private static void SetGenericRootNode(ModelImporter importer, string rootNodeName)
        {
            SerializedObject importerObject = new(importer);
            SerializedProperty rootNode = importerObject.FindProperty(
                "m_HumanDescription.m_RootMotionBoneName");
            if (rootNode == null)
            {
                throw new InvalidOperationException(
                    $"Could not configure the Generic root node for '{importer.assetPath}'.");
            }

            rootNode.stringValue = rootNodeName;
            importerObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static AnimationClip[] LoadFallingClips()
        {
            AnimationClip[] result = new AnimationClip[FallingClipNames.Length];
            for (int i = 0; i < FallingClipNames.Length; i++)
            {
                result[i] = LoadAnimationClip(FallingClipNames[i]);
            }

            return result;
        }

        private static AnimationClip LoadAnimationClip(string name)
        {
            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(AnimationPath)
                .OfType<AnimationClip>()
                .Where(candidate => candidate.name == name)
                .ToArray();
            if (clips.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Expected exactly one '{name}' clip in '{AnimationPath}', found {clips.Length}.");
            }

            return clips[0];
        }

        private static AnimatorController ConfigureController(
            AnimationClip idleClip,
            AnimationClip[] clips)
        {
            if (!AssetDatabase.IsValidFolder(AnimationFolder))
            {
                AssetDatabase.CreateFolder("Assets/RuntimeAssets", "Animations");
            }

            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState idleState = FindOrAddState(stateMachine, "Idle", new Vector3(40f, 80f));
            idleState.motion = idleClip;
            idleState.writeDefaultValues = false;
            stateMachine.defaultState = idleState;

            for (int i = 0; i < StateNames.Length; i++)
            {
                AnimatorState state = FindOrAddState(
                    stateMachine,
                    StateNames[i],
                    new Vector3(280f, 80f + i * 70f));
                state.motion = clips[i];
                state.writeDefaultValues = false;
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static AnimatorState FindOrAddState(
            AnimatorStateMachine stateMachine,
            string stateName,
            Vector3 position)
        {
            return stateMachine.states.Select(child => child.state)
                .FirstOrDefault(state => state.name == stateName)
                ?? stateMachine.AddState(stateName, position);
        }

        private static void ConfigurePrefab(
            AnimatorController controller,
            Avatar avatar,
            AnimationClip[] fallingClips)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Transform presentationRoot = prefabRoot.transform.Find("PresentationRoot");
                if (presentationRoot == null || presentationRoot.childCount == 0)
                {
                    throw new InvalidOperationException(
                        "CatCollectible prefab requires PresentationRoot with a cat model child.");
                }

                Transform previousModelRoot = presentationRoot.GetChild(0);
                Renderer[] previousRenderers = previousModelRoot
                    .GetComponentsInChildren<Renderer>(includeInactive: true);
                Material[][] previousMaterials = previousRenderers
                    .Select(renderer => renderer.sharedMaterials)
                    .ToArray();
                int modelSiblingIndex = previousModelRoot.GetSiblingIndex();

                GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
                if (modelAsset == null)
                {
                    throw new InvalidOperationException(
                        $"Could not load the canonical cat model at '{ModelPath}'.");
                }

                GameObject catModel = previousModelRoot.gameObject;
                if (PrefabUtility.GetCorrespondingObjectFromSource(catModel) != modelAsset)
                {
                    catModel = PrefabUtility.InstantiatePrefab(
                        modelAsset,
                        prefabRoot.scene) as GameObject;
                    if (catModel == null)
                    {
                        throw new InvalidOperationException(
                            "Could not instantiate the canonical cat model in CatCollectible.");
                    }

                    catModel.transform.SetParent(presentationRoot, worldPositionStays: false);
                    catModel.transform.SetSiblingIndex(modelSiblingIndex);
                    catModel.name = "Cat";
                    UnityEngine.Object.DestroyImmediate(previousModelRoot.gameObject);
                }

                Renderer[] catRenderers = catModel
                    .GetComponentsInChildren<Renderer>(includeInactive: true);
                for (int i = 0; i < Mathf.Min(previousMaterials.Length, catRenderers.Length); i++)
                {
                    if (previousMaterials[i].Length == catRenderers[i].sharedMaterials.Length)
                    {
                        catRenderers[i].sharedMaterials = previousMaterials[i];
                    }
                }

                Animator presentationAnimator = presentationRoot.GetComponent<Animator>();
                if (presentationAnimator != null)
                {
                    UnityEngine.Object.DestroyImmediate(presentationAnimator);
                }

                Animator animator = catModel.GetComponent<Animator>();
                if (animator == null)
                {
                    animator = catModel.AddComponent<Animator>();
                }

                animator.runtimeAnimatorController = controller;
                animator.avatar = avatar;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                DropTheManCatCollectionPresentation presentation =
                    prefabRoot.GetComponent<DropTheManCatCollectionPresentation>();
                if (presentation == null)
                {
                    presentation = prefabRoot.AddComponent<DropTheManCatCollectionPresentation>();
                }
                SerializedObject presentationObject = new(presentation);
                presentationObject.FindProperty("presentationRoot").objectReferenceValue =
                    presentationRoot;
                presentationObject.FindProperty("animator").objectReferenceValue = animator;
                SerializedProperty serializedFallingClips =
                    presentationObject.FindProperty("fallingClips");
                serializedFallingClips.arraySize = fallingClips.Length;
                for (int i = 0; i < fallingClips.Length; i++)
                {
                    serializedFallingClips.GetArrayElementAtIndex(i).objectReferenceValue =
                        fallingClips[i];
                }

                presentationObject.ApplyModifiedPropertiesWithoutUndo();

                DropTheManStickmanView view =
                    prefabRoot.GetComponent<DropTheManStickmanView>();
                if (view == null)
                {
                    throw new InvalidOperationException(
                        "CatCollectible prefab requires DropTheManStickmanView.");
                }

                SerializedObject viewObject = new(view);
                viewObject.FindProperty("collectionPresentation").objectReferenceValue =
                    presentation;
                SerializedProperty renderersToHide =
                    viewObject.FindProperty("renderersToHide");
                renderersToHide.arraySize = catRenderers.Length;
                for (int i = 0; i < catRenderers.Length; i++)
                {
                    renderersToHide.GetArrayElementAtIndex(i).objectReferenceValue =
                        catRenderers[i];
                }
                viewObject.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
    }
}
