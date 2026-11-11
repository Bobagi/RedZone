using RedZone.Audio;
using RedZone.Core;
using RedZone.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace RedZone.Game
{
    public enum State { Title, Levels, Primer, Options, Credits, Intro, Playing, Paused, Result }

    /// <summary>
    /// The whole game in one scene: title, campaign, a run, results. Owns the board being played and moves between
    /// screens; the UI (GameUI) only draws and calls back here.
    /// </summary>
    public sealed class GameFlow : MonoBehaviour
    {
        public static GameFlow I { get; private set; }

        public State State { get; private set; }
        public Board Board { get; private set; }
        public LevelDef Level { get; private set; }
        public GameUI Ui { get; private set; }
        public bool LastBest { get; private set; }
        public int LastStars { get; private set; }

        Tutorial tutorial;
        State optionsReturn = State.Title;
        float endTimer = -1f, introTime;
        int seed = 1;

        void Awake()
        {
            I = this;
            Time.timeScale = 1f;
            Settings.Apply();
            Ui = gameObject.AddComponent<GameUI>();
            Ui.Build();
            Loc.Changed += OnLanguage;
        }

        void OnDestroy() { Loc.Changed -= OnLanguage; if (I == this) I = null; }

        void Start()
        {
            ShowTitle();
            foreach (var a in System.Environment.GetCommandLineArgs())
                if (a == "-autoplay") gameObject.AddComponent<AutoPlayer>();
        }

        void OnLanguage()
        {
            Ui.Rebuild();
            if (Board != null) Ui.Board.Show(Board);
            Ui.Show(State);
        }

        // ------------------------------------------------------------------ screens

        public void ShowTitle()
        {
            EndBoard();
            State = State.Title;
            Ui.Show(State);
            Sfx.Music("menu");
            Sfx.Duck(1f);
        }

        public void ShowLevels() { EndBoard(); State = State.Levels; Ui.Show(State); Sfx.Music("menu"); Sfx.Duck(1f); }
        public void ShowPrimer() { State = State.Primer; Ui.Show(State); }
        public void ShowCredits() { State = State.Credits; Ui.Show(State); }

        public void ShowOptions()
        {
            optionsReturn = State == State.Paused ? State.Paused : State.Title;
            State = State.Options;
            Ui.Show(State);
        }

        /// <summary>Esc / gamepad B on a menu screen.</summary>
        public void Back()
        {
            switch (State)
            {
                case State.Levels: case State.Primer: case State.Credits: ShowTitle(); break;
                case State.Options:
                    if (optionsReturn == State.Paused) { State = State.Paused; Ui.Show(State); }
                    else ShowTitle();
                    break;
                case State.Paused: Resume(); break;
                case State.Result: ShowLevels(); break;
            }
        }

        // ------------------------------------------------------------------ a run

        public void StartLevel(LevelDef def)
        {
            EndBoard();
            Level = def;
            Board = new Board(def, seed++ * 7919 + System.Environment.TickCount % 1000);
            Board.Victory += OnEnd;
            Board.Crashed += (c, z) => OnEnd();
            Ui.Board.Show(Board);
            tutorial = def.tutorial ? new Tutorial(Board, Ui) : null;
            State = State.Intro;
            introTime = 0f;
            Ui.Show(State);
            Sfx.Music("game");
            Sfx.Duck(0.6f);
            Sfx.Play("ui_start", 0.6f);
            GameInput.ResetHeld();
        }

        public void Restart() { if (Level != null) StartLevel(Level); }

        public void Next()
        {
            int i = Levels.IndexOf(Level);
            if (i >= 0 && i + 1 < Levels.Campaign.Length) StartLevel(Levels.Campaign[i + 1]);
            else ShowLevels();
        }

        void BeginPlay()
        {
            State = State.Playing;
            Ui.Show(State);
            Sfx.Duck(1f);
            GameInput.ResetHeld();
            tutorial?.Begin();
        }

        public void Pause()
        {
            if (State != State.Playing) return;
            State = State.Paused;
            Ui.Show(State);
            Sfx.Duck(0.35f);
            Sfx.Play("switch_1", 0.5f, 0.8f);
        }

        public void Resume()
        {
            if (State != State.Paused) return;
            State = State.Playing;
            Ui.Show(State);
            Sfx.Duck(1f);
            GameInput.ResetHeld();
        }

        void OnEnd()
        {
            endTimer = Board.Won ? 0.9f : 1.6f;
            Ui.Callout(null);
            if (Board.Won) { Sfx.Play("win", 0.8f); Ui.Flash(Theme.A(Theme.Gate, 0.35f)); }
            else { Sfx.Play("panic", 0.8f); Ui.Flash(Theme.A(Theme.Critical, 0.55f)); }
        }

        void ShowResult()
        {
            State = State.Result;
            LastStars = Progress.StarsFor(Board);
            int score = Board.Completed;
            LastBest = Progress.Record(Level, Board.Won, LastStars, score);
            Sfx.Duck(0.45f);
            Ui.Show(State);
        }

        void EndBoard()
        {
            Board = null;
            tutorial = null;
            endTimer = -1f;
            Ui.Board.Clear();
            Ui.Callout(null);
        }

        void Update()
        {
            var es = EventSystem.current;
            // the arrows belong to the board while a run is on screen, not to (hidden) buttons
            if (es) es.sendNavigationEvents = State != State.Playing && State != State.Intro;

            switch (State)
            {
                case State.Intro:
                    introTime += Time.unscaledDeltaTime;
                    if (introTime > 0.35f && (GameInput.Any() || GameInput.ReadMove() != null)) BeginPlay();
                    break;

                case State.Playing:
                    if (endTimer >= 0f)
                    {
                        Board.Tick(0f);
                        endTimer -= Time.deltaTime;
                        if (endTimer < 0f) ShowResult();
                        break;
                    }
                    if (GameInput.Pause()) { Pause(); break; }
                    if (GameInput.Restart()) { Restart(); break; }
                    var d = GameInput.ReadMove();
                    if (d != null && (tutorial == null || tutorial.AllowMove)) Board.Move(d.Value);
                    tutorial?.Tick(Time.deltaTime);
                    Board.Tick(Time.deltaTime);
                    break;

                case State.Paused:
                    if (GameInput.Pause()) Resume();
                    break;

                default:
                    if (BackPressed()) Back();
                    break;
            }
        }

        static bool BackPressed()
        {
            var k = Keyboard.current; var g = Gamepad.current;
            return (k != null && k.escapeKey.wasPressedThisFrame) || (g != null && g.buttonEast.wasPressedThisFrame);
        }

        /// <summary>Used by the automated playtest: the intro waits for a key that a robot never presses.</summary>
        public void SkipIntro() { if (State == State.Intro) BeginPlay(); }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
