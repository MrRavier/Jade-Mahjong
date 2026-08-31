using System;
using System.Collections.Generic;
using UnityEngine;

namespace JadeMahjong.Runtime
{
    public static class PixelArt
    {
        public const int TileWidth = 96;
        public const int TileHeight = 128;

        public static readonly Color32 Ink = new(13, 30, 29, 255);
        public static readonly Color32 DeepJade = new(14, 66, 58, 255);
        public static readonly Color32 Jade = new(29, 126, 94, 255);
        public static readonly Color32 PaleJade = new(132, 203, 156, 255);
        public static readonly Color32 Gold = new(236, 183, 66, 255);
        public static readonly Color32 Ivory = new(249, 235, 191, 255);
        public static readonly Color32 Vermilion = new(207, 60, 43, 255);
        public static readonly Color32 Night = new(5, 24, 31, 245);

        private static readonly Dictionary<int, Sprite> Tiles = new();
        private static Sprite _panel;
        private static Sprite _button;
        private static Sprite _buttonPressed;
        private static Sprite _progress;
        private static Texture2D _cutoutEmperor;

        public static Sprite Tile(int kind)
        {
            if (Tiles.TryGetValue(kind, out var cached))
                return cached;

            var texture = NewTexture(TileWidth, TileHeight);
            Rect(texture, 6, 3, 86, 116, new Color32(5, 35, 32, 220));
            Rect(texture, 2, 9, 88, 116, DeepJade);
            Rect(texture, 5, 12, 86, 112, Gold);
            Rect(texture, 8, 15, 80, 106, Ivory);
            Rect(texture, 11, 18, 74, 100, new Color32(255, 247, 218, 255));
            Frame(texture, 13, 20, 70, 96, Jade, 2);
            CornerClouds(texture);

            if (kind <= 8)
                DrawCircles(texture, kind + 1);
            else if (kind <= 17)
                DrawBamboo(texture, kind - 8);
            else if (kind <= 26)
                DrawCharacter(texture, kind - 17);
            else if (kind <= 30)
                DrawWind(texture, kind - 27);
            else if (kind <= 33)
                DrawDragon(texture, kind - 31);
            else if (kind <= 37)
                DrawFlower(texture, kind - 34);
            else
                DrawSeason(texture, kind - 38);

            texture.Apply(false, false);
            cached = Sprite.Create(texture, new Rect(0, 0, TileWidth, TileHeight),
                new Vector2(0.5f, 0.08f), 128f, 0, SpriteMeshType.FullRect);
            cached.name = $"tile_{kind:00}";
            Tiles[kind] = cached;
            return cached;
        }

        public static Sprite Background()
        {
            var texture = Resources.Load<Texture2D>("Art/Backgrounds/jade_palace");
            if (texture == null)
                return Panel();
            texture.filterMode = FilterMode.Point;
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        public static Sprite EmperorPose(int pose)
        {
            var texture = CutoutEmperorTexture();
            if (texture == null)
                return Tile(31);
            pose = Mathf.Clamp(pose, 0, 5);
            var width = texture.width / 3;
            var height = texture.height / 2;
            var column = pose % 3;
            var rowFromTop = pose / 3;
            var rect = new Rect(column * width, (1 - rowFromTop) * height, width, height);
            var sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.06f),
                Mathf.Max(width, height), 0, SpriteMeshType.FullRect);
            sprite.name = $"jade_emperor_{pose}";
            return sprite;
        }

