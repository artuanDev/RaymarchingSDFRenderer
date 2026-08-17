using System;
using UnityEngine;

namespace SdfRenderer
{
    public enum SDFShapeType
    {
        Sphere,
        Box,
        RoundBox,
        BoxFrame,
        Torus,
        CappedTorus,
        Link,
        InfiniteCylinder,
        Cone,
        InfiniteCone,
        Plane,
        HexagonalPrism,
        TriangularPrismBound,
        Capsule,
        VerticalCapsule,
        CappedCylinder,
        ArbitraryCappedCylinder,
        RoundedCylinder,
        CappedCone,
        ArbitraryCappedCone,
        SolidAngle,
        CutSphere,
        CutHollowSphere,
        DeathStar,
        RoundCone,
        RevolvedVesica,
        EllipsoidBound,
        Rhombus,
        Octahedron,
        OctahedronBound,
        Pyramid,
        TriangleUnsigned,
        QuadUnsigned
    }

    public enum SDFOperationType
    {
        Union,
        Subtraction,
        Intersection,
        SmoothUnion,
        SmoothSubtraction,
        SmoothIntersection
    }

    public enum SDFModifierType
    {
        Round,
        Onion,
        Elongate,
        Mirror,
        FiniteRepeat,
        InfiniteRepeat,
        Twist,
        Bend,
        Revolution,
        Extrusion
    }

    public enum SDFShadingModel
    {
        BlinnPhong,
        Unlit,
        Cel,
        [InspectorName("URP Lit")]
        PbrLike,
        Custom
    }

    public enum SDFDistanceKind
    {
        Exact,
        Bound,
        Unsigned
    }

    public enum SDFQualityPreset
    {
        Balanced,
        High,
        Ultra,
        Custom
    }

    [Flags]
    public enum SDFModifierAxes
    {
        None = 0,
        X = 1,
        Y = 2,
        Z = 4,
        All = X | Y | Z
    }
}
