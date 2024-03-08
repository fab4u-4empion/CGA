using System.Collections.Concurrent;
using System.Collections.Generic;
using System;
using System.Numerics;
using System.Threading.Tasks;
using lab1.Shaders;
using System.Threading;
using lab1.Effects;
using Rasterization;
using System.Timers;
using lab1.Shadow;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;

namespace lab1
{
    using Color = (Vector3 Color, float Alpha, float Dissolve);
    using Layer = (int Index, float Z);
    using DPIScale = (double X, double Y);

    public enum ShaderTypes
    {
        MetallicPBR,
        Phong,
        SpecularPBR
    }

    public class Renderer
    {
        public static bool UseTangentNormals = true;
        public static bool UseBloom = false;
        public static float Smoothing = 1f;

        public static Model Sphere;

        public static ShaderTypes CurrentShader = ShaderTypes.MetallicPBR;

        public static Vector3 BackColor = new(0.1f, 0.1f, 0.1f);

        public Buffer<Vector3> BufferHDR;
        public Buffer<SpinLock> Spins;
        public Buffer<int> ViewBuffer;
        public Buffer<float> ZBuffer;

        public Buffer<int> OffsetBuffer;
        public Buffer<byte> CountBuffer;
        public Layer[] LayersBuffer;

        public Camera Camera = new();

        public Pbgra32Bitmap Bitmap;

        private int width;
        private int height;

        private static float PerpDotProduct(Vector2 a, Vector2 b)
        {
            return a.X * b.Y - a.Y * b.X;
        }

        private void TransformCoordinates(Model model)
        {
            Matrix4x4 scaleMatrix = Matrix4x4.CreateScale(model.Scale);
            Matrix4x4 rotationMatrix = Matrix4x4.CreateFromYawPitchRoll(model.Yaw, model.Pitch, model.Roll);
            Matrix4x4 translationMatrix = Matrix4x4.CreateTranslation(model.Translation);
            Matrix4x4 modelMatrix = scaleMatrix * rotationMatrix * translationMatrix;
            Matrix4x4 viewMatrix = Matrix4x4.CreateLookAt(Camera.Position, Camera.Target, Camera.Up);
            Matrix4x4 projectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(Camera.FoV, (float)width / (float)height, 0.1f, 500f);
            Matrix4x4 viewportMatrix = Matrix4x4.CreateViewportLeftHanded(-0.5f, -0.5f, width, height, 0, 1);

            Matrix4x4 matrix = modelMatrix * viewMatrix * projectionMatrix * viewportMatrix;

            model.ViewVertices = new Vector4[model.Positions.Count];
            for (int i = 0; i < model.ViewVertices.Length; i++)
            {
                Vector4 vertex = Vector4.Transform(model.Positions[i], matrix);
                vertex /= new Vector4(new(vertex.W), vertex.W * vertex.W);
                model.ViewVertices[i] = vertex;
            }
        }

        private void Rasterize(List<int> facesIndices, Model model, Action<int, int, float, int> action, BlendModes blendMode)
        {
            Parallel.ForEach(Partitioner.Create(0, facesIndices.Count), (range) =>
            {
                for (int i = range.Item1; i < range.Item2; i++)
                {
                    int index = facesIndices[i] * 3;
                    Vector4 a = model.ViewVertices[model.PositionIndices[index]];
                    Vector4 b = model.ViewVertices[model.PositionIndices[index + 1]];
                    Vector4 c = model.ViewVertices[model.PositionIndices[index + 2]];
                    if ((PerpDotProduct(new(b.X - a.X, b.Y - a.Y), new(c.X - b.X, c.Y - b.Y)) <= 0 || blendMode == BlendModes.AlphaBlending) && 1 / a.W > 0 && 1 / b.W > 0 && 1 / c.W > 0)
                    {
                        if (b.X < a.X)
                            (a, b) = (b, a);

                        if (c.X < a.X)
                            (a, c) = (c, a);

                        if (c.X < b.X)
                            (b, c) = (c, b);

                        Vector4 k1 = (c - a) / (c.X - a.X);
                        Vector4 k2 = (b - a) / (b.X - a.X);
                        Vector4 k3 = (c - b) / (c.X - b.X);

                        int left = int.Max((int)float.Ceiling(a.X), 0);
                        int right = int.Min((int)float.Ceiling(c.X), width);

                        for (int x = left; x < right; x++)
                        {
                            Vector4 p1 = a + (x - a.X) * k1;
                            Vector4 p2 = x < b.X ? a + (x - a.X) * k2 : b + (x - b.X) * k3;

                            if (p1.Y > p2.Y)
                                (p1, p2) = (p2, p1);

                            Vector4 k = (p2 - p1) / (p2.Y - p1.Y);

                            int top = int.Max((int)float.Ceiling(p1.Y), 0);
                            int bottom = int.Min((int)float.Ceiling(p2.Y), height);

                            for (int y = top; y < bottom; y++)
                            {
                                Vector4 p = p1 + (y - p1.Y) * k;
                                if (p.Z >= 0 && p.Z <= 1)
                                    action(x, y, p.Z, facesIndices[i]);
                            }
                        }
                    }
                }
            });
        }

