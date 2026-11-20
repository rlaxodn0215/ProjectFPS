#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GSPAWN
{
    public static class SpawnActionMeshOverlap
    {
        private struct MeshEntry
        {
            public PluginMesh mesh;
            public Matrix4x4 meshToRoot;
        }

        private sealed class Geometry
        {
            public bool precise;
            public readonly List<MeshEntry> meshes = new List<MeshEntry>(4);
        }

        private struct TriangleEntry
        {
            public PluginMeshTriangle triangle;
            public Vector3 min;
            public Vector3 max;
        }

        private static readonly Dictionary<GameObject, Geometry> _cache = new Dictionary<GameObject, Geometry>();
        private static readonly List<TriangleEntry> _firstTriangles = new List<TriangleEntry>();
        private static readonly List<TriangleEntry> _secondTriangles = new List<TriangleEntry>();
        private static readonly Vector3[] _triangleVertices = new Vector3[3];

        public static void clearCache()
        {
            _cache.Clear();
        }

        public static bool tryMeshOverlap(GameObject firstRoot, Matrix4x4 firstWorld,
            GameObject secondRoot, Matrix4x4 secondWorld, out bool overlaps)
        {
            overlaps = false;
            if (!firstRoot || !secondRoot || Mathf.Abs(firstWorld.determinant) <= 1e-10f ||
                Mathf.Abs(secondWorld.determinant) <= 1e-10f)
                return false;

            Geometry first = getGeometry(firstRoot);
            Geometry second = getGeometry(secondRoot);
            if (!first.precise || !second.precise) return false;

            foreach (MeshEntry a in first.meshes)
            {
                Matrix4x4 aWorld = firstWorld * a.meshToRoot;
                foreach (MeshEntry b in second.meshes)
                {
                    Matrix4x4 bWorld = secondWorld * b.meshToRoot;
                    if (meshIntersects(a.mesh, aWorld, b.mesh, bWorld))
                    {
                        overlaps = true;
                        return true;
                    }
                }
            }
            return true;
        }

        private static Geometry getGeometry(GameObject root)
        {
            Geometry geometry;
            if (_cache.TryGetValue(root, out geometry)) return geometry;

            geometry = new Geometry();
            _cache.Add(root, geometry);
            if (!root) return geometry;

            Transform rootTransform = root.transform;
            Matrix4x4 rootInverse = rootTransform.worldToLocalMatrix;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (!activeInHierarchyFromRoot(child, rootTransform)) continue;
                GameObject go = child.gameObject;
                SpriteRenderer sprite = go.GetComponent<SpriteRenderer>();
                SkinnedMeshRenderer skinned = go.GetComponent<SkinnedMeshRenderer>();
                if ((sprite && sprite.enabled) || (skinned && skinned.enabled)) return geometry;

                MeshRenderer renderer = go.GetComponent<MeshRenderer>();
                if (!renderer || !renderer.enabled) continue;
                MeshFilter filter = go.GetComponent<MeshFilter>();
                if (!filter || !filter.sharedMesh || !filter.sharedMesh.isReadable) return geometry;

                PluginMesh pluginMesh;
                try { pluginMesh = PluginMeshDb.instance.getPluginMesh(filter.sharedMesh); }
                catch (Exception) { return geometry; }
                if (pluginMesh == null) return geometry;
                geometry.meshes.Add(new MeshEntry
                {
                    mesh = pluginMesh,
                    meshToRoot = rootInverse * child.localToWorldMatrix
                });
            }
            geometry.precise = geometry.meshes.Count != 0;
            return geometry;
        }

        private static bool activeInHierarchyFromRoot(Transform current, Transform root)
        {
            while (current)
            {
                if (!current.gameObject.activeSelf) return false;
                if (current == root) return true;
                current = current.parent;
            }
            return false;
        }

        private static bool meshIntersects(PluginMesh first, Matrix4x4 firstWorld,
            PluginMesh second, Matrix4x4 secondWorld)
        {
            buildTriangles(first, firstWorld, _firstTriangles);
            buildTriangles(second, secondWorld, _secondTriangles);
            _firstTriangles.Sort((a, b) => a.min.x.CompareTo(b.min.x));
            _secondTriangles.Sort((a, b) => a.min.x.CompareTo(b.min.x));

            int secondStart = 0;
            foreach (TriangleEntry a in _firstTriangles)
            {
                while (secondStart < _secondTriangles.Count &&
                    _secondTriangles[secondStart].max.x < a.min.x)
                    ++secondStart;

                for (int j = secondStart; j < _secondTriangles.Count; ++j)
                {
                    TriangleEntry b = _secondTriangles[j];
                    if (b.min.x > a.max.x) break;
                    if (a.min.y > b.max.y || b.min.y > a.max.y ||
                        a.min.z > b.max.z || b.min.z > a.max.z) continue;
                    if (TestTrianglesIntersect(a.triangle.v0, a.triangle.v1, a.triangle.v2,
                        b.triangle.v0, b.triangle.v1, b.triangle.v2)) return true;
                }
            }
            return false;
        }

        private static void buildTriangles(PluginMesh mesh, Matrix4x4 world, List<TriangleEntry> target)
        {
            target.Clear();
            for (int i = 0; i < mesh.numTriangles; ++i)
            {
                mesh.getTriangleVerts(i, _triangleVertices, world);
                Vector3 a = _triangleVertices[0];
                Vector3 b = _triangleVertices[1];
                Vector3 c = _triangleVertices[2];
                target.Add(new TriangleEntry
                {
                    triangle = new PluginMeshTriangle(a, b, c, i),
                    min = Vector3.Min(a, Vector3.Min(b, c)),
                    max = Vector3.Max(a, Vector3.Max(b, c))
                });
            }
        }

        static bool TestTrianglesIntersect(Vector3 a0, Vector3 a1, Vector3 a2,
            Vector3 b0, Vector3 b1, Vector3 b2)
        {
            Vector3 aEdge0 = a1 - a0;
            Vector3 aEdge1 = a2 - a1;
            Vector3 aEdge2 = a0 - a2;
            Vector3 bEdge0 = b1 - b0;
            Vector3 bEdge1 = b2 - b1;
            Vector3 bEdge2 = b0 - b2;
            Vector3 aNormal = Vector3.Cross(aEdge0, a2 - a0);
            Vector3 bNormal = Vector3.Cross(bEdge0, b2 - b0);

            float aNormalSq = aNormal.sqrMagnitude;
            float bNormalSq = bNormal.sqrMagnitude;
            if (aNormalSq <= 1e-12f || bNormalSq <= 1e-12f)
                return false;

            Vector3 normalCross = Vector3.Cross(aNormal, bNormal);
            float normalScale = aNormalSq * bNormalSq;
            if (normalCross.sqrMagnitude <= normalScale * 1e-10f)
            {
                float planeTolerance = Mathf.Sqrt(aNormalSq) * 1e-5f;
                if (Mathf.Abs(Vector3.Dot(aNormal, b0 - a0)) <= planeTolerance &&
                    Mathf.Abs(Vector3.Dot(aNormal, b1 - a0)) <= planeTolerance &&
                    Mathf.Abs(Vector3.Dot(aNormal, b2 - a0)) <= planeTolerance)
                    return TestCoplanarTrianglesIntersect(a0, a1, a2, b0, b1, b2, aNormal);
            }

            if (TrianglesSeparatedOnAxis(a0, a1, a2, b0, b1, b2, aNormal) ||
                TrianglesSeparatedOnAxis(a0, a1, a2, b0, b1, b2, bNormal) ||
                TrianglesSeparatedOnAxis(a0, a1, a2, b0, b1, b2, Vector3.Cross(aEdge0, bEdge0)) ||
                TrianglesSeparatedOnAxis(a0, a1, a2, b0, b1, b2, Vector3.Cross(aEdge0, bEdge1)) ||
                TrianglesSeparatedOnAxis(a0, a1, a2, b0, b1, b2, Vector3.Cross(aEdge0, bEdge2)) ||
                TrianglesSeparatedOnAxis(a0, a1, a2, b0, b1, b2, Vector3.Cross(aEdge1, bEdge0)) ||
                TrianglesSeparatedOnAxis(a0, a1, a2, b0, b1, b2, Vector3.Cross(aEdge1, bEdge1)) ||
                TrianglesSeparatedOnAxis(a0, a1, a2, b0, b1, b2, Vector3.Cross(aEdge1, bEdge2)) ||
                TrianglesSeparatedOnAxis(a0, a1, a2, b0, b1, b2, Vector3.Cross(aEdge2, bEdge0)) ||
                TrianglesSeparatedOnAxis(a0, a1, a2, b0, b1, b2, Vector3.Cross(aEdge2, bEdge1)) ||
                TrianglesSeparatedOnAxis(a0, a1, a2, b0, b1, b2, Vector3.Cross(aEdge2, bEdge2)))
                return false;

            return true;
        }

        static bool TrianglesSeparatedOnAxis(Vector3 a0, Vector3 a1, Vector3 a2,
            Vector3 b0, Vector3 b1, Vector3 b2, Vector3 axis)
        {
            float axisLengthSq = axis.sqrMagnitude;
            if (axisLengthSq <= 1e-12f)
                return false;

            axis *= 1.0f / Mathf.Sqrt(axisLengthSq);
            float aProj0 = Vector3.Dot(a0, axis);
            float aProj1 = Vector3.Dot(a1, axis);
            float aProj2 = Vector3.Dot(a2, axis);
            float bProj0 = Vector3.Dot(b0, axis);
            float bProj1 = Vector3.Dot(b1, axis);
            float bProj2 = Vector3.Dot(b2, axis);
            float aMin = Mathf.Min(aProj0, Mathf.Min(aProj1, aProj2));
            float aMax = Mathf.Max(aProj0, Mathf.Max(aProj1, aProj2));
            float bMin = Mathf.Min(bProj0, Mathf.Min(bProj1, bProj2));
            float bMax = Mathf.Max(bProj0, Mathf.Max(bProj1, bProj2));
            float tolerance = Mathf.Max(1.0f, Mathf.Max(Mathf.Abs(aMin), Mathf.Max(Mathf.Abs(aMax),
                Mathf.Max(Mathf.Abs(bMin), Mathf.Abs(bMax))))) * 1e-6f;
            return aMax < bMin - tolerance || bMax < aMin - tolerance;
        }

        static bool TestCoplanarTrianglesIntersect(Vector3 a0, Vector3 a1, Vector3 a2,
            Vector3 b0, Vector3 b1, Vector3 b2, Vector3 normal)
        {
            Vector3 absNormal = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
            int dropAxis = absNormal.x > absNormal.y ?
                (absNormal.x > absNormal.z ? 0 : 2) :
                (absNormal.y > absNormal.z ? 1 : 2);

            Vector2 a2D0 = ProjectTrianglePoint(a0, dropAxis);
            Vector2 a2D1 = ProjectTrianglePoint(a1, dropAxis);
            Vector2 a2D2 = ProjectTrianglePoint(a2, dropAxis);
            Vector2 b2D0 = ProjectTrianglePoint(b0, dropAxis);
            Vector2 b2D1 = ProjectTrianglePoint(b1, dropAxis);
            Vector2 b2D2 = ProjectTrianglePoint(b2, dropAxis);

            if (SegmentsIntersect2D(a2D0, a2D1, b2D0, b2D1) ||
                SegmentsIntersect2D(a2D0, a2D1, b2D1, b2D2) ||
                SegmentsIntersect2D(a2D0, a2D1, b2D2, b2D0) ||
                SegmentsIntersect2D(a2D1, a2D2, b2D0, b2D1) ||
                SegmentsIntersect2D(a2D1, a2D2, b2D1, b2D2) ||
                SegmentsIntersect2D(a2D1, a2D2, b2D2, b2D0) ||
                SegmentsIntersect2D(a2D2, a2D0, b2D0, b2D1) ||
                SegmentsIntersect2D(a2D2, a2D0, b2D1, b2D2) ||
                SegmentsIntersect2D(a2D2, a2D0, b2D2, b2D0))
                return true;

            return PointInTriangle2D(a2D0, b2D0, b2D1, b2D2) ||
                   PointInTriangle2D(b2D0, a2D0, a2D1, a2D2);
        }

        static Vector2 ProjectTrianglePoint(Vector3 point, int dropAxis)
        {
            if (dropAxis == 0) return new Vector2(point.y, point.z);
            if (dropAxis == 1) return new Vector2(point.x, point.z);
            return new Vector2(point.x, point.y);
        }

        static bool SegmentsIntersect2D(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1)
        {
            float o0 = Cross2D(a1 - a0, b0 - a0);
            float o1 = Cross2D(a1 - a0, b1 - a0);
            float o2 = Cross2D(b1 - b0, a0 - b0);
            float o3 = Cross2D(b1 - b0, a1 - b0);
            const float epsilon = 1e-6f;

            if (((o0 > epsilon && o1 < -epsilon) || (o0 < -epsilon && o1 > epsilon)) &&
                ((o2 > epsilon && o3 < -epsilon) || (o2 < -epsilon && o3 > epsilon)))
                return true;

            if (Mathf.Abs(o0) <= epsilon && PointOnSegment2D(b0, a0, a1)) return true;
            if (Mathf.Abs(o1) <= epsilon && PointOnSegment2D(b1, a0, a1)) return true;
            if (Mathf.Abs(o2) <= epsilon && PointOnSegment2D(a0, b0, b1)) return true;
            if (Mathf.Abs(o3) <= epsilon && PointOnSegment2D(a1, b0, b1)) return true;
            return false;
        }

        static bool PointOnSegment2D(Vector2 point, Vector2 segmentStart, Vector2 segmentEnd)
        {
            const float epsilon = 1e-6f;
            return point.x >= Mathf.Min(segmentStart.x, segmentEnd.x) - epsilon &&
                   point.x <= Mathf.Max(segmentStart.x, segmentEnd.x) + epsilon &&
                   point.y >= Mathf.Min(segmentStart.y, segmentEnd.y) - epsilon &&
                   point.y <= Mathf.Max(segmentStart.y, segmentEnd.y) + epsilon;
        }

        static bool PointInTriangle2D(Vector2 point, Vector2 p0, Vector2 p1, Vector2 p2)
        {
            float c0 = Cross2D(p1 - p0, point - p0);
            float c1 = Cross2D(p2 - p1, point - p1);
            float c2 = Cross2D(p0 - p2, point - p2);
            const float epsilon = 1e-6f;
            bool hasNegative = c0 < -epsilon || c1 < -epsilon || c2 < -epsilon;
            bool hasPositive = c0 > epsilon || c1 > epsilon || c2 > epsilon;
            return !(hasNegative && hasPositive);
        }

        static float Cross2D(Vector2 first, Vector2 second)
        {
            return first.x * second.y - first.y * second.x;
        }
    }
}
#endif
