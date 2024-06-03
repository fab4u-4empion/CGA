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
using System.Windows;
using System.Buffers;

namespace lab1
{
    using Color = (Vector3 Color, float Alpha, float Dissolve);
    using Layer = (int Index, float Z);
    using DPIScale = (double X, double Y);

    public enum ShaderTypes
    {
        MetallicPBR,
        Phong,
        SpecularPBR,
        Toon
    }

    public class Renderer
    {
        public static bool UseTangentNormals = true;
        public static bool UseBloom = false;
        public static float Smoothing = 1f;

        public static Model Sphere;

        public static ShaderTypes CurrentShader = ShaderTypes.MetallicPBR;
        public static bool UseSkyBox = true;

        public static Vector3 BackColor = new(0.15f, 0.15f, 0.15f);

        public Buffer<Vector3> BufferHDR;
        public Buffer<float> AlphaBuffer;
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

        private Matrix4x4 viewportMatrix;

        Vector3 p0, dpdx, dpdy;

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

            Matrix4x4 matrix = modelMatrix * viewMatrix * projectionMatrix;

            for (int i = 0; i < model.ProjectionVertices.Length; i++)
            {
                model.ProjectionVertices[i] = Vector4.Transform(model.Positions[i], matrix);
            }
        }

        private void Rasterize(List<int> facesIndices, Model model, Action<int, int, float, int> action, BlendModes blendMode)
        {
            Parallel.ForEach(Partitioner.Create(0, facesIndices.Count), (range) =>
            {
                Vector4[] result = ArrayPool<Vector4>.Shared.Rent(4);

                for (int i = range.Item1; i < range.Item2; i++)
                {
                    int index = facesIndices[i] * 3;
                    byte count = 0;

                    Vector4 v1 = model.ProjectionVertices[model.PositionIndices[index]];
                    Vector4 v2 = model.ProjectionVertices[model.PositionIndices[index + 1]];
                    Vector4 v3 = model.ProjectionVertices[model.PositionIndices[index + 2]];

                    if (v1.Z >= 0)
                    result[count++] = v1;

                    if (v1.Z < 0 != v2.Z < 0)
                    {
                        float t = -v1.Z / (v2.Z - v1.Z);
                        result[count++] = Vector4.Lerp(v1, v2, t);
                    }

                    if (v2.Z >= 0)
                        result[count++] = v2;

                    if (v2.Z < 0 != v3.Z < 0)
                    {
                        float t = -v2.Z / (v3.Z - v2.Z);
                        result[count++] = Vector4.Lerp(v2, v3, t);
                    }

                    if (v3.Z >= 0)
                        result[count++] = v3;
                    
                    if (v1.Z < 0 != v3.Z < 0)
                    {
                        float t = -v1.Z / (v3.Z - v1.Z);
                        result[count++] = Vector4.Lerp(v1, v3, t);
                    }

                    for (int j = 0; j < count; j++)
                        result[j] = Vector4.Transform(result[j] / result[j].W, viewportMatrix);

                    for (int j = 1; j < count - 1; j++)
                    {
                        Vector4 a = result[0];
                        Vector4 b = result[j];
                        Vector4 c = result[j + 1];

                        if (PerpDotProduct(new(b.X - a.X, b.Y - a.Y), new(c.X - b.X, c.Y - b.Y)) <= 0 || blendMode == BlendModes.AlphaBlending)
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
                }

                ArrayPool<Vector4>.Shared.Return(result);
            });
        }

        private Color GetPixelColor(int faceIndex, int x, int y, Model model)
        {
            int materialIndex = model.MaterialIndices[faceIndex];
            int index = faceIndex * 3;

            Vector3 aw = model.Positions[model.PositionIndices[index]];
            Vector3 bw = model.Positions[model.PositionIndices[index + 1]];
            Vector3 cw = model.Positions[model.PositionIndices[index + 2]];

            Vector3 D1 = p0 + x * dpdx + y * dpdy;
            Vector3 D2 = D1 + dpdx;
            Vector3 D3 = D1 + dpdy; 

            Vector3 tvec = Camera.Position - cw;

            Vector3 e1 = aw - cw;
            Vector3 e2 = bw - cw;

            Vector3 cross1 = Vector3.Cross(e2, e1);
            Vector3 cross2 = Vector3.Cross(e2, tvec);
            Vector3 cross3 = Vector3.Cross(tvec, e1);

            float det1 = 1 / Vector3.Dot(D1, cross1);
            float det2 = 1 / Vector3.Dot(D2, cross1);
            float det3 = 1 / Vector3.Dot(D3, cross1);

            float u = Vector3.Dot(D1, cross2) * det1;
            float u1 = Vector3.Dot(D2, cross2) * det2;
            float u2 = Vector3.Dot(D3, cross2) * det3;

            float v = Vector3.Dot(cross3, D1) * det1;
            float v1 = Vector3.Dot(cross3, D2) * det2;
            float v2 = Vector3.Dot(cross3, D3) * det3;

            float w = 1f - u - v;
            float w1 = 1f - u1 - v1;
            float w2 = 1f - u2 - v2;

            bool isBackFace = det1 <= 0;

            Vector3 n1 = model.Normals[model.NormalIndices[index]];
            Vector3 n2 = model.Normals[model.NormalIndices[index + 1]];
            Vector3 n3 = model.Normals[model.NormalIndices[index + 2]];

            Vector2 uv_1 = model.UV[model.UVIndices[index]];
            Vector2 uv_2 = model.UV[model.UVIndices[index + 1]];
            Vector2 uv_3 = model.UV[model.UVIndices[index + 2]];

            Vector3 t1 = model.Tangents[model.TangentIndices[index]];
            Vector3 t2 = model.Tangents[model.TangentIndices[index + 1]];
            Vector3 t3 = model.Tangents[model.TangentIndices[index + 2]];

            Vector2 uv = u * uv_1 + v * uv_2 + w * uv_3;
            Vector2 uv1 = u1 * uv_1 + v1 * uv_2 + w1 * uv_3;
            Vector2 uv2 = u2 * uv_1 + v2 * uv_2 + w2 * uv_3;

            Vector3 oN = u * n1 + v * n2 + w * n3;
            Vector3 pw = u * aw + v * bw + w * cw;

            Vector3 T = (u * t1 + v * t2 + w * t3);
            Vector3 B = Vector3.Cross(oN, T) * model.Signs[faceIndex];

            float dissolve = model.Materials[materialIndex].GetDissolve(uv, uv1, uv2);

            if (dissolve == 0)
                return (Vector3.Zero, 0, 0);

            Vector3 baseColor = model.Materials[materialIndex].GetDiffuse(uv, uv1, uv2);
            Vector3 emission = model.Materials[materialIndex].GetEmission(uv, uv1, uv2);
            float opacity = 1 - model.Materials[materialIndex].GetTransmission(uv, uv1, uv2);
            Vector3 MRAO = model.Materials[materialIndex].GetMRAO(uv, uv1, uv2);
            Vector3 specular = model.Materials[materialIndex].GetSpecular(uv, uv1, uv2);

            Vector3 n, nc;
            float clearCoatRougness, clearCoat;

            if (UseTangentNormals)
            {
                n = model.Materials[materialIndex].GetNormal(uv, Vector3.UnitZ, uv1, uv2);
                n = T * n.X + B * n.Y + oN * n.Z;

                (clearCoatRougness, clearCoat, nc) = model.Materials[materialIndex].GetClearCoat(uv, Vector3.UnitZ, uv1, uv2);
                nc = T * nc.X + B * nc.Y + oN * nc.Z;
            }
            else
            {
                n = model.Materials[materialIndex].GetNormal(uv, oN, uv1, uv2);
                (clearCoatRougness, clearCoat, nc) = model.Materials[materialIndex].GetClearCoat(uv, oN, uv1, uv2);
            }

            if (isBackFace)
                (n, nc) = (-n, -nc);

            Vector3 color = Vector3.One;

            switch (CurrentShader)
            {
                case ShaderTypes.MetallicPBR:
                    color = PBR.GetPixelColorMetallic(baseColor, MRAO.X, MRAO.Y, MRAO.Z, opacity, dissolve, emission, n, nc, clearCoat, clearCoatRougness, Camera.Position, pw);
                    break;

                case ShaderTypes.Phong:
                    color = Phong.GetPixelColor(baseColor, n, specular, Camera.Position, pw, emission, opacity, dissolve, MRAO.Z, 1f - MRAO.Y);
                    break;

                case ShaderTypes.SpecularPBR:
                    color = PBR.GetPixelColorSpecular(baseColor, specular, 1 - MRAO.Y, MRAO.Z, opacity, dissolve, emission, n, nc, clearCoat, clearCoatRougness, Camera.Position, pw);
                    break;

                case ShaderTypes.Toon:
                    color = Toon.GetPixelColor(baseColor, n, pw, Camera.Position, emission);
                    int d = (int)float.Ceiling(2 * Smoothing);
                    for (int i = -d; i <= d; i++)
                        for (int j = -d; j <= d; j++)
                            if (
                                ViewBuffer[int.Clamp(x + i, 0, ViewBuffer.Width - 1), int.Clamp(y + j, 0, ViewBuffer.Height - 1)] == -1
                                &&
                                CountBuffer[int.Clamp(x + i, 0, ViewBuffer.Width - 1), int.Clamp(y + j, 0, ViewBuffer.Height - 1)] == 0
                            )
                            {
                                color = Vector3.One;
                                break;
                            }
                    opacity = 1;
                    dissolve = 1;
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

        private Color GetResultColor(int start, int length, int x, int y, Model model)
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
                Color pixel = GetPixelColor(LayersBuffer[i].Index, x, y, model);

                if (pixel.Dissolve == 0) continue;

                color += (1 - alpha) * pixel.Color;
                alpha += (1 - alpha) * pixel.Alpha * pixel.Dissolve;

                if (pixel.Alpha == 1 && pixel.Dissolve == 1)
                    return (color, 1f, 1f);
            }

            return (color, alpha, 1f);
        }

        private void DrawViewBuffer(Model model)
        {
            Parallel.For(0, width, (x) =>
            {
                for (int y = 0; y < height; y++)
                {
                    if (ViewBuffer[x, y] != -1)
                    {
                        Color color = GetPixelColor(ViewBuffer[x, y], x, y, model);
                        BufferHDR[x, y] = color.Color;
                        AlphaBuffer[x, y] = color.Alpha;
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
                    Color color = (Vector3.Zero, 0f, 0f);

                    if (CountBuffer[x, y] > 0)
                    {
                        color = GetResultColor(OffsetBuffer[x, y], CountBuffer[x, y], x, y, model);
                    }

                    if (color.Alpha == 1f)
                    {
                        BufferHDR[x, y] = color.Color;
                        AlphaBuffer[x, y] = 1f;
                        continue;
                    }

                    int index = ViewBuffer[x, y];
                    if (index != -1)
                    {
                        Color pixel = GetPixelColor(index, x, y, model);
                        BufferHDR[x, y] = pixel.Color;
                        AlphaBuffer[x, y] = pixel.Alpha;
                    }

                    if (AlphaBuffer[x, y] == 1f)
                    {
                        BufferHDR[x, y] = BufferHDR[x, y] * (1f - color.Alpha) + color.Color;
                        continue;
                    }

                    BufferHDR[x, y] = color.Color;
                    AlphaBuffer[x, y] = color.Alpha;
                }
            });
        }

        private void DrawHDRBuffer()
        {
            if (UseBloom)
            {
                Buffer<Vector3> bloomBuffer = Bloom.GetBoolmBuffer(BufferHDR, width, height, Smoothing);
                Parallel.For(0, width, (x) =>
                {
                    for (int y = 0; y < height; y++)
                    {
                        Vector3 backColor = BackColor;
                        if (UseSkyBox && LightingConfig.SkyBox != null)
                        {
                            Vector3 p = p0 + dpdx * x + dpdy * y;
                            backColor = LightingConfig.SkyBox.GetColor(Vector3.Normalize(p));
                            backColor *= LightingConfig.AmbientIntensity;
                        }

                        Bitmap.SetPixel(x, y, ToneMapping.CompressColor(BufferHDR[x, y] + backColor * (1f - AlphaBuffer[x, y]) + bloomBuffer[x, y]));
                    }
                });
            }
            else
            {
                Parallel.For(0, width, (x) =>
                {
                    for (int y = 0; y < height; y++)
                    {
                        Vector3 backColor = BackColor;
                        if (UseSkyBox && LightingConfig.SkyBox != null)
                        {
                            Vector3 p = p0 + dpdx * x + dpdy * y;
                            backColor = LightingConfig.SkyBox.GetColor(Vector3.Normalize(p));
                            backColor *= LightingConfig.AmbientIntensity;
                        }

                        Bitmap.SetPixel(x, y, ToneMapping.CompressColor(BufferHDR[x, y] + backColor * (1f - AlphaBuffer[x, y])));
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
                            ViewBuffer[x, y] = -1;
                            AlphaBuffer[x, y] = 1;
                        }

                        Spins[x, y].Exit(false);
                    }, BlendModes.Opaque);
                }
            }
        }

        private void DrawScene(Model model)
        {
            TransformCoordinates(model);

            if (model.OpaqueFacesIndices.Count > 0)
                Rasterize(model.OpaqueFacesIndices, model, DrawPixelIntoViewBuffer, BlendModes.Opaque);

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

                if (prefixSum > 0)
                {
                    LayersBuffer = new Layer[prefixSum];

                    Rasterize(model.TransparentFacesIndices, model, DrawPixelIntoLayers, BlendModes.AlphaBlending);

                    DrawLayers(model);
                }
                else
                {
                    DrawViewBuffer(model);
                }
            }
            else
            {
                DrawViewBuffer(model);
            }
        }

