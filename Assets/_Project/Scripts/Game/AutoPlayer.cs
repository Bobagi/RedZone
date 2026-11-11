using System.Collections;
using System.IO;
using System.Text;
using RedZone.Core;
using UnityEngine;

namespace RedZone.Game
{
    /// <summary>
    /// Robot playtest (the Wilkins Pest Control AutoPlayer): started with "-autoplay [-autoplayOut dir]" on a build.
    /// Visits every screen, plays every level with the Bot, takes screenshots and writes autoplay.log, then quits.
    /// </summary>
    public sealed class AutoPlayer : MonoBehaviour
    {
        string outDir;
        readonly StringBuilder log = new StringBuilder();

        void Start()
        {
            var args = System.Environment.GetCommandLineArgs();
            outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "autoplay"));
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-autoplayOut") outDir = args[i + 1];
            Directory.CreateDirectory(outDir);
            StartCoroutine(Run());
        }

        void Log(string s) { log.AppendLine(s); Debug.Log("[AutoPlayer] " + s); File.WriteAllText(Path.Combine(outDir, "autoplay.log"), log.ToString()); }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(outDir, name + ".png"));
            yield return null;
            yield return null;
        }

        IEnumerator Run()
        {
            var f = GameFlow.I;
            Log("screen " + Screen.width + "x" + Screen.height);
            yield return new WaitForSecondsRealtime(2.5f);
            yield return Shot("01_title");
            f.ShowLevels(); yield return new WaitForSecondsRealtime(0.6f); yield return Shot("02_levels");
            f.ShowPrimer(); yield return new WaitForSecondsRealtime(0.6f); yield return Shot("03_primer");
            f.ShowOptions(); yield return new WaitForSecondsRealtime(0.6f); yield return Shot("04_options");
            f.ShowCredits(); yield return new WaitForSecondsRealtime(0.6f); yield return Shot("05_credits");

            int n = 0;
            foreach (var def in Levels.Campaign) yield return Play(def, ++n, 300f);
            yield return Play(Levels.Classic, ++n, 400f);
            yield return Play(Levels.Endless, ++n, 150f);

            Time.timeScale = 1f;
            f.ShowTitle();
            yield return new WaitForSecondsRealtime(1f);
            Log("done");
            Application.Quit();
        }

        IEnumerator Play(LevelDef def, int n, float timeout)
        {
            var f = GameFlow.I;
            f.StartLevel(def);
            yield return new WaitForSecondsRealtime(0.8f);
            string tag = n.ToString("00") + "_" + def.id;
            if (n <= 3 || def == Levels.Classic) yield return Shot("10_" + tag + "_intro");
            f.SkipIntro();
            Time.timeScale = 3f;
            float played = 0f, next = 0f;
            bool shotPlaying = false, shotPause = false;
            while (f.State == State.Playing || f.State == State.Paused)
            {
                played += Time.deltaTime;
                if (f.State == State.Playing && played >= next && f.Board != null && !f.Board.Over)
                {
                    next = played + 0.12f;
                    var d = Bot.Choose(f.Board);
                    if (d != null) f.Board.Move(d.Value);
                }
                if (!shotPlaying && played > 14f)
                {
                    shotPlaying = true;
                    Time.timeScale = 0f;
                    yield return Shot("11_" + tag + "_play");
                    if (n == 3 && !shotPause)
                    {
                        shotPause = true;
                        f.Pause(); yield return new WaitForSecondsRealtime(0.4f); yield return Shot("12_pause"); f.Resume();
                    }
                    Time.timeScale = 3f;
                }
                if (played > timeout && def.goal == 0) break;
                if (played > timeout + 60f) { Log(def.id + " TIMEOUT"); break; }
                yield return null;
            }
            var b = f.Board;
            if (b != null)
                Log($"{def.id}: won={b.Won} cause={b.Cause} completed={b.Completed}/{def.goal} forced={b.Forced} releases={b.Releases} moves={b.Moves} t={b.Elapsed:0.0}s");
            Time.timeScale = 1f;
            float wait = 0f;
            while (f.State != State.Result && wait < 4f && def.goal > 0) { wait += Time.unscaledDeltaTime; yield return null; }
            yield return new WaitForSecondsRealtime(1.8f);     // the result stars pop in over ~1.2 s
            if (n <= 3 || def == Levels.Classic || f.State == State.Result) yield return Shot("13_" + tag + "_result");
        }
    }
}
