using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class MiscItemPrefabRepair
{
    private const string MiscPrefabFolder = "Assets/Prefabs/Items/Misc";
    private const string MiscModelFolder = "Assets/Models/Items/Misc";
    private const string MiscDefinitionFolder = "Assets/Definitions/Items/Misc";

    private static readonly Regex SourcePrefabGuidRegex = new Regex(
        @"m_SourcePrefab:\s*\{fileID:\s*[-0-9]+,\s*guid:\s*([0-9a-fA-F]{32}),\s*type:\s*3\}",
        RegexOptions.Compiled);

    [MenuItem("Tools/Fallout Angles/Items/Reimport and Resave Misc Item Prefabs")]
    public static void ReimportAndResaveMiscItemPrefabs()
    {
        string[] prefabPaths = FindMiscPrefabPaths();
        int reimportedSources = 0;
        int savedPrefabs = 0;
        int missingPrefabs = 0;
        int failedPrefabs = 0;

        try
        {
            AssetDatabase.DisallowAutoRefresh();

            for (int i = 0; i < prefabPaths.Length; i++)
            {
                string sourceGuid = ReadSourcePrefabGuid(prefabPaths[i]);
                if (string.IsNullOrEmpty(sourceGuid))
                    continue;

                string sourcePath = AssetDatabase.GUIDToAssetPath(sourceGuid);
                if (string.IsNullOrEmpty(sourcePath))
                {
                    Debug.LogWarning("Missing source GLB for prefab: " + prefabPaths[i] + " source guid " + sourceGuid);
                    continue;
                }

                AssetDatabase.ImportAsset(sourcePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                reimportedSources++;
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            for (int i = 0; i < prefabPaths.Length; i++)
            {
                AssetDatabase.ImportAsset(prefabPaths[i], ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPaths[i]);

                if (!prefab || PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.MissingAsset)
                {
                    missingPrefabs++;
                    Debug.LogWarning("Still missing after GLB reimport: " + prefabPaths[i]);
                    continue;
                }

                GameObject root = null;
                try
                {
                    root = PrefabUtility.LoadPrefabContents(prefabPaths[i]);
                    bool success;
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPaths[i], out success);
                    if (success)
                        savedPrefabs++;
                    else
                        failedPrefabs++;
                }
                catch (Exception ex)
                {
                    failedPrefabs++;
                    Debug.LogError("Failed to resave " + prefabPaths[i] + "\n" + ex);
                }
                finally
                {
                    if (root)
                        PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }
        finally
        {
            AssetDatabase.AllowAutoRefresh();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        ItemDatabaseBuilder.RebuildDatabase();
        Debug.Log(
            "Misc item prefab reimport/resave complete. Source GLBs reimported: " + reimportedSources +
            ", prefabs saved: " + savedPrefabs +
            ", still missing: " + missingPrefabs +
            ", failed: " + failedPrefabs + ".");
    }

    [MenuItem("Tools/Fallout Angles/Items/Rebuild Misc Item Prefabs From GLBs")]
    public static void RebuildMiscItemPrefabsFromGlbs()
    {
        if (!EditorUtility.DisplayDialog(
            "Rebuild misc item prefabs",
            "This rebuilds Assets/Prefabs/Items/Misc/*.prefab from matching GLB model assets. Existing prefab .meta GUIDs are kept, but prefab contents are overwritten.",
            "Rebuild",
            "Cancel"))
        {
            return;
        }

        string[] definitionGuids = AssetDatabase.FindAssets("t:MiscDefinition", new[] { MiscDefinitionFolder });
        int rebuilt = 0;
        int skipped = 0;
        int failed = 0;

        for (int i = 0; i < definitionGuids.Length; i++)
        {
            string definitionPath = AssetDatabase.GUIDToAssetPath(definitionGuids[i]);
            string itemName = Path.GetFileNameWithoutExtension(definitionPath);
            string prefabPath = MiscPrefabFolder + "/" + itemName + ".prefab";
            string modelPath = MiscModelFolder + "/" + itemName + ".glb";

            if (!File.Exists(modelPath))
            {
                skipped++;
                Debug.LogWarning("No matching misc GLB for " + itemName + ": " + modelPath);
                continue;
            }

            try
            {
                AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

                GameObject sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                ScriptableObject definition = AssetDatabase.LoadAssetAtPath<ScriptableObject>(definitionPath);
                if (!sourceModel || !definition)
                {
                    skipped++;
                    Debug.LogWarning("Cannot load model or definition for " + itemName);
                    continue;
                }

                RebuildOnePrefab(itemName, prefabPath, sourceModel, definition);
                rebuilt++;
            }
            catch (Exception ex)
            {
                failed++;
                Debug.LogError("Failed to rebuild misc item prefab " + itemName + "\n" + ex);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ItemDatabaseBuilder.RebuildDatabase();

        Debug.Log(
            "Misc item prefab rebuild complete. Rebuilt: " + rebuilt +
            ", skipped: " + skipped +
            ", failed: " + failed + ".");
    }

    private static void RebuildOnePrefab(string itemName, string prefabPath, GameObject sourceModel, ScriptableObject definition)
    {
        PrefabSnapshot snapshot = PrefabSnapshot.Read(prefabPath);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(sourceModel);
        if (!instance)
            instance = UnityEngine.Object.Instantiate(sourceModel);

        try
        {
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = itemName;
            ApplyTransform(instance.transform, snapshot);
            instance.layer = snapshot.Layer;

            WorldItem worldItem = instance.GetComponent<WorldItem>();
            if (!worldItem)
                worldItem = instance.AddComponent<WorldItem>();
            ApplyWorldItem(worldItem, definition, snapshot);

            MeshCollider meshCollider = instance.GetComponent<MeshCollider>();
            if (!meshCollider)
                meshCollider = instance.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = FindSharedMesh(instance);
            meshCollider.convex = snapshot.ColliderConvex;
            meshCollider.isTrigger = snapshot.ColliderIsTrigger;
            meshCollider.enabled = snapshot.ColliderEnabled;

            Rigidbody rigidbody = instance.GetComponent<Rigidbody>();
            if (!rigidbody)
                rigidbody = instance.AddComponent<Rigidbody>();
            rigidbody.mass = snapshot.RigidbodyMass;
            rigidbody.linearDamping = snapshot.RigidbodyLinearDamping;
            rigidbody.angularDamping = snapshot.RigidbodyAngularDamping;
            rigidbody.useGravity = snapshot.RigidbodyUseGravity;
            rigidbody.isKinematic = snapshot.RigidbodyIsKinematic;
            rigidbody.interpolation = snapshot.RigidbodyInterpolation;
            rigidbody.constraints = snapshot.RigidbodyConstraints;
            rigidbody.collisionDetectionMode = snapshot.RigidbodyCollisionDetection;

            bool success;
            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath, out success);
            if (!success)
                throw new InvalidOperationException("SaveAsPrefabAsset returned false for " + prefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    private static string[] FindMiscPrefabPaths()
    {
        string[] paths = Directory.GetFiles(MiscPrefabFolder, "*.prefab", SearchOption.TopDirectoryOnly);
        for (int i = 0; i < paths.Length; i++)
            paths[i] = paths[i].Replace('\\', '/');

        Array.Sort(paths, StringComparer.OrdinalIgnoreCase);
        return paths;
    }

    private static string ReadSourcePrefabGuid(string prefabPath)
    {
        if (!File.Exists(prefabPath))
            return string.Empty;

        Match match = SourcePrefabGuidRegex.Match(File.ReadAllText(prefabPath));
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private static void ApplyTransform(Transform transform, PrefabSnapshot snapshot)
    {
        transform.localPosition = snapshot.LocalPosition;
        transform.localRotation = snapshot.LocalRotation;
        transform.localScale = snapshot.LocalScale;
    }

    private static void ApplyWorldItem(WorldItem worldItem, ScriptableObject definition, PrefabSnapshot snapshot)
    {
        SerializedObject serializedObject = new SerializedObject(worldItem);
        serializedObject.FindProperty("itemDefinition").objectReferenceValue = definition;
        serializedObject.FindProperty("quantity").intValue = snapshot.Quantity;
        serializedObject.FindProperty("condition").intValue = snapshot.Condition;
        serializedObject.FindProperty("destroyOnPickup").boolValue = snapshot.DestroyOnPickup;
        serializedObject.FindProperty("disableOnPickup").boolValue = snapshot.DisableOnPickup;
        serializedObject.FindProperty("pickupSfx").objectReferenceValue = snapshot.PickupSfx;
        serializedObject.FindProperty("pickupSfxVolume").floatValue = snapshot.PickupSfxVolume;
        serializedObject.FindProperty("autoPickupOnTrigger").boolValue = snapshot.AutoPickupOnTrigger;
        serializedObject.FindProperty("promptVerbOverride").stringValue = snapshot.PromptVerbOverride;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Mesh FindSharedMesh(GameObject root)
    {
        MeshFilter meshFilter = root.GetComponentInChildren<MeshFilter>(true);
        if (meshFilter && meshFilter.sharedMesh)
            return meshFilter.sharedMesh;

        SkinnedMeshRenderer skinnedMeshRenderer = root.GetComponentInChildren<SkinnedMeshRenderer>(true);
        return skinnedMeshRenderer ? skinnedMeshRenderer.sharedMesh : null;
    }

    private sealed class PrefabSnapshot
    {
        public Vector3 LocalPosition = Vector3.zero;
        public Quaternion LocalRotation = Quaternion.identity;
        public Vector3 LocalScale = Vector3.one;
        public int Layer = 8;
        public int Quantity = 1;
        public int Condition = 100;
        public bool DestroyOnPickup = true;
        public bool DisableOnPickup;
        public AudioClip PickupSfx;
        public float PickupSfxVolume = 0.8f;
        public bool AutoPickupOnTrigger;
        public string PromptVerbOverride = string.Empty;
        public bool ColliderConvex = true;
        public bool ColliderIsTrigger;
        public bool ColliderEnabled = true;
        public float RigidbodyMass = 1.0f;
        public float RigidbodyLinearDamping;
        public float RigidbodyAngularDamping = 0.05f;
        public bool RigidbodyUseGravity = true;
        public bool RigidbodyIsKinematic;
        public RigidbodyInterpolation RigidbodyInterpolation = RigidbodyInterpolation.None;
        public RigidbodyConstraints RigidbodyConstraints = RigidbodyConstraints.None;
        public CollisionDetectionMode RigidbodyCollisionDetection = CollisionDetectionMode.Discrete;

        public static PrefabSnapshot Read(string prefabPath)
        {
            PrefabSnapshot snapshot = new PrefabSnapshot();
            if (!File.Exists(prefabPath))
                return snapshot;

            string text = File.ReadAllText(prefabPath);
            snapshot.LocalPosition = new Vector3(
                ReadFloat(text, "m_LocalPosition.x", 0.0f),
                ReadFloat(text, "m_LocalPosition.y", 0.0f),
                ReadFloat(text, "m_LocalPosition.z", 0.0f));
            snapshot.LocalRotation = new Quaternion(
                ReadFloat(text, "m_LocalRotation.x", 0.0f),
                ReadFloat(text, "m_LocalRotation.y", 0.0f),
                ReadFloat(text, "m_LocalRotation.z", 0.0f),
                ReadFloat(text, "m_LocalRotation.w", 1.0f));
            snapshot.LocalScale = new Vector3(
                ReadFloat(text, "m_LocalScale.x", 1.0f),
                ReadFloat(text, "m_LocalScale.y", 1.0f),
                ReadFloat(text, "m_LocalScale.z", 1.0f));
            snapshot.Layer = ReadInt(text, "m_Layer", 8);
            snapshot.Quantity = ReadYamlInt(text, "quantity", 1);
            snapshot.Condition = ReadYamlInt(text, "condition", 100);
            snapshot.DestroyOnPickup = ReadYamlBool(text, "destroyOnPickup", true);
            snapshot.DisableOnPickup = ReadYamlBool(text, "disableOnPickup", false);
            snapshot.PickupSfxVolume = ReadYamlFloat(text, "pickupSfxVolume", 0.8f);
            snapshot.AutoPickupOnTrigger = ReadYamlBool(text, "autoPickupOnTrigger", false);
            snapshot.PromptVerbOverride = ReadYamlString(text, "promptVerbOverride", string.Empty);
            snapshot.ColliderConvex = ReadYamlBool(text, "m_Convex", true);
            snapshot.ColliderIsTrigger = ReadYamlBool(text, "m_IsTrigger", false);
            snapshot.ColliderEnabled = ReadYamlBool(text, "m_Enabled", true, "--- !u!64");
            snapshot.RigidbodyMass = ReadYamlFloat(text, "m_Mass", 1.0f);
            snapshot.RigidbodyLinearDamping = ReadYamlFloat(text, "m_LinearDamping", 0.0f);
            snapshot.RigidbodyAngularDamping = ReadYamlFloat(text, "m_AngularDamping", 0.05f);
            snapshot.RigidbodyUseGravity = ReadYamlBool(text, "m_UseGravity", true);
            snapshot.RigidbodyIsKinematic = ReadYamlBool(text, "m_IsKinematic", false);
            snapshot.RigidbodyInterpolation = (RigidbodyInterpolation)ReadYamlInt(text, "m_Interpolate", 0);
            snapshot.RigidbodyConstraints = (RigidbodyConstraints)ReadYamlInt(text, "m_Constraints", 0);
            snapshot.RigidbodyCollisionDetection = (CollisionDetectionMode)ReadYamlInt(text, "m_CollisionDetection", 0);
            return snapshot;
        }

        private static float ReadFloat(string text, string propertyPath, float defaultValue)
        {
            Match match = Regex.Match(
                text,
                @"propertyPath:\s*" + Regex.Escape(propertyPath) + @"\s*\r?\n\s*value:\s*([-+0-9.eE]+)");
            return match.Success ? ParseFloat(match.Groups[1].Value, defaultValue) : defaultValue;
        }

        private static int ReadInt(string text, string propertyPath, int defaultValue)
        {
            Match match = Regex.Match(
                text,
                @"propertyPath:\s*" + Regex.Escape(propertyPath) + @"\s*\r?\n\s*value:\s*([-+0-9]+)");
            return match.Success ? ParseInt(match.Groups[1].Value, defaultValue) : defaultValue;
        }

        private static int ReadYamlInt(string text, string propertyName, int defaultValue)
        {
            Match match = Regex.Match(text, @"^\s*" + Regex.Escape(propertyName) + @":\s*([-+0-9]+)\s*$", RegexOptions.Multiline);
            return match.Success ? ParseInt(match.Groups[1].Value, defaultValue) : defaultValue;
        }

        private static bool ReadYamlBool(string text, string propertyName, bool defaultValue, string sectionMarker = null)
        {
            string source = NarrowToSection(text, sectionMarker);
            Match match = Regex.Match(source, @"^\s*" + Regex.Escape(propertyName) + @":\s*([01])\s*$", RegexOptions.Multiline);
            return match.Success ? match.Groups[1].Value == "1" : defaultValue;
        }

        private static float ReadYamlFloat(string text, string propertyName, float defaultValue)
        {
            Match match = Regex.Match(text, @"^\s*" + Regex.Escape(propertyName) + @":\s*([-+0-9.eE]+)\s*$", RegexOptions.Multiline);
            return match.Success ? ParseFloat(match.Groups[1].Value, defaultValue) : defaultValue;
        }

        private static string ReadYamlString(string text, string propertyName, string defaultValue)
        {
            Match match = Regex.Match(text, @"^\s*" + Regex.Escape(propertyName) + @":\s*(.*)$", RegexOptions.Multiline);
            return match.Success ? match.Groups[1].Value.Trim() : defaultValue;
        }

        private static string NarrowToSection(string text, string sectionMarker)
        {
            if (string.IsNullOrEmpty(sectionMarker))
                return text;

            int start = text.IndexOf(sectionMarker, StringComparison.Ordinal);
            if (start < 0)
                return text;

            int next = text.IndexOf("--- !u!", start + sectionMarker.Length, StringComparison.Ordinal);
            return next < 0 ? text.Substring(start) : text.Substring(start, next - start);
        }

        private static float ParseFloat(string value, float defaultValue)
        {
            float parsed;
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ? parsed : defaultValue;
        }

        private static int ParseInt(string value, int defaultValue)
        {
            int parsed;
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ? parsed : defaultValue;
        }
    }
}
