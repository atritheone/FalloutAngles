// imports
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;



// class
public class AnimationFaceCurveStripper : EditorWindow
{

    // variables
    // Optional output folder for stripped copies. If empty, copies are created beside each source clip.
    [SerializeField] private DefaultAsset outputFolder;

    // Suffix used when creating stripped copies.
    [SerializeField] private string outputSuffix = "_NoFace";

    // If true, object reference curves matching face bindings are stripped too.
    [SerializeField] private bool stripObjectReferenceCurves = true;

    // If true, all BlendShape curves are stripped. This is broader than the TheAfflictedOne skeleton path match.
    [SerializeField] private bool stripBlendShapeCurves = false;

    // Clip asset paths gathered from the current project selection.
    private List<string> selectedClipPaths = new List<string>();

    // Scroll position for selected clip list.
    private Vector2 scrollPosition;

    // Status message shown to the user.
    private string status = "Select one or more .anim assets or folders, then click Refresh From Selection.";

    // The facial root used by TheAfflictedOne animation paths.
    private const string TheAfflictedOneFaceRoot = "/ORG-neck/ORG-head/ORG-facs_control";

    // Some clips contain the generated control object without the ORG- prefix.
    private const string TheAfflictedOnePlainFaceRoot = "/ORG-neck/ORG-head/facs_control";

    // Head children on TheAfflictedOne that should be considered facial animation, not body animation.
    private static readonly string[] TheAfflictedOneHeadFaceRoots =
    {
        "ORG-facs_control",
        "facs_control",
        "ORG-eyeball.R",
        "ORG-eyeball.L",
        "ORG-eyeball_lookat.R",
        "ORG-eyeball_lookat.L",
        "ORG-eyeball_lookat_master",
        "ORG-jaw",
        "ORG-jaw_upper"
    };

    // Facial control name prefixes from the TheAfflictedOne FACS hierarchy.
    private static readonly string[] TheAfflictedOneFacialNamePrefixes =
    {
        "ORG-brow_",
        "ORG-cheek_",
        "ORG-eye_",
        "ORG-jaw_",
        "ORG-mouth_",
        "ORG-nose_",
        "ORG-pucker_",
        "ORG-tongue_",
        "brow_",
        "cheek_",
        "eye_",
        "jaw_",
        "mouth_",
        "nose_",
        "pucker_",
        "tongue_"
    };



    // methods
    [MenuItem("Tools/Fallout Angles/Animation/Face Curve Stripper")]
    private static void OpenWindow()
    {
        // Open the tool window.
        GetWindow<AnimationFaceCurveStripper>("Face Curve Stripper");
    }


    private void OnEnable()
    {
        // Load current selection when the window opens.
        RefreshFromSelection();
    }


