using UnityEngine;

namespace SdfRenderer
{
    public static class SDFMath
    {
        public static float EvaluatePrimitive(Vector3 p, SDFShapeType type, Vector4 a, Vector4 b, Vector4 c, Vector4 d)
        {
            switch (type)
            {
                case SDFShapeType.Sphere: return p.magnitude - a.x;
                case SDFShapeType.Box: return Box(p, new Vector3(b.x, b.y, b.z));
                case SDFShapeType.RoundBox: return Box(p, new Vector3(b.x, b.y, b.z)) - b.w;
                case SDFShapeType.BoxFrame: return BoxFrame(p, new Vector3(b.x, b.y, b.z), c.w);
                case SDFShapeType.Torus:
                    return new Vector2(new Vector2(p.x, p.z).magnitude - a.z, p.y).magnitude - a.w;
                case SDFShapeType.CappedTorus:
                    p.x = Mathf.Abs(p.x);
                    Vector2 cap = new Vector2(Mathf.Sin(d.w), Mathf.Cos(d.w));
                    float cappedK = cap.y * p.x > cap.x * p.y ? Vector2.Dot(new Vector2(p.x, p.y), cap) : new Vector2(p.x, p.y).magnitude;
                    return Mathf.Sqrt(Mathf.Max(Vector3.Dot(p, p) + a.z * a.z - 2f * a.z * cappedK, 0f)) - a.w;
                case SDFShapeType.Link:
                    Vector3 link = new Vector3(p.x, Mathf.Max(Mathf.Abs(p.y) - a.y, 0f), p.z);
                    return new Vector2(new Vector2(link.x, link.y).magnitude - a.z, link.z).magnitude - a.w;
                case SDFShapeType.InfiniteCylinder:
                    return new Vector2(p.x, p.z).magnitude - a.x;
                case SDFShapeType.Cone:
                    return CappedCone(p, a.y, a.z, 0f);
                case SDFShapeType.InfiniteCone:
                    Vector2 cone = new Vector2(Mathf.Sin(d.w), Mathf.Cos(d.w));
                    Vector2 coneQ = new Vector2(new Vector2(p.x, p.z).magnitude, -p.y);
                    float coneDistance = (coneQ - cone * Mathf.Max(Vector2.Dot(coneQ, cone), 0f)).magnitude;
                    return coneDistance * (coneQ.x * cone.y - coneQ.y * cone.x < 0f ? -1f : 1f);
                case SDFShapeType.Plane:
                    return Vector3.Dot(p, new Vector3(a.x, a.y, a.z)) + a.w;
                case SDFShapeType.HexagonalPrism:
                    return HexagonalPrism(p, a.z, a.y);
                case SDFShapeType.VerticalCapsule:
                    p.y -= Mathf.Clamp(p.y, 0f, a.y);
                    return p.magnitude - a.x;
                case SDFShapeType.Capsule:
                    return Capsule(p, new Vector3(a.x, a.y, a.z), new Vector3(b.x, b.y, b.z), c.x);
                case SDFShapeType.CappedCylinder:
                    return CappedCylinder(p, a.y, a.x);
                case SDFShapeType.ArbitraryCappedCylinder:
                    return ArbitraryCappedCylinder(p, new Vector3(a.x, a.y, a.z), new Vector3(b.x, b.y, b.z), c.x);
                case SDFShapeType.RoundedCylinder:
                    Vector2 rounded = new Vector2(new Vector2(p.x, p.z).magnitude - a.z + a.w, Mathf.Abs(p.y) - a.y);
                    return Mathf.Min(Mathf.Max(rounded.x, rounded.y), 0f) + Max(rounded, Vector2.zero).magnitude - a.w;
                case SDFShapeType.CappedCone:
                    return CappedCone(p, a.y, a.z, a.w);
                case SDFShapeType.ArbitraryCappedCone:
                    return ArbitraryCappedCone(p, new Vector3(a.x, a.y, a.z), new Vector3(b.x, b.y, b.z), a.w, b.w);
                case SDFShapeType.SolidAngle:
                    return SolidAngle(p, a.x, d.w);
                case SDFShapeType.CutSphere:
                    return CutSphere(p, a.x, a.y);
                case SDFShapeType.CutHollowSphere:
                    return CutHollowSphere(p, a.x, a.y, c.w);
                case SDFShapeType.DeathStar:
                    return DeathStar(p, a.z, a.w, a.y);
                case SDFShapeType.RoundCone:
                    return RoundCone(p, new Vector3(a.x, a.y, a.z), new Vector3(b.x, b.y, b.z), a.w, b.w);
                case SDFShapeType.RevolvedVesica:
                    return RevolvedVesica(p, new Vector3(a.x, a.y, a.z), new Vector3(b.x, b.y, b.z), c.x);
                case SDFShapeType.EllipsoidBound:
                    return EllipsoidBound(p, new Vector3(b.x, b.y, b.z));
                case SDFShapeType.Rhombus:
                    return Rhombus(p, a.z, a.w, a.y, b.w);
                case SDFShapeType.Octahedron:
                    return Octahedron(p, a.x);
                case SDFShapeType.OctahedronBound:
                    p = Abs(p);
                    return (p.x + p.y + p.z - a.x) * 0.57735027f;
                case SDFShapeType.TriangularPrismBound:
                    p = Abs(p);
                    return Mathf.Max(p.z - b.z, Mathf.Max(p.x * 0.866025f + p.y * 0.5f, -p.y) - b.x * 0.5f);
                case SDFShapeType.Pyramid:
                    return Pyramid(p, a.y);
                case SDFShapeType.TriangleUnsigned:
                    return TriangleDistance(p, new Vector3(a.x, a.y, a.z), new Vector3(b.x, b.y, b.z), new Vector3(c.x, c.y, c.z)) - a.w;
                case SDFShapeType.QuadUnsigned:
                    return Mathf.Min(
                        TriangleDistance(p, new Vector3(a.x, a.y, a.z), new Vector3(b.x, b.y, b.z), new Vector3(c.x, c.y, c.z)),
                        TriangleDistance(p, new Vector3(a.x, a.y, a.z), new Vector3(c.x, c.y, c.z), new Vector3(d.x, d.y, d.z))) - a.w;
                default:
                    return float.PositiveInfinity;
            }
        }

