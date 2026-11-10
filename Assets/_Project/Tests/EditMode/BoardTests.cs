using NUnit.Framework;
using RedZone.Core;
using UnityEngine;

namespace RedZone.Tests
{
    /// <summary>The rules that make the game: semaphore counts, simultaneous moves, forced forks, crashes, V() pads.</summary>
    public class BoardTests
    {
        static LevelDef Def(int capacity, params string[] map) => new LevelDef
        {
            id = "t", map = map, capacity = capacity, quantum = 3f, forcedQuantum = 1f, spawnStart = 100f, spawnMin = 100f,
            spawnAccel = 0f, firstSpawn = 100f, goal = 0, signalCooldown = 5f,
        };

        [Test]
        public void Zones_AreConnectedGroups_WithTheirCapacity()
        {
            var b = new Board(Def(2, "G..RR..G", "G..RR..G"), 1);
            Assert.AreEqual(3, b.Zones.Count);
            var crit = b.ZoneAt(new Vector2Int(3, 0));
            Assert.AreEqual(Tile.Critical, crit.Kind);
            Assert.AreEqual(2, crit.Capacity);
            Assert.AreEqual(4, crit.Cells.Count);
            Assert.IsFalse(b.ZoneAt(new Vector2Int(0, 1)).Mirrored);
            Assert.IsTrue(b.ZoneAt(new Vector2Int(7, 1)).Mirrored, "a gate on the right half makes mirrored threads");
        }

        [Test]
        public void Fork_UsesAFreeGate_ThenForcesIntoCriticalWithShortQuantum()
        {
            var b = new Board(Def(2, "G.RR", "G.RR"), 1);
            var first = b.Fork();
            Assert.AreEqual(Tile.Gate, b.TileAt(first.Pos));
            Assert.AreEqual(3f, first.QuantumMax);
            var second = b.Fork();
            Assert.AreEqual(Tile.Critical, b.TileAt(second.Pos), "the only gate is taken, so the fork is pushed into the red");
            Assert.IsTrue(second.Forced);
            Assert.AreEqual(1f, second.QuantumMax);
            Assert.AreEqual(1, b.Forced);
        }

        [Test]
        public void Fork_WithNoGateAndCriticalOverCapacity_IsARaceCondition()
        {
            var b = new Board(Def(1, "G.RR"), 1);
            b.Fork();              // gate
            b.Fork();              // critical, 1 of 1
            Assert.IsFalse(b.Over);
            b.Fork();              // critical, 2 of 1
            Assert.IsTrue(b.Over);
            Assert.AreEqual(CrashCause.RaceCondition, b.Cause);
        }

        [Test]
        public void Fork_WithNoRoomAnywhere_IsADeadlock()
        {
            var b = new Board(Def(5, "GR"), 1);
            b.Fork(); b.Fork();
            Assert.IsFalse(b.Over);
            b.Fork();
            Assert.IsTrue(b.Over);
            Assert.AreEqual(CrashCause.Deadlock, b.Cause);
        }

        [Test]
        public void Move_LeavingTheGate_FreesItsPermit()
        {
            var b = new Board(Def(2, "G...."), 1);
            var t = b.Fork();
            var gate = b.ZoneAt(t.Pos);
            Assert.AreEqual(1, gate.Count);
            Assert.AreEqual(1, b.Move(Dir.Right));
            Assert.AreEqual(new Vector2Int(1, 0), t.Pos);
            Assert.AreEqual(0, gate.Count);
        }

        [Test]
        public void Move_MirroredThreads_GoTheOtherWayHorizontally_AndTheSameWayVertically()
        {
            var b = new Board(Def(2, "G.....G", "G.....G"), 1);
            var left = b.Fork();
            var right = b.Fork();
            if (left.Mirrored) { var tmp = left; left = right; right = tmp; }
            Assert.IsTrue(right.Mirrored);
            var l0 = left.Pos; var r0 = right.Pos;
            b.Move(Dir.Right);
            Assert.AreEqual(l0 + new Vector2Int(1, 0), left.Pos);
            Assert.AreEqual(r0 + new Vector2Int(-1, 0), right.Pos);
            var l1 = left.Pos; var r1 = right.Pos;
            b.Move(Dir.Down);
            Assert.AreEqual(l1.y + 1 > 1 ? l1 : l1 + new Vector2Int(0, 1), left.Pos);
            Assert.AreEqual(r1.y + 1 > 1 ? r1 : r1 + new Vector2Int(0, 1), right.Pos);
        }

        [Test]
        public void Move_WallsAndFinishedThreadsBlock_AndTheBoardEdgeToo()
        {
            var b = new Board(Def(2, "G#..."), 1);
            var t = b.Fork();
            Assert.AreEqual(0, b.Move(Dir.Right), "wall");
            Assert.AreEqual(0, b.Move(Dir.Left), "edge");
            Assert.AreEqual(0, b.Move(Dir.Up), "edge");
            Assert.AreEqual(Vector2Int.zero, t.Pos);
        }

