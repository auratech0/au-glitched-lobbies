using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.InteropTypes;
using BepInEx.Unity.IL2CPP;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Il2CppInterop.Runtime;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting;
using AmongUs.GameOptions;
using InnerNet;

namespace AUGLMod
{
    [BepInPlugin("com.al3x4nderr.auglmod", "AUGL Menu", "04.09.18")]
    [BepInProcess("Among Us.exe")]
    public class AUGLModPlugin : BasePlugin
    {
        public static new BepInEx.Logging.ManualLogSource Log;

        public override void Load()
        {
            Log = base.Log;

            // 1. Auto-Inject Region on startup
            try
            {
                RegionInstaller.Inject();
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Auto region injection warning: {ex.Message}");
            }

            // 2. Register custom MonoBehaviour into IL2CPP domain & instantiate persistent GameObject
            try
            {
                ClassInjector.RegisterTypeInIl2Cpp<AUGLMenuBehaviour>();
                var menuGO = new GameObject("AUGLMenuObject");
                UnityEngine.Object.DontDestroyOnLoad(menuGO);
                menuGO.hideFlags = HideFlags.HideAndDontSave;
                menuGO.AddComponent<AUGLMenuBehaviour>();
                Log.LogInfo("AUGL Menu GUI Component registered and initialized successfully!");
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to register GUI component: {ex}");
            }

            // 3. Safe Harmony Patching with Crash-Proof Isolation
            var harmony = new Harmony("com.al3x4nderr.auglmod");
            
            GameOptionsPatch.ApplySafe(harmony);
            SabotageBlockPatch.ApplySafe(harmony);
            CosmeticsUnlocker.ApplySafe(harmony);
            ChatCommandsPatch.ApplySafe(harmony);
            FastStartPatch.ApplySafe(harmony);
            AntiLeavePenaltyPatch.ApplySafe(harmony);
            LevelSpooferPatch.ApplySafe(harmony);
            MeetingVoteRevealerPatch.ApplySafe(harmony);
            GhostSpeedPatch.ApplySafe(harmony);
            NoClipPatch.ApplySafe(harmony);
            MaxVisionPatch.ApplySafe(harmony);
            DeadRoleRevealerPatch.ApplySafe(harmony);
            NoKillAnimationPatch.ApplySafe(harmony);
            CustomNameColorPatch.ApplySafe(harmony);
            AntiKickShieldPatch.ApplySafe(harmony);
            AntiCheatManager.ApplySafe(harmony);
            RoleManagerPatch.ApplySafe(harmony);
            LobbyResetPatch.ApplySafe(harmony);
            EndGameResetPatch.ApplySafe(harmony);
            SetKillTimerPatch.ApplySafe(harmony);
            DevTabManager.ApplySafe(harmony);

            // 4. Initialize Discord RPC (Pure C# IPC)
            DiscordRpcManager.Init();

            // 5. Fetch online codes asynchronously & Load Config
            ConfigManager.LoadConfig();
            _ = AUGLMenuGUI.FetchCodes();

            Log.LogInfo("AUGL Mod loaded successfully with all Fun AddOns, Troll & QoL features!");
        }
    }

    // ================= Helper Utilities =================
    public static class ReflectionUtils
    {
        public static void SetMemberValue(object target, string name, object value)
        {
            if (target == null) return;
            var t = target.GetType();
            var f = AccessTools.Field(t, name);
            if (f != null) { f.SetValue(target, value); return; }
            var p = AccessTools.Property(t, name);
            if (p != null) { p.SetValue(target, value); return; }
        }

        public static object GetMemberValue(object target, string name)
        {
            if (target == null) return null;
            var t = target.GetType();
            var f = AccessTools.Field(t, name);
            if (f != null) return f.GetValue(target);
            var p = AccessTools.Property(t, name);
            if (p != null) return p.GetValue(target);
            return null;
        }
    }

    // ================= Config Persistence =================
    public static class ConfigManager
    {
        private static readonly string ConfigPath = Path.Combine(Paths.ConfigPath ?? ".", "AUGLMod.json");

        public class ModConfigData
        {
            public bool PlayStationSpoof { get; set; } = false;
            public bool CosmeticsUnlock { get; set; } = false;
            public bool AntiLeavePenalty { get; set; } = true;
            public bool LevelSpoofer { get; set; } = false;
            public uint CustomLevel { get; set; } = 999;
            public bool GlitchBadgeHUD { get; set; } = true;
            public bool AutoRefresh { get; set; } = true;
            public bool AutoCopyOnJoin { get; set; } = true;
            public bool FpsPingOverlay { get; set; } = true;
            public bool DeadRoleRevealer { get; set; } = true;
            public bool GhostSpeedBooster { get; set; } = true;
            public bool VoteTrackerHUD { get; set; } = true;
            public bool DiscordRPC { get; set; } = true;
            public KeyCode MenuKey { get; set; } = KeyCode.F7;
            public float CameraZoom { get; set; } = 3.0f;
            public bool AntiCheatEnabled { get; set; } = true;
            public List<string> Whitelist { get; set; } = new List<string>();
            public List<string> Blacklist { get; set; } = new List<string>();
        }

        public static ModConfigData Current = new ModConfigData();

        public static void SaveConfig()
        {
            try
            {
                var json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, json);
                AUGLModPlugin.Log?.LogInfo("AUGLMod config saved.");
            }
            catch { }
        }