        private Color GetPixelColor(int faceIndex, Vector2 p, Model model)
        {
            int materialIndex = model.MaterialIndices[faceIndex];
            int index = faceIndex * 3;

            Vector4 a = model.ViewVertices[model.PositionIndices[index]];
            Vector4 b = model.ViewVertices[model.PositionIndices[index + 1]];
            Vector4 c = model.ViewVertices[model.PositionIndices[index + 2]];

            Vector2 pa = new Vector2(a.X, a.Y) - p;
            Vector2 pb = new Vector2(b.X, b.Y) - p;
            Vector2 pc = new Vector2(c.X, c.Y) - p;

            float u = PerpDotProduct(pc, pb) * a.W;
            float v = PerpDotProduct(pa, pc) * b.W;
            float w = PerpDotProduct(pb, pa) * c.W;
            float sum = u + v + w;

            float dudx = (pc.Y - pb.Y) * a.W * 0.5f;
            float dvdx = (pa.Y - pc.Y) * b.W * 0.5f;
            float dwdx = (pb.Y - pa.Y) * c.W * 0.5f;

            float dudy = (pb.X - pc.X) * a.W * 0.5f;
            float dvdy = (pc.X - pa.X) * b.W * 0.5f;
            float dwdy = (pa.X - pb.X) * c.W * 0.5f;

            (float u1, float v1, float w1) = (u - dudx, v - dvdx, w - dwdx);
            (float u2, float v2, float w2) = (u + dudy, v + dvdy, w + dwdy);
            (float u3, float v3, float w3) = (u + dudx, v + dvdx, w + dwdx);
            (float u4, float v4, float w4) = (u - dudy, v - dvdy, w - dwdy);

            Vector3 n1 = model.Normals[model.NormalIndices[index]];
            Vector3 n2 = model.Normals[model.NormalIndices[index + 1]];
            Vector3 n3 = model.Normals[model.NormalIndices[index + 2]];

            Vector2 uv_1 = model.UV[model.UVIndices[index]];
            Vector2 uv_2 = model.UV[model.UVIndices[index + 1]];
            Vector2 uv_3 = model.UV[model.UVIndices[index + 2]];

            Vector3 aw = model.Positions[model.PositionIndices[index]];
            Vector3 bw = model.Positions[model.PositionIndices[index + 1]];
            Vector3 cw = model.Positions[model.PositionIndices[index + 2]];

            Vector3 t1 = model.Tangents[model.TangentIndices[index]];
            Vector3 t2 = model.Tangents[model.TangentIndices[index + 1]];
            Vector3 t3 = model.Tangents[model.TangentIndices[index + 2]];

            Vector2 uv = (u * uv_1 + v * uv_2 + w * uv_3) / sum;
            Vector2 uv1 = (u1 * uv_1 + v1 * uv_2 + w1 * uv_3) / (u1 + v1 + w1);
            Vector2 uv2 = (u2 * uv_1 + v2 * uv_2 + w2 * uv_3) / (u2 + v2 + w2);
            Vector2 uv3 = (u3 * uv_1 + v3 * uv_2 + w3 * uv_3) / (u3 + v3 + w3);
            Vector2 uv4 = (u4 * uv_1 + v4 * uv_2 + w4 * uv_3) / (u4 + v4 + w4);

            Vector3 oN = (u * n1 + v * n2 + w * n3) / sum;
            Vector3 pw = (u * aw + v * bw + w * cw) / sum;

            Vector3 T = (u * t1 + v * t2 + w * t3) / sum;
            Vector3 B = Vector3.Cross(oN, T) * model.Signs[faceIndex];

            Vector3 baseColor = model.Materials[materialIndex].GetDiffuse(uv, uv1, uv2, uv3, uv4);
            Vector3 emission = model.Materials[materialIndex].GetEmission(uv, uv1, uv2, uv3, uv4);
            float opacity = 1 - model.Materials[materialIndex].GetTransmission(uv, uv1, uv2, uv3, uv4);
            float dissolve = model.Materials[materialIndex].GetDissolve(uv, uv1, uv2, uv3, uv4);
            Vector3 MRAO = model.Materials[materialIndex].GetMRAO(uv, uv1, uv2, uv3, uv4);
            Vector3 specular = model.Materials[materialIndex].GetSpecular(uv, uv1, uv2, uv3, uv4);

            Vector3 n, nc;
            float clearCoatRougness, clearCoat;

            if (UseTangentNormals)
            {
                n = model.Materials[materialIndex].GetNormal(uv, Vector3.UnitZ, uv1, uv2, uv3, uv4);
                n = T * n.X + B * n.Y + oN * n.Z;

                (clearCoatRougness, clearCoat, nc) = model.Materials[materialIndex].GetClearCoat(uv, Vector3.UnitZ, uv1, uv2, uv3, uv4);
                nc = T * nc.X + B * nc.Y + oN * nc.Z;
            }
            else
            {
                n = model.Materials[materialIndex].GetNormal(uv, oN, uv1, uv2, uv3, uv4);
                (clearCoatRougness, clearCoat, nc) = model.Materials[materialIndex].GetClearCoat(uv, oN, uv1, uv2, uv3, uv4);
            }

            Vector3 color = new(0.5f);

            switch (CurrentShader)
            {
                case ShaderTypes.MetallicPBR:
                    color = PBR.GetPixelColorMetallic(baseColor, MRAO.X, MRAO.Y, MRAO.Z, opacity, dissolve, emission, n, nc, clearCoat, clearCoatRougness, Camera.Position, pw, faceIndex);
                    break;

                case ShaderTypes.Phong:
                    color = Phong.GetPixelColor(baseColor, n, specular, Camera.Position, pw, faceIndex, emission, opacity, dissolve, MRAO.Z, 1f - MRAO.Y);
                    break;

                case ShaderTypes.SpecularPBR:
                    color = PBR.GetPixelColorSpecular(baseColor, specular, 1 - MRAO.Y, MRAO.Z, opacity, dissolve, emission, n, nc, clearCoat, clearCoatRougness, Camera.Position, pw, faceIndex);
                    break;
            }

            return (color, opacity, dissolve);
        }

