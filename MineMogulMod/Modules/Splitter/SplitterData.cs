using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using UnityEngine;

namespace MineMogulMod.Modules.Splitter
{
    // ── Enumeraties gespiegeld van de game ────────────────────────────────────

    public static class SplitterEnums
    {
        public static readonly string[] ResourceTypes =
            { "(any)", "Iron", "Coal", "Gold", "Slag", "Diamond", "Emerald",
              "Copper", "Ruby", "Steel", "Celestite", "Quartz", "Amethyst", "Mystery" };

        public static readonly string[] PieceTypes =
            { "(any)", "Ore", "Crushed", "Ingot", "Plate", "Gem", "Pipe",
              "Rod", "DrillBit", "ThreadedRod", "Gear", "OreCluster", "JunkCast", "Geode" };

        public static string ResourceName(ResourceType rt) =>
            rt == ResourceType.INVALID ? "(any)" : rt.ToString();
        public static string PieceName(PieceType pt) =>
            pt == PieceType.INVALID ? "(any)" : pt.ToString();

        public static bool MatchesResourceType(string name, ResourceType rt)
        {
            if (string.IsNullOrEmpty(name) || name == "(any)") return true;
            return Enum.TryParse<ResourceType>(name, true, out var parsed) && parsed == rt;
        }
        public static bool MatchesPieceType(string name, PieceType pt)
        {
            if (string.IsNullOrEmpty(name) || name == "(any)") return true;
            return Enum.TryParse<PieceType>(name, true, out var parsed) && parsed == pt;
        }
    }

    // ── Per-item regel ────────────────────────────────────────────────────────

    [Serializable]
    public class ItemRatioRule
    {
        public string ResourceTypeName = "(any)";
        public string PieceTypeName    = "(any)";
        /// <summary>0-100  percentage dat naar LINKS gaat</summary>
        public float  LeftPercent      = 50f;
    }

    // ── Config per machine ────────────────────────────────────────────────────

    [Serializable]
    internal class SplitterSaveEntry
    {
        public string Key               = "";
        public float  GlobalLeftPercent = 50f;
        public List<ItemRatioRule> Rules = new List<ItemRatioRule>();
    }

    [Serializable]
    internal class SplitterSaveFile
    {
        public List<SplitterSaveEntry> Splitters = new List<SplitterSaveEntry>();
    }

    // ── Routing-logica per splitter ───────────────────────────────────────────

    public class MMLSplitterConfig : MonoBehaviour
    {
        public float GlobalLeftPercent = 50f;
        public List<ItemRatioRule> PerItemRules = new List<ItemRatioRule>();
        internal bool ConfigLoaded = false;

        private readonly Dictionary<int, (int l, int r)> _counters = new();

        public bool DecideShouldGoStraight(ResourceType rt, PieceType pt)
        {
            int   ruleIndex = -1;
            float leftPct   = GlobalLeftPercent;

            for (int i = 0; i < PerItemRules.Count; i++)
            {
                var r = PerItemRules[i];
                if (SplitterEnums.MatchesResourceType(r.ResourceTypeName, rt) &&
                    SplitterEnums.MatchesPieceType(r.PieceTypeName, pt))
                { ruleIndex = i; leftPct = r.LeftPercent; break; }
            }

            if (!_counters.TryGetValue(ruleIndex, out var cnt)) cnt = (0, 0);
            int   total   = cnt.l + cnt.r;
            float current = total == 0 ? 0f : (float)cnt.l / total;
            bool  goLeft  = current < (leftPct / 100f) || (total == 0 && leftPct >= 50f);

            if (goLeft) _counters[ruleIndex] = (cnt.l + 1, cnt.r);
            else        _counters[ruleIndex] = (cnt.l, cnt.r + 1);

            var up = _counters[ruleIndex];
            if (up.l + up.r > 1000)
                _counters[ruleIndex] = (Math.Max(1, up.l / 2), Math.Max(0, up.r / 2));

            return goLeft;
        }

        public string GetPositionKey() =>
            $"{Mathf.RoundToInt(transform.position.x):D}" +
            $",{Mathf.RoundToInt(transform.position.y):D}" +
            $",{Mathf.RoundToInt(transform.position.z):D}";
    }

    // ── Opslag manager ────────────────────────────────────────────────────────

    public static class SplitterStorage
    {
        private static readonly string SavePath =
            Path.Combine(Paths.ConfigPath, "mml_splitters.json");

        private static SplitterSaveFile _file = new SplitterSaveFile();

        public static void Load()
        {
            if (!File.Exists(SavePath)) return;
            try
            {
                var f = JsonUtility.FromJson<SplitterSaveFile>(File.ReadAllText(SavePath));
                if (f != null) _file = f;
                Plugin.Logger.LogInfo($"[MML] Splitter config loaded - {_file.Splitters.Count} entries.");
            }
            catch (Exception ex) { Plugin.Logger.LogWarning("[MML] Splitter load: " + ex.Message); }
        }

        public static void ApplyToConfig(MMLSplitterConfig cfg)
        {
            if (cfg.ConfigLoaded) return;
            cfg.ConfigLoaded = true;
            var e = _file.Splitters.FirstOrDefault(x => x.Key == cfg.GetPositionKey());
            if (e == null) return;
            cfg.GlobalLeftPercent = e.GlobalLeftPercent;
            cfg.PerItemRules = new List<ItemRatioRule>(e.Rules);
        }

        public static void Save(MMLSplitterConfig cfg)
        {
            string key = cfg.GetPositionKey();
            var e = _file.Splitters.FirstOrDefault(x => x.Key == key);
            if (e == null) { e = new SplitterSaveEntry { Key = key }; _file.Splitters.Add(e); }
            e.GlobalLeftPercent = cfg.GlobalLeftPercent;
            e.Rules = new List<ItemRatioRule>(cfg.PerItemRules);
            try   { File.WriteAllText(SavePath, JsonUtility.ToJson(_file, true)); }
            catch (Exception ex) { Plugin.Logger.LogWarning("[MML] Splitter save: " + ex.Message); }
        }
    }
}
