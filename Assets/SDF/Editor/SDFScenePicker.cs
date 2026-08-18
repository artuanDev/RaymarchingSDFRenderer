using System.Collections.Generic;
using UnityEditor;
using Unity.Profiling;
using UnityEngine;

namespace SdfRenderer.Editor
{
    [InitializeOnLoad]
    public static class SDFScenePicker
    {
        private const string PreferenceKey = "SDF.ScenePicking.Enabled";
        private const string MenuPath = "Tools/SDF/Enable Scene Picking";
        private static readonly int PickerControlHint = "SDFScenePicker".GetHashCode();
        private static readonly ProfilerMarker RaycastMarker = new ProfilerMarker("SDF/Editor Pick Raycast");
        private static readonly ProfilerMarker RebuildMarker = new ProfilerMarker("SDF/Editor Pick BVH Rebuild");
        private static readonly ProfilerMarker RefitMarker = new ProfilerMarker("SDF/Editor Pick BVH Refit");
        private static readonly List<SDFShape> Shapes = new List<SDFShape>(256);
        private static readonly List<SDFModifier> Modifiers = new List<SDFModifier>(256);
        private static readonly List<SDFModifier> ModifierScratch = new List<SDFModifier>(8);
        private static readonly List<Object> SelectionScratch = new List<Object>(16);
        private static readonly EntryComparer SortComparer = new EntryComparer();
        private static PickerEntry[] s_Entries = System.Array.Empty<PickerEntry>();
        private static PickerNode[] s_Nodes = System.Array.Empty<PickerNode>();
        private static int[] s_EntryOrder = System.Array.Empty<int>();
        private static int[] s_TraversalStack = System.Array.Empty<int>();
        private static int s_EntryCount;
        private static int s_NodeCount;
        private static int s_RootNode = -1;
        private static uint s_SpatialVersion;
        private static int s_CachedSceneViewId;
        private static uint s_CachedRaycastVersion;
        private static bool s_HasCachedRaycast;
        private static bool s_CachedHit;
        private static double s_CachedRaycastTime;
        private static Ray s_CachedRay;
        private static SDFShape s_CachedShape;
        private static float s_CachedDistance;

        private struct PickerEntry
        {
            public SDFShape Shape;
            public int ModifierStart;
            public int ModifierCount;
            public Bounds LocalBounds;
            public Bounds Bounds;
        }

        private struct PickerNode
        {
            public Bounds Bounds;
            public int Left;
            public int Right;
            public int Entry;
        }

        private sealed class EntryComparer : IComparer<int>
        {
            internal int Axis;

            public int Compare(int left, int right)
            {
                float a = s_Entries[left].Bounds.center[Axis];
                float b = s_Entries[right].Bounds.center[Axis];
                int comparison = a.CompareTo(b);
                return comparison != 0 ? comparison : left.CompareTo(right);
            }
        }

        static SDFScenePicker()
        {
            SceneView.duringSceneGui += DuringSceneGui;
            Menu.SetChecked(MenuPath, IsEnabled);
        }

        private static bool IsEnabled => EditorPrefs.GetBool(PreferenceKey, true);

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            bool enabled = !IsEnabled;
            EditorPrefs.SetBool(PreferenceKey, enabled);
            Menu.SetChecked(MenuPath, enabled);
            SceneView.RepaintAll();
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidation()
        {
            Menu.SetChecked(MenuPath, IsEnabled);
            return true;
        }

        private static void DuringSceneGui(SceneView sceneView)
        {
            Event current = Event.current;
            if (!IsEnabled || current == null)
                return;

            int pickerControl = GUIUtility.GetControlID(PickerControlHint, FocusType.Passive);
            if (current.type == EventType.Layout)
            {
                // Register only over a real, visible SDF surface. Unlike a default
                // control registered everywhere, this does not steal ordinary geometry
                // or empty-space clicks. Registering it as the fallback also gives
                // transform handles and other gizmos higher picking priority.
                if (TryRaycastSdf(sceneView, current.mousePosition, out _, out _, out _))
                    HandleUtility.AddDefaultControl(pickerControl);
                return;
            }

            if (current.type != EventType.MouseDown ||
                current.button != 0 ||
                current.alt ||
                GUIUtility.hotControl != 0 ||
                HandleUtility.nearestControl != pickerControl)
                return;

            if (!TryRaycastSdf(sceneView, current.mousePosition, out SDFShape bestShape,
                    out float bestDistance, out Ray ray))
                return;

            GameObject target = bestShape.gameObject;
            // PickGameObject performs an internal rendered pick, so it must never run
            // during Layout. On the actual click it lets regular geometry in front of
            // an SDF win while the conditional SDF control remains responsible for the
            // event itself.
            GameObject conventionalPick = HandleUtility.PickGameObject(current.mousePosition, false);
            if (conventionalPick != null && conventionalPick.GetComponentInParent<SDFShape>() == null &&
                (!TryGetConventionalDistance(conventionalPick, ray, out float conventionalDistance) ||
                 conventionalDistance + 0.001f < bestDistance))
                target = conventionalPick;

            // Match Unity's normal selection modifiers: Shift adds to the current
            // selection and Ctrl/Cmd toggles the clicked item.
            ApplySelection(target, current.shift, EditorGUI.actionKey);
            current.Use();
            SceneView.RepaintAll();
        }

