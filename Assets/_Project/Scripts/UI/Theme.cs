using UnityEngine;

namespace RedZone.UI
{
    /// <summary>Palette, fonts and procedural sprites. Every visual constant of the game lives here.</summary>
    public static class Theme
    {
        // a dark kernel console: navy ground, the three semaphore colours of the 2019 game kept as signals
        public static readonly Color Ground = Hex(0x0A0D1A);
        public static readonly Color GroundHi = Hex(0x111733);
        public static readonly Color Panel = Hex(0x141B33, 0.94f);
        public static readonly Color PanelLine = Hex(0x2A3560);
        public static readonly Color Cell = Hex(0x18203D);
        public static readonly Color CellLine = Hex(0x222C52);
        public static readonly Color Gate = Hex(0x2EE07F);
        public static readonly Color Critical = Hex(0xFF3B4E);
        public static readonly Color Signal = Hex(0x43A8FF);
        public static readonly Color Wall = Hex(0x3B4466);
        public static readonly Color Yellow = Hex(0xFFD23F);
        public static readonly Color Text = Hex(0xEAF0FF);
        public static readonly Color Dim = Hex(0x8C97BF);
        public static readonly Color Ink = Hex(0x080A14);
        public static readonly Color Bsod = Hex(0x0A3AA8);
        public static readonly Color Win = Hex(0x1E9E52);

        /// <summary>Thread colours: bright, distinct from the three zone colours.</summary>
        public static readonly Color[] Threads =
        {
            Hex(0x5CE1FF), Hex(0xFF7AD9), Hex(0xFFB547), Hex(0xB28CFF), Hex(0xC8FF5C), Hex(0xFF8E6B), Hex(0x7CF5D2), Hex(0xF5F07C),
        };

        public static Color Hex(int rgb, float a = 1f) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);
        public static Color A(Color c, float a) { c.a = a; return c; }

        static Font display, body, light, mono;
        public static Font Display => display ? display : (display = Load("Oswald-Medium"));
        public static Font Body => body ? body : (body = Load("Oswald-Regular"));
        public static Font Light => light ? light : (light = Load("Oswald-Light"));
        public static Font Mono => mono ? mono : (mono = Load("VT323-Regular"));
        static Font Load(string n) => Resources.Load<Font>("Fonts/" + n) ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // ------------------------------------------------------------ sprites made once in code

        static Sprite round, roundSmall, disc, ring, glow, tri, scan, stripes, star, starHollow;

        /// <summary>A rounded rectangle, 9-sliced, radius 16 px of a 64 px texture.</summary>
        public static Sprite Round => round ? round : (round = RoundRect(64, 16));
        public static Sprite RoundSmall => roundSmall ? roundSmall : (roundSmall = RoundRect(32, 6));

        static Sprite RoundRect(int n, int r)
        {
            var tex = NewTex(n, n);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, r, n - r), cy = Mathf.Clamp(y + 0.5f, r, n - r);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(r - d + 0.5f)));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        }

        public static Sprite Disc => disc ? disc : (disc = Radial(64, d => Mathf.Clamp01((0.5f - d) * 64f)));
        /// <summary>A ring for the quantum timer (Image.Type.Filled, radial).</summary>
        public static Sprite Ring => ring ? ring : (ring = Radial(128, d => Mathf.Clamp01((0.5f - d) * 128f) * Mathf.Clamp01((d - 0.40f) * 128f)));
        public static Sprite Glow => glow ? glow : (glow = Radial(128, d => Mathf.Pow(Mathf.Clamp01(1f - d * 2f), 2.2f)));

        static Sprite Radial(int n, System.Func<float, float> alpha)
        {
            var tex = NewTex(n, n);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2((x + 0.5f) / n, (y + 0.5f) / n), new Vector2(0.5f, 0.5f));
                tex.SetPixel(x, y, new Color(1, 1, 1, alpha(d)));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        /// <summary>A triangle pointing right (arrows on keycaps, the mirrored badge).</summary>
        public static Sprite Triangle
        {
            get
            {
                if (tri) return tri;
                const int n = 64; var tex = NewTex(n, n);
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                    float half = 0.42f * (1f - (u - 0.12f) / 0.76f);
                    float inside = Mathf.Min((u - 0.12f) * n, (0.88f - u) * n, (half - Mathf.Abs(v - 0.5f)) * n);
                    tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(inside + 0.5f)));
                }
                tex.Apply();
                return tri = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            }
        }

        public static Sprite Star => star ? star : (star = StarSprite(false));
        public static Sprite StarHollow => starHollow ? starHollow : (starHollow = StarSprite(true));

        static Sprite StarSprite(bool hollow)
        {
            const int n = 96; var tex = NewTex(n, n);
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float ang = Mathf.PI / 2f + i * Mathf.PI / 5f, r = i % 2 == 0 ? 0.47f : 0.2f;
                pts[i] = new Vector2(0.5f + Mathf.Cos(ang) * r, 0.5f + Mathf.Sin(ang) * r);
            }
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                var p = new Vector2((x + 0.5f) / n, (y + 0.5f) / n);
                float sd = SignedDistance(p, pts) * n;
                float a = hollow ? Mathf.Clamp01(3.5f - Mathf.Abs(sd + 3.5f)) : Mathf.Clamp01(-sd + 0.5f);
                tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        static float SignedDistance(Vector2 p, Vector2[] poly)
        {
            float d = float.MaxValue; bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                Vector2 a = poly[j], b = poly[i], e = b - a, w = p - a;
                float t = Mathf.Clamp01(Vector2.Dot(w, e) / e.sqrMagnitude);
                d = Mathf.Min(d, (w - e * t).magnitude);
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside ? -d : d;
        }

        /// <summary>CRT scanlines, tiled over the whole screen at low alpha.</summary>
        public static Sprite Scanlines
        {
            get
            {
                if (scan) return scan;
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
                for (int y = 0; y < 4; y++) for (int x = 0; x < 4; x++) tex.SetPixel(x, y, new Color(0, 0, 0, y < 2 ? 1f : 0f));
                tex.Apply();
                return scan = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            }
        }

        /// <summary>Diagonal hazard stripes for walls.</summary>
        public static Sprite Stripes
        {
            get
            {
                if (stripes) return stripes;
                const int n = 32; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
                for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                {
                    float k = Mathf.Repeat((x + y) / (float)n, 0.5f) < 0.25f ? 1f : 0.55f;
                    tex.SetPixel(x, y, new Color(k, k, k, 1f));
                }
                tex.Apply();
                return stripes = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            }
        }

        static Texture2D NewTex(int w, int h) =>
            new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
    }
}