        public static float SmoothUnion(float current, float operand, float k, out float currentWeight)
        {
            if (k <= 0.0000001f)
            {
                currentWeight = current <= operand ? 1f : 0f;
                return Mathf.Min(current, operand);
            }
            currentWeight = Mathf.Clamp01(0.5f + 0.5f * (operand - current) / k);
            return Mathf.Lerp(operand, current, currentWeight) - k * currentWeight * (1f - currentWeight);
        }

        public static float SmoothSubtraction(float current, float operand, float k, out float currentWeight)
        {
            if (k <= 0.0000001f)
            {
                float cut = -operand;
                currentWeight = current >= cut ? 1f : 0f;
                return Mathf.Max(current, cut);
            }
            float operandWeight = Mathf.Clamp01(0.5f - 0.5f * (current + operand) / k);
            currentWeight = 1f - operandWeight;
            return Mathf.Lerp(current, -operand, operandWeight) + k * operandWeight * (1f - operandWeight);
        }

        public static float SmoothIntersection(float current, float operand, float k, out float currentWeight)
        {
            if (k <= 0.0000001f)
            {
                currentWeight = current >= operand ? 1f : 0f;
                return Mathf.Max(current, operand);
            }
            currentWeight = Mathf.Clamp01(0.5f - 0.5f * (operand - current) / k);
            return Mathf.Lerp(operand, current, currentWeight) + k * currentWeight * (1f - currentWeight);
        }

