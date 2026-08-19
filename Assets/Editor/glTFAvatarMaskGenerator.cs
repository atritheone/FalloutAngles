// imports
using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;



// class
public class gTlFAvatarMaskGenerator: EditorWindow
{

    // variables
    // The GameObject whose Transform hierarchy will be used to build the mask paths (paths are relative to this root).
    [SerializeField] private GameObject rigRoot;

    // Folder to save the created Avatar Mask asset into.
    [SerializeField] private DefaultAsset outputFolder;

    // Optional explicit name for the generated mask asset.
    [SerializeField] private string maskName = "";

    // Optional root prefix to apply when exporting mask paths.
    [SerializeField] private string rootPrefix = "";

    // If true, automatically include all parent paths of selected transforms.
    [SerializeField] private bool autoIncludeParents = true;

    // If true, automatically include all child paths of a selected transform.
    [SerializeField] private bool autoIncludeChildren = false;

    // Search filter for quickly finding bones/transforms.
    [SerializeField] private string searchFilter = "";

    // Scroll position for the hierarchy UI.
    private Vector2 scrollPosition;

    // Cached list of all transform paths (relative to rigRoot).
    private List<string> allPaths = new List<string>();

    // Selection state per transform path.
    private Dictionary<string, bool> selectedByPath = new Dictionary<string, bool>();

    // Foldout state per hierarchy node key.
    private Dictionary<string, bool> foldoutByKey = new Dictionary<string, bool>();

    // Status message shown to the user.
    private string status = "1) Assign Rig Root (GameObject)\n2) Click Load Paths\n3) Tick transforms\n4) Create Mask";



    // methods
    [MenuItem("Tools/Fallout Angles/glTF/Avatar Mask Generator")]
    private static void OpenWindow()
    {
        // Open the tool window.
        GetWindow<gTlFAvatarMaskGenerator>("Selectable Transform Mask");
    }


    private void OnGUI()
    {
        // Draw header.
        EditorGUILayout.LabelField("Avatar Mask Generator", EditorStyles.boldLabel);

        EditorGUILayout.Space();

        // Draw rig root picker.
        rigRoot = (GameObject)EditorGUILayout.ObjectField("Rig Root (GameObject)", rigRoot, typeof(GameObject), true);

        EditorGUILayout.Space();

        // Draw output folder picker.
        outputFolder = (DefaultAsset)EditorGUILayout.ObjectField("Output Folder", outputFolder, typeof(DefaultAsset), false);

        EditorGUILayout.Space();

        // Draw mask name field.
        maskName = EditorGUILayout.TextField("Mask Name", maskName);

        EditorGUILayout.Space();

        // Draw root prefix field.
        rootPrefix = EditorGUILayout.TextField("Root Prefix (optional)", rootPrefix);

        EditorGUILayout.Space();

        // Draw behaviour toggles.
        autoIncludeParents = EditorGUILayout.Toggle("Auto Include Parents", autoIncludeParents);

        autoIncludeChildren = EditorGUILayout.Toggle("Auto Include Children", autoIncludeChildren);

        EditorGUILayout.Space();

        // Draw load button.
        using (new EditorGUI.DisabledScope(rigRoot == null))
        {
            if (GUILayout.Button("Load Paths From Rig Root"))
                LoadPathsFromRigRoot();
        }

        EditorGUILayout.Space();

        // If we have paths, draw selection UI.
        if (allPaths != null && allPaths.Count > 0)
        {
            // Draw search filter.
            searchFilter = EditorGUILayout.TextField("Search", searchFilter);

            EditorGUILayout.Space();

            // Draw selection buttons.
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Select All"))
                SetAllSelections(true);

            if (GUILayout.Button("Select None"))
                SetAllSelections(false);

            if (GUILayout.Button("Invert"))
                InvertSelections();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Draw expand/collapse buttons.
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Expand All"))
                SetAllFoldouts(true);

            if (GUILayout.Button("Collapse All"))
                SetAllFoldouts(false);

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Draw hierarchy header.
            EditorGUILayout.LabelField("Transform Hierarchy (relative to Rig Root):", EditorStyles.boldLabel);

            EditorGUILayout.Space();

            // Draw scroll view containing hierarchy.
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(420f));

            DrawHierarchyChecklist();

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();

            // Validate output folder.
            bool folderOk = outputFolder != null && AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(outputFolder));

