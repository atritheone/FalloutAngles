// imports
using UnityEngine; 
using UnityEditor; 
using System.Collections.Generic; 
using System.Linq;



// class
public class GenericAvatarFromglTF : EditorWindow
{

    // variables
    // The model to scan/build from. Accepts scene objects and imported assets that are GameObjects.
    [SerializeField] private GameObject sourceObject;

    // Folder to save the created Avatar asset into.
    [SerializeField] private DefaultAsset outputFolder;

    // If true, automatically select the most likely correct root node path after scanning.
    [SerializeField] private bool autoPickRootPath = true;

    // If true, assign the created Avatar to an Animator on the currently selected scene object (if present).
    [SerializeField] private bool assignToSelectedSceneAnimator = false;

    // Optional prefix inserted before the skeleton root path when building the Avatar.
    [SerializeField] private string bonePathPrefix = "";

    // The selected root path (relative to the scan root transform). NOTE: empty string is valid and means "(root)".
    [SerializeField] private string selectedRootPath = "";

    // Dropdown index for root paths.
    [SerializeField] private int selectedRootIndex = 0;

    // Candidate root paths (raw values; may include empty string).
    private List<string> candidateRootPaths = new List<string>();

    // Candidate root paths (display values; empty string becomes "(root)").
    private List<string> candidateRootPathsDisplay = new List<string>();

    // Whether a scan has been performed and produced at least one candidate.
    private bool hasScanResults = false;

    // Status text shown in the UI.
    private string status = "1) Pick a Source Object\n2) Click Scan Skeleton\n3) Pick Root Node Path\n4) Build Generic Avatar";

    


    // methods
    [MenuItem("Tools/Fallout Angles/glTF/Generic Avatar Generator")]
    private static void OpenWindow()
    {
        // Open the tool window.
        GetWindow<GenericAvatarFromglTF>("Generic Avatar From glTF");
    }


    private void OnGUI()
    {
        // Title label.
        EditorGUILayout.LabelField("Generic Avatar Generator From glTF", EditorStyles.boldLabel);

        EditorGUILayout.Space();

        // Allow selecting scene objects as well as assets.
        sourceObject = (GameObject)EditorGUILayout.ObjectField("Source Object", sourceObject, typeof(GameObject), true);

        // Choose where to save the Avatar asset.
        outputFolder = (DefaultAsset)EditorGUILayout.ObjectField("Output Folder", outputFolder, typeof(DefaultAsset), false);

        // Toggle autopick.
        autoPickRootPath = EditorGUILayout.Toggle("Auto-Pick Root Path", autoPickRootPath);

        // Toggle assignment.
        assignToSelectedSceneAnimator = EditorGUILayout.Toggle("Assign To Selected Scene", assignToSelectedSceneAnimator);

        // Optional prefix for animation paths.
        bonePathPrefix = EditorGUILayout.TextField("Root Prefix (optional)", bonePathPrefix);

        EditorGUILayout.Space();

        // Scan button.
        using (new EditorGUI.DisabledScope(sourceObject == null))
        {
            if (GUILayout.Button("Scan Skeleton"))
                ScanSkeletonAndPopulateCandidates();
        }

        // Root selection UI.
        EditorGUILayout.Space();

        if (hasScanResults && candidateRootPaths != null && candidateRootPaths.Count > 0)
        {
            // Clamp index.
            selectedRootIndex = Mathf.Clamp(selectedRootIndex, 0, candidateRootPaths.Count - 1);

            // Show dropdown using display strings.
            selectedRootIndex = EditorGUILayout.Popup("Root Node Path", selectedRootIndex, candidateRootPathsDisplay.ToArray());

            // Map dropdown choice to raw path.
            selectedRootPath = candidateRootPaths[selectedRootIndex];
        }
        else
        {
            // Show current selection as text if no scan results exist.
            string display = string.IsNullOrEmpty(selectedRootPath) ? "(root)" : selectedRootPath;

            EditorGUILayout.LabelField("Root Node Path", hasScanResults ? display : "(scan first)");
        }

        EditorGUILayout.Space();

        // Build button enable conditions.
        bool folderOk = outputFolder != null && AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(outputFolder));