        public static float Combine(float current, float operand, SDFOperationType operation, float smoothing, out float currentWeight)
        {
            switch (operation)
            {
                case SDFOperationType.Union:
                    currentWeight = current <= operand ? 1f : 0f;
                    return Mathf.Min(current, operand);
                case SDFOperationType.Subtraction:
                    float cut = -operand;
                    currentWeight = current >= cut ? 1f : 0f;
                    return Mathf.Max(current, cut);
                case SDFOperationType.Intersection:
                    currentWeight = current >= operand ? 1f : 0f;
                    return Mathf.Max(current, operand);
                case SDFOperationType.SmoothUnion:
                    return SmoothUnion(current, operand, smoothing, out currentWeight);
                case SDFOperationType.SmoothSubtraction:
                    return SmoothSubtraction(current, operand, smoothing, out currentWeight);
                case SDFOperationType.SmoothIntersection:
                    return SmoothIntersection(current, operand, smoothing, out currentWeight);
                default:
                    currentWeight = 1f;
                    return current;
            }
        }

        private static float Box(Vector3 p, Vector3 extents)
        {
            Vector3 q = Abs(p) - extents;
            return Max(q, Vector3.zero).magnitude + Mathf.Min(Mathf.Max(q.x, Mathf.Max(q.y, q.z)), 0f);
        }

        private static float BoxFrame(Vector3 p, Vector3 b, float e)
        {
            p = Abs(p) - b;
            Vector3 q = Abs(p + Vector3.one * e) - Vector3.one * e;
            return Mathf.Min(
                BoxDistance(new Vector3(p.x, q.y, q.z)),
                Mathf.Min(BoxDistance(new Vector3(q.x, p.y, q.z)), BoxDistance(new Vector3(q.x, q.y, p.z))));
        }

        private static float BoxDistance(Vector3 q) => Max(q, Vector3.zero).magnitude + Mathf.Min(Mathf.Max(q.x, Mathf.Max(q.y, q.z)), 0f);

        private static float Capsule(Vector3 p, Vector3 a, Vector3 b, float radius)
        {
            Vector3 pa = p - a;
            Vector3 ba = b - a;
            float h = Mathf.Clamp01(Vector3.Dot(pa, ba) / Mathf.Max(Vector3.Dot(ba, ba), 0.0000001f));
            return (pa - ba * h).magnitude - radius;
        }

        private static float CappedCylinder(Vector3 p, float height, float radius)
        {
            Vector2 q = new Vector2(new Vector2(p.x, p.z).magnitude, p.y);
            Vector2 delta = Abs(q) - new Vector2(radius, height);
            return Mathf.Min(Mathf.Max(delta.x, delta.y), 0f) + Max(delta, Vector2.zero).magnitude;
        }

        private static float CappedCone(Vector3 p, float height, float radiusA, float radiusB)
        {
            Vector2 q = new Vector2(new Vector2(p.x, p.z).magnitude, p.y);
            Vector2 k1 = new Vector2(radiusB, height);
            Vector2 k2 = new Vector2(radiusB - radiusA, 2f * height);
            Vector2 ca = new Vector2(q.x - Mathf.Min(q.x, q.y < 0f ? radiusA : radiusB), Mathf.Abs(q.y) - height);
            Vector2 cb = q - k1 + k2 * Mathf.Clamp01(Vector2.Dot(k1 - q, k2) / Mathf.Max(Vector2.Dot(k2, k2), 0.0000001f));
            float sign = cb.x < 0f && ca.y < 0f ? -1f : 1f;
            return sign * Mathf.Sqrt(Mathf.Min(Vector2.Dot(ca, ca), Vector2.Dot(cb, cb)));
        }

