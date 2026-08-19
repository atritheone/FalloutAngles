// imports
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;



// class
public class AttachmentSkinner : EditorWindow
{
    // constants
    private const string DefaultOutputFolder = "Assets/Generated/AttachmentSkinner/SkinnedAttachments";

    private static readonly string[] DefaultTargetKeywords =
    {
        "hair",
        "beard",
        "brow",
        "eyebrow",
        "lash",
        "eyelash"
    };



    // variables
    [SerializeField] private GameObject prefabAsset;
    [SerializeField] private GameObject sceneRoot;
    [SerializeField] private DefaultAsset outputFolder;
    [SerializeField] private SkinnedMeshRenderer donorRendererOverride;
    [SerializeField] private string donorRendererName = "";
    [SerializeField] private bool autoPickDonorRenderer = true;
    [SerializeField] private bool useExplicitTargetNames = false;
    [SerializeField] private string explicitTargetNames = "";
    [SerializeField] private string targetKeywords = "hair\nbeard\nbrow\neyebrow\nlash\neyelash";
    [SerializeField] private bool transferBlendShapes = true;
    [SerializeField] private bool reparentToDonorContainer = true;

    private string status = "Ready.";



    // methods
    [MenuItem("Tools/Fallout Angles/Attachment Skinner/Open Window")]
    private static void OpenWindow()
    {
        GetWindow<AttachmentSkinner>("Attachment Skinner");
    }


    [MenuItem("Tools/Fallout Angles/Attachment Skinner/Skin Selected Scene Model")]
    private static void SkinSelectedSceneModel()
    {
        GameObject selected = Selection.activeGameObject;

        if (selected == null)
        {
            Debug.LogError("Select a scene model root, or one of its children, before running the attachment skinner.");

            return;
        }

        GameObject root = PrefabUtility.GetNearestPrefabInstanceRoot(selected);

        if (root == null)
            root = selected.transform.root.gameObject;

        Debug.Log(SkinSceneInstance(root, DefaultOutputFolder, null, "", true, null, DefaultTargetKeywords, true, true));
    }


    private void OnEnable()
    {
        if (outputFolder == null)
            outputFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(DefaultOutputFolder);
    }