        private void DrawPixelIntoViewBuffer(int x, int y, float z, int index)
        {
            bool gotLock = false;
            Spins[x, y].Enter(ref gotLock);

            if (z < ZBuffer[x, y])
            {
                ViewBuffer[x, y] = index;
                ZBuffer[x, y] = z;
            }

            Spins[x, y].Exit(false);
        }

        private void IncDepth(int x, int y, float z, int index)
        {
            if (z < ZBuffer[x, y])
                Interlocked.Increment(ref OffsetBuffer[x, y]);
        }

        private void DrawPixelIntoLayers(int x, int y, float z, int index)
        {
            if (z < ZBuffer[x, y])
            {
                bool gotLock = false;
                Spins[x, y].Enter(ref gotLock);

                LayersBuffer[OffsetBuffer[x, y] + CountBuffer[x, y]] = (index, z);
                CountBuffer[x, y] += 1;

                Spins[x, y].Exit(false);
            }
        }

        private Vector3 GetResultColor(int start, int length, int x, int y, Model model)
        {
            float key;
            Layer layer;
            int j;
            for (int i = 1 + start; i < length + start; i++)
            {
                key = LayersBuffer[i].Z;
                layer = LayersBuffer[i];
                j = i - 1;

                while (j >= start && LayersBuffer[j].Z > key)
                {
                    LayersBuffer[j + 1] = LayersBuffer[j];
                    j--;
                }
                LayersBuffer[j + 1] = layer;
            }

            Vector3 color = Vector3.Zero;
            float alpha = 0;

            for (int i = start; i < length + start; i++)
            {
                Color pixel = GetPixelColor(LayersBuffer[i].Index, new(x, y), model);

                color += (1 - alpha) * pixel.Color;
                alpha += (1 - alpha) * pixel.Alpha * pixel.Dissolve;

                if (pixel.Alpha == 1 && pixel.Dissolve == 1)
                    break;
            }

            color += (1 - alpha) * BufferHDR[x, y];

            return color;
        }

        private void DrawViewBuffer(Model model)
        {
            Parallel.For(0, width, (x) =>
            {
                for (int y = 0; y < height; y++)
                {
                    if (ViewBuffer[x, y] != -1)
                    {
                        Color color = GetPixelColor(ViewBuffer[x, y], new(x, y), model);
                        BufferHDR[x, y] = color.Color;
                    }
                }
            });
        }