        private static bool TryRaycastSdf(SceneView sceneView, Vector2 mousePosition,
            out SDFShape bestShape, out float bestDistance, out Ray ray)
        {
            ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            Event current = Event.current;
            if (s_HasCachedRaycast &&
                EditorApplication.timeSinceStartup - s_CachedRaycastTime <= 0.1d &&
                s_CachedSceneViewId == sceneView.GetInstanceID() &&
                s_CachedRaycastVersion == SDFSceneRegistry.Version &&
                (s_CachedRay.origin - ray.origin).sqrMagnitude < 1e-10f &&
                (s_CachedRay.direction - ray.direction).sqrMagnitude < 1e-10f)
            {
                if (!s_CachedHit)
                {
                    bestShape = null;
                    bestDistance = s_CachedDistance;
                    return false;
                }
                if (s_CachedShape != null &&
                    (s_CachedShape.hideFlags & HideFlags.NotEditable) == 0 &&
                    !SceneVisibilityManager.instance.IsHidden(s_CachedShape.gameObject) &&
                    !SceneVisibilityManager.instance.IsPickingDisabled(s_CachedShape.gameObject))
                {
                    bestShape = s_CachedShape;
                    bestDistance = s_CachedDistance;
                    return true;
                }
            }

            bool hit;
            using (RaycastMarker.Auto())
                hit = TryRaycastSdfInternal(sceneView, ray, out bestShape, out bestDistance);
            if (current != null && (current.type == EventType.Layout || current.type == EventType.MouseDown))
            {
                s_HasCachedRaycast = true;
                s_CachedHit = hit;
                s_CachedSceneViewId = sceneView.GetInstanceID();
                s_CachedRaycastVersion = SDFSceneRegistry.Version;
                s_CachedRaycastTime = EditorApplication.timeSinceStartup;
                s_CachedRay = ray;
                s_CachedShape = bestShape;
                s_CachedDistance = bestDistance;
            }
            return hit;
        }

        private static bool TryRaycastSdfInternal(SceneView sceneView, Ray ray,
            out SDFShape bestShape, out float bestDistance)
        {
            float maximumDistance = sceneView.camera != null ? sceneView.camera.farClipPlane : 100000f;
            bestShape = null;
            bestDistance = maximumDistance;
            EnsureSpatialIndex();
            if (s_RootNode < 0 || s_NodeCount == 0)
                return false;

            EnsureCapacity(ref s_TraversalStack, s_NodeCount);
            int stackCount = 0;
            s_TraversalStack[stackCount++] = s_RootNode;
            while (stackCount > 0)
            {
                int nodeIndex = s_TraversalStack[--stackCount];
                PickerNode node = s_Nodes[nodeIndex];
                if (!IntersectAabb(ray, node.Bounds, bestDistance, out _))
                    continue;
                if (node.Entry < 0)
                {
                    PushChildrenNearestFirst(ray, node, bestDistance, ref stackCount);
                    continue;
                }

                SDFShape shape = s_Entries[node.Entry].Shape;
                if (shape == null ||
                    (shape.hideFlags & HideFlags.NotEditable) != 0 ||
                    SceneVisibilityManager.instance.IsHidden(shape.gameObject) ||
                    SceneVisibilityManager.instance.IsPickingDisabled(shape.gameObject))
                    continue;
                PickerEntry entry = s_Entries[node.Entry];
                if (SDFCpuEvaluator.Raycast(shape, ray, Modifiers, entry.ModifierStart,
                        entry.ModifierCount, entry.LocalBounds, out float distance, bestDistance) &&
                    distance < bestDistance)
                {
                    bestDistance = distance;
                    bestShape = shape;
                }
            }
            return bestShape != null;
        }

