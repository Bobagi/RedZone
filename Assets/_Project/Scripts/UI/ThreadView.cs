using RedZone.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RedZone.UI
{
    /// <summary>
    /// One thread on the board: a coloured chip, the quantum as a ring that empties, the seconds left, and a
    /// mirror badge for threads that run with left and right swapped. A finished thread turns into a dark block.
    /// </summary>
    public sealed class ThreadView : MonoBehaviour
    {
        static readonly string[] Digits = { "", "1", "2", "3", "4", "5", "6", "7", "8", "9" };
        ThreadUnit unit;
        BoardView view;
        RectTransform r;
        Image body, ring, glow, core;
        Text seconds;
        GameObject badge;
        Color color;
        float spawnT, releaseT = -1f, crashT = -1f;
        bool finished;
        int shownSecond = -1;

        public static ThreadView Create(RectTransform layer, ThreadUnit t, BoardView view)
        {
            var r = Ui.Box(layer, "Thread " + t.Id, new Vector2(0.5f, 0.5f), view.CellPos(t.Pos), new Vector2(84, 84));
            var v = r.gameObject.AddComponent<ThreadView>();
            v.unit = t; v.view = view; v.r = r;
            v.color = Theme.Threads[t.Id % Theme.Threads.Length];
            v.glow = Ui.Image(r, "Glow", Theme.A(v.color, 0.45f), Theme.Glow);
            Ui.Rect(v.glow.gameObject, Vector2.zero, Vector2.one, new Vector2(-40, -40), new Vector2(40, 40));
            v.body = Ui.Image(r, "Body", v.color, Theme.Round, true);
            Ui.Rect(v.body.gameObject, Vector2.zero, Vector2.one, new Vector2(6, 6), new Vector2(-6, -6));
            v.core = Ui.Image(r, "Core", Theme.A(Theme.Ink, 0.0f), Theme.Round, true);
            Ui.Rect(v.core.gameObject, Vector2.zero, Vector2.one, new Vector2(22, 22), new Vector2(-22, -22));
            v.ring = Ui.Image(r, "Quantum", Theme.A(Theme.Ink, 0.75f), Theme.Ring);
            Ui.Rect(v.ring.gameObject, Vector2.zero, Vector2.one, new Vector2(14, 14), new Vector2(-14, -14));
            v.ring.type = Image.Type.Filled; v.ring.fillMethod = Image.FillMethod.Radial360; v.ring.fillOrigin = 2; v.ring.fillClockwise = false;
            v.seconds = Ui.Label(r, "", 46, Theme.Mono, Theme.Ink);
            if (t.Mirrored)
            {
                var b = Ui.Box(r, "Mirror", new Vector2(0.5f, 1f), new Vector2(0, 2), new Vector2(46, 22));
                Ui.Image(b, "Pill", Theme.Ink, Theme.RoundSmall, true);
                var la = Ui.Box(b, "L", new Vector2(0.5f, 0.5f), new Vector2(-10, 0), new Vector2(16, 16));
                Ui.Image(la, "T", v.color, Theme.Triangle).rectTransform.localEulerAngles = new Vector3(0, 0, 180);
                var ra = Ui.Box(b, "R", new Vector2(0.5f, 0.5f), new Vector2(10, 0), new Vector2(16, 16));
                Ui.Image(ra, "T", v.color, Theme.Triangle);
                v.badge = b.gameObject;
            }
            if (t.Forced) Ui.Outline(v.body, Theme.Critical, 4f);
            r.localScale = Vector3.zero;
            return v;
        }

        public void Finish()
        {
            finished = true;
            body.color = Color.Lerp(color, Theme.Ink, 0.62f);
            Ui.Outline(body, Theme.A(color, 0.9f), 3f);
            core.color = Theme.A(color, 0.35f);
            ring.enabled = false; seconds.text = ""; glow.enabled = false;
            if (badge) badge.SetActive(false);
            r.localScale = Vector3.one * 1.15f;
        }

        public void Release() { releaseT = 0f; }
        public void Crash() { crashT = 0f; }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (releaseT >= 0f)
            {
                releaseT += dt;
                r.anchoredPosition += new Vector2(0, 260f * dt);
                r.localScale = Vector3.one * Mathf.Max(0f, 1f - releaseT * 2.2f);
                if (releaseT > 0.5f) Destroy(gameObject);
                return;
            }
            var target = view.CellPos(unit.Pos);
            r.anchoredPosition = Vector2.Lerp(r.anchoredPosition, target, 1f - Mathf.Exp(-28f * dt));

            spawnT = Mathf.Min(1f, spawnT + dt * 4f);
            float pop = spawnT < 1f ? Mathf.Sin(spawnT * Mathf.PI * 0.5f) * (1f + 0.25f * Mathf.Sin(spawnT * Mathf.PI)) : 1f;
            if (!finished) r.localScale = Vector3.one * pop;
            else r.localScale = Vector3.Lerp(r.localScale, Vector3.one, 1f - Mathf.Exp(-12f * dt));

            if (crashT >= 0f)
            {
                crashT += dt;
                body.color = Color.Lerp(body.color, Theme.Critical, dt * 3f);
                return;
            }
            if (finished || !unit.Running) return;

            float q = Mathf.Clamp01(unit.Quantum / Mathf.Max(0.01f, unit.QuantumMax));
            ring.fillAmount = q;
            int sec = Settings.ShowSeconds ? Mathf.CeilToInt(unit.Quantum) : 0;
            if (sec != shownSecond) { shownSecond = sec; seconds.text = sec > 0 ? Digits[Mathf.Min(sec, Digits.Length - 1)] : ""; }
            // the last second blinks so the player sees which thread is about to stop
            float urgent = unit.Quantum < 1.2f ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 22f) : 0f;
            body.color = Color.Lerp(color, Color.white, urgent * 0.45f);
            glow.color = Theme.A(color, 0.35f + 0.3f * urgent);
        }
    }
}
