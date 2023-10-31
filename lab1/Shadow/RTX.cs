using System.Collections.Generic;
using System.Numerics;
using static System.Numerics.Vector3;

namespace lab1.Shadow
{
    public class RTX
    {
        public static float TriangleIntersections(Vector3 orig, Vector3 dir, Vector3 a, Vector3 b, Vector3 c) {
            Vector3 e1 = b - a;
            Vector3 e2 = c - a;

            Vector3 pvec = Cross(dir, e2);
            float det = Dot(e1, pvec);

            if (det < 1e-8f && det > -1e-8f) {
                return 0;
            }

            float inv_det = 1 / det;
            Vector3 tvec = orig - a;
            float u = Dot(tvec, pvec) * inv_det;

            if (u < 0 || u > 1) {
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

        public static bool CheckIntersection(Vector3 dir, Vector3 orig, float dist, List<List<Vector3>> faces, int faceIndex)
        {
            for (int i = 0; i < faces.Count; i++)
            {
                if (i == faceIndex)
                    continue;
                float d = RTX.TriangleIntersections(orig, dir, faces[i][0], faces[i][1], faces[i][2]);
                if (d > -0 && d < dist)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