        private static void EnsureSpatialIndex()
        {
            uint version = SDFSceneRegistry.Version;
            if (s_RootNode < 0)
            {
                using (RebuildMarker.Auto())
                    RebuildSpatialIndex();
                s_SpatialVersion = version;
                return;
            }
            if (s_SpatialVersion == version)
                return;

            SDFDirtyFlags dirty = SDFSceneRegistry.GetDirtyFlagsSince(s_SpatialVersion);
            const SDFDirtyFlags topologyFlags = SDFDirtyFlags.Topology;
            const SDFDirtyFlags boundsFlags = SDFDirtyFlags.Shapes | SDFDirtyFlags.Modifiers |
                SDFDirtyFlags.Bounds | SDFDirtyFlags.Transforms;
            if ((dirty & topologyFlags) != 0)
            {
                using (RebuildMarker.Auto())
                    RebuildSpatialIndex();
            }
            else if ((dirty & boundsFlags) != 0)
            {
                using (RefitMarker.Auto())
                    RefitSpatialIndex((dirty & (SDFDirtyFlags.Shapes | SDFDirtyFlags.Modifiers | SDFDirtyFlags.Bounds)) != 0);
            }
            s_SpatialVersion = version;
        }

        private static void RebuildSpatialIndex()
        {
            SDFSceneRegistry.GetRegisteredShapes(Shapes);
            Modifiers.Clear();
            s_EntryCount = Shapes.Count;
            EnsureCapacity(ref s_Entries, s_EntryCount);
            EnsureCapacity(ref s_EntryOrder, s_EntryCount);
            EnsureCapacity(ref s_Nodes, Mathf.Max(1, s_EntryCount * 2 - 1));
            for (int i = 0; i < s_EntryCount; ++i)
            {
                SDFShape shape = Shapes[i];
                int modifierStart = Modifiers.Count;
                if (shape != null)
                {
                    ModifierScratch.Clear();
                    shape.GetComponents(ModifierScratch);
                    Modifiers.AddRange(ModifierScratch);
                }
                int modifierCount = Modifiers.Count - modifierStart;
                Bounds localBounds = shape != null
                    ? SDFCpuEvaluator.GetLocalBounds(shape, Modifiers, modifierStart, modifierCount) : default;
                s_Entries[i] = new PickerEntry
                {
                    Shape = shape,
                    ModifierStart = modifierStart,
                    ModifierCount = modifierCount,
                    LocalBounds = localBounds,
                    Bounds = shape != null ? SDFCpuEvaluator.GetWorldBounds(shape, localBounds) : default
                };
                s_EntryOrder[i] = i;
            }

            s_NodeCount = 0;
            s_RootNode = s_EntryCount > 0 ? BuildNode(0, s_EntryCount) : -1;
        }

        private static int BuildNode(int start, int count)
        {
            Bounds bounds = s_Entries[s_EntryOrder[start]].Bounds;
            Bounds centerBounds = new Bounds(bounds.center, Vector3.zero);
            for (int i = 1; i < count; ++i)
            {
                Bounds entryBounds = s_Entries[s_EntryOrder[start + i]].Bounds;
                bounds.Encapsulate(entryBounds);
                centerBounds.Encapsulate(entryBounds.center);
            }

            int nodeIndex = s_NodeCount++;
            if (count == 1)
            {
                s_Nodes[nodeIndex] = new PickerNode
                {
                    Bounds = bounds,
                    Left = -1,
                    Right = -1,
                    Entry = s_EntryOrder[start]
                };
                return nodeIndex;
            }

            Vector3 size = centerBounds.size;
            SortComparer.Axis = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
            System.Array.Sort(s_EntryOrder, start, count, SortComparer);
            int leftCount = count / 2;
            int left = BuildNode(start, leftCount);
            int right = BuildNode(start + leftCount, count - leftCount);
            s_Nodes[nodeIndex] = new PickerNode
            {
                Bounds = bounds,
                Left = left,
                Right = right,
                Entry = -1
            };
            return nodeIndex;
        }

