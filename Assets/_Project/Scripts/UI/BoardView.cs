using System.Collections.Generic;
using RedZone.Audio;
using RedZone.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RedZone.UI
{
    /// <summary>
    /// Draws a <see cref="Board"/> inside a UI rect and animates it: cells and semaphores, threads with their quantum
    /// ring, P()/V() popups, bursts and shake. It only listens to the board's events; it never changes the rules.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        public const float CellSize = 100f;

        Board board;
        RectTransform area, root, cellLayer, zoneLayer, threadLayer, fxLayer;
        readonly Dictionary<int, ThreadView> views = new Dictionary<int, ThreadView>();
        readonly List<ZoneChip> chips = new List<ZoneChip>();
        readonly Dictionary<Vector2Int, Image> padFill = new Dictionary<Vector2Int, Image>();
        float shake, flashCritical;
        Vector2 shakeOffset;

        /// <summary>The title screen's live board plays no sound.</summary>
        public bool Silent;

        void Play(string name, float volume, float pitch = 1f) { if (!Silent) Sfx.Play(name, volume, pitch); }

        public Board Board => board;
        public RectTransform Root => root;

        public static BoardView Create(RectTransform area)
        {
            var v = area.gameObject.AddComponent<BoardView>();
            v.area = area;
            return v;
        }

        public void Show(Board b)
        {
            Clear();
            board = b;
            root = Ui.Box(area, "Board", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(b.W * CellSize, b.H * CellSize));
            // a frame around the whole board
            var frame = Ui.Image(root, "Frame", Theme.A(Theme.PanelLine, 0.9f), Theme.Round, true);
            Ui.Rect(frame.gameObject, Vector2.zero, Vector2.one, new Vector2(-18, -18), new Vector2(18, 18));
            var inner = Ui.Image(root, "Inner", Theme.Ground, Theme.Round, true);
            Ui.Rect(inner.gameObject, Vector2.zero, Vector2.one, new Vector2(-14, -14), new Vector2(14, 14));
            cellLayer = Ui.Fill(root, "Cells");
            zoneLayer = Ui.Fill(root, "Zones");
            threadLayer = Ui.Fill(root, "Threads");
            fxLayer = Ui.Fill(root, "Fx");

            for (int y = 0; y < b.H; y++)
            for (int x = 0; x < b.W; x++)
                BuildCell(new Vector2Int(x, y));

            foreach (var z in b.Zones) chips.Add(new ZoneChip(this, z, zoneLayer));

            b.Spawned_ += OnSpawn;
            b.Finished += OnFinish;
            b.Released += OnRelease;
            b.Entered += OnEnter;
            b.Left += OnLeave;
            b.SignalUsed += OnSignal;
            b.Crashed += OnCrash;
            b.Moved += () => Play("move", 0.35f, Random.Range(0.96f, 1.04f));
            b.Bumped += () => { Play("bump", 0.4f); Shake(4f); };
            foreach (var t in b.Threads) OnSpawn(t);
            Fit();
        }

        public void Clear()
        {
            views.Clear(); chips.Clear(); padFill.Clear();
            if (root) Destroy(root.gameObject);
            board = null;
        }

        public Vector2 CellPos(Vector2 cell) =>
            new Vector2((cell.x + 0.5f) * CellSize - board.W * CellSize * 0.5f, board.H * CellSize * 0.5f - (cell.y + 0.5f) * CellSize);

        void BuildCell(Vector2Int p)
        {
            var tile = board.TileAt(p);
            var r = Ui.Box(cellLayer, "Cell", new Vector2(0.5f, 0.5f), CellPos(p), new Vector2(CellSize - 8, CellSize - 8));
            Color c;
            switch (tile)
            {
                case Tile.Gate: c = Theme.A(Theme.Gate, 0.26f); break;
                case Tile.Critical: c = Theme.A(Theme.Critical, 0.30f); break;
                case Tile.Signal: c = Theme.A(Theme.Signal, 0.30f); break;
                case Tile.Wall: c = Theme.Wall; break;
                default: c = Theme.Cell; break;
            }
            var img = Ui.Image(r, "Fill", c, Theme.Round, true);
            if (tile == Tile.Wall)
            {
                var s = Ui.Image(r, "Stripes", Theme.A(Theme.Ink, 0.35f), Theme.Stripes);
                s.type = Image.Type.Tiled;
                Ui.Rect(s.gameObject, Vector2.zero, Vector2.one, new Vector2(6, 6), new Vector2(-6, -6));
            }
            if (tile == Tile.Signal)
            {
                var ring = Ui.Image(r, "Cooldown", Theme.A(Theme.Signal, 0.85f), Theme.Ring);
                Ui.Rect(ring.gameObject, Vector2.zero, Vector2.one, new Vector2(10, 10), new Vector2(-10, -10));
                ring.type = Image.Type.Filled; ring.fillMethod = Image.FillMethod.Radial360; ring.fillOrigin = 2; ring.fillClockwise = true;
                padFill[p] = ring;
                var t = Ui.Label(r, "V()", 40, Theme.Mono, Theme.Signal);
                Ui.Shadow(t, 2f, 0.6f);
            }
            if (tile == Tile.Gate || tile == Tile.Critical)
            {
                // a brighter edge on the zone's border cells so the semaphore reads as one shape
                var edge = Ui.Image(r, "Edge", Theme.A(tile == Tile.Gate ? Theme.Gate : Theme.Critical, 0.55f), Theme.Round, true);
                edge.transform.SetAsFirstSibling();
                Ui.Rect(edge.gameObject, Vector2.zero, Vector2.one, new Vector2(-3, -3), new Vector2(3, 3));
            }
        }

        // ------------------------------------------------------------------ events

        void OnSpawn(ThreadUnit t)
        {
            if (views.ContainsKey(t.Id)) return;
            var v = ThreadView.Create(threadLayer, t, this);
            views[t.Id] = v;
            Popup(t.Forced ? "fork()!" : "fork()", t.Forced ? Theme.Critical : Theme.Gate, t.Pos, 0.9f);
            Play(t.Forced ? "forced" : "fork", 0.6f);
            if (t.Forced) Shake(10f);
        }

        void OnFinish(ThreadUnit t)
        {
            var tile = board.TileAt(t.Pos);
            if (views.TryGetValue(t.Id, out var v)) v.Finish();
            bool bad = tile == Tile.Gate || tile == Tile.Critical;
            Popup(bad ? "exit()" : "exit(0)", bad ? Theme.Yellow : Theme.Text, t.Pos, 0.8f);
            Burst(t.Pos, bad ? Theme.Yellow : Theme.Threads[t.Id % Theme.Threads.Length], 10);
            Play(bad ? "finish_bad" : "finish", 0.55f);
        }

        void OnRelease(ThreadUnit t)
        {
            if (views.TryGetValue(t.Id, out var v)) { v.Release(); views.Remove(t.Id); }
            Burst(t.Pos, Theme.Signal, 18);
            Popup("V(S)", Theme.Signal, t.Pos, 1.1f);
        }

        void OnEnter(ThreadUnit t, Zone z)
        {
            if (z.Kind != Tile.Critical) return;
            Popup("P(S)", Theme.Critical, t.Pos, 1f);
            Play("enter", 0.5f, 1f + 0.08f * z.Count);
            ChipFor(z)?.Pulse();
        }

        void OnLeave(ThreadUnit t, Zone z)
        {
            if (z.Kind != Tile.Critical) return;
            Popup("V(S)", Theme.Gate, t.Pos, 0.9f);
            Play("leave", 0.45f);
            ChipFor(z)?.Pulse();
        }

        void OnSignal(Vector2Int pad)
        {
            Play("signal", 0.7f);
            Burst(pad, Theme.Signal, 14);
        }

        void OnCrash(CrashCause cause, Zone z)
        {
            Shake(36f);
            flashCritical = 1f;
            Play("crash", 0.9f);
            if (z != null) foreach (var c in z.Cells) Burst(c, Theme.Critical, 8);
            foreach (var v in views.Values) v.Crash();
        }

        ZoneChip ChipFor(Zone z) { foreach (var c in chips) if (c.zone == z) return c; return null; }

        // ------------------------------------------------------------------ juice

        public void Shake(float px) { if (!Settings.ReduceFlashes || px < 20f) shake = Mathf.Max(shake, Settings.ReduceFlashes ? px * 0.4f : px); }

        public void Popup(string text, Color color, Vector2 cell, float scale)
        {
            var r = Ui.Box(fxLayer, "Popup", new Vector2(0.5f, 0.5f), CellPos(cell) + new Vector2(0, 40), new Vector2(240, 60));
            var t = Ui.Label(r, text, Mathf.RoundToInt(44 * scale), Theme.Mono, color);
            Ui.Outline(t, Theme.A(Theme.Ink, 0.9f), 2f);
            r.gameObject.AddComponent<FloatAway>().Init(new Vector2(0, 70f), 0.9f);
        }

        public void Burst(Vector2 cell, Color color, int n)
        {
            for (int i = 0; i < n; i++)
            {
                var r = Ui.Box(fxLayer, "Spark", new Vector2(0.5f, 0.5f), CellPos(cell), Vector2.one * Random.Range(8f, 16f));
                Ui.Image(r, "Q", color, Theme.RoundSmall, true);
                float a = Random.value * Mathf.PI * 2f, s = Random.Range(120f, 300f);
                r.gameObject.AddComponent<FloatAway>().Init(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s, Random.Range(0.35f, 0.6f));
            }
        }

        void Fit()
        {
            if (!root || board == null) return;
            var a = area.rect;
            float k = Mathf.Min(a.width / (board.W * CellSize + 60f), a.height / (board.H * CellSize + 60f));
            if (k > 0f) root.localScale = Vector3.one * k;
        }

        void Update()
        {
            if (board == null) return;
            Fit();
            shake = Mathf.MoveTowards(shake, 0f, Time.unscaledDeltaTime * 90f);
            shakeOffset = shake > 0.01f ? Random.insideUnitCircle * shake : Vector2.zero;
            root.anchoredPosition = shakeOffset;
            foreach (var c in chips) c.Update();
            foreach (var kv in padFill)
                kv.Value.fillAmount = 1f - (board.SignalCooldown.TryGetValue(kv.Key, out var cd) ? cd / Mathf.Max(0.01f, board.Def.signalCooldown) : 0f);
        }

        /// <summary>The label above each semaphore: S = free permits, with one dot per permit.</summary>
        sealed class ZoneChip
        {
            public readonly Zone zone;
            readonly Text text;
            readonly Image back;
            readonly Image[] dots;
            float pulse;
            readonly Color color;

            public ZoneChip(BoardView view, Zone z, RectTransform layer)
            {
                zone = z;
                color = z.Kind == Tile.Gate ? Theme.Gate : Theme.Critical;
                // top-most cell of the zone; gate chips sit just above it, critical chips centred over the region
                float minY = float.MaxValue, sumX = 0f;
                foreach (var c in z.Cells) { minY = Mathf.Min(minY, c.y); sumX += c.x; }
                var top = new Vector2(sumX / z.Cells.Count, minY);
                var pos = view.CellPos(top) + new Vector2(0, CellSize * 0.5f + 4f);
                bool gate = z.Kind == Tile.Gate;
                var size = gate ? new Vector2(96, 44) : new Vector2(150, 50);
                var r = Ui.Box(layer, "Chip", new Vector2(0.5f, 0.5f), pos, size);
                if (gate && minY == 0) r.anchoredPosition += new Vector2(0, 14);
                back = Ui.Image(r, "Back", Theme.A(Theme.Ink, 0.92f), Theme.RoundSmall, true);
                Ui.Outline(back, Theme.A(color, 0.9f), 2f);
                text = Ui.Text(r, "S", "", gate ? 34 : 40, Theme.Mono, color, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, new Vector2(10, 0), Vector2.zero);
                dots = new Image[Mathf.Min(z.Capacity, 6)];
                for (int i = 0; i < dots.Length; i++)
                {
                    var d = Ui.Box(r, "Permit", new Vector2(1, 0.5f), new Vector2(-14 - i * 18, 0), new Vector2(13, 13));
                    dots[i] = Ui.Image(d, "Dot", color, Theme.Disc);
                }
            }

            public void Pulse() => pulse = 1f;

            public void Update()
            {
                pulse = Mathf.MoveTowards(pulse, 0f, Time.unscaledDeltaTime * 2.5f);
                int free = zone.Free;
                text.text = "S=" + free;
                for (int i = 0; i < dots.Length; i++)
                    dots[dots.Length - 1 - i].color = i < free ? color : Theme.A(color, 0.18f);
                float warn = zone.Kind == Tile.Critical && free == 0 ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f) : 0f;
                back.color = Color.Lerp(Theme.A(Theme.Ink, 0.92f), Theme.A(color, 0.85f), Mathf.Max(pulse * 0.6f, warn * 0.5f));
                text.color = Color.Lerp(color, Theme.Text, Mathf.Max(pulse, warn));
                back.rectTransform.localScale = Vector3.one * (1f + pulse * 0.15f);
            }
        }
    }

    /// <summary>Moves a UI element by a velocity while fading it out, then destroys it.</summary>
    public sealed class FloatAway : MonoBehaviour
    {
        Vector2 vel; float life, t;
        Graphic[] gs; RectTransform r;
        public void Init(Vector2 velocity, float seconds) { vel = velocity; life = seconds; r = (RectTransform)transform; gs = GetComponentsInChildren<Graphic>(); }
        void Update()
        {
            t += Time.unscaledDeltaTime;
            r.anchoredPosition += vel * Time.unscaledDeltaTime;
            vel *= 1f - Time.unscaledDeltaTime * 2.5f;
            float a = 1f - Mathf.Clamp01(t / life);
            foreach (var g in gs) { var c = g.color; c.a = Mathf.Min(c.a, a); g.color = c; }
            if (t >= life) Destroy(gameObject);
        }
    }
}
