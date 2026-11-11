using System.Collections.Generic;
using UnityEngine;

namespace RedZone.Core
{
    /// <summary>
    /// The game's languages (same model as Beneath the Shroud): every string lives in one table,
    /// Settings/Resources/Localization/ui.txt, one line per key and one TAB separated column per language
    /// (key, pt-BR, en, es). T(key) falls back to Portuguese and then to the key itself, so a missing line shows up
    /// on screen instead of hiding. The first run follows the system language; the choice is saved in PlayerPrefs.
    /// </summary>
    public static class Loc
    {
        public static readonly string[] Codes = { "pt-BR", "en", "es" };
        public static readonly string[] Names = { "Português", "English", "Español" };
        const string PrefKey = "rz_lang";
        static Dictionary<string, string[]> table;

        public static int Language { get; private set; }
        public static event System.Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            table = null;
            Language = Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, SystemDefault()), 0, Codes.Length - 1);
        }

        static int SystemDefault()
        {
            switch (Application.systemLanguage)
            {
                case SystemLanguage.Portuguese: return 0;
                case SystemLanguage.Spanish: return 2;
                default: return 1;
            }
        }

        public static void SetLanguage(int index)
        {
            Language = (index % Codes.Length + Codes.Length) % Codes.Length;
            PlayerPrefs.SetInt(PrefKey, Language);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static string T(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            Load();
            if (!table.TryGetValue(key, out var row)) return key;
            string text = Language < row.Length ? row[Language] : null;
            return string.IsNullOrEmpty(text) ? (string.IsNullOrEmpty(row[0]) ? key : row[0]) : text;
        }

        public static string F(string key, params object[] args) => string.Format(T(key), args);

        public static bool Has(string key) { Load(); return !string.IsNullOrEmpty(key) && table.ContainsKey(key); }

        static void Load()
        {
            if (table != null) return;
            table = new Dictionary<string, string[]>();
            var asset = Resources.Load<TextAsset>("Localization/ui");
            if (asset == null) { Debug.LogWarning("[Loc] Localization/ui.txt not found"); return; }
            foreach (var raw in asset.text.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (line.Length == 0 || line[0] == '#') continue;
                var cells = line.Split('\t');
                if (cells.Length < 2 || cells[0] == "key") continue;
                var row = new string[Codes.Length];
                for (int i = 0; i < row.Length; i++) row[i] = i + 1 < cells.Length ? cells[i + 1].Replace("\\n", "\n") : null;
                table[cells[0]] = row;
            }
        }
    }
}
