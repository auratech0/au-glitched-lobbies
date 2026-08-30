using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace AUGL_Features
{
    [BepInPlugin("com.augl.features", "AUGL Features", "01092026.v18.AUGLFeatures")]
    [BepInProcess("Among Us.exe")]
    public class AUGLFeaturesPlugin : BasePlugin
    {
        internal static ManualLogSource Log;
        private Harmony _harmony;

        public override void Load()
        {
            Log = base.Log;
            _harmony = new Harmony("com.augl.features");
            _harmony.PatchAll();
            Log.LogInfo("AUGL Features loaded");
        }
    }

    // ================= Chat Commands =================
    [HarmonyPatch]
    public static class ChatCommandPatch
    {
        static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("ChatController");
            return AccessTools.Method(type, "SendFreeChat");
        }

        static bool Prefix(Il2CppSystem.Object __instance)
        {
            var textField = AccessTools.Field(__instance.GetType(), "freeChatField");
            var freeChatField = textField?.GetValue(__instance);
            if (freeChatField == null) return true;

            var textAreaField = AccessTools.Field(freeChatField.GetType(), "textArea");
            var textArea = textAreaField?.GetValue(freeChatField);
            if (textArea == null) return true;

            var textFieldInfo = AccessTools.Field(textArea.GetType(), "text");
            var text = textFieldInfo?.GetValue(textArea) as Il2CppSystem.String;
            if (text == null) return true;

            string msg = text;
            if (string.IsNullOrEmpty(msg) || !msg.StartsWith("/")) return true;

            ProcessCommand(msg);
            return false; // skip original send
        }

        static void ProcessCommand(string msg)
        {
            var parts = msg.Split(' ');
            string cmd = parts[0].ToLower();

            // Host-only commands
            bool isHost = IsHost();
            switch (cmd)
            {
                case "/start":
                    if (isHost) InvokeGameMethod("GameStartManager", "ReallyBegin", new object[] { true });
                    break;
                case "/endgame":
                    if (isHost) InvokeGameMethod("GameManager", "RpcEndGame", new object[] { (int)0, false });
                    break;
                case "/endmeeting":
                    if (isHost) InvokeGameMethod("MeetingHud", "RpcClose", new object[] { });
                    break;
                case "/kick":
                    if (isHost && parts.Length > 1) KickPlayer(parts[1], false);
                    break;
                case "/ban":
                    if (isHost && parts.Length > 1) KickPlayer(parts[1], true);
                    break;
                case "/tpout":
                    TeleportPlayer(true);
                    break;
                case "/tpin":
                    TeleportPlayer(false);
                    break;
                case "/color":
                    if (parts.Length > 1) ForceColor(parts[1]);
                    break;
                case "/help":
                    SendChat("Commands: /start, /endgame, /endmeeting, /kick, /ban, /tpout, /tpin, /color, /help");
                    break;
            }
        }

        static bool IsHost()
        {
            var clientType = AccessTools.TypeByName("AmongUsClient");
            var instanceProp = clientType.GetProperty("Instance");
            var client = instanceProp?.GetValue(null);
            if (client == null) return false;
            var amHostProp = clientType.GetProperty("AmHost");
            return (bool)amHostProp?.GetValue(client);
        }

        static void InvokeGameMethod(string typeName, string methodName, object[] args)
        {
            try
            {
                var type = AccessTools.TypeByName(typeName);
                if (type == null) return;
                var method = AccessTools.Method(type, methodName);
                method?.Invoke(null, args);
            }
            catch { }
        }

        static void KickPlayer(string name, bool ban)
        {
            try
            {
                var allPlayersType = AccessTools.TypeByName("PlayerControl");
                var allPlayersField = AccessTools.Field(allPlayersType, "AllPlayerControls");
                var allPlayers = allPlayersField?.GetValue(null) as IEnumerable<Il2CppSystem.Object>;
                if (allPlayers == null) return;

                foreach (var player in allPlayers)
                {
                    var dataProp = AccessTools.Property(player.GetType(), "Data");
                    var data = dataProp?.GetValue(player);
                    if (data == null) continue;
                    var nameProp = AccessTools.Property(data.GetType(), "PlayerName");
                    string playerName = nameProp?.GetValue(data) as string;
                    if (playerName == name)
                    {
                        var ownerIdField = AccessTools.Field(player.GetType(), "OwnerId");
                        int ownerId = (int)ownerIdField?.GetValue(player);
                        var clientType = AccessTools.TypeByName("InnerNetClient");
                        var clientInstance = clientType.GetProperty("Instance")?.GetValue(null);
                        var kickMethod = AccessTools.Method(clientType, "KickPlayer");
                        kickMethod?.Invoke(clientInstance, new object[] { ownerId, ban });
                        return;
                    }
                }
            }
            catch { }
        }

        static void TeleportPlayer(bool outside)
        {
            try
            {
                var localPlayerType = AccessTools.TypeByName("PlayerControl");
                var localPlayerField = AccessTools.Field(localPlayerType, "LocalPlayer");
                var localPlayer = localPlayerField?.GetValue(null);
                if (localPlayer == null) return;

                var netTransformProp = AccessTools.Property(localPlayer.GetType(), "NetTransform");
                var netTransform = netTransformProp?.GetValue(localPlayer);
                if (netTransform == null) return;

                var snapMethod = AccessTools.Method(netTransform.GetType(), "SnapTo");
                Vector2 target = outside ? new Vector2(9999f, 9999f) : new Vector2(0f, 0f);
                snapMethod?.Invoke(netTransform, new object[] { target, (ushort)0 });
            }
            catch { }
        }

        static void ForceColor(string colorArg)
        {
            try
            {
                int colorId;
                if (int.TryParse(colorArg, out colorId))
                {
                    // Allow 0-17 (or whatever max)
                }
                else
                {
                    // Map name to ID (simple: "red"=0, "blue"=1, etc.)
                    string[] colorNames = { "red", "blue", "green", "pink", "orange", "yellow", "black", "white", "purple", "brown", "cyan", "lime", "maroon", "rose", "banana", "gray", "tan", "coral" };
                    colorId = Array.IndexOf(colorNames, colorArg.ToLower());
                    if (colorId < 0) return;
                }

                var localPlayerType = AccessTools.TypeByName("PlayerControl");
                var localPlayerField = AccessTools.Field(localPlayerType, "LocalPlayer");
                var localPlayer = localPlayerField?.GetValue(null);
                if (localPlayer == null) return;

                var rpcSetColorMethod = AccessTools.Method(localPlayerType, "RpcSetColor");
                rpcSetColorMethod?.Invoke(localPlayer, new object[] { (byte)colorId });
            }
            catch { }
        }

        static void SendChat(string message)
        {
            try
            {
                var localPlayerType = AccessTools.TypeByName("PlayerControl");
                var localPlayerField = AccessTools.Field(localPlayerType, "LocalPlayer");
                var localPlayer = localPlayerField?.GetValue(null);
                if (localPlayer == null) return;

                var rpcSendChatMethod = AccessTools.Method(localPlayerType, "RpcSendChat");
                rpcSendChatMethod?.Invoke(localPlayer, new object[] { message });
            }
            catch { }
        }
    }

    // ================= Mod Detection (Simple) =================
    [HarmonyPatch]
    public static class ModDetectionPatch
    {
        static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("PlayerControl");
            return AccessTools.Method(type, "FixedUpdate");
        }

        static void Postfix(Il2CppSystem.Object __instance)
        {
            // Check if player has modded nameplate (e.g., "SickoMenu" or "AUGL")
            // Simple detection: check if PlayerName contains known mod prefixes
            var dataProp = AccessTools.Property(__instance.GetType(), "Data");
            var data = dataProp?.GetValue(__instance);
            if (data == null) return;
            var nameProp = AccessTools.Property(data.GetType(), "PlayerName");
            string name = nameProp?.GetValue(data) as string;
            if (string.IsNullOrEmpty(name)) return;

            // Known mod indicators (adjust as needed)
            string[] modIndicators = { "Sicko", "AUGL", "EHR", "Better" };
            foreach (var indicator in modIndicators)
            {
                if (name.Contains(indicator))
                {
                    AUGLFeaturesPlugin.Log.LogInfo($"Detected mod player: {name}");
                    break;
                }
            }
        }
    }

    // ================= Kill Cooldown Display =================
    [HarmonyPatch]
    public static class KillCooldownDisplayPatch
    {
        static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("HudManager");
            return AccessTools.Method(type, "Update");
        }

        static void Postfix()
        {
            try
            {
                var allPlayersType = AccessTools.TypeByName("PlayerControl");
                var allPlayersField = AccessTools.Field(allPlayersType, "AllPlayerControls");
                var allPlayers = allPlayersField?.GetValue(null) as IEnumerable<Il2CppSystem.Object>;
                if (allPlayers == null) return;

                foreach (var player in allPlayers)
                {
                    var dataProp = AccessTools.Property(player.GetType(), "Data");
                    var data = dataProp?.GetValue(player);
                    if (data == null) continue;
                    var isDeadProp = AccessTools.Property(data.GetType(), "IsDead");
                    bool isDead = (bool)isDeadProp?.GetValue(data);
                    if (isDead) continue;

                    var roleProp = AccessTools.Property(data.GetType(), "Role");
                    var role = roleProp?.GetValue(data);
                    if (role == null) continue;

                    var canUseKillProp = AccessTools.Property(role.GetType(), "CanUseKillButton");
                    bool canUseKill = (bool)canUseKillProp?.GetValue(role);
                    if (!canUseKill) continue;

                    var killTimerField = AccessTools.Field(player.GetType(), "killTimer");
                    float killTimer = (float)killTimerField?.GetValue(player);
                    if (killTimer < 0) killTimer = 0;

                    // Draw text above player (using WorldToScreenPoint)
                    var truePosProp = AccessTools.Method(player.GetType(), "GetTruePosition");
                    Vector2 pos = (Vector2)truePosProp?.Invoke(player, null);
                    Vector3 screenPos = Camera.main.WorldToScreenPoint(new Vector3(pos.x, pos.y, 0));
                    if (screenPos.z > 0)
                    {
                        GUI.Label(new Rect(screenPos.x - 50, Screen.height - screenPos.y - 30, 100, 20), $"Kill: {killTimer:F1}s", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
                    }
                }
            }
            catch { }
        }
    }
}
