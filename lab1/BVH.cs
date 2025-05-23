﻿using lab1.Shadow;
using System.Collections.Generic;
using System.Numerics;
using static System.Numerics.Vector3;
using static System.Single;

namespace lab1
{
    public class BVHNode
    {
        public Vector3 aabbMin, aabbMax;
        public int leftNode, firstTri, triCount;

        public bool IsLeaf() { return triCount > 0; }
    }

    public class Tri
    {
        public Vector3 v0, v1, v2;
        public Vector3 Centroid;
        public int Index;
    }

    public class BVH
    {
        public static BVHNode[]? Nodes { get; set; }
        public static Tri[]? Tris { get; set; }

        private static int rootNodeIndx = 0, nodesUsed = 1;

        public static void Destroy()
        {
            Tris = null;
            Nodes = null;
            rootNodeIndx = 0;
            nodesUsed = 1;
        }

        public static void Build(List<Vector3> vertices, List<int> opaqueFacesIndexes, List<int> verticesIndices)
        {
            Tris = new Tri[opaqueFacesIndexes.Count];
            Nodes = new BVHNode[opaqueFacesIndexes.Count * 2 - 1];
            rootNodeIndx = 0;
            nodesUsed = 1;

            for (int i = 0; i < Nodes.Length; i++)
            {
                Nodes[i] = new();
            }

            for (int i = 0; i < opaqueFacesIndexes.Count; i++)
            {
                int index = opaqueFacesIndexes[i] * 3;
                Vector3 v0 = vertices[verticesIndices[index]];
                Vector3 v1 = vertices[verticesIndices[index + 1]];
                Vector3 v2 = vertices[verticesIndices[index + 2]];
                Tri tri = new()
                {
                    Index = opaqueFacesIndexes[i],
                    v0 = v0,
                    v1 = v1,
                    v2 = v2,
                };
                tri.Centroid = (tri.v0 + tri.v1 + tri.v2) * 0.3333f;
                Tris[i] = tri;
            }
            BVHNode root = Nodes[rootNodeIndx];
            root.leftNode = 0;
            root.firstTri = 0;
            root.triCount = Tris.Length;
            UpdateNodeBounds(rootNodeIndx);
            Subdivide(rootNodeIndx);
        }

        private static void UpdateNodeBounds(int nodeIndx)
        {
            BVHNode node = Nodes![nodeIndx];
            node.aabbMin = new(1e30f);
            node.aabbMax = new(-1e30f);
            for (int first = node.firstTri, i = 0; i < node.triCount; i++)
            {
                Tri leaf = Tris![first + i];
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
            BVHNode node = Nodes![nodeIndx];
            Vector3 extent = node.aabbMax - node.aabbMin;
            int[] axes = [0, 1, 2];
            if (extent.Y > extent.X)
                (axes[0], axes[1]) = (axes[1], axes[0]);
            if (extent.Z > extent[axes[0]])
                (axes[0], axes[2]) = (axes[2], axes[0]);

            for (int a = 0; a < axes.Length; a++)
            {
                int axis = axes[a];
                float splitPos = node.aabbMin[axis] + extent[axis] * 0.5f;
                int i = node.firstTri;
                int j = i + node.triCount - 1;
                while (i <= j)
                {
                    if (Tris![i].Centroid[axis] < splitPos)
                        i++;
                    else
                    {
                        (Tris[i], Tris[j]) = (Tris[j], Tris[i]);
                        j--;
                    }
                }

                int leftCount = i - node.firstTri;
                if (leftCount == 0 || leftCount == node.triCount) continue;

                int leftChild = nodesUsed++;
                int rightChild = nodesUsed++;
                Nodes[leftChild].firstTri = node.firstTri;
                Nodes[leftChild].triCount = leftCount;
                Nodes[rightChild].firstTri = i;
                Nodes[rightChild].triCount = node.triCount - leftCount;
                node.leftNode = leftChild;
                node.triCount = 0;

                UpdateNodeBounds(leftChild);
                UpdateNodeBounds(rightChild);

                Subdivide(leftChild);
                Subdivide(rightChild);

                break;
            }
        }

        public static bool IntersectBVH(Vector3 orig, Vector3 dir, float dist, int nodeIndx)
        {
            if (Nodes == null) return false;

            BVHNode node = Nodes[nodeIndx];
            if (!RTX.IntersectAABB(orig, dir, node.aabbMin, node.aabbMax)) return false;
            if (node.IsLeaf())
            {
                for (int i = 0; i < node.triCount; i++)
                {
                    Tri tri = Tris![node.firstTri + i];
                    float d = RTX.IntersectTriangle(orig, dir, tri.v0, tri.v1, tri.v2);
                    if (d > 1e-4f && d < dist) return true;
                }
                return false;
            }
            else
            {
                if (IntersectBVH(orig, dir, dist, node.leftNode)) return true;
                return IntersectBVH(orig, dir, dist, node.leftNode + 1);
            }
        }

    }
}