            // Enable create button only when we can actually create.
            using (new EditorGUI.DisabledScope(!folderOk))
            {
                if (GUILayout.Button("Create Avatar Mask From Selection"))
                    CreateMaskFromSelection();
            }
        }

        EditorGUILayout.Space();

        // Draw status.
        EditorGUILayout.HelpBox(status, MessageType.Info);
    }


    private void LoadPathsFromRigRoot()
    {
        // Clear status.
        status = "";

        // Validate rig root.
        if (rigRoot == null)
        {
            // Report missing rig root.
            status = "No Rig Root assigned.";

            return;
        }

        // Get the root transform.
        Transform rootTransform = rigRoot.transform;

        // Gather paths for every child transform under the root.
        List<string> gathered = new List<string>();

        // Add each transform path by traversing the hierarchy.
        foreach (Transform t in rootTransform.GetComponentsInChildren<Transform>(true))
        {
            // Skip the root itself because the mask path list is relative to root (empty path is not used).
            if (t == rootTransform) continue;

            // Build the path relative to root.
            string relativePath = GetRelativePath(rootTransform, t);

            // Skip invalid.
            if (string.IsNullOrWhiteSpace(relativePath)) continue;

            // Add to list.
            gathered.Add(relativePath);
        }

        // Normalize and sort by depth then name.
        allPaths = gathered
            .Select(p => p.Trim())
            .Distinct()
            .OrderBy(p => p.Count(c => c == '/'))
            .ThenBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Reset selections.
        selectedByPath.Clear();

        // Initialize selection entries (default false).
        for (int i = 0; i < allPaths.Count; i++)
            selectedByPath[allPaths[i]] = false;

        // Reset foldouts.
        foldoutByKey.Clear();

        // Update status.
        status = "Loaded transform paths: " + allPaths.Count + "\nTick the ones you want included in the mask.";
    }


    private string GetRelativePath(Transform root, Transform target)
    {
        // Stop if invalid.
        if (root == null || target == null) return "";

        // If target is the root, return empty.
        if (target == root) return "";

        // Build segments from target up to root.
        List<string> segments = new List<string>();

        // Start at target.
        Transform current = target;

        // Climb until we reach root or run out.
        while (current != null && current != root)
        {
            // Add this name.
            segments.Add(current.name);

            // Move up.
            current = current.parent;
        }

        // If we never reached root, target isn't under root.
        if (current != root) return "";

        // Reverse to make root-to-leaf order.
        segments.Reverse();

        // Join into path.
        return string.Join("/", segments);
    }


    private void SetAllSelections(bool value)
    {
        // Stop if no paths.
        if (allPaths == null || allPaths.Count == 0) return;

        // Set each selection.
        for (int i = 0; i < allPaths.Count; i++)
            selectedByPath[allPaths[i]] = value;
    }


    private void InvertSelections()
    {
        // Stop if no paths.
        if (allPaths == null || allPaths.Count == 0) return;

        // Flip each selection.
        for (int i = 0; i < allPaths.Count; i++)
        {
            // Cache path.
            string p = allPaths[i];

            // Flip selection.
            selectedByPath[p] = !selectedByPath[p];
        }
    }


    private void DrawHierarchyChecklist()
    {
        // Build a tree from the path list.
        PathNode rootNode = BuildPathTree(allPaths);

        // Draw children starting from root.
        DrawNodeChildren(rootNode, 0);
    }


    private void DrawNodeChildren(PathNode node, int indent)
    {
        // Stop if null.
        if (node == null) return;

        // Stop if no children.
        if (node.Children.Count == 0) return;

        // Order children alphabetically.
        List<PathNode> ordered = node.Children.Values
            .OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Draw each child.
        for (int i = 0; i < ordered.Count; i++)
        {
            // Cache child.
            PathNode child = ordered[i];

            // Apply search filter.
            if (!PassesSearch(child, searchFilter))
                continue;

            // Ensure foldout state exists.
            if (!foldoutByKey.ContainsKey(child.Key))
                foldoutByKey[child.Key] = indent < 1;

            // Begin row.
            EditorGUILayout.BeginHorizontal();

            // Apply indentation.
            GUILayout.Space(indent * 16f);

            // Draw foldout if this node has children.
            if (child.Children.Count > 0)
            {
                // Draw foldout control (no label, label is drawn separately).
                foldoutByKey[child.Key] = EditorGUILayout.Foldout(foldoutByKey[child.Key], GUIContent.none, true);
            }
            else
            {
                // Keep alignment for leaf nodes.
                GUILayout.Space(12f);
            }

            // Determine if selectable.
            bool selectable = selectedByPath.ContainsKey(child.Path);

            if (selectable)
            {
                // Get current value.
                bool currentValue = selectedByPath[child.Path];

                // Draw checkbox.
                bool nextValue = EditorGUILayout.Toggle(currentValue, GUILayout.Width(16f));

                // Draw label.
                EditorGUILayout.LabelField(child.Name);

                // Apply selection change.
                if (nextValue != currentValue)
                {
                    // Set this node selection.
                    selectedByPath[child.Path] = nextValue;

                    // Optionally cascade to children.
                    if (autoIncludeChildren)
                        SetChildrenSelection(child, nextValue);
                }
            }
            else
            {
                // Draw label for non-selectable nodes.
                EditorGUILayout.LabelField(child.Name);
            }

            // End row.
            EditorGUILayout.EndHorizontal();

            // Recurse if expanded.
            if (child.Children.Count > 0 && foldoutByKey[child.Key])
                DrawNodeChildren(child, indent + 1);
        }
    }


    private void SetChildrenSelection(PathNode node, bool value)
    {
        // Stop if invalid.
        if (node == null) return;

        // Apply to this node if selectable.
        if (selectedByPath.ContainsKey(node.Path))
            selectedByPath[node.Path] = value;

        // Apply to all descendants.
        foreach (var kvp in node.Children)
            SetChildrenSelection(kvp.Value, value);
    }


    private void SetAllFoldouts(bool expanded)
    {
        // Stop if no paths.
        if (allPaths == null || allPaths.Count == 0) return;

        // Build a tree from the path list.
        PathNode rootNode = BuildPathTree(allPaths);

        // Apply to all nodes with children.
        ApplyFoldoutState(rootNode, expanded);
    }


    private void ApplyFoldoutState(PathNode node, bool expanded)
    {
        // Stop if invalid.
        if (node == null) return;

        // Apply to this node if it has children.
        if (node.Children.Count > 0)
            foldoutByKey[node.Key] = expanded;

        // Recurse into children.
        foreach (var kvp in node.Children)
            ApplyFoldoutState(kvp.Value, expanded);
    }


    private bool PassesSearch(PathNode node, string filter)
    {
        // If no filter, show everything.
        if (string.IsNullOrWhiteSpace(filter)) return true;

        // Normalize filter.
        string f = filter.Trim();

        // Match on node name.
        if (node.Name.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0) return true;

        // Match on full path.
        if (node.Path.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0) return true;

        // Match if any descendant matches.
        foreach (var kvp in node.Children)
        {
            if (PassesSearch(kvp.Value, f))
                return true;
        }

        // No match.
        return false;
    }


    private void CreateMaskFromSelection()
    {
        // Clear status.
        status = "";

        // Validate rig root.
        if (rigRoot == null)
        {
            // Report missing root.
            status = "No Rig Root assigned.";

            return;
        }

        // Validate output folder.
        if (outputFolder == null)
        {
            // Report missing folder.
            status = "No Output Folder selected.";

            return;
        }

        // Resolve folder path.
        string folderPath = AssetDatabase.GetAssetPath(outputFolder);

        // Validate folder path.
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            // Report invalid folder.
            status = "Output Folder is not a valid folder.";

            return;
        }

        // Collect selected paths.
        List<string> selected = selectedByPath
            .Where(kvp => kvp.Value)
            .Select(kvp => kvp.Key)
            .Distinct()
            .ToList();

        // Stop if nothing selected.
        if (selected.Count == 0)
        {
            // Report empty selection.
            status = "Nothing selected. Tick at least one transform.";

            return;
        }

        // Optionally add all parents.
        if (autoIncludeParents)
            selected = IncludeAllParents(selected);

        // Create the mask.
        AvatarMask mask = new AvatarMask();

        // Name the mask based on explicit name or rig root.
        string finalMaskName = string.IsNullOrWhiteSpace(maskName)
            ? rigRoot.name + "_TransformMask"
            : maskName.Trim();

        mask.name = finalMaskName;

        // Build the mask using Unity's public API so m_Elements gets populated correctly.
        BuildMaskUsingUnityApi(mask, rigRoot.transform, selected, rootPrefix);

        // Create output asset path.
        string outPath = AssetDatabase.GenerateUniqueAssetPath(folderPath + "/" + mask.name + ".mask");

        // Create the asset.
        AssetDatabase.CreateAsset(mask, outPath);

        // Save.
        AssetDatabase.SaveAssets();

        // Refresh.
        AssetDatabase.Refresh();

        // Report success.
        status = "Created Avatar Mask:\n" + outPath + "\nPaths included: " + selected.Count;
    }


    private List<string> IncludeAllParents(List<string> paths)
    {
        // Create a set for uniqueness.
        HashSet<string> set = new HashSet<string>(paths);

        // Add all parent chains.
        for (int i = 0; i < paths.Count; i++)
        {
            // Start at the path.
            string p = paths[i];

            // Climb up parent paths.
            while (!string.IsNullOrEmpty(p))
            {
                // Find last separator.
                int lastSlash = p.LastIndexOf('/');

                // Stop when no more parents.
                if (lastSlash < 0) break;

                // Remove last segment.
                p = p.Substring(0, lastSlash);

                // Add parent.
                set.Add(p);
            }
        }

        // Return stable order.
        return set
            .OrderBy(p => p.Count(c => c == '/'))
            .ThenBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }


    private static PathNode BuildPathTree(List<string> paths)
    {
        // Create root node.
        PathNode root = new PathNode("", "", "ROOT");

        // Add each path.
        for (int i = 0; i < paths.Count; i++)
        {
            // Read path.
            string full = paths[i];

            // Skip invalid.
            if (string.IsNullOrWhiteSpace(full)) continue;

            // Split into parts.
            string[] parts = full.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            // Start at root.
            PathNode current = root;

            // Build running key/path.
            string runningPath = "";

            // Walk parts.
            for (int j = 0; j < parts.Length; j++)
            {
                // Cache segment.
                string seg = parts[j];

                // Update running path.
                runningPath = string.IsNullOrEmpty(runningPath) ? seg : runningPath + "/" + seg;

                // Create child if missing.
                if (!current.Children.ContainsKey(seg))
                    current.Children[seg] = new PathNode(seg, runningPath, runningPath);

                // Step to child.
                current = current.Children[seg];
            }
        }

        // Return tree.
        return root;
    }


    private static void BuildMaskUsingUnityApi(AvatarMask mask, Transform rigRootTransform, List<string> selectedPaths, string rootPrefix)
    {
        // Stop if invalid.
        if (mask == null) return;

        if (selectedPaths == null || selectedPaths.Count == 0) return;

        // Normalize prefix.
        string trimmedPrefix = string.IsNullOrWhiteSpace(rootPrefix) ? "" : rootPrefix.Trim();

        string[] prefixSegments = SplitPath(trimmedPrefix);

        // Use rig hierarchy directly when no prefix is needed.
        if (prefixSegments.Length == 0)
        {
            if (rigRootTransform == null) return;

            // Add each selected transform path to the mask via Unity API.
            for (int i = 0; i < selectedPaths.Count; i++)
            {
                // Read the path.
                string path = selectedPaths[i];

                // Skip invalid.
                if (string.IsNullOrWhiteSpace(path)) continue;

                // Find the transform under the rig root.
                Transform t = rigRootTransform.Find(path);

                // Skip if it doesn't exist (path mismatch).
                if (t == null) continue;

                // Add the transform path (non-recursive because we handle parents/children ourselves).
                mask.AddTransformPath(t, false);

                // Ensure the last-added transform is active.
                int lastIndex = mask.transformCount - 1;

                if (lastIndex >= 0)
                    mask.SetTransformActive(lastIndex, true);
            }

            return;
        }

        // Build a temporary hierarchy so AddTransformPath includes the prefix.
        GameObject tempRoot = new GameObject("__MaskRoot");

        tempRoot.hideFlags = HideFlags.HideAndDontSave;

        try
        {
            Dictionary<string, Transform> nodeByPath = new Dictionary<string, Transform>();

            HashSet<string> added = new HashSet<string>();

            nodeByPath[""] = tempRoot.transform;

            // Add each selected transform path to the mask via Unity API.
            for (int i = 0; i < selectedPaths.Count; i++)
            {
                // Read the path.
                string path = selectedPaths[i];

                // Skip invalid.
                if (string.IsNullOrWhiteSpace(path)) continue;

                string[] pathSegments = SplitPath(path);

                if (pathSegments.Length == 0) continue;

                // Walk or build the hierarchy for prefix + path.
                Transform current = tempRoot.transform;

                string runningPath = "";

                for (int j = 0; j < prefixSegments.Length; j++)
                {
                    string seg = prefixSegments[j];

                    runningPath = string.IsNullOrEmpty(runningPath) ? seg : runningPath + "/" + seg;

                    if (!nodeByPath.TryGetValue(runningPath, out Transform next))
                    {
                        GameObject go = new GameObject(seg);

                        go.hideFlags = HideFlags.HideAndDontSave;

                        next = go.transform;

                        next.SetParent(current, false);

                        nodeByPath[runningPath] = next;
                    }

                    current = next;
                }

                for (int j = 0; j < pathSegments.Length; j++)
                {
                    string seg = pathSegments[j];

                    runningPath = string.IsNullOrEmpty(runningPath) ? seg : runningPath + "/" + seg;

                    if (!nodeByPath.TryGetValue(runningPath, out Transform next))
                    {
                        GameObject go = new GameObject(seg);

                        go.hideFlags = HideFlags.HideAndDontSave;

                        next = go.transform;

                        next.SetParent(current, false);

                        nodeByPath[runningPath] = next;
                    }

                    current = next;
                }

                if (!added.Add(runningPath)) continue;

                // Add the transform path (non-recursive because we handle parents/children ourselves).
                mask.AddTransformPath(current, false);

                // Ensure the last-added transform is active.
                int lastIndex = mask.transformCount - 1;

                if (lastIndex >= 0)
                    mask.SetTransformActive(lastIndex, true);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(tempRoot);
        }
    }


    private static string[] SplitPath(string path)
    {
        // Stop if invalid.
        if (string.IsNullOrWhiteSpace(path)) return Array.Empty<string>();

        return path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
    }



    // nested types
    private class PathNode
    {

        // variables
        // The node name (single path segment).
        public string Name;

        // The full path for this node relative to rig root.
        public string Path;

        // A stable key for foldout state.
        public string Key;

        // Child nodes by name.
        public Dictionary<string, PathNode> Children = new Dictionary<string, PathNode>();



        // methods
        public PathNode(string name, string path, string key)
        {
            // Store name.
            Name = name;

            // Store path.
            Path = path;

            // Store key.
            Key = key;
        }
    }
}