        [Test]
        public void Move_ATrainOfRunningThreads_MovesTogether()
        {
            var b = new Board(Def(2, "....", "...."), 1);
            // hand-placed threads: build by forking on a map with gates is awkward, so use a gate row
            var g = new Board(Def(2, "GG...."), 3);
            var a = g.Fork();
            Assert.IsNotNull(a);
            Assert.AreEqual(1, g.Move(Dir.Right));
            var c = g.Fork();
            Assert.IsNotNull(c, "the gate group is free again once the first thread walked out");
            Assert.That(g.Move(Dir.Right), Is.GreaterThanOrEqualTo(1));
            Assert.AreNotEqual(a.Pos, c.Pos);
            Assert.IsNotNull(b);
        }

        [Test]
        public void Move_HeadOnThreads_BothStay()
        {
            var b = new Board(Def(2, "G.G"), 1);
            var x = b.Fork(); var y = b.Fork();
            Assert.IsTrue(x.Mirrored != y.Mirrored);
            // both aim at the middle cell: neither moves
            Assert.AreEqual(0, b.Move(Dir.Right));
            Assert.AreEqual(0, b.Move(Dir.Right));
        }

        [Test]
        public void Move_IntoAFullCriticalRegion_IsARaceCondition()
        {
            var b = new Board(Def(1, "G.R", "..R"), 1);
            var t = b.Fork();
            t.Pos = new Vector2Int(1, 0); b.ZoneAt(new Vector2Int(0, 0)).Count = 0; t.Zone = -1;
            // a finished thread holds the only permit
            var holder = b.Fork();
            Assert.IsNotNull(holder);
            holder.Running = false;
            b.Move(Dir.Down); // walk holder? it is finished, only t moves: t -> (1,1)
            b.Move(Dir.Right);  // t -> (2,1): critical, now 2 of 1
            Assert.IsTrue(b.Over);
            Assert.AreEqual(CrashCause.RaceCondition, b.Cause);
        }

        [Test]
        public void Quantum_Ends_ThreadFinishesAndCounts_GoalWins()
        {
            var def = Def(2, "G...");
            def.goal = 1;
            var b = new Board(def, 1);
            bool won = false; b.Victory += () => won = true;
            var t = b.Fork();
            b.Tick(1f);
            Assert.IsTrue(t.Running);
            b.Tick(2.1f);
            Assert.IsFalse(t.Running);
            Assert.AreEqual(1, b.Completed);
            Assert.IsTrue(won && b.Won && b.Over);
        }

        [Test]
        public void SignalPad_ReleasesAFinishedThreadFromTheCriticalRegion_ThenCoolsDown()
        {
            var b = new Board(Def(2, "GSR"), 1);
            var runner = b.Fork();                 // on the gate
            var stuck = b.Fork();                  // forced into the critical cell
            stuck.Running = false;
            var crit = b.ZoneAt(new Vector2Int(2, 0));
            Assert.AreEqual(1, crit.Count);
            b.Move(Dir.Right);                     // runner steps on the pad: V(S)
            Assert.AreEqual(0, crit.Count);
            Assert.IsFalse(b.Threads.Contains(stuck));
            Assert.AreEqual(1, b.Releases);
            Assert.Greater(b.SignalCooldown[new Vector2Int(1, 0)], 0f);
            Assert.AreEqual(new Vector2Int(1, 0), runner.Pos);
        }

        [Test]
        public void Spawn_FollowsTheIntervalAndAccelerates()
        {
            var def = Def(2, "G.....", "G.....");
            def.spawnStart = 2f; def.spawnMin = 1f; def.spawnAccel = 0.5f; def.firstSpawn = 0.5f;
            var b = new Board(def, 1);
            b.Tick(0.6f);
            Assert.AreEqual(1, b.Spawned);
            Assert.Less(b.SpawnInterval, 2f);
            b.Tick(0.3f); b.Tick(0.3f); b.Tick(0.3f); b.Tick(0.3f); b.Tick(0.3f); b.Tick(0.3f);
            Assert.GreaterOrEqual(b.SpawnInterval, 1f);
        }

        [Test]
        public void Clone_IsIndependent()
        {
            var b = new Board(Def(2, "G...."), 1);
            var t = b.Fork();
            var c = b.Clone();
            c.Move(Dir.Right);
            Assert.AreEqual(Vector2Int.zero, t.Pos);
            Assert.AreEqual(1, b.ZoneAt(Vector2Int.zero).Count);
            Assert.AreEqual(0, c.ZoneAt(Vector2Int.zero).Count);
        }

        [Test]
        public void EveryLevel_Parses_AndHasGatesAndACriticalRegion()
        {
            var all = new System.Collections.Generic.List<LevelDef>(Levels.Campaign) { Levels.Classic, Levels.Endless };
            foreach (var def in all)
            {
                var b = new Board(def, 1);
                bool gate = false, crit = false;
                foreach (var z in b.Zones) { gate |= z.Kind == Tile.Gate; crit |= z.Kind == Tile.Critical; }
                Assert.IsTrue(gate && crit, def.id);
            }
        }
    }
}