    private void OnGUI()
    {
        // Draw header.
        EditorGUILayout.LabelField("Animation Face Curve Stripper", EditorStyles.boldLabel);

        EditorGUILayout.Space();

        // Draw settings.
        outputFolder = (DefaultAsset)EditorGUILayout.ObjectField("Output Folder (optional)", outputFolder, typeof(DefaultAsset), false);

        outputSuffix = EditorGUILayout.TextField("Copy Suffix", outputSuffix);

        stripObjectReferenceCurves = EditorGUILayout.Toggle("Strip Object Curves", stripObjectReferenceCurves);

        stripBlendShapeCurves = EditorGUILayout.Toggle("Strip BlendShape Curves", stripBlendShapeCurves);

        EditorGUILayout.Space();

        // Selection controls.
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Refresh From Selection"))
            RefreshFromSelection();

        using (new EditorGUI.DisabledScope(selectedClipPaths.Count == 0))
        {
            if (GUILayout.Button("Clear"))
                selectedClipPaths.Clear();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // Draw selected clips.
        EditorGUILayout.LabelField("Input .anim Assets: " + selectedClipPaths.Count, EditorStyles.boldLabel);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(220f));

        if (selectedClipPaths.Count == 0)
        {
            EditorGUILayout.HelpBox("No .anim assets found in the current selection.", MessageType.Warning);
        }
        else
        {
            for (int i = 0; i < selectedClipPaths.Count; i++)
                EditorGUILayout.LabelField(selectedClipPaths[i]);
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();

        // Action buttons.
        using (new EditorGUI.DisabledScope(selectedClipPaths.Count == 0))
        {
            if (GUILayout.Button("Create Stripped Copies"))
                CreateStrippedCopies();

            if (GUILayout.Button("Strip Original Clips In Place"))
                StripOriginalClipsInPlace();
        }

        EditorGUILayout.Space();

        // Draw status.
        EditorGUILayout.HelpBox(status, MessageType.Info);
    }


    private void RefreshFromSelection()
    {
        // Gather .anim clips from selected clips and selected folders.
        selectedClipPaths = GetAnimClipPathsFromSelection()
            .Distinct()
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Update status.
        status = selectedClipPaths.Count == 0
            ? "No .anim assets found. Select .anim assets or folders in the Project window."
            : "Loaded .anim assets from selection: " + selectedClipPaths.Count;
    }


    private void CreateStrippedCopies()
    {
        // Validate suffix.
        string suffix = string.IsNullOrWhiteSpace(outputSuffix) ? "_NoFace" : outputSuffix.Trim();

        // Resolve optional output folder.
        string explicitFolder = GetValidOutputFolderPath();

        int clipsProcessed = 0;
        int bindingsRemoved = 0;

        for (int i = 0; i < selectedClipPaths.Count; i++)
        {
            string sourcePath = selectedClipPaths[i];

            AnimationClip sourceClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(sourcePath);

            if (sourceClip == null) continue;

            AnimationClip copy = Instantiate(sourceClip);

            copy.name = sourceClip.name + suffix;

            string sourceFolder = Path.GetDirectoryName(sourcePath);

            string outputPath = AssetDatabase.GenerateUniqueAssetPath((string.IsNullOrEmpty(explicitFolder) ? sourceFolder : explicitFolder) + "/" + copy.name + ".anim");

            AssetDatabase.CreateAsset(copy, outputPath);

            int removed = StripFaceCurves(copy, stripObjectReferenceCurves, stripBlendShapeCurves);

            EditorUtility.SetDirty(copy);

            bindingsRemoved += removed;

            clipsProcessed++;
        }

        AssetDatabase.SaveAssets();

        AssetDatabase.Refresh();

        status = "Created stripped copies: " + clipsProcessed + "\nRemoved bindings: " + bindingsRemoved;
    }


    private void StripOriginalClipsInPlace()
    {
        // Confirm destructive edit.
        bool confirmed = EditorUtility.DisplayDialog(
            "Strip Face Curves In Place",
            "This will remove matching face curves from the selected .anim assets. This cannot be undone unless the files are restored from source control or backup.",
            "Strip Originals",
            "Cancel");

        if (!confirmed)
        {
            status = "Cancelled in-place strip.";

            return;
        }

        int clipsProcessed = 0;
        int bindingsRemoved = 0;

        for (int i = 0; i < selectedClipPaths.Count; i++)
        {
            string sourcePath = selectedClipPaths[i];

            AnimationClip sourceClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(sourcePath);

            if (sourceClip == null) continue;

            int removed = StripFaceCurves(sourceClip, stripObjectReferenceCurves, stripBlendShapeCurves);

            if (removed > 0)
                EditorUtility.SetDirty(sourceClip);

            bindingsRemoved += removed;

            clipsProcessed++;
        }

        AssetDatabase.SaveAssets();

        AssetDatabase.Refresh();

        status = "Stripped original clips: " + clipsProcessed + "\nRemoved bindings: " + bindingsRemoved;
    }


    private static List<string> GetAnimClipPathsFromSelection()
    {
        // Create a stable ordered set.
        HashSet<string> paths = new HashSet<string>();

        UnityEngine.Object[] selectedObjects = Selection.objects;

        for (int i = 0; i < selectedObjects.Length; i++)
        {
            UnityEngine.Object selected = selectedObjects[i];

            if (selected == null) continue;

            string assetPath = AssetDatabase.GetAssetPath(selected);

            if (string.IsNullOrWhiteSpace(assetPath)) continue;

            if (AssetDatabase.IsValidFolder(assetPath))
            {
                string[] guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { assetPath });

                for (int j = 0; j < guids.Length; j++)
                {
                    string clipPath = AssetDatabase.GUIDToAssetPath(guids[j]);

                    if (IsAnimFilePath(clipPath))
                        paths.Add(clipPath);
                }

                continue;
            }

            if (IsAnimFilePath(assetPath) && selected is AnimationClip)
                paths.Add(assetPath);
        }

        return paths.ToList();
    }


