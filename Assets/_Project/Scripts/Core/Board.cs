using System;
using System.Collections.Generic;
using UnityEngine;

namespace RedZone.Core
{
    public enum Tile : byte { Free, Gate, Critical, Signal, Wall }
    public enum Dir { Up, Down, Left, Right }
    public enum CrashCause { None, RaceCondition, Deadlock }

    public sealed class ThreadUnit
    {
        public int Id;
        public Vector2Int Pos;
        public float Quantum, QuantumMax;
        public bool Running = true;
        public bool Mirrored;
        public bool Forced;
        public int Zone = -1;

        public ThreadUnit Copy() => (ThreadUnit)MemberwiseClone();
    }

    /// <summary>A semaphore: a connected group of gate or critical cells and how many threads it lets in.</summary>
    public sealed class Zone
    {
        public int Index;
        public Tile Kind;
        public int Capacity;
        public bool Mirrored;
        public readonly List<Vector2Int> Cells = new List<Vector2Int>();
        public int Count;
        public int Free => Capacity - Count;
        public Vector2 Center
        {
            get
            {
                Vector2 c = Vector2.zero;
                foreach (var p in Cells) c += p;
                return c / Mathf.Max(1, Cells.Count);
            }
        }
    }

    /// <summary>
    /// The rules of Red Zone, with no Unity objects: fork() creates threads on a free entry gate; every running
    /// thread executes the same instruction (the arrow the player pressed, mirrored for threads born on the right);
    /// when its quantum ends the thread stops where it is and keeps holding the semaphore of that cell.
    /// A critical region with more threads than permits is a race condition; a fork with no free gate is pushed
    /// into the critical region with a short quantum, and with no room there either the kernel deadlocks.
    /// y = 0 is the top row.
    /// </summary>
    public sealed class Board
    {
        public readonly LevelDef Def;
        public readonly int W, H;
        readonly Tile[,] tiles;
        readonly int[,] zoneOf;
        public readonly List<Zone> Zones = new List<Zone>();
        public readonly List<ThreadUnit> Threads = new List<ThreadUnit>();
        public readonly Dictionary<Vector2Int, float> SignalCooldown = new Dictionary<Vector2Int, float>();
        readonly List<Vector2Int> pads = new List<Vector2Int>();     // the V() pads, kept so Tick allocates nothing

        public float SpawnTimer, SpawnInterval, Elapsed;
        public int Completed, Spawned, Forced, Moves, Releases, nextId = 1;
        public bool Over, Won, Hold;
        public CrashCause Cause;
        public Zone CrashZone;
        System.Random rng;

        public event Action<ThreadUnit> Spawned_;
        public event Action<ThreadUnit> Finished;
        public event Action<ThreadUnit> Released;
        public event Action<ThreadUnit, Zone> Entered;
        public event Action<ThreadUnit, Zone> Left;
        public event Action<Vector2Int> SignalUsed;
        public event Action Moved;
        public event Action Bumped;
        public event Action<CrashCause, Zone> Crashed;
        public event Action Victory;

        public Board(LevelDef def, int seed)
        {
            Def = def;
            W = def.Width; H = def.Height;
            tiles = new Tile[W, H];
            zoneOf = new int[W, H];
            rng = new System.Random(seed);
            for (int y = 0; y < H; y++)
            {
                if (def.map[y].Length != W) throw new ArgumentException($"level {def.id}: row {y} has {def.map[y].Length} cells, expected {W}");
                for (int x = 0; x < W; x++)
                {
                    zoneOf[x, y] = -1;
                    switch (def.map[y][x])
                    {
                        case 'G': tiles[x, y] = Tile.Gate; break;
                        case 'R': tiles[x, y] = Tile.Critical; break;
                        case 'S': tiles[x, y] = Tile.Signal; SignalCooldown[new Vector2Int(x, y)] = 0f; pads.Add(new Vector2Int(x, y)); break;
                        case '#': tiles[x, y] = Tile.Wall; break;
                        default: tiles[x, y] = Tile.Free; break;
                    }
                }
            }
            BuildZones();
            SpawnInterval = def.spawnStart;
            SpawnTimer = def.firstSpawn;
        }