        private void DrawLayers(Model model)
        {
            Parallel.For(0, width, (x) =>
            {
                for (int y = 0; y < height; y++)
                {
                    if (CountBuffer[x, y] > 0)
                        BufferHDR[x, y] = GetResultColor(OffsetBuffer[x, y], CountBuffer[x, y], x, y, model);
                }
            });
        }

        private void DrawHDRBuffer()
        {
            if (UseBloom)
            {
                Buffer<Vector3> bloomBuffer = Bloom.GetBoolmBuffer(BufferHDR, width, height, 1);
                Parallel.For(0, width, (x) =>
                {
                    for (int y = 0; y < height; y++)
                    {
                        Bitmap.SetPixel(x, y, ToneMapping.CompressColor(BufferHDR[x, y] + bloomBuffer[x, y]));
                        BufferHDR[x, y] = BackColor;
                    }
                });
            }
            else
            {
                Parallel.For(0, width, (x) =>
                {
                    for (int y = 0; y < height; y++)
                    {
                        Bitmap.SetPixel(x, y, ToneMapping.CompressColor(BufferHDR[x, y]));
                        BufferHDR[x, y] = BackColor;
                    }
                });
            }
        }

        private void DrawLights()
        {
            if (LightingConfig.DrawLights)
            {
                for (int i = 0; i < LightingConfig.Lights.Count; i++)
                {
                    Lamp lamp = LightingConfig.Lights[i];

                    Sphere.Translation = lamp.Position;
                    Sphere.Scale = float.Max(0.05f, RTX.LightSize);

                    TransformCoordinates(Sphere);

                    Rasterize(Sphere.OpaqueFacesIndices, Sphere, (int x, int y, float z, int unused) =>
                    {
                        bool gotLock = false;
                        Spins[x, y].Enter(ref gotLock);

                        if (z < ZBuffer[x, y])
                        {
                            BufferHDR[x, y] = lamp.Color * (lamp.Intensity + 1f);
                            ZBuffer[x, y] = z;
                        }

                        Spins[x, y].Exit(false);
                    }, BlendModes.Opaque);
                }
            }
        }

        private void DrawScene(Model model)
        {
            TransformCoordinates(model);

            Rasterize(model.OpaqueFacesIndices, model, DrawPixelIntoViewBuffer, BlendModes.Opaque);

            DrawViewBuffer(model);

            DrawLights();

            if (model.TransparentFacesIndices.Count > 0)
            {
                Rasterize(model.TransparentFacesIndices, model, IncDepth, BlendModes.AlphaBlending);

                int prefixSum = 0;
                int depth = 0;

                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        depth = OffsetBuffer[x, y];
                        OffsetBuffer[x, y] = prefixSum;
                        prefixSum += depth;
                    }
                }

                LayersBuffer = new Layer[prefixSum];

                Rasterize(model.TransparentFacesIndices, model, DrawPixelIntoLayers, BlendModes.AlphaBlending);

                DrawLayers(model);
            }
        }

        public void Draw(Model model)
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    ZBuffer[x, y] = float.MaxValue;
                    ViewBuffer[x, y] = -1;
                    CountBuffer[x, y] = 0;
                    OffsetBuffer[x, y] = 0;
                }
            }

            if (model != null)
                DrawScene(model);
            else
                DrawLights();

            Bitmap.Source.Lock();
            Bitmap.Clear();

            DrawHDRBuffer();

            Bitmap.Source.AddDirtyRect(new(0, 0, width, height));
            Bitmap.Source.Unlock();
        }

        public void CreateBuffers(double width, double height, DPIScale scale)
        {
            Bitmap = new((int)(width * Smoothing * scale.X), (int)(height * Smoothing * scale.Y));

            this.width = Bitmap.PixelWidth;
            this.height = Bitmap.PixelHeight;

            BufferHDR = new(this.width, this.height);
            
            Spins = new(this.width, this.height);
            ViewBuffer = new(this.width, this.height);
            CountBuffer = new(this.width, this.height);
            OffsetBuffer = new(this.width, this.height);
            for (int i = 0; i < this.width; i++)
            {
                for (int j = 0; j < this.height; j++)
                {
                    Spins[i, j] = new(false);
                    BufferHDR[i, j] = BackColor;
                }
            }
            ZBuffer = new(this.width, this.height);
        }

    }
}
