// imports
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;



// class
public class GameObjectExactSwapTool : EditorWindow
{

	// variables
	// The object in your scene that you want to overwrite so it becomes an exact match.
	[SerializeField] private GameObject targetToOverwrite;

	// The object you want to copy from (your “template” model / rig / setup).
	[SerializeField] private GameObject sourceTemplate;

	// If true, keep the target root Transform (position/rotation/scale) instead of copying the source root Transform.
	[SerializeField] private bool keepTargetRootTransform = true;

	// If true, keep the target root name instead of copying the source root name.
	[SerializeField] private bool keepTargetRootName = true;

	// If true, keep the target root parent and sibling index (so it stays where it is in the hierarchy).
	[SerializeField] private bool keepTargetRootParentAndSibling = true;

	// If true, keep the target root active state (SetActive) instead of copying the source root active state.
	[SerializeField] private bool keepTargetRootActiveState = false;

	// If true, do not touch tags/layers/static flags at the root (useful if your scene expects a specific tag/layer).
	[SerializeField] private bool keepTargetRootTagLayerStatic = false;

	// If true, also copy tag/layer/static flags for every child GameObject (usually you want this ON for exact mirroring).
	[SerializeField] private bool copyChildTagLayerStatic = true;

	// If true, child order will be made identical (extra children removed, missing created, and order matched).
	[SerializeField] private bool enforceExactChildOrder = true;



	// methods
	[MenuItem("Tools/Fallout Angles/Exact Swap GameObject...")]
	private static void OpenWindow()
	{
		// Create and show the window.
		GetWindow<GameObjectExactSwapTool>("Exact Swap GameObject");

		// Ensure the window can be found after domain reload.
	}

	
	private void OnGUI()
	{
		// Draw a small header.
		EditorGUILayout.LabelField("Exact Swap (Copy Source -> Target)", EditorStyles.boldLabel);

		// Add some spacing.
		EditorGUILayout.Space();

		// Allow user to pick the target root.
		targetToOverwrite = (GameObject)EditorGUILayout.ObjectField("Target (overwrite)", targetToOverwrite, typeof(GameObject), true);

		// Allow user to pick the source template root.
		sourceTemplate = (GameObject)EditorGUILayout.ObjectField("Source (copy from)", sourceTemplate, typeof(GameObject), true);

		// Add some spacing.
		EditorGUILayout.Space();

		// Show options.
		EditorGUILayout.LabelField("Root Options", EditorStyles.boldLabel);

		// Toggle whether to keep target root transform.
		keepTargetRootTransform = EditorGUILayout.Toggle("Keep target root Transform", keepTargetRootTransform);

		// Toggle whether to keep target root name.
		keepTargetRootName = EditorGUILayout.Toggle("Keep target root name", keepTargetRootName);

		// Toggle whether to keep target root parent/sibling.
		keepTargetRootParentAndSibling = EditorGUILayout.Toggle("Keep root parent/sibling", keepTargetRootParentAndSibling);

		// Toggle whether to keep target active state.
		keepTargetRootActiveState = EditorGUILayout.Toggle("Keep target root active", keepTargetRootActiveState);

		// Toggle whether to keep target tag/layer/static.
		keepTargetRootTagLayerStatic = EditorGUILayout.Toggle("Keep root tag/layer/static", keepTargetRootTagLayerStatic);

		// Add some spacing.
		EditorGUILayout.Space();

		// Show child options.
		EditorGUILayout.LabelField("Child Options", EditorStyles.boldLabel);

		// Toggle whether to copy tag/layer/static for children.
		copyChildTagLayerStatic = EditorGUILayout.Toggle("Copy child tag/layer/static", copyChildTagLayerStatic);

		// Toggle whether to enforce exact child order.
		enforceExactChildOrder = EditorGUILayout.Toggle("Enforce exact child order", enforceExactChildOrder);

		// Add some spacing.
		EditorGUILayout.Space();

		// Disable the button if inputs are missing.
		EditorGUI.BeginDisabledGroup(targetToOverwrite == null || sourceTemplate == null);

		// Run button.
		if (GUILayout.Button("Swap Now (Make Target Exactly Match Source)"))
		{
			// Execute the swap operation.
			SwapToExactMatch(targetToOverwrite, sourceTemplate);
		}

		// Re-enable GUI.
		EditorGUI.EndDisabledGroup();

		// Provide a quick usage hint.
		EditorGUILayout.HelpBox("This overwrites the TARGET hierarchy/components so it becomes an exact match of SOURCE. Use Undo if needed.", MessageType.Info);
	}