        Board(Board o)
        {
            Def = o.Def; W = o.W; H = o.H; tiles = o.tiles; zoneOf = o.zoneOf;
            foreach (var z in o.Zones)
            {
                var c = new Zone { Index = z.Index, Kind = z.Kind, Capacity = z.Capacity, Mirrored = z.Mirrored, Count = z.Count };
                c.Cells.AddRange(z.Cells);
                Zones.Add(c);
            }
            foreach (var t in o.Threads) Threads.Add(t.Copy());
            foreach (var kv in o.SignalCooldown) SignalCooldown[kv.Key] = kv.Value;
            pads.AddRange(o.pads);
            SpawnTimer = o.SpawnTimer; SpawnInterval = o.SpawnInterval; Elapsed = o.Elapsed;
            Completed = o.Completed; Spawned = o.Spawned; Forced = o.Forced; Moves = o.Moves; Releases = o.Releases; nextId = o.nextId;
            Over = o.Over; Won = o.Won; Cause = o.Cause;
            rng = new System.Random(o.nextId * 7919);
        }

        /// <summary>A deep copy without event listeners (the auto player looks ahead with it).</summary>
        public Board Clone() => new Board(this);

        void BuildZones()
        {
            var seen = new bool[W, H];
            var stack = new Stack<Vector2Int>();
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                var kind = tiles[x, y];
                if (seen[x, y] || (kind != Tile.Gate && kind != Tile.Critical)) continue;
                var z = new Zone { Index = Zones.Count, Kind = kind, Capacity = kind == Tile.Gate ? 1 : Def.capacity };
                stack.Push(new Vector2Int(x, y)); seen[x, y] = true;
                while (stack.Count > 0)
                {
                    var p = stack.Pop();
                    z.Cells.Add(p); zoneOf[p.x, p.y] = z.Index;
                    foreach (var d in Neighbours)
                    {
                        var n = p + d;
                        if (In(n) && !seen[n.x, n.y] && tiles[n.x, n.y] == kind) { seen[n.x, n.y] = true; stack.Push(n); }
                    }
                }
                z.Mirrored = kind == Tile.Gate && Def.mirrorRight && z.Center.x > (W - 1) * 0.5f;
                Zones.Add(z);
            }
        }

