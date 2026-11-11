using RedZone.Core;
using RedZone.UI;

namespace RedZone.Game
{
    /// <summary>
    /// Level 1 teaches by doing (Wilkins Pest Control's rule: the tutorial, not a manual, explains the game).
    /// The clock stands still on the first thread until the player moves it; each later tip waits for the thing it
    /// talks about to happen on the board.
    /// </summary>
    public sealed class Tutorial
    {
        readonly Board board;
        readonly GameUI ui;
        int step = -1;
        float t;

        public bool AllowMove => true;

        public Tutorial(Board b, GameUI ui) { board = b; this.ui = ui; }

        public void Begin() => step = 0;

        void Go(int s, string key) { step = s; t = 0f; ui.Callout(key); }

        public void Tick(float dt)
        {
            if (step < 0 || board.Over) return;
            t += dt;
            switch (step)
            {
                case 0:     // wait for the first fork, then freeze time and ask for a move
                    if (board.Threads.Count > 0) { board.Hold = true; Go(1, "tut.move"); }
                    break;
                case 1:
                    if (board.Moves > 0) { board.Hold = false; Go(2, "tut.gate"); }
                    break;
                case 2:
                    if (board.Threads.Count > 0 && board.TileAt(board.Threads[0].Pos) != Tile.Gate) Go(3, "tut.quantum");
                    break;
                case 3:
                    if (board.Completed > 0) Go(4, "tut.finish");
                    break;
                case 4:
                    if (board.Spawned > 1 && t > 3f) Go(5, "tut.same");
                    break;
                case 5:
                    if (t > 6f) Go(6, "tut.crit");
                    break;
                case 6:
                    if (t > 7f) { step = 7; ui.Callout(null); }
                    break;
            }
        }
    }
}