	private void SwapToExactMatch(GameObject targetRoot, GameObject sourceRoot)
	{
		// Stop if inputs are invalid.
		if (targetRoot == null || sourceRoot == null) return;

		// Stop if they are the same object.
		if (targetRoot == sourceRoot) return;

		// Cache the target root parent if requested.
		Transform cachedParent = keepTargetRootParentAndSibling ? targetRoot.transform.parent : null;

		// Cache the target root sibling index if requested.
		int cachedSiblingIndex = keepTargetRootParentAndSibling ? targetRoot.transform.GetSiblingIndex() : 0;

		// Cache the target root local transform if requested.
		Vector3 cachedLocalPos = keepTargetRootTransform ? targetRoot.transform.localPosition : Vector3.zero;

		// Cache the target root local rotation if requested.
		Quaternion cachedLocalRot = keepTargetRootTransform ? targetRoot.transform.localRotation : Quaternion.identity;

		// Cache the target root local scale if requested.
		Vector3 cachedLocalScale = keepTargetRootTransform ? targetRoot.transform.localScale : Vector3.one;

		// Cache the target root name if requested.
		string cachedName = keepTargetRootName ? targetRoot.name : string.Empty;

		// Cache the target root active state if requested.
		bool cachedActive = keepTargetRootActiveState ? targetRoot.activeSelf : false;

		// Cache root tag/layer/static flags if requested.
		string cachedTag = keepTargetRootTagLayerStatic ? targetRoot.tag : string.Empty;

		// Cache root layer if requested.
		int cachedLayer = keepTargetRootTagLayerStatic ? targetRoot.layer : 0;

		// Cache root static flags if requested.
		StaticEditorFlags cachedStaticFlags = keepTargetRootTagLayerStatic ? GameObjectUtility.GetStaticEditorFlags(targetRoot) : 0;

		// Register Undo for the whole target hierarchy.
		Undo.RegisterFullObjectHierarchyUndo(targetRoot, "Exact Swap GameObject");

		// Make the target root match the source root (name/tag/layer/static/active).
		CopyGameObjectMeta(sourceRoot, targetRoot, copyMeta: !keepTargetRootTagLayerStatic, copyActive: !keepTargetRootActiveState, copyName: !keepTargetRootName);

		// Copy transform data from source root to target root unless we keep target transform.
		if (!keepTargetRootTransform)
			CopyTransform(sourceRoot.transform, targetRoot.transform);

		// Sync hierarchy so children match exactly.
		SyncHierarchyExact(sourceRoot.transform, targetRoot.transform);

		// Sync components for the root.
		SyncComponentsExact(sourceRoot, targetRoot);

		// Sync components for every child pair by path.
		SyncAllChildComponentsByPath(sourceRoot.transform, targetRoot.transform);

		// Restore root parent/sibling if requested.
		if (keepTargetRootParentAndSibling)
		{
			// Restore the original parent.
			targetRoot.transform.SetParent(cachedParent, true);

			// Restore the original sibling index.
			targetRoot.transform.SetSiblingIndex(cachedSiblingIndex);
		}

		// Restore root transform if requested.
		if (keepTargetRootTransform)
		{
			// Restore local position.
			targetRoot.transform.localPosition = cachedLocalPos;

			// Restore local rotation.
			targetRoot.transform.localRotation = cachedLocalRot;

			// Restore local scale.
			targetRoot.transform.localScale = cachedLocalScale;
		}

		// Restore root name if requested.
		if (keepTargetRootName)
			targetRoot.name = cachedName;

		// Restore root active state if requested.
		if (keepTargetRootActiveState)
			targetRoot.SetActive(cachedActive);

		// Restore root tag/layer/static flags if requested.
		if (keepTargetRootTagLayerStatic)
		{
			// Restore tag.
			targetRoot.tag = cachedTag;

			// Restore layer.
			targetRoot.layer = cachedLayer;

			// Restore static flags.
			GameObjectUtility.SetStaticEditorFlags(targetRoot, cachedStaticFlags);
		}

		// Mark the scene dirty so the change is saved.
		EditorSceneManager.MarkSceneDirty(targetRoot.scene);
	}