        private static float ArbitraryCappedCylinder(Vector3 p, Vector3 a, Vector3 b, float radius)
        {
            Vector3 ba = b - a;
            Vector3 pa = p - a;
            float baba = Mathf.Max(Vector3.Dot(ba, ba), 0.0000001f);
            float paba = Vector3.Dot(pa, ba);
            float x = (pa * baba - ba * paba).magnitude - radius * baba;
            float y = Mathf.Abs(paba - baba * 0.5f) - baba * 0.5f;
            float x2 = x * x;
            float y2 = y * y * baba;
            float value = Mathf.Max(x, y) < 0f ? -Mathf.Min(x2, y2) : Mathf.Max(x, 0f) * Mathf.Max(x, 0f) + Mathf.Max(y, 0f) * Mathf.Max(y, 0f) * baba;
            return Mathf.Sign(value) * Mathf.Sqrt(Mathf.Abs(value)) / baba;
        }

        private static float ArbitraryCappedCone(Vector3 p, Vector3 a, Vector3 b, float radiusA, float radiusB)
        {
            Vector3 ba = b - a;
            Vector3 pa = p - a;
            float rba = radiusB - radiusA;
            float baba = Mathf.Max(Vector3.Dot(ba, ba), 0.0000001f);
            float papa = Vector3.Dot(pa, pa);
            float paba = Vector3.Dot(pa, ba) / baba;
            float x = Mathf.Sqrt(Mathf.Max(papa - paba * paba * baba, 0f));
            float cax = Mathf.Max(0f, x - (paba < 0.5f ? radiusA : radiusB));
            float cay = Mathf.Abs(paba - 0.5f) - 0.5f;
            float k = rba * rba + baba;
            float f = Mathf.Clamp01((rba * (x - radiusA) + paba * baba) / Mathf.Max(k, 0.0000001f));
            float cbx = x - radiusA - f * rba;
            float cby = paba - f;
            float sign = cbx < 0f && cay < 0f ? -1f : 1f;
            return sign * Mathf.Sqrt(Mathf.Min(cax * cax + cay * cay * baba, cbx * cbx + cby * cby * baba));
        }

        private static float HexagonalPrism(Vector3 p, float radius, float halfHeight)
        {
            Vector3 k = new Vector3(-0.8660254f, 0.5f, 0.57735f);
            p = Abs(p);
            Vector2 xy = new Vector2(p.x, p.y);
            xy -= 2f * Mathf.Min(Vector2.Dot(new Vector2(k.x, k.y), xy), 0f) * new Vector2(k.x, k.y);
            Vector2 q = new Vector2(
                (xy - new Vector2(Mathf.Clamp(xy.x, -k.z * radius, k.z * radius), radius)).magnitude * Mathf.Sign(xy.y - radius),
                p.z - halfHeight);
            return Mathf.Min(Mathf.Max(q.x, q.y), 0f) + Max(q, Vector2.zero).magnitude;
        }

        private static float SolidAngle(Vector3 p, float radius, float angle)
        {
            Vector2 direction = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
            Vector2 q = new Vector2(new Vector2(p.x, p.z).magnitude, p.y);
            float l = q.magnitude - radius;
            float m = (q - direction * Mathf.Clamp(Vector2.Dot(q, direction), 0f, radius)).magnitude;
            return Mathf.Max(l, m * Mathf.Sign(direction.y * q.x - direction.x * q.y));
        }

        private static float CutSphere(Vector3 p, float radius, float height)
        {
            float w = Mathf.Sqrt(Mathf.Max(radius * radius - height * height, 0f));
            Vector2 q = new Vector2(new Vector2(p.x, p.z).magnitude, p.y);
            float s = Mathf.Max((height - radius) * q.x * q.x + w * w * (height + radius - 2f * q.y), height * q.x - w * q.y);
            if (s < 0f) return q.magnitude - radius;
            return q.x < w ? height - q.y : (q - new Vector2(w, height)).magnitude;
        }

        private static float CutHollowSphere(Vector3 p, float radius, float height, float thickness)
        {
            float w = Mathf.Sqrt(Mathf.Max(radius * radius - height * height, 0f));
            Vector2 q = new Vector2(new Vector2(p.x, p.z).magnitude, p.y);
            return (height * q.x < w * q.y ? (q - new Vector2(w, height)).magnitude : Mathf.Abs(q.magnitude - radius)) - thickness;
        }