        private (Vector3, Vector3, Vector3) GetViewportToWorldParams(float tanParam)
        {
            float aspect = (float)width / height;
            float tan = float.Tan(tanParam);
            Matrix4x4 cameraRotation = Matrix4x4.CreateRotationX(Camera.Pitch) * Matrix4x4.CreateRotationY(Camera.Yaw);

            Vector3 X = new Vector3(cameraRotation.M11, cameraRotation.M12, cameraRotation.M13) * tan * aspect;
            Vector3 Y = new Vector3(cameraRotation.M21, cameraRotation.M22, cameraRotation.M23) * tan;
            Vector3 Z = new(cameraRotation.M31, cameraRotation.M32, cameraRotation.M33);
            Vector3 p0 = (1f / width - 1) * X + (-1f / height + 1) * Y - Z;
            Vector3 dpdx = X * 2 / width;
            Vector3 dpdy = Y * -2 / height;

            return (p0, dpdx, dpdy);
        }

        public void Draw(Model model)
        {
            Array.Fill(ZBuffer.Array, float.MaxValue);
            Array.Fill(ViewBuffer.Array, -1);
            Array.Fill(CountBuffer.Array, (byte)0);
            Array.Fill(OffsetBuffer.Array, 0);
            Array.Fill(BufferHDR.Array, Vector3.Zero);
            Array.Fill(AlphaBuffer.Array, 0);

            (p0, dpdx, dpdy) = GetViewportToWorldParams(Camera.FoV / 2);

            if (model != null)
                DrawScene(model);
            else
                DrawLights();

            (p0, dpdx, dpdy) = GetViewportToWorldParams(float.Pi / 4);

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

            viewportMatrix = Matrix4x4.CreateViewportLeftHanded(-0.5f, -0.5f, this.width, this.height, 0, 1);

            BufferHDR = new(this.width, this.height);
            AlphaBuffer = new(this.width, this.height);
            
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
