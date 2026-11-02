using System.Collections;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace SilksongPersian
{
    /// <summary>
    /// Entry point. Loads the Persian sheet store, serves it through the game's own
    /// localisation lookup, and installs the RTL/shaping text pipeline.
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.silksong.fa.persian";
        public const string Name = "Silksong Persian";
        public const string Version = "0.1.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> ForcePersian;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            Log.LogInfo($"{Name} {Version} loading...");

            _harmony = new Harmony(Guid);
            Patcher.Apply(_harmony);

            Log.LogInfo("Patches applied successfully.");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