        private static float DeathStar(Vector3 point, float radiusA, float radiusB, float centerDistance)
        {
            centerDistance = Mathf.Max(centerDistance, 0.0001f);
            float a = (radiusA * radiusA - radiusB * radiusB + centerDistance * centerDistance) / (2f * centerDistance);
            float b = Mathf.Sqrt(Mathf.Max(radiusA * radiusA - a * a, 0f));
            Vector2 p = new Vector2(point.x, new Vector2(point.y, point.z).magnitude);
            if (p.x * b - p.y * a > centerDistance * Mathf.Max(b - p.y, 0f))
                return (p - new Vector2(a, b)).magnitude;
            return Mathf.Max(p.magnitude - radiusA, -(p - new Vector2(centerDistance, 0f)).magnitude + radiusB);
        }

        private static float RoundCone(Vector3 p, Vector3 a, Vector3 b, float radiusA, float radiusB)
        {
            Vector3 ba = b - a;
            Vector3 pa = p - a;
            float l2 = Mathf.Max(Vector3.Dot(ba, ba), 0.0000001f);
            float rr = radiusA - radiusB;
            float a2 = Mathf.Max(l2 - rr * rr, 0.0000001f);
            float il2 = 1f / l2;
            float y = Vector3.Dot(pa, ba);
            float z = y - l2;
            float x2 = Vector3.Dot(pa * l2 - ba * y, pa * l2 - ba * y);
            float y2 = y * y * l2;
            float z2 = z * z * l2;
            float k = Mathf.Sign(rr) * rr * rr * x2;
            if (Mathf.Sign(z) * a2 * z2 > k) return Mathf.Sqrt(x2 + z2) * il2 - radiusB;
            if (Mathf.Sign(y) * a2 * y2 < k) return Mathf.Sqrt(x2 + y2) * il2 - radiusA;
            return (Mathf.Sqrt(x2 * a2 * il2) + y * rr) * il2 - radiusA;
        }

        private static float RevolvedVesica(Vector3 p, Vector3 a, Vector3 b, float width)
        {
            width = Mathf.Max(width, 0.0001f);
            Vector3 center = (a + b) * 0.5f;
            float length = Mathf.Max((b - a).magnitude, 0.0001f);
            Vector3 axis = (b - a) / length;
            float y = Vector3.Dot(p - center, axis);
            Vector2 q = new Vector2((p - center - y * axis).magnitude, Mathf.Abs(y));
            float radius = length * 0.5f;
            float offset = 0.5f * (radius * radius - width * width) / width;
            Vector3 h = radius * q.x < offset * (q.y - radius) ? new Vector3(0f, radius, 0f) : new Vector3(-offset, 0f, offset + width);
            return (q - new Vector2(h.x, h.y)).magnitude - h.z;
        }

        private static float Rhombus(Vector3 p, float diagonalA, float diagonalB, float halfHeight, float rounding)
        {
            p = Abs(p);
            Vector2 b = new Vector2(diagonalA, diagonalB);
            Vector2 pxz = new Vector2(p.x, p.z);
            float f = Mathf.Clamp(Ndot(b, b - 2f * pxz) / Mathf.Max(Vector2.Dot(b, b), 0.0000001f), -1f, 1f);
            Vector2 point = pxz - 0.5f * new Vector2(b.x * (1f - f), b.y * (1f + f));
            float sign = Mathf.Sign(p.x * b.y + p.z * b.x - b.x * b.y);
            Vector2 q = new Vector2(point.magnitude * sign - rounding, p.y - halfHeight);
            return Mathf.Min(Mathf.Max(q.x, q.y), 0f) + Max(q, Vector2.zero).magnitude;
        }

