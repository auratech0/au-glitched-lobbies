using System;
using BepInEx;
using BepInEx.Logging;
using DiscordRPC;
using DiscordRPC.Logging;
using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace AUGL_DiscordRPC
{
    [BepInPlugin("com.augl.discordrpc", "AUGL DiscordRPC", "01092026.v18.AUGLDiscordRPC")]
    [BepInProcess("Among Us.exe")]
    public class AUGLDiscordRPCPlugin : BasePlugin
    {
        internal static ManualLogSource Log;
        private DiscordRpcClient _client;
        private Harmony _harmony;

        public override void Load()
        {
            Log = base.Log;
            _harmony = new Harmony("com.augl.discordrpc");
            _harmony.PatchAll();

            // Initialize Discord RPC
            _client = new DiscordRpcClient("1543635619476013086");
            _client.Logger = new ConsoleLogger();
            _client.OnReady += (sender, e) => Log.LogInfo("Discord RPC ready");
            _client.Initialize();

            // Set initial presence
            UpdatePresence("In Lobby", null, 0, 0, null);
            Log.LogInfo("AUGL DiscordRPC loaded");
        }

        public override void Unload()
        {
            _client?.Dispose();
            _harmony?.UnpatchAll("com.augl.discordrpc");
        }

        // ================= Presence Updater =================
        void UpdatePresence(string state, string mapName, int playerCount, int maxPlayers, string lobbyCode)
        {
            var presence = new RichPresence
            {
                Details = "Playing Among Us with AUGL Mod",
                State = state,
                Timestamps = Timestamps.Now,
                Assets = new Assets
                {
                    LargeImageKey = "among_us_logo", // Replace with your asset key or URL
                    LargeImageText = "AUGL Menu"
                },
                Party = new Party
                {
                    Size = playerCount,
                    Max = maxPlayers,
                    ID = lobbyCode ?? "none"
                },
                Buttons = new Button[]
                {
                    new Button { Label = "AUGL Discord", Url = "https://discord.gg/HeNGYArCkY" },
                    new Button { Label = "Website", Url = "https://augl.net" }
                }
            };
            _client.SetPresence(presence);
        }

        // ================= Harmony Patches for Auto-Updates =================
        [HarmonyPatch]
        public class GameStatePatch
        {
            static MethodBase TargetMethod()
            {
                var type = AccessTools.TypeByName("AmongUsClient");
                return AccessTools.Method(type, "OnGameJoined");
            }

            static void Postfix()
            {
                AUGLDiscordRPCPlugin.Instance.UpdatePresence("In Lobby", null, 0, 10, null);
            }
        }

        [HarmonyPatch]
        public class GameEndPatch
        {
            static MethodBase TargetMethod()
            {
                var type = AccessTools.TypeByName("GameManager");
                return AccessTools.Method(type, "RpcEndGame");
            }

            static void Postfix()
            {
                AUGLDiscordRPCPlugin.Instance.UpdatePresence("In Lobby", null, 0, 10, null);
            }
        }

        [HarmonyPatch]
        public class GameStartPatch
        {
            static MethodBase TargetMethod()
            {
                var type = AccessTools.TypeByName("GameManager");
                return AccessTools.Method(type, "StartGame");
            }

            static void Postfix()
            {
                // Get map name
                string mapName = "The Skeld";
                var shipStatusType = AccessTools.TypeByName("ShipStatus");
                var instanceField = AccessTools.Field(shipStatusType, "Instance");
                var shipStatus = instanceField?.GetValue(null);
                if (shipStatus != null)
                {
                    var typeProp = AccessTools.Property(shipStatus.GetType(), "Type");
                    int mapId = (int)typeProp?.GetValue(shipStatus);
                    string[] maps = { "The Skeld", "Mira HQ", "Polus", "The Airship", "The Fungle" };
                    if (mapId >= 0 && mapId < maps.Length) mapName = maps[mapId];
                }

                // Get player count
                int count = 0;
                var gameDataType = AccessTools.TypeByName("GameData");
                var gameDataInstanceField = AccessTools.Field(gameDataType, "Instance");
                var gameData = gameDataInstanceField?.GetValue(null);
                if (gameData != null)
                {
                    var allPlayersProp = AccessTools.Property(gameData.GetType(), "AllPlayers");
                    var allPlayers = allPlayersProp?.GetValue(gameData) as System.Collections.IEnumerable;
                    if (allPlayers != null) foreach (var p in allPlayers) count++;
                }

                AUGLDiscordRPCPlugin.Instance.UpdatePresence($"Being tuff in {mapName}", null, count, 10, null);
            }
        }
    }
}