        public static void LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    Current = JsonSerializer.Deserialize<ModConfigData>(json) ?? new ModConfigData();
                }
            }
            catch { }

            if (Current.MenuKey == KeyCode.None) Current.MenuKey = KeyCode.F7;
            ApplyConfig();
        }

        public static void ApplyConfig()
        {
            if (Current.PlayStationSpoof) PlatformSpoofManager.Enable();
            CosmeticsUnlocker.Enabled = Current.CosmeticsUnlock;
            AntiLeavePenaltyPatch.Enabled = Current.AntiLeavePenalty;
            LevelSpooferPatch.Enabled = Current.LevelSpoofer;
            LevelSpooferPatch.SpoofedLevel = Current.CustomLevel;
            AUGLMenuGUI.ShowGlitchBadgeHUD = Current.GlitchBadgeHUD;
            AUGLMenuGUI.AutoRefreshEnabled = Current.AutoRefresh;
            AUGLMenuGUI.AutoCopyOnJoin = Current.AutoCopyOnJoin;
            AUGLMenuGUI.ShowFpsPingHUD = Current.FpsPingOverlay;
            DeadRoleRevealerPatch.Enabled = Current.DeadRoleRevealer;
            GhostSpeedPatch.Enabled = Current.GhostSpeedBooster;
            MeetingVoteRevealerPatch.Enabled = Current.VoteTrackerHUD;
            DiscordRpcManager.Enabled = Current.DiscordRPC;
            AUGLMenuGUI.ToggleKey = Current.MenuKey != KeyCode.None ? Current.MenuKey : KeyCode.F7;
            AUGLMenuGUI.TargetZoom = Current.CameraZoom;
            AntiCheatManager.Enabled = Current.AntiCheatEnabled;
            AntiCheatManager.Whitelist = Current.Whitelist ?? new List<string>();
            AntiCheatManager.Blacklist = Current.Blacklist ?? new List<string>();
        }
    }

    // ================= API Client =================
    public class GlitchedCodeResponse
    {
        public string Code { get; set; }
        public string Region { get; set; }
        public bool Glitched { get; set; }
        public bool Dormant { get; set; }
        public int Port { get; set; }
    }

    public class GlitchedStatsResponse
    {
        public int Glitched { get; set; }
        public int Total_Codes { get; set; }
        public int CodesPerMin { get; set; }
    }

    public static class AUGLApiClient
    {
        private static readonly HttpClient Client = new HttpClient();
        private const string CodesEndpoint = "https://api.augl.net/v1/codes";
        private const string StatsEndpoint = "https://api.augl.net/v1/stats";

        public static async Task<(List<GlitchedCodeResponse> codes, GlitchedStatsResponse stats)> FetchDataAsync()
        {
            var codesList = new List<GlitchedCodeResponse>();
            var statsResp = new GlitchedStatsResponse();
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            try
            {
                var jsonCodes = await Client.GetStringAsync(CodesEndpoint);
                codesList = JsonSerializer.Deserialize<List<GlitchedCodeResponse>>(jsonCodes, opts) ?? new List<GlitchedCodeResponse>();
            }
            catch (Exception ex)
            {
                AUGLModPlugin.Log?.LogWarning($"API codes fetch warning: {ex.Message}");
            }

            try
            {
                var jsonStats = await Client.GetStringAsync(StatsEndpoint);
                using (var doc = JsonDocument.Parse(jsonStats))
                {
                    var root = doc.RootElement;
                    if (root.TryGetProperty("glitched", out var g)) statsResp.Glitched = g.GetInt32();
                    if (root.TryGetProperty("total_codes", out var t)) statsResp.Total_Codes = t.GetInt32();
                    if (root.TryGetProperty("codes/min", out var c)) statsResp.CodesPerMin = c.GetInt32();
                }
            }
            catch (Exception ex)
            {
                AUGLModPlugin.Log?.LogWarning($"API stats fetch warning: {ex.Message}");
            }

            return (codesList, statsResp);
        }

        public static async Task<List<GlitchedCodeResponse>> FetchCodesAsync()
        {
            var (codes, _) = await FetchDataAsync();
            return codes;
        }
    }

    // ================= Platform Spoofing (BeninexPlugin 1:1) =================
    public static class PlatformSpoofManager
    {
        private static Harmony _harmony;
        private static bool _active;
        public static bool IsActive => _active;

        public static void Enable()
        {
            if (_active) return;
            try
            {
                _harmony = new Harmony("com.augl.spoof");

                var psdType = AccessTools.TypeByName("PlatformSpecificData");
                if (psdType != null)
                {
                    var serializeMethod = AccessTools.Method(psdType, "Serialize");
                    if (serializeMethod != null)
                    {
                        _harmony.Patch(serializeMethod, prefix: new HarmonyMethod(typeof(PlatformSpoofManager).GetMethod(nameof(PrefixSerialize), BindingFlags.NonPublic | BindingFlags.Static)));
                    }
                }

                _active = true;
                ConfigManager.Current.PlayStationSpoof = true;
                AUGLModPlugin.Log?.LogInfo("PlayStation platform spoofing enabled (Beninex 1:1).");
            }
            catch (Exception ex)
            {
                AUGLModPlugin.Log?.LogWarning($"Platform spoof error: {ex.Message}");
            }
        }

        public static void Disable()
        {
            if (!_active) return;
            try
            {
                _harmony?.UnpatchSelf();
            }
            catch { }
            _active = false;
            ConfigManager.Current.PlayStationSpoof = false;
            AUGLModPlugin.Log?.LogInfo("PlayStation platform spoofing disabled.");
        }

        private static void PrefixSerialize(PlatformSpecificData __instance)
        {
            if (!_active || __instance == null) return;
            try
            {
                __instance.Platform = (Platforms)10;
                ulong uid = 1234567890123456789UL;
                __instance.PsnPlatformId = uid;
                __instance.XboxPlatformId = uid;
            }
            catch { }
        }
    }

    // ================= Cosmetics Unlocker =================
    public static class CosmeticsUnlocker
    {
        public static bool Enabled = false;

        public static void ApplySafe(Harmony harmony)
        {
            try
            {
                var ppdType = AccessTools.TypeByName("PlayerPurchasesData");
                if (ppdType != null)
                {
                    var getPurchase = AccessTools.Method(ppdType, "GetPurchase");
                    if (getPurchase != null)
                    {
                        harmony.Patch(getPurchase, prefix: new HarmonyMethod(typeof(CosmeticsUnlocker).GetMethod(nameof(PrefixGetPurchase), BindingFlags.NonPublic | BindingFlags.Static)));
                    }
                }

                var type = AccessTools.TypeByName("HatManager");
                if (type != null)
                {
                    string[] boolMethods = new string[] { "CheckPurchased", "OwnsItem", "HasPurchased", "HasItem" };
                    foreach (var mName in boolMethods)
                    {
                        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                        {
                            if (method.Name == mName && method.ReturnType == typeof(bool))
                            {
                                try
                                {
                                    harmony.Patch(method, prefix: new HarmonyMethod(typeof(CosmeticsUnlocker).GetMethod(nameof(PrefixAlwaysTrue), BindingFlags.NonPublic | BindingFlags.Static)));
                                }
                                catch { }
                            }
                        }
                    }

                    string[] listMethods = new string[] { "GetUnlockedHats", "GetUnlockedSkins", "GetUnlockedPets", "GetUnlockedVisors", "GetUnlockedNameplates" };
                    foreach (var mName in listMethods)
                    {
                        var m = AccessTools.Method(type, mName);
                        if (m != null)
                        {
                            try
                            {
                                harmony.Patch(m, postfix: new HarmonyMethod(typeof(CosmeticsUnlocker).GetMethod(nameof(PostfixUnlockedList), BindingFlags.NonPublic | BindingFlags.Static)));
                            }
                            catch { }
                        }
                    }
                }

                AUGLModPlugin.Log?.LogInfo("CosmeticsUnlocker applied.");
            }
            catch (Exception ex)
            {
                AUGLModPlugin.Log?.LogWarning($"CosmeticsUnlocker error: {ex.Message}");
            }
        }

        private static bool PrefixGetPurchase(string itemKey, string bundleKey, ref bool __result)
        {
            if (!Enabled) return true;
            try
            {
                if (DestroyableSingleton<CosmicubeManager>.InstanceExists)
                {
                    CosmicubeManager manager = DestroyableSingleton<CosmicubeManager>.Instance;
                    if (manager != null && manager.allCubes != null)
                    {
                        foreach (CosmicubeData cube in manager.allCubes)
                        {
                            if (cube == null) continue;
                            string[] ids = { cube.ProdId, cube.productId, cube.podId };
                            foreach (string id in ids)
                            {
                                if (string.IsNullOrWhiteSpace(id)) continue;
                                if (string.Equals(itemKey, id, StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(bundleKey, id, StringComparison.OrdinalIgnoreCase))
                                {
                                    __result = true;
                                    return false;
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            __result = true;
            return false;
        }

        private static bool PrefixAlwaysTrue(ref bool __result)
        {
            if (!Enabled) return true;
            __result = true;
            return false;
        }

        private static void PostfixUnlockedList(MethodBase __originalMethod, ref Il2CppSystem.Object __result)
        {
            if (!Enabled || __result == null) return;
            try
            {
                var hmType = AccessTools.TypeByName("HatManager");
                var instProp = hmType?.GetProperty("Instance");
                var hm = instProp?.GetValue(null);
                if (hm == null) return;

                string allFieldName = null;
                if (__originalMethod.Name.Contains("Hat")) allFieldName = "allHats";
                else if (__originalMethod.Name.Contains("Skin")) allFieldName = "allSkins";
                else if (__originalMethod.Name.Contains("Pet")) allFieldName = "allPets";
                else if (__originalMethod.Name.Contains("Visor")) allFieldName = "allVisors";
                else if (__originalMethod.Name.Contains("Nameplate")) allFieldName = "allNamePlates";

                if (allFieldName != null)
                {
                    var allList = ReflectionUtils.GetMemberValue(hm, allFieldName);
                    if (allList != null)
                    {
                        __result = allList as Il2CppSystem.Object ?? __result;
                    }
                }
            }
            catch { }
        }
    }

    // ================= Anti-Leave Matchmaking Penalty =================
    public static class AntiLeavePenaltyPatch
    {
        public static bool Enabled = true;

        public static void ApplySafe(Harmony harmony)
        {
            try
            {
                var banDataType = AccessTools.TypeByName("PlayerBanData");
                if (banDataType != null)
                {
                    var banPointsProp = AccessTools.Property(banDataType, "BanPoints");
                    var setter = banPointsProp?.GetSetMethod();
                    if (setter != null)
                    {
                        harmony.Patch(setter, prefix: new HarmonyMethod(typeof(AntiLeavePenaltyPatch).GetMethod(nameof(PrefixSetBanPoints), BindingFlags.NonPublic | BindingFlags.Static)));
                    }

                    var banMins = AccessTools.Method(banDataType, "get_BanMinutesLeft");
                    if (banMins != null)
                    {
                        harmony.Patch(banMins, prefix: new HarmonyMethod(typeof(AntiLeavePenaltyPatch).GetMethod(nameof(PrefixBanMinutesLeft), BindingFlags.NonPublic | BindingFlags.Static)));
                    }
                }

                var accountType = AccessTools.TypeByName("AccountManager");
                if (accountType != null)
                {
                    var canPlay = AccessTools.Method(accountType, "CanPlayOnline");
                    if (canPlay != null)
                    {
                        harmony.Patch(canPlay, prefix: new HarmonyMethod(typeof(AntiLeavePenaltyPatch).GetMethod(nameof(PrefixCanPlayOnline), BindingFlags.NonPublic | BindingFlags.Static)));
                    }
                }

                AUGLModPlugin.Log?.LogInfo("AntiLeavePenaltyPatch applied (BeninexPlugin 1:1).");
            }
            catch (Exception ex)
            {
                AUGLModPlugin.Log?.LogWarning($"AntiLeavePenaltyPatch warning: {ex.Message}");
            }
        }

        private static bool PrefixSetBanPoints(ref float value)
        {
            if (!Enabled) return true;
            try
            {
                if (AmongUsClient.Instance == null || AmongUsClient.Instance.NetworkMode != NetworkModes.OnlineGame)
                    return true;
            }
            catch { }

            value = 0f;
            return false;
        }

        private static bool PrefixBanMinutesLeft(ref int __result)
        {
            if (!Enabled) return true;
            __result = 0;
            return false;
        }

        private static bool PrefixCanPlayOnline(ref bool __result)
        {
            if (!Enabled) return true;
            __result = true;
            return false;
        }
    }

    // ================= Level Spoofer =================
    public static class LevelSpooferPatch
    {
        public static bool Enabled = false;
        public static uint SpoofedLevel = 999;

        public static void ApplySafe(Harmony harmony)
        {
            try
            {
                var saveType = AccessTools.TypeByName("SaveManager");
                if (saveType != null)
                {
                    var getLevel = AccessTools.Method(saveType, "get_PlayerLevel");
                    if (getLevel != null)
                    {
                        harmony.Patch(getLevel, prefix: new HarmonyMethod(typeof(LevelSpooferPatch).GetMethod(nameof(PrefixPlayerLevel), BindingFlags.NonPublic | BindingFlags.Static)));
                    }
                }
                AUGLModPlugin.Log?.LogInfo("LevelSpooferPatch applied.");
            }
            catch (Exception ex)
            {
                AUGLModPlugin.Log?.LogWarning($"LevelSpooferPatch warning: {ex.Message}");
            }
        }

        private static bool PrefixPlayerLevel(ref uint __result)
        {
            if (!Enabled) return true;
            __result = SpoofedLevel;
            return false;
        }
    }

    // ================= Meeting Vote Revealer =================
    public static class MeetingVoteRevealerPatch
    {
        public static bool Enabled = true;
        public static readonly Dictionary<byte, byte> LiveVotes = new Dictionary<byte, byte>();

        public static void ApplySafe(Harmony harmony)
        {
            try
            {
                var meetingType = AccessTools.TypeByName("MeetingHud");
                if (meetingType != null)
                {
                    var castVote = AccessTools.Method(meetingType, "CastVote") ?? AccessTools.Method(meetingType, "RpcCastVote");
                    if (castVote != null)
                    {
                        harmony.Patch(castVote, prefix: new HarmonyMethod(typeof(MeetingVoteRevealerPatch).GetMethod(nameof(PrefixCastVote), BindingFlags.NonPublic | BindingFlags.Static)));
                    }

                    var startMethod = AccessTools.Method(meetingType, "Start") ?? AccessTools.Method(meetingType, "Awake");
                    if (startMethod != null)
                    {
                        harmony.Patch(startMethod, postfix: new HarmonyMethod(typeof(MeetingVoteRevealerPatch).GetMethod(nameof(PostfixMeetingStart), BindingFlags.NonPublic | BindingFlags.Static)));
                    }
                }
                AUGLModPlugin.Log?.LogInfo("MeetingVoteRevealerPatch applied.");
            }
            catch (Exception ex)
            {
                AUGLModPlugin.Log?.LogWarning($"MeetingVoteRevealerPatch warning: {ex.Message}");
            }
        }

        private static void PostfixMeetingStart()
        {
            LiveVotes.Clear();
        }

        private static void PrefixCastVote(Il2CppSystem.Object __instance, byte srcPlayerId, byte suspectPlayerId)
        {
            if (!Enabled) return;
            try
            {
                LiveVotes[srcPlayerId] = suspectPlayerId;
            }
            catch { }
        }
    }

    // ================= Ghost Speed Booster =================
    public static class GhostSpeedPatch
    {
        public static bool Enabled = true;
        public static float GhostSpeedMultiplier = 2.5f;

        public static void ApplySafe(Harmony harmony)
        {
            try
            {
                var pcType = AccessTools.TypeByName("PlayerPhysics");
                if (pcType != null)
                {
                    var fixedUpdate = AccessTools.Method(pcType, "FixedUpdate");
                    if (fixedUpdate != null)
                    {
                        harmony.Patch(fixedUpdate, postfix: new HarmonyMethod(typeof(GhostSpeedPatch).GetMethod(nameof(PostfixFixedUpdate), BindingFlags.NonPublic | BindingFlags.Static)));
                    }
                }
            }
            catch { }
        }

        private static void PostfixFixedUpdate(Il2CppSystem.Object __instance)
        {
            if (!Enabled || __instance == null || GhostSpeedMultiplier <= 1.0f) return;
            try
            {
                var myPlayerProp = AccessTools.Property(__instance.GetType(), "myPlayer");
                var myPlayer = myPlayerProp?.GetValue(__instance);
                if (myPlayer == null) return;

                if (!(myPlayer is PlayerControl pc) || pc.Data == null) return;

                bool isDead = pc.Data.IsDead;

                if (isDead)
                {
                    var speedField = AccessTools.Field(__instance.GetType(), "Speed");
                    if (speedField != null)
                    {
                        float spd = (float)speedField.GetValue(__instance);
                        speedField.SetValue(__instance, spd * GhostSpeedMultiplier);
                    }
                }
            }
            catch { }
        }
    }

    // ================= NoClip (Troll Tab) =================
    public static class NoClipPatch
    {
        public static bool Enabled = false;

        public static void ApplySafe(Harmony harmony)
        {
            // Driven directly per-frame in AUGLMenuBehaviour.Update
        }
    }

    // ================= Max Vision & Wall Vision (Troll Tab) =================
    public static class MaxVisionPatch
    {
        public static bool Enabled = false;

        public static void ApplySafe(Harmony harmony)
        {
            try
            {
                var ssType = AccessTools.TypeByName("ShipStatus");
                if (ssType != null)
                {
                    var calcLight = AccessTools.Method(ssType, "CalculateLightRadius");
                    if (calcLight != null)
                    {
                        harmony.Patch(calcLight, prefix: new HarmonyMethod(typeof(MaxVisionPatch).GetMethod(nameof(PrefixCalculateLightRadius), BindingFlags.NonPublic | BindingFlags.Static)));
                    }
                }
            }
            catch { }
        }

        private static bool PrefixCalculateLightRadius(ref float __result)
        {
            if (!Enabled)
            {
                try
                {
                    if (HudManager.Instance != null && HudManager.Instance.ShadowQuad != null)
                    {
                        HudManager.Instance.ShadowQuad.gameObject.SetActive(true);
                    }
                }
                catch { }
                return true;
            }

            try
            {
                if (HudManager.Instance != null && HudManager.Instance.ShadowQuad != null)
                {
                    HudManager.Instance.ShadowQuad.gameObject.SetActive(false);
                }
            }
            catch { }

            __result = 1000f;
            return false;
        }
    }

    // ================= Dead Role Revealer (Fun AddOns) =================
    public static class DeadRoleRevealerPatch
    {
        public static bool Enabled = true;
        private static FieldInfo _nameTextCache;

        public static void ApplySafe(Harmony harmony)
        {
            try
            {
                var pcType = AccessTools.TypeByName("PlayerControl");
                if (pcType != null)
                {
                    var update = AccessTools.Method(pcType, "Update");
                    if (update != null)
                    {
                        harmony.Patch(update, postfix: new HarmonyMethod(typeof(DeadRoleRevealerPatch).GetMethod(nameof(PostfixPlayerUpdate), BindingFlags.NonPublic | BindingFlags.Static)));
                    }
                }
            }
            catch { }
        }

        private static void PostfixPlayerUpdate(Il2CppSystem.Object __instance)
        {
            if (!Enabled || __instance == null) return;
            try
            {
                var localPlayer = PlayerControl.LocalPlayer;
                if (localPlayer == null || localPlayer.Data == null) return;

                bool isDead = localPlayer.Data.IsDead;

                if (isDead && __instance is PlayerControl targetPc && targetPc.Data != null)
                {
                    bool isImp = targetPc.Data.Role?.IsImpostor ?? false;
                    if (isImp)
                    {
                        if (_nameTextCache == null) _nameTextCache = AccessTools.Field(targetPc.GetType(), "nameText") ?? AccessTools.Field(targetPc.GetType(), "NameText");
                        var nameTextObj = _nameTextCache?.GetValue(targetPc);
                        if (nameTextObj != null)
                        {
                            ReflectionUtils.SetMemberValue(nameTextObj, "color", Color.red);
                            ReflectionUtils.SetMemberValue(nameTextObj, "color32", (Color32)Color.red);
                        }
                    }
                }
            }
            catch { }
        }
    }

    // ================= No Kill Animation (Fun AddOns) =================
    public static class NoKillAnimationPatch
    {
        public static bool Enabled = false;

        public static void ApplySafe(Harmony harmony)
        {
            try
            {
                var koType = AccessTools.TypeByName("KillOverlay");
                if (koType != null)
                {
                    foreach (var m in koType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                    {
                        if (m.Name == "ShowKillAnimation")
                        {
                            try
                            {
                                harmony.Patch(m, prefix: new HarmonyMethod(typeof(NoKillAnimationPatch).GetMethod(nameof(PrefixShowKill), BindingFlags.NonPublic | BindingFlags.Static)));
                            }
                            catch { }
                        }
                    }
                    AUGLModPlugin.Log?.LogInfo("NoKillAnimationPatch applied.");
                }
            }
            catch { }
        }

        private static bool PrefixShowKill()
        {
            if (Enabled) return false;
            return true;
        }
    }

    // ================= Custom Name Colors (Fun AddOns) =================
    public static class CustomNameColorPatch
    {
        public static bool Enabled = true;

        public static void ApplySafe(Harmony harmony)
        {
            try
            {
                var smType = AccessTools.TypeByName("SaveManager");
                if (smType != null)
                {
                    var checkName = AccessTools.Method(smType, "CheckName");
                    if (checkName != null)
                    {
                        harmony.Patch(checkName, prefix: new HarmonyMethod(typeof(CustomNameColorPatch).GetMethod(nameof(PrefixCheckName), BindingFlags.NonPublic | BindingFlags.Static)));
                    }
                }

                var pcType = AccessTools.TypeByName("PlayerControl");
                if (pcType != null)
                {
                    var setPlayerName = AccessTools.Method(pcType, "RpcSetName") ?? AccessTools.Method(pcType, "CmdCheckName");
                    if (setPlayerName != null)
                    {
                        harmony.Patch(setPlayerName, prefix: new HarmonyMethod(typeof(CustomNameColorPatch).GetMethod(nameof(PrefixCheckName), BindingFlags.NonPublic | BindingFlags.Static)));
                    }
                }
            }
            catch { }
        }

        private static bool PrefixCheckName(ref string name, ref bool __result)
        {
            if (Enabled && !string.IsNullOrEmpty(name) && name.Contains("<color="))
            {
                __result = true;
                return false;
            }
            return true;
        }
    }

    // ================= Anti-Kick Shield (Fun AddOns) =================
    public static class AntiKickShieldPatch
    {
        public static bool Enabled = true;

        public static void ApplySafe(Harmony harmony)
        {
            try
            {
                var incType = AccessTools.TypeByName("InnerNetClient");
                if (incType != null)
                {
                    foreach (var m in incType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        if (m.Name == "HandleDisconnect")
                        {
                            try
                            {
                                harmony.Patch(m, prefix: new HarmonyMethod(typeof(AntiKickShieldPatch).GetMethod(nameof(PrefixHandleDisconnect), BindingFlags.NonPublic | BindingFlags.Static)));
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }
        }

        private static bool PrefixHandleDisconnect(Il2CppSystem.Object reason, string customReason)
        {
            if (!Enabled) return true;
            try
            {
                if (!string.IsNullOrEmpty(customReason) && customReason.ToLower().Contains("kicked"))
                {
                    AUGLModPlugin.Log?.LogInfo("AntiKickShield blocked kick attempt.");
                    return false;
                }
            }
            catch { }
            return true;
        }
    }

    // ================= Pure C# Discord RPC Manager =================
    public static class DiscordRpcManager
    {
        public static bool Enabled = true;
        private static Stream _stream;
        private static bool _connected = false;
        private static bool _connecting = false;
        private static float _lastUpdateTime = 0f;
        private const string ClientId = "1543635619476013086";

        public static void Init()
        {
            _ = Task.Run(() =>
            {
                try
                {
                    ConnectPipe();
                }
                catch { }
            });
        }

        private static void ConnectPipe()
        {
            if (_connecting) return;
            _connecting = true;
            try
            {
                // 1. Try Windows / Standard Named Pipe first
                for (int i = 0; i < 10; i++)
                {
                    try
                    {
                        var np = new NamedPipeClientStream(".", $"discord-ipc-{i}", PipeDirection.InOut);
                        np.Connect(30);
                        if (np.IsConnected)
                        {
                            _stream = np;
                            _connected = true;
                            SendHandshake();
                            AUGLModPlugin.Log?.LogInfo($"Discord RPC connected via Windows NamedPipe {i}.");
                            return;
                        }
                    }
                    catch { }
                }

                // 2. Try Unix Domain Sockets for Linux / Steam Deck / Flatpak / Wine / Proton
                string xdgRuntime = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
                string[] searchDirs = new string[]
                {
                    xdgRuntime,
                    "/tmp",
                    !string.IsNullOrEmpty(xdgRuntime) ? Path.Combine(xdgRuntime, "app", "com.discordapp.Discord") : null,
                    "/tmp/app/com.discordapp.Discord"
                };

                foreach (var dir in searchDirs)
                {
                    if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;

                    for (int i = 0; i < 10; i++)
                    {
                        string socketPath = Path.Combine(dir, $"discord-ipc-{i}");
                        if (!File.Exists(socketPath)) continue;

                        try
                        {
                            var np = new NamedPipeClientStream(".", socketPath, PipeDirection.InOut);
                            np.Connect(30);
                            if (np.IsConnected)
                            {
                                _stream = np;
                                _connected = true;
                                SendHandshake();
                                AUGLModPlugin.Log?.LogInfo($"Discord RPC connected via Unix Pipe at {socketPath}.");
                                return;
                            }
                        }
                        catch { }

                        try
                        {
                            var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                            socket.Connect(new UnixDomainSocketEndPoint(socketPath));
                            if (socket.Connected)
                            {
                                _stream = new NetworkStream(socket, true);
                                _connected = true;
                                SendHandshake();
                                AUGLModPlugin.Log?.LogInfo($"Discord RPC connected via Unix Socket at {socketPath}.");
                                return;
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { _connected = false; }
            finally { _connecting = false; }
        }

        private static void SendHandshake()
        {
            try
            {
                if (!_connected || _stream == null) return;
                string json = $"{{\"v\":1,\"client_id\":\"{ClientId}\"}}";
                byte[] body = Encoding.UTF8.GetBytes(json);
                byte[] header = new byte[8];
                BitConverter.GetBytes(0).CopyTo(header, 0);
                BitConverter.GetBytes(body.Length).CopyTo(header, 4);
                _stream.Write(header, 0, 8);
                _stream.Write(body, 0, body.Length);
                _stream.Flush();
            }
            catch { _connected = false; }
        }

        public static void UpdatePresence(string details, string state, string partyCode = null, int partySize = 0, int partyMax = 15)
        {
            if (!Enabled || !_connected || _stream == null) return;
            try
            {
                var presenceObj = new
                {
                    cmd = "SET_ACTIVITY",
                    args = new
                    {
                        pid = System.Diagnostics.Process.GetCurrentProcess().Id,
                        activity = new
                        {
                            details = details ?? "Playing Among Us with AUGL Mod",
                            state = state ?? "In Menu",
                            assets = new
                            {
                                large_image = "among_us_logo",
                                large_text = "AUGL Menu 04.09.18"
                            },
                            party = string.IsNullOrEmpty(partyCode) ? null : new { id = partyCode, size = new int[] { partySize, partyMax } }
                        }
                    },
                    nonce = Guid.NewGuid().ToString()
                };

                string json = JsonSerializer.Serialize(presenceObj);
                byte[] body = Encoding.UTF8.GetBytes(json);
                byte[] header = new byte[8];
                BitConverter.GetBytes(1).CopyTo(header, 0);
                BitConverter.GetBytes(body.Length).CopyTo(header, 4);
                _stream.Write(header, 0, 8);
                _stream.Write(body, 0, body.Length);
                _stream.Flush();
            }
            catch
            {
                _connected = false;
            }
        }

        public static void PeriodicUpdate()
        {
            if (!Enabled) return;
            if (Time.time - _lastUpdateTime < 5f) return;
            _lastUpdateTime = Time.time;

            if (!_connected && !_connecting)
            {
                _ = Task.Run(() => ConnectPipe());
                return;
            }

            string curCode = AUGLMenuGUI.GetCurrentLobbyCode();
            bool isGlitched = AUGLMenuGUI.IsCodeGlitched(curCode);

            if (!string.IsNullOrEmpty(curCode))
            {
                string state = isGlitched ? $"⚡ Glitched Lobby: {curCode}" : $"Lobby: {curCode}";
                UpdatePresence("AUGL Mod Menu Active", state, curCode, 1, 15);
            }
            else
            {
                UpdatePresence("AUGL Mod Active", "Browsing Lobbies");
            }
        }
    }

    // ================= Chat Commands =================
    public static class ChatCommandsPatch
    {
        public static void ApplySafe(Harmony harmony)
        {
            try
            {
                var type = AccessTools.TypeByName("ChatController");
                if (type == null) return;

                var method = AccessTools.Method(type, "SendChat") ?? AccessTools.Method(type, "SendFreeChat");
                if (method != null)
                {
                    harmony.Patch(method, prefix: new HarmonyMethod(typeof(ChatCommandsPatch).GetMethod(nameof(Prefix), BindingFlags.NonPublic | BindingFlags.Static)));
                    AUGLModPlugin.Log?.LogInfo("ChatCommandsPatch applied.");
                }
            }
            catch (Exception ex)
            {
                AUGLModPlugin.Log?.LogWarning($"ChatCommandsPatch error: {ex.Message}");
            }
        }

        private static bool Prefix(Il2CppSystem.Object __instance)
        {
            try
            {
                if (__instance == null) return true;
                var chatControllerType = __instance.GetType();

                var freeChatField = AccessTools.Field(chatControllerType, "freeChatField")?.GetValue(__instance);
                if (freeChatField == null) return true;

                var textProp = AccessTools.Property(freeChatField.GetType(), "Text") ?? AccessTools.Property(freeChatField.GetType(), "text");
                string text = textProp?.GetValue(freeChatField) as string;

                if (string.IsNullOrWhiteSpace(text) || !text.StartsWith("/")) return true;

                string[] parts = text.Trim().Split(' ');
                string cmd = parts[0].ToLower();
                textProp?.SetValue(freeChatField, "");

                var addChatMethod = AccessTools.Method(chatControllerType, "AddChat", new Type[] { AccessTools.TypeByName("PlayerControl"), typeof(string) });
                var localPlayerProp = AccessTools.Property(AccessTools.TypeByName("PlayerControl"), "LocalPlayer");
                var localPlayer = localPlayerProp?.GetValue(null);

                void ShowChatMsg(string msg)
                {
                    try
                    {
                        addChatMethod?.Invoke(__instance, new object[] { localPlayer, $"<color=#00FFFF>[AUGL]</color> {msg}" });
                    }
                    catch { }
                }

                bool isHost = false;
                var clientType = AccessTools.TypeByName("AmongUsClient");
                var clientInst = clientType?.GetProperty("Instance")?.GetValue(null);
                if (clientInst != null)
                {
                    var amHostProp = clientType.GetProperty("AmHost");
                    isHost = (bool)(amHostProp?.GetValue(clientInst) ?? false);
                }

                switch (cmd)
                {
                    case "/help":
                        ShowChatMsg("Commands: /codes, /glitch, /spoof, /level, /start, /endgame, /endmeeting, /kick, /ban, /tpout, /tpin, /color, /menu, /help");
                        break;
                    case "/menu":
                    case "/gui":
                        AUGLMenuGUI.ToggleOpen();
                        ShowChatMsg("Menu toggled.");
                        break;
                    case "/spoof":
                        if (PlatformSpoofManager.IsActive) { PlatformSpoofManager.Disable(); ShowChatMsg("PlayStation Spoof: <color=#FF4444>OFF</color>"); }
                        else { PlatformSpoofManager.Enable(); ShowChatMsg("PlayStation Spoof: <color=#00FF66>ON (PS)</color>"); }
                        break;
                    case "/glitch":
                    case "/check":
                        string curCode = AUGLMenuGUI.GetCurrentLobbyCode();
                        if (string.IsNullOrEmpty(curCode)) ShowChatMsg("Not currently in a lobby.");
                        else
                        {
                            bool isGlitched = AUGLMenuGUI.IsCodeGlitched(curCode);
                            if (isGlitched) ShowChatMsg($"Lobby <color=#00FF66>{curCode}</color> is <color=#00FF66>GLITCHED</color>!");
                            else ShowChatMsg($"Lobby <color=#FFFFFF>{curCode}</color> is <color=#FFAA00>NORMAL</color>.");
                        }
                        break;
                    case "/codes":
                        var glitched = AUGLMenuGUI.GetGlitchedCodesList();
                        if (glitched.Count == 0) ShowChatMsg("No active glitched codes online.");
                        else ShowChatMsg($"Glitched codes ({glitched.Count}): " + string.Join(", ", glitched.ConvertAll(c => c.Code)));
                        break;
                    case "/start":
                        if (isHost) HostQoLManager.InstantStart();
                        else ShowChatMsg("Host-only command.");
                        break;
                    case "/tpout":
                        TeleportLocalPlayer(new Vector2(9999f, 9999f));
                        ShowChatMsg("Teleported outside.");
                        break;
                    case "/tpin":
                        TeleportLocalPlayer(Vector2.zero);
                        ShowChatMsg("Teleported center.");
                        break;
                    case "/color":
                        if (parts.Length > 1 && int.TryParse(parts[1], out int cId)) ForceLocalPlayerColor((byte)cId);
                        break;
                    default:
                        ShowChatMsg($"Unknown command '{text}'. Type /help.");
                        break;
                }

                return false;
            }
            catch (Exception ex)
            {
                AUGLModPlugin.Log?.LogWarning($"Chat command handling exception: {ex.Message}");
                return true;
            }
        }

        public static void TeleportLocalPlayer(Vector2 pos)
        {
            try
            {
                var localPlayerProp = AccessTools.Property(AccessTools.TypeByName("PlayerControl"), "LocalPlayer");
                var localPlayer = localPlayerProp?.GetValue(null);
                if (localPlayer == null) return;

                var transformProp = AccessTools.Property(localPlayer.GetType(), "transform");
                var transform = transformProp?.GetValue(localPlayer);
                if (transform != null)
                {
                    ReflectionUtils.SetMemberValue(transform, "position", new Vector3(pos.x, pos.y, -1f));
                }

                var netTransformProp = AccessTools.Property(localPlayer.GetType(), "NetTransform");
                var netTransform = netTransformProp?.GetValue(localPlayer);
                if (netTransform != null)
                {
                    var rpcSnap = AccessTools.Method(netTransform.GetType(), "RpcSnapTo", new Type[] { typeof(Vector2) })
                                ?? AccessTools.Method(netTransform.GetType(), "RpcSnapTo");
                    rpcSnap?.Invoke(netTransform, new object[] { pos });

                    var snapMethod = AccessTools.Method(netTransform.GetType(), "SnapTo", new Type[] { typeof(Vector2), typeof(ushort) })
                                  ?? AccessTools.Method(netTransform.GetType(), "SnapTo");
                    snapMethod?.Invoke(netTransform, new object[] { pos, (ushort)0 });
                }
            }
            catch { }
        }

        public static void ForceLocalPlayerColor(byte colorId)
        {
            try
            {
                var lp = PlayerControl.LocalPlayer;
                if (lp == null) return;
                lp.CmdCheckColor(colorId);
                lp.RpcSetColor(colorId);
            }
            catch { }
        }
    }

    // ================= Host Tools & Fast Start =================
    public static class HostQoLManager
    {
        public static bool FastStartEnabled = false;
        public static float PlayerSpeed = 1.0f;
        public static float CrewLight = 1.0f;
        public static float ImpLight = 1.5f;

        public static void InstantStart()
        {
            try
            {
                var lbType = AccessTools.TypeByName("LobbyBehaviour");
                var lbInstProp = lbType?.GetProperty("Instance");
                var lb = lbInstProp?.GetValue(null);
                if (lb != null)
                {
                    var timerField = AccessTools.Field(lbType, "CountDownTimer");
                    if (timerField != null) timerField.SetValue(lb, 0.05f);
                    var startMethod = AccessTools.Method(lbType, "StartGame");
                    startMethod?.Invoke(lb, null);
                    AUGLModPlugin.Log?.LogInfo("Instant start triggered.");
                }
            }
            catch (Exception ex)
            {
                AUGLModPlugin.Log?.LogWarning($"InstantStart exception: {ex.Message}");
            }
        }

        public static void ApplyHostSettings()
        {
            try
            {
                var gom = GameOptionsManager.Instance;
                var options = gom?.CurrentGameOptions;
                if (options == null) return;

                ReflectionUtils.SetMemberValue(options, "PlayerSpeedMod", PlayerSpeed);
                ReflectionUtils.SetMemberValue(options, "CrewLightMod", CrewLight);
                ReflectionUtils.SetMemberValue(options, "ImpostorLightMod", ImpLight);

                var setFloat = options.GetType().GetMethods()
                    .FirstOrDefault(m => m.Name == "SetFloat" && m.GetParameters().Length == 2
                        && m.GetParameters()[0].ParameterType != typeof(int));
                if (setFloat == null)
                {
                    setFloat = AccessTools.Method(options.GetType(), "SetFloat");
                }
                if (setFloat != null)
                {
                    var ps = setFloat.GetParameters();
                    if (ps.Length == 2)
                    {
                        object speed2 = Enum.ToObject(ps[0].ParameterType, 2);
                        object crew3 = Enum.ToObject(ps[0].ParameterType, 3);
                        object imp4 = Enum.ToObject(ps[0].ParameterType, 4);
                        try { setFloat.Invoke(options, new object[] { speed2, PlayerSpeed }); } catch { }
                        try { setFloat.Invoke(options, new object[] { crew3, CrewLight }); } catch { }
                        try { setFloat.Invoke(options, new object[] { imp4, ImpLight }); } catch { }
                    }
                }

                SyncCurrentOptions();
                AUGLModPlugin.Log?.LogInfo($"Host settings synced: Speed={PlayerSpeed}x, CrewLight={CrewLight}x, ImpLight={ImpLight}x");
            }
            catch (Exception ex)
            {
                AUGLModPlugin.Log?.LogWarning($"ApplyHostSettings exception: {ex.Message}");
            }
        }

        public static void SetMaxPlayers25()
        {
            try
            {
                var gom = GameOptionsManager.Instance;
                if (gom == null) return;
                var options = gom.CurrentGameOptions;
                if (options == null) return;

                var maxPlayersProp = AccessTools.Property(options.GetType(), "MaxPlayers");
                if (maxPlayersProp != null)
                {
                    maxPlayersProp.SetValue(options, 25);
                }
                else
                {
                    var setInt = AccessTools.Method(options.GetType(), "SetInt", new Type[] { typeof(int), typeof(int) });
                    setInt?.Invoke(options, new object[] { 0, 25 });
                }

                SyncCurrentOptions();
                AUGLModPlugin.Log?.LogInfo("Host QoL: Set Max Players to 25 (public vanilla compatible).");
            }
            catch (Exception ex)
            {
                AUGLModPlugin.Log?.LogWarning($"SetMaxPlayers25 error: {ex.Message}");
            }
        }

        public static void SyncCurrentOptions()
        {
            try
            {
                var client = AmongUsClient.Instance;
                if (client == null || !client.AmHost) return;

                var gom = GameOptionsManager.Instance;
                var options = gom?.CurrentGameOptions;
                if (options == null) return;

                try
                {
                    var syncMethod = AccessTools.Method(typeof(GameOptionsManager), "SyncGameOptions")
                                  ?? AccessTools.Method(typeof(GameOptionsManager), "Dirty");
                    syncMethod?.Invoke(gom, null);
                }
                catch { }

                try
                {
                    var lp = PlayerControl.LocalPlayer;
                    if (lp != null)
                    {
                        MethodInfo toBytesMethod = options.GetType().GetMethod("ToBytes", new Type[] { typeof(byte) })
                                                ?? options.GetType().GetMethod("ToBytes", Type.EmptyTypes);
                        object bytes = null;
                        if (toBytesMethod != null)
                        {
                            var ps = toBytesMethod.GetParameters();
                            bytes = ps.Length == 1 ? toBytesMethod.Invoke(options, new object[] { (byte)4 }) : toBytesMethod.Invoke(options, null);
                        }

                        if (bytes != null)
                        {
                            var rpcSync = lp.GetType().GetMethods().FirstOrDefault(m => m.Name == "RpcSyncSettings" && m.GetParameters().Length == 1);
                            rpcSync?.Invoke(lp, new object[] { bytes });
                        }
                    }
                }
                catch { }
            }
            catch (Exception ex)
            {
                AUGLModPlugin.Log?.LogWarning($"SyncCurrentOptions error: {ex.Message}");
            }
        }
    }

    // ================= Anti-Cheat, Whitelist & Blacklist =================
    public static class AntiCheatManager
    {
        public static bool Enabled = true;
        public static List<string> Whitelist = new List<string>();
        public static List<string> Blacklist = new List<string>();

        public static bool IsWhitelisted(string name, string friendCode = null)
        {
            if (string.IsNullOrEmpty(name)) return false;
            foreach (var w in Whitelist)
            {
                if (string.Equals(w.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
                if (!string.IsNullOrEmpty(friendCode) && string.Equals(w.Trim(), friendCode.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public static bool IsBlacklisted(string name, string friendCode = null)
        {
            if (string.IsNullOrEmpty(name)) return false;
            foreach (var b in Blacklist)
            {
                if (string.Equals(b.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
                if (!string.IsNullOrEmpty(friendCode) && string.Equals(b.Trim(), friendCode.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public static void ApplySafe(Harmony harmony)
        {
            try
            {
                var pcType = AccessTools.TypeByName("PlayerControl");
                if (pcType != null)
                {
                    var initMethod = AccessTools.Method(pcType, "Initialize");
                    if (initMethod != null)
                    {
                        harmony.Patch(initMethod, postfix: new HarmonyMethod(typeof(AntiCheatManager).GetMethod(nameof(PostfixPlayerInit), BindingFlags.NonPublic | BindingFlags.Static)));
                    }

                    var murderMethod = AccessTools.Method(pcType, "MurderPlayer");
                    if (murderMethod != null)
                    {
                        harmony.Patch(murderMethod, prefix: new HarmonyMethod(typeof(AntiCheatManager).GetMethod(nameof(PrefixMurderPlayer), BindingFlags.NonPublic | BindingFlags.Static)));
                    }
                }

                var voteKickType = AccessTools.TypeByName("VoteBanSystem");
                if (voteKickType != null)
                {
                    var cmdAddVote = AccessTools.Method(voteKickType, "CmdAddVote") ?? AccessTools.Method(voteKickType, "RpcAddVote");
                    if (cmdAddVote != null)
                    {
                        harmony.Patch(cmdAddVote, prefix: new HarmonyMethod(typeof(AntiCheatManager).GetMethod(nameof(PrefixAddVoteKick), BindingFlags.NonPublic | BindingFlags.Static)));
                    }
                }

                AUGLModPlugin.Log?.LogInfo("AntiCheatManager applied with Whitelist & Blacklist protection.");
            }
            catch (Exception ex)
            {
                AUGLModPlugin.Log?.LogWarning($"AntiCheatManager error: {ex.Message}");
            }
        }

        private static bool PrefixAddVoteKick(int targetClientId)
        {
            if (!Enabled) return true;
            try
            {
                var client = AmongUsClient.Instance;
                if (client != null)
                {
                    if (targetClientId == client.HostId || targetClientId == client.ClientId)
                    {
                        AUGLModPlugin.Log?.LogWarning($"Blocked malicious vote kick targeting Host (client {targetClientId})!");
                        AUGLMenuGUI.TriggerToast("Anti-Cheat: Blocked Vote Kick on Host!");
                        return false;
                    }
                }
            }
            catch { }
            return true;
        }

        private static void PostfixPlayerInit(PlayerControl __instance)
        {
            if (!Enabled || __instance == null || AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return;
            try
            {
                if (__instance.Data == null) return;
                string pName = __instance.Data.PlayerName;
                string fc = __instance.Data.FriendCode;

                if (IsWhitelisted(pName, fc))
                {
                    AUGLModPlugin.Log?.LogInfo($"Whitelisted friend joined: {pName}");
                    return;
                }

                if (IsBlacklisted(pName, fc))
                {
                    AUGLModPlugin.Log?.LogWarning($"Auto-banning blacklisted player: {pName}");
                    AmongUsClient.Instance.KickPlayer(__instance.OwnerId, true);
                    AUGLMenuGUI.TriggerToast($"Banned Blacklisted Player: {pName}");
                }
            }
            catch { }
        }

        private static bool PrefixMurderPlayer(PlayerControl __instance, PlayerControl target)
        {
            if (!Enabled || __instance == null || AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return true;
            try
            {
                if (__instance.Data == null) return true;
                string pName = __instance.Data.PlayerName;
                string fc = __instance.Data.FriendCode;

                if (IsWhitelisted(pName, fc)) return true;

                if (!__instance.Data.Role.IsImpostor)
                {
                    AUGLModPlugin.Log?.LogWarning($"Illegal murder attempt blocked from non-impostor: {pName}");
                    AmongUsClient.Instance.KickPlayer(__instance.OwnerId, false);
                    AUGLMenuGUI.TriggerToast($"Anti-Cheat: Kicked {pName} (Illegal Kill)");
                    return false;
                }
            }
            catch { }
            return true;
        }
    }

    // ================= Reset & Game State Patches for Players Tab =================
    public static class LobbyResetPatch
    {
        public static void ApplySafe(Harmony harmony)
        {
            try
            {
                var type = AccessTools.TypeByName("LobbyBehaviour");
                if (type == null) return;
                var method = AccessTools.Method(type, "Start");
                if (method != null)
                {
                    harmony.Patch(method, postfix: new HarmonyMethod(typeof(LobbyResetPatch).GetMethod(nameof(Postfix), BindingFlags.NonPublic | BindingFlags.Static)));
                }
            }
            catch { }
        }

        private static void Postfix()
        {
            PlayersTabManager.ResetAllPlayerLoops();
        }
    }

    public static class EndGameResetPatch
    {
        public static void ApplySafe(Harmony harmony)
        {
            try
            {
                var type = AccessTools.TypeByName("EndGameManager");
                if (type == null) return;
                var method = AccessTools.Method(type, "Start");
                if (method != null)
                {
                    harmony.Patch(method, postfix: new HarmonyMethod(typeof(EndGameResetPatch).GetMethod(nameof(Postfix), BindingFlags.NonPublic | BindingFlags.Static)));
                }
            }
            catch { }
        }

        private static void Postfix()
        {
            PlayersTabManager.ResetAllPlayerLoops();
        }
    }

    public static class SetKillTimerPatch
    {
        public static void ApplySafe(Harmony harmony)
        {
            try
            {
                var type = AccessTools.TypeByName("PlayerControl");
                if (type == null) return;
                var method = AccessTools.Method(type, "SetKillTimer");
                if (method != null)
                {
                    harmony.Patch(method, prefix: new HarmonyMethod(typeof(SetKillTimerPatch).GetMethod(nameof(Prefix), BindingFlags.NonPublic | BindingFlags.Static)));
                }
            }
            catch { }
        }

        private static void Prefix(PlayerControl __instance, ref float time)
        {
            try
            {
                if (PlayersTabManager.PermanentMurderLoops.Count == 0) return;
                if (__instance != PlayerControl.LocalPlayer) return;
                time = 0f;
            }
            catch { }
        }
    }

    // ================= Players Tab Manager (Beninex Reference) =================
    public static class PlayersTabManager
    {
        public static byte SelectedPlayerId = 255;
        public static readonly HashSet<byte> PermanentShields = new HashSet<byte>();
        public static readonly HashSet<byte> PermanentMurderLoops = new HashSet<byte>();
        public static readonly Dictionary<byte, RoleTypes> ForcedRoles = new Dictionary<byte, RoleTypes>();
        public static int TargetRoleIdx = 0;
        private static float _lastMurderLoopTick = 0f;

        public static void ResetAllPlayerLoops()
        {
            PermanentShields.Clear();
            PermanentMurderLoops.Clear();
            ForcedRoles.Clear();
            SelectedPlayerId = 255;
        }

        public static readonly RoleTypes[] RolesList =
        {
            RoleTypes.Impostor,
            RoleTypes.Shapeshifter,
            RoleTypes.Phantom,
            RoleTypes.Viper,
            RoleTypes.GuardianAngel,
            RoleTypes.Crewmate,
            RoleTypes.Scientist,
            RoleTypes.Engineer,
            RoleTypes.Noisemaker,
            RoleTypes.Tracker,
            RoleTypes.Detective
        };

        public static readonly string[] RoleNames =
        {
            "Impostor",
            "Shapeshifter",
            "Phantom",
            "Viper",
            "Guardian Angel",
            "Crewmate",
            "Scientist",
            "Engineer",
            "Noisemaker",
            "Tracker",
            "Detective"
        };

        public static List<PlayerControl> GetPlayers()
        {
            var list = new List<PlayerControl>();
            try
            {
                if (PlayerControl.AllPlayerControls == null) return list;
                foreach (var p in PlayerControl.AllPlayerControls)
                {
                    if (p != null && p.Data != null && !p.Data.Disconnected) list.Add(p);
                }
            }
            catch { }
            return list;
        }

        public static PlayerControl GetPlayerById(byte id)
        {
            try
            {
                if (PlayerControl.AllPlayerControls == null) return null;
                foreach (var p in PlayerControl.AllPlayerControls)
                {
                    if (p != null && p.Data != null && p.Data.PlayerId == id)
                        return p;
                }
            }
            catch { }
            return null;
        }

        public static void ApplyRoleToPlayer(PlayerControl target, RoleTypes role)
        {
            try
            {
                if (target == null || target.Data == null) return;

                if (RoleManager.Instance != null)
                {
                    RoleManager.Instance.SetRole(target, role);
                }

                target.RpcSetRole(role, true);

                if (target.Data != null)
                {
                    target.Data.RoleType = role;
                    if (target.Data.Role != null)
                    {
                        target.Data.Role.Initialize(target);
                    }
                }
            }
            catch { }
        }

        public static void Tick()
        {
            try
            {
                // Auto reset when game ends, left game, or not started
                if (AmongUsClient.Instance == null || 
                    AmongUsClient.Instance.GameState != InnerNetClient.GameStates.Started ||
                    LobbyBehaviour.Instance != null ||
                    ShipStatus.Instance == null)
                {
                    if (PermanentShields.Count > 0 || PermanentMurderLoops.Count > 0)
                    {
                        ResetAllPlayerLoops();
                    }
                    return;
                }

                PlayerControl local = PlayerControl.LocalPlayer;
                if (local == null || local.Data == null || local.Data.IsDead || local.Data.Disconnected)
                {
                    if (PermanentShields.Count > 0 || PermanentMurderLoops.Count > 0)
                    {
                        ResetAllPlayerLoops();
                    }
                    return;
                }

                // 1. Maintain Permanent Shields
                if (PermanentShields.Count > 0)
                {
                    foreach (byte pid in PermanentShields.ToList())
                    {
                        PlayerControl target = GetPlayerById(pid);
                        if (target == null || target.Data == null || target.Data.IsDead || target.Data.Disconnected)
                        {
                            PermanentShields.Remove(pid);
                            continue;
                        }

                        if (target.protectedByGuardianId < 0)
                        {
                            int col = 0;
                            try { col = (int)target.Data.DefaultOutfit.ColorId; } catch { }
                            local.RpcProtectPlayer(target, col);
                        }
                    }
                }

                // 2. Execute Murder Loops (50Hz / 50 RPC/s / 20ms / 0.020f interval gate)
                if (PermanentMurderLoops.Count > 0)
                {
                    if (Time.unscaledTime - _lastMurderLoopTick >= 0.020f)
                    {
                        _lastMurderLoopTick = Time.unscaledTime;

                        foreach (byte pid in PermanentMurderLoops.ToList())
                        {
                            PlayerControl target = GetPlayerById(pid);
                            if (target == null || target.Data == null || target.Data.IsDead || target.Data.Disconnected)
                            {
                                PermanentMurderLoops.Remove(pid);
                                continue;
                            }

                            local.RpcMurderPlayer(target, false);
                        }
                    }
                }
            }
            catch { }
        }
    }

    // ================= Role Selection Harmony Patch =================
    public static class RoleManagerPatch
    {
        public static void ApplySafe(Harmony harmony)
        {
            try
            {
                var rmType = AccessTools.TypeByName("RoleManager");
                if (rmType != null)
                {
                    var selectRoles = AccessTools.Method(rmType, "SelectRoles");
                    if (selectRoles != null)
                    {
                        harmony.Patch(selectRoles, postfix: new HarmonyMethod(typeof(RoleManagerPatch).GetMethod(nameof(PostfixSelectRoles), BindingFlags.NonPublic | BindingFlags.Static)));
                        AUGLModPlugin.Log?.LogInfo("RoleManagerPatch applied.");
                    }
                }
            }
            catch (Exception ex)
            {
                AUGLModPlugin.Log?.LogWarning($"RoleManagerPatch warning: {ex.Message}");
            }
        }

        private static void PostfixSelectRoles()
        {
            if (PlayersTabManager.ForcedRoles.Count == 0) return;
            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return;

            try
            {
                var players = PlayersTabManager.GetPlayers();
                foreach (var pc in players)
                {
                    if (pc != null && pc.Data != null && !pc.Data.IsDead)
                    {
                        if (PlayersTabManager.ForcedRoles.TryGetValue(pc.PlayerId, out RoleTypes role))
                        {
                            PlayersTabManager.ApplyRoleToPlayer(pc, role);
                        }
                    }
                }
            }
            catch { }
        }
    }

    // =========================================================
    // DEV & MATCH MANAGER (Backend Logic & EndGame Harmony Patch)
    // =========================================================
    public static class DevTabManager
    {
        public static bool EnabledNoGameEnd = false;
        public static void ForceStartGame()
        {
            try
            {
                var lbType = AccessTools.TypeByName("LobbyBehaviour");
                var lbInst = lbType?.GetProperty("Instance")?.GetValue(null);
                if (lbInst != null)
                {
                    var timerField = AccessTools.Field(lbType, "CountDownTimer");
                    if (timerField != null) timerField.SetValue(lbInst, 0f);
                    var startMethod = AccessTools.Method(lbType, "StartGame");
                    startMethod?.Invoke(lbInst, null);
                }
                var aucType = AccessTools.TypeByName("AmongUsClient");
                var aucInst = aucType?.GetProperty("Instance")?.GetValue(null);
                if (aucInst != null)
                {
                    var aucStart = AccessTools.Method(aucType, "StartGame");
                    aucStart?.Invoke(aucInst, null);
                }
                AUGLModPlugin.Log?.LogInfo("[DevTab] Force Start triggered!");
            }
            catch (Exception ex)
            {
                AUGLModPlugin.Log?.LogWarning($"ForceStart exception: {ex.Message}");
            }
        }
        public static void ApplySafe(Harmony harmony)
        {
            try
            {
                var gmType = AccessTools.TypeByName("GameManager");
                if (gmType != null)
                {
                    var rpcEnd = AccessTools.Method(gmType, "RpcEndGame") ?? AccessTools.Method(gmType, "EndGame");
                    if (rpcEnd != null)
                    {
                        harmony.Patch(rpcEnd, prefix: new HarmonyMethod(typeof(DevTabManager).GetMethod(nameof(PrefixRpcEndGame), BindingFlags.NonPublic | BindingFlags.Static)));
                    }
                }
                AUGLModPlugin.Log?.LogInfo("DevTabManager patches applied.");
            }
            catch (Exception ex)
            {
                AUGLModPlugin.Log?.LogWarning($"DevTabManager patch error: {ex.Message}");
            }
        }
        private static bool PrefixRpcEndGame()
        {
            if (EnabledNoGameEnd)
            {
                AUGLModPlugin.Log?.LogInfo("[DevTab] Blocked Game End (No Game End active)");
                return false;
            }
            return true;
        }
    }

    public static class FastStartPatch
    {
        public static void ApplySafe(Harmony harmony)
        {
            try
            {
                var type = AccessTools.TypeByName("LobbyBehaviour");
                if (type == null) return;

                var method = AccessTools.Method(type, "Update");
                if (method != null)
                {
                    harmony.Patch(method, postfix: new HarmonyMethod(typeof(FastStartPatch).GetMethod(nameof(Postfix), BindingFlags.NonPublic | BindingFlags.Static)));
                }
            }
            catch { }
        }

        private static void Postfix(Il2CppSystem.Object __instance)
        {
            if (!HostQoLManager.FastStartEnabled || __instance == null) return;
            try
            {
                var timerField = AccessTools.Field(__instance.GetType(), "CountDownTimer");
                if (timerField != null)
                {
                    float timer = (float)timerField.GetValue(__instance);
                    if (timer > 1f)
                    {
                        timerField.SetValue(__instance, 1f);
                    }
                }
            }
            catch { }
        }
    }

    // ================= Region Installer =================
    public static class RegionInstaller
    {
        public const string DefaultRegionJson = @"{
            ""CurrentRegionIdx"": 1,
            ""Regions"": [
                { ""$type"": ""StaticHttpRegionInfo, Assembly-CSharp"", ""Name"": ""North America"", ""PingServer"": ""matchmaker.among.us"", ""Servers"": [ { ""Name"": ""Http-1"", ""Ip"": ""https://matchmaker.among.us"", ""Port"": 443, ""UseDtls"": false } ], ""TargetServer"": null, ""TranslateName"": 289 },
                { ""$type"": ""StaticHttpRegionInfo, Assembly-CSharp"", ""Name"": ""Europe"", ""PingServer"": ""matchmaker-eu.among.us"", ""Servers"": [ { ""Name"": ""Http-1"", ""Ip"": ""https://matchmaker-eu.among.us"", ""Port"": 443, ""UseDtls"": false } ], ""TargetServer"": null, ""TranslateName"": 290 },
                { ""$type"": ""StaticHttpRegionInfo, Assembly-CSharp"", ""Name"": ""Asia"", ""PingServer"": ""matchmaker-as.among.us"", ""Servers"": [ { ""Name"": ""Http-1"", ""Ip"": ""https://matchmaker-as.among.us"", ""Port"": 443, ""UseDtls"": false } ], ""TargetServer"": null, ""TranslateName"": 291 },
                { ""$type"": ""StaticHttpRegionInfo, Assembly-CSharp"", ""Name"": ""<#AA37F7>A</color><#B361E8>U</color><#BD7CD8>G</color><#C793C7>L</color><#CEA0BA> </color><#D6AEAC>C</color><#E1C094>o</color><#EDD175>d</color><#F9E246>e</color><#FFEA00>s</color>"", ""PingServer"": ""augl.net"", ""Servers"": [ { ""Name"": ""Http-1"", ""Ip"": ""augl.net"", ""Port"": 443, ""UseDtls"": false } ], ""TargetServer"": null, ""TranslateName"": 1003 }
            ]
        }";

        public const string ModdedRegionsJson = @"{
            ""Regions"": [
                { ""$type"": ""StaticHttpRegionInfo, Assembly-CSharp"", ""Name"": ""Modded EU (MEU)"", ""PingServer"": ""https://au-eu.duikbo.at"", ""Servers"": [ { ""Name"": ""Http-1"", ""Ip"": ""https://au-eu.duikbo.at"", ""Port"": 443, ""UseDtls"": false } ], ""TargetServer"": null, ""TranslateName"": 1003 },
                { ""$type"": ""StaticHttpRegionInfo, Assembly-CSharp"", ""Name"": ""Modded NA (MNA)"", ""PingServer"": ""https://aumods.org"", ""Servers"": [ { ""Name"": ""Http-1"", ""Ip"": ""https://aumods.org"", ""Port"": 443, ""UseDtls"": false } ], ""TargetServer"": null, ""TranslateName"": 1003 },
                { ""$type"": ""StaticHttpRegionInfo, Assembly-CSharp"", ""Name"": ""Modded Asia (MAS)"", ""PingServer"": ""https://au-as.duikbo.at"", ""Servers"": [ { ""Name"": ""Http-1"", ""Ip"": ""https://au-as.duikbo.at"", ""Port"": 443, ""UseDtls"": false } ], ""TargetServer"": null, ""TranslateName"": 1003 },
                { ""$type"": ""StaticHttpRegionInfo, Assembly-CSharp"", ""Name"": ""Niko233 (NA)"", ""PingServer"": ""https://au-us.niko233.top"", ""Servers"": [ { ""Name"": ""Http-1"", ""Ip"": ""https://au-us.niko233.top"", ""Port"": 443, ""UseDtls"": false } ], ""TargetServer"": null, ""TranslateName"": 1003 },
                { ""$type"": ""StaticHttpRegionInfo, Assembly-CSharp"", ""Name"": ""Niko233 (AS)"", ""PingServer"": ""https://au-as.niko233.top"", ""Servers"": [ { ""Name"": ""Http-1"", ""Ip"": ""https://au-as.niko233.top"", ""Port"": 443, ""UseDtls"": false } ], ""TargetServer"": null, ""TranslateName"": 1003 },
                { ""$type"": ""StaticHttpRegionInfo, Assembly-CSharp"", ""Name"": ""Niko233 (EU)"", ""PingServer"": ""https://au-eu.niko233.top"", ""Servers"": [ { ""Name"": ""Http-1"", ""Ip"": ""https://au-eu.niko233.top"", ""Port"": 443, ""UseDtls"": false } ], ""TargetServer"": null, ""TranslateName"": 1003 },
                { ""$type"": ""StaticHttpRegionInfo, Assembly-CSharp"", ""Name"": ""AllOfUs (EU)"", ""PingServer"": ""https://eu.allofus.dev"", ""Servers"": [ { ""Name"": ""Http-1"", ""Ip"": ""https://eu.allofus.dev"", ""Port"": 443, ""UseDtls"": false } ], ""TargetServer"": null, ""TranslateName"": 1003 }
            ]
        }";

        public static bool Enabled = true;

        public static void Inject(bool includeModded = false)
        {
            if (!Enabled) return;
            try
            {
                var smType = AccessTools.TypeByName("ServerManager");
                var instanceProp = smType?.GetProperty("Instance");
                var sm = instanceProp?.GetValue(null);
                if (sm == null) return;

                var field = AccessTools.Field(smType, "availableServers") ?? AccessTools.Field(smType, "AvailableRegions");
                var list = field?.GetValue(sm) as IList;
                if (list == null) return;

                var regionType = AccessTools.TypeByName("StaticHttpRegionInfo");
                var data = JsonSerializer.Deserialize<RegionData>(DefaultRegionJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (data?.Regions == null) return;

                InjectRegionList(list, regionType, data.Regions);

                if (includeModded)
                {
                    var moddedData = JsonSerializer.Deserialize<RegionData>(ModdedRegionsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (moddedData?.Regions != null)
                    {
                        InjectRegionList(list, regionType, moddedData.Regions);
                        AUGLModPlugin.Log?.LogInfo("Community modded regions injected on request.");
                    }
                }

                AUGLModPlugin.Log?.LogInfo("Standard regions (Official + AUGL) verified.");
            }
            catch (Exception ex) { AUGLModPlugin.Log?.LogError($"Region error: {ex.Message}"); }
        }

        private static void InjectRegionList(IList list, Type regionType, List<RegionInfo> regions)
        {
            foreach (var r in regions)
            {
                bool exists = false;
                foreach (var existing in list)
                {
                    var nameProp = AccessTools.Property(existing.GetType(), "Name");
                    if ((string)nameProp?.GetValue(existing) == r.Name) { exists = true; break; }
                }
                if (exists) continue;

                var newRegion = Activator.CreateInstance(regionType);
                AccessTools.Property(regionType, "Name")?.SetValue(newRegion, r.Name);
                AccessTools.Property(regionType, "PingServer")?.SetValue(newRegion, r.PingServer);

                var serversField = AccessTools.Field(regionType, "Servers") ?? AccessTools.Field(regionType, "servers");
                if (serversField != null)
                {
                    var listType = serversField.FieldType;
                    var servers = Activator.CreateInstance(listType);
                    var addMethod = listType.GetMethod("Add");
                    foreach (var s in r.Servers)
                    {
                        var serverType = listType.GetGenericArguments()[0];
                        var server = Activator.CreateInstance(serverType);
                        AccessTools.Property(serverType, "Name")?.SetValue(server, s.Name);
                        AccessTools.Property(serverType, "Ip")?.SetValue(server, s.Ip);
                        AccessTools.Property(serverType, "Port")?.SetValue(server, s.Port);
                        AccessTools.Property(serverType, "UseDtls")?.SetValue(server, s.UseDtls);
                        addMethod.Invoke(servers, new object[] { server });
                    }
                    serversField.SetValue(newRegion, servers);
                }
                list.Add(newRegion);
            }
        }

        private class RegionData { public List<RegionInfo> Regions { get; set; } }
        private class RegionInfo { public string Name { get; set; } public string PingServer { get; set; } public List<ServerInfo> Servers { get; set; } }
        private class ServerInfo { public string Name { get; set; } public string Ip { get; set; } public int Port { get; set; } public bool UseDtls { get; set; } }
    }

    // ================= Unlocker Manager =================
    public static class UnlockerManager
    {
        public static bool ShouldApply = false;
        public static float KillCooldown = 0f;
        public static float AngelDuration = 30f;
        public static float AngelCooldown = 0f;

        public static void ApplyKillCd(float cd)
        {
            KillCooldown = cd;
            ShouldApply = true;
            SyncWithGame();
        }

        public static void ApplyAngel(float d)
        {
            AngelDuration = d;
            ShouldApply = true;
            SyncWithGame();
        }

        public static void ApplyAngelCd(float cd)
        {
            AngelCooldown = cd;
            ShouldApply = true;
            SyncWithGame();
        }

        public static void Reset()
        {
            ShouldApply = false;
            KillCooldown = 0f;
            AngelDuration = 30f;
            AngelCooldown = 0f;
            SyncWithGame();
        }

        public static void SyncWithGame()
        {
            try
            {
                var gomType = AccessTools.TypeByName("GameOptionsManager");
                var instanceProp = gomType?.GetProperty("Instance");
                var gom = instanceProp?.GetValue(null);
                if (gom == null) return;

                var currentOptionsProp = gomType.GetProperty("CurrentGameOptions");
                var options = currentOptionsProp?.GetValue(gom);
                if (options == null) return;

                Type optType = options.GetType();

                var setFloat = AccessTools.Method(optType, "SetFloat", new Type[] { typeof(int), typeof(float) })
                            ?? AccessTools.Method(optType, "SetFloat");

                var setInt = AccessTools.Method(optType, "SetInt", new Type[] { typeof(int), typeof(int) })
                          ?? AccessTools.Method(optType, "SetInt");

                if (setFloat != null)
                {
                    setFloat.Invoke(options, new object[] { 1, KillCooldown });
                    setFloat.Invoke(options, new object[] { 1100, AngelDuration });
                    setFloat.Invoke(options, new object[] { 0x44C, AngelDuration });

                    int[] angelCdKeys = new int[] { 1098, 1099, 1101, 1096, 1097, 0x44A, 0x44B, 0x44D };
                    foreach (int key in angelCdKeys)
                    {
                        setFloat.Invoke(options, new object[] { key, AngelCooldown });
                        setInt?.Invoke(options, new object[] { key, (int)AngelCooldown });
                    }
                }

                foreach (var prop in optType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    string pName = prop.Name.ToLower();
                    if ((pName.Contains("angel") || pName.Contains("guardian")) && pName.Contains("cooldown"))
                    {
                        if (prop.PropertyType == typeof(float)) prop.SetValue(options, AngelCooldown);
                        else if (prop.PropertyType == typeof(int)) prop.SetValue(options, (int)AngelCooldown);
                    }
                }

                foreach (var field in optType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    string fName = field.Name.ToLower();
                    if ((fName.Contains("angel") || fName.Contains("guardian")) && fName.Contains("cooldown"))
                    {
                        if (field.FieldType == typeof(float)) field.SetValue(options, AngelCooldown);
                        else if (field.FieldType == typeof(int)) field.SetValue(options, (int)AngelCooldown);
                    }
                }

                AUGLModPlugin.Log?.LogInfo($"Unlocked options synced: KillCD={KillCooldown}s, AngelDur={AngelDuration}s, AngelCD={AngelCooldown}s");

                var syncMethod = AccessTools.Method(gomType, "SyncGameOptions") ?? AccessTools.Method(gomType, "Dirty");
                syncMethod?.Invoke(gom, null);
            }
            catch (Exception ex)
            {
                AUGLModPlugin.Log?.LogWarning($"SyncWithGame exception: {ex.Message}");
            }
        }
    }

    public static class GameOptionsPatch
    {
        public static void ApplySafe(Harmony harmony)
        {
            try
            {
                var typesToSearch = new string[] { "IGameOptionsExtensions", "GameOptionsManager", "NormalGameOptionsV07", "GameOptionsData", "IGameOptions" };
                bool hooked = false;
                foreach (var name in typesToSearch)
                {
                    var t = AccessTools.TypeByName(name);
                    if (t != null)
                    {
                        var m = AccessTools.Method(t, "ToBytes");
                        if (m != null)
                        {
                            harmony.Patch(m, postfix: new HarmonyMethod(typeof(GameOptionsPatch).GetMethod("Postfix", BindingFlags.NonPublic | BindingFlags.Static)));
                            AUGLModPlugin.Log?.LogInfo($"GameOptionsPatch successfully hooked {name}.ToBytes!");
                            hooked = true;
                            break;
                        }
                    }
                }

                if (!hooked)
                {
                    AUGLModPlugin.Log?.LogWarning("GameOptionsPatch: ToBytes method not found across candidate types.");
                }
            }
            catch (Exception ex)
            {
                AUGLModPlugin.Log?.LogWarning($"GameOptionsPatch error: {ex.Message}");
            }
        }

        private static void Postfix(Il2CppSystem.Object __instance)
        {
            if (!UnlockerManager.ShouldApply || __instance == null) return;
            try
            {
                var clientType = AccessTools.TypeByName("AmongUsClient");
                var instanceProp = clientType?.GetProperty("Instance");
                var client = instanceProp?.GetValue(null);
                if (client == null) return;
                var amHostProp = clientType?.GetProperty("AmHost");
                bool amHost = (bool)(amHostProp?.GetValue(client) ?? false);
                var isPublicField = AccessTools.Field(clientType, "_IsGamePublic_k__BackingField");
                bool isPublic = (bool)(isPublicField?.GetValue(client) ?? false);
                if (!amHost || isPublic) return;

                var setFloat = AccessTools.Method(__instance.GetType(), "SetFloat", new Type[] { typeof(int), typeof(float) });
                setFloat?.Invoke(__instance, new object[] { (int)1, UnlockerManager.KillCooldown });
                
                int[] angelCdKeys = new int[] { 1098, 1099, 1101, 1096, 1097, 0x44A, 0x44B, 0x44D };
                foreach (int key in angelCdKeys)
                {
                    setFloat?.Invoke(__instance, new object[] { key, UnlockerManager.AngelCooldown });
                }
                setFloat?.Invoke(__instance, new object[] { (int)0x44C, UnlockerManager.AngelDuration });
            }
            catch { }
        }
    }

    // ================= Game Modes (SNS, Shields, Normal) =================
    public static class GameModePresetManager
    {
        public static bool SabotageBlocking = false;
        public static string ActiveModeName = "Normal";

        public static void ApplyPreset(string name)
        {
            try
            {
                var client = AmongUsClient.Instance;
                if (client == null || !client.AmHost)
                {
                    AUGLMenuGUI.TriggerToast("Must be Lobby Host to change mode!");
                    return;
                }

                var gom = GameOptionsManager.Instance;
                var options = gom?.CurrentGameOptions;
                if (options == null)
                {
                    AUGLMenuGUI.TriggerToast("No game options loaded yet!");
                    return;
                }

                MethodInfo setInt = options.GetType().GetMethods()
                    .FirstOrDefault(m => m.Name == "SetInt" && m.GetParameters().Length == 2
                        && m.GetParameters()[0].ParameterType != typeof(int));
                MethodInfo setFloat = options.GetType().GetMethods()
                    .FirstOrDefault(m => m.Name == "SetFloat" && m.GetParameters().Length == 2
                        && m.GetParameters()[0].ParameterType != typeof(int));

                Type intEnumType = setInt?.GetParameters()[0].ParameterType;
                Type floatEnumType = setFloat?.GetParameters()[0].ParameterType;

                void TrySetInt(int enumVal, int value)
                {
                    try { setInt?.Invoke(options, new object[] { Enum.ToObject(intEnumType!, enumVal), value }); } catch { }
                }
                void TrySetFloat(int enumVal, float value)
                {
                    try { setFloat?.Invoke(options, new object[] { Enum.ToObject(floatEnumType!, enumVal), value }); } catch { }
                }

                ActiveModeName = name;

                switch (name)
                {
                    case "SNS":
                        SabotageBlocking = true;
                        TrySetInt(0, 2);   // NumImpostors = 2
                        TrySetFloat(1, 12.5f); // KillCooldown = 12.5
                        ReflectionUtils.SetMemberValue(options, "NumImpostors", 2);
                        ReflectionUtils.SetMemberValue(options, "KillCooldown", 12.5f);

                        ResetAllRolesToZero(options);
                        SetRoleSettings(options, "Shapeshifter", 2, 100, 7f, 35f);
                        SetRoleSettings(options, "Engineer", 13, 100, 5f, 5f);
                        break;

                    case "Shields":
                        SabotageBlocking = false;
                        TrySetInt(0, 1);   // NumImpostors = 1
                        TrySetFloat(1, 0f); // KillCooldown = 0
                        ReflectionUtils.SetMemberValue(options, "NumImpostors", 1);
                        ReflectionUtils.SetMemberValue(options, "KillCooldown", 0f);

                        ResetAllRolesToZero(options);
                        SetRoleSettings(options, "Impostor", 1, 100, 0f, 0f);
                        SetRoleSettings(options, "Engineer", 14, 100, 0f, 9999f);
                        SetRoleSettings(options, "GuardianAngel", 15, 100, 0f, 9999f);
                        break;

                    case "Normal":
                    default:
                        SabotageBlocking = false;
                        TrySetInt(0, 2);
                        TrySetFloat(1, 15f);
                        ReflectionUtils.SetMemberValue(options, "NumImpostors", 2);
                        ReflectionUtils.SetMemberValue(options, "KillCooldown", 15f);
                        
                        SetRoleSettings(options, "Scientist", 1, 50, 15f, 5f);
                        SetRoleSettings(options, "Engineer", 1, 50, 30f, 15f);
                        SetRoleSettings(options, "GuardianAngel", 1, 50, 60f, 10f);
                        SetRoleSettings(options, "Shapeshifter", 1, 50, 10f, 30f);
                        ActiveModeName = "Normal (Roles Galore)";
                        break;
                }

                HostQoLManager.SyncCurrentOptions();
                AUGLMenuGUI.TriggerToast($"Gamemode applied: {ActiveModeName}");
                AUGLModPlugin.Log?.LogInfo($"Preset '{name}' applied successfully.");
            }
            catch (Exception ex)
            {
                AUGLModPlugin.Log?.LogWarning($"ApplyPreset exception: {ex.Message}");
            }
        }

        private static Type FindRoleTypesEnum()
        {
            return AccessTools.TypeByName("AmongUs.GameOptions.RoleTypes")
                ?? AccessTools.TypeByName("RoleTypes")
                ?? AccessTools.TypeByName("RoleType");
        }

        private static void ResetAllRolesToZero(object options)
        {
            string[] roles = { "Scientist", "Engineer", "GuardianAngel", "Shapeshifter", "Tracker", "Noisemaker", "Phantom", "Impostor" };
            foreach (var r in roles)
            {
                SetRoleSettings(options, r, 0, 0, 0, 0);
            }
        }

        private static void SetRoleSettings(object options, string roleName, int count, int chance, float cooldown, float duration)
        {
            try
            {
                Type roleTypesEnum = FindRoleTypesEnum();
                object roleEnumValue = null;

                if (roleTypesEnum != null)
                {
                    try
                    {
                        roleEnumValue = Enum.Parse(roleTypesEnum, roleName, true);
                    }
                    catch { }
                }

                object roleCollection = ReflectionUtils.GetMemberValue(options, "RoleOptions")
                                     ?? ReflectionUtils.GetMemberValue(options, "roleOptionsCollectionV10")
                                     ?? ReflectionUtils.GetMemberValue(options, "RoleOptionsCollection")
                                     ?? options;

                if (roleEnumValue != null)
                {
                    MethodInfo setRoleRate = options.GetType().GetMethods().FirstOrDefault(m => m.Name == "SetRoleRate" && m.GetParameters().Length == 3)
                                          ?? roleCollection.GetType().GetMethods().FirstOrDefault(m => m.Name == "SetRoleRate" && m.GetParameters().Length == 3);

                    if (setRoleRate != null)
                    {
                        var targetObj = setRoleRate.DeclaringType.IsAssignableFrom(options.GetType()) ? options : roleCollection;
                        setRoleRate.Invoke(targetObj, new object[] { roleEnumValue, count, chance });
                    }

                    MethodInfo getRoleOptions = roleCollection.GetType().GetMethods().FirstOrDefault(m => m.Name == "GetRoleOptions" && m.GetParameters().Length >= 1);
                    if (getRoleOptions != null)
                    {
                        object[] args = getRoleOptions.GetParameters().Length == 1 ? new object[] { roleEnumValue } : new object[] { roleEnumValue, true };
                        object roleData = getRoleOptions.Invoke(roleCollection, args);
                        if (roleData != null)
                        {
                            ReflectionUtils.SetMemberValue(roleData, "Count", count);
                            ReflectionUtils.SetMemberValue(roleData, "Chance", chance);
                            foreach (var f in roleData.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                            {
                                string fn = f.Name.ToLower();
                                if (fn.Contains("cooldown")) f.SetValue(roleData, cooldown);
                                else if (fn.Contains("duration") || fn.Contains("time")) f.SetValue(roleData, duration);
                            }
                        }
                    }
                }

                foreach (var prop in roleCollection.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (prop.Name.IndexOf(roleName, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var roleData = prop.GetValue(roleCollection);
                        if (roleData != null)
                        {
                            ReflectionUtils.SetMemberValue(roleData, "Count", count);
                            ReflectionUtils.SetMemberValue(roleData, "Chance", chance);
                            foreach (var f in roleData.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                            {
                                string fn = f.Name.ToLower();
                                if (fn.Contains("cooldown")) f.SetValue(roleData, cooldown);
                                else if (fn.Contains("duration") || fn.Contains("time")) f.SetValue(roleData, duration);
                            }
                        }
                    }
                }
            }
            catch { }
        }
    }

    public static class SabotageBlockPatch
    {
        public static void ApplySafe(Harmony harmony)
        {
            try
            {
                var type = AccessTools.TypeByName("ShipStatus");
                var method = AccessTools.Method(type, "RpcUpdateSystem");
                if (method != null)
                {
                    harmony.Patch(method, prefix: new HarmonyMethod(typeof(SabotageBlockPatch).GetMethod("Prefix", BindingFlags.NonPublic | BindingFlags.Static)));
                    AUGLModPlugin.Log?.LogInfo("SabotageBlockPatch applied.");
                }

                var doorMethod = AccessTools.Method(type, "RpcCloseDoorsOfType");
                if (doorMethod != null)
                {
                    harmony.Patch(doorMethod, prefix: new HarmonyMethod(typeof(SabotageBlockPatch).GetMethod("PrefixDoors", BindingFlags.NonPublic | BindingFlags.Static)));
                    AUGLModPlugin.Log?.LogInfo("Door sabotage block hooked.");
                }
            }
            catch (Exception ex) { AUGLModPlugin.Log?.LogWarning($"SabotageBlockPatch error: {ex.Message}"); }
        }

        private static bool Prefix(Il2CppSystem.Object __instance, SystemTypes systemType, byte amount)
        {
            try
            {
                if (!GameModePresetManager.SabotageBlocking) return true;

                if (systemType == SystemTypes.Comms || (int)systemType == 14)
                {
                    return true;
                }

                return false;
            }
            catch { return true; }
        }

        private static bool PrefixDoors()
        {
            if (GameModePresetManager.SabotageBlocking)
            {
                return false;
            }
            return true;
        }
    }

    // ================= GUI via IL2CPP Registered Behaviour =================
    public class AUGLMenuBehaviour : MonoBehaviour
    {
        public AUGLMenuBehaviour(IntPtr ptr) : base(ptr) { }

        private float _lastAutoRefreshTime = 0f;
        private string _lastObservedLobbyCode = null;

        private void Update()
        {
            try
            {
                KeyCode key = AUGLMenuGUI.ToggleKey;
                if (key == KeyCode.None) key = KeyCode.F7;

                if (Input.GetKeyDown(key))
                {
                    AUGLMenuGUI.ToggleOpen();
                }

                try
                {
                    var lp = PlayerControl.LocalPlayer;
                    if (lp != null)
                    {
                        Collider2D col = null;
                        try { col = (Collider2D)ReflectionUtils.GetMemberValue(lp, "myCollider"); } catch { }
                        if (col == null) col = lp.GetComponent<Collider2D>();
                        if (col != null) col.enabled = !NoClipPatch.Enabled;
                    }
                }
                catch { }

                if (Camera.main != null && AUGLMenuGUI.TargetZoom > 0.5f)
                {
                    float scroll = Input.GetAxis("Mouse ScrollWheel");
                    if (Mathf.Abs(scroll) > 0.01f && !AUGLMenuGUI.IsOpen)
                    {
                        AUGLMenuGUI.TargetZoom = Mathf.Clamp(AUGLMenuGUI.TargetZoom - (scroll * 2.5f), 1.0f, 15.0f);
                    }
                    Camera.main.orthographicSize = Mathf.Lerp(Camera.main.orthographicSize, AUGLMenuGUI.TargetZoom, Time.deltaTime * 5f);
                }

                if (AUGLMenuGUI.AutoRefreshEnabled && Time.time - _lastAutoRefreshTime > AUGLMenuGUI.AutoRefreshInterval)
                {
                    _lastAutoRefreshTime = Time.time;
                    _ = AUGLMenuGUI.FetchCodes();
                }

                if (AUGLMenuGUI.AutoCopyOnJoin)
                {
                    string currentCode = AUGLMenuGUI.GetCurrentLobbyCode();
                    if (!string.IsNullOrEmpty(currentCode) && currentCode != _lastObservedLobbyCode)
                    {
                        _lastObservedLobbyCode = currentCode;
                        GUIUtility.systemCopyBuffer = currentCode;
                        AUGLMenuGUI.TriggerToast($"Lobby Code Copied: {currentCode}");
                        AUGLModPlugin.Log?.LogInfo($"Auto-copied lobby code: {currentCode}");
                    }
                }

                DiscordRpcManager.PeriodicUpdate();
                PlayersTabManager.Tick();
            }
            catch { }
        }

        private void OnGUI()
        {
            try
            {
                AUGLMenuGUI.Draw();

                if (AUGLMenuGUI.ShowGlitchBadgeHUD)
                {
                    AUGLMenuGUI.DrawGlitchBadgeHUD();
                }

                if (MeetingVoteRevealerPatch.Enabled)
                {
                    AUGLMenuGUI.DrawLiveMeetingVotesHUD();
                }

                if (AUGLMenuGUI.ShowFpsPingHUD)
                {
                    AUGLMenuGUI.DrawFpsPingHUD();
                }

                AUGLMenuGUI.DrawToast();
            }
            catch (Exception ex)
            {
                if (ex.GetType().Name == "ExitGUIException") throw;
                AUGLModPlugin.Log?.LogWarning($"Draw error: {ex.Message}");
            }
        }
    }

    // ================= Static GUI Class =================
    public static class AUGLMenuGUI
    {
        private static bool _open = true;
        public static bool IsOpen => _open;
        private static int _tab;
        private static Vector2 _scroll;
        private static List<GlitchedCodeResponse> _codes = new List<GlitchedCodeResponse>();
        private static GlitchedStatsResponse _apiStats = new GlitchedStatsResponse();

        private static string _lastCachedQuery = null;
        private static int _lastCachedCodesCount = -1;
        private static DateTime _lastCacheTime = DateTime.MinValue;

        private static List<GlitchedCodeResponse> _naGlitched = new List<GlitchedCodeResponse>();
        private static List<GlitchedCodeResponse> _naActive = new List<GlitchedCodeResponse>();
        private static List<GlitchedCodeResponse> _euGlitched = new List<GlitchedCodeResponse>();
        private static List<GlitchedCodeResponse> _euActive = new List<GlitchedCodeResponse>();
        private static List<GlitchedCodeResponse> _asGlitched = new List<GlitchedCodeResponse>();
        private static List<GlitchedCodeResponse> _asActive = new List<GlitchedCodeResponse>();
        private static string _status = "Loading...";
        private static string _killCdInput = "0";
        private static string _angelInput = "30";
        private static string _angelCdInput = "0";
        private static string _levelInput = "999";
        private static string _searchQuery = "";
        private static string _focusedField = null;
        private static string _whitelistInput = "";
        private static string _blacklistInput = "";
        private static bool _spoofToggle;
        private static bool _regionToggle = true;

        public static bool ShowGlitchBadgeHUD = true;
        public static bool ShowFpsPingHUD = true;
        public static bool AutoRefreshEnabled = true;
        public static bool AutoCopyOnJoin = true;
        public static float AutoRefreshInterval = 45f;
        public static KeyCode ToggleKey = KeyCode.F7;
        public static float TargetZoom = 3.0f;

        private static string _toastMsg = null;
        private static float _toastExpires = 0f;

        private static int _eggClicks;
        private static string[] _eggTexts = { "sus", "hi", "meow", "prrr", "S- Senpai", "please dont" };
        private static Texture2D _eggTex;

        // Valid, clean Base64-encoded PNG
        private static readonly string _eggB64 = 
            "iVBORw0KGgoAAAANSUhEUgAAAEAAAABACAYAAACqaXHeAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZ" +
            "cwAADsMAAA7DAcdvqGQAAAIcSURBVHhe7Zs9buMwDIUX6B3aO2R/mQkK36NcoqfIbdw+i1yn8x7bVwixB5BtkRR/5P" +
            "lDgmzZlh6Jop6o6XQ6nU6n0+l0Op1f1816e3o8u/bT7W798Xz258+e7q/Xm1s9vK5b9r3v29/VqfflU/0c79qTf/9u" +
            "d7/5155+jX3W/Z+4/Rvf8fOvd3v72d+d/f6b+7uT/67e2o/c3lq/f+c9b/zZ6lWf+uF3/qrv/tY/676q997f7t7u3q" +
            "P9vN868l13/b/eXfd5b8/6r1e7e/593u3y/b3f/ff2411Pft+3H+/25Puu/79/fNvv/r1e7W/v9vVuj7/r3f8O3/21" +
            "7r/d/X/f59Veeve7/n/v/uvdt/bce/v/d9z/z747e9bdu++t/dme879+e/zZnvO/fs79ffec//Xb48/2nP/1c+7vu+" +
            "f8r98ef7bn/K+fc3/fPee/v3vO//o59/f9zef877e/d3vvPee/v9t//j/f3f33c87/e5/zP/9/f/fc/7v29+75v5/z" +
            "//7v//3cc/77u+f8r59zf98953/9nPv77jn//d1z/tfPub/vnvPf3z3nf/2c+/vuOf/93XP+18+5v++e89/fPed//Z" +
            "z7++45//3dc/7Xz7m/757z398953/9nPv77jn//d1z/tfPub/vnvPf3z3nf/2c+/vuOf/93XP+18+5v++e89/fPed/" +
            "/Zz7++45//3dc/7Xz7m/757z398953/9nPv77jn//d1z/tfPub/vnn6n0+l0Op1Op/Pr+g84yGvUvUflpAAAAABJRU" +
            "5ErkJggg==";

        private static Rect _windowRect = new Rect(150, 100, 860, 540);
        private static bool _initialized;
        private static Texture2D _winBgTex;
        private static Texture2D _boxBgTex;
        private static Texture2D _btnNormTex;
        private static Texture2D _btnHoverTex;
        private static Texture2D _textFieldBgTex;
        private static Texture2D _activeTextFieldBgTex;
        private static Texture2D _pillNormTex;
        private static Texture2D _pillHoverTex;
        private static Texture2D _glitchedPillNormTex;
        private static Texture2D _glitchedPillHoverTex;
        private static Texture2D _regionCardBgTex;
        private static GUIStyle _windowStyle;
        private static GUIStyle _buttonStyle;
        private static GUIStyle _labelStyle;
        private static GUIStyle _boldLabelStyle;
        private static GUIStyle _glitchedLabelStyle;
        private static GUIStyle _boxStyle;
        private static GUIStyle _textFieldStyle;
        private static GUIStyle _activeTextFieldStyle;
        private static GUIStyle _linkButtonStyle;
        private static GUIStyle _titleStyle;
        private static GUIStyle _paragraphStyle;
        private static GUIStyle _activePillStyle;
        private static GUIStyle _glitchedPillStyle;
        private static GUIStyle _regionCardBoxStyle;
        private static GUIStyle _headerTitleStyle;
        private static GUIStyle _statsBarStyle;

        private static void InitStyles()
        {
            if (_initialized && _winBgTex != null && _btnNormTex != null && _boxBgTex != null && _activeTextFieldBgTex != null && _pillNormTex != null)
            {
                return;
            }

            if (Screen.width > 0 && Screen.height > 0)
            {
                _windowRect = new Rect((Screen.width - 860) / 2, (Screen.height - 540) / 2, 860, 540);
            }

            _winBgTex = MakeTex(new Color(0.07f, 0.07f, 0.10f, 0.97f));
            _boxBgTex = MakeTex(new Color(0.13f, 0.13f, 0.17f, 0.85f));
            _btnNormTex = MakeTex(new Color(0.15f, 0.15f, 0.20f, 1f));
            _btnHoverTex = MakeTex(new Color(0.25f, 0.25f, 0.35f, 1f));
            _textFieldBgTex = MakeTex(new Color(0.05f, 0.05f, 0.07f, 1f));
            _activeTextFieldBgTex = MakeTex(new Color(0.12f, 0.22f, 0.40f, 1f));
            _pillNormTex = MakeTex(new Color(0.16f, 0.16f, 0.20f, 1f));
            _pillHoverTex = MakeTex(new Color(0.24f, 0.32f, 0.45f, 1f));
            _glitchedPillNormTex = MakeTex(new Color(0.08f, 0.22f, 0.12f, 1f));
            _glitchedPillHoverTex = MakeTex(new Color(0.12f, 0.38f, 0.20f, 1f));
            _regionCardBgTex = MakeTex(new Color(0.09f, 0.09f, 0.12f, 0.95f));

            _winBgTex.hideFlags = HideFlags.DontSave;
            _boxBgTex.hideFlags = HideFlags.DontSave;
            _btnNormTex.hideFlags = HideFlags.DontSave;
            _btnHoverTex.hideFlags = HideFlags.DontSave;
            _textFieldBgTex.hideFlags = HideFlags.DontSave;
            _activeTextFieldBgTex.hideFlags = HideFlags.DontSave;
            _pillNormTex.hideFlags = HideFlags.DontSave;
            _pillHoverTex.hideFlags = HideFlags.DontSave;
            _glitchedPillNormTex.hideFlags = HideFlags.DontSave;
            _glitchedPillHoverTex.hideFlags = HideFlags.DontSave;
            _regionCardBgTex.hideFlags = HideFlags.DontSave;

            _windowStyle = new GUIStyle(GUI.skin.window) { fontSize = 14, fontStyle = FontStyle.Bold };
            _windowStyle.normal.background = _winBgTex;
            _windowStyle.normal.textColor = Color.cyan;
            _windowStyle.onNormal.background = _winBgTex;
            _windowStyle.focused.background = _winBgTex;
            _windowStyle.active.background = _winBgTex;

            _buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 12 };
            _buttonStyle.normal.background = _btnNormTex;
            _buttonStyle.normal.textColor = Color.white;
            _buttonStyle.hover.background = _btnHoverTex;
            _buttonStyle.hover.textColor = Color.green;
            _buttonStyle.active.background = _btnHoverTex;
            _buttonStyle.active.textColor = Color.yellow;

            _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            _labelStyle.normal.textColor = Color.white;

            _boldLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
            _boldLabelStyle.normal.textColor = Color.white;

            _glitchedLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold };
            _glitchedLabelStyle.normal.textColor = new Color(0f, 1f, 0.4f);

            _boxStyle = new GUIStyle(GUI.skin.box);
            _boxStyle.normal.background = _boxBgTex;

            _textFieldStyle = new GUIStyle(GUI.skin.button) { fontSize = 12, alignment = TextAnchor.MiddleLeft };
            _textFieldStyle.normal.background = _textFieldBgTex;
            _textFieldStyle.normal.textColor = Color.white;

            _activeTextFieldStyle = new GUIStyle(_textFieldStyle);
            _activeTextFieldStyle.normal.background = _activeTextFieldBgTex;
            _activeTextFieldStyle.normal.textColor = Color.yellow;

            _linkButtonStyle = new GUIStyle(GUI.skin.button) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
            _linkButtonStyle.normal.background = _btnNormTex;
            _linkButtonStyle.normal.textColor = new Color(0.4f, 0.75f, 1f);
            _linkButtonStyle.hover.background = _btnHoverTex;
            _linkButtonStyle.hover.textColor = Color.cyan;
            _linkButtonStyle.active.background = _btnHoverTex;
            _linkButtonStyle.active.textColor = Color.yellow;

            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
            _titleStyle.normal.textColor = new Color(0.3f, 0.85f, 1f);

            _paragraphStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            _paragraphStyle.normal.textColor = new Color(0.9f, 0.9f, 0.95f);

            _activePillStyle = new GUIStyle(GUI.skin.button) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _activePillStyle.normal.background = _pillNormTex;
            _activePillStyle.normal.textColor = new Color(0.92f, 0.92f, 0.95f);
            _activePillStyle.hover.background = _pillHoverTex;
            _activePillStyle.hover.textColor = Color.cyan;

            _glitchedPillStyle = new GUIStyle(GUI.skin.button) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _glitchedPillStyle.normal.background = _glitchedPillNormTex;
            _glitchedPillStyle.normal.textColor = new Color(0.1f, 1f, 0.4f);
            _glitchedPillStyle.hover.background = _glitchedPillHoverTex;
            _glitchedPillStyle.hover.textColor = Color.yellow;

            _regionCardBoxStyle = new GUIStyle(GUI.skin.box);
            _regionCardBoxStyle.normal.background = _regionCardBgTex;

            _headerTitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _headerTitleStyle.normal.textColor = Color.white;

            _statsBarStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
            _statsBarStyle.normal.textColor = new Color(0.85f, 0.85f, 0.90f);

            _initialized = true;
        }

        public static void ToggleOpen()
        {
            _open = !_open;
        }

        public static void TriggerToast(string msg)
        {
            _toastMsg = msg;
            _toastExpires = Time.time + 3.0f;
        }

        public static void DrawToast()
        {
            if (string.IsNullOrEmpty(_toastMsg) || Time.time > _toastExpires) return;
            InitStyles();
            Rect rect = new Rect((Screen.width - 320) / 2, 20, 320, 36);
            GUI.Box(rect, GUIContent.none, _boxStyle);
            GUI.Label(new Rect(rect.x + 8, rect.y + 8, rect.width - 16, 20), $"🔔 {_toastMsg}", _glitchedLabelStyle);
        }

        public static async Task FetchCodes()
        {
            try
            {
                _status = "Fetching...";
                var (codes, stats) = await AUGLApiClient.FetchDataAsync();
                _codes = codes ?? new List<GlitchedCodeResponse>();
                _apiStats = stats ?? new GlitchedStatsResponse();

                if (_apiStats.Total_Codes == 0 && _codes.Count > 0)
                {
                    _apiStats.Total_Codes = _codes.Count;
                    _apiStats.Glitched = _codes.Count(c => c.Glitched);
                }

                _status = _codes.Count > 0 ? $"Active ({_codes.Count} codes)" : "No active codes";
            }
            catch
            {
                _status = "Connection failed";
            }
        }

        public static List<GlitchedCodeResponse> GetGlitchedCodesList()
        {
            var list = new List<GlitchedCodeResponse>();
            foreach (var c in _codes)
            {
                if (c.Glitched) list.Add(c);
            }
            return list;
        }

        private static Type _lobbyClientTypeCache;
        private static PropertyInfo _lobbyInstPropCache;
        private static PropertyInfo _lobbyGameIdPropCache;
        private static Type _gameCodeTypeCache;
        private static MethodInfo _intToGameNameMethodCache;
        private static bool _lobbyCodeLookupDone;

        public static string GetCurrentLobbyCode()
        {
            try
            {
                if (!_lobbyCodeLookupDone)
                {
                    _lobbyClientTypeCache = AccessTools.TypeByName("AmongUsClient");
                    _lobbyInstPropCache = _lobbyClientTypeCache?.GetProperty("Instance");
                    _lobbyGameIdPropCache = _lobbyClientTypeCache?.GetProperty("GameId") ?? _lobbyClientTypeCache?.GetProperty("gameId");
                    _gameCodeTypeCache = AccessTools.TypeByName("GameCode") ?? AccessTools.TypeByName("InnerNet.GameCode");
                    _intToGameNameMethodCache = AccessTools.Method(_gameCodeTypeCache, "IntToGameName", new Type[] { typeof(int) });
                    _lobbyCodeLookupDone = true;
                }

                var client = _lobbyInstPropCache?.GetValue(null);
                if (client == null) return null;

                int gameId = (int)(_lobbyGameIdPropCache?.GetValue(client) ?? 0);
                if (gameId == 0) return null;

                if (_intToGameNameMethodCache != null)
                {
                    return (string)_intToGameNameMethodCache.Invoke(null, new object[] { gameId });
                }

                return DecodeGameCode(gameId);
            }
            catch { return null; }
        }

        private static readonly char[] GameCodeV2Chars = "QWERTYUIOPASDFGHJKLZXCVBNM".ToCharArray();

        private static string DecodeGameCode(int gameId)
        {
            try
            {
                if (gameId == 0) return null;
                if (gameId < 0)
                {
                    int first = gameId & 0x3FF;
                    int second = (gameId >> 10) & 0xFFFFF;
                    char a = GameCodeV2Chars[first % 26];
                    char b = GameCodeV2Chars[(first / 26) % 26];
                    char c = GameCodeV2Chars[second % 26];
                    char d = GameCodeV2Chars[(second / 26) % 26];
                    char e = GameCodeV2Chars[(second / 676) % 26];
                    char f = GameCodeV2Chars[(second / 17576) % 26];
                    return new string(new char[] { a, b, c, d, e, f });
                }
                else
                {
                    byte[] bytes = BitConverter.GetBytes(gameId);
                    return System.Text.Encoding.UTF8.GetString(bytes);
                }
            }
            catch { return null; }
        }

        public static bool IsCodeGlitched(string fullCode)
        {
            if (string.IsNullOrEmpty(fullCode)) return false;
            fullCode = fullCode.Trim().ToUpper();

            foreach (var c in _codes)
            {
                if (string.IsNullOrEmpty(c.Code)) continue;
                string target = c.Code.Trim().ToUpper();
                if (c.Glitched)
                {
                    if (fullCode == target || fullCode.EndsWith(target) || target.EndsWith(fullCode))
                        return true;
                }
            }
            return false;
        }

        public static void DrawGlitchBadgeHUD()
        {
            try
            {
                InitStyles();
                string curCode = GetCurrentLobbyCode();
                if (string.IsNullOrEmpty(curCode)) return;

                bool isGlitched = IsCodeGlitched(curCode);

                Rect badgeRect = new Rect(Screen.width - 250, 10, 240, 32);
                GUI.Box(badgeRect, GUIContent.none, _boxStyle);

                if (isGlitched)
                {
                    GUI.Label(new Rect(badgeRect.x + 8, badgeRect.y + 6, badgeRect.width - 16, 20),
                        $"⚡ GLITCHED: {curCode}", _glitchedLabelStyle);
                }
                else
                {
                    GUI.Label(new Rect(badgeRect.x + 8, badgeRect.y + 6, badgeRect.width - 16, 20),
                        $"🛡️ LOBBY: {curCode} (NORMAL)", _labelStyle);
                }
            }
            catch { }
        }

        private static Type _clientTypeCache;
        private static PropertyInfo _instancePropCache;
        private static FieldInfo _pingFieldCache;
        private static PropertyInfo _pingPropCache;
        private static bool _pingLookupDone;

        public static void DrawFpsPingHUD()
        {
            try
            {
                InitStyles();
                int fps = (int)(1.0f / Mathf.Max(0.0001f, Time.smoothDeltaTime));
                int ping = 0;

                if (!_pingLookupDone)
                {
                    _clientTypeCache = AccessTools.TypeByName("AmongUsClient");
                    _instancePropCache = _clientTypeCache?.GetProperty("Instance");
                    _pingFieldCache = AccessTools.Field(_clientTypeCache, "Ping");
                    if (_pingFieldCache == null)
                        _pingPropCache = AccessTools.Property(_clientTypeCache, "Ping");
                    _pingLookupDone = true;
                }

                var inst = _instancePropCache?.GetValue(null);
                if (inst != null)
                {
                    if (_pingFieldCache != null) ping = (int)(_pingFieldCache.GetValue(inst) ?? 0);
                    else if (_pingPropCache != null) ping = (int)(_pingPropCache.GetValue(inst) ?? 0);
                }

                Rect fpsRect = new Rect(10, 10, 150, 24);
                GUI.Box(fpsRect, GUIContent.none, _boxStyle);
                GUI.Label(new Rect(fpsRect.x + 6, fpsRect.y + 2, fpsRect.width - 12, 20),
                    $"FPS: {fps} | Ping: {ping}ms", _labelStyle);
            }
            catch { }
        }

        public static void DrawLiveMeetingVotesHUD()
        {
            try
            {
                var meeting = MeetingHud.Instance;
                if (meeting == null || MeetingVoteRevealerPatch.LiveVotes.Count == 0) return;

                InitStyles();
                Rect hudRect = new Rect(10, 40, 240, 25 + (MeetingVoteRevealerPatch.LiveVotes.Count * 20));
                GUI.Box(hudRect, GUIContent.none, _boxStyle);
                GUI.Label(new Rect(hudRect.x + 8, hudRect.y + 4, hudRect.width - 16, 20), "🗳️ <b>Live Votes Tracker:</b>", _boldLabelStyle);

                int index = 0;
                var allPlayers = PlayerControl.AllPlayerControls;
                Dictionary<byte, string> nameMap = new Dictionary<byte, string>();
                if (allPlayers != null)
                {
                    for (int i = 0; i < allPlayers.Count; i++)
                    {
                        var p = allPlayers[i];
                        if (p != null && p.Data != null)
                        {
                            nameMap[p.Data.PlayerId] = p.Data.PlayerName;
                        }
                    }
                }

                string GetPlayerName(byte id)
                {
                    if (id == 254 || id == 255) return "Skipped Vote";
                    if (nameMap.TryGetValue(id, out string n)) return n;
                    return $"Player {id}";
                }

                foreach (var kvp in MeetingVoteRevealerPatch.LiveVotes)
                {
                    string voterName = GetPlayerName(kvp.Key);
                    string targetName = GetPlayerName(kvp.Value);
                    GUI.Label(new Rect(hudRect.x + 8, hudRect.y + 24 + (index * 20), hudRect.width - 16, 20),
                        $"<color=#00FFFF>{voterName}</color> ➔ <color=#FFFF00>{targetName}</color>", _labelStyle);
                    index++;
                }
            }
            catch { }
        }

        private static bool _isMedbayScanning = false;

        public static void TriggerMedBayScan()
        {
            try
            {
                var lp = PlayerControl.LocalPlayer;
                if (lp == null) return;

                _isMedbayScanning = !_isMedbayScanning;
                lp.SetScanner(_isMedbayScanning, 0);
                lp.RpcSetScanner(_isMedbayScanning);

                TriggerToast(_isMedbayScanning ? "MedBay Scan: ACTIVATED" : "MedBay Scan: CANCELLED");
            }
            catch { }
        }

        public static void AutoSolveCurrentTask()
        {
            try
            {
                var minigameType = AccessTools.TypeByName("Minigame");
                var instProp = minigameType?.GetProperty("Instance");
                var minigame = instProp?.GetValue(null);
                if (minigame != null)
                {
                    var myNormTaskProp = AccessTools.Property(minigame.GetType(), "MyNormTask");
                    var myNormTask = myNormTaskProp?.GetValue(minigame);
                    if (myNormTask != null)
                    {
                        var nextStep = AccessTools.Method(myNormTask.GetType(), "NextStep");
                        nextStep?.Invoke(myNormTask, null);
                    }
                    var closeMethod = AccessTools.Method(minigame.GetType(), "Close");
                    closeMethod?.Invoke(minigame, null);
                    TriggerToast("Task Solved!");
                }
            }
            catch { }
        }

        public static void Draw()
        {
            if (!_open) return;

            InitStyles();

            _windowRect = GUI.Window(0, _windowRect, (GUI.WindowFunction)DrawWindow, "AUGL Menu 04.09.18", _windowStyle);
        }

        private static void DrawWindow(int id)
        {
            GUI.DragWindow(new Rect(0, 0, 860, 25));

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("About", _buttonStyle, GUILayout.Width(70), GUILayout.Height(26))) SwitchTab(0);
            if (GUILayout.Button("Glitched Lobbies", _buttonStyle, GUILayout.Width(115), GUILayout.Height(26))) SwitchTab(1);
            if (GUILayout.Button("Unlockers", _buttonStyle, GUILayout.Width(75), GUILayout.Height(26))) SwitchTab(3);
            if (GUILayout.Button("Host & Modes", _buttonStyle, GUILayout.Width(90), GUILayout.Height(26))) SwitchTab(4);
            if (GUILayout.Button("Players", _buttonStyle, GUILayout.Width(70), GUILayout.Height(26))) SwitchTab(8);
            if (GUILayout.Button("Fun AddOns", _buttonStyle, GUILayout.Width(85), GUILayout.Height(26))) SwitchTab(5);
            if (GUILayout.Button("Troll", _buttonStyle, GUILayout.Width(50), GUILayout.Height(26))) SwitchTab(6);
            if (GUILayout.Button("Docs", _buttonStyle, GUILayout.Width(50), GUILayout.Height(26))) SwitchTab(7);
            GUILayout.EndHorizontal();
            GUILayout.Space(10);

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Width(830), GUILayout.Height(450));
            try
            {
                switch (_tab)
                {
                    case 0: DrawAbout(); break;
                    case 1: DrawGlitchedLobbiesTab(); break;
                    case 3: DrawUnlocker(); break;
                    case 4: DrawHostAndModes(); break;
                    case 5: DrawFunAddOns(); break;
                    case 6: DrawTrollTab(); break;
                    case 7: DrawDocs(); break;
                    case 8: DrawPlayersTab(); break;
                }
            }
            catch (Exception ex)
            {
                if (ex.GetType().Name == "ExitGUIException") throw;
                AUGLModPlugin.Log?.LogWarning($"Tab drawing exception: {ex.Message}");
            }
            GUILayout.EndScrollView();
        }

        private static void SwitchTab(int tabIndex)
        {
            _tab = tabIndex;
            _scroll = Vector2.zero;
            _focusedField = null;
        }

        private static string DrawCustomInputField(string controlId, string text, float width)
        {
            bool isFocused = (_focusedField == controlId);
            string displayText = text ?? "";

            if (isFocused && (Time.time % 1f > 0.5f))
            {
                displayText += "|";
            }

            GUIStyle style = isFocused ? _activeTextFieldStyle : _textFieldStyle;
            Rect rect = GUILayoutUtility.GetRect(width, 24f, GUILayout.ExpandWidth(false));

            if (GUI.Button(rect, displayText, style))
            {
                _focusedField = controlId;
            }

            if (isFocused && Event.current != null && Event.current.type == EventType.KeyDown)
            {
                KeyCode code = Event.current.keyCode;
                char ch = Event.current.character;

                if (code == KeyCode.Backspace && text.Length > 0)
                {
                    text = text.Substring(0, text.Length - 1);
                    Event.current.Use();
                }
                else if (code == KeyCode.Return || code == KeyCode.KeypadEnter || code == KeyCode.Tab || code == KeyCode.Escape)
                {
                    _focusedField = null;
                    Event.current.Use();
                }
                else if (char.IsLetterOrDigit(ch) || ch == '.' || ch == ',' || ch == '+' || ch == '-')
                {
                    if (ch == ',') ch = '.';
                    text += ch;
                    Event.current.Use();
                }
            }

            return text;
        }

        private static bool TryParseHugeFloat(string input, out float val)
        {
            input = (input ?? "").Trim().ToLower();
            if (input == "inf" || input == "infinity" || input == "max")
            {
                val = float.MaxValue;
                return true;
            }
            if (float.TryParse(input, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float result))
            {
                val = result;
                return true;
            }
            if (double.TryParse(input, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double dResult))
            {
                val = (float)Math.Min((double)float.MaxValue, dResult);
                return true;
            }
            val = 0f;
            return false;
        }

        private static bool SafeToggle(bool value, string label)
        {
            try
            {
                Rect r = GUILayoutUtility.GetRect(260f, 22f, GUILayout.ExpandWidth(false));
                return GUI.Toggle(r, value, label);
            }
            catch
            {
                try { return GUILayout.Toggle(value, label); }
                catch { return value; }
            }
        }

        private static void DrawAbout()
        {
            GUILayout.Label("AUGL Menu", _boldLabelStyle);
            GUILayout.Label("Version: 04.09.18", _labelStyle);
            GUILayout.Label("Version Build Date: 04/09/2026", _labelStyle);
            GUILayout.Label("Creators: sparxist (original), auratech0 (menu)", _labelStyle);
            GUILayout.Space(8);

            GUILayout.Label("Links & Community:", _boldLabelStyle);
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("Discord (AUGL):", _labelStyle, GUILayout.Width(140));
            if (GUILayout.Button("discord.gg/HeNGYArCkY", _linkButtonStyle, GUILayout.Width(220), GUILayout.Height(22)))
            {
                Application.OpenURL("https://discord.gg/HeNGYArCkY");
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(3);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Discord (Tech Lounge):", _labelStyle, GUILayout.Width(140));
            if (GUILayout.Button("discord.gg/rj3fWwrc8Q", _linkButtonStyle, GUILayout.Width(220), GUILayout.Height(22)))
            {
                Application.OpenURL("https://discord.gg/rj3fWwrc8Q");
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(3);
            GUILayout.BeginHorizontal();
            GUILayout.Label("GitHub Repository:", _labelStyle, GUILayout.Width(140));
            if (GUILayout.Button("github.com/auratech0/au-glitched-lobbies", _linkButtonStyle, GUILayout.Width(290), GUILayout.Height(22)))
            {
                Application.OpenURL("https://github.com/auratech0/au-glitched-lobbies");
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(3);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Official Website:", _labelStyle, GUILayout.Width(140));
            if (GUILayout.Button("augl.net", _linkButtonStyle, GUILayout.Width(140), GUILayout.Height(22)))
            {
                Application.OpenURL("https://augl.net");
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            GUILayout.Label("Open-Source Contributions & Credits:", _boldLabelStyle);
            GUILayout.Label("• <b>TheOtherRoles & TownOfUs</b>: Meeting vote tracker, Dead player role reveal & Cosmetic bypass", _labelStyle);
            GUILayout.Label("• <b>Reactor & BepInEx Unity IL2CPP</b>: Interop hooks, Domain injection & Mod architecture", _labelStyle);

            GUILayout.Space(10);
            GUILayout.Label($"Status: {_status}", _labelStyle);
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh Codes Now", _buttonStyle, GUILayout.Width(150), GUILayout.Height(24)))
            {
                _ = FetchCodes();
            }

            // Easter Egg Button inside normal flow so it is never clipped
            string eggText = _eggClicks >= _eggTexts.Length ? "🐱" : _eggTexts[_eggClicks];
            if (GUILayout.Button(eggText, _buttonStyle, GUILayout.Width(90), GUILayout.Height(24)))
            {
                _eggClicks = Mathf.Min(_eggClicks + 1, _eggTexts.Length);
                if (_eggClicks == _eggTexts.Length)
                {
                    LoadEgg();
                }
            }
            GUILayout.EndHorizontal();

            // Render Easter Egg Texture inside tab layout
            if (_eggClicks >= _eggTexts.Length && _eggTex != null)
            {
                GUILayout.Space(6);
                Rect texRect = GUILayoutUtility.GetRect(64, 64, GUILayout.ExpandWidth(false));
                GUI.DrawTexture(texRect, _eggTex, ScaleMode.ScaleToFit);
            }
        }

        private static void LoadEgg()
        {
            try
            {
                if (_eggTex == null)
                {
                    byte[] pngBytes = Convert.FromBase64String(_eggB64.Trim());
                    _eggTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    _eggTex.LoadImage(pngBytes);
                    _eggTex.hideFlags = HideFlags.DontSave;
                }
            }
            catch (Exception ex)
            {
                AUGLModPlugin.Log?.LogWarning($"Egg load failed: {ex.Message}");
            }
        }

        private static GUIStyle _boldCenterLabelStyle;
        private static GUIStyle _centerLabelStyle;

        private static void RebuildRegionalCache()
        {
            string query = (_searchQuery ?? "").Trim().ToUpper();
            if (query == _lastCachedQuery && _codes.Count == _lastCachedCodesCount && (DateTime.UtcNow - _lastCacheTime).TotalSeconds < 2.0)
            {
                return;
            }

            _lastCachedQuery = query;
            _lastCachedCodesCount = _codes.Count;
            _lastCacheTime = DateTime.UtcNow;

            _naGlitched.Clear(); _naActive.Clear();
            _euGlitched.Clear(); _euActive.Clear();
            _asGlitched.Clear(); _asActive.Clear();

            int maxActivePerRegion = 84;

            for (int i = 0; i < _codes.Count; i++)
            {
                var c = _codes[i];
                if (c == null || c.Dormant || string.IsNullOrEmpty(c.Code)) continue;
                if (!string.IsNullOrEmpty(query) && !c.Code.ToUpper().Contains(query)) continue;

                string reg = (c.Region ?? "na").Trim().ToLower();
                if (reg == "na")
                {
                    if (c.Glitched) _naGlitched.Add(c);
                    else if (_naActive.Count < maxActivePerRegion) _naActive.Add(c);
                }
                else if (reg == "eu")
                {
                    if (c.Glitched) _euGlitched.Add(c);
                    else if (_euActive.Count < maxActivePerRegion) _euActive.Add(c);
                }
                else if (reg == "as")
                {
                    if (c.Glitched) _asGlitched.Add(c);
                    else if (_asActive.Count < maxActivePerRegion) _asActive.Add(c);
                }
            }
        }

        private static void DrawGlitchedLobbiesTab()
        {
            if (_boldCenterLabelStyle == null)
            {
                _boldCenterLabelStyle = new GUIStyle(_boldLabelStyle) { alignment = TextAnchor.MiddleCenter };
                _centerLabelStyle = new GUIStyle(_labelStyle) { alignment = TextAnchor.MiddleCenter };
            }

            RebuildRegionalCache();

            // Title Header (GLITCHED LOBBIES)
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("GLITCHED LOBBIES", _headerTitleStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(2);

            // Stats Subheader Bar
            int glitchedCount = _apiStats.Glitched > 0 ? _apiStats.Glitched : (_naGlitched.Count + _euGlitched.Count + _asGlitched.Count);
            int codesPerMin = _apiStats.CodesPerMin;
            int totalCodes = _apiStats.Total_Codes > 0 ? _apiStats.Total_Codes : _codes.Count;

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label($"💥 <b>Glitched Codes:</b> <color=#00FF66>{glitchedCount}</color>    🔍 <b>Codes found in last minute:</b> {codesPerMin}    👁 <b>Total active codes:</b> {totalCodes}", _statsBarStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // Search Bar & Controls
            GUILayout.BeginHorizontal();
            GUILayout.Label("🔍 Search Code:", _labelStyle, GUILayout.Width(100));
            string oldQuery = _searchQuery;
            _searchQuery = DrawCustomInputField("codeSearch", _searchQuery, 160f);
            if (oldQuery != _searchQuery) _lastCachedQuery = null;

            if (GUILayout.Button("Clear", _buttonStyle, GUILayout.Width(50), GUILayout.Height(24)))
            {
                _searchQuery = "";
                _lastCachedQuery = null;
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("🔄 Refresh", _buttonStyle, GUILayout.Width(80), GUILayout.Height(24)))
            {
                _lastCachedQuery = null;
                _ = FetchCodes();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(12);

            // 3 Columns: North America, Europe, Asia
            GUILayout.BeginHorizontal();
            DrawRegionCard("North America", _naGlitched, _naActive);
            GUILayout.Space(10);
            DrawRegionCard("Europe", _euGlitched, _euActive);
            GUILayout.Space(10);
            DrawRegionCard("Asia", _asGlitched, _asActive);
            GUILayout.EndHorizontal();
        }

        private static void DrawRegionCard(string regionTitle, List<GlitchedCodeResponse> glitchedCodes, List<GlitchedCodeResponse> activeCodes)
        {
            int glitchedRows = (glitchedCodes != null && glitchedCodes.Count > 0) ? (int)Math.Ceiling(glitchedCodes.Count / 3.0) : 1;
            int activeRows = (activeCodes != null && activeCodes.Count > 0) ? (int)Math.Ceiling(activeCodes.Count / 3.0) : 1;

            float cardHeight = 30 + 22 + (glitchedRows * 27) + 24 + (activeRows * 27) + 12;
            Rect cardRect = GUILayoutUtility.GetRect(255f, cardHeight, GUILayout.ExpandWidth(false));

            GUI.Box(cardRect, GUIContent.none, _regionCardBoxStyle);

            float curY = cardRect.y + 6;

            // Region Name Title
            GUI.Label(new Rect(cardRect.x, curY, cardRect.width, 22), $"<b><size=14>{regionTitle}</size></b>", _boldCenterLabelStyle);
            curY += 26;

            // Glitched Header
            GUI.Label(new Rect(cardRect.x, curY, cardRect.width, 20), "<b><color=#00FF66>Glitched</color></b>", _boldCenterLabelStyle);
            curY += 22;

            if (glitchedCodes == null || glitchedCodes.Count == 0)
            {
                GUI.Label(new Rect(cardRect.x, curY, cardRect.width, 20), "<color=#777777><size=11>Nothing found!</size></color>", _centerLabelStyle);
                curY += 24;
            }
            else
            {
                curY = DrawDirectPillGrid(cardRect.x + 12, curY, glitchedCodes, true);
            }

            curY += 6;

            // Active Header
            GUI.Label(new Rect(cardRect.x, curY, cardRect.width, 20), "<b><color=#CCCCCC>Active</color></b>", _boldCenterLabelStyle);
            curY += 22;

            if (activeCodes == null || activeCodes.Count == 0)
            {
                GUI.Label(new Rect(cardRect.x, curY, cardRect.width, 20), "<color=#777777><size=11>Nothing found!</size></color>", _centerLabelStyle);
            }
            else
            {
                DrawDirectPillGrid(cardRect.x + 12, curY, activeCodes, false);
            }
        }

        private static float DrawDirectPillGrid(float startX, float startY, List<GlitchedCodeResponse> codeList, bool isGlitched)
        {
            GUIStyle pillStyle = isGlitched ? _glitchedPillStyle : _activePillStyle;
            float curX = startX;
            float curY = startY;
            int countInRow = 0;

            for (int i = 0; i < codeList.Count; i++)
            {
                var c = codeList[i];
                Rect pillRect = new Rect(curX, curY, 74, 24);

                if (GUI.Button(pillRect, c.Code, pillStyle))
                {
                    GUIUtility.systemCopyBuffer = c.Code;
                    TriggerToast($"Copied {c.Code} ({(c.Region ?? "NA").ToUpper()})");
                }

                countInRow++;
                if (countInRow >= 3)
                {
                    countInRow = 0;
                    curX = startX;
                    curY += 27;
                }
                else
                {
                    curX += 78;
                }
            }

            if (countInRow > 0)
            {
                curY += 27;
            }

            return curY;
        }

        private static void DrawUnlocker()
        {
            GUILayout.Label("Unlockers & Client Spoofing", _boldLabelStyle);
            GUILayout.Space(6);
            
            GUILayout.Label("PlayStation Spoofing (Guaranteed):", _labelStyle);
            _spoofToggle = SafeToggle(_spoofToggle, "Enable PlayStation Spoof (ID 10)");
            if (_spoofToggle != PlatformSpoofManager.IsActive) { if (_spoofToggle) PlatformSpoofManager.Enable(); else PlatformSpoofManager.Disable(); }
            if (GUILayout.Button("Revert Spoof", _buttonStyle, GUILayout.Width(110), GUILayout.Height(24))) { _spoofToggle = false; PlatformSpoofManager.Disable(); }

            GUILayout.Space(10);
            GUILayout.Label("Anti-Leave Penalty & Cosmetics:", _labelStyle);
            AntiLeavePenaltyPatch.Enabled = SafeToggle(AntiLeavePenaltyPatch.Enabled, "Bypass Matchmaking Disconnect Penalty (Anti-Ban)");
            CosmeticsUnlocker.Enabled = SafeToggle(CosmeticsUnlocker.Enabled, "Unlock All Items (Hats/Skins/Visors/Pets)");

            GUILayout.Space(10);
            GUILayout.Label("Level Spoofer:", _labelStyle);
            LevelSpooferPatch.Enabled = SafeToggle(LevelSpooferPatch.Enabled, "Enable Custom Displayed Level");
            if (LevelSpooferPatch.Enabled)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Level Value:", _labelStyle, GUILayout.Width(90));
                _levelInput = DrawCustomInputField("lvlInput", _levelInput, 100f);
                if (uint.TryParse(_levelInput, out uint lvl)) LevelSpooferPatch.SpoofedLevel = lvl;
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(12);
            GUILayout.Label("Kill Cooldown (seconds):", _labelStyle);
            _killCdInput = DrawCustomInputField("killCd", _killCdInput, 180f);

            GUILayout.Space(6);
            if (GUILayout.Button("Apply Kill CD", _buttonStyle, GUILayout.Width(110), GUILayout.Height(24)))
            {
                if (TryParseHugeFloat(_killCdInput, out float cd))
                {
                    UnlockerManager.ApplyKillCd(cd);
                }
            }

            GUILayout.Space(12);
            GUILayout.Label("Angel Protection Duration (seconds):", _labelStyle);
            _angelInput = DrawCustomInputField("angelDur", _angelInput, 180f);

            GUILayout.Space(6);
            if (GUILayout.Button("Apply Angel Duration", _buttonStyle, GUILayout.Width(150), GUILayout.Height(24)))
            {
                if (TryParseHugeFloat(_angelInput, out float d))
                {
                    UnlockerManager.ApplyAngel(d);
                }
            }

            GUILayout.Space(12);
            GUILayout.Label("Angel Protection Cooldown (seconds):", _labelStyle);
            _angelCdInput = DrawCustomInputField("angelCd", _angelCdInput, 180f);

            GUILayout.Space(6);
            if (GUILayout.Button("Apply Angel Cooldown", _buttonStyle, GUILayout.Width(160), GUILayout.Height(24)))
            {
                if (TryParseHugeFloat(_angelCdInput, out float cd))
                {
                    UnlockerManager.ApplyAngelCd(cd);
                }
            }

            GUILayout.Space(14);
            if (GUILayout.Button("Reset Option Modifiers", _buttonStyle, GUILayout.Width(160), GUILayout.Height(24)))
            {
                UnlockerManager.Reset();
                _killCdInput = "0";
                _angelInput = "30";
                _angelCdInput = "0";
            }
        }

        private static void DrawHostAndModes()
        {
            GUILayout.BeginHorizontal();

            // Left Column: Original Host QoL, Anti-Cheat, & Presets (480px)
            GUILayout.BeginVertical(GUILayout.Width(480));

            GUILayout.Label("Host Quality-of-Life Tools", _boldLabelStyle);
            GUILayout.Space(4);
            GUILayout.Label($"<b>Active Mode Status:</b> <color=#00FF66>{GameModePresetManager.ActiveModeName}</color>", _labelStyle);
            GUILayout.Space(6);

            if (GUILayout.Button("⚡ Instant Start Game (0s Countdown)", _buttonStyle, GUILayout.Width(280), GUILayout.Height(30)))
            {
                HostQoLManager.InstantStart();
            }

            GUILayout.Space(6);
            HostQoLManager.FastStartEnabled = SafeToggle(HostQoLManager.FastStartEnabled, "Force 1s Fast Countdown Loop");

            GUILayout.Space(12);
            GUILayout.Label($"Player Speed Multiplier: {HostQoLManager.PlayerSpeed:0.0}x", _labelStyle);
            HostQoLManager.PlayerSpeed = GUILayout.HorizontalSlider(HostQoLManager.PlayerSpeed, 0.5f, 5.0f, GUILayout.Width(280));

            GUILayout.Space(6);
            GUILayout.Label($"Crewmate Light Vision: {HostQoLManager.CrewLight:0.0}x", _labelStyle);
            HostQoLManager.CrewLight = GUILayout.HorizontalSlider(HostQoLManager.CrewLight, 0.2f, 5.0f, GUILayout.Width(280));

            GUILayout.Space(6);
            GUILayout.Label($"Impostor Light Vision: {HostQoLManager.ImpLight:0.0}x", _labelStyle);
            HostQoLManager.ImpLight = GUILayout.HorizontalSlider(HostQoLManager.ImpLight, 0.2f, 5.0f, GUILayout.Width(280));

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply Host Options", _buttonStyle, GUILayout.Width(140), GUILayout.Height(26)))
            {
                HostQoLManager.ApplyHostSettings();
            }

            bool canClickPlus25 = false;
            try
            {
                var client = AmongUsClient.Instance;
                if (client != null && client.AmHost)
                {
                    var isPubField = AccessTools.Field(client.GetType(), "_IsGamePublic_k__BackingField");
                    bool isPub = (bool)(isPubField?.GetValue(client) ?? false);
                    canClickPlus25 = isPub;
                }
            }
            catch { }

            GUI.enabled = canClickPlus25;
            string plus25Text = canClickPlus25 ? "👥 +25 Lobby Cap (Vanilla Joinable)" : "👥 +25 Lobby (Host Public Lobby Only)";
            if (GUILayout.Button(plus25Text, _buttonStyle, GUILayout.Width(255), GUILayout.Height(26)))
            {
                HostQoLManager.SetMaxPlayers25();
                TriggerToast("Max Players set to 25!");
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Space(14);
            GUILayout.Label("Anti-Cheat & Player Access Controls", _boldLabelStyle);
            AntiCheatManager.Enabled = SafeToggle(AntiCheatManager.Enabled, "Enable Host Anti-Cheat (Blocks illegal kills/bans blacklisted)");

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Whitelist Friend:", _labelStyle, GUILayout.Width(105));
            _whitelistInput = DrawCustomInputField("wlInput", _whitelistInput, 135f);
            if (GUILayout.Button("Add Friend", _buttonStyle, GUILayout.Width(85), GUILayout.Height(24)))
            {
                if (!string.IsNullOrWhiteSpace(_whitelistInput))
                {
                    string friend = _whitelistInput.Trim();
                    if (!AntiCheatManager.Whitelist.Contains(friend))
                    {
                        AntiCheatManager.Whitelist.Add(friend);
                        ConfigManager.Current.Whitelist = AntiCheatManager.Whitelist;
                        ConfigManager.SaveConfig();
                        TriggerToast($"Whitelisted: {friend}");
                    }
                    _whitelistInput = "";
                }
            }
            if (GUILayout.Button("Clear All", _buttonStyle, GUILayout.Width(65), GUILayout.Height(24)))
            {
                AntiCheatManager.Whitelist.Clear();
                ConfigManager.Current.Whitelist = AntiCheatManager.Whitelist;
                ConfigManager.SaveConfig();
                TriggerToast("Whitelist cleared.");
            }
            GUILayout.EndHorizontal();
            if (AntiCheatManager.Whitelist.Count > 0)
            {
                GUILayout.Label($"Whitelisted ({AntiCheatManager.Whitelist.Count}): <color=#00FF66>" + string.Join(", ", AntiCheatManager.Whitelist) + "</color>", _labelStyle);
            }

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Blacklist Player:", _labelStyle, GUILayout.Width(105));
            _blacklistInput = DrawCustomInputField("blInput", _blacklistInput, 135f);
            if (GUILayout.Button("Ban/Block", _buttonStyle, GUILayout.Width(85), GUILayout.Height(24)))
            {
                if (!string.IsNullOrWhiteSpace(_blacklistInput))
                {
                    string target = _blacklistInput.Trim();
                    if (!AntiCheatManager.Blacklist.Contains(target))
                    {
                        AntiCheatManager.Blacklist.Add(target);
                        ConfigManager.Current.Blacklist = AntiCheatManager.Blacklist;
                        ConfigManager.SaveConfig();
                        TriggerToast($"Blacklisted: {target}");
                    }
                    _blacklistInput = "";
                }
            }
            if (GUILayout.Button("Clear All", _buttonStyle, GUILayout.Width(65), GUILayout.Height(24)))
            {
                AntiCheatManager.Blacklist.Clear();
                ConfigManager.Current.Blacklist = AntiCheatManager.Blacklist;
                ConfigManager.SaveConfig();
                TriggerToast("Blacklist cleared.");
            }
            GUILayout.EndHorizontal();
            if (AntiCheatManager.Blacklist.Count > 0)
            {
                GUILayout.Label($"Blacklisted ({AntiCheatManager.Blacklist.Count}): <color=#FF4444>" + string.Join(", ", AntiCheatManager.Blacklist) + "</color>", _labelStyle);
            }

            GUILayout.Space(16);
            GUILayout.Label("Game Mode Presets", _boldLabelStyle);
            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Normal / Roles Galore", _buttonStyle, GUILayout.Width(150), GUILayout.Height(28))) GameModePresetManager.ApplyPreset("Normal");
            if (GUILayout.Button("SNS (Shift & Seek)", _buttonStyle, GUILayout.Width(150), GUILayout.Height(28))) GameModePresetManager.ApplyPreset("SNS");
            if (GUILayout.Button("Shields (Angels vs Killer)", _buttonStyle, GUILayout.Width(165), GUILayout.Height(28))) GameModePresetManager.ApplyPreset("Shields");
            GUILayout.EndHorizontal();

            GUILayout.EndVertical(); // End Left Column

            GUILayout.Space(15);

            // Right Column: New Match Controls (270px)
            GUILayout.BeginVertical(GUILayout.Width(270));

            GUILayout.Label("Match Controls", _boldLabelStyle);
            GUILayout.Space(8);

            if (GUILayout.Button("⚡ Force Start Game (Bypass Player Count)", _buttonStyle, GUILayout.Width(265), GUILayout.Height(34)))
            {
                DevTabManager.ForceStartGame();
                TriggerToast("Force Start Triggered!");
            }

            GUILayout.Space(14);
            DevTabManager.EnabledNoGameEnd = SafeToggle(DevTabManager.EnabledNoGameEnd, "No Game End (Infinite Match)");
            GUILayout.Space(6);
            if (DevTabManager.EnabledNoGameEnd)
            {
                GUILayout.Label("🔒 Matches will not terminate automatically when win conditions are met.", _paragraphStyle);
            }

            GUILayout.EndVertical(); // End Right Column

            GUILayout.EndHorizontal();
        }

        private static void DrawFunAddOns()
        {
            GUILayout.Label("Fun AddOns & Utilities", _boldLabelStyle);
            GUILayout.Space(6);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("💾 Save Configuration to Disk", _buttonStyle, GUILayout.Width(220), GUILayout.Height(26)))
            {
                ConfigManager.SaveConfig();
                TriggerToast("Config saved to AUGLMod.json!");
            }
            if (GUILayout.Button("📂 Reload Config", _buttonStyle, GUILayout.Width(140), GUILayout.Height(26)))
            {
                ConfigManager.LoadConfig();
                TriggerToast("Config reloaded!");
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(12);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Menu Toggle Key:", _labelStyle, GUILayout.Width(120));
            if (GUILayout.Button($"Key: {ToggleKey}", _buttonStyle, GUILayout.Width(100), GUILayout.Height(24)))
            {
                if (ToggleKey == KeyCode.F7) ToggleKey = KeyCode.F1;
                else if (ToggleKey == KeyCode.F1) ToggleKey = KeyCode.Insert;
                else if (ToggleKey == KeyCode.Insert) ToggleKey = KeyCode.RightShift;
                else ToggleKey = KeyCode.F7;
                ConfigManager.Current.MenuKey = ToggleKey;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(12);
            GUILayout.Label($"Camera Zoom Multiplier: {TargetZoom:0.0}x (Scroll Wheel supported)", _labelStyle);
            TargetZoom = GUILayout.HorizontalSlider(TargetZoom, 1.0f, 15.0f, GUILayout.Width(280));

            GUILayout.Space(12);
            ShowFpsPingHUD = SafeToggle(ShowFpsPingHUD, "FPS & Ping Telemetry Overlay");
            DeadRoleRevealerPatch.Enabled = SafeToggle(DeadRoleRevealerPatch.Enabled, "Dead Player Role Revealer (Red Impostors)");
            NoKillAnimationPatch.Enabled = SafeToggle(NoKillAnimationPatch.Enabled, "No Kill Animation (Skip Overlay)");
            CustomNameColorPatch.Enabled = SafeToggle(CustomNameColorPatch.Enabled, "Rich Text Custom Name Colors (<color=...>)");
            AntiKickShieldPatch.Enabled = SafeToggle(AntiKickShieldPatch.Enabled, "Anti-Kick / Anti-Ban Shield");
            DiscordRpcManager.Enabled = SafeToggle(DiscordRpcManager.Enabled, "Discord Rich Presence (RPC)");

            GUILayout.Space(12);
            GUILayout.Label("Quick-Chat Messages:", _boldLabelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("⚡ 'Glitched lobby active!'", _buttonStyle, GUILayout.Width(180), GUILayout.Height(24))) SendQuickChatMessage("Glitched lobby active! Join up via AUGL!");
            if (GUILayout.Button("👁️ 'I am on security cameras'", _buttonStyle, GUILayout.Width(180), GUILayout.Height(24))) SendQuickChatMessage("I am watching on security cameras.");
            if (GUILayout.Button("🛡️ 'Shield active'", _buttonStyle, GUILayout.Width(140), GUILayout.Height(24))) SendQuickChatMessage("Shield protection active!");
            GUILayout.EndHorizontal();

            GUILayout.Space(12);
            if (GUILayout.Button("✅ Auto-Solve Open Task Minigame", _buttonStyle, GUILayout.Width(260), GUILayout.Height(28)))
            {
                AutoSolveCurrentTask();
            }
        }

        private static void SendQuickChatMessage(string msg)
        {
            try
            {
                var localPlayerProp = AccessTools.Property(AccessTools.TypeByName("PlayerControl"), "LocalPlayer");
                var localPlayer = localPlayerProp?.GetValue(null);
                if (localPlayer != null)
                {
                    var rpcSend = AccessTools.Method(localPlayer.GetType(), "RpcSendChat");
                    rpcSend?.Invoke(localPlayer, new object[] { msg });
                }
            }
            catch { }
        }

        private static void DrawTrollTab()
        {
            GUILayout.Label("Troll & Experimental Utilities", _boldLabelStyle);
            GUILayout.Space(6);

            NoClipPatch.Enabled = SafeToggle(NoClipPatch.Enabled, "🚀 NoClip (Walk through all walls & barriers)");
            MaxVisionPatch.Enabled = SafeToggle(MaxVisionPatch.Enabled, "💡 Max Vision Through Walls (50f Radius + Full Visibility)");
            
            GUILayout.Space(12);
            if (GUILayout.Button("🧪 Trigger MedBay Scan Anywhere", _buttonStyle, GUILayout.Width(260), GUILayout.Height(28)))
            {
                TriggerMedBayScan();
            }

            GUILayout.Space(12);
            GUILayout.Label("Local Teleportation:", _boldLabelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Teleport Out of Bounds", _buttonStyle, GUILayout.Width(180), GUILayout.Height(26))) ChatCommandsPatch.TeleportLocalPlayer(new Vector2(-15f, 8f));
            if (GUILayout.Button("Teleport to Center", _buttonStyle, GUILayout.Width(150), GUILayout.Height(26))) ChatCommandsPatch.TeleportLocalPlayer(new Vector2(0f, -0.5f));
            GUILayout.EndHorizontal();

            GUILayout.Space(12);
            GUILayout.Label("Force Player Color (0-17):", _boldLabelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Red", _buttonStyle, GUILayout.Width(60))) ChatCommandsPatch.ForceLocalPlayerColor(0);
            if (GUILayout.Button("Blue", _buttonStyle, GUILayout.Width(60))) ChatCommandsPatch.ForceLocalPlayerColor(1);
            if (GUILayout.Button("Green", _buttonStyle, GUILayout.Width(60))) ChatCommandsPatch.ForceLocalPlayerColor(2);
            if (GUILayout.Button("Pink", _buttonStyle, GUILayout.Width(60))) ChatCommandsPatch.ForceLocalPlayerColor(3);
            if (GUILayout.Button("Orange", _buttonStyle, GUILayout.Width(65))) ChatCommandsPatch.ForceLocalPlayerColor(4);
            if (GUILayout.Button("Cyan", _buttonStyle, GUILayout.Width(60))) ChatCommandsPatch.ForceLocalPlayerColor(10);
            if (GUILayout.Button("Black", _buttonStyle, GUILayout.Width(60))) ChatCommandsPatch.ForceLocalPlayerColor(6);
            if (GUILayout.Button("White", _buttonStyle, GUILayout.Width(60))) ChatCommandsPatch.ForceLocalPlayerColor(7);
            GUILayout.EndHorizontal();
        }

        private static void DrawDocs()
        {
            GUILayout.Label("Documentation & Extras", _boldLabelStyle);
            GUILayout.Space(8);

            GUILayout.Label("What is a glitched lobby?", _titleStyle);
            GUILayout.Space(6);

            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Label(
                "Glitched Among Us servers forget your level. In a glitch lobby, the server shows level 1, but your true level is higher, for example 100. This mismatch changes how the server counts your level gain. When the server tries to raise your level from 1 to 2, it uses your true level instead. The server raises your level from 100 to 101. This is how players raise their level past 100.\n\n" +
                "Each lobby has a code. The last four letters of this code identify the server. Check these four letters against a list of glitch codes on this website. This check tells you if your server is glitched. For example, assume code XYZW is a glitch code. Lobby ABXYZW ends in XYZW. This lobby would be glitched.",
                _paragraphStyle);
            GUILayout.EndVertical();

            GUILayout.Space(14);
            GUILayout.Label("Chat Commands Reference", _boldLabelStyle);
            GUILayout.Space(4);
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Label("• <b>/codes</b> : List all active glitched lobby codes in chat\n" +
                            "• <b>/glitch</b> (or <b>/check</b>) : Check if current room is glitched\n" +
                            "• <b>/spoof</b> : Toggle PlayStation client spoofing\n" +
                            "• <b>/tpout /tpin</b> : Teleport outside / inside\n" +
                            "• <b>/menu</b> : Toggle this GUI menu\n" +
                            "• <b>/help</b> : Display command helper in local chat", _paragraphStyle);
            GUILayout.EndVertical();

            GUILayout.Space(14);
            GUILayout.Label("Preferences & In-Game Overlays", _boldLabelStyle);
            GUILayout.Space(4);
            ShowGlitchBadgeHUD = SafeToggle(ShowGlitchBadgeHUD, "Show Glitch Detector HUD Badge");
            AutoRefreshEnabled = SafeToggle(AutoRefreshEnabled, "Background Auto-Refresh (every 45s)");
            AutoCopyOnJoin = SafeToggle(AutoCopyOnJoin, "Auto-Copy Lobby Code to Clipboard on Join");

            GUILayout.Space(12);
            GUILayout.Label("Custom Region Management", _boldLabelStyle);
            GUILayout.Space(4);
            _regionToggle = SafeToggle(_regionToggle, "Enable Region Injection");
            RegionInstaller.Enabled = _regionToggle;
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Verify Default Regions (Official + AUGL)", _buttonStyle, GUILayout.Width(270), GUILayout.Height(24)))
            {
                RegionInstaller.Inject(false);
                TriggerToast("Verified Official + AUGL Regions!");
            }
            if (GUILayout.Button("Inject Community Modded Servers (MEU/MNA/Niko)", _buttonStyle, GUILayout.Width(330), GUILayout.Height(24)))
            {
                RegionInstaller.Inject(true);
                TriggerToast("Modded Servers Injected!");
            }
            GUILayout.EndHorizontal();
        }

        private static Vector2 _playerListScroll = Vector2.zero;
        private static Vector2 _playerActionScroll = Vector2.zero;

        private static void DrawPlayersTab()
        {
            var players = PlayersTabManager.GetPlayers();
            if (players.Count == 0)
            {
                GUILayout.Label("⚠ No players found in lobby/game.", _boldLabelStyle);
                return;
            }

            if (PlayersTabManager.SelectedPlayerId == 255 || !players.Any(p => p != null && p.PlayerId == PlayersTabManager.SelectedPlayerId))
            {
                PlayersTabManager.SelectedPlayerId = players[0].PlayerId;
            }

            PlayerControl target = players.FirstOrDefault(p => p != null && p.PlayerId == PlayersTabManager.SelectedPlayerId) ?? players[0];

            GUILayout.BeginHorizontal();

            // Left column: player list
            GUILayout.BeginVertical(GUILayout.Width(180));
            GUILayout.Label("<b>Lobby Players:</b>", _boldLabelStyle);
            _playerListScroll = GUILayout.BeginScrollView(_playerListScroll, GUILayout.Width(180), GUILayout.Height(370));

            foreach (var pc in players)
            {
                if (pc == null || pc.Data == null) continue;
                string pName = pc.Data.PlayerName ?? "???";
                if (pc == PlayerControl.LocalPlayer) pName += " (You)";

                if (PlayersTabManager.ForcedRoles.ContainsKey(pc.PlayerId))
                    pName += " [" + PlayersTabManager.ForcedRoles[pc.PlayerId] + "]";

                bool isSel = (pc.PlayerId == PlayersTabManager.SelectedPlayerId);
                GUIStyle btnSt = isSel ? _activeTextFieldStyle : _buttonStyle;

                if (GUILayout.Button(pName, btnSt, GUILayout.Height(26)))
                {
                    PlayersTabManager.SelectedPlayerId = pc.PlayerId;
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.Space(12);

            // Right column: selected player actions
            GUILayout.BeginVertical();
            _playerActionScroll = GUILayout.BeginScrollView(_playerActionScroll, GUILayout.Height(370));

            if (target != null && target.Data != null)
            {
                GUILayout.Label($"<b>Selected Player:</b> <color=cyan>{target.Data.PlayerName}</color> (ID: {target.PlayerId})", _boldLabelStyle);
                GUILayout.Space(8);

                // Murder Loop
                bool isMurderLoop = PlayersTabManager.PermanentMurderLoops.Contains(target.PlayerId);
                string murderLoopLabel = isMurderLoop ? "● STOP Murder Loop" : "▶ START Murder Loop";
                if (GUILayout.Button(murderLoopLabel, _buttonStyle, GUILayout.Width(220), GUILayout.Height(28)))
                {
                    if (isMurderLoop)
                    {
                        PlayersTabManager.PermanentMurderLoops.Remove(target.PlayerId);
                        TriggerToast($"Murder Loop stopped for {target.Data.PlayerName}");
                    }
                    else
                    {
                        PlayersTabManager.PermanentMurderLoops.Add(target.PlayerId);
                        TriggerToast($"Murder Loop started for {target.Data.PlayerName}");
                    }
                }

                GUILayout.Space(6);

                // 3. Permanent Shield
                bool isPermShield = PlayersTabManager.PermanentShields.Contains(target.PlayerId);
                string shieldLabel = isPermShield ? "🛡 SHIELD: ON (Click to Remove)" : "🛡 PROTECT (Permanent Shield)";
                if (GUILayout.Button(shieldLabel, _buttonStyle, GUILayout.Width(260), GUILayout.Height(28)))
                {
                    PlayerControl local = PlayerControl.LocalPlayer;
                    if (local != null)
                    {
                        if (isPermShield)
                        {
                            PlayersTabManager.PermanentShields.Remove(target.PlayerId);
                            target.protectedByGuardianThisRound = false;
                            target.protectedByGuardianId = -1;
                            try { target.TurnOnProtection(false, 0, -1); } catch { }
                            TriggerToast($"Shield removed for {target.Data.PlayerName}");
                        }
                        else
                        {
                            PlayersTabManager.PermanentShields.Add(target.PlayerId);
                            int col = 0;
                            try { col = (int)target.Data.DefaultOutfit.ColorId; } catch { }
                            try { target.TurnOnProtection(true, col, (int)local.PlayerId); } catch { }
                            local.RpcProtectPlayer(target, col);
                            TriggerToast($"Permanent Shield ON for {target.Data.PlayerName}");
                        }
                    }
                }

                GUILayout.Space(12);
                GUILayout.Label("🎭 Role Control", _boldLabelStyle);

                PlayersTabManager.TargetRoleIdx = Mathf.Clamp(PlayersTabManager.TargetRoleIdx, 0, PlayersTabManager.RolesList.Length - 1);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("◄", _buttonStyle, GUILayout.Width(30), GUILayout.Height(24)))
                {
                    PlayersTabManager.TargetRoleIdx = (PlayersTabManager.TargetRoleIdx - 1 + PlayersTabManager.RolesList.Length) % PlayersTabManager.RolesList.Length;
                }
                GUILayout.Label($"<color=#55ff55><b>{PlayersTabManager.RoleNames[PlayersTabManager.TargetRoleIdx]}</b></color>", _boldLabelStyle, GUILayout.Width(140), GUILayout.Height(24));
                if (GUILayout.Button("►", _buttonStyle, GUILayout.Width(30), GUILayout.Height(24)))
                {
                    PlayersTabManager.TargetRoleIdx = (PlayersTabManager.TargetRoleIdx + 1) % PlayersTabManager.RolesList.Length;
                }
                GUILayout.EndHorizontal();

                RoleTypes selectedRole = PlayersTabManager.RolesList[PlayersTabManager.TargetRoleIdx];

                GUILayout.Space(6);
                GUILayout.BeginHorizontal();

                if (GUILayout.Button("💾 Force on Start", _buttonStyle, GUILayout.Width(130), GUILayout.Height(26)))
                {
                    PlayersTabManager.ForcedRoles[target.PlayerId] = selectedRole;
                    TriggerToast($"Assigned {selectedRole} to {target.Data.PlayerName} for game start!");
                }

                if (GUILayout.Button("⚡ Set Role NOW", _buttonStyle, GUILayout.Width(130), GUILayout.Height(26)))
                {
                    PlayersTabManager.ForcedRoles[target.PlayerId] = selectedRole;
                    PlayersTabManager.ApplyRoleToPlayer(target, selectedRole);
                    TriggerToast($"Set {target.Data.PlayerName} to {selectedRole}!");
                }
                GUILayout.EndHorizontal();

                if (PlayersTabManager.ForcedRoles.Count > 0)
                {
                    GUILayout.Space(8);
                    GUILayout.Label("<b>Active Forced Roles:</b>", _boldLabelStyle);
                    foreach (var kvp in PlayersTabManager.ForcedRoles.ToList())
                    {
                        var p = PlayersTabManager.GetPlayerById(kvp.Key);
                        string name = p?.Data?.PlayerName ?? ("ID " + kvp.Key);
                        GUILayout.Label($"• {name} ➔ {kvp.Value}", _labelStyle);
                    }
                    if (GUILayout.Button("🗑 Clear All Role Assignments", _buttonStyle, GUILayout.Width(220), GUILayout.Height(24)))
                    {
                        PlayersTabManager.ForcedRoles.Clear();
                        TriggerToast("Cleared all forced roles.");
                    }
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }


        private static Texture2D MakeTex(Color col)
        {
            var t = new Texture2D(1, 1); t.SetPixel(0, 0, col); t.Apply(); return t;
        }
    }
}
