using System;
using RedZone.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RedZone.UI
{
    /// <summary>Tiny builder for the uGUI widgets the game uses (no prefabs), after Wilkins Pest Control's Ui.</summary>
    public static class Ui
    {
        public static Canvas Canvas(string name, int order, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            if (parent) go.transform.SetParent(parent, false);
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = order;
            var s = go.GetComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; s.referenceResolution = new Vector2(1920, 1080); s.matchWidthOrHeight = 0.5f;
            return c;
        }

        public static RectTransform Rect(GameObject go, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            var r = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
            r.anchorMin = aMin; r.anchorMax = aMax; r.offsetMin = oMin; r.offsetMax = oMax;
            return r;
        }

        public static RectTransform Node(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return Rect(go, aMin, aMax, oMin, oMax);
        }

        public static RectTransform Fill(Transform parent, string name) => Node(parent, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        /// <summary>A box of size w x h centred on an anchor point.</summary>
        public static RectTransform Box(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var r = Node(parent, name, anchor, anchor, Vector2.zero, Vector2.zero);
            r.sizeDelta = size; r.anchoredPosition = pos;
            return r;
        }

        public static Image Image(Transform parent, string name, Color color, Sprite sprite = null, bool sliced = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Rect(go, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var img = go.GetComponent<Image>();
            img.color = color; img.sprite = sprite; img.raycastTarget = false;
            if (sliced) img.type = UnityEngine.UI.Image.Type.Sliced;
            return img;
        }

        public static Image Panel(Transform parent, string name, Color color, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax, bool rounded = true)
        {
            var img = Image(parent, name, color, rounded ? Theme.Round : null, rounded);
            Rect(img.gameObject, aMin, aMax, oMin, oMax);
            return img;
        }

        public static Text Text(Transform parent, string name, string text, int size, Font font, Color color, TextAnchor align,
            Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Rect(go, aMin, aMax, oMin, oMax);
            var t = go.GetComponent<Text>();
            t.text = text; t.fontSize = size; t.font = font; t.color = color; t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow; t.raycastTarget = false;
            t.supportRichText = true;
            return t;
        }

        public static Text Label(Transform parent, string text, int size, Font font, Color color, TextAnchor align = TextAnchor.MiddleCenter) =>
            Text(parent, "Text", text, size, font, color, align, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        public static T Shadow<T>(T g, float px = 2f, float a = 0.7f) where T : Graphic
        {
            var sh = g.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            sh.effectColor = new Color(0, 0, 0, a); sh.effectDistance = new Vector2(px, -px);
            return g;
        }

        public static T Outline<T>(T g, Color c, float px = 2f) where T : Graphic
        {
            var o = g.gameObject.AddComponent<UnityEngine.UI.Outline>();
            o.effectColor = c; o.effectDistance = new Vector2(px, -px);
            return g;
        }

        /// <summary>
        /// The game's button: a dark plate with a thin outline; the selected one lights up yellow with ink text and a
        /// side marker (MenuButtonFx). Hover selects, so mouse and keyboard always agree.
        /// </summary>
        public static Button Button(Transform parent, string label, UnityAction onClick, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize = 40)
        {
            var r = Box(parent, "Button " + label, anchor, pos, size);
            var plate = Image(r, "Plate", Theme.A(Theme.GroundHi, 0.9f), Theme.Round, true); plate.raycastTarget = true;
            var line = Image(r, "Line", Theme.PanelLine, Theme.Round, true);
            // outline drawn as a slightly larger plate behind
            line.transform.SetAsFirstSibling();
            Rect(line.gameObject, Vector2.zero, Vector2.one, new Vector2(-2, -2), new Vector2(2, 2));
            var marker = Image(r, "Marker", Theme.Ink, Theme.Triangle);
            Rect(marker.gameObject, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            marker.rectTransform.sizeDelta = new Vector2(22, 22); marker.rectTransform.anchoredPosition = new Vector2(28, 0);
            var t = Label(r, label, fontSize, Theme.Body, Theme.Text);
            var b = r.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None; b.targetGraphic = plate;
            var nav = b.navigation; nav.mode = Navigation.Mode.Automatic; b.navigation = nav;
            b.onClick.AddListener(() => { Sfx.Play("ui_click", 0.7f); onClick?.Invoke(); });
            var fx = r.gameObject.AddComponent<MenuButtonFx>(); fx.plate = plate; fx.line = line; fx.label = t; fx.marker = marker;
            return b;
        }

        /// <summary>A keyboard key drawn on screen; arrows use the triangle sprite rotated.</summary>
        public static RectTransform Keycap(Transform parent, string key, Vector2 anchor, Vector2 pos, float h = 46f, float rotateArrow = float.NaN)
        {
            float w = float.IsNaN(rotateArrow) ? Mathf.Max(h, 18f + key.Length * h * 0.36f) : h;
            var r = Box(parent, "Key " + key, anchor, pos, new Vector2(w, h));
            Image(r, "Base", Theme.A(Theme.Text, 0.85f), Theme.RoundSmall, true);
            var top = Image(r, "Top", Theme.GroundHi, Theme.RoundSmall, true);
            Rect(top.gameObject, Vector2.zero, Vector2.one, new Vector2(3, 6), new Vector2(-3, -3));
            if (float.IsNaN(rotateArrow))
            {
                var t = Label(top.transform, key, Mathf.RoundToInt(h * 0.52f), Theme.Display, Theme.Text);
                t.rectTransform.offsetMin = new Vector2(0, 2);
            }
            else
            {
                var a = Image(top.transform, "Arrow", Theme.Text, Theme.Triangle);
                Rect(a.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                a.rectTransform.sizeDelta = new Vector2(h * 0.42f, h * 0.42f);
                a.rectTransform.localEulerAngles = new Vector3(0, 0, rotateArrow);
            }
            return r;
        }

        /// <summary>A button that steps a value with left/right (and click). Shows "label   &lt; value &gt;".</summary>
        public static OptionRow Option(Transform parent, string label, Func<string> value, Action<int> step, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var b = Button(parent, label, null, anchor, pos, size, 34);
            var fx = b.GetComponent<MenuButtonFx>();
            fx.label.alignment = TextAnchor.MiddleLeft;
            fx.label.rectTransform.offsetMin = new Vector2(60, 0);
            var v = Text(b.transform, "Value", "", 34, Theme.Mono, Theme.Yellow, TextAnchor.MiddleRight, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-34, 0));
            v.fontSize = 44;
            fx.value = v;
            var row = b.gameObject.AddComponent<OptionRow>();
            row.value = value; row.step = step; row.text = v;
            b.onClick.AddListener(() => row.Step(1));
            return row;
        }

        public static Image Bar(Transform parent, string name, Color back, Color fill, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            var bg = Panel(parent, name, back, aMin, aMax, oMin, oMax);
            var f = Image(bg.transform, "Fill", fill, null);
            Rect(f.gameObject, Vector2.zero, Vector2.one, new Vector2(4, 4), new Vector2(-4, -4));
            f.type = UnityEngine.UI.Image.Type.Filled; f.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal; f.fillOrigin = 0;
            f.sprite = Theme.RoundSmall;
            return f;
        }

        public static void Select(GameObject go)
        {
            var es = EventSystem.current;
            if (es && go) { es.SetSelectedGameObject(null); es.SetSelectedGameObject(go); }
        }
    }

    /// <summary>The look of the selected button: yellow plate, ink text, a marker on the left; a soft pop on select.</summary>
    public sealed class MenuButtonFx : MonoBehaviour, IPointerEnterHandler, ISelectHandler, IDeselectHandler
    {
        public Image plate, line, marker;
        public Text label, value;
        public Text[] extra;      // secondary lines (level cards): dim at rest, ink when selected
        public Image[] icons;     // stars: yellow at rest, ink when selected
        bool selected;
        float t;
        Selectable selectable;

        void Awake() { selectable = GetComponent<Selectable>(); Apply(0f); }
        void OnDisable() { selected = false; t = 0f; Apply(0f); }

        public void OnPointerEnter(PointerEventData e)
        {
            var es = EventSystem.current;
            if (es && es.currentSelectedGameObject != gameObject && selectable && selectable.IsInteractable()) es.SetSelectedGameObject(gameObject);
        }

        public void OnSelect(BaseEventData e) { if (!selected) Sfx.Play("switch_2", 0.3f, 1.5f); selected = true; }
        public void OnDeselect(BaseEventData e) { selected = false; }

        void Update()
        {
            t = Mathf.MoveTowards(t, selected ? 1f : 0f, Time.unscaledDeltaTime * 9f);
            Apply(t);
        }

        void Apply(float k)
        {
            bool on = selectable == null || selectable.interactable;
            if (plate) plate.color = Color.Lerp(Theme.A(Theme.GroundHi, 0.9f), Theme.Yellow, k);
            if (line) line.color = Color.Lerp(Theme.PanelLine, Theme.Yellow, k);
            if (label) label.color = Color.Lerp(on ? Theme.Text : Theme.Dim, Theme.Ink, k);
            if (value) value.color = Color.Lerp(Theme.Yellow, Theme.Ink, k);
            if (marker) { marker.color = Theme.A(Theme.Ink, k); }
            if (extra != null) foreach (var e in extra) if (e) e.color = Color.Lerp(Theme.Dim, Theme.Ink, k);
            if (icons != null) foreach (var i in icons) if (i) i.color = Color.Lerp(Theme.A(Theme.Yellow, i.color.a), Theme.A(Theme.Ink, i.color.a), k);
            transform.localScale = Vector3.one * (1f + 0.025f * k);
        }
    }

    /// <summary>An options line: left/right (arrows, A/D, d-pad) or a click steps the value.</summary>
    public sealed class OptionRow : MonoBehaviour, IMoveHandler
    {
        public Func<string> value;
        public Action<int> step;
        public Text text;

        void OnEnable() => Refresh();

        public void OnMove(AxisEventData e)
        {
            if (e.moveDir != MoveDirection.Left && e.moveDir != MoveDirection.Right) return;
            Step(e.moveDir == MoveDirection.Right ? 1 : -1);
            e.Use();
        }

        public void Step(int d)
        {
            step?.Invoke(d);
            Sfx.Play("switch_1", 0.4f, d > 0 ? 1.2f : 1f);
            Refresh();
        }

        public void Refresh() { if (text != null && value != null) text.text = "<  " + value() + "  >"; }
    }
}