    private void OnGUI()
    {
        EditorGUILayout.LabelField("Attachment Skinner", EditorStyles.boldLabel);

        EditorGUILayout.Space();

        prefabAsset = (GameObject)EditorGUILayout.ObjectField("Prefab Asset", prefabAsset, typeof(GameObject), false);
        sceneRoot = (GameObject)EditorGUILayout.ObjectField("Scene Root", sceneRoot, typeof(GameObject), true);
        outputFolder = (DefaultAsset)EditorGUILayout.ObjectField("Output Folder", outputFolder, typeof(DefaultAsset), false);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Donor Skinned Mesh", EditorStyles.boldLabel);

        autoPickDonorRenderer = EditorGUILayout.Toggle("Auto Pick Donor", autoPickDonorRenderer);

        using (new EditorGUI.DisabledScope(autoPickDonorRenderer))
        {
            donorRendererOverride = (SkinnedMeshRenderer)EditorGUILayout.ObjectField("Donor Override", donorRendererOverride, typeof(SkinnedMeshRenderer), true);
            donorRendererName = EditorGUILayout.TextField("Donor Name Fallback", donorRendererName);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Targets", EditorStyles.boldLabel);

        useExplicitTargetNames = EditorGUILayout.Toggle("Use Explicit Names", useExplicitTargetNames);

        using (new EditorGUI.DisabledScope(!useExplicitTargetNames))
        {
            EditorGUILayout.LabelField("Explicit Target Names");
            explicitTargetNames = EditorGUILayout.TextArea(explicitTargetNames, GUILayout.MinHeight(64));
        }

        using (new EditorGUI.DisabledScope(useExplicitTargetNames))
        {
            EditorGUILayout.LabelField("Target Name Keywords");
            targetKeywords = EditorGUILayout.TextArea(targetKeywords, GUILayout.MinHeight(64));
        }

        transferBlendShapes = EditorGUILayout.Toggle("Transfer BlendShapes", transferBlendShapes);
        reparentToDonorContainer = EditorGUILayout.Toggle("Reparent To Donor Container", reparentToDonorContainer);

        EditorGUILayout.Space();

        string folderPath = outputFolder != null ? AssetDatabase.GetAssetPath(outputFolder) : DefaultOutputFolder;
        string[] targetNames = useExplicitTargetNames ? ParseLines(explicitTargetNames) : null;
        string[] keywords = ParseLines(targetKeywords);
        SkinnedMeshRenderer donorOverride = autoPickDonorRenderer ? null : donorRendererOverride;
        string donorName = autoPickDonorRenderer ? "" : donorRendererName;

        using (new EditorGUI.DisabledScope(prefabAsset == null))
        {
            if (GUILayout.Button("Skin Prefab Asset"))
            {
                string prefabPath = AssetDatabase.GetAssetPath(prefabAsset);

                status = SkinPrefab(prefabPath, folderPath, donorOverride, donorName, autoPickDonorRenderer, targetNames, keywords, transferBlendShapes, reparentToDonorContainer);
            }
        }

        using (new EditorGUI.DisabledScope(sceneRoot == null))
        {
            if (GUILayout.Button("Skin Scene Root"))
                status = SkinSceneInstance(sceneRoot, folderPath, donorOverride, donorName, autoPickDonorRenderer, targetNames, keywords, transferBlendShapes, reparentToDonorContainer);
        }

        if (sceneRoot != null)
        {
            if (GUILayout.Button("Preview Auto Targets In Console"))
                PreviewAutoTargets(sceneRoot, keywords);
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(status, MessageType.Info);
    }


    private static string SkinPrefab(string prefabPath, string outputFolderPath, SkinnedMeshRenderer donorOverride, string donorName, bool autoPickDonor, string[] targetNames, string[] keywords, bool copyBlendShapes, bool reparentTargets)
    {
        if (string.IsNullOrWhiteSpace(prefabPath) || AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            return "Prefab asset path is invalid: " + prefabPath;

        EnsureFolder(outputFolderPath);

        GameObject prefabRoot = null;

        try
        {
            prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

            if (prefabRoot == null)
                return "Failed to load prefab contents: " + prefabPath;

            string processResult = SkinRoot(prefabRoot, outputFolderPath, donorOverride, donorName, autoPickDonor, targetNames, keywords, copyBlendShapes, reparentTargets, true);

            if (IsBlockingResult(processResult))
                return processResult;

            EditorUtility.DisplayProgressBar("Skinning Attachments", "Saving prefab", 1f);

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return "Finished prefab asset.\n" + processResult;
        }
        finally
        {
            EditorUtility.ClearProgressBar();

            if (prefabRoot != null)
                PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }


    private static string SkinSceneInstance(GameObject instanceRoot, string outputFolderPath, SkinnedMeshRenderer donorOverride, string donorName, bool autoPickDonor, string[] targetNames, string[] keywords, bool copyBlendShapes, bool reparentTargets)
    {
        if (instanceRoot == null)
            return "No scene root was provided.";

        if (EditorUtility.IsPersistent(instanceRoot))
            return "Scene Root must be a scene object, not a prefab asset.";

        EnsureFolder(outputFolderPath);

        Undo.RegisterFullObjectHierarchyUndo(instanceRoot, "Skin Attachment Renderers");

        string result;

        try
        {
            result = SkinRoot(instanceRoot, outputFolderPath, donorOverride, donorName, autoPickDonor, targetNames, keywords, copyBlendShapes, reparentTargets, false);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        EditorUtility.SetDirty(instanceRoot);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return "Finished scene root.\n" + result;
    }


    private static string SkinRoot(GameObject root, string outputFolderPath, SkinnedMeshRenderer donorOverride, string donorName, bool autoPickDonor, string[] targetNames, string[] keywords, bool copyBlendShapes, bool reparentTargets, bool isPrefabAssetEdit)
    {
        if (root == null)
            return "Root is null.";

        SkinnedMeshRenderer donorRenderer = ResolveDonorRenderer(root, donorOverride, donorName, autoPickDonor, keywords);

        if (donorRenderer == null)
            return "Could not resolve a donor SkinnedMeshRenderer. Pick one manually, or make sure the model has a skinned body renderer.";

        Mesh donorMesh = donorRenderer.sharedMesh;

        if (donorMesh == null)
            return "Donor renderer has no shared mesh: " + donorRenderer.name;

        if (donorRenderer.bones == null || donorRenderer.bones.Length == 0)
            return "Donor renderer has no bones: " + donorRenderer.name;

        List<Transform> targets = ResolveTargets(root, donorRenderer, targetNames, keywords);
        List<Transform> alreadySkinnedAutoTargets = targetNames == null || targetNames.Length == 0 ? ResolveAlreadySkinnedKeywordTargets(root, donorRenderer, keywords) : new List<Transform>();

        if (targets.Count == 0)
        {
            if (alreadySkinnedAutoTargets.Count > 0)
                return "No unskinned target MeshRenderers were found. Already skinned attachments skipped: " + alreadySkinnedAutoTargets.Count + ".";

            return "No target MeshRenderers were found. Add explicit names or adjust the target keywords.";
        }

        Debug.Log("AttachmentSkinner: root=" + root.name + " donor=" + donorRenderer.name + " vertices=" + donorMesh.vertexCount + " subMeshes=" + donorMesh.subMeshCount + " blendShapes=" + donorMesh.blendShapeCount + " targets=" + targets.Count);

        DonorMeshData donorData = DonorMeshData.Create(donorRenderer);

        List<string> results = new List<string>();

        for (int i = 0; i < targets.Count; i++)
        {
            Transform target = targets[i];

            if (target == null) continue;

            if (EditorUtility.DisplayCancelableProgressBar("Skinning Attachments", "Preparing " + target.name + " (" + (i + 1) + "/" + targets.Count + ")", (float)i / targets.Count))
                return "Cancelled while preparing " + target.name + ".";

            Debug.Log("AttachmentSkinner: skinning " + target.name + " (" + (i + 1) + "/" + targets.Count + ")");

            string result = SkinTarget(root, donorRenderer, donorData, target, outputFolderPath, copyBlendShapes, reparentTargets, isPrefabAssetEdit);

            if (result.StartsWith("Cancelled"))
                return result;

            Debug.Log("AttachmentSkinner: " + result);

            results.Add(result);
        }

        if (alreadySkinnedAutoTargets.Count > 0)
            results.Add("Auto-skipped already skinned attachments: " + alreadySkinnedAutoTargets.Count);

        return string.Join("\n", results);
    }


    private static string SkinTarget(GameObject root, SkinnedMeshRenderer donorRenderer, DonorMeshData donorData, Transform targetTransform, string outputFolderPath, bool copyBlendShapes, bool reparentTarget, bool isPrefabAssetEdit)
    {
        MeshFilter meshFilter = targetTransform.GetComponent<MeshFilter>();
        MeshRenderer meshRenderer = targetTransform.GetComponent<MeshRenderer>();
        SkinnedMeshRenderer existingSkinnedRenderer = targetTransform.GetComponent<SkinnedMeshRenderer>();

        if (IsAlreadySkinnedAttachment(meshFilter, meshRenderer, existingSkinnedRenderer))
            return targetTransform.name + ": already skinned; skipped.";

        Mesh sourceMesh = meshFilter != null ? meshFilter.sharedMesh : existingSkinnedRenderer != null ? existingSkinnedRenderer.sharedMesh : null;
        Renderer sourceRenderer = meshRenderer != null ? meshRenderer : existingSkinnedRenderer;

        if (sourceMesh == null)
            return targetTransform.name + ": no source mesh found.";

        if (sourceRenderer == null)
            return targetTransform.name + ": no source renderer found.";

        Material[] materials = sourceRenderer.sharedMaterials;
        RendererSnapshot rendererSnapshot = RendererSnapshot.Capture(sourceRenderer);

        Mesh skinnedMesh = BuildSkinnedAttachmentMesh(sourceMesh, targetTransform, donorRenderer, donorData, targetTransform.name + "_Skinned", copyBlendShapes, targetTransform.name);

        if (skinnedMesh == null)
            return "Cancelled while skinning " + targetTransform.name + ".";

        string rootFolder = outputFolderPath.Replace("\\", "/").TrimEnd('/') + "/" + SanitizeFileName(root.name);
        EnsureFolder(rootFolder);

        string meshAssetPath = rootFolder + "/" + SanitizeFileName(targetTransform.name) + "_Skinned.asset";
        Mesh meshAsset = SaveOrUpdateMeshAsset(skinnedMesh, meshAssetPath);

        if (meshRenderer != null)
            DestroyImmediate(meshRenderer, isPrefabAssetEdit);

        if (meshFilter != null)
            DestroyImmediate(meshFilter, isPrefabAssetEdit);

        SkinnedMeshRenderer targetSkinnedRenderer = existingSkinnedRenderer != null ? existingSkinnedRenderer : targetTransform.gameObject.AddComponent<SkinnedMeshRenderer>();

        targetSkinnedRenderer.sharedMesh = meshAsset;
        targetSkinnedRenderer.rootBone = donorRenderer.rootBone;
        targetSkinnedRenderer.bones = donorRenderer.bones;
        targetSkinnedRenderer.sharedMaterials = materials;
        targetSkinnedRenderer.quality = donorRenderer.quality;
        targetSkinnedRenderer.updateWhenOffscreen = donorRenderer.updateWhenOffscreen;
        targetSkinnedRenderer.skinnedMotionVectors = donorRenderer.skinnedMotionVectors;
        targetSkinnedRenderer.localBounds = meshAsset.bounds;

        rendererSnapshot.ApplyTo(targetSkinnedRenderer);

        if (reparentTarget && donorRenderer.transform.parent != null)
        {
            targetTransform.SetParent(donorRenderer.transform.parent, false);
            targetTransform.localPosition = Vector3.zero;
            targetTransform.localRotation = Quaternion.identity;
            targetTransform.localScale = Vector3.one;
        }

        EditorUtility.SetDirty(targetSkinnedRenderer);
        EditorUtility.SetDirty(targetTransform);

        int zeroWeightCount = CountZeroWeightedVertices(meshAsset);
        string blendShapeText = copyBlendShapes ? ", blendshapes: " + meshAsset.blendShapeCount : "";

        return targetTransform.name + ": skinned vertices: " + meshAsset.vertexCount + ", zero-weight vertices: " + zeroWeightCount + blendShapeText;
    }


    private static bool IsAlreadySkinnedAttachment(MeshFilter meshFilter, MeshRenderer meshRenderer, SkinnedMeshRenderer skinnedRenderer)
    {
        if (skinnedRenderer == null)
            return false;

        if (meshFilter != null || meshRenderer != null)
            return false;

        return skinnedRenderer.sharedMesh != null && skinnedRenderer.rootBone != null && skinnedRenderer.bones != null && skinnedRenderer.bones.Length > 0;
    }


    private static Mesh BuildSkinnedAttachmentMesh(Mesh sourceMesh, Transform sourceTransform, SkinnedMeshRenderer donorRenderer, DonorMeshData donorData, string meshName, bool copyBlendShapes, string targetName)
    {
        Mesh outputMesh = new Mesh();
        outputMesh.name = meshName;
        outputMesh.indexFormat = sourceMesh.indexFormat;

        Matrix4x4 sourceToDonor = donorRenderer.transform.worldToLocalMatrix * sourceTransform.localToWorldMatrix;

        Vector3[] vertices = sourceMesh.vertices;
        Vector3[] convertedVertices = new Vector3[vertices.Length];
        AttachmentVertexMap[] vertexMaps = new AttachmentVertexMap[vertices.Length];

        for (int i = 0; i < vertices.Length; i++)
        {
            if (i % 25 == 0 && EditorUtility.DisplayCancelableProgressBar("Skinning Attachments", targetName + ": transferring vertex weights " + i + "/" + vertices.Length, vertices.Length > 0 ? (float)i / vertices.Length : 1f))
            {
                DestroyImmediate(outputMesh);

                return null;
            }

            convertedVertices[i] = sourceToDonor.MultiplyPoint3x4(vertices[i]);
            vertexMaps[i] = donorData.FindClosestTriangle(convertedVertices[i]);
        }

        outputMesh.vertices = convertedVertices;

        Vector3[] normals = sourceMesh.normals;
        if (normals != null && normals.Length == vertices.Length)
            outputMesh.normals = TransformDirections(sourceToDonor, normals);

        Vector4[] tangents = sourceMesh.tangents;
        if (tangents != null && tangents.Length == vertices.Length)
            outputMesh.tangents = TransformTangents(sourceToDonor, tangents);

        Color[] colors = sourceMesh.colors;
        if (colors != null && colors.Length == vertices.Length)
            outputMesh.colors = colors;

        Color32[] colors32 = sourceMesh.colors32;
        if (colors32 != null && colors32.Length == vertices.Length)
            outputMesh.colors32 = colors32;

        CopyUvChannels(sourceMesh, outputMesh);
        CopySubMeshes(sourceMesh, outputMesh);

        outputMesh.bindposes = donorRenderer.sharedMesh.bindposes;
        SetTransferredBoneWeights(outputMesh, donorData, vertexMaps);

        if (copyBlendShapes)
        {
            if (!TransferBlendShapes(outputMesh, donorData, vertexMaps, targetName))
            {
                DestroyImmediate(outputMesh);

                return null;
            }
        }

        outputMesh.RecalculateBounds();

        return outputMesh;
    }


    private static SkinnedMeshRenderer ResolveDonorRenderer(GameObject root, SkinnedMeshRenderer donorOverride, string donorName, bool autoPick, string[] keywords)
    {
        if (!autoPick && donorOverride != null)
            return donorOverride;

        if (!autoPick && !string.IsNullOrWhiteSpace(donorName))
        {
            SkinnedMeshRenderer named = FindRendererByName<SkinnedMeshRenderer>(root.transform, donorName);

            if (named != null)
                return named;
        }

        SkinnedMeshRenderer[] renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        SkinnedMeshRenderer best = null;
        int bestScore = int.MinValue;

        for (int i = 0; i < renderers.Length; i++)
        {
            SkinnedMeshRenderer renderer = renderers[i];

            if (renderer == null || renderer.sharedMesh == null || renderer.bones == null || renderer.bones.Length == 0)
                continue;

            string lowerName = renderer.name.ToLowerInvariant();

            if (NameMatchesAnyKeyword(lowerName, keywords))
                continue;

            int score = renderer.sharedMesh.vertexCount + renderer.sharedMesh.blendShapeCount * 100000;

            if (score <= bestScore) continue;

            best = renderer;
            bestScore = score;
        }

        return best;
    }


    private static List<Transform> ResolveTargets(GameObject root, SkinnedMeshRenderer donorRenderer, string[] explicitNames, string[] keywords)
    {
        List<Transform> targets = new List<Transform>();
        HashSet<Transform> seen = new HashSet<Transform>();

        if (explicitNames != null && explicitNames.Length > 0)
        {
            for (int i = 0; i < explicitNames.Length; i++)
            {
                Transform target = FindTransformByName(root.transform, explicitNames[i]);

                if (target != null && target != donorRenderer.transform && seen.Add(target))
                    targets.Add(target);
            }

            return targets;
        }

        MeshRenderer[] meshRenderers = root.GetComponentsInChildren<MeshRenderer>(true);

        for (int i = 0; i < meshRenderers.Length; i++)
        {
            MeshRenderer renderer = meshRenderers[i];

            if (renderer == null || renderer.GetComponent<MeshFilter>() == null)
                continue;

            if (!NameMatchesAnyKeyword(renderer.name.ToLowerInvariant(), keywords))
                continue;

            if (seen.Add(renderer.transform))
                targets.Add(renderer.transform);
        }

        return targets;
    }


    private static void PreviewAutoTargets(GameObject root, string[] keywords)
    {
        SkinnedMeshRenderer donor = ResolveDonorRenderer(root, null, "", true, keywords);
        List<Transform> targets = ResolveTargets(root, donor, null, keywords);
        List<Transform> alreadySkinned = ResolveAlreadySkinnedKeywordTargets(root, donor, keywords);

        Debug.Log("AttachmentSkinner preview for " + root.name + ": donor=" + (donor != null ? donor.name : "<none>") + ", unskinned targets=" + targets.Count + ", already skinned skipped=" + alreadySkinned.Count);

        for (int i = 0; i < targets.Count; i++)
            Debug.Log("AttachmentSkinner target: " + GetRelativePath(root.transform, targets[i]));

        for (int i = 0; i < alreadySkinned.Count; i++)
            Debug.Log("AttachmentSkinner already skinned: " + GetRelativePath(root.transform, alreadySkinned[i]));
    }


    private static List<Transform> ResolveAlreadySkinnedKeywordTargets(GameObject root, SkinnedMeshRenderer donorRenderer, string[] keywords)
    {
        List<Transform> targets = new List<Transform>();
        SkinnedMeshRenderer[] skinnedRenderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);

        for (int i = 0; i < skinnedRenderers.Length; i++)
        {
            SkinnedMeshRenderer skinnedRenderer = skinnedRenderers[i];

            if (skinnedRenderer == null || skinnedRenderer == donorRenderer)
                continue;

            if (!NameMatchesAnyKeyword(skinnedRenderer.name.ToLowerInvariant(), keywords))
                continue;

            MeshFilter meshFilter = skinnedRenderer.GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = skinnedRenderer.GetComponent<MeshRenderer>();

            if (IsAlreadySkinnedAttachment(meshFilter, meshRenderer, skinnedRenderer))
                targets.Add(skinnedRenderer.transform);
        }

        return targets;
    }


    private static bool NameMatchesAnyKeyword(string lowerName, string[] keywords)
    {
        if (keywords == null || keywords.Length == 0)
            keywords = DefaultTargetKeywords;

        for (int i = 0; i < keywords.Length; i++)
        {
            string keyword = keywords[i];

            if (string.IsNullOrWhiteSpace(keyword)) continue;

            if (lowerName.Contains(keyword.Trim().ToLowerInvariant()))
                return true;
        }

        return false;
    }


    private static void SetTransferredBoneWeights(Mesh outputMesh, DonorMeshData donorData, AttachmentVertexMap[] vertexMaps)
    {
        List<BoneWeight1> allWeights = new List<BoneWeight1>();
        NativeArray<byte> bonesPerVertex = new NativeArray<byte>(vertexMaps.Length, Allocator.Temp);

        for (int vertexIndex = 0; vertexIndex < vertexMaps.Length; vertexIndex++)
        {
            List<BoneInfluence> influences = donorData.InterpolateWeights(vertexMaps[vertexIndex]);

            int count = Mathf.Min(4, influences.Count);

            if (count == 0)
            {
                influences.Add(new BoneInfluence(donorData.DefaultBoneIndex, 1f));
                count = 1;
            }

            bonesPerVertex[vertexIndex] = (byte)count;

            for (int i = 0; i < count; i++)
            {
                allWeights.Add(new BoneWeight1
                {
                    boneIndex = influences[i].BoneIndex,
                    weight = influences[i].Weight
                });
            }
        }

        NativeArray<BoneWeight1> boneWeights = new NativeArray<BoneWeight1>(allWeights.Count, Allocator.Temp);

        for (int i = 0; i < allWeights.Count; i++)
            boneWeights[i] = allWeights[i];

        outputMesh.SetBoneWeights(bonesPerVertex, boneWeights);

        bonesPerVertex.Dispose();
        boneWeights.Dispose();
    }


    private static bool TransferBlendShapes(Mesh outputMesh, DonorMeshData donorData, AttachmentVertexMap[] vertexMaps, string targetName)
    {
        Mesh donorMesh = donorData.Mesh;

        int sourceVertexCount = donorMesh.vertexCount;
        int targetVertexCount = outputMesh.vertexCount;

        Vector3[] sourceDeltaVertices = new Vector3[sourceVertexCount];
        Vector3[] sourceDeltaNormals = new Vector3[sourceVertexCount];
        Vector3[] sourceDeltaTangents = new Vector3[sourceVertexCount];

        Vector3[] targetDeltaVertices = new Vector3[targetVertexCount];
        Vector3[] targetDeltaNormals = new Vector3[targetVertexCount];
        Vector3[] targetDeltaTangents = new Vector3[targetVertexCount];

        for (int shapeIndex = 0; shapeIndex < donorMesh.blendShapeCount; shapeIndex++)
        {
            if (EditorUtility.DisplayCancelableProgressBar("Skinning Attachments", targetName + ": transferring blendshape " + (shapeIndex + 1) + "/" + donorMesh.blendShapeCount, donorMesh.blendShapeCount > 0 ? (float)shapeIndex / donorMesh.blendShapeCount : 1f))
                return false;

            string shapeName = donorMesh.GetBlendShapeName(shapeIndex);
            int frameCount = donorMesh.GetBlendShapeFrameCount(shapeIndex);

            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                float frameWeight = donorMesh.GetBlendShapeFrameWeight(shapeIndex, frameIndex);

                donorMesh.GetBlendShapeFrameVertices(shapeIndex, frameIndex, sourceDeltaVertices, sourceDeltaNormals, sourceDeltaTangents);

                for (int vertexIndex = 0; vertexIndex < targetVertexCount; vertexIndex++)
                {
                    AttachmentVertexMap map = vertexMaps[vertexIndex];

                    targetDeltaVertices[vertexIndex] = Interpolate(sourceDeltaVertices, map);
                    targetDeltaNormals[vertexIndex] = Interpolate(sourceDeltaNormals, map);
                    targetDeltaTangents[vertexIndex] = Interpolate(sourceDeltaTangents, map);
                }

                outputMesh.AddBlendShapeFrame(shapeName, frameWeight, targetDeltaVertices, targetDeltaNormals, targetDeltaTangents);
            }
        }

        return true;
    }


    private static Vector3 Interpolate(Vector3[] values, AttachmentVertexMap map)
    {
        return values[map.A] * map.Barycentric.x + values[map.B] * map.Barycentric.y + values[map.C] * map.Barycentric.z;
    }


    private static Vector3[] TransformDirections(Matrix4x4 matrix, Vector3[] directions)
    {
        Vector3[] transformed = new Vector3[directions.Length];

        for (int i = 0; i < directions.Length; i++)
            transformed[i] = matrix.MultiplyVector(directions[i]).normalized;

        return transformed;
    }


    private static Vector4[] TransformTangents(Matrix4x4 matrix, Vector4[] tangents)
    {
        Vector4[] transformed = new Vector4[tangents.Length];

        for (int i = 0; i < tangents.Length; i++)
        {
            Vector3 tangent = matrix.MultiplyVector(new Vector3(tangents[i].x, tangents[i].y, tangents[i].z)).normalized;

            transformed[i] = new Vector4(tangent.x, tangent.y, tangent.z, tangents[i].w);
        }

        return transformed;
    }


    private static void CopyUvChannels(Mesh sourceMesh, Mesh outputMesh)
    {
        for (int channel = 0; channel < 8; channel++)
        {
            List<Vector4> uvs = new List<Vector4>();

            sourceMesh.GetUVs(channel, uvs);

            if (uvs.Count == sourceMesh.vertexCount)
                outputMesh.SetUVs(channel, uvs);
        }
    }


    private static void CopySubMeshes(Mesh sourceMesh, Mesh outputMesh)
    {
        outputMesh.subMeshCount = sourceMesh.subMeshCount;

        for (int subMesh = 0; subMesh < sourceMesh.subMeshCount; subMesh++)
        {
            MeshTopology topology = sourceMesh.GetTopology(subMesh);
            int[] indices = sourceMesh.GetIndices(subMesh, true);

            outputMesh.SetIndices(indices, topology, subMesh, false);
        }
    }


    private static Mesh SaveOrUpdateMeshAsset(Mesh mesh, string path)
    {
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);

        if (existing != null)
        {
            EditorUtility.CopySerialized(mesh, existing);
            existing.name = mesh.name;
            EditorUtility.SetDirty(existing);

            return existing;
        }

        AssetDatabase.CreateAsset(mesh, path);

        return mesh;
    }


    private static int CountZeroWeightedVertices(Mesh mesh)
    {
        NativeArray<byte> bonesPerVertex = mesh.GetBonesPerVertex();
        int zeroCount = 0;

        for (int i = 0; i < bonesPerVertex.Length; i++)
        {
            if (bonesPerVertex[i] == 0)
                zeroCount++;
        }

        if (bonesPerVertex.IsCreated)
            bonesPerVertex.Dispose();

        return zeroCount;
    }


    private static string[] ParseLines(string names)
    {
        if (string.IsNullOrWhiteSpace(names)) return new string[0];

        string[] lines = names.Replace("\r", "").Split('\n');
        List<string> parsed = new List<string>();

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();

            if (!string.IsNullOrEmpty(line))
                parsed.Add(line);
        }

        return parsed.ToArray();
    }


    private static bool IsBlockingResult(string result)
    {
        return result.StartsWith("Cancelled") || result.StartsWith("Could not") || result.Contains("no shared mesh") || result.Contains("no bones") || result.StartsWith("No target");
    }


    private static void EnsureFolder(string folderPath)
    {
        string normalized = folderPath.Replace("\\", "/").Trim('/');

        if (AssetDatabase.IsValidFolder(normalized)) return;

        string[] parts = normalized.Split('/');

        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];

            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }
    }


    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Model";

        char[] invalid = System.IO.Path.GetInvalidFileNameChars();
        string output = value;

        for (int i = 0; i < invalid.Length; i++)
            output = output.Replace(invalid[i], '_');

        return output.Replace(' ', '_');
    }


    private static T FindRendererByName<T>(Transform root, string objectName) where T : Renderer
    {
        Transform transform = FindTransformByName(root, objectName);

        return transform != null ? transform.GetComponent<T>() : null;
    }


    private static Transform FindTransformByName(Transform root, string objectName)
    {
        if (root == null) return null;

        if (root.name == objectName) return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindTransformByName(root.GetChild(i), objectName);

            if (found != null)
                return found;
        }

        return null;
    }


    private static string GetRelativePath(Transform root, Transform target)
    {
        if (target == root) return "";

        List<string> segments = new List<string>();
        Transform current = target;

        while (current != null && current != root)
        {
            segments.Add(current.name);
            current = current.parent;
        }

        segments.Reverse();

        return string.Join("/", segments);
    }


    private struct RendererSnapshot
    {
        public bool Enabled;
        public ShadowCastingMode ShadowCastingMode;
        public bool ReceiveShadows;
        public MotionVectorGenerationMode MotionVectorGenerationMode;
        public LightProbeUsage LightProbeUsage;
        public ReflectionProbeUsage ReflectionProbeUsage;
        public Transform ProbeAnchor;
        public GameObject LightProbeProxyVolumeOverride;
        public uint RenderingLayerMask;
        public int RendererPriority;
        public int SortingLayerId;
        public int SortingOrder;


        public static RendererSnapshot Capture(Renderer renderer)
        {
            return new RendererSnapshot
            {
                Enabled = renderer.enabled,
                ShadowCastingMode = renderer.shadowCastingMode,
                ReceiveShadows = renderer.receiveShadows,
                MotionVectorGenerationMode = renderer.motionVectorGenerationMode,
                LightProbeUsage = renderer.lightProbeUsage,
                ReflectionProbeUsage = renderer.reflectionProbeUsage,
                ProbeAnchor = renderer.probeAnchor,
                LightProbeProxyVolumeOverride = renderer.lightProbeProxyVolumeOverride,
                RenderingLayerMask = renderer.renderingLayerMask,
                RendererPriority = renderer.rendererPriority,
                SortingLayerId = renderer.sortingLayerID,
                SortingOrder = renderer.sortingOrder
            };
        }


        public void ApplyTo(Renderer renderer)
        {
            renderer.enabled = Enabled;
            renderer.shadowCastingMode = ShadowCastingMode;
            renderer.receiveShadows = ReceiveShadows;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode;
            renderer.lightProbeUsage = LightProbeUsage;
            renderer.reflectionProbeUsage = ReflectionProbeUsage;
            renderer.probeAnchor = ProbeAnchor;
            renderer.lightProbeProxyVolumeOverride = LightProbeProxyVolumeOverride;
            renderer.renderingLayerMask = RenderingLayerMask;
            renderer.rendererPriority = RendererPriority;
            renderer.sortingLayerID = SortingLayerId;
            renderer.sortingOrder = SortingOrder;
        }
    }


    private struct AttachmentVertexMap
    {
        public int A;
        public int B;
        public int C;
        public Vector3 Barycentric;
    }


    private struct BoneInfluence
    {
        public int BoneIndex;
        public float Weight;


        public BoneInfluence(int boneIndex, float weight)
        {
            BoneIndex = boneIndex;
            Weight = weight;
        }
    }


    private class DonorMeshData
    {
        public Mesh Mesh;
        public Vector3[] Vertices;
        public int[] Triangles;
        public List<BoneInfluence>[] VertexWeights;
        public int DefaultBoneIndex;


        public static DonorMeshData Create(SkinnedMeshRenderer renderer)
        {
            Mesh mesh = renderer.sharedMesh;

            return new DonorMeshData
            {
                Mesh = mesh,
                Vertices = mesh.vertices,
                Triangles = CollectTriangles(mesh),
                VertexWeights = ReadVertexWeights(mesh),
                DefaultBoneIndex = FindDefaultBoneIndex(renderer)
            };
        }


        public AttachmentVertexMap FindClosestTriangle(Vector3 point)
        {
            float bestDistance = float.PositiveInfinity;
            AttachmentVertexMap bestMap = new AttachmentVertexMap
            {
                A = 0,
                B = 0,
                C = 0,
                Barycentric = new Vector3(1f, 0f, 0f)
            };

            for (int i = 0; i < Triangles.Length; i += 3)
            {
                int a = Triangles[i];
                int b = Triangles[i + 1];
                int c = Triangles[i + 2];

                Vector3 closest = ClosestPointOnTriangle(point, Vertices[a], Vertices[b], Vertices[c]);
                float distance = (point - closest).sqrMagnitude;

                if (distance >= bestDistance) continue;

                bestDistance = distance;

                bestMap = new AttachmentVertexMap
                {
                    A = a,
                    B = b,
                    C = c,
                    Barycentric = Barycentric(closest, Vertices[a], Vertices[b], Vertices[c])
                };
            }

            return bestMap;
        }


        public List<BoneInfluence> InterpolateWeights(AttachmentVertexMap map)
        {
            Dictionary<int, float> accumulated = new Dictionary<int, float>();

            AddWeightedInfluences(accumulated, VertexWeights[map.A], map.Barycentric.x);
            AddWeightedInfluences(accumulated, VertexWeights[map.B], map.Barycentric.y);
            AddWeightedInfluences(accumulated, VertexWeights[map.C], map.Barycentric.z);

            List<BoneInfluence> influences = new List<BoneInfluence>();

            foreach (KeyValuePair<int, float> pair in accumulated)
            {
                if (pair.Value > 0.000001f)
                    influences.Add(new BoneInfluence(pair.Key, pair.Value));
            }

            influences.Sort((left, right) => right.Weight.CompareTo(left.Weight));

            if (influences.Count > 4)
                influences.RemoveRange(4, influences.Count - 4);

            float totalWeight = 0f;

            for (int i = 0; i < influences.Count; i++)
                totalWeight += influences[i].Weight;

            if (totalWeight > 0.000001f)
            {
                for (int i = 0; i < influences.Count; i++)
                    influences[i] = new BoneInfluence(influences[i].BoneIndex, influences[i].Weight / totalWeight);
            }

            return influences;
        }


        private static void AddWeightedInfluences(Dictionary<int, float> accumulated, List<BoneInfluence> influences, float scale)
        {
            if (scale <= 0f || influences == null) return;

            for (int i = 0; i < influences.Count; i++)
            {
                BoneInfluence influence = influences[i];

                if (!accumulated.ContainsKey(influence.BoneIndex))
                    accumulated.Add(influence.BoneIndex, 0f);

                accumulated[influence.BoneIndex] += influence.Weight * scale;
            }
        }


        private static int[] CollectTriangles(Mesh mesh)
        {
            List<int> triangles = new List<int>();

            for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            {
                if (mesh.GetTopology(subMesh) != MeshTopology.Triangles)
                    continue;

                triangles.AddRange(mesh.GetTriangles(subMesh, true));
            }

            return triangles.ToArray();
        }


        private static List<BoneInfluence>[] ReadVertexWeights(Mesh mesh)
        {
            List<BoneInfluence>[] weightsByVertex = new List<BoneInfluence>[mesh.vertexCount];

            for (int i = 0; i < weightsByVertex.Length; i++)
                weightsByVertex[i] = new List<BoneInfluence>();

            NativeArray<byte> bonesPerVertex = mesh.GetBonesPerVertex();
            NativeArray<BoneWeight1> allWeights = mesh.GetAllBoneWeights();

            int weightIndex = 0;

            for (int vertexIndex = 0; vertexIndex < bonesPerVertex.Length; vertexIndex++)
            {
                int count = bonesPerVertex[vertexIndex];

                for (int i = 0; i < count; i++)
                {
                    BoneWeight1 weight = allWeights[weightIndex++];

                    if (weight.weight > 0.000001f)
                        weightsByVertex[vertexIndex].Add(new BoneInfluence(weight.boneIndex, weight.weight));
                }
            }

            if (bonesPerVertex.IsCreated)
                bonesPerVertex.Dispose();

            if (allWeights.IsCreated)
                allWeights.Dispose();

            return weightsByVertex;
        }


        private static int FindDefaultBoneIndex(SkinnedMeshRenderer renderer)
        {
            if (renderer.rootBone == null || renderer.bones == null) return 0;

            for (int i = 0; i < renderer.bones.Length; i++)
            {
                if (renderer.bones[i] == renderer.rootBone)
                    return i;
            }

            return 0;
        }
    }


    private static Vector3 ClosestPointOnTriangle(Vector3 point, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 ab = b - a;
        Vector3 ac = c - a;
        Vector3 ap = point - a;

        float d1 = Vector3.Dot(ab, ap);
        float d2 = Vector3.Dot(ac, ap);

        if (d1 <= 0f && d2 <= 0f)
            return a;

        Vector3 bp = point - b;
        float d3 = Vector3.Dot(ab, bp);
        float d4 = Vector3.Dot(ac, bp);

        if (d3 >= 0f && d4 <= d3)
            return b;

        float vc = d1 * d4 - d3 * d2;

        if (vc <= 0f && d1 >= 0f && d3 <= 0f)
        {
            float v = d1 / (d1 - d3);

            return a + ab * v;
        }

        Vector3 cp = point - c;
        float d5 = Vector3.Dot(ab, cp);
        float d6 = Vector3.Dot(ac, cp);

        if (d6 >= 0f && d5 <= d6)
            return c;

        float vb = d5 * d2 - d1 * d6;

        if (vb <= 0f && d2 >= 0f && d6 <= 0f)
        {
            float w = d2 / (d2 - d6);

            return a + ac * w;
        }

        float va = d3 * d6 - d5 * d4;

        if (va <= 0f && d4 - d3 >= 0f && d5 - d6 >= 0f)
        {
            float w = (d4 - d3) / ((d4 - d3) + (d5 - d6));

            return b + (c - b) * w;
        }

        float denominator = 1f / (va + vb + vc);
        float vFace = vb * denominator;
        float wFace = vc * denominator;

        return a + ab * vFace + ac * wFace;
    }


    private static Vector3 Barycentric(Vector3 point, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 v0 = b - a;
        Vector3 v1 = c - a;
        Vector3 v2 = point - a;

        float d00 = Vector3.Dot(v0, v0);
        float d01 = Vector3.Dot(v0, v1);
        float d11 = Vector3.Dot(v1, v1);
        float d20 = Vector3.Dot(v2, v0);
        float d21 = Vector3.Dot(v2, v1);
        float denominator = d00 * d11 - d01 * d01;

        if (Mathf.Abs(denominator) < 0.0000001f)
            return new Vector3(1f, 0f, 0f);

        float v = (d11 * d20 - d01 * d21) / denominator;
        float w = (d00 * d21 - d01 * d20) / denominator;
        float u = 1f - v - w;

        return new Vector3(u, v, w);
    }
}