        // IMPORTANT: empty string is a valid root path, so we only require scan results + a valid index.
        bool hasValidRootChoice = hasScanResults && candidateRootPaths != null && candidateRootPaths.Count > 0 && selectedRootIndex >= 0 && selectedRootIndex < candidateRootPaths.Count;

        bool canBuild = sourceObject != null && folderOk && hasValidRootChoice;

        using (new EditorGUI.DisabledScope(!canBuild))
        {
            if (GUILayout.Button("Build Generic Avatar"))
                BuildAvatarFromSelectedPath();
        }

        EditorGUILayout.Space();

        // Status.
        EditorGUILayout.HelpBox(status, MessageType.Info);
    }


    private void ScanSkeletonAndPopulateCandidates()
    {
        // Reset flags and lists.
        hasScanResults = false;

        candidateRootPaths = new List<string>();

        candidateRootPathsDisplay = new List<string>();

        // Reset status.
        status = "";

        // Validate source.
        if (sourceObject == null)
        {
            status = "No Source Object selected.";

            return;
        }

        // Load a stable hierarchy for scanning.
        string assetPath = AssetDatabase.GetAssetPath(sourceObject);

        bool isAsset = !string.IsNullOrEmpty(assetPath) && AssetDatabase.Contains(sourceObject);

        GameObject scanRoot = null;

        if (isAsset)
            scanRoot = PrefabUtility.LoadPrefabContents(assetPath);

        else
            scanRoot = sourceObject;

        // Validate scan root.
        if (scanRoot == null)
        {
            status = "Failed to load object hierarchy for scanning.";

            return;
        }

        // Find renderers.
        SkinnedMeshRenderer[] skinnedMeshes = scanRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);

        MeshRenderer[] meshRenderers = scanRoot.GetComponentsInChildren<MeshRenderer>(true);

        // Print diagnostics.
        status += "Scan Root: " + scanRoot.name + "\n";

        status += "SkinnedMeshRenderer count: " + (skinnedMeshes != null ? skinnedMeshes.Length : 0) + "\n";

        status += "MeshRenderer count: " + (meshRenderers != null ? meshRenderers.Length : 0) + "\n";

        // If no skinned meshes exist, we cannot build an Avatar.
        if (skinnedMeshes == null || skinnedMeshes.Length == 0)
        {
            if (isAsset)
                PrefabUtility.UnloadPrefabContents(scanRoot);

            status += "\nNo SkinnedMeshRenderer found.\n";

            status += "Unity cannot build an Avatar unless the model is imported as a skinned rig.";

            return;
        }

        // Collect bone roots from each skinned mesh.
        List<Transform> boneRoots = new List<Transform>();

        for (int i = 0; i < skinnedMeshes.Length; i++)
        {
            SkinnedMeshRenderer smr = skinnedMeshes[i];

            if (smr == null) continue;

            if (smr.rootBone != null)
                boneRoots.Add(smr.rootBone);

            else if (smr.bones != null && smr.bones.Length > 0 && smr.bones[0] != null)
                boneRoots.Add(smr.bones[0]);
        }

        // If we still have no bones, report.
        if (boneRoots.Count == 0)
        {
            if (isAsset)
                PrefabUtility.UnloadPrefabContents(scanRoot);

            status += "\nSkinned meshes exist, but no bones/rootBone were found.";

            return;
        }

        // Compute skeleton root by lowest common ancestor of all bone roots.
        Transform skeletonRoot = GetLowestCommonAncestor(boneRoots);

        if (skeletonRoot == null)
            skeletonRoot = boneRoots[0];

        // Build candidate transforms by walking from skeletonRoot up toward scanRoot.
        List<Transform> upwardCandidates = new List<Transform>();

        Transform current = skeletonRoot;

        while (current != null)
        {
            upwardCandidates.Add(current);

            if (current == scanRoot.transform) break;

            current = current.parent;
        }

        // Convert candidates to relative paths.
        for (int i = 0; i < upwardCandidates.Count; i++)
        {
            string rawPath = GetRelativePath(scanRoot.transform, upwardCandidates[i]);

            // Avoid duplicates.
            if (!candidateRootPaths.Contains(rawPath))
                candidateRootPaths.Add(rawPath);
        }

        // We want the most “specific” (closest to skeleton) first.
        candidateRootPaths.Reverse();

        // Build display list where empty string becomes "(root)".
        for (int i = 0; i < candidateRootPaths.Count; i++)
        {
            string raw = candidateRootPaths[i];

            string display = string.IsNullOrEmpty(raw) ? "(root)" : raw;

            candidateRootPathsDisplay.Add(display);
        }

        // Mark scan results.
        hasScanResults = candidateRootPaths.Count > 0;

        // Auto-pick first.
        if (hasScanResults && autoPickRootPath)
        {
            selectedRootIndex = 0;

            selectedRootPath = candidateRootPaths[selectedRootIndex];
        }

        // Unload if we loaded prefab contents.
        if (isAsset)
            PrefabUtility.UnloadPrefabContents(scanRoot);

        // Finish status.
        status += "\nScan complete. Candidate root paths: " + candidateRootPaths.Count + "\n";

        status += "If the dropdown looks blank, the option may be (root), which is valid.";
    }


    private void BuildAvatarFromSelectedPath()
    {
        // Reset status.
        status = "";

        // Validate folder.
        if (outputFolder == null)
        {
            status = "No Output Folder selected.";

            return;
        }

        string folderPath = AssetDatabase.GetAssetPath(outputFolder);

        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            status = "Output Folder is not a valid folder.";

            return;
        }

        // Validate scan results.
        if (!hasScanResults || candidateRootPaths == null || candidateRootPaths.Count == 0)
        {
            status = "No scan results. Click Scan Skeleton first.";

            return;
        }

        // Ensure index is valid.
        selectedRootIndex = Mathf.Clamp(selectedRootIndex, 0, candidateRootPaths.Count - 1);

        // NOTE: empty string is valid and means root.
        selectedRootPath = candidateRootPaths[selectedRootIndex];

        // Load a stable hierarchy for building.
        string assetPath = AssetDatabase.GetAssetPath(sourceObject);

        bool isAsset = !string.IsNullOrEmpty(assetPath) && AssetDatabase.Contains(sourceObject);

        GameObject buildRoot = null;

        GameObject prefabRoot = null;

        if (isAsset)
        {
            prefabRoot = PrefabUtility.LoadPrefabContents(assetPath);

            if (string.IsNullOrEmpty(bonePathPrefix))
                buildRoot = prefabRoot;

            else
                buildRoot = Instantiate(prefabRoot);
        }

        else
            buildRoot = Instantiate(sourceObject);

        if (buildRoot == null)
        {
            status = "Failed to load object hierarchy for avatar build.";

            return;
        }

        // Optionally create a wrapper to insert a prefix into bone paths.
        string normalizedPrefix = NormalizePath(bonePathPrefix);

        GameObject avatarRoot = buildRoot;

        string rootPathForAvatar = selectedRootPath;

        if (!string.IsNullOrEmpty(normalizedPrefix))
        {
            avatarRoot = new GameObject(buildRoot.name + "_AvatarRoot");

            Transform prefixParent = CreateTransformChain(avatarRoot.transform, normalizedPrefix);

            buildRoot.transform.SetParent(prefixParent, false);

            rootPathForAvatar = CombinePaths(normalizedPrefix, selectedRootPath);
        }

        // Build avatar.
        Avatar avatar = AvatarBuilder.BuildGenericAvatar(avatarRoot, rootPathForAvatar);

        if (avatar == null || !avatar.isValid)
        {
            if (prefabRoot != null)
                PrefabUtility.UnloadPrefabContents(prefabRoot);

            if (avatarRoot != null && avatarRoot != prefabRoot)
                DestroyImmediate(avatarRoot);

            status = "AvatarBuilder failed. Re-scan and try a different Root Node Path.";

            return;
        }

        // Name avatar.
        avatar.name = sourceObject.name + "_GenericAvatar";

        // Generate unique asset path.
        string avatarAssetPath = AssetDatabase.GenerateUniqueAssetPath(folderPath + "/" + avatar.name + ".asset");

        // Create asset.
        AssetDatabase.CreateAsset(avatar, avatarAssetPath);

        // Save.
        AssetDatabase.SaveAssets();

        // Cleanup build root.
        if (prefabRoot != null)
            PrefabUtility.UnloadPrefabContents(prefabRoot);

        if (avatarRoot != null && avatarRoot != prefabRoot)
            DestroyImmediate(avatarRoot);

        // Optional assign.
        if (assignToSelectedSceneAnimator)
            TryAssignToSelectedSceneAnimator(avatar);

        // Report success.
        string displayUsed = string.IsNullOrEmpty(rootPathForAvatar) ? "(root)" : rootPathForAvatar;

        status = "Created Avatar: " + avatarAssetPath + "\nUsed Root Path: " + displayUsed;
    }


    private static void TryAssignToSelectedSceneAnimator(Avatar avatar)
    {
        // Get selected scene object.
        GameObject selected = Selection.activeGameObject;

        if (selected == null) return;

        Animator animator = selected.GetComponentInChildren<Animator>(true);

        if (animator == null) return;

        animator.avatar = avatar;

        EditorUtility.SetDirty(animator);
    }


    private static Transform GetLowestCommonAncestor(List<Transform> transforms)
    {
        if (transforms == null || transforms.Count == 0) return null;

        List<List<Transform>> chains = new List<List<Transform>>();

        for (int i = 0; i < transforms.Count; i++)
        {
            Transform t = transforms[i];

            if (t == null) continue;

            List<Transform> chain = new List<Transform>();

            Transform current = t;

            while (current != null)
            {
                chain.Add(current);

                current = current.parent;
            }

            chain.Reverse();

            chains.Add(chain);
        }

        if (chains.Count == 0) return null;

        int minLength = chains.Min(c => c.Count);

        Transform lastCommon = null;

        for (int index = 0; index < minLength; index++)
        {
            Transform candidate = chains[0][index];

            bool allMatch = true;

            for (int c = 1; c < chains.Count; c++)
            {
                if (chains[c][index] != candidate)
                {
                    allMatch = false;

                    break;
                }
            }

            if (allMatch)
                lastCommon = candidate;

            else
                break;
        }

        return lastCommon;
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


    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "";

        string normalized = path.Replace("\\", "/").Trim();

        normalized = normalized.Trim('/');

        return normalized;
    }


    private static string CombinePaths(string prefix, string path)
    {
        if (string.IsNullOrEmpty(prefix)) return path;

        if (string.IsNullOrEmpty(path)) return prefix;

        return prefix + "/" + path;
    }


    private static Transform CreateTransformChain(Transform parent, string path)
    {
        string normalized = NormalizePath(path);

        if (string.IsNullOrEmpty(normalized)) return parent;

        string[] segments = normalized.Split('/');

        Transform current = parent;

        for (int i = 0; i < segments.Length; i++)
        {
            string segment = segments[i];

            if (string.IsNullOrEmpty(segment)) continue;

            GameObject node = new GameObject(segment);

            node.transform.SetParent(current, false);

            current = node.transform;
        }

        return current;
    }
}
