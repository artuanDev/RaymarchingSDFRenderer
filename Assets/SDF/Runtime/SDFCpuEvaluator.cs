using System.Collections.Generic;
using UnityEngine;

namespace SdfRenderer
{
    /// <summary>CPU reference evaluation used by tests and editor Scene-view picking.</summary>
    public static class SDFCpuEvaluator
    {
        [System.ThreadStatic] private static List<SDFModifier> s_ModifierScratch;

        public static float EvaluateWorld(SDFShape shape, Vector3 positionWS)
        {
            if (shape == null) return float.PositiveInfinity;
            List<SDFModifier> modifiers = GetModifiers(shape);
            return EvaluateWorld(shape, positionWS, modifiers, 0, modifiers.Count);
        }

        private static float EvaluateWorld(SDFShape shape, Vector3 positionWS,
            IReadOnlyList<SDFModifier> modifiers, int modifierStart, int modifierCount)
        {
            Vector3 p = shape.transform.InverseTransformPoint(positionWS);
            float correction = 1f;
            float extrusionDistance = float.NegativeInfinity;

            int modifierEnd = Mathf.Min(modifiers.Count, modifierStart + modifierCount);
            for (int i = Mathf.Max(0, modifierStart); i < modifierEnd; ++i)
            {
                SDFModifier modifier = modifiers[i];
                if (modifier == null || !modifier.isActiveAndEnabled) continue;
                ApplyDomainModifier(ref p, ref correction, ref extrusionDistance, modifier);
            }

            shape.GetPackedParameters(out Vector4 p0, out Vector4 p1, out Vector4 p2, out Vector4 p3);
            float distance = SDFMath.EvaluatePrimitive(p, shape.ShapeType, p0, p1, p2, p3);
            if (!float.IsNegativeInfinity(extrusionDistance))
            {
                Vector2 extrusion = new Vector2(distance, extrusionDistance);
                distance = Mathf.Min(Mathf.Max(extrusion.x, extrusion.y), 0f) + Max(extrusion, Vector2.zero).magnitude;
            }

            float scale = shape.GetConservativeDistanceScale() * correction;
            distance *= scale;
            for (int i = Mathf.Max(0, modifierStart); i < modifierEnd; ++i)
            {
                SDFModifier modifier = modifiers[i];
                if (modifier == null || !modifier.isActiveAndEnabled) continue;
                float amount = Mathf.Abs(modifier.Amount) * scale;
                if (modifier.Type == SDFModifierType.Round) distance -= amount;
                else if (modifier.Type == SDFModifierType.Onion) distance = Mathf.Abs(distance) - amount;
            }
            return distance;
        }

        public static Bounds GetWorldBounds(SDFShape shape)
        {
            if (shape == null) return default;
            return GetWorldBounds(shape, GetLocalBounds(shape));
        }

        public static Bounds GetLocalBounds(SDFShape shape)
        {
            if (shape == null) return default;
            List<SDFModifier> modifiers = GetModifiers(shape);
            return GetLocalBounds(shape, modifiers, 0, modifiers.Count);
        }

        public static Bounds GetLocalBounds(SDFShape shape, IReadOnlyList<SDFModifier> modifiers,
            int modifierStart, int modifierCount)
        {
            if (shape == null) return default;
            Bounds local = shape.GetLocalBounds();
            int modifierEnd = Mathf.Min(modifiers.Count, modifierStart + modifierCount);
            for (int i = Mathf.Max(0, modifierStart); i < modifierEnd; ++i)
            {
                SDFModifier modifier = modifiers[i];
                if (modifier == null || !modifier.isActiveAndEnabled) continue;
                if (modifier.Type == SDFModifierType.InfiniteRepeat)
                {
                    local = shape.ClipBounds;
                    break;
                }
                ExpandBounds(ref local, modifier);
            }
            return local;
        }

        public static Bounds GetWorldBounds(SDFShape shape, Bounds localBounds)
        {
            if (shape == null) return default;
            Bounds world = TransformBounds(shape.transform.localToWorldMatrix, localBounds);
            world.Expand(0.004f);
            return world;
        }

        public static bool Raycast(SDFShape shape, Ray ray, out float hitDistance, float maximumDistance = 100000f)
        {
            if (shape == null)
            {
                hitDistance = 0f;
                return false;
            }
            List<SDFModifier> modifiers = GetModifiers(shape);
            Bounds localBounds = GetLocalBounds(shape, modifiers, 0, modifiers.Count);
            return Raycast(shape, ray, modifiers, 0, modifiers.Count, localBounds, out hitDistance, maximumDistance);
        }

