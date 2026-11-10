namespace RedZone.Core
{
    /// <summary>
    /// One board. The map is a list of rows, top row first, one character per cell:
    /// '.' free, 'G' entry gate (a binary semaphore per connected group), 'R' critical region (counting semaphore
    /// with <see cref="capacity"/> permits per connected group), 'S' signal pad (V operation), '#' wall.
    /// Gates on the right half of the board create mirrored threads when <see cref="mirrorRight"/> is on.
    /// </summary>
    public sealed class LevelDef
    {
        public string id;
        public string titleKey;       // Loc key of the level name
        public string conceptKey;     // Loc key of the one-line idea the level teaches
        public string[] map;
        public int capacity = 2;
        public float quantum = 4f;        // seconds a new thread runs
        public float forcedQuantum = 1f;  // seconds a thread pushed into the critical region runs
        public float spawnStart = 4f;     // seconds between forks at the start
        public float spawnMin = 2f;       // fastest fork interval
        public float spawnAccel = 0.02f;  // seconds removed from the interval per second of play
        public float firstSpawn = 1f;
        public int goal;                  // threads to complete; 0 = endless
        public bool mirrorRight = true;
        public float signalCooldown = 6f;
        public bool tutorial;
        public bool campaign = true;

        public int Width => map[0].Length;
        public int Height => map.Length;
    }
}