	private void CopyGameObjectMeta(GameObject source, GameObject target, bool copyMeta, bool copyActive, bool copyName)
	{
		// Copy the name if requested.
		if (copyName)
			target.name = source.name;

		// Copy active state if requested.
		if (copyActive)
			target.SetActive(source.activeSelf);

		// Copy tag/layer/static flags if requested.
		if (copyMeta)
		{
			// Copy tag.
			target.tag = source.tag;

			// Copy layer.
			target.layer = source.layer;

			// Copy static editor flags.
			GameObjectUtility.SetStaticEditorFlags(target, GameObjectUtility.GetStaticEditorFlags(source));
		}
	}


	private void CopyTransform(Transform source, Transform target)
	{
		// Copy local position.
		target.localPosition = source.localPosition;

		// Copy local rotation.
		target.localRotation = source.localRotation;

		// Copy local scale.
		target.localScale = source.localScale;
	}


	private void SyncHierarchyExact(Transform sourceRoot, Transform targetRoot)
	{
		// Build a map of source children by name, preserving order.
		List<Transform> sourceChildren = new List<Transform>();

		// Collect source children.
		for (int i = 0; i < sourceRoot.childCount; i++)
			sourceChildren.Add(sourceRoot.GetChild(i));

		// Build a map of target children by name (first match only).
		Dictionary<string, Transform> targetByName = new Dictionary<string, Transform>();

		// Collect target children.
		for (int i = 0; i < targetRoot.childCount; i++)
		{
			// Read the child.
			Transform child = targetRoot.GetChild(i);

			// Add if not present yet.
			if (!targetByName.ContainsKey(child.name))
				targetByName.Add(child.name, child);
		}

		// Create or match children in source order.
		for (int i = 0; i < sourceChildren.Count; i++)
		{
			// Read the source child.
			Transform sourceChild = sourceChildren[i];

			// Try to find a matching target child by name.
			Transform targetChild = targetByName.ContainsKey(sourceChild.name) ? targetByName[sourceChild.name] : null;

			// If missing, create it.
			if (targetChild == null)
			{
				// Create a new GameObject with the same name.
				GameObject created = new GameObject(sourceChild.name);

				// Register Undo for creation.
				Undo.RegisterCreatedObjectUndo(created, "Create Missing Child");

				// Parent it under the target root.
				created.transform.SetParent(targetRoot, false);

				// Set its sibling index to match source ordering.
				created.transform.SetSiblingIndex(i);

				// Copy transform values from source child.
				CopyTransform(sourceChild, created.transform);

				// Copy tag/layer/static flags if enabled.
				if (copyChildTagLayerStatic)
					CopyGameObjectMeta(sourceChild.gameObject, created, copyMeta: true, copyActive: true, copyName: true);

				// Replace handle for recursion.
				targetChild = created.transform;
			}
			else
			{
				// Ensure sibling order matches if enabled.
				if (enforceExactChildOrder)
					targetChild.SetSiblingIndex(i);

				// Copy transform values to match the source child.
				CopyTransform(sourceChild, targetChild);

				// Copy tag/layer/static flags if enabled.
				if (copyChildTagLayerStatic)
					CopyGameObjectMeta(sourceChild.gameObject, targetChild.gameObject, copyMeta: true, copyActive: true, copyName: true);
			}

			// Recurse to sync grandchildren.
			SyncHierarchyExact(sourceChild, targetChild);
		}

		// Remove extra target children that do not exist in the source (only if enforcing exactness).
		if (enforceExactChildOrder)
		{
			// Build a set of valid source child names.
			HashSet<string> validNames = new HashSet<string>();

			// Fill the set from source.
			for (int i = 0; i < sourceRoot.childCount; i++)
				validNames.Add(sourceRoot.GetChild(i).name);

			// Collect removals first (can’t modify while iterating indices safely).
			List<GameObject> toRemove = new List<GameObject>();

			// Scan target children.
			for (int i = 0; i < targetRoot.childCount; i++)
			{
				// Read the target child.
				Transform targetChild = targetRoot.GetChild(i);

				// If not in the source list, schedule removal.
				if (!validNames.Contains(targetChild.name))
					toRemove.Add(targetChild.gameObject);
			}

			// Remove extras.
			for (int i = 0; i < toRemove.Count; i++)
				Undo.DestroyObjectImmediate(toRemove[i]);
		}
	}


