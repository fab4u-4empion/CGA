using Rasterization;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Diagnostics;

namespace lab1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Pbgra32Bitmap bitmap;

        object locker = new object();
        Model model = new();
        Camera camera = new();
        ZBuffer ZBuffer = new(1, 2);
        Vector3 Light = Vector3.Normalize(new(5, 10, 15));

        Point mouse_position;
        
        public MainWindow()
        {
            InitializeComponent();
        }

        private void ParseModelFromFile(string path)
        {
            using (StreamReader reader = new(path))
            {
                while (!reader.EndOfStream)
                {
                    String line = reader.ReadLine().Trim();

                    if (line.StartsWith("v "))
                    {
                        List<float> coordinates = line
                            .Remove(0, 2)
                            .Split(" ")
                            .Select(c =>
                                float.Parse(c, CultureInfo.InvariantCulture)
                             )
                            .ToList();
                        model.AddVertex(coordinates[0], coordinates[1], coordinates[2]);
                    }

                    if (line.StartsWith("f "))
                    {
                        List<Vector3> vertices = line
                                .Remove(0, 2)
                                .Split(" ")
                                .Select(v =>
                                {
                                    List<int> indexes = v
                                        .Split("/")
                                        .Select(i => int.Parse(i, CultureInfo.InvariantCulture))
                                        .ToList();
                                    return new Vector3(indexes[0], indexes[1], indexes[2]);
                                }
                                )
                                .ToList();
                        for (int i = 0; i < vertices.Count - 2;  i++)
                        {
                            model.AddFace(new() {
                                vertices[0],
                                vertices[i + 1],
                                vertices[i + 2]
                            });
                        }
                    }

                    if (line.StartsWith("vn "))
                    {
                        List<float> coordinates = line
                            .Remove(0, 3)
                            .Split(" ")
                            .Select(c =>
                                float.Parse(c, CultureInfo.InvariantCulture)
                             )
                            .ToList();
                        model.AddNormal(coordinates[0], coordinates[1], coordinates[2]);
                    }
                }
            }
        }

        private Vector4[] TransformCoordinates()
        {
            Matrix4x4 scaleMatrix = Matrix4x4.CreateScale(model.Scale);
            Matrix4x4 rotationMatrix = Matrix4x4.CreateFromYawPitchRoll(model.Yaw, model.Pitch, model.Roll);
            Matrix4x4 translationMatrix = Matrix4x4.CreateTranslation(model.Translation);
            Matrix4x4 modelMatrix = scaleMatrix * rotationMatrix * translationMatrix;
            Matrix4x4 viewMatrix = Matrix4x4.CreateLookAt(camera.Position, camera.Target, camera.Up);
            Matrix4x4 projectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(camera.FoV, (float)bitmap.PixelWidth / (float)bitmap.PixelHeight, 0.1f, 1000);
            Matrix4x4 modelViewProjectionMatrix = modelMatrix * viewMatrix * projectionMatrix;
            Matrix4x4 viewportMatrix = Matrix4x4.CreateViewportLeftHanded(0, 0, bitmap.PixelWidth, bitmap.PixelHeight, 0, 1);

            Vector4[] windowVertices = new Vector4[model.Vertices.Count];
            for (int i = 0; i < windowVertices.Length; i++)
            {
                windowVertices[i] = Vector4.Transform(model.Vertices[i], modelViewProjectionMatrix);
                windowVertices[i] /= windowVertices[i].W;
                windowVertices[i] = Vector4.Transform(windowVertices[i], viewportMatrix);
            }

            return windowVertices;
        }

        private void ClearBitmap()
        {
            for (int i = 0; i < bitmap.PixelWidth; i++)
            {
                for (int j = 0; j < bitmap.PixelHeight; j++) {
                    bitmap.ClearPixel(i, j);
                }
            }
        }

        private Vector3 GetColor(Vector3 normal, Vector3 color)
        {
            float c = Math.Max(Vector3.Dot(normal, Light), 0);
            return Vector3.Multiply(color, c);
        }

        public static Vector3 GetAverageColor(Vector3 color1, Vector3 color2, Vector3 color3)
        {
            float sumR = color1.X + color2.X + color3.X;
            float sumG = color1.Y + color2.Y + color3.Y;
            float sumB = color1.Z + color2.Z + color3.Z;

            return Vector3.Divide(new(sumR, sumG, sumB), 3);
        }

        private Vector3 GetFaceColor(List<Vector3> face, Vector3 color)
        {
            Vector3 normal1 = model.Normals[(int)face[0].Z - 1];
            Vector3 normal2 = model.Normals[(int)face[1].Z - 1];
            Vector3 normal3 = model.Normals[(int)face[2].Z - 1];

            Vector3 color1 = GetColor(normal1, color);
            Vector3 color2 = GetColor(normal2, color);
            Vector3 color3 = GetColor(normal3, color);

            return GetAverageColor(color1, color2, color3);
        }

        private Vector3 GetNormal(List<Vector3> face, Vector4[] vertices)
        {
            Vector4 v1 = vertices[(int)face[0].X - 1];
            Vector4 v2 = vertices[(int)face[1].X - 1];
            Vector4 v3 = vertices[(int)face[2].X - 1];

            Vector3 s1 = new(v2.X - v1.X, v2.Y - v1.Y, v2.Z - v1.Z);
            Vector3 s2 = new(v3.X - v2.X, v3.Y - v2.Y, v3.Z - v2.Z);
            return Vector3.Normalize(Vector3.Cross(s1, s2));
        }

        private void DrawLine(Pixel a, Pixel b, Vector3 color, List<Pixel> sides = null)
        {
            // разница координат начальной и конечной точек
            int dx = Math.Abs(b.X - a.X);
            int dy = Math.Abs(b.Y - a.Y);
            float dz = Math.Abs(b.Z - a.Z);

            // учитываем квадрант
            int signX = a.X < b.X ? 1 : -1;
            int signY = a.Y < b.Y ? 1 : -1;
            float signZ = a.Z < b.Z ? 1 : -1;

            //float curZ = a.Z;  // текущее z
            float deltaZ = dz / dy;  // при изменении y будем менять z

            int err = dx - dy;   // ошибка

            // пока не достигнем конца
           
            while (a.X != b.X || a.Y != b.Y)
            {
                DrawPixel(a.X, a.Y, a.Z, color);
                sides.Add(a.Copy());

                int err2 = err * 2;      // модифицированное значение ошибки

                if (err2 > -dy)
                {
                    a.X += signX;
                    err -= dy;           // корректируем ошибку
                }

                if (err2 < dx)
                {
                    a.Y += signY;            // изменяем y на единицу
                    a.Z += signZ * deltaZ;  // меняем z
                    err += dx;               // корректируем ошибку   
                }
            }

            // отрисовывем последний пиксель
            DrawPixel(b.X, b.Y, b.Z, color);
            sides.Add(b.Copy());
        }

        private void FillFace(List<Pixel> sidesPixels, Vector3 color) // список всех точек ребер грани
        {
            (int? minY, int? maxY) = GetMinMaxY(sidesPixels);
            if (minY is null || maxY is null)
            {
                return;
            }

            for (int y = (int)minY; y < maxY; y++)      // по очереди отрисовываем линии для каждой y-координаты
            {
                (Pixel? startPixel, Pixel? endPixel) = GetStartEndXForY(sidesPixels, y);
                if (startPixel is null || endPixel is null)
                {
                    continue;
                }

                Pixel start = startPixel;
                Pixel end = endPixel;

                float z = start.Z;                                       // в какую сторону приращение z
                float dz = (end.Z - start.Z) / Math.Abs(end.X - start.X);  // z += dz при изменении x

                // отрисовываем линию
                for (int x = start.X; x < end.X; x++, z += dz)
                {
                    DrawPixel(x, y, z, color);
                }
            }
        }

        // Сортируем точки по Y-координате и находим min & max
        private static (int? min, int? max) GetMinMaxY(List<Pixel> pixels)
        {
            List<Pixel> sorted = pixels.OrderBy(x => x.Y).ToList();
            return sorted.Count == 0 ? (min: null, max: null) : (min: sorted.First().Y, max: sorted.Last().Y);
        }

        // Находим стартовый и конечный X для определенного Y 
        private static (Pixel? start, Pixel? end) GetStartEndXForY(List<Pixel> pixels, int y)
        {
            // Фильтруем пиксели с нужным Y и сортируем по X
            List<Pixel> filtered = pixels.Where(pixel => pixel.Y == y).OrderBy(pixel => pixel.X).ToList();
            return filtered.Count == 0 ? (start: null, end: null) : (start: filtered.First(), end: filtered.Last());
        }

        private void DrawFace(List<Vector3> face, List<Vector4> vertices)
        {
           
            List<Pixel> sides = new();

            //Vector3 color = GetFaceColor(face, new(0.5f, 0.5f, 0.5f));
            Vector3 color = Vector3.Zero;

                for (int i = 0; i < face.Count - 1; i++)
                {
                    DrawLine(
                        new(vertices[i]),
                        new(vertices[i + 1]),
                        color,
                        sides
                    );
                }


                DrawLine(
                    new(vertices[0]),
                    new(vertices[^1]),
                    color,
                    sides
                );

            

         
            //FillFace(sides, color);
            
            
        }

        private void DrawPixel(int x, int y, float z, Vector3 color)
        {
            //&& z > 0 && z < 1 && z <= ZBuffer[x, y]

            if (x >= 0 && y >= 0 && x < bitmap.PixelWidth && y < bitmap.PixelHeight)
            {
                //bitmap.SetPixel(x, y, color);
                //ZBuffer[x, y] = z;
            }
        }

        private void Draw()
        {
            ClearBitmap();
            DateTime t = DateTime.Now;
            Vector4[] vertices = TransformCoordinates();
            //ZBuffer = new(bitmap.PixelWidth, bitmap.PixelHeight);

            bitmap.Source.Lock();

            
            

            List<Task> tasks = new List<Task>();

            foreach (List<Vector3> face in model.Faces)
            {
                Vector3 normal = GetNormal(face, vertices);
                if (normal.Z < 0)
                {
                    List<Vector4> faceVertices = new List<Vector4>()
                    {
                        vertices[(int)face[0].X - 1],
                        vertices[(int)face[1].X - 1],
                        vertices[(int)face[2].X - 1]
                    };
                    tasks.Add(
                        Task
                            .Run(() =>
                                {
                                    DrawFace(face, faceVertices);
                                }
                            ));
                }
            }
            tasks.ForEach(task => task.Wait());
            bitmap.Source.AddDirtyRect(new(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));
            bitmap.Source.Unlock();
            Time.Content = (DateTime.Now - t).Milliseconds.ToString();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

            bitmap = new((int)Grid.ActualWidth, (int)Grid.ActualHeight);
            Canvas.Source = bitmap.Source;
            /*model.Translation = new(0, -10, 0);
            ParseModelFromFile("./model/tree.obj");*/
            ParseModelFromFile("./model/shovel_low.obj");
            model.Translation = new(0, -6, -3);
            /*model.Translation = new(-54, -54, 0);
            ParseModelFromFile("./model/cube.obj");*/
            Draw();
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point current_position = e.GetPosition(this);
                camera.UpdatePosition(0, 0, (float)(current_position.X - mouse_position.X) * -0.5f);
                camera.UpdatePosition(0, (float)(current_position.Y - mouse_position.Y) * -0.5f, 0);
                Draw();
            }
            mouse_position = e.GetPosition(this);
            
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            mouse_position = e.GetPosition(this);
        }

        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
            {
                camera.UpdatePosition(-1f, 0, 0);
            } else
            {
                camera.UpdatePosition(1f, 0, 0);
            }
            Draw();
        }
    }
}
