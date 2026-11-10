namespace RedZone.Core
{
    /// <summary>
    /// Every board of the game, and the only place gameplay numbers live (quantum, fork rhythm, goals).
    /// Campaign levels teach one idea each; Classic keeps the 2019 university rules; Endless has no goal.
    /// </summary>
    public static class Levels
    {
        public static readonly LevelDef[] Campaign =
        {
            new LevelDef
            {
                id = "l1", titleKey = "lvl.l1", conceptKey = "lvl.l1.c", tutorial = true,
                capacity = 2, quantum = 6f, spawnStart = 7f, spawnMin = 6f, spawnAccel = 0f, goal = 4, mirrorRight = false,
                map = new[]
                {
                    "G............",
                    "G............",
                    "G.....RR.....",
                    "G.....RR.....",
                    "G............",
                    "G............",
                    "G............",
                },
            },
            new LevelDef
            {
                id = "l2", titleKey = "lvl.l2", conceptKey = "lvl.l2.c",
                capacity = 1, quantum = 5f, spawnStart = 5.5f, spawnMin = 4.5f, spawnAccel = 0.02f, goal = 6, mirrorRight = false,
                map = new[]
                {
                    "G............",
                    "G............",
                    "G.....RR.....",
                    "G.....RR.....",
                    "G............",
                    "G............",
                    "G............",
                },
            },
            new LevelDef
            {
                id = "l3", titleKey = "lvl.l3", conceptKey = "lvl.l3.c",
                capacity = 2, quantum = 4.5f, spawnStart = 4.5f, spawnMin = 3.2f, spawnAccel = 0.02f, goal = 8,
                map = new[]
                {
                    "G............G",
                    "G............G",
                    "G............G",
                    "G.....RR.....G",
                    "G.....RR.....G",
                    "G............G",
                    "G............G",
                    "G............G",
                },
            },
            new LevelDef
            {
                id = "l4", titleKey = "lvl.l4", conceptKey = "lvl.l4.c",
                capacity = 2, quantum = 4.5f, spawnStart = 4.2f, spawnMin = 3f, spawnAccel = 0.02f, goal = 10,
                map = new[]
                {
                    "G......#.....G",
                    "G......#.....G",
                    "G............G",
                    "G..#..RR..#..G",
                    "G..#..RR..#..G",
                    "G............G",
                    "G.....#......G",
                    "G.....#......G",
                },
            },
            new LevelDef
            {
                id = "l5", titleKey = "lvl.l5", conceptKey = "lvl.l5.c",
                capacity = 1, quantum = 4.5f, spawnStart = 4f, spawnMin = 2.8f, spawnAccel = 0.02f, goal = 12, signalCooldown = 6f,
                map = new[]
                {
                    "G............G",
                    "G..S.........G",
                    "G............G",
                    "G.....RR.....G",
                    "G.....RR.....G",
                    "G............G",
                    "G.........S..G",
                    "G............G",
                },
            },
            new LevelDef
            {
                id = "l6", titleKey = "lvl.l6", conceptKey = "lvl.l6.c",
                capacity = 1, quantum = 4.2f, spawnStart = 3.8f, spawnMin = 2.6f, spawnAccel = 0.02f, goal = 14, signalCooldown = 6f,
                map = new[]
                {
                    "G............G",
                    "G............G",
                    "G...RR..RR...G",
                    "G...RR..RR...G",
                    "G............G",
                    "G......S.....G",
                    "G............G",
                    "G............G",
                },
            },
            new LevelDef
            {
                id = "l7", titleKey = "lvl.l7", conceptKey = "lvl.l7.c",
                capacity = 2, quantum = 4f, spawnStart = 3.6f, spawnMin = 2.4f, spawnAccel = 0.025f, goal = 16, signalCooldown = 5f,
                map = new[]
                {
                    "#............#",
                    "#..S.........#",
                    "#....#..#....#",
                    "G....#RR#....G",
                    "G.....RR.....G",
                    "#....#..#....#",
                    "#.........S..#",
                    "#............#",
                },
            },
            new LevelDef
            {
                id = "l8", titleKey = "lvl.l8", conceptKey = "lvl.l8.c",
                capacity = 2, quantum = 3.8f, spawnStart = 3.2f, spawnMin = 1.9f, spawnAccel = 0.025f, goal = 24, signalCooldown = 5f,
                map = new[]
                {
                    "G..............G",
                    "G...#......#...G",
                    "G....RR..S.#...G",
                    "G....RR........G",
                    "G......##......G",
                    "G........RR....G",
                    "G...#.S..RR....G",
                    "G...#......#...G",
                    "G..............G",
                },
            },
        };

        /// <summary>The 2019 university rules: same board, same numbers, 116 threads to win.</summary>
        public static readonly LevelDef Classic = new LevelDef
        {
            id = "classic", titleKey = "lvl.classic", conceptKey = "lvl.classic.c", campaign = false,
            capacity = 2, quantum = 3f, forcedQuantum = 1f, spawnStart = 3.15f, spawnMin = 0.7f, spawnAccel = 0.3f, firstSpawn = 0.5f, goal = 116,
            map = new[]
            {
                "G............G",
                "G............G",
                "G............G",
                "G............G",
                "G.....RR.....G",
                "G.....RR.....G",
                "G............G",
                "G............G",
                "G............G",
                "G............G",
            },
        };

        /// <summary>No goal: how many threads does the kernel survive?</summary>
        public static readonly LevelDef Endless = new LevelDef
        {
            id = "endless", titleKey = "lvl.endless", conceptKey = "lvl.endless.c", campaign = false,
            capacity = 2, quantum = 4f, spawnStart = 3.6f, spawnMin = 1.2f, spawnAccel = 0.02f, goal = 0, signalCooldown = 7f,
            map = new[]
            {
                "G..............G",
                "G..............G",
                "G...S..........G",
                "G..............G",
                "G......RR......G",
                "G......RR......G",
                "G..............G",
                "G..........S...G",
                "G..............G",
                "G..............G",
            },
        };

        public static LevelDef Find(string id)
        {
            foreach (var l in Campaign) if (l.id == id) return l;
            if (id == Classic.id) return Classic;
            if (id == Endless.id) return Endless;
            return null;
        }

        public static int IndexOf(LevelDef def) => System.Array.IndexOf(Campaign, def);
    }
}