	private void SyncAllChildComponentsByPath(Transform sourceRoot, Transform targetRoot)
	{
		// Build a dictionary of all source transforms by relative path.
		Dictionary<string, Transform> sourceByPath = BuildPathMap(sourceRoot);

		// Build a dictionary of all target transforms by relative path.
		Dictionary<string, Transform> targetByPath = BuildPathMap(targetRoot);

		// For every source path, sync the matching target.
		foreach (KeyValuePair<string, Transform> kvp in sourceByPath)
		{
			// Read the relative path.
			string path = kvp.Key;

			// Read the source transform.
			Transform source = kvp.Value;

			// Skip the root path (empty string) because we handled root already.
			if (string.IsNullOrEmpty(path)) continue;

			// Stop if the target is missing (should not happen after hierarchy sync, but stay safe).
			if (!targetByPath.ContainsKey(path)) continue;

			// Read the target transform.
			Transform target = targetByPath[path];

			// Sync the GameObject-level components.
			SyncComponentsExact(source.gameObject, target.gameObject);
		}
	}


	private Dictionary<string, Transform> BuildPathMap(Transform root)
	{
		// Create a dictionary for path mapping.
		Dictionary<string, Transform> map = new Dictionary<string, Transform>();

		// Add the root as empty path.
		map.Add(string.Empty, root);

		// Recurse children.
		BuildPathMapRecursive(root, string.Empty, map);

		// Return the completed map.
		return map;
	}


	private void BuildPathMapRecursive(Transform current, string currentPath, Dictionary<string, Transform> map)
	{
		// Iterate children.
		for (int i = 0; i < current.childCount; i++)
		{
			// Read child.
			Transform child = current.GetChild(i);

			// Build the child path.
			string childPath = string.IsNullOrEmpty(currentPath) ? child.name : currentPath + "/" + child.name;

			// Add to map if not present.
			if (!map.ContainsKey(childPath))
				map.Add(childPath, child);

			// Recurse deeper.
			BuildPathMapRecursive(child, childPath, map);
		}
	}


	private void SyncComponentsExact(GameObject sourceGO, GameObject targetGO)
	{
		// Get all components on source (including inactive).
		Component[] sourceComponents = sourceGO.GetComponents<Component>();

		// Get all components on target (including inactive).
		Component[] targetComponents = targetGO.GetComponents<Component>();

		// Build lists excluding Transform (must always exist).
		List<Component> sourceList = BuildComponentListExcludingTransform(sourceComponents);

		// Build lists excluding Transform (must always exist).
		List<Component> targetList = BuildComponentListExcludingTransform(targetComponents);

		// Group target components by type.
		Dictionary<System.Type, List<Component>> targetByType = GroupComponentsByType(targetList);

		// Track which target components should remain.
		HashSet<Component> keep = new HashSet<Component>();

		// For each source component, ensure the target has a matching one (by type and index order).
		Dictionary<System.Type, int> seenTypeCount = new Dictionary<System.Type, int>();

		// Iterate source components in their inspector order.
		for (int i = 0; i < sourceList.Count; i++)
		{
			// Read source component.
			Component src = sourceList[i];

			// Read its type.
			System.Type type = src.GetType();

			// Update per-type index.
			if (!seenTypeCount.ContainsKey(type))
				seenTypeCount.Add(type, 0);

			// Read the desired index for this type.
			int typeIndex = seenTypeCount[type];

			// Increment for next time.
			seenTypeCount[type] = typeIndex + 1;

			// Find or create the destination component at this type index.
			Component dst = GetOrCreateComponentAtTypeIndex(targetGO, targetByType, type, typeIndex);

			// Mark this destination component as kept.
			if (dst != null)
				keep.Add(dst);

			// Copy serialized data, but preserve target visuals (mesh/materials) so the model stays visible.
			CopySerializedPreservingTargetVisuals(src, dst);
		}

		// Remove target components that are not present in the source (exact match).
		for (int i = 0; i < targetList.Count; i++)
		{
			// Read target component.
			Component tgt = targetList[i];

			// Skip anything we want to keep.
			if (keep.Contains(tgt)) continue;

			// Destroy the extra component via Undo.
			Undo.DestroyObjectImmediate(tgt);
		}

		// Refresh and reorder components to match the source order (best-effort).
		ReorderTargetComponentsToMatchSource(sourceGO, targetGO);
	}