        public static bool Raycast(SDFShape shape, Ray ray, IReadOnlyList<SDFModifier> modifiers,
            int modifierStart, int modifierCount, Bounds localBounds, out float hitDistance,
            float maximumDistance = 100000f)
        {
            hitDistance = 0f;
            if (shape == null || !shape.isActiveAndEnabled || !shape.gameObject.activeInHierarchy)
                return false;
            Vector3 direction = ray.direction.normalized;
            if (direction.sqrMagnitude < 0.5f || !IntersectAabb(ray.origin, direction, GetWorldBounds(shape, localBounds), out float near, out float far))
                return false;
            float current = Mathf.Max(near, 0f);
            far = Mathf.Min(far, maximumDistance);
            if (far < current) return false;

            for (int step = 0; step < 384 && current <= far; ++step)
            {
                float distance = EvaluateWorld(shape, ray.origin + direction * current,
                    modifiers, modifierStart, modifierCount);
                if (float.IsNaN(distance) || float.IsInfinity(distance)) return false;
                float epsilon = Mathf.Max(0.0001f, current * 0.00001f);
                if (Mathf.Abs(distance) <= epsilon)
                {
                    hitDistance = current;
                    return true;
                }
                current += Mathf.Max(Mathf.Abs(distance) * 0.65f, epsilon * 0.5f);
            }
            return false;
        }

        private static void ApplyDomainModifier(ref Vector3 p, ref float correction, ref float extrusionDistance, SDFModifier modifier)
        {
            SDFModifierAxes axes = modifier.Axes;
            Vector3 vector = modifier.Vector;
            float amount = modifier.Amount;
            switch (modifier.Type)
            {
                case SDFModifierType.Elongate:
                    Vector3 h = Vector3.Scale(Abs(vector), AxesMask(axes));
                    p -= new Vector3(Mathf.Clamp(p.x, -h.x, h.x), Mathf.Clamp(p.y, -h.y, h.y), Mathf.Clamp(p.z, -h.z, h.z));
                    break;
                case SDFModifierType.Mirror:
                    if (HasAxis(axes, SDFModifierAxes.X)) p.x = Mathf.Abs(p.x) - vector.x;
                    if (HasAxis(axes, SDFModifierAxes.Y)) p.y = Mathf.Abs(p.y) - vector.y;
                    if (HasAxis(axes, SDFModifierAxes.Z)) p.z = Mathf.Abs(p.z) - vector.z;
                    break;
                case SDFModifierType.FiniteRepeat:
                case SDFModifierType.InfiniteRepeat:
                    Vector3 spacing = Max(Abs(vector), Vector3.one * 0.00001f);
                    Vector3 cell = new Vector3(Mathf.Round(p.x / spacing.x), Mathf.Round(p.y / spacing.y), Mathf.Round(p.z / spacing.z));
                    if (modifier.Type == SDFModifierType.FiniteRepeat)
                    {
                        Vector3Int count = modifier.Count;
                        cell = new Vector3(Mathf.Clamp(cell.x, -count.x, count.x), Mathf.Clamp(cell.y, -count.y, count.y), Mathf.Clamp(cell.z, -count.z, count.z));
                    }
                    if (!HasAxis(axes, SDFModifierAxes.X)) cell.x = 0f;
                    if (!HasAxis(axes, SDFModifierAxes.Y)) cell.y = 0f;
                    if (!HasAxis(axes, SDFModifierAxes.Z)) cell.z = 0f;
                    p -= Vector3.Scale(cell, spacing);
                    break;
                case SDFModifierType.Twist:
                    float twist = amount * p.y;
                    float twistSin = Mathf.Sin(twist), twistCos = Mathf.Cos(twist);
                    p = new Vector3(twistCos * p.x - twistSin * p.z, p.y, twistSin * p.x + twistCos * p.z);
                    correction /= 1f + Mathf.Abs(amount) * new Vector2(p.x, p.z).magnitude;
                    break;
                case SDFModifierType.Bend:
                    float bend = amount * p.x;
                    float bendSin = Mathf.Sin(bend), bendCos = Mathf.Cos(bend);
                    p = new Vector3(bendCos * p.x - bendSin * p.y, bendSin * p.x + bendCos * p.y, p.z);
                    correction /= 1f + Mathf.Abs(amount) * new Vector2(p.x, p.y).magnitude;
                    break;
                case SDFModifierType.Revolution:
                    p = new Vector3(new Vector2(p.x, p.z).magnitude - amount, p.y, 0f);
                    break;
                case SDFModifierType.Extrusion:
                    float extent = Mathf.Abs(amount);
                    if (HasAxis(axes, SDFModifierAxes.X)) { extrusionDistance = Mathf.Max(extrusionDistance, Mathf.Abs(p.x) - extent); p.x = 0f; }
                    if (HasAxis(axes, SDFModifierAxes.Y)) { extrusionDistance = Mathf.Max(extrusionDistance, Mathf.Abs(p.y) - extent); p.y = 0f; }
                    if (HasAxis(axes, SDFModifierAxes.Z)) { extrusionDistance = Mathf.Max(extrusionDistance, Mathf.Abs(p.z) - extent); p.z = 0f; }
                    break;
            }
        }

