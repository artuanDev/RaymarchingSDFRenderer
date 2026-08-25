using UnityEngine;

namespace SdfRenderer
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class SDFShape : MonoBehaviour
    {
        [SerializeField] private SDFShapeType m_ShapeType = SDFShapeType.Sphere;
        [SerializeField, Min(0.0001f)] private float m_Radius = 0.5f;
        [SerializeField] private Vector3 m_Size = Vector3.one * 0.5f;
        [SerializeField, Min(0f)] private float m_Roundness = 0.1f;
        [SerializeField, Min(0.0001f)] private float m_Height = 1f;
        [SerializeField, Min(0.0001f)] private float m_RadiusA = 0.75f;
        [SerializeField, Min(0.0001f)] private float m_RadiusB = 0.2f;
        [SerializeField, Min(0.0001f)] private float m_Thickness = 0.1f;
        [SerializeField, Range(0.1f, 179f)] private float m_Angle = 45f;
        [SerializeField] private Vector3 m_PointA = new Vector3(0f, -0.5f, 0f);
        [SerializeField] private Vector3 m_PointB = new Vector3(0f, 0.5f, 0f);
        [SerializeField] private Vector3 m_PointC = new Vector3(0.5f, -0.5f, 0f);
        [SerializeField] private Vector3 m_PointD = new Vector3(-0.5f, -0.5f, 0f);
        [SerializeField] private Vector3 m_Normal = Vector3.up;
        [SerializeField] private float m_Offset;
        [SerializeField] private Bounds m_ClipBounds = new Bounds(Vector3.zero, Vector3.one * 20f);
        [SerializeField] private SDFMaterialAsset m_Material;
        [System.NonSerialized] private uint m_DataVersion;

        internal uint DataVersion => m_DataVersion;
        public SDFShapeType ShapeType { get => m_ShapeType; set { if (m_ShapeType == value) return; m_ShapeType = value; MarkDirty(); } }
        public float Radius { get => m_Radius; set { value = Mathf.Max(0.0001f, value); if (Mathf.Approximately(m_Radius, value)) return; m_Radius = value; MarkDirty(); } }
        public Vector3 Size { get => m_Size; set { value = Positive(value); if (m_Size == value) return; m_Size = value; MarkDirty(); } }
        public float Roundness { get => m_Roundness; set { value = Mathf.Max(0f, value); if (Mathf.Approximately(m_Roundness, value)) return; m_Roundness = value; MarkDirty(); } }
        public float Height { get => m_Height; set { value = Mathf.Max(0.0001f, value); if (Mathf.Approximately(m_Height, value)) return; m_Height = value; MarkDirty(); } }
        public float RadiusA => m_RadiusA;
        public float RadiusB => m_RadiusB;
        public float Thickness => m_Thickness;
        public float Angle => m_Angle;
        public Vector3 PointA => m_PointA;
        public Vector3 PointB => m_PointB;
        public Vector3 PointC => m_PointC;
        public Vector3 PointD => m_PointD;
        public Vector3 Normal => m_Normal.sqrMagnitude > 0f ? m_Normal.normalized : Vector3.up;
        public float Offset => m_Offset;
        public Bounds ClipBounds { get => m_ClipBounds; set { if (m_ClipBounds == value) return; m_ClipBounds = value; ClampParameters(); MarkDirty(); } }
        public SDFMaterialAsset Material { get => m_Material; set { if (m_Material == value) return; m_Material = value; MarkDirty(); } }

        public bool IsUnbounded => m_ShapeType == SDFShapeType.Plane || m_ShapeType == SDFShapeType.InfiniteCylinder ||
            m_ShapeType == SDFShapeType.InfiniteCone;

        public SDFDistanceKind DistanceKind
        {
            get
            {
                if (m_ShapeType == SDFShapeType.TriangularPrismBound || m_ShapeType == SDFShapeType.EllipsoidBound ||
                    m_ShapeType == SDFShapeType.OctahedronBound)
                    return SDFDistanceKind.Bound;
                if (m_ShapeType == SDFShapeType.TriangleUnsigned || m_ShapeType == SDFShapeType.QuadUnsigned)
                    return SDFDistanceKind.Unsigned;
                return SDFDistanceKind.Exact;
            }
        }

        internal void GetPackedParameters(out Vector4 p0, out Vector4 p1, out Vector4 p2, out Vector4 p3)
        {
            p0 = new Vector4(m_Radius, m_Height, m_RadiusA, m_RadiusB);
            p1 = new Vector4(m_Size.x, m_Size.y, m_Size.z, m_Roundness);
            p2 = new Vector4(m_PointA.x, m_PointA.y, m_PointA.z, m_Thickness);
            p3 = new Vector4(m_PointB.x, m_PointB.y, m_PointB.z, m_Angle * Mathf.Deg2Rad);

            switch (m_ShapeType)
            {
                case SDFShapeType.Plane:
                    Vector3 n = Normal;
                    p0 = new Vector4(n.x, n.y, n.z, m_Offset);
                    break;
                case SDFShapeType.CutSphere:
                case SDFShapeType.CutHollowSphere:
                    p0.y = Mathf.Clamp(m_Offset, -m_Radius, m_Radius);
                    break;
                case SDFShapeType.TriangleUnsigned:
                    p0 = new Vector4(m_PointA.x, m_PointA.y, m_PointA.z, m_Thickness);
                    p1 = new Vector4(m_PointB.x, m_PointB.y, m_PointB.z, 0f);
                    p2 = new Vector4(m_PointC.x, m_PointC.y, m_PointC.z, 0f);
                    break;
                case SDFShapeType.QuadUnsigned:
                    p0 = new Vector4(m_PointA.x, m_PointA.y, m_PointA.z, m_Thickness);
                    p1 = new Vector4(m_PointB.x, m_PointB.y, m_PointB.z, 0f);
                    p2 = new Vector4(m_PointC.x, m_PointC.y, m_PointC.z, 0f);
                    p3 = new Vector4(m_PointD.x, m_PointD.y, m_PointD.z, 0f);
                    break;
                case SDFShapeType.Capsule:
                case SDFShapeType.ArbitraryCappedCylinder:
                case SDFShapeType.ArbitraryCappedCone:
                case SDFShapeType.RoundCone:
                case SDFShapeType.RevolvedVesica:
                    p0 = new Vector4(m_PointA.x, m_PointA.y, m_PointA.z, m_RadiusA);
                    p1 = new Vector4(m_PointB.x, m_PointB.y, m_PointB.z, m_RadiusB);
                    p2 = new Vector4(m_Radius, m_Thickness, 0f, 0f);
                    break;
            }
        }

        public Bounds GetLocalBounds()
        {
            if (IsUnbounded)
                return SanitizedClipBounds();

            Vector3 extents;
            switch (m_ShapeType)
            {
                case SDFShapeType.Sphere:
                    extents = Vector3.one * m_Radius;
                    break;
                case SDFShapeType.Box:
                    extents = m_Size;
                    break;
                case SDFShapeType.RoundBox:
                    extents = m_Size + Vector3.one * m_Roundness;
                    break;
                case SDFShapeType.BoxFrame:
                    extents = m_Size + Vector3.one * m_Thickness;
                    break;
                case SDFShapeType.Torus:
                    extents = new Vector3(m_RadiusA + m_RadiusB, m_RadiusB, m_RadiusA + m_RadiusB);
                    break;
                case SDFShapeType.CappedTorus:
                    extents = new Vector3(m_RadiusA + m_RadiusB, m_RadiusA + m_RadiusB, m_RadiusB);
                    break;
                case SDFShapeType.Link:
                    extents = new Vector3(m_RadiusA + m_RadiusB, m_Height + m_RadiusA + m_RadiusB, m_RadiusB);
                    break;
                case SDFShapeType.Cone:
                case SDFShapeType.CappedCone:
                    extents = new Vector3(Mathf.Max(m_RadiusA, m_RadiusB), m_Height, Mathf.Max(m_RadiusA, m_RadiusB));
                    break;
                case SDFShapeType.HexagonalPrism:
                    extents = new Vector3(m_RadiusA, m_RadiusA, m_Height);
                    break;
                case SDFShapeType.TriangularPrismBound:
                    extents = new Vector3(m_Size.x, m_Size.y, m_Size.z);
                    break;
                case SDFShapeType.VerticalCapsule:
                    return new Bounds(new Vector3(0f, m_Height * 0.5f, 0f), new Vector3(m_Radius * 2f, m_Height + m_Radius * 2f, m_Radius * 2f));
                case SDFShapeType.Capsule:
                    return BoundsAroundSegment(m_PointA, m_PointB, Mathf.Max(m_Radius, m_RadiusA));
                case SDFShapeType.CappedCylinder:
                case SDFShapeType.RoundedCylinder:
                    extents = new Vector3(Mathf.Max(m_Radius, m_RadiusA) + m_RadiusB, m_Height + m_RadiusB, Mathf.Max(m_Radius, m_RadiusA) + m_RadiusB);
                    break;
                case SDFShapeType.ArbitraryCappedCylinder:
                    return BoundsAroundSegment(m_PointA, m_PointB, Mathf.Max(m_Radius, m_RadiusA));
                case SDFShapeType.ArbitraryCappedCone:
                case SDFShapeType.RoundCone:
                    return BoundsAroundSegment(m_PointA, m_PointB, Mathf.Max(m_RadiusA, m_RadiusB));
                case SDFShapeType.SolidAngle:
                case SDFShapeType.CutSphere:
                case SDFShapeType.CutHollowSphere:
                    extents = Vector3.one * (m_Radius + m_Thickness);
                    break;
                case SDFShapeType.DeathStar:
                    extents = Vector3.one * Mathf.Max(m_RadiusA, m_RadiusB + Mathf.Abs(m_Height));
                    break;
                case SDFShapeType.RevolvedVesica:
                    return BoundsAroundSegment(m_PointA, m_PointB, Mathf.Max(m_Radius, m_Thickness));
                case SDFShapeType.EllipsoidBound:
                    extents = m_Size;
                    break;
                case SDFShapeType.Rhombus:
                    extents = new Vector3(m_RadiusA + m_Roundness, m_Height + m_Roundness, m_RadiusB + m_Roundness);
                    break;
                case SDFShapeType.Octahedron:
                case SDFShapeType.OctahedronBound:
                    extents = Vector3.one * m_Radius;
                    break;
                case SDFShapeType.Pyramid:
                    extents = new Vector3(0.5f, m_Height * 0.5f, 0.5f);
                    return new Bounds(new Vector3(0f, m_Height * 0.5f, 0f), extents * 2f);
                case SDFShapeType.TriangleUnsigned:
                    return BoundsAroundPoints(m_PointA, m_PointB, m_PointC, m_PointC, m_Thickness);
                case SDFShapeType.QuadUnsigned:
                    return BoundsAroundPoints(m_PointA, m_PointB, m_PointC, m_PointD, m_Thickness);
                default:
                    extents = Vector3.one;
                    break;
            }
            return new Bounds(Vector3.zero, Vector3.Max(extents, Vector3.one * 0.0001f) * 2f);
        }

        // Distance from the centre of GetLocalBounds() to the furthest point of the shape.
        // A world AABB may be taken as the smaller, per axis, of the rotated local box and
        // this radius scaled by the transform's largest singular value: both are valid
        // bounds, so the minimum is one too. Unlike the box term, the radius does not grow
        // when the shape rotates, and the box term inflates by up to sqrt(3) per axis - five
        // times the volume - under rotation. Every extra cubic unit of a shape's bounds
        // becomes fragments that rasterize, march and then miss, so the tighter of the two
        // is worth taking.
        //
        // Shapes whose surface stays far inside their own AABB corners gain the most: a
        // sphere is bounded at 0.577 of its box diagonal and does not move under rotation at
        // all. A type with no tighter analytic bound returns the box diagonal, which is
        // always valid and simply leaves the box term winning the minimum.
        public float GetLocalBoundingRadius()
        {
            Bounds local = GetLocalBounds();
            Vector3 e = local.extents;
            float diagonal = e.magnitude;
            if (IsUnbounded)
                return diagonal;

            float radius;
            switch (m_ShapeType)
            {
                // Contained in a ball about the bounds centre, so the largest half extent
                // is already the exact radius.
                case SDFShapeType.Sphere:
                case SDFShapeType.Octahedron:
                case SDFShapeType.OctahedronBound:
                case SDFShapeType.EllipsoidBound:
                case SDFShapeType.SolidAngle:
                case SDFShapeType.CutSphere:
                case SDFShapeType.CutHollowSphere:
                case SDFShapeType.DeathStar:
                case SDFShapeType.Torus:
                case SDFShapeType.CappedTorus:
                case SDFShapeType.VerticalCapsule:
                    radius = Mathf.Max(e.x, Mathf.Max(e.y, e.z));
                    break;
                // Surfaces of revolution about local Y: contained in a cylinder of radius
                // max(e.x, e.z) and half height e.y, whose diagonal drops one of the three
                // squared terms the box diagonal carries.
                case SDFShapeType.Cone:
                case SDFShapeType.CappedCone:
                case SDFShapeType.CappedCylinder:
                case SDFShapeType.RoundedCylinder:
                case SDFShapeType.Rhombus:
                    radius = CylinderRadius(Mathf.Max(e.x, e.z), e.y);
                    break;
                // The hexagonal prism is extruded along local Z instead.
                case SDFShapeType.HexagonalPrism:
                    radius = CylinderRadius(Mathf.Max(e.x, e.y), e.z);
                    break;
                // Segment shapes bound a capsule whose axis runs A to B. GetLocalBounds
                // centres their box on the segment midpoint, so half the segment plus the
                // sweep radius is the exact distance to the furthest point.
                case SDFShapeType.Capsule:
                case SDFShapeType.ArbitraryCappedCylinder:
                    radius = SegmentRadius(m_PointA, m_PointB, Mathf.Max(m_Radius, m_RadiusA));
                    break;
                case SDFShapeType.ArbitraryCappedCone:
                case SDFShapeType.RoundCone:
                    radius = SegmentRadius(m_PointA, m_PointB, Mathf.Max(m_RadiusA, m_RadiusB));
                    break;
                case SDFShapeType.RevolvedVesica:
                    radius = SegmentRadius(m_PointA, m_PointB, Mathf.Max(m_Radius, m_Thickness));
                    break;
                case SDFShapeType.TriangleUnsigned:
                    radius = PointsRadius(local.center, m_PointA, m_PointB, m_PointC, m_PointC, m_Thickness);
                    break;
                case SDFShapeType.QuadUnsigned:
                    radius = PointsRadius(local.center, m_PointA, m_PointB, m_PointC, m_PointD, m_Thickness);
                    break;
                default:
                    radius = diagonal;
                    break;
            }
            return Mathf.Min(radius, diagonal);
        }

        private static float CylinderRadius(float radial, float halfHeight) =>
            Mathf.Sqrt(radial * radial + halfHeight * halfHeight);

        private static float SegmentRadius(Vector3 a, Vector3 b, float radius) =>
            (b - a).magnitude * 0.5f + Mathf.Max(0.0001f, radius);

        private static float PointsRadius(Vector3 center, Vector3 a, Vector3 b, Vector3 c, Vector3 d, float thickness)
        {
            float squared = Mathf.Max(
                Mathf.Max((a - center).sqrMagnitude, (b - center).sqrMagnitude),
                Mathf.Max((c - center).sqrMagnitude, (d - center).sqrMagnitude));
            return Mathf.Sqrt(squared) + Mathf.Max(0.0001f, thickness);
        }

        internal float GetConservativeDistanceScale()
        {
            Vector3 scale = transform.lossyScale;
            return Mathf.Max(0.000001f, Mathf.Min(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
        }

        internal float GetMaximumScale()
        {
            Vector3 scale = transform.lossyScale;
            return Mathf.Max(0.000001f, Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
        }

        internal void GetScaleRange(out float minimum, out float maximum)
        {
            Vector3 scale = transform.lossyScale;
            float x = Mathf.Abs(scale.x);
            float y = Mathf.Abs(scale.y);
            float z = Mathf.Abs(scale.z);
            minimum = Mathf.Max(0.000001f, Mathf.Min(x, Mathf.Min(y, z)));
            maximum = Mathf.Max(0.000001f, Mathf.Max(x, Mathf.Max(y, z)));
        }

        public float EvaluateLocal(Vector3 position)
        {
            GetPackedParameters(out Vector4 p0, out Vector4 p1, out Vector4 p2, out Vector4 p3);
            return SDFMath.EvaluatePrimitive(position, m_ShapeType, p0, p1, p2, p3);
        }

        public void MarkDirty()
        {
            unchecked { ++m_DataVersion; }
            SDFSceneRegistry.MarkDirty(SDFDirtyFlags.Shapes | SDFDirtyFlags.Bounds);
        }
        private void OnEnable() { ClampParameters(); SDFSceneRegistry.Register(this); MarkDirty(); }
        private void OnDisable() { SDFSceneRegistry.Unregister(this); MarkDirty(); }
        private void OnDestroy() { SDFSceneRegistry.Unregister(this); MarkDirty(); }
        private void OnDidApplyAnimationProperties() { ClampParameters(); MarkDirty(); }
        private void OnTransformParentChanged() => SDFSceneRegistry.MarkDirty(
            SDFDirtyFlags.Topology | SDFDirtyFlags.Shapes | SDFDirtyFlags.Bounds);

        private void OnValidate()
        {
            ClampParameters();
            MarkDirty();
        }

        private void ClampParameters()
        {
            m_Radius = Mathf.Max(0.0001f, m_Radius);
            m_Size = Positive(m_Size);
            m_Roundness = Mathf.Max(0f, m_Roundness);
            m_Height = Mathf.Max(0.0001f, m_Height);
            m_RadiusA = Mathf.Max(0.0001f, m_RadiusA);
            m_RadiusB = Mathf.Max(0.0001f, m_RadiusB);
            m_Thickness = Mathf.Max(0.0001f, m_Thickness);
            m_Angle = Mathf.Clamp(m_Angle, 0.1f, 179f);
            if (m_Normal.sqrMagnitude < 0.000001f)
                m_Normal = Vector3.up;
            m_ClipBounds = SanitizedClipBounds();
        }

        private Bounds SanitizedClipBounds()
        {
            Bounds b = m_ClipBounds;
            b.size = Positive(b.size);
            return b;
        }

        private static Vector3 Positive(Vector3 value) => new Vector3(Mathf.Max(0.0001f, Mathf.Abs(value.x)), Mathf.Max(0.0001f, Mathf.Abs(value.y)), Mathf.Max(0.0001f, Mathf.Abs(value.z)));

        private static Bounds BoundsAroundSegment(Vector3 a, Vector3 b, float radius)
        {
            Bounds bounds = new Bounds(a, Vector3.zero);
            bounds.Encapsulate(b);
            bounds.Expand(Mathf.Max(0.0001f, radius) * 2f);
            return bounds;
        }

        private static Bounds BoundsAroundPoints(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float thickness)
        {
            Bounds bounds = new Bounds(a, Vector3.zero);
            bounds.Encapsulate(b);
            bounds.Encapsulate(c);
            bounds.Encapsulate(d);
            bounds.Expand(Mathf.Max(0.0001f, thickness) * 2f);
            return bounds;
        }
    }
}
