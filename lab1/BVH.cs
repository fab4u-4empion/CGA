using System.Collections.Generic;
using System.Numerics;
using System.Reflection.Emit;
using System.Security.Permissions;
using static System.Numerics.Vector3;
using static System.Single;

namespace lab1
{
    public class BVHNode
    {
        public Vector3 aabbMin, aabbMax;
        public int leftNode, firstTri, triCount;

        public bool IsLeaf() {  return triCount > 0; }
    }

    public class Tri
    {
        public Vector3 v0, v1, v2;
        public Vector3 Centroid;
        public int Index;
    }

    public class BVH
    {
        private static BVHNode[] nodes;
        public static Tri[] Tris;

        private static int rootNodeIndx = 0, nodesUsed = 1;

        public static void Build(List<Vector4> vertices, List<int> opaqueFacesIndexes, List<int> verticesIndices)
        {
            Tris = new Tri[opaqueFacesIndexes.Count];
            nodes = new BVHNode[opaqueFacesIndexes.Count * 2 - 1];
            rootNodeIndx = 0;
            nodesUsed = 1;

            for (int i = 0; i < nodes.Length; i++)
            {
                nodes[i] = new();
            }

            for (int i = 0; i < opaqueFacesIndexes.Count; i++)
            {
                int index = opaqueFacesIndexes[i] * 3;
                Vector4 v0 = vertices[verticesIndices[index]];
                Vector4 v1 = vertices[verticesIndices[index + 1]];
                Vector4 v2 = vertices[verticesIndices[index + 2]];
                Tri tri = new() {
                    Index = opaqueFacesIndexes[i],
                    v0 = new(v0.X, v0.Y, v0.Z),
                    v1 = new(v1.X, v1.Y, v1.Z),
                    v2 = new(v2.X, v2.Y, v2.Z),
                };
                tri.Centroid = (tri.v0 + tri.v1 + tri.v2) * 0.3333f;
                Tris[i] = tri;
            }
            BVHNode root = nodes[rootNodeIndx];
            root.leftNode = 0;
            root.firstTri = 0;
            root.triCount = Tris.Length;
            UpdateNodeBounds(rootNodeIndx);
            Subdivide(rootNodeIndx);
        }

        private static void UpdateNodeBounds(int nodeIndx)
        {
            BVHNode node = nodes[nodeIndx];
            node.aabbMin = new(1e30f);
            node.aabbMax = new(-1e30f);
            for(int first = node.firstTri, i = 0; i < node.triCount; i++)
            {
                Tri leaf = Tris[first + i];
                node.aabbMin = Min(node.aabbMin, leaf.v0);
                node.aabbMin = Min(node.aabbMin, leaf.v1);
                node.aabbMin = Min(node.aabbMin, leaf.v2);
                node.aabbMax = Max(node.aabbMax, leaf.v0);
                node.aabbMax = Max(node.aabbMax, leaf.v1);
                node.aabbMax = Max(node.aabbMax, leaf.v2);
            }
        }

        private static void Subdivide(int nodeIndx)
        {
            BVHNode node = nodes[nodeIndx];
            Vector3 extent = node.aabbMax - node.aabbMin;
            int axis = 0;
            if (extent.Y > extent.X) axis = 1;
            if (extent.Z > extent[axis]) axis = 2;
            float splitPos = node.aabbMin[axis] + extent[axis] * 0.5f;

            int i = node.firstTri;
            int j = i + node.triCount - 1;
            while (i <= j)
            {
                if (Tris[i].Centroid[axis] < splitPos)
                    i++;
                else
                {
                    (Tris[i], Tris[j]) = (Tris[j], Tris[i]);
                    j--;
                }
            }

            int leftCount = i - node.firstTri;
            if (leftCount == 0 || leftCount == node.triCount) return;

            int leftChild = nodesUsed++;
            int rightChild = nodesUsed++;
            nodes[leftChild].firstTri = node.firstTri;
            nodes[leftChild].triCount = leftCount;
            nodes[rightChild].firstTri = i;
            nodes[rightChild].triCount = node.triCount - leftCount;
            node.leftNode = leftChild;
            node.triCount = 0;

            UpdateNodeBounds(leftChild);
            UpdateNodeBounds(rightChild);
            
            Subdivide(leftChild);
            Subdivide(rightChild);
        }

        public static bool IntersectBVH(Vector3 orig, Vector3 dir, float dist, int faceIndex, int nodeIndx)
        {
            BVHNode node = nodes[nodeIndx];
            if (!IntersectAABB(orig, dir, node.aabbMin, node.aabbMax)) return false;
            if (node.IsLeaf())
            {
                for (int i = 0; i < node.triCount; i++)
                {
                    Tri tri = Tris[node.firstTri + i];
                    if (faceIndex == tri.Index) continue;
                    float d = IntersectTriangle(orig, dir, tri.v0, tri.v1, tri.v2);
                    if (d > -0 && d < dist) return true;
                }
                return false;
            }
            else
            {
                if (IntersectBVH(orig, dir, dist, faceIndex, node.leftNode)) return true;
                return IntersectBVH(orig, dir, dist, faceIndex, node.leftNode + 1);
            }
        }

        private static bool IntersectAABB(Vector3 O, Vector3 D, Vector3 bmin, Vector3 bmax)
        {
            Vector3 t1 = (bmin - O) / D;
            Vector3 t2 = (bmax - O) / D;
            float tmin = Max(Max(Min(t1.X, t2.X), Min(t1.Y, t2.Y)), Min(t1.Z, t2.Z));
            float tmax = Min(Min(Max(t1.X, t2.X), Max(t1.Y, t2.Y)), Max(t1.Z, t2.Z));
            return tmax >= tmin && tmax > 0;
        }

        private static float IntersectTriangle(Vector3 orig, Vector3 dir, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 e1 = b - a;
            Vector3 e2 = c - a;

            Vector3 pvec = Cross(dir, e2);
            float det = Dot(e1, pvec);

            if (det < 1e-8f && det > -1e-8f)
            {
                return 0;
            }

            float inv_det = 1 / det;
            Vector3 tvec = orig - a;
            float u = Dot(tvec, pvec) * inv_det;

            if (u < 0 || u > 1)
            {
                return 0;
            }

            Vector3 qvec = Cross(tvec, e1);
            float v = Dot(dir, qvec) * inv_det;

            if (v < 0 || u + v > 1)
            {
                return 0;
            }

            return Dot(e2, qvec) * inv_det;
        }
    }
}