        static readonly Vector2Int[] Neighbours = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        public bool In(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x < W && p.y < H;
        public Tile TileAt(Vector2Int p) => In(p) ? tiles[p.x, p.y] : Tile.Wall;
        public Zone ZoneAt(Vector2Int p) => In(p) && zoneOf[p.x, p.y] >= 0 ? Zones[zoneOf[p.x, p.y]] : null;

        public ThreadUnit ThreadAt(Vector2Int p)
        {
            foreach (var t in Threads) if (t.Pos == p) return t;
            return null;
        }

        public int Running
        {
            get { int n = 0; foreach (var t in Threads) if (t.Running) n++; return n; }
        }

        public float Progress => Def.goal > 0 ? Mathf.Clamp01(Completed / (float)Def.goal) : 0f;

        // ------------------------------------------------------------------ time

        public void Tick(float dt)
        {
            if (Over || Hold || dt <= 0f) return;
            Elapsed += dt;

            for (int i = 0; i < Threads.Count && !Over; i++)
            {
                var t = Threads[i];
                if (!t.Running) continue;
                t.Quantum -= dt;
                if (t.Quantum <= 0f) Finish(t);
            }
            if (Over) return;

            for (int i = 0; i < pads.Count; i++)
            {
                var k = pads[i];
                SignalCooldown[k] = Mathf.Max(0f, SignalCooldown[k] - dt);
            }

            SpawnInterval = Mathf.Max(Def.spawnMin, SpawnInterval - Def.spawnAccel * dt);
            SpawnTimer -= dt;
            if (SpawnTimer <= 0f)
            {
                Fork();
                SpawnTimer = SpawnInterval;
            }
        }

        void Finish(ThreadUnit t)
        {
            t.Running = false;
            t.Quantum = 0f;
            Completed++;
            Finished?.Invoke(t);
            if (Def.goal > 0 && Completed >= Def.goal && !Over)
            {
                Over = true; Won = true;
                Victory?.Invoke();
            }
        }

        /// <summary>fork(): a new thread on a free gate, or pushed into the critical region when every gate is taken.</summary>
        public ThreadUnit Fork()
        {
            if (Over) return null;
            var freeGates = new List<Zone>();
            foreach (var z in Zones) if (z.Kind == Tile.Gate && z.Count == 0) freeGates.Add(z);

            if (freeGates.Count > 0)
            {
                var gate = freeGates[rng.Next(freeGates.Count)];
                var cells = FreeCells(gate);
                if (cells.Count > 0)
                    return Add(cells[rng.Next(cells.Count)], Def.quantum, gate.Mirrored, false);
            }

            var crit = new List<Vector2Int>();
            foreach (var z in Zones) if (z.Kind == Tile.Critical) crit.AddRange(FreeCells(z));
            if (crit.Count == 0)
            {
                Crash(CrashCause.Deadlock, null);
                return null;
            }
            var p = crit[rng.Next(crit.Count)];
            Forced++;
            return Add(p, Def.forcedQuantum, Def.mirrorRight && p.x > (W - 1) * 0.5f, true);
        }

        List<Vector2Int> FreeCells(Zone z)
        {
            var list = new List<Vector2Int>();
            foreach (var c in z.Cells) if (ThreadAt(c) == null) list.Add(c);
            return list;
        }

        ThreadUnit Add(Vector2Int p, float quantum, bool mirrored, bool forced)
        {
            var t = new ThreadUnit { Id = nextId++, Pos = p, Quantum = quantum, QuantumMax = quantum, Mirrored = mirrored, Forced = forced };
            Threads.Add(t);
            Spawned++;
            Spawned_?.Invoke(t);
            EnterZone(t, ZoneAt(p));
            return t;
        }

        // ------------------------------------------------------------------ input

        public static Vector2Int Delta(Dir d)
        {
            switch (d)
            {
                case Dir.Up: return new Vector2Int(0, -1);
                case Dir.Down: return new Vector2Int(0, 1);
                case Dir.Left: return new Vector2Int(-1, 0);
                default: return new Vector2Int(1, 0);
            }
        }

        public Vector2Int TargetOf(ThreadUnit t, Dir d)
        {
            var delta = Delta(d);
            if (t.Mirrored) delta.x = -delta.x;
            return t.Pos + delta;
        }

        /// <summary>
        /// One instruction for every running thread at once. A thread moves when its target cell is inside the board,
        /// not a wall, and either empty or being vacated by a running thread that also moves. Two threads aiming at the
        /// same cell, or swapping places, both stay. Returns how many threads moved.
        /// </summary>
        public int Move(Dir d)
        {
            if (Over) return 0;
            var movers = new List<ThreadUnit>();
            foreach (var t in Threads) if (t.Running) movers.Add(t);
            if (movers.Count == 0) return 0;

            var target = new Dictionary<ThreadUnit, Vector2Int>();
            var ok = new Dictionary<ThreadUnit, bool>();
            var occ = new Dictionary<Vector2Int, ThreadUnit>();
            foreach (var t in Threads) occ[t.Pos] = t;
            foreach (var t in movers)
            {
                var p = TargetOf(t, d);
                target[t] = p;
                ok[t] = In(p) && TileAt(p) != Tile.Wall && !(occ.TryGetValue(p, out var o) && !o.Running);
            }

            bool changed = true;
            while (changed)
            {
                changed = false;
                var claims = new Dictionary<Vector2Int, int>();
                foreach (var t in movers) if (ok[t]) claims[target[t]] = claims.TryGetValue(target[t], out var n) ? n + 1 : 1;
                foreach (var t in movers)
                {
                    if (!ok[t]) continue;
                    var p = target[t];
                    bool blocked = claims[p] > 1;
                    if (!blocked && occ.TryGetValue(p, out var o) && o != t)
                        blocked = !o.Running || !ok[o] || target[o] == t.Pos;
                    if (blocked) { ok[t] = false; changed = true; }
                }
            }

            int moved = 0;
            foreach (var t in movers) if (ok[t]) moved++;
            if (moved == 0) { Bumped?.Invoke(); return 0; }

            Moves++;
            var steppedOnPad = new List<Vector2Int>();
            foreach (var t in movers)
            {
                if (!ok[t]) continue;
                var from = ZoneAt(t.Pos);
                t.Pos = target[t];
                var to = ZoneAt(t.Pos);
                if (from != to)
                {
                    if (from != null) { from.Count--; t.Zone = -1; Left?.Invoke(t, from); }
                }
                if (TileAt(t.Pos) == Tile.Signal) steppedOnPad.Add(t.Pos);
            }
            Moved?.Invoke();
            // entering after every exit, so a thread leaving a region frees its permit before another one takes it
            foreach (var t in movers)
            {
                if (!ok[t] || Over) continue;
                var to = ZoneAt(t.Pos);
                if (to != null && t.Zone != to.Index) EnterZone(t, to);
            }
            foreach (var pad in steppedOnPad) if (!Over) Signal(pad);
            return moved;
        }

        void EnterZone(ThreadUnit t, Zone z)
        {
            if (z == null || Over) return;
            t.Zone = z.Index;
            z.Count++;
            Entered?.Invoke(t, z);
            if (z.Kind == Tile.Critical && z.Count > z.Capacity) Crash(CrashCause.RaceCondition, z);
        }

        /// <summary>V(S) on a signal pad: the oldest finished thread holding a critical permit (else a gate) is released.</summary>
        void Signal(Vector2Int pad)
        {
            if (SignalCooldown.TryGetValue(pad, out var cd) && cd > 0f) return;
            ThreadUnit victim = Oldest(Tile.Critical) ?? Oldest(Tile.Gate);
            if (victim == null) return;
            SignalCooldown[pad] = Def.signalCooldown;
            var z = ZoneAt(victim.Pos);
            if (z != null) { z.Count--; Left?.Invoke(victim, z); }
            Threads.Remove(victim);
            Releases++;
            SignalUsed?.Invoke(pad);
            Released?.Invoke(victim);
        }

        ThreadUnit Oldest(Tile kind)
        {
            foreach (var t in Threads)
                if (!t.Running && TileAt(t.Pos) == kind) return t;
            return null;
        }

        void Crash(CrashCause cause, Zone z)
        {
            if (Over) return;
            Over = true; Won = false; Cause = cause; CrashZone = z;
            foreach (var t in Threads) t.Running = false;
            Crashed?.Invoke(cause, z);
        }

        /// <summary>The worst load among the critical regions, 0..1+ (for music and warnings).</summary>
        public float Pressure
        {
            get
            {
                float p = 0f;
                foreach (var z in Zones) if (z.Kind == Tile.Critical) p = Mathf.Max(p, z.Count / (float)Mathf.Max(1, z.Capacity));
                int blocked = 0, gates = 0;
                foreach (var z in Zones) if (z.Kind == Tile.Gate) { gates++; if (z.Count > 0 && IsHeldByFinished(z)) blocked++; }
                if (gates > 0) p = Mathf.Max(p, blocked / (float)gates);
                return p;
            }
        }

        public bool IsHeldByFinished(Zone z)
        {
            foreach (var t in Threads) if (!t.Running && t.Zone == z.Index) return true;
            return false;
        }
    }
}