        private static float Pyramid(Vector3 p, float height)
        {
            float m2 = height * height + 0.25f;
            p.x = Mathf.Abs(p.x); p.z = Mathf.Abs(p.z);
            if (p.z > p.x) { float swap = p.x; p.x = p.z; p.z = swap; }
            p.x -= 0.5f; p.z -= 0.5f;
            Vector3 q = new Vector3(p.z, height * p.y - 0.5f * p.x, height * p.x + 0.5f * p.y);
            float s = Mathf.Max(-q.x, 0f);
            float t = Mathf.Clamp01((q.y - 0.5f * p.z) / (m2 + 0.25f));
            float a = m2 * (q.x + s) * (q.x + s) + q.y * q.y;
            float b = m2 * (q.x + 0.5f * t) * (q.x + 0.5f * t) + (q.y - m2 * t) * (q.y - m2 * t);
            float distance2 = Mathf.Min(q.y, -q.x * m2 - q.y * 0.5f) > 0f ? 0f : Mathf.Min(a, b);
            return Mathf.Sqrt((distance2 + q.z * q.z) / Mathf.Max(m2, 0.0000001f)) * Mathf.Sign(Mathf.Max(q.z, -p.y));
        }

        private static float Ndot(Vector2 a, Vector2 b) => a.x * b.x - a.y * b.y;

        private static float EllipsoidBound(Vector3 p, Vector3 radii)
        {
            radii = Max(radii, Vector3.one * 0.0001f);
            float k0 = new Vector3(p.x / radii.x, p.y / radii.y, p.z / radii.z).magnitude;
            Vector3 rr = new Vector3(radii.x * radii.x, radii.y * radii.y, radii.z * radii.z);
            float k1 = new Vector3(p.x / rr.x, p.y / rr.y, p.z / rr.z).magnitude;
            return k0 * (k0 - 1f) / Mathf.Max(k1, 0.0000001f);
        }

        private static float Octahedron(Vector3 p, float s)
        {
            p = Abs(p);
            float m = p.x + p.y + p.z - s;
            Vector3 q;
            if (3f * p.x < m) q = p;
            else if (3f * p.y < m) q = new Vector3(p.y, p.z, p.x);
            else if (3f * p.z < m) q = new Vector3(p.z, p.x, p.y);
            else return m * 0.57735027f;
            float k = Mathf.Clamp(0.5f * (q.z - q.y + s), 0f, s);
            return new Vector3(q.x, q.y - s + k, q.z - k).magnitude;
        }

        private static float TriangleDistance(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ba = b - a; Vector3 pa = p - a;
            Vector3 cb = c - b; Vector3 pb = p - b;
            Vector3 ac = a - c; Vector3 pc = p - c;
            Vector3 normal = Vector3.Cross(ba, ac);
            float inside = Mathf.Sign(Vector3.Dot(Vector3.Cross(ba, normal), pa)) +
                           Mathf.Sign(Vector3.Dot(Vector3.Cross(cb, normal), pb)) +
                           Mathf.Sign(Vector3.Dot(Vector3.Cross(ac, normal), pc));
            if (inside < 2f)
                return Mathf.Sqrt(Mathf.Min(SegmentDistanceSquared(pa, ba), Mathf.Min(SegmentDistanceSquared(pb, cb), SegmentDistanceSquared(pc, ac))));
            return Mathf.Abs(Vector3.Dot(normal, pa)) / Mathf.Max(normal.magnitude, 0.0000001f);
        }

        private static float SegmentDistanceSquared(Vector3 pointFromStart, Vector3 segment)
        {
            float h = Mathf.Clamp01(Vector3.Dot(pointFromStart, segment) / Mathf.Max(Vector3.Dot(segment, segment), 0.0000001f));
            return (segment * h - pointFromStart).sqrMagnitude;
        }

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        private static Vector2 Abs(Vector2 v) => new Vector2(Mathf.Abs(v.x), Mathf.Abs(v.y));
        private static Vector3 Max(Vector3 a, Vector3 b) => new Vector3(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y), Mathf.Max(a.z, b.z));
        private static Vector2 Max(Vector2 a, Vector2 b) => new Vector2(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
    }
}