        private static void RefitSpatialIndex(bool refreshLocalBounds)
        {
            for (int i = 0; i < s_EntryCount; ++i)
            {
                PickerEntry entry = s_Entries[i];
                if (entry.Shape != null)
                {
                    if (refreshLocalBounds)
                        entry.LocalBounds = SDFCpuEvaluator.GetLocalBounds(entry.Shape, Modifiers,
                            entry.ModifierStart, entry.ModifierCount);
                    entry.Bounds = SDFCpuEvaluator.GetWorldBounds(entry.Shape, entry.LocalBounds);
                }
                s_Entries[i] = entry;
            }
            if (s_RootNode >= 0)
                RefitNode(s_RootNode);
        }

        private static Bounds RefitNode(int nodeIndex)
        {
            PickerNode node = s_Nodes[nodeIndex];
            if (node.Entry >= 0)
            {
                node.Bounds = s_Entries[node.Entry].Bounds;
                s_Nodes[nodeIndex] = node;
                return node.Bounds;
            }

            Bounds bounds = RefitNode(node.Left);
            bounds.Encapsulate(RefitNode(node.Right));
            node.Bounds = bounds;
            s_Nodes[nodeIndex] = node;
            return bounds;
        }

        private static void PushChildrenNearestFirst(Ray ray, PickerNode node, float maximumDistance, ref int stackCount)
        {
            bool hitLeft = IntersectAabb(ray, s_Nodes[node.Left].Bounds, maximumDistance, out float leftDistance);
            bool hitRight = IntersectAabb(ray, s_Nodes[node.Right].Bounds, maximumDistance, out float rightDistance);
            if (hitLeft && hitRight)
            {
                if (leftDistance <= rightDistance)
                {
                    s_TraversalStack[stackCount++] = node.Right;
                    s_TraversalStack[stackCount++] = node.Left;
                }
                else
                {
                    s_TraversalStack[stackCount++] = node.Left;
                    s_TraversalStack[stackCount++] = node.Right;
                }
            }
            else if (hitLeft)
                s_TraversalStack[stackCount++] = node.Left;
            else if (hitRight)
                s_TraversalStack[stackCount++] = node.Right;
        }

        private static bool IntersectAabb(Ray ray, Bounds bounds, float maximumDistance, out float near)
        {
            near = 0f;
            float far = maximumDistance;
            Vector3 minimum = bounds.min;
            Vector3 maximum = bounds.max;
            for (int axis = 0; axis < 3; ++axis)
            {
                float origin = ray.origin[axis];
                float direction = ray.direction[axis];
                if (Mathf.Abs(direction) < 0.0000001f)
                {
                    if (origin < minimum[axis] || origin > maximum[axis])
                        return false;
                    continue;
                }
                float inverse = 1f / direction;
                float a = (minimum[axis] - origin) * inverse;
                float b = (maximum[axis] - origin) * inverse;
                if (a > b) { float swap = a; a = b; b = swap; }
                near = Mathf.Max(near, a);
                far = Mathf.Min(far, b);
                if (near > far)
                    return false;
            }
            return far >= 0f;
        }

        private static void EnsureCapacity<T>(ref T[] array, int required)
        {
            if (array.Length >= required)
                return;
            System.Array.Resize(ref array, Mathf.NextPowerOfTwo(Mathf.Max(1, required)));
        }

        private static bool TryGetConventionalDistance(GameObject gameObject, Ray ray, out float distance)
        {
            distance = float.PositiveInfinity;
            bool found = false;
            Collider collider = gameObject.GetComponent<Collider>();
            if (collider != null && collider.Raycast(ray, out RaycastHit hit, float.PositiveInfinity))
            {
                distance = hit.distance;
                found = true;
            }
            Renderer renderer = gameObject.GetComponent<Renderer>();
            if (renderer != null && renderer.bounds.IntersectRay(ray, out float rendererDistance))
            {
                distance = Mathf.Min(distance, rendererDistance);
                found = true;
            }
            return found;
        }

        private static void ApplySelection(GameObject target, bool additive, bool toggle)
        {
            if (!additive && !toggle)
            {
                Selection.activeGameObject = target;
                return;
            }

            SelectionScratch.Clear();
            SelectionScratch.AddRange(Selection.objects);
            int index = SelectionScratch.IndexOf(target);
            if (toggle && index >= 0)
                SelectionScratch.RemoveAt(index);
            else if (index < 0)
                SelectionScratch.Add(target);
            Selection.objects = SelectionScratch.ToArray();
        }
    }
}
