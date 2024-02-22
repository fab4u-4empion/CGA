using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Numerics;
using System.Reflection.Emit;
using System.Security.Permissions;
using System.Windows.Media.Media3D;
using System.Xml.Linq;
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

    public class Bin { 
        public AABB bounds = new(); 
        public int triCount = 0; 
    };

    public class AABB
    {
        public Vector3 BMin = new(1e30f), BMax = new(-1e30f);

        public void Grow(Vector3 p)
        {
            BMin = Min(p, BMin);
            BMax = Max(p, BMax);
        }

        public void Grow(AABB b) 
        { 
            if (b.BMin.X != 1e30f) 
            { 
                Grow(b.BMin); 
                Grow(b.BMax); 
            } 
        }

        public float Area()
        {
            Vector3 e = BMax - BMin;
            return e.X * e.Y + e.Y * e.Z + e.Z * e.X;
        }
    }

    public class BVH
    {
        private static BVHNode[] nodes;
        private static int BINS = 8;
        public static Tri[] Tris;

        private static int rootNodeIndx = 0, nodesUsed = 1;

        public static void Build(List<Vector3> vertices, List<int> opaqueFacesIndexes, List<int> verticesIndices)
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
                Vector3 v0 = vertices[verticesIndices[index]];
                Vector3 v1 = vertices[verticesIndices[index + 1]];
                Vector3 v2 = vertices[verticesIndices[index + 2]];
                Tri tri = new() {
                    Index = opaqueFacesIndexes[i],
                    v0 = v0,
                    v1 = v1,
                    v2 = v2,
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

        private static float FindBestSplitPlane(BVHNode node, ref int axis, ref float splitPos)
        {
            float bestCost = 1e30f;
            for (int a = 0; a < 3; a++)
            {
                float boundsMin = 1e30f, boundsMax = -1e30f;
                for (int i = 0; i < node.triCount; i++)
                {
                    Tri triangle = Tris[node.firstTri + i];
                    boundsMin = Min(boundsMin, triangle.Centroid[a]);
                    boundsMax = Max(boundsMax, triangle.Centroid[a]);
                }

                if (boundsMin == boundsMax) continue;

                Bin[] bin = new Bin[BINS];
                for (int i = 0; i < BINS; i++)
                    bin[i] = new();

                float scale = BINS / (boundsMax - boundsMin);
                for (uint i = 0; i < node.triCount; i++)
                {
                    Tri triangle = Tris[node.firstTri + i];
                    int binIdx = int.Min(BINS - 1, (int)((triangle.Centroid[a] - boundsMin) * scale));
                    bin[binIdx].triCount++;
                    bin[binIdx].bounds.Grow(triangle.v0);
                    bin[binIdx].bounds.Grow(triangle.v1);
                    bin[binIdx].bounds.Grow(triangle.v2);
                }

                float[] leftArea = new float[BINS - 1], rightArea = new float[BINS - 1];
                int[] leftCount = new int[BINS - 1], rightCount = new int[BINS - 1];

                AABB leftBox = new(), rightBox = new();
                int leftSum = 0, rightSum = 0;
                for (int i = 0; i < BINS - 1; i++)
                {
                    leftSum += bin[i].triCount;
                    leftCount[i] = leftSum;
                    leftBox.Grow(bin[i].bounds);
                    leftArea[i] = leftBox.Area();

                    rightSum += bin[BINS - 1 - i].triCount;
                    rightCount[BINS - 2 - i] = rightSum;
                    rightBox.Grow(bin[BINS - 1 - i].bounds);
                    rightArea[BINS - 2 - i] = rightBox.Area();
                }

                scale = (boundsMax - boundsMin) / BINS;
                for (int i = 0; i < BINS - 1; i++)
                {
                    float planeCost = leftCount[i] * leftArea[i] + rightCount[i] * rightArea[i];
                    if (planeCost < bestCost)
                        (splitPos, axis, bestCost) = (boundsMin + scale * (i + 1), a, planeCost);
                }
            }
            return bestCost;
        }

        private static float CalculateNodeCost(BVHNode node)
        {
            Vector3 e = node.aabbMax - node.aabbMin;
            float surfaceArea = e.X * e.Y + e.Y * e.Z + e.Z * e.X;
            return node.triCount * surfaceArea;
        }

        private static void Subdivide(int nodeIndx)
        {
            BVHNode node = nodes[nodeIndx];

            int axis = 0;
            float splitPos = 0;

            float splitCost = FindBestSplitPlane(node, ref axis, ref splitPos);

            float nosplitCost = CalculateNodeCost(node);
            if (splitCost >= nosplitCost) return;

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
