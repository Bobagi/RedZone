using UnityEngine;

namespace RedZone.Core
{
    /// <summary>Player options, saved in PlayerPrefs. Volumes are 0..10 steps.</summary>
    public static class Settings
    {
        public static int Master = 8, Music = 6, Effects = 8;
        public static bool ReduceFlashes, ShowSeconds = true;
        public static int MenuFps = 60;

        public static float MasterVol => Master / 10f;
        public static float MusicVol => Music / 10f;
        public static float EffectsVol => Effects / 10f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Load()
        {
            Master = PlayerPrefs.GetInt("rz_master", Master);
            Music = PlayerPrefs.GetInt("rz_music", Music);
            Effects = PlayerPrefs.GetInt("rz_effects", Effects);
            ReduceFlashes = PlayerPrefs.GetInt("rz_flash", 0) == 1;
            ShowSeconds = PlayerPrefs.GetInt("rz_secs", 1) == 1;
            Apply();
        }

        public static void Save()
        {
            PlayerPrefs.SetInt("rz_master", Master); PlayerPrefs.SetInt("rz_music", Music); PlayerPrefs.SetInt("rz_effects", Effects);
            PlayerPrefs.SetInt("rz_flash", ReduceFlashes ? 1 : 0); PlayerPrefs.SetInt("rz_secs", ShowSeconds ? 1 : 0);
            PlayerPrefs.Save();
            Apply();
        }

        public static void Apply()
        {
            AudioListener.volume = MasterVol;
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = -1;
        }
    }

    /// <summary>Stars, best scores and the furthest unlocked level, in PlayerPrefs.</summary>
    public static class Progress
    {
        public static int Stars(string id) => PlayerPrefs.GetInt("rz_stars_" + id, 0);
        public static int Best(string id) => PlayerPrefs.GetInt("rz_best_" + id, 0);

        public static bool Unlocked(int campaignIndex) => campaignIndex <= PlayerPrefs.GetInt("rz_unlocked", 0);

        public static int TotalStars
        {
            get { int s = 0; foreach (var l in Levels.Campaign) s += Stars(l.id); return s; }
        }

        /// <summary>Records a finished run; returns true when it beat the stored best.</summary>
        public static bool Record(LevelDef def, bool won, int stars, int score)
        {
            bool best = score > Best(def.id);
            if (best) PlayerPrefs.SetInt("rz_best_" + def.id, score);
            if (won && stars > Stars(def.id)) PlayerPrefs.SetInt("rz_stars_" + def.id, stars);
            int idx = Levels.IndexOf(def);
            if (won && idx >= 0 && idx + 1 > PlayerPrefs.GetInt("rz_unlocked", 0)) PlayerPrefs.SetInt("rz_unlocked", idx + 1);
            PlayerPrefs.Save();
            return best;
        }

        /// <summary>3 stars: no thread ever forced into the critical region; 2: at most two; 1: finished.</summary>
        public static int StarsFor(Board b) => !b.Won ? 0 : b.Forced == 0 ? 3 : b.Forced <= 2 ? 2 : 1;

        public static void ResetAll()
        {
            foreach (var l in Levels.Campaign) { PlayerPrefs.DeleteKey("rz_stars_" + l.id); PlayerPrefs.DeleteKey("rz_best_" + l.id); }
            PlayerPrefs.DeleteKey("rz_best_" + Levels.Classic.id); PlayerPrefs.DeleteKey("rz_best_" + Levels.Endless.id);
            PlayerPrefs.DeleteKey("rz_unlocked");
            PlayerPrefs.Save();
        }
    }
}