        private static Texture2D CutoutEmperorTexture()
        {
            if (_cutoutEmperor != null)
                return _cutoutEmperor;
            var source = Resources.Load<Texture2D>("Art/Characters/jade_emperor_sheet");
            if (source == null)
                return null;
            source.filterMode = FilterMode.Point;

            try
            {
                var pixels = source.GetPixels32();
                var width = source.width;
                var height = source.height;
                var cellWidth = width / 3;
                var cellHeight = height / 2;
                for (var row = 0; row < 2; row++)
                    for (var column = 0; column < 3; column++)
                        FloodCellBackground(pixels, width, height,
                            column * cellWidth, row * cellHeight, cellWidth, cellHeight);

                _cutoutEmperor = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    name = "Jade Emperor Cutout"
                };
                _cutoutEmperor.SetPixels32(pixels);
                _cutoutEmperor.Apply(false, false);
                return _cutoutEmperor;
            }
            catch (UnityException)
            {
                return source;
            }
        }

        private static void FloodCellBackground(Color32[] pixels, int textureWidth, int textureHeight,
            int originX, int originY, int cellWidth, int cellHeight)
        {
            var visited = new bool[cellWidth * cellHeight];
            var queue = new Queue<int>(cellWidth * 2 + cellHeight * 2);

            void Add(int localX, int localY)
            {
                if (localX < 0 || localY < 0 || localX >= cellWidth || localY >= cellHeight)
                    return;
                var local = localY * cellWidth + localX;
                if (visited[local])
                    return;
                visited[local] = true;
                queue.Enqueue(local);
            }

            for (var x = 0; x < cellWidth; x++)
            {
                Add(x, 0);
                Add(x, cellHeight - 1);
            }
            for (var y = 0; y < cellHeight; y++)
            {
                Add(0, y);
                Add(cellWidth - 1, y);
            }

            while (queue.Count > 0)
            {
                var local = queue.Dequeue();
                var x = local % cellWidth;
                var y = local / cellWidth;
                var global = (originY + y) * textureWidth + originX + x;
                var current = pixels[global];
                pixels[global] = new Color32(current.r, current.g, current.b, 0);

                TrySpread(x - 1, y, current);
                TrySpread(x + 1, y, current);
                TrySpread(x, y - 1, current);
                TrySpread(x, y + 1, current);
            }

            void TrySpread(int x, int y, Color32 from)
            {
                if (x < 0 || y < 0 || x >= cellWidth || y >= cellHeight)
                    return;
                var local = y * cellWidth + x;
                if (visited[local])
                    return;
                var global = (originY + y) * textureWidth + originX + x;
                var to = pixels[global];
                var red = from.r - to.r;
                var green = from.g - to.g;
                var blue = from.b - to.b;
                if (red * red + green * green + blue * blue > 900)
                    return;
                visited[local] = true;
                queue.Enqueue(local);
            }
        }

        public static Sprite Panel()
        {
            if (_panel != null)
                return _panel;
            var texture = NewTexture(64, 64);
            Rect(texture, 0, 0, 64, 64, new Color32(3, 18, 25, 235));
            Frame(texture, 0, 0, 64, 64, Gold, 3);
            Frame(texture, 4, 4, 56, 56, Jade, 3);
            Frame(texture, 8, 8, 48, 48, PaleJade, 1);
            for (var index = 12; index < 52; index += 8)
            {
                Rect(texture, index, 5, 3, 3, Vermilion);
                Rect(texture, index, 56, 3, 3, Vermilion);
            }
            texture.Apply(false, false);
            _panel = Sprite.Create(texture, new Rect(0, 0, 64, 64),
                new Vector2(0.5f, 0.5f), 64f, 0, SpriteMeshType.FullRect,
                new Vector4(12, 12, 12, 12));
            return _panel;
        }

        public static Sprite Button(bool pressed)
        {
            if (pressed && _buttonPressed != null)
                return _buttonPressed;
            if (!pressed && _button != null)
                return _button;

            var texture = NewTexture(64, 32);
            Rect(texture, 0, 0, 64, 32, pressed ? DeepJade : Jade);
            Frame(texture, 0, 0, 64, 32, Ink, 2);
            Frame(texture, 3, 3, 58, 26, Gold, 2);
            Rect(texture, 8, pressed ? 8 : 10, 48, 2, PaleJade);
            Rect(texture, 8, 6, 48, 2, Vermilion);
            texture.Apply(false, false);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 64, 32),
                new Vector2(0.5f, 0.5f), 64f, 0, SpriteMeshType.FullRect,
                new Vector4(9, 9, 9, 9));
            if (pressed)
                _buttonPressed = sprite;
            else
                _button = sprite;
            return sprite;
        }

        public static Sprite ProgressFill()
        {
            if (_progress != null)
                return _progress;
            var texture = NewTexture(32, 12);
            Rect(texture, 0, 0, 32, 12, DeepJade);
            Frame(texture, 0, 0, 32, 12, Gold, 1);
            for (var x = 3; x < 29; x += 4)
                Rect(texture, x, 3, 2, 6, PaleJade);
            texture.Apply(false, false);
            _progress = Sprite.Create(texture, new Rect(0, 0, 32, 12),
                new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect,
                new Vector4(3, 3, 3, 3));
            return _progress;
        }

        private static Texture2D NewTexture(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "JadeMahjongPixelArt"
            };
            var pixels = new Color32[width * height];
            for (var index = 0; index < pixels.Length; index++)
                pixels[index] = new Color32(0, 0, 0, 0);
            texture.SetPixels32(pixels);
            return texture;
        }

        private static void DrawCircles(Texture2D texture, int count)
        {
            var positions = PipPositions(count);
            var colors = new[] { Vermilion, Jade, new Color32(40, 83, 145, 255) };
            for (var index = 0; index < positions.Count; index++)
            {
                var point = positions[index];
                Circle(texture, point.x, point.y, count == 1 ? 17 : 8, Ink);
                Circle(texture, point.x, point.y, count == 1 ? 14 : 6, colors[index % colors.Length]);
                Circle(texture, point.x, point.y, count == 1 ? 9 : 3, Gold);
                Circle(texture, point.x, point.y, count == 1 ? 4 : 1, Ivory);
            }
        }

        private static List<Vector2Int> PipPositions(int count)
        {
            var result = new List<Vector2Int>();
            int[] xs = { 28, 48, 68 };
            int[] ys = { 42, 67, 92 };
            if (count == 1)
                return new List<Vector2Int> { new(48, 66) };
            var order = new[] { 0, 8, 2, 6, 4, 1, 7, 3, 5 };
            for (var index = 0; index < count; index++)
            {
                var cell = order[index];
                result.Add(new Vector2Int(xs[cell % 3], ys[cell / 3]));
            }
            return result;
        }

        private static void DrawBamboo(Texture2D texture, int count)
        {
            if (count == 1)
            {
                Circle(texture, 48, 70, 18, Jade);
                Circle(texture, 48, 70, 13, Gold);
                Line(texture, 35, 63, 59, 80, Ink, 4);
                Line(texture, 39, 83, 55, 56, Vermilion, 3);
                Circle(texture, 58, 82, 3, Ink);
                return;
            }

            var positions = PipPositions(Mathf.Min(count, 9));
            foreach (var point in positions)
            {
                Line(texture, point.x - 2, point.y - 8, point.x + 2, point.y + 8, Jade, 4);
                Line(texture, point.x - 5, point.y - 1, point.x + 5, point.y + 2, Gold, 2);
                Line(texture, point.x - 2, point.y + 4, point.x - 7, point.y + 8, DeepJade, 2);
                Line(texture, point.x + 2, point.y - 4, point.x + 7, point.y - 8, DeepJade, 2);
            }
        }

        private static void DrawCharacter(Texture2D texture, int number)
        {
            DrawGlyph(texture, (char)('0' + number), 33, 65, 5, Ink);
            Line(texture, 25, 40, 70, 40, Vermilion, 3);
            Line(texture, 30, 34, 65, 34, Vermilion, 2);
            Line(texture, 39, 28, 57, 47, Vermilion, 3);
            Frame(texture, 62, 24, 14, 14, Vermilion, 2);
            Line(texture, 65, 27, 72, 34, Vermilion, 2);
        }

        private static void DrawWind(Texture2D texture, int wind)
        {
            var glyphs = new[] { 'N', 'L', 'S', 'O' };
            DrawGlyph(texture, glyphs[wind], 30, 60, 6, Ink);
            for (var offset = 0; offset < 3; offset++)
            {
                Line(texture, 22 + offset * 8, 42 - offset * 3, 38 + offset * 8, 42 - offset * 3,
                    offset == 1 ? Vermilion : Jade, 2);
                Circle(texture, 28 + offset * 14, 93, 5 + offset, Gold);
            }
        }

        private static void DrawDragon(Texture2D texture, int dragon)
        {
            var color = dragon switch
            {
                0 => Vermilion,
                1 => Jade,
                _ => new Color32(232, 224, 194, 255)
            };
            Line(texture, 27, 42, 62, 94, Ink, 8);
            Line(texture, 27, 42, 62, 94, color, 5);
            Line(texture, 31, 57, 65, 52, color, 5);
            Line(texture, 44, 78, 26, 88, color, 5);
            Circle(texture, 64, 95, 9, Gold);
            Circle(texture, 61, 98, 2, Ink);
            Circle(texture, 68, 98, 2, Ink);
            Line(texture, 58, 103, 51, 112, Vermilion, 2);
            Line(texture, 70, 103, 77, 112, Vermilion, 2);
        }

        private static void DrawFlower(Texture2D texture, int flower)
        {
            var petals = new[] { Vermilion, Jade, new Color32(119, 84, 158, 255), Gold };
            var color = petals[flower];
            for (var angle = 0; angle < 8; angle++)
            {
                var radians = angle * Mathf.PI / 4f;
                var x = 48 + Mathf.RoundToInt(Mathf.Cos(radians) * 17);
                var y = 71 + Mathf.RoundToInt(Mathf.Sin(radians) * 17);
                Circle(texture, x, y, 8, color);
                Circle(texture, x, y, 4, Ivory);
            }
            Circle(texture, 48, 71, 10, Gold);
            Line(texture, 48, 59, 43, 33, Jade, 3);
            Line(texture, 44, 45, 31, 51, PaleJade, 3);
            DrawGlyph(texture, (char)('1' + flower), 67, 25, 2, Vermilion);
        }

        private static void DrawSeason(Texture2D texture, int season)
        {
            var skies = new[]
            {
                new Color32(104, 169, 214, 255),
                Gold,
                Vermilion,
                new Color32(180, 210, 228, 255)
            };
            Circle(texture, 65, 92, 10, skies[season]);
            Line(texture, 20, 43, 45, 83, DeepJade, 5);
            Line(texture, 45, 83, 76, 43, DeepJade, 5);
            Line(texture, 31, 61, 39, 67, Ivory, 3);
            Line(texture, 39, 67, 46, 58, Ivory, 3);
            for (var index = 0; index <= season; index++)
                Circle(texture, 27 + index * 13, 36 + (index % 2) * 5, 4, skies[season]);
            DrawGlyph(texture, (char)('1' + season), 67, 25, 2, Ink);
        }

        private static void CornerClouds(Texture2D texture)
        {
            Line(texture, 16, 27, 27, 27, PaleJade, 2);
            Circle(texture, 18, 31, 3, PaleJade);
            Circle(texture, 24, 32, 4, PaleJade);
            Line(texture, 67, 105, 78, 105, Gold, 2);
            Circle(texture, 71, 109, 3, Gold);
            Circle(texture, 77, 110, 4, Gold);
        }

        private static void DrawGlyph(Texture2D texture, char glyph, int x, int y, int scale, Color32 color)
        {
            var pattern = Glyph(glyph);
            for (var row = 0; row < pattern.Length; row++)
            {
                for (var column = 0; column < pattern[row].Length; column++)
                {
                    if (pattern[row][column] != '1')
                        continue;
                    Rect(texture, x + column * scale, y + (pattern.Length - 1 - row) * scale,
                        scale, scale, color);
                }
            }
        }

        private static string[] Glyph(char glyph)
        {
            return glyph switch
            {
                '0' => new[] { "111", "101", "101", "101", "111" },
                '1' => new[] { "010", "110", "010", "010", "111" },
                '2' => new[] { "111", "001", "111", "100", "111" },
                '3' => new[] { "111", "001", "111", "001", "111" },
                '4' => new[] { "101", "101", "111", "001", "001" },
                '5' => new[] { "111", "100", "111", "001", "111" },
                '6' => new[] { "111", "100", "111", "101", "111" },
                '7' => new[] { "111", "001", "010", "010", "010" },
                '8' => new[] { "111", "101", "111", "101", "111" },
                '9' => new[] { "111", "101", "111", "001", "111" },
                'N' => new[] { "10001", "11001", "10101", "10011", "10001" },
                'L' => new[] { "1000", "1000", "1000", "1000", "1111" },
                'S' => new[] { "1111", "1000", "1111", "0001", "1111" },
                'O' => new[] { "1111", "1001", "1001", "1001", "1111" },
                _ => new[] { "111", "101", "010", "000", "010" }
            };
        }

        private static void Rect(Texture2D texture, int x, int y, int width, int height, Color32 color)
        {
            var xMin = Mathf.Clamp(x, 0, texture.width);
            var yMin = Mathf.Clamp(y, 0, texture.height);
            var xMax = Mathf.Clamp(x + width, 0, texture.width);
            var yMax = Mathf.Clamp(y + height, 0, texture.height);
            for (var py = yMin; py < yMax; py++)
                for (var px = xMin; px < xMax; px++)
                    texture.SetPixel(px, py, color);
        }

        private static void Frame(Texture2D texture, int x, int y, int width, int height,
            Color32 color, int thickness)
        {
            Rect(texture, x, y, width, thickness, color);
            Rect(texture, x, y + height - thickness, width, thickness, color);
            Rect(texture, x, y, thickness, height, color);
            Rect(texture, x + width - thickness, y, thickness, height, color);
        }

        private static void Circle(Texture2D texture, int centerX, int centerY, int radius, Color32 color)
        {
            var squared = radius * radius;
            for (var y = -radius; y <= radius; y++)
            {
                for (var x = -radius; x <= radius; x++)
                {
                    if (x * x + y * y <= squared)
                        Rect(texture, centerX + x, centerY + y, 1, 1, color);
                }
            }
        }

        private static void Line(Texture2D texture, int x0, int y0, int x1, int y1,
            Color32 color, int thickness)
        {
            var dx = Math.Abs(x1 - x0);
            var sx = x0 < x1 ? 1 : -1;
            var dy = -Math.Abs(y1 - y0);
            var sy = y0 < y1 ? 1 : -1;
            var error = dx + dy;
            while (true)
            {
                Rect(texture, x0 - thickness / 2, y0 - thickness / 2, thickness, thickness, color);
                if (x0 == x1 && y0 == y1)
                    break;
                var doubleError = 2 * error;
                if (doubleError >= dy)
                {
                    error += dy;
                    x0 += sx;
                }
                if (doubleError <= dx)
                {
                    error += dx;
                    y0 += sy;
                }
            }
        }
    }
}