	private List<Component> BuildComponentListExcludingTransform(Component[] components)
	{
		// Create output list.
		List<Component> list = new List<Component>();

		// Iterate components.
		for (int i = 0; i < components.Length; i++)
		{
			// Skip null (missing scripts).
			if (components[i] == null) continue;

			// Skip Transform.
			if (components[i] is Transform) continue;

			// Keep everything else.
			list.Add(components[i]);
		}

		// Return list.
		return list;
	}


	private Dictionary<System.Type, List<Component>> GroupComponentsByType(List<Component> comps)
	{
		// Create dictionary.
		Dictionary<System.Type, List<Component>> grouped = new Dictionary<System.Type, List<Component>>();

		// Iterate components.
		for (int i = 0; i < comps.Count; i++)
		{
			// Read component.
			Component c = comps[i];

			// Read type.
			System.Type t = c.GetType();

			// Create list if missing.
			if (!grouped.ContainsKey(t))
				grouped.Add(t, new List<Component>());

			// Add component.
			grouped[t].Add(c);
		}

		// Return grouped dictionary.
		return grouped;
	}


	private Component GetOrCreateComponentAtTypeIndex(GameObject targetGO, Dictionary<System.Type, List<Component>> targetByType, System.Type type, int index)
	{
		// Ensure list exists for this type.
		if (!targetByType.ContainsKey(type))
			targetByType.Add(type, new List<Component>());

		// Read the list.
		List<Component> list = targetByType[type];

		// If the component exists at that index, return it.
		if (index >= 0 && index < list.Count)
			return list[index];

		// Otherwise, add components until we reach the requested index.
		while (list.Count <= index)
		{
			// Add a new component of the given type.
			Component created = Undo.AddComponent(targetGO, type);

			// Add it to the tracking list.
			list.Add(created);
		}

		// Return the newly created one at index.
		return list[index];
	}


	private void ReorderTargetComponentsToMatchSource(GameObject sourceGO, GameObject targetGO)
	{
		// Grab component arrays again after changes.
		Component[] sourceComponents = sourceGO.GetComponents<Component>();

		// Grab component arrays again after changes.
		Component[] targetComponents = targetGO.GetComponents<Component>();

		// Build desired order list excluding Transform.
		List<System.Type> desiredTypesInOrder = new List<System.Type>();

		// Build desired order by inspector order.
		for (int i = 0; i < sourceComponents.Length; i++)
		{
			// Skip null (missing scripts).
			if (sourceComponents[i] == null) continue;

			// Skip Transform.
			if (sourceComponents[i] is Transform) continue;

			// Add the type to the order list (duplicates allowed).
			desiredTypesInOrder.Add(sourceComponents[i].GetType());
		}

		// For each desired type occurrence, move a matching target component upwards (best-effort).
		int insertAfterTransformIndex = 1;

		// Track how many of each type we have already placed.
		Dictionary<System.Type, int> placedCount = new Dictionary<System.Type, int>();

		// Iterate desired types.
		for (int i = 0; i < desiredTypesInOrder.Count; i++)
		{
			// Read desired type.
			System.Type desiredType = desiredTypesInOrder[i];

			// Ensure placed count exists.
			if (!placedCount.ContainsKey(desiredType))
				placedCount.Add(desiredType, 0);

			// Read which instance of this type we want.
			int desiredInstanceIndex = placedCount[desiredType];

			// Increment for next time.
			placedCount[desiredType] = desiredInstanceIndex + 1;

			// Find the Nth component of this type on target.
			Component targetMatch = GetNthComponentOfType(targetGO, desiredType, desiredInstanceIndex);

			// Skip if not found.
			if (targetMatch == null) continue;

			// Move the component up until it sits after the previously placed ones (Unity only supports MoveUp).
			MoveComponentToIndexBestEffort(targetMatch, insertAfterTransformIndex);

			// Advance desired insert index.
			insertAfterTransformIndex++;
		}
	}


