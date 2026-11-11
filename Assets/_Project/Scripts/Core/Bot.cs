using UnityEngine;

namespace RedZone.Core
{
    /// <summary>
    /// A greedy player: tries every instruction on a copy of the board and keeps the one that leaves the running
    /// threads furthest from trouble. Drives the title screen's live board and the automated playtest.
    /// </summary>
    public static class Bot
    {
        static readonly Dir[] All = { Dir.Up, Dir.Down, Dir.Left, Dir.Right };

        /// <summary>The best instruction now, or null when standing still is best.</summary>
        public static Dir? Choose(Board b, float skill = 1f)
        {
            if (b.Over || b.Running == 0) return null;
            float best = Score(b, b);
            Dir? pick = null;
            foreach (var d in All)
            {
                var c = b.Clone();
                if (c.Move(d) == 0) continue;
                float s = Score(c, b);
                if (skill < 1f) s += Random.Range(-40f, 40f) * (1f - skill);
                if (s > best + 0.01f) { best = s; pick = d; }
            }
            return pick;
        }

        static float Score(Board c, Board original)
        {
            if (c.Over && !c.Won) return -1e6f;
            float s = c.Releases * 400f;
            foreach (var t in c.Threads)
            {
                if (!t.Running) continue;
                var tile = c.TileAt(t.Pos);
                // urgency: a thread about to stop must already be somewhere harmless
                float urgency = 1f + 3f / Mathf.Max(0.3f, t.Quantum);
                if (tile == Tile.Gate) s -= 60f * urgency;
                else if (tile == Tile.Critical) s -= 90f * urgency;
                s += Mathf.Min(DistanceTo(c, t.Pos, Tile.Gate), 4) * 6f * urgency;
                s += Mathf.Min(DistanceTo(c, t.Pos, Tile.Critical), 3) * 5f * urgency;
                // room to keep moving: free neighbours
                foreach (var d in All)
                {
                    var n = t.Pos + Board.Delta(d);
                    if (c.In(n) && c.TileAt(n) != Tile.Wall && c.ThreadAt(n) == null) s += 1.5f;
                }
                // a pad helps only when something is holding a permit
                if (tile == Tile.Signal && HeldPermits(c) > 0) s += 30f;
            }
            return s;
        }

        static int HeldPermits(Board c)
        {
            int n = 0;
            foreach (var t in c.Threads) if (!t.Running && (c.TileAt(t.Pos) == Tile.Critical || c.TileAt(t.Pos) == Tile.Gate)) n++;
            return n;
        }

        static int DistanceTo(Board c, Vector2Int p, Tile kind)
        {
            int best = 99;
            foreach (var z in c.Zones)
            {
                if (z.Kind != kind) continue;
                foreach (var cell in z.Cells) best = Mathf.Min(best, Mathf.Abs(cell.x - p.x) + Mathf.Abs(cell.y - p.y));
            }
            return best;
        }
    }
}