        private static void ExpandBounds(ref Bounds bounds, SDFModifier modifier)
        {
            switch (modifier.Type)
            {
                case SDFModifierType.Round:
                case SDFModifierType.Onion:
                    bounds.Expand(Mathf.Abs(modifier.Amount) * 2f);
                    break;
                case SDFModifierType.Elongate:
                    bounds.Expand(Vector3.Scale(Abs(modifier.Vector), AxesMask(modifier.Axes)) * 2f);
                    break;
                case SDFModifierType.Mirror:
                    Vector3 offset = Vector3.Scale(Abs(modifier.Vector), AxesMask(modifier.Axes));
                    Vector3 center = bounds.center;
                    Vector3 extents = bounds.extents;
                    if (HasAxis(modifier.Axes, SDFModifierAxes.X)) { extents.x += Mathf.Abs(center.x) + offset.x; center.x = 0f; }
                    if (HasAxis(modifier.Axes, SDFModifierAxes.Y)) { extents.y += Mathf.Abs(center.y) + offset.y; center.y = 0f; }
                    if (HasAxis(modifier.Axes, SDFModifierAxes.Z)) { extents.z += Mathf.Abs(center.z) + offset.z; center.z = 0f; }
                    bounds.center = center;
                    bounds.extents = extents;
                    break;
                case SDFModifierType.FiniteRepeat:
                    Vector3Int count = modifier.Count;
                    Vector3 repetition = Vector3.Scale(Abs(modifier.Vector), new Vector3(count.x, count.y, count.z));
                    bounds.Expand(Vector3.Scale(repetition, AxesMask(modifier.Axes)) * 2f);
                    break;
                case SDFModifierType.Twist:
                case SDFModifierType.Bend:
                    float radius = bounds.center.magnitude + bounds.extents.magnitude;
                    bounds.center = Vector3.zero;
                    bounds.extents = Vector3.one * radius;
                    break;
                case SDFModifierType.Revolution:
                    float revolutionRadius = Mathf.Abs(modifier.Amount) + Mathf.Abs(bounds.center.x) + bounds.extents.x;
                    bounds.center = new Vector3(0f, bounds.center.y, 0f);
                    bounds.extents = new Vector3(revolutionRadius, bounds.extents.y, revolutionRadius);
                    break;
                case SDFModifierType.Extrusion:
                    bounds.Expand(Vector3.Scale(Vector3.one * Mathf.Abs(modifier.Amount), AxesMask(modifier.Axes)) * 2f);
                    break;
            }
        }

        private static bool IntersectAabb(Vector3 origin, Vector3 direction, Bounds bounds, out float near, out float far)
        {
            near = float.NegativeInfinity;
            far = float.PositiveInfinity;
            Vector3 minimum = bounds.min, maximum = bounds.max;
            for (int axis = 0; axis < 3; ++axis)
            {
                float o = origin[axis], d = direction[axis];
                if (Mathf.Abs(d) < 0.0000001f)
                {
                    if (o < minimum[axis] || o > maximum[axis]) return false;
                    continue;
                }
                float a = (minimum[axis] - o) / d;
                float b = (maximum[axis] - o) / d;
                if (a > b) { float swap = a; a = b; b = swap; }
                near = Mathf.Max(near, a);
                far = Mathf.Min(far, b);
                if (near > far) return false;
            }
            return far >= 0f;
        }

        private static bool HasAxis(SDFModifierAxes value, SDFModifierAxes axis) => (value & axis) != 0;
        private static List<SDFModifier> GetModifiers(SDFShape shape)
        {
            List<SDFModifier> modifiers = s_ModifierScratch ??= new List<SDFModifier>(8);
            modifiers.Clear();
            shape.GetComponents(modifiers);
            return modifiers;
        }
        private static Vector3 AxesMask(SDFModifierAxes axes) => new Vector3(HasAxis(axes, SDFModifierAxes.X) ? 1f : 0f, HasAxis(axes, SDFModifierAxes.Y) ? 1f : 0f, HasAxis(axes, SDFModifierAxes.Z) ? 1f : 0f);
        private static Vector3 Abs(Vector3 value) => new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        private static Vector3 Max(Vector3 a, Vector3 b) => new Vector3(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y), Mathf.Max(a.z, b.z));
        private static Vector2 Max(Vector2 a, Vector2 b) => new Vector2(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));

        private static Bounds TransformBounds(Matrix4x4 matrix, Bounds local)
        {
            Vector3 center = matrix.MultiplyPoint3x4(local.center);
            Vector3 extents = local.extents;
            Vector3 x = matrix.MultiplyVector(new Vector3(extents.x, 0f, 0f));
            Vector3 y = matrix.MultiplyVector(new Vector3(0f, extents.y, 0f));
            Vector3 z = matrix.MultiplyVector(new Vector3(0f, 0f, extents.z));
            return new Bounds(center, (Abs(x) + Abs(y) + Abs(z)) * 2f);
        }
    }
}
