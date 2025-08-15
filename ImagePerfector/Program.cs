using System;
using System.Collections.Generic;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

class Program
{
    static void Main(string[] args)
    {
        string folder = @"D:/to-outline";

        foreach (var file in Directory.GetFiles(folder))
        {
            if (Path.GetExtension(file).ToLower() is ".jpg" or ".png" or ".jpeg")
            {
                Console.WriteLine($"Perfecting: {file}");
                using var image = Image.Load<Rgba32>(file);

                // ✅ Hard quantize to flatten subtle shades
                var quantizer = new WuQuantizer(new QuantizerOptions { MaxColors = 12 });
                image.Mutate(x => x.Quantize(quantizer));

                RemoveFullyIsolatedSpecks(image);

                image.Save(file);
                Console.WriteLine($"Saved perfected: {file}");
            }
        }

        Console.WriteLine("Done!");
    }

    static void RemoveFullyIsolatedSpecks(Image<Rgba32> image)
    {
        int w = image.Width, h = image.Height;
        var mask = new bool[w, h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (!mask[x, y])
                {
                    var color = image[x, y];
                    var region = FloodFill(image, mask, x, y, color);

                    if (IsIsolated(region, image))
                    {
                        if (region.Count < 50)
                        {
                            var dominant = GetDominantNeighborColor(image, region);
                            RecolorRegion(image, region, dominant);
                        }
                    }
                }
            }
        }
    }

    static bool IsIsolated(HashSet<Point> region, Image<Rgba32> image)
    {
        foreach (var p in region)
        {
            if (p.X == 0 || p.Y == 0 || p.X == image.Width - 1 || p.Y == image.Height - 1)
                return false; // touches border, so not enclosed
        }
        return true;
    }

    static HashSet<Point> FloodFill(Image<Rgba32> image, bool[,] mask, int sx, int sy, Rgba32 target)
    {
        int w = image.Width, h = image.Height;
        var region = new HashSet<Point>();
        var queue = new Queue<Point>();
        queue.Enqueue(new Point(sx, sy));

        while (queue.Count > 0)
        {
            var p = queue.Dequeue();
            int x = p.X, y = p.Y;

            if (x < 0 || y < 0 || x >= w || y >= h) continue;
            if (mask[x, y]) continue;

            if (IsSame(target, image[x, y]))
            {
                mask[x, y] = true;
                region.Add(p);

                queue.Enqueue(new Point(x + 1, y));
                queue.Enqueue(new Point(x - 1, y));
                queue.Enqueue(new Point(x, y + 1));
                queue.Enqueue(new Point(x, y - 1));
            }
        }

        return region;
    }

    static bool IsSame(Rgba32 a, Rgba32 b) =>
        a.R == b.R && a.G == b.G && a.B == b.B;

    static void RecolorRegion(Image<Rgba32> image, HashSet<Point> region, Rgba32 color)
    {
        foreach (var p in region)
            image[p.X, p.Y] = color;
    }

    static Rgba32 GetDominantNeighborColor(Image<Rgba32> image, HashSet<Point> region)
    {
        var counts = new Dictionary<Rgba32, int>();
        foreach (var p in region)
        {
            foreach (var offset in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
            {
                int nx = p.X + offset.Item1, ny = p.Y + offset.Item2;
                if (nx < 0 || ny < 0 || nx >= image.Width || ny >= image.Height) continue;

                var neighbor = image[nx, ny];
                if (counts.ContainsKey(neighbor)) counts[neighbor]++;
                else counts[neighbor] = 1;
            }
        }

        int max = 0;
        Rgba32 dominant = new Rgba32(255, 255, 255);
        foreach (var kv in counts)
        {
            if (kv.Value > max)
            {
                max = kv.Value;
                dominant = kv.Key;
            }
        }
        return dominant;
    }

    struct Point
    {
        public int X, Y;
        public Point(int x, int y) { X = x; Y = y; }
        public override bool Equals(object obj) => obj is Point p && p.X == X && p.Y == Y;
        public override int GetHashCode() => HashCode.Combine(X, Y);
    }
}