    private static bool IsAnimFilePath(string assetPath)
    {
        // Only process native .anim files, not clips embedded in model imports.
        return !string.IsNullOrWhiteSpace(assetPath)
            && string.Equals(Path.GetExtension(assetPath), ".anim", StringComparison.OrdinalIgnoreCase);
    }


    private string GetValidOutputFolderPath()
    {
        // Empty means use each source folder.
        if (outputFolder == null) return "";

        string folderPath = AssetDatabase.GetAssetPath(outputFolder);

        return AssetDatabase.IsValidFolder(folderPath) ? folderPath : "";
    }


    private static int StripFaceCurves(AnimationClip clip, bool removeObjectReferenceCurves, bool removeBlendShapeCurves)
    {
        // Stop if invalid.
        if (clip == null) return 0;

        int removed = 0;

        // Remove float curves.
        EditorCurveBinding[] curveBindings = AnimationUtility.GetCurveBindings(clip);

        for (int i = 0; i < curveBindings.Length; i++)
        {
            EditorCurveBinding binding = curveBindings[i];

            if (!IsFaceBinding(binding, removeBlendShapeCurves)) continue;

            AnimationUtility.SetEditorCurve(clip, binding, null);

            removed++;
        }

        // Remove object reference curves if requested.
        if (removeObjectReferenceCurves)
        {
            EditorCurveBinding[] objectBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);

            for (int i = 0; i < objectBindings.Length; i++)
            {
                EditorCurveBinding binding = objectBindings[i];

                if (!IsFaceBinding(binding, removeBlendShapeCurves)) continue;

                AnimationUtility.SetObjectReferenceCurve(clip, binding, null);

                removed++;
            }
        }

        return removed;
    }


    private static bool IsFaceBinding(EditorCurveBinding binding, bool removeBlendShapeCurves)
    {
        // BlendShape curves are commonly used for imported facial animation.
        if (removeBlendShapeCurves && binding.propertyName.StartsWith("blendShape.", StringComparison.OrdinalIgnoreCase))
            return true;

        string path = NormalizePath(binding.path);

        if (string.IsNullOrEmpty(path)) return false;

        if (path.IndexOf(TheAfflictedOneFaceRoot, StringComparison.OrdinalIgnoreCase) >= 0) return true;

        if (path.IndexOf(TheAfflictedOnePlainFaceRoot, StringComparison.OrdinalIgnoreCase) >= 0) return true;

        return IsTheAfflictedOneHeadFacePath(path);
    }


    private static bool IsTheAfflictedOneHeadFacePath(string path)
    {
        // Find the head subtree in paths like _AnimationRoot/.../ORG-neck/ORG-head/ORG-jaw.
        const string marker = "/ORG-neck/ORG-head/";

        int markerIndex = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);

        if (markerIndex < 0)
        {
            // Also support paths that are already relative to ORG-neck.
            string relativeHeadMarker = "/ORG-neck/ORG-head/";

            if (!path.StartsWith(relativeHeadMarker, StringComparison.OrdinalIgnoreCase)) return false;

            markerIndex = -1;
        }

        string afterHead = markerIndex >= 0
            ? path.Substring(markerIndex + marker.Length)
            : path.Substring("/ORG-neck/ORG-head/".Length);

        if (string.IsNullOrWhiteSpace(afterHead)) return false;

        string firstSegment = afterHead.Split('/')[0];

        for (int i = 0; i < TheAfflictedOneHeadFaceRoots.Length; i++)
        {
            if (string.Equals(firstSegment, TheAfflictedOneHeadFaceRoots[i], StringComparison.OrdinalIgnoreCase))
                return true;
        }

        for (int i = 0; i < TheAfflictedOneFacialNamePrefixes.Length; i++)
        {
            if (firstSegment.StartsWith(TheAfflictedOneFacialNamePrefixes[i], StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (firstSegment.IndexOf("facs", StringComparison.OrdinalIgnoreCase) >= 0) return true;

        return false;
    }


    private static string NormalizePath(string path)
    {
        // Normalize Unity paths for simple matching.
        if (string.IsNullOrWhiteSpace(path)) return "";

        string normalized = path.Replace("\\", "/").Trim();

        normalized = normalized.Trim('/');

        return "/" + normalized;
    }
}
