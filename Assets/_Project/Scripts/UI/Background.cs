using UnityEngine;
using UnityEngine.UI;

namespace RedZone.UI
{
    /// <summary>
    /// The ground behind every screen: deep navy, a faint grid that drifts, a soft red glow from below (the red
    /// zone of the title) and a few dim packets crossing the grid like data on a bus.
    /// </summary>
    public sealed class Background : MonoBehaviour
    {
        RectTransform grid;
        RectTransform[] packets;
        float[] speeds;

        public static Background Create(RectTransform root)
        {
            var r = Ui.Fill(root, "Background");
            var bg = r.gameObject.AddComponent<Background>();
            Ui.Image(r, "Ground", Theme.Ground, null);
            var glow = Ui.Image(r, "Red glow", Theme.A(Theme.Critical, 0.10f), Theme.Glow);
            Ui.Rect(glow.gameObject, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-1400, -900), new Vector2(1400, 700));
            var glow2 = Ui.Image(r, "Blue glow", Theme.A(Theme.Signal, 0.06f), Theme.Glow);
            Ui.Rect(glow2.gameObject, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-1100, -900), new Vector2(500, 500));

            bg.grid = Ui.Node(r, "Grid", Vector2.zero, Vector2.one, new Vector2(-120, -120), new Vector2(120, 120));
            var tex = new Texture2D(60, 60, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
            for (int y = 0; y < 60; y++) for (int x = 0; x < 60; x++) tex.SetPixel(x, y, new Color(1, 1, 1, x == 0 || y == 0 ? 1f : 0f));
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, 60, 60), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            var g = Ui.Image(bg.grid, "Lines", Theme.A(Theme.CellLine, 0.55f), sprite);
            g.type = Image.Type.Tiled;

            bg.packets = new RectTransform[14];
            bg.speeds = new float[bg.packets.Length];
            for (int i = 0; i < bg.packets.Length; i++)
            {
                var p = bg.packets[i] = Ui.Box(r, "Packet", new Vector2(0, 0), Vector2.zero, new Vector2(24, 24));
                Ui.Image(p, "Q", Theme.A(Theme.Threads[i % Theme.Threads.Length], 0.18f), Theme.RoundSmall, true);
                bg.Respawn(i, true);
            }
            return bg;
        }

        void Respawn(int i, bool anywhere)
        {
            var p = packets[i];
            bool horizontal = Random.value < 0.6f;
            float lane = Mathf.Round(Random.Range(2f, 30f)) * 60f + 6f;
            speeds[i] = Random.Range(40f, 120f) * (Random.value < 0.5f ? 1f : -1f);
            p.localEulerAngles = Vector3.zero;
            p.anchoredPosition = horizontal
                ? new Vector2(anywhere ? Random.Range(0f, 1920f) : (speeds[i] > 0 ? -40f : 1960f), Mathf.Min(lane, 1074f))
                : new Vector2(lane, anywhere ? Random.Range(0f, 1080f) : (speeds[i] > 0 ? -40f : 1120f));
            p.name = horizontal ? "H" : "V";
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            grid.anchoredPosition = new Vector2(Mathf.Repeat(Time.unscaledTime * 6f, 60f), Mathf.Repeat(Time.unscaledTime * 3f, 60f)) - new Vector2(30, 30);
            for (int i = 0; i < packets.Length; i++)
            {
                var p = packets[i];
                var d = p.name == "H" ? new Vector2(speeds[i], 0) : new Vector2(0, speeds[i]);
                p.anchoredPosition += d * dt;
                var a = p.anchoredPosition;
                if (a.x < -60 || a.x > 1990 || a.y < -60 || a.y > 1140) Respawn(i, false);
            }
        }
    }
}