	private Component GetNthComponentOfType(GameObject go, System.Type type, int n)
	{
		// Get all components.
		Component[] comps = go.GetComponents<Component>();

		// Track count.
		int count = 0;

		// Iterate components.
		for (int i = 0; i < comps.Length; i++)
		{
			// Skip null.
			if (comps[i] == null) continue;

			// Skip Transform.
			if (comps[i] is Transform) continue;

			// Skip if not matching type.
			if (comps[i].GetType() != type) continue;

			// If this is the nth, return it.
			if (count == n)
				return comps[i];

			// Otherwise increment.
			count++;
		}

		// Not found.
		return null;
	}


	private void MoveComponentToIndexBestEffort(Component component, int desiredIndexAfterTransform)
	{
		// Unity component ordering is limited; we can only MoveUp/MoveDown in editor via internal utility.
		// We do a simple “bubble up” by repeatedly moving up while its current index is too low priority.

		// Get the current components array.
		Component[] comps = component.gameObject.GetComponents<Component>();

		// Find current index.
		int currentIndex = IndexOfComponent(comps, component);

		// Stop if not found.
		if (currentIndex < 0) return;

		// Transform is index 0, so desiredIndexAfterTransform is already in “component array index space”.
		int desiredIndex = Mathf.Max(1, desiredIndexAfterTransform);

		// While we are below the desired index, move up.
		while (currentIndex > desiredIndex)
		{
			// Move up once.
			UnityEditorInternal.ComponentUtility.MoveComponentUp(component);

			// Refresh components.
			comps = component.gameObject.GetComponents<Component>();

			// Recompute current index.
			currentIndex = IndexOfComponent(comps, component);

			// Stop if something went wrong.
			if (currentIndex < 0) break;
		}
	}


	private int IndexOfComponent(Component[] comps, Component target)
	{
		// Iterate array.
		for (int i = 0; i < comps.Length; i++)
		{
			// Check reference equality.
			if (comps[i] == target)
				return i;
		}

		// Not found.
		return -1;
	}


	private void CopySerializedPreservingTargetVisuals(Component source, Component target)
	{
		// Nothing to copy if either side is missing.
		if (source == null || target == null) return;

		// Preserve target mesh/materials so the swapped-in model stays visible.
		Mesh preservedMesh = null;
		Mesh preservedColliderMesh = null;
		Sprite preservedSprite = null;
		Material[] preservedMaterials = null;

		MeshFilter meshFilter = target as MeshFilter;
		if (meshFilter != null)
			preservedMesh = meshFilter.sharedMesh;

		SkinnedMeshRenderer skinned = target as SkinnedMeshRenderer;
		if (skinned != null)
			preservedMesh = skinned.sharedMesh;

		MeshCollider meshCollider = target as MeshCollider;
		if (meshCollider != null)
			preservedColliderMesh = meshCollider.sharedMesh;

		SpriteRenderer spriteRenderer = target as SpriteRenderer;
		if (spriteRenderer != null)
			preservedSprite = spriteRenderer.sprite;

		Renderer renderer = target as Renderer;
		if (renderer != null)
			preservedMaterials = renderer.sharedMaterials;

		// Copy all serialized data.
		EditorUtility.CopySerialized(source, target);

		// Restore preserved visuals.
		if (meshFilter != null)
			meshFilter.sharedMesh = preservedMesh;

		if (skinned != null)
			skinned.sharedMesh = preservedMesh;

		if (meshCollider != null)
			meshCollider.sharedMesh = preservedColliderMesh;

		if (spriteRenderer != null)
			spriteRenderer.sprite = preservedSprite;

		if (renderer != null)
			renderer.sharedMaterials = preservedMaterials;
	}
}
