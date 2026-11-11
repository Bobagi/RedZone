using System.Collections.Generic;
using RedZone.Audio;
using RedZone.Core;
using RedZone.Game;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace RedZone.UI
{
    /// <summary>
    /// Every screen of the game, built in code (Wilkins Pest Control's GameUI model): title with a live board,
    /// campaign, primer, options, credits, the run HUD, pause, results and the tutorial callout.
    /// Changing the language rebuilds it all.
    /// </summary>
    public sealed class GameUI : MonoBehaviour
    {
        public BoardView Board { get; private set; }

        Canvas canvas;
        RectTransform root, boardArea, title, levels, primer, options, credits, intro, hud, pause, result, calloutBox;
        Text calloutText;
        Image flash;
        float flashA;
        readonly Dictionary<RectTransform, GameObject> firstButton = new Dictionary<RectTransform, GameObject>();
        GameObject remembered;
        RectTransform current;

        // HUD (texts rewritten only when the level or the count changes)
        (LevelDef def, int done) hudKey;
        Text hudTitle, hudProgress, hudSpawn, hudBest;
        Image hudProgressFill, hudSpawnFill;

        // title screen live board
        BoardView attract;
        Board attractBoard;
        float attractStep, attractReset;
        static readonly LevelDef AttractDef = new LevelDef
        {
            id = "attract", capacity = 2, quantum = 4f, spawnStart = 1.8f, spawnMin = 1.8f, spawnAccel = 0f, firstSpawn = 0.3f, goal = 0, signalCooldown = 4f,
            map = new[]
            {
                "G..........G",
                "G..S.......G",
                "G..........G",
                "G....RR....G",
                "G....RR....G",
                "G..........G",
                "G.......S..G",
                "G..........G",
            },
        };

        // primer
        int primerPage;
        RectTransform primerBody;

        // options
        bool resetArmed;

        public void Build()
        {
            EnsureEventSystem();
            canvas = Ui.Canvas("[UI]", 0, transform);
            root = (RectTransform)canvas.transform;

            // moving layers get their own nested canvas: a drifting packet or a sliding thread then rebuilds only its
            // own layer instead of every text and button of the screen, every frame
            Isolate(Background.Create(root).GetComponent<RectTransform>(), false);
            boardArea = Ui.Node(root, "Board Area", Vector2.zero, Vector2.one, new Vector2(70, 120), new Vector2(-70, -170));
            Isolate(boardArea, false);
            Board = BoardView.Create(boardArea);

            title = BuildTitle();
            levels = BuildLevels();
            primer = BuildPrimer();
            options = BuildOptions();
            credits = BuildCredits();
            hud = BuildHud();
            Isolate(hud, true);
            intro = Ui.Fill(root, "Intro");
            pause = BuildPause();
            result = Ui.Fill(root, "Result");
            BuildCallout();

            flash = Ui.Image(root, "Flash", new Color(1, 1, 1, 0), null);
            var scan = Ui.Image(root, "Scanlines", new Color(1, 1, 1, 0.07f), Theme.Scanlines);
            scan.type = Image.Type.Tiled;
            HideAll();
        }

        public void Rebuild()
        {
            Board?.Clear();
            if (canvas) DestroyImmediate(canvas.gameObject);
            firstButton.Clear();
            attract = null; attractBoard = null;
            Build();
        }

        static void Isolate(RectTransform r, bool clickable)
        {
            if (!r.GetComponent<Canvas>()) r.gameObject.AddComponent<Canvas>();
            if (clickable && !r.GetComponent<GraphicRaycaster>()) r.gameObject.AddComponent<GraphicRaycaster>();
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            DontDestroyOnLoad(es);
        }

        void HideAll()
        {
            foreach (var r in new[] { title, levels, primer, options, credits, intro, hud, pause, result }) r.gameObject.SetActive(false);
        }

        public void Show(State s)
        {
            HideAll();
            boardArea.gameObject.SetActive(false);
            // the tutorial's callout sits under the board: give it room instead of covering the bottom row
            var lvl = GameFlow.I != null ? GameFlow.I.Level : null;
            boardArea.offsetMin = new Vector2(70, lvl != null && lvl.tutorial ? 270 : 120);
            current = null;
            switch (s)
            {
                case State.Title: Open(title); StartAttract(); break;
                case State.Levels: RefreshLevels(); Open(levels); break;
                case State.Primer: primerPage = 0; ShowPrimerPage(); Open(primer); break;
                case State.Options: resetArmed = false; Open(options); break;
                case State.Credits: Open(credits); break;
                case State.Intro: boardArea.gameObject.SetActive(true); hud.gameObject.SetActive(true); BuildIntro(); intro.gameObject.SetActive(true); break;
                case State.Playing: boardArea.gameObject.SetActive(true); hud.gameObject.SetActive(true); Deselect(); break;
                case State.Paused: boardArea.gameObject.SetActive(true); hud.gameObject.SetActive(true); Open(pause); break;
                case State.Result: boardArea.gameObject.SetActive(true); BuildResult(); Open(result); break;
            }
            if (s != State.Title) StopAttract();
            if (s != State.Playing && s != State.Paused && s != State.Intro) Callout(null);
            calloutBox.gameObject.SetActive(calloutText.text.Length > 0 && (s == State.Playing));
        }

        void Open(RectTransform screen)
        {
            screen.gameObject.SetActive(true);
            current = screen;
            remembered = null;
            if (firstButton.TryGetValue(screen, out var b) && b) Ui.Select(b);
        }

        static void Deselect() { if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null); }

        void LateUpdate()
        {
            // keyboard and gamepad never lose the menu: a click on empty space restores the last selection
            var es = EventSystem.current;
            if (!es || current == null || !current.gameObject.activeInHierarchy) return;
            var sel = es.currentSelectedGameObject;
            if (sel && sel.activeInHierarchy && sel.transform.IsChildOf(current)) { remembered = sel; return; }
            var want = remembered && remembered.activeInHierarchy ? remembered : (firstButton.TryGetValue(current, out var b) ? b : null);
            if (want) es.SetSelectedGameObject(want);
        }

        // ------------------------------------------------------------------ title

        RectTransform BuildTitle()
        {
            var s = Ui.Fill(root, "Title");
            var left = Ui.Node(s, "Left", new Vector2(0, 0), new Vector2(0.48f, 1), new Vector2(120, 0), Vector2.zero);

            // the logo: RED ZONE with a red offset copy behind it (a nod to the 2019 red title screen)
            var logo = Ui.Box(left, "Logo", new Vector2(0, 1), new Vector2(380, -210), new Vector2(760, 200));
            var back = Ui.Text(logo, "Echo", "RED ZONE", 170, Theme.Display, Theme.A(Theme.Critical, 0.85f), TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, new Vector2(8, -6), new Vector2(8, -6));
            back.gameObject.AddComponent<Glitch>();
            Ui.Text(logo, "Main", "RED ZONE", 170, Theme.Display, Theme.Text, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var sub = Ui.Text(left, "Sub", Loc.T("title.sub"), 40, Theme.Mono, Theme.Yellow, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -360), new Vector2(0, -300));
            Ui.Shadow(sub);

            var items = new List<(string key, UnityEngine.Events.UnityAction act)>
            {
                ("menu.campaign", () => GameFlow.I.ShowLevels()),
                ("menu.classic", () => GameFlow.I.StartLevel(Levels.Classic)),
                ("menu.endless", () => GameFlow.I.StartLevel(Levels.Endless)),
                ("menu.how", () => GameFlow.I.ShowPrimer()),
                ("menu.options", () => GameFlow.I.ShowOptions()),
                ("menu.credits", () => GameFlow.I.ShowCredits()),
            };
            if (Application.platform != RuntimePlatform.WebGLPlayer) items.Add(("menu.quit", () => GameFlow.I.Quit()));
            float y = -440f;
            foreach (var it in items)
            {
                var b = Ui.Button(left, Loc.T(it.key), it.act, new Vector2(0, 1), new Vector2(260, y), new Vector2(520, 70), 38);
                if (!firstButton.ContainsKey(s)) firstButton[s] = b.gameObject;
                y -= 84f;
            }
            var stars = Ui.Text(left, "Stars", "", 32, Theme.Mono, Theme.Yellow, TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 40), new Vector2(0, 90));
            stars.gameObject.AddComponent<LiveText>().get = () => Loc.F("menu.stars", Progress.TotalStars, Levels.Campaign.Length * 3);
            Ui.Text(s, "Footer", Loc.T("menu.footer"), 24, Theme.Light, Theme.Dim, TextAnchor.LowerRight, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 24), new Vector2(-40, 70));

            // language at a glance (also in Options): PT / EN / ES
            var lang = Ui.Button(s, Loc.Codes[Loc.Language].Substring(0, 2).ToUpperInvariant(), () => Loc.SetLanguage(Loc.Language + 1),
                new Vector2(1, 1), new Vector2(-100, -64), new Vector2(150, 70), 34);
            lang.GetComponent<MenuButtonFx>().marker.enabled = false;
            lang.name = "Language";

            var demoArea = Ui.Node(s, "Live board", new Vector2(0.5f, 0.12f), new Vector2(0.97f, 0.9f), Vector2.zero, Vector2.zero);
            attract = BoardView.Create(demoArea);
            attract.Silent = true;
            var cap = Ui.Text(s, "Live caption", Loc.T("title.live"), 26, Theme.Mono, Theme.Dim, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.05f), new Vector2(0.97f, 0.11f), Vector2.zero, Vector2.zero);
            Ui.Shadow(cap);
            return s;
        }

        void StartAttract()
        {
            if (attract == null) return;
            attractBoard = new Board(AttractDef, Random.Range(1, 99999));
            attract.Show(attractBoard);
            attractReset = -1f;
        }

        void StopAttract() { attract?.Clear(); attractBoard = null; }

        void TickAttract(float dt)
        {
            if (attractBoard == null) return;
            if (attractBoard.Over)
            {
                if (attractReset < 0f) attractReset = 2f;
                attractReset -= dt;
                if (attractReset <= 0f) StartAttract();
                return;
            }
            attractStep -= dt;
            if (attractStep <= 0f)
            {
                attractStep = 0.22f;
                var d = Bot.Choose(attractBoard, 0.85f);
                if (d != null) attractBoard.Move(d.Value);
            }
            attractBoard.Tick(dt);
        }

        // ------------------------------------------------------------------ campaign

        readonly List<(LevelDef def, Button b, Image[] stars, Text best)> cards = new List<(LevelDef, Button, Image[], Text)>();

        RectTransform BuildLevels()
        {
            var s = Ui.Fill(root, "Levels");
            Header(s, Loc.T("levels.title"));
            cards.Clear();
            const float w = 400, h = 250, gx = 430, gy = 280;
            for (int i = 0; i < Levels.Campaign.Length; i++)
            {
                var def = Levels.Campaign[i];
                int col = i % 4, row = i / 4;
                var pos = new Vector2((col - 1.5f) * gx, 170 - row * gy);
                cards.Add(Card(s, def, (i + 1).ToString("00"), pos, new Vector2(w, h)));
            }
            var c1 = Card(s, Levels.Classic, "2019", new Vector2(-gx * 0.5f - 95, 170 - 2 * gy + 20), new Vector2(w + 200, 200));
            var c2 = Card(s, Levels.Endless, "∞", new Vector2(gx * 0.5f + 95, 170 - 2 * gy + 20), new Vector2(w + 200, 200));
            c2.Item2.GetComponent<MenuButtonFx>().value.font = Theme.Display;
            cards.Add(c1); cards.Add(c2);
            firstButton[s] = cards[0].b.gameObject;
            var back = Ui.Button(s, Loc.T("menu.back"), () => GameFlow.I.Back(), new Vector2(0, 0), new Vector2(200, 70), new Vector2(260, 64), 32);
            back.name = "Back";
            return s;
        }

        (LevelDef, Button, Image[], Text) Card(RectTransform parent, LevelDef def, string number, Vector2 pos, Vector2 size)
        {
            var b = Ui.Button(parent, "", () => GameFlow.I.StartLevel(def), new Vector2(0.5f, 0.5f), pos, size, 30);
            var fx = b.GetComponent<MenuButtonFx>();
            fx.marker.enabled = false;
            var num = Ui.Text(b.transform, "Number", number, 64, Theme.Mono, Theme.Yellow, TextAnchor.UpperLeft, Vector2.zero, Vector2.one, new Vector2(26, 0), new Vector2(0, -10));
            var name = Ui.Text(b.transform, "Name", Loc.T(def.titleKey), 40, Theme.Display, Theme.Text, TextAnchor.UpperLeft, Vector2.zero, Vector2.one, new Vector2(26, 0), new Vector2(-20, -78));
            var concept = Ui.Text(b.transform, "Concept", Loc.T(def.conceptKey), 24, Theme.Light, Theme.Dim, TextAnchor.UpperLeft, Vector2.zero, Vector2.one, new Vector2(26, 0), new Vector2(-20, -132));
            fx.label = name; fx.value = num; fx.extra = new[] { concept };
            var stars = new Image[def.campaign ? 3 : 0];
            for (int i = 0; i < stars.Length; i++)
            {
                var sr = Ui.Box(b.transform, "Star", new Vector2(1, 1), new Vector2(-40 - (2 - i) * 46, -40), new Vector2(42, 42));
                stars[i] = Ui.Image(sr, "S", Theme.Yellow, Theme.Star);
            }
            fx.icons = stars;
            var best = Ui.Text(b.transform, "Best", "", 26, Theme.Mono, Theme.Dim, TextAnchor.LowerRight, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-24, 14));
            return (def, b, stars, best);
        }

        void RefreshLevels()
        {
            foreach (var (def, b, stars, best) in cards)
            {
                int idx = Levels.IndexOf(def);
                bool open = idx < 0 || Progress.Unlocked(idx);
                b.interactable = open;
                int got = Progress.Stars(def.id);
                for (int i = 0; i < stars.Length; i++)
                {
                    stars[i].sprite = i < got ? Theme.Star : Theme.StarHollow;
                    stars[i].color = Theme.A(Theme.Yellow, open ? (i < got ? 1f : 0.45f) : 0.15f);
                }
                int rec = Progress.Best(def.id);
                best.text = !open ? Loc.T("levels.locked") : def.goal == 0 || !def.campaign ? (rec > 0 ? Loc.F("levels.best", rec) : "") : "";
                var fx = b.GetComponent<MenuButtonFx>();
                fx.label.color = open ? Theme.Text : Theme.Dim;
            }
            // first unlocked level not yet finished
            GameObject pick = cards[0].b.gameObject;
            for (int i = 0; i < Levels.Campaign.Length; i++)
                if (Progress.Unlocked(i)) pick = cards[i].b.gameObject;
            firstButton[levels] = pick;
        }

        void Header(RectTransform s, string text)
        {
            var t = Ui.Text(s, "Header", text, 84, Theme.Display, Theme.Text, TextAnchor.UpperCenter, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -150), new Vector2(0, -40));
            Ui.Shadow(t, 3f);
            var line = Ui.Panel(s, "Rule", Theme.Critical, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-90, -162), new Vector2(90, -156), false);
            line.raycastTarget = false;
        }

        // ------------------------------------------------------------------ primer

        static readonly (string key, string[] diagram)[] PrimerPages =
        {
            ("primer.1", new[] { "G.T...", "G....." }),
            ("primer.2", new[] { "..RR..", "..TR.." }),
            ("primer.3", new[] { "F.....", "G..T.." }),
            ("primer.4", new[] { ".TRR..", "..TT.." }),
            ("primer.5", new[] { "..T.RR", ".S..FR" }),
            ("primer.6", new[] { "GT..MG", "G....G" }),
        };

        RectTransform BuildPrimer()
        {
            var s = Ui.Fill(root, "Primer");
            Header(s, Loc.T("primer.title"));
            var card = Ui.Panel(s, "Card", Theme.Panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-760, -330), new Vector2(760, 330));
            Ui.Outline(card, Theme.PanelLine, 2f);
            primerBody = Ui.Fill(card.transform, "Body");
            var prev = Ui.Button(s, Loc.T("primer.prev"), () => FlipPrimer(-1), new Vector2(0.5f, 0), new Vector2(-330, 110), new Vector2(280, 70), 32);
            var next = Ui.Button(s, Loc.T("primer.next"), () => FlipPrimer(1), new Vector2(0.5f, 0), new Vector2(0, 110), new Vector2(280, 70), 32);
            Ui.Button(s, Loc.T("menu.back"), () => GameFlow.I.Back(), new Vector2(0.5f, 0), new Vector2(330, 110), new Vector2(280, 70), 32);
            firstButton[s] = next.gameObject;
            prev.name = "Prev";
            return s;
        }

        void FlipPrimer(int d)
        {
            primerPage = Mathf.Clamp(primerPage + d, 0, PrimerPages.Length - 1);
            ShowPrimerPage();
        }

        void ShowPrimerPage()
        {
            foreach (Transform c in primerBody) Destroy(c.gameObject);
            var (key, diagram) = PrimerPages[primerPage];
            Ui.Text(primerBody, "Page", Loc.F("primer.page", primerPage + 1, PrimerPages.Length), 34, Theme.Mono, Theme.Dim, TextAnchor.UpperRight,
                Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-40, -26));
            var t = Ui.Text(primerBody, "Title", Loc.T(key + ".t"), 60, Theme.Display, Theme.Yellow, TextAnchor.UpperLeft,
                new Vector2(0, 0), new Vector2(0.56f, 1), new Vector2(60, 0), new Vector2(0, -40));
            Ui.Shadow(t);
            Ui.Text(primerBody, "Body", Loc.T(key + ".b"), 34, Theme.Body, Theme.Text, TextAnchor.UpperLeft,
                new Vector2(0, 0), new Vector2(0.56f, 1), new Vector2(60, 40), new Vector2(0, -140)).lineSpacing = 1.15f;
            var dia = Ui.Node(primerBody, "Diagram", new Vector2(0.58f, 0.15f), new Vector2(0.97f, 0.85f), Vector2.zero, Vector2.zero);
            DrawDiagram(dia, diagram);
        }

        static void DrawDiagram(RectTransform area, string[] rows)
        {
            const float cell = 96f;
            int w = rows[0].Length, h = rows.Length;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                char ch = rows[y][x];
                var pos = new Vector2((x - (w - 1) * 0.5f) * cell, ((h - 1) * 0.5f - y) * cell);
                var r = Ui.Box(area, "C", new Vector2(0.5f, 0.5f), pos, new Vector2(cell - 8, cell - 8));
                Color c = Theme.A(Theme.PanelLine, 0.8f);
                if (ch == 'G') c = Theme.A(Theme.Gate, 0.3f);
                else if (ch == 'R') c = Theme.A(Theme.Critical, 0.32f);
                else if (ch == 'S') c = Theme.A(Theme.Signal, 0.32f);
                else if (ch == '#') c = Theme.Wall;
                // threads drawn over the zone they stand on: F and T on the left column stand on a gate
                bool onGate = x == 0 && (ch == 'F' || ch == 'T');
                bool onCrit = (ch == 'T' || ch == 'F') && (Neighbour(rows, x, y, 'R'));
                if (onGate) c = Theme.A(Theme.Gate, 0.3f);
                else if (onCrit && (ch == 'F' || y > 0 && rows[y - 1][x] == 'R')) c = Theme.A(Theme.Critical, 0.32f);
                Ui.Image(r, "Fill", c, Theme.Round, true);
                if (ch == 'S') Ui.Label(r, "V()", 34, Theme.Mono, Theme.Signal);
                if (ch == 'T' || ch == 'M' || ch == 'F')
                {
                    var col = Theme.Threads[(x + y * 3) % Theme.Threads.Length];
                    var body = Ui.Image(r, "Thread", ch == 'F' ? Color.Lerp(col, Theme.Ink, 0.62f) : col, Theme.Round, true);
                    Ui.Rect(body.gameObject, Vector2.zero, Vector2.one, new Vector2(10, 10), new Vector2(-10, -10));
                    if (ch == 'F') Ui.Outline(body, col, 3f);
                    else Ui.Label(body.transform, ch == 'M' ? "<>" : "3", 40, Theme.Mono, Theme.Ink);
                }
            }
        }

        static bool Neighbour(string[] rows, int x, int y, char c)
        {
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if ((dx != 0 || dy != 0) && ny >= 0 && ny < rows.Length && nx >= 0 && nx < rows[ny].Length && rows[ny][nx] == c) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ options

        RectTransform BuildOptions()
        {
            var s = Ui.Fill(root, "Options");
            Header(s, Loc.T("opt.title"));
            var size = new Vector2(900, 70);
            float y = 210f;
            OptionRow Row(string key, System.Func<string> value, System.Action<int> step)
            {
                var r = Ui.Option(s, Loc.T(key), value, step, new Vector2(0.5f, 0.5f), new Vector2(0, y), size);
                y -= 84f;
                return r;
            }
            string Vol(int v) => new string('|', v) + new string('.', 10 - v);
            var first = Row("opt.master", () => Vol(Settings.Master), d => { Settings.Master = Mathf.Clamp(Settings.Master + d, 0, 10); Settings.Save(); });
            Row("opt.music", () => Vol(Settings.Music), d => { Settings.Music = Mathf.Clamp(Settings.Music + d, 0, 10); Settings.Save(); });
            Row("opt.effects", () => Vol(Settings.Effects), d => { Settings.Effects = Mathf.Clamp(Settings.Effects + d, 0, 10); Settings.Save(); Sfx.Play("fork", 0.6f); });
            Row("opt.language", () => Loc.Names[Loc.Language], d => Loc.SetLanguage(Loc.Language + d));
            Row("opt.fullscreen", () => Loc.T(Screen.fullScreen ? "opt.on" : "opt.off"), d => Screen.fullScreen = !Screen.fullScreen);
            Row("opt.flashes", () => Loc.T(Settings.ReduceFlashes ? "opt.on" : "opt.off"), d => { Settings.ReduceFlashes = !Settings.ReduceFlashes; Settings.Save(); });
            Row("opt.seconds", () => Loc.T(Settings.ShowSeconds ? "opt.on" : "opt.off"), d => { Settings.ShowSeconds = !Settings.ShowSeconds; Settings.Save(); });
            Row("opt.reset", () => Loc.T(resetArmed ? "opt.reset.confirm" : "opt.reset.idle"), d =>
            {
                if (!resetArmed) { resetArmed = true; return; }
                Progress.ResetAll(); resetArmed = false; Sfx.Play("crash", 0.4f);
            });
            Ui.Button(s, Loc.T("menu.back"), () => GameFlow.I.Back(), new Vector2(0.5f, 0.5f), new Vector2(0, y - 20f), new Vector2(320, 70), 34);
            firstButton[s] = first.gameObject;
            return s;
        }

        // ------------------------------------------------------------------ credits

        RectTransform BuildCredits()
        {
            var s = Ui.Fill(root, "Credits");
            Header(s, Loc.T("credits.title"));
            var card = Ui.Panel(s, "Card", Theme.Panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-760, -320), new Vector2(760, 330));
            Ui.Outline(card, Theme.PanelLine, 2f);
            Ui.Text(card.transform, "Body", Loc.T("credits.body"), 30, Theme.Body, Theme.Text, TextAnchor.UpperLeft,
                Vector2.zero, Vector2.one, new Vector2(60, 40), new Vector2(-60, -40)).lineSpacing = 1.12f;
            var back = Ui.Button(s, Loc.T("menu.back"), () => GameFlow.I.Back(), new Vector2(0.5f, 0), new Vector2(0, 100), new Vector2(320, 70), 34);
            firstButton[s] = back.gameObject;
            return s;
        }

        // ------------------------------------------------------------------ HUD

        RectTransform BuildHud()
        {
            hudKey = default;     // a rebuilt HUD (language change) must be filled again
            var s = Ui.Fill(root, "HUD");
            var bar = Ui.Panel(s, "Top", Theme.A(Theme.Panel, 0.9f), new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -140), new Vector2(-24, -18));
            Ui.Outline(bar, Theme.PanelLine, 2f);
            hudTitle = Ui.Text(bar.transform, "Level", "", 44, Theme.Display, Theme.Text, TextAnchor.MiddleLeft, Vector2.zero, new Vector2(0.34f, 1), new Vector2(34, 0), Vector2.zero);
            Ui.Shadow(hudTitle);

            hudProgress = Ui.Text(bar.transform, "Progress", "", 34, Theme.Mono, Theme.Text, TextAnchor.UpperCenter, new Vector2(0.34f, 0), new Vector2(0.66f, 1), Vector2.zero, new Vector2(0, -12));
            hudProgressFill = Ui.Bar(bar.transform, "Progress Bar", Theme.A(Theme.Ink, 0.9f), Theme.Gate, new Vector2(0.36f, 0), new Vector2(0.64f, 0), new Vector2(0, 22), new Vector2(0, 52));

            hudSpawn = Ui.Text(bar.transform, "Fork", Loc.T("hud.fork"), 30, Theme.Mono, Theme.Dim, TextAnchor.UpperLeft, new Vector2(0.69f, 0), new Vector2(0.9f, 1), Vector2.zero, new Vector2(0, -12));
            hudSpawnFill = Ui.Bar(bar.transform, "Fork Bar", Theme.A(Theme.Ink, 0.9f), Theme.Yellow, new Vector2(0.69f, 0), new Vector2(0.9f, 0), new Vector2(0, 22), new Vector2(0, 52));
            hudBest = Ui.Text(bar.transform, "Best", "", 26, Theme.Mono, Theme.Dim, TextAnchor.UpperRight, new Vector2(0.69f, 0), new Vector2(0.9f, 1), Vector2.zero, new Vector2(0, -14));

            var pb = Ui.Button(bar.transform, "II", () => GameFlow.I.Pause(), new Vector2(1, 0.5f), new Vector2(-70, 0), new Vector2(84, 84), 40);
            pb.GetComponent<MenuButtonFx>().marker.enabled = false;
            var nav = pb.navigation; nav.mode = Navigation.Mode.None; pb.navigation = nav;

            // bottom: controls and legend
            var foot = Ui.Node(s, "Foot", new Vector2(0, 0), new Vector2(1, 0), new Vector2(40, 18), new Vector2(-40, 100));
            float x = 30;
            Ui.Keycap(foot, "", new Vector2(0, 0.5f), new Vector2(x, 0), 46, 180); x += 52;
            Ui.Keycap(foot, "", new Vector2(0, 0.5f), new Vector2(x, 0), 46, 90); x += 52;
            Ui.Keycap(foot, "", new Vector2(0, 0.5f), new Vector2(x, 0), 46, 270); x += 52;
            Ui.Keycap(foot, "", new Vector2(0, 0.5f), new Vector2(x, 0), 46, 0); x += 40;
            Ui.Text(foot, "Move", Loc.T("hud.move"), 30, Theme.Body, Theme.Dim, TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(0, 1), new Vector2(x, 0), new Vector2(x + 260, 0));
            x += 250;
            Ui.Keycap(foot, "Esc", new Vector2(0, 0.5f), new Vector2(x + 30, 0)); x += 76;
            Ui.Text(foot, "Pause", Loc.T("hud.pause"), 30, Theme.Body, Theme.Dim, TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(0, 1), new Vector2(x, 0), new Vector2(x + 160, 0));
            x += 150;
            Ui.Keycap(foot, "R", new Vector2(0, 0.5f), new Vector2(x + 24, 0)); x += 60;
            Ui.Text(foot, "Restart", Loc.T("hud.restart"), 30, Theme.Body, Theme.Dim, TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(0, 1), new Vector2(x, 0), new Vector2(x + 220, 0));

            Legend(foot, Theme.Gate, Loc.T("legend.gate"), -760);
            Legend(foot, Theme.Critical, Loc.T("legend.crit"), -470);
            Legend(foot, Theme.Signal, Loc.T("legend.signal"), -170);
            return s;
        }

        static void Legend(RectTransform foot, Color c, string text, float x)
        {
            var sw = Ui.Box(foot, "Swatch", new Vector2(1, 0.5f), new Vector2(x, 0), new Vector2(30, 30));
            Ui.Image(sw, "C", Theme.A(c, 0.85f), Theme.RoundSmall, true);
            Ui.Text(foot, "Legend", text, 28, Theme.Body, Theme.Dim, TextAnchor.MiddleLeft, new Vector2(1, 0), new Vector2(1, 1), new Vector2(x + 24, 0), new Vector2(x + 300, 0));
        }

        void UpdateHud()
        {
            var f = GameFlow.I;
            if (f == null || f.Board == null || !hud.gameObject.activeSelf) return;
            var b = f.Board; var def = f.Level;
            int idx = Levels.IndexOf(def);
            if (hudKey.def != def || hudKey.done != b.Completed)
            {
                hudKey = (def, b.Completed);
                hudTitle.text = (idx >= 0 ? (idx + 1).ToString("00") + "  " : "") + Loc.T(def.titleKey);
                hudProgress.text = Loc.T("hud.done") + "  " + b.Completed + (def.goal > 0 ? " / " + def.goal : "");
                hudBest.text = def.goal > 0 ? "" : Loc.F("levels.best", Progress.Best(def.id));
            }
            hudProgressFill.fillAmount = def.goal > 0 ? b.Progress : (b.Elapsed % 10f) / 10f;
            hudSpawnFill.fillAmount = Mathf.Clamp01(1f - b.SpawnTimer / Mathf.Max(0.01f, b.SpawnInterval));
            hudSpawnFill.color = b.Pressure >= 1f ? Theme.Critical : Theme.Yellow;
        }

        // ------------------------------------------------------------------ intro, pause, result

        void BuildIntro()
        {
            foreach (Transform c in intro) Destroy(c.gameObject);
            var f = GameFlow.I; var def = f.Level;
            Ui.Image(intro, "Dim", Theme.A(Theme.Ground, 0.72f), null);
            var card = Ui.Panel(intro, "Card", Theme.Panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-640, -250), new Vector2(640, 250));
            Ui.Outline(card, Theme.PanelLine, 2f);
            int idx = Levels.IndexOf(def);
            string tag = idx >= 0 ? Loc.F("intro.level", idx + 1) : def == Levels.Classic ? Loc.T("intro.classic") : Loc.T("intro.endless.tag");
            Ui.Text(card.transform, "Tag", tag, 36, Theme.Mono, Theme.Yellow, TextAnchor.UpperCenter, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -30));
            Ui.Shadow(Ui.Text(card.transform, "Title", Loc.T(def.titleKey), 84, Theme.Display, Theme.Text, TextAnchor.UpperCenter, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -70)), 3f);
            Ui.Text(card.transform, "Concept", Loc.T(def.conceptKey), 34, Theme.Body, Theme.Text, TextAnchor.UpperCenter, Vector2.zero, Vector2.one, new Vector2(80, 0), new Vector2(-80, -195));
            string goal = def.goal > 0 ? Loc.F("intro.goal", def.goal) : Loc.T("intro.endless");
            Ui.Text(card.transform, "Goal", goal + "    ·    " + Loc.F("intro.capacity", def.capacity), 32, Theme.Mono, Theme.Gate, TextAnchor.LowerCenter,
                Vector2.zero, Vector2.one, new Vector2(0, 110), Vector2.zero);
            var press = Ui.Text(card.transform, "Press", Loc.T("intro.press"), 30, Theme.Body, Theme.Dim, TextAnchor.LowerCenter, Vector2.zero, Vector2.one, new Vector2(0, 36), Vector2.zero);
            press.gameObject.AddComponent<Blink>();
        }

        RectTransform BuildPause()
        {
            var s = Ui.Fill(root, "Pause");
            Ui.Image(s, "Dim", Theme.A(Theme.Ground, 0.8f), null);
            Ui.Shadow(Ui.Text(s, "Title", Loc.T("pause.title"), 110, Theme.Display, Theme.Text, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-500, 180), new Vector2(500, 330)), 3f);
            var r = Ui.Button(s, Loc.T("pause.resume"), () => GameFlow.I.Resume(), new Vector2(0.5f, 0.5f), new Vector2(0, 100), new Vector2(520, 72), 38);
            Ui.Button(s, Loc.T("pause.restart"), () => GameFlow.I.Restart(), new Vector2(0.5f, 0.5f), new Vector2(0, 16), new Vector2(520, 72), 38);
            Ui.Button(s, Loc.T("pause.options"), () => GameFlow.I.ShowOptions(), new Vector2(0.5f, 0.5f), new Vector2(0, -68), new Vector2(520, 72), 38);
            Ui.Button(s, Loc.T("pause.levels"), () => GameFlow.I.ShowLevels(), new Vector2(0.5f, 0.5f), new Vector2(0, -152), new Vector2(520, 72), 38);
            Ui.Button(s, Loc.T("pause.menu"), () => GameFlow.I.ShowTitle(), new Vector2(0.5f, 0.5f), new Vector2(0, -236), new Vector2(520, 72), 38);
            firstButton[s] = r.gameObject;
            return s;
        }

        void BuildResult()
        {
            foreach (Transform c in result) Destroy(c.gameObject);
            var f = GameFlow.I; var b = f.Board; var def = f.Level;
            if (b.Won) BuildWin(f, b, def); else BuildLoss(f, b, def);
        }

        string Stats(Board b) =>
            Loc.F("stat.threads", b.Completed) + "\n" + Loc.F("stat.forced", b.Forced) + "\n" + Loc.F("stat.releases", b.Releases) + "\n" +
            Loc.F("stat.moves", b.Moves) + "\n" + Loc.F("stat.time", Mathf.FloorToInt(b.Elapsed / 60f) + ":" + Mathf.FloorToInt(b.Elapsed % 60f).ToString("00"));

        void BuildWin(GameFlow f, Board b, LevelDef def)
        {
            Ui.Image(result, "Dim", Theme.A(Theme.Ground, 0.78f), null);
            var card = Ui.Panel(result, "Card", Theme.Panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-620, -400), new Vector2(620, 400));
            Ui.Outline(card, Theme.Gate, 3f);
            var head = Ui.Panel(card.transform, "Head", Theme.Win, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -150), Vector2.zero);
            string titleKey = def == Levels.Classic ? "win.classic" : "win.title";
            Ui.Shadow(Ui.Label(head.transform, Loc.T(titleKey), 80, Theme.Display, Theme.Text), 3f);
            int stars = f.LastStars;
            for (int i = 0; i < 3 && def.campaign; i++)
            {
                var sr = Ui.Box(card.transform, "Star", new Vector2(0.5f, 1), new Vector2((i - 1) * 130, -240), new Vector2(110, 110));
                var img = Ui.Image(sr, "S", i < stars ? Theme.Yellow : Theme.A(Theme.Yellow, 0.55f), i < stars ? Theme.Star : Theme.StarHollow);
                sr.gameObject.AddComponent<PopIn>().delay = 0.25f + i * 0.28f;
                if (i < stars) sr.GetComponent<PopIn>().sound = true;
            }
            float top = def.campaign ? -320 : -190;
            Ui.Text(card.transform, "Stats", Stats(b), 42, Theme.Mono, Theme.Text, TextAnchor.UpperLeft, Vector2.zero, Vector2.one, new Vector2(110, 0), new Vector2(-560, top));
            var hint = def.campaign ? Loc.T("win.hint") + (f.LastBest ? "" : "") : (f.LastBest ? Loc.T("win.newbest") : "");
            Ui.Text(card.transform, "Hint", hint, 28, Theme.Body, Theme.Dim, TextAnchor.UpperLeft, Vector2.zero, Vector2.one, new Vector2(660, 0), new Vector2(-70, top - 10));
            bool hasNext = Levels.IndexOf(def) >= 0 && Levels.IndexOf(def) + 1 < Levels.Campaign.Length;
            float bx = hasNext ? -330 : -170;
            GameObject first = null;
            if (hasNext) { first = Ui.Button(card.transform, Loc.T("win.next"), () => GameFlow.I.Next(), new Vector2(0.5f, 0), new Vector2(bx, 80), new Vector2(300, 72), 34).gameObject; bx += 330; }
            var retry = Ui.Button(card.transform, Loc.T("win.retry"), () => GameFlow.I.Restart(), new Vector2(0.5f, 0), new Vector2(bx, 80), new Vector2(300, 72), 34); bx += 330;
            Ui.Button(card.transform, Loc.T("win.levels"), () => GameFlow.I.ShowLevels(), new Vector2(0.5f, 0), new Vector2(bx, 80), new Vector2(300, 72), 34);
            firstButton[result] = first ? first : retry.gameObject;
        }

        void BuildLoss(GameFlow f, Board b, LevelDef def)
        {
            // the 2019 game ended on a Windows blue screen; the rework keeps it, and says what went wrong
            Ui.Image(result, "Blue", Theme.Bsod, null);
            var s = Ui.Node(result, "Text", Vector2.zero, Vector2.one, new Vector2(220, 120), new Vector2(-220, -110));
            Ui.Text(s, "Sad", ":(", 220, Theme.Light, Color.white, TextAnchor.UpperLeft, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Ui.Text(s, "Lead", Loc.T("lose.title"), 50, Theme.Light, Color.white, TextAnchor.UpperLeft, Vector2.zero, Vector2.one, new Vector2(0, 0), new Vector2(-300, -290));
            string cause = b.Cause == CrashCause.Deadlock ? "lose.deadlock" : "lose.race";
            string why = b.Cause == CrashCause.Deadlock ? Loc.T("lose.deadlock.why") :
                Loc.F("lose.race.why", b.CrashZone != null ? b.CrashZone.Count : def.capacity + 1, b.CrashZone != null ? b.CrashZone.Capacity : def.capacity);
            Ui.Text(s, "Why", Loc.T(cause) + "\n" + why, 34, Theme.Body, Color.white, TextAnchor.UpperLeft, Vector2.zero, Vector2.one, new Vector2(0, 0), new Vector2(-300, -440));
            string score = def.goal > 0 ? Loc.F("lose.progress", b.Completed, def.goal) : Loc.F("lose.endless", b.Completed) + (f.LastBest ? "   " + Loc.T("win.newbest") : "");
            Ui.Text(s, "Score", score, 34, Theme.Mono, Color.white, TextAnchor.UpperLeft, Vector2.zero, Vector2.one, new Vector2(0, 0), new Vector2(0, -700));
            Ui.Text(s, "Code", Loc.F("lose.code", b.Cause == CrashCause.Deadlock ? "DEADLOCK_NO_FREE_SEMAPHORE" : "RACE_CONDITION_IN_CRITICAL_SECTION"), 28, Theme.Mono, Theme.A(Color.white, 0.8f),
                TextAnchor.UpperLeft, Vector2.zero, Vector2.one, new Vector2(0, 0), new Vector2(0, -752));
            var retry = Ui.Button(result, Loc.T("lose.retry"), () => GameFlow.I.Restart(), new Vector2(0, 0), new Vector2(390, 84), new Vector2(340, 72), 34);
            Ui.Button(result, Loc.T(def.campaign ? "win.levels" : "pause.menu"), () => { if (def.campaign) GameFlow.I.ShowLevels(); else GameFlow.I.ShowTitle(); },
                new Vector2(0, 0), new Vector2(750, 84), new Vector2(340, 72), 34);
            firstButton[result] = retry.gameObject;
        }

        // ------------------------------------------------------------------ callout and flash

        void BuildCallout()
        {
            calloutBox = Ui.Node(root, "Callout", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-760, 118), new Vector2(760, 248));
            var back = Ui.Image(calloutBox, "Back", Theme.A(Theme.Ink, 0.94f), Theme.Round, true);
            Ui.Outline(back, Theme.Yellow, 3f);
            back.gameObject.AddComponent<Pulse>().target = back;
            calloutText = Ui.Text(calloutBox, "Text", "", 36, Theme.Body, Theme.Text, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(40, 10), new Vector2(-40, -10));
            calloutBox.gameObject.SetActive(false);
        }

        public void Callout(string key)
        {
            if (calloutText == null) return;
            calloutText.text = key == null ? "" : Loc.T(key);
            calloutBox.gameObject.SetActive(key != null);
            if (key != null) Sfx.Play("callout", 0.5f);
        }

        public void Flash(Color c)
        {
            if (Settings.ReduceFlashes) c.a *= 0.3f;
            flash.color = c; flashA = c.a;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            TickAttract(dt);
            UpdateHud();
            if (flashA > 0f)
            {
                flashA = Mathf.MoveTowards(flashA, 0f, dt * 1.6f);
                var c = flash.color; c.a = flashA; flash.color = c;
            }
        }
    }

    /// <summary>A text whose content is pulled every frame (cheap counters on menus).</summary>
    public sealed class LiveText : MonoBehaviour
    {
        public System.Func<string> get;
        Text t;
        void Awake() => t = GetComponent<Text>();
        string last;
        void Update() { if (get == null || !t) return; var v = get(); if (v != last) { last = v; t.text = v; } }
    }

    public sealed class Blink : MonoBehaviour
    {
        Graphic g;
        void Awake() => g = GetComponent<Graphic>();
        void Update() { var c = g.color; c.a = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.4f)); g.color = c; }
    }

    public sealed class Pulse : MonoBehaviour
    {
        public Graphic target;
        void Update() { if (target) target.transform.localScale = Vector3.one * (1f + 0.012f * Mathf.Sin(Time.unscaledTime * 5f)); }
    }

    /// <summary>Scales a UI element from nothing with an overshoot after a delay (result stars).</summary>
    public sealed class PopIn : MonoBehaviour
    {
        public float delay;
        public bool sound;
        float t;
        void Awake() => transform.localScale = Vector3.zero;
        void Update()
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01((t - delay) * 3.5f);
            if (k > 0f && sound) { sound = false; Sfx.Play("star", 0.7f, 1f + delay * 0.2f); }
            float s = k < 1f ? Mathf.Sin(k * Mathf.PI * 0.5f) * (1f + 0.35f * Mathf.Sin(k * Mathf.PI)) : 1f;
            transform.localScale = Vector3.one * s;
        }
    }

    /// <summary>Jitters the red echo of the logo now and then.</summary>
    public sealed class Glitch : MonoBehaviour
    {
        RectTransform r; Vector2 basePos; float next;
        void Awake() { r = (RectTransform)transform; basePos = r.anchoredPosition; }
        void Update()
        {
            if (Time.unscaledTime < next) { r.anchoredPosition = Vector2.Lerp(r.anchoredPosition, basePos, Time.unscaledDeltaTime * 20f); return; }
            next = Time.unscaledTime + Random.Range(0.05f, Settings.ReduceFlashes ? 3f : 1.4f);
            r.anchoredPosition = basePos + new Vector2(Random.Range(-14f, 14f), Random.Range(-4f, 4f));
        }
    }
}
