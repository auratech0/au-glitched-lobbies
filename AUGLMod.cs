using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.InteropTypes;
using BepInEx.Unity.IL2CPP;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace AUGLMod
{
    [BepInPlugin("com.al3x4nderr.auglmod", "AUGL Menu", "1.9.21")]
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
            DoorBlockPatch.ApplySafe(harmony);
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
            public bool CosmeticsUnlock { get; set; } = true;
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
                    ApplyConfig();
                }
            }
            catch { }
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
            AUGLMenuGUI.ToggleKey = Current.MenuKey;
            AUGLMenuGUI.TargetZoom = Current.CameraZoom;
        }
    }

    // ================= API Client =================
    public class GlitchedCodeResponse
    {
        public string Code { get; set; }
        public bool Glitched { get; set; }
        public int Port { get; set; }
    }

    public static class AUGLApiClient
    {
        private static readonly HttpClient Client = new HttpClient();
        private const string Endpoint = "https://api.augl.net/v1/codes";

        public static async Task<List<GlitchedCodeResponse>> FetchCodesAsync()
        {
            try
            {
                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var json = await Client.GetStringAsync(Endpoint);
                return JsonSerializer.Deserialize<List<GlitchedCodeResponse>>(json, opts) ?? new List<GlitchedCodeResponse>();
            }
            catch (Exception ex)
            {
                AUGLModPlugin.Log?.LogWarning($"API fetch warning: {ex.Message}");
                return new List<GlitchedCodeResponse>();
            }
        }
    }

    // ================= Platform Spoofing =================
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

                    var getPlatformData = AccessTools.Method(psdType, "GetPlatformData");
                    if (getPlatformData != null)
                    {
                        _harmony.Patch(getPlatformData, postfix: new HarmonyMethod(typeof(PlatformSpoofManager).GetMethod(nameof(PostfixGetPlatformData), BindingFlags.NonPublic | BindingFlags.Static)));
                    }
                }

                var constType = AccessTools.TypeByName("Constants");
                if (constType != null)
                {
                    var getPlatMethod = AccessTools.Method(constType, "GetPlatform") ?? AccessTools.Method(constType, "GetPlatformType");
                    if (getPlatMethod != null)
                    {
                        _harmony.Patch(getPlatMethod, prefix: new HarmonyMethod(typeof(PlatformSpoofManager).GetMethod(nameof(PrefixGetPlatform), BindingFlags.NonPublic | BindingFlags.Static)));
                    }
                }

                _active = true;
                ConfigManager.Current.PlayStationSpoof = true;
                AUGLModPlugin.Log?.LogInfo("PlayStation platform spoofing enabled (Platform ID 10).");
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

        private static void PrefixSerialize(Il2CppSystem.Object __instance)
        {
            if (__instance == null) return;
            try
            {
                ReflectionUtils.SetMemberValue(__instance, "Platform", (int)10);
                ReflectionUtils.SetMemberValue(__instance, "platform", (int)10);
            }
            catch { }
        }

        private static void PostfixGetPlatformData(ref Il2CppSystem.Object __result)
        {
            if (__result == null) return;
            try
            {
                ReflectionUtils.SetMemberValue(__result, "Platform", (int)10);
                ReflectionUtils.SetMemberValue(__result, "platform", (int)10);
            }
            catch { }
        }

        private static bool PrefixGetPlatform(ref Il2CppSystem.Object __result)
        {
            try
            {
                __result = (Il2CppSystem.Object)(object)(int)10;
                return false;
            }
            catch { return true; }
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
                var statsType = AccessTools.TypeByName("StatsManager");
                if (statsType == null) return;

                string[] penaltyMethods = new string[] { "get_BanMinutesLeft", "get_BanMinutes", "get_AmBanned" };
                foreach (var mName in penaltyMethods)
                {
                    var m = AccessTools.Method(statsType, mName);
                    if (m != null)
                    {
                        harmony.Patch(m, prefix: new HarmonyMethod(typeof(AntiLeavePenaltyPatch).GetMethod(nameof(PrefixBypass), BindingFlags.NonPublic | BindingFlags.Static)));
                    }
                }
                AUGLModPlugin.Log?.LogInfo("AntiLeavePenaltyPatch applied.");
            }
            catch (Exception ex)
            {
                AUGLModPlugin.Log?.LogWarning($"AntiLeavePenaltyPatch warning: {ex.Message}");
            }
        }

        private static bool PrefixBypass(MethodBase __originalMethod, ref Il2CppSystem.Object __result)
        {
            if (!Enabled) return true;
            try
            {
                if (__originalMethod.Name.Contains("AmBanned"))
                {
                    __result = (Il2CppSystem.Object)(object)false;
                    return false;
                }
                else
                {
                    __result = (Il2CppSystem.Object)(object)0;
                    return false;
                }
            }
            catch { return true; }
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

                var dataProp = AccessTools.Property(myPlayer.GetType(), "Data");
                var data = dataProp?.GetValue(myPlayer);
                if (data == null) return;

                var isDeadField = AccessTools.Field(data.GetType(), "IsDead");
                bool isDead = (bool)(isDeadField?.GetValue(data) ?? false);

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
            try
            {
                var pcType = AccessTools.TypeByName("PlayerPhysics");
                if (pcType != null)
                {
                    var handleMove = AccessTools.Method(pcType, "HandleMovement");
                    if (handleMove != null)
                    {
                        harmony.Patch(handleMove, prefix: new HarmonyMethod(typeof(NoClipPatch).GetMethod(nameof(PrefixHandleMovement), BindingFlags.NonPublic | BindingFlags.Static)));
                    }
                }
            }
            catch { }
        }

        private static void PrefixHandleMovement(Il2CppSystem.Object __instance)
        {
            if (!Enabled || __instance == null) return;
            try
            {
                var colliderProp = AccessTools.Property(__instance.GetType(), "Collider");
                var collider = colliderProp?.GetValue(__instance);
                if (collider != null)
                {
                    ReflectionUtils.SetMemberValue(collider, "enabled", false);
                }
            }
            catch { }
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
                var lsType = AccessTools.TypeByName("LightSource");
                if (lsType != null)
                {
                    var update = AccessTools.Method(lsType, "Update");
                    if (update != null)
                    {
                        harmony.Patch(update, postfix: new HarmonyMethod(typeof(MaxVisionPatch).GetMethod(nameof(PostfixLightUpdate), BindingFlags.NonPublic | BindingFlags.Static)));
                    }
                }
            }
            catch { }
        }

        private static void PostfixLightUpdate(Il2CppSystem.Object __instance)
        {
            if (!Enabled || __instance == null) return;
            try
            {
                ReflectionUtils.SetMemberValue(__instance, "viewDistance", 50f);
                ReflectionUtils.SetMemberValue(__instance, "ViewDistance", 50f);
            }
            catch { }
        }
    }

    // ================= Dead Role Revealer (Fun AddOns) =================
    public static class DeadRoleRevealerPatch
    {
        public static bool Enabled = true;

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
                var localPlayerProp = AccessTools.Property(AccessTools.TypeByName("PlayerControl"), "LocalPlayer");
                var localPlayer = localPlayerProp?.GetValue(null);
                if (localPlayer == null) return;

                var localDataProp = AccessTools.Property(localPlayer.GetType(), "Data");
                var localData = localDataProp?.GetValue(localPlayer);
                if (localData == null) return;

                var localIsDead = ReflectionUtils.GetMemberValue(localData, "IsDead");
                bool isDead = (bool)(localIsDead ?? false);

                if (isDead)
                {
                    var pDataProp = AccessTools.Property(__instance.GetType(), "Data");
                    var pData = pDataProp?.GetValue(__instance);
                    if (pData != null)
                    {
                        var isImpVal = ReflectionUtils.GetMemberValue(pData, "IsImpostor");
                        bool isImp = (bool)(isImpVal ?? false);

                        if (isImp)
                        {
                            var nameTextProp = AccessTools.Field(__instance.GetType(), "nameText")?.GetValue(__instance);
                            if (nameTextProp != null)
                            {
                                ReflectionUtils.SetMemberValue(nameTextProp, "color", Color.red);
                            }
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
                    var showKill = AccessTools.Method(koType, "ShowKillAnimation");
                    if (showKill != null)
                    {
                        harmony.Patch(showKill, prefix: new HarmonyMethod(typeof(NoKillAnimationPatch).GetMethod(nameof(PrefixShowKill), BindingFlags.NonPublic | BindingFlags.Static)));
                    }
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
                    var handleKick = AccessTools.Method(incType, "HandleDisconnect");
                    if (handleKick != null)
                    {
                        harmony.Patch(handleKick, prefix: new HarmonyMethod(typeof(AntiKickShieldPatch).GetMethod(nameof(PrefixHandleDisconnect), BindingFlags.NonPublic | BindingFlags.Static)));
                    }
                }
            }
            catch { }
        }

        private static bool PrefixHandleDisconnect(Il2CppSystem.Object reason, string customReason)
        {
            if (Enabled && customReason != null && customReason.ToLower().Contains("kicked"))
            {
                AUGLModPlugin.Log?.LogInfo("AntiKickShield blocked an unauthorized kick attempt!");
                return false;
            }
            return true;
        }
    }

    // ================= Pure C# Discord RPC Manager =================
    public static class DiscordRpcManager
    {
        public static bool Enabled = true;
        private static NamedPipeClientStream _pipe;
        private static bool _connected = false;
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
            try
            {
                for (int i = 0; i < 10; i++)
                {
                    string pipeName = Environment.OSVersion.Platform == PlatformID.Unix
                        ? Path.Combine(Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") ?? "/tmp", $"discord-ipc-{i}")
                        : $"discord-ipc-{i}";

                    if (Environment.OSVersion.Platform == PlatformID.Unix && !File.Exists(pipeName)) continue;

                    _pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
                    _pipe.Connect(500);
                    if (_pipe.IsConnected)
                    {
                        _connected = true;
                        SendHandshake();
                        AUGLModPlugin.Log?.LogInfo($"Discord RPC connected via pipe {i}.");
                        break;
                    }
                }
            }
            catch { _connected = false; }
        }

        private static void SendHandshake()
        {
            try
            {
                if (!_connected || _pipe == null) return;
                string json = $"{{\"v\":1,\"client_id\":\"{ClientId}\"}}";
                byte[] body = Encoding.UTF8.GetBytes(json);
                byte[] header = new byte[8];
                BitConverter.GetBytes(0).CopyTo(header, 0);
                BitConverter.GetBytes(body.Length).CopyTo(header, 4);
                _pipe.Write(header, 0, 8);
                _pipe.Write(body, 0, body.Length);
                _pipe.Flush();
            }
            catch { _connected = false; }
        }

        public static void UpdatePresence(string details, string state, string partyCode = null, int partySize = 0, int partyMax = 15)
        {
            if (!Enabled || !_connected || _pipe == null) return;
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
                                large_text = "AUGL Menu v1.9.21"
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
                _pipe.Write(header, 0, 8);
                _pipe.Write(body, 0, body.Length);
                _pipe.Flush();
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

            if (!_connected)
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

    // ================= Cosmetics Unlocker =================
    public static class CosmeticsUnlocker
    {
        public static bool Enabled = true;

        public static void ApplySafe(Harmony harmony)
        {
            try
            {
                var type = AccessTools.TypeByName("HatManager");
                if (type == null) return;

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
                AUGLModPlugin.Log?.LogInfo("CosmeticsUnlocker patches applied.");
            }
            catch (Exception ex)
            {
                AUGLModPlugin.Log?.LogWarning($"CosmeticsUnlocker setup warning: {ex.Message}");
            }
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
                        ShowChatMsg("Commands: /codes, /glitch, /autojoin, /spoof, /level, /start, /endgame, /endmeeting, /kick, /ban, /tpout, /tpin, /color, /menu, /help");
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
                    case "/autojoin":
                    case "/join":
                        AUGLMenuGUI.AutoJoinGlitchedLobby();
                        ShowChatMsg("Connecting to active glitched lobby...");
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

                var netTransformProp = AccessTools.Property(localPlayer.GetType(), "NetTransform");
                var netTransform = netTransformProp?.GetValue(localPlayer);
                if (netTransform == null) return;

                var snapMethod = AccessTools.Method(netTransform.GetType(), "SnapTo");
                snapMethod?.Invoke(netTransform, new object[] { pos, (ushort)0 });
            }
            catch { }
        }

        public static void ForceLocalPlayerColor(byte colorId)
        {
            try
            {
                var localPlayerProp = AccessTools.Property(AccessTools.TypeByName("PlayerControl"), "LocalPlayer");
                var localPlayer = localPlayerProp?.GetValue(null);
                if (localPlayer == null) return;

                var rpcSetColor = AccessTools.Method(localPlayer.GetType(), "RpcSetColor");
                rpcSetColor?.Invoke(localPlayer, new object[] { colorId });
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
                var gomType = AccessTools.TypeByName("GameOptionsManager");
                var gomInstProp = gomType?.GetProperty("Instance");
                var gom = gomInstProp?.GetValue(null);
                var currOptProp = gomType?.GetProperty("CurrentGameOptions");
                var options = currOptProp?.GetValue(gom);
                if (options == null) return;

                var setFloat = AccessTools.Method(options.GetType(), "SetFloat", new Type[] { typeof(int), typeof(float) })
                            ?? AccessTools.Method(options.GetType(), "SetFloat");

                if (setFloat != null)
                {
                    setFloat.Invoke(options, new object[] { 2, PlayerSpeed });
                    setFloat.Invoke(options, new object[] { 3, CrewLight });
                    setFloat.Invoke(options, new object[] { 4, ImpLight });
                }

                var syncMethod = AccessTools.Method(gomType, "SyncGameOptions") ?? AccessTools.Method(gomType, "Dirty");
                syncMethod?.Invoke(gom, null);
                AUGLModPlugin.Log?.LogInfo($"Host settings synced: Speed={PlayerSpeed}x, CrewLight={CrewLight}x, ImpLight={ImpLight}x");
            }
            catch (Exception ex)
            {
                AUGLModPlugin.Log?.LogWarning($"ApplyHostSettings exception: {ex.Message}");
            }
        }

        public static void TriggerRemoteDoor(int systemType, bool close)
        {
            try
            {
                var shipStatusType = AccessTools.TypeByName("ShipStatus");
                var ssInst = shipStatusType?.GetProperty("Instance")?.GetValue(null);
                if (ssInst == null) return;

                var rpcClose = AccessTools.Method(shipStatusType, "RpcCloseDoorsOfType");
                rpcClose?.Invoke(ssInst, new object[] { systemType });
                AUGLModPlugin.Log?.LogInfo($"Remote door action triggered for system {systemType}.");
            }
            catch { }
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
        public const string RegionJson = @"{
            ""CurrentRegionIdx"": 1,
            ""Regions"": [
                { ""$type"": ""StaticHttpRegionInfo, Assembly-CSharp"", ""Name"": ""AUGL Codes"", ""PingServer"": ""augl.net"", ""Servers"": [ { ""Name"": ""Http-1"", ""Ip"": ""augl.net"", ""Port"": 443, ""UseDtls"": false } ], ""TargetServer"": null, ""TranslateName"": 1003 },
                { ""$type"": ""StaticHttpRegionInfo, Assembly-CSharp"", ""Name"": ""North America"", ""PingServer"": ""matchmaker.among.us"", ""Servers"": [ { ""Name"": ""Http-1"", ""Ip"": ""https://matchmaker.among.us"", ""Port"": 443, ""UseDtls"": false } ], ""TargetServer"": null, ""TranslateName"": 289 },
                { ""$type"": ""StaticHttpRegionInfo, Assembly-CSharp"", ""Name"": ""Europe"", ""PingServer"": ""matchmaker-eu.among.us"", ""Servers"": [ { ""Name"": ""Http-1"", ""Ip"": ""https://matchmaker-eu.among.us"", ""Port"": 443, ""UseDtls"": false } ], ""TargetServer"": null, ""TranslateName"": 290 },
                { ""$type"": ""StaticHttpRegionInfo, Assembly-CSharp"", ""Name"": ""Asia"", ""PingServer"": ""matchmaker-as.among.us"", ""Servers"": [ { ""Name"": ""Http-1"", ""Ip"": ""https://matchmaker-as.among.us"", ""Port"": 443, ""UseDtls"": false } ], ""TargetServer"": null, ""TranslateName"": 291 }
            ]
        }";

        public static bool Enabled = true;

        public static void Inject()
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
                var data = JsonSerializer.Deserialize<RegionData>(RegionJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (data?.Regions == null) return;

                foreach (var r in data.Regions)
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
                AUGLModPlugin.Log?.LogInfo("Regions injected");
            }
            catch (Exception ex) { AUGLModPlugin.Log?.LogError($"Region error: {ex.Message}"); }
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
                var clientType = AccessTools.TypeByName("AmongUsClient");
                var instanceProp = clientType?.GetProperty("Instance");
                var client = instanceProp?.GetValue(null);
                if (client == null) return;
                var amHostProp = clientType.GetProperty("AmHost");
                bool amHost = (bool)(amHostProp?.GetValue(client) ?? false);
                var isPublicField = AccessTools.Field(clientType, "_IsGamePublic_k__BackingField");
                bool isPublic = (bool)(isPublicField?.GetValue(client) ?? false);
                if (!amHost || isPublic) return;

                var gomType = AccessTools.TypeByName("GameOptionsManager");
                var gomInstanceProp = gomType?.GetProperty("Instance");
                var gom = gomInstanceProp?.GetValue(null);
                var currentOptionsProp = gomType?.GetProperty("CurrentGameOptions");
                var options = currentOptionsProp?.GetValue(gom);
                if (options == null) return;

                var setInt = AccessTools.Method(options.GetType(), "SetInt", new Type[] { typeof(int), typeof(int) });
                var setFloat = AccessTools.Method(options.GetType(), "SetFloat", new Type[] { typeof(int), typeof(float) });

                ActiveModeName = name;

                switch (name)
                {
                    case "SNS":
                        SabotageBlocking = true;
                        setInt?.Invoke(options, new object[] { 0, 2 });
                        setFloat?.Invoke(options, new object[] { 1, 12.5f });

                        SetRoleOptionValue(options, "Shapeshifter", 2, 100, 7f, 35f);
                        SetRoleOptionValue(options, "Engineer", 13, 100, 5f, 5f);
                        ZeroOutOtherRoles(options, new string[] { "Shapeshifter", "Engineer" });
                        break;

                    case "Shields":
                        SabotageBlocking = false;
                        setInt?.Invoke(options, new object[] { 0, 1 });
                        setFloat?.Invoke(options, new object[] { 1, 0f });

                        SetRoleOptionValue(options, "Viper", 1, 100, 3f, 3f);
                        SetRoleOptionValue(options, "Engineer", 14, 100, 0f, 9999f);
                        SetRoleOptionValue(options, "GuardianAngel", 15, 100, 0f, 9999f);
                        ZeroOutOtherRoles(options, new string[] { "Viper", "Engineer", "GuardianAngel" });
                        break;

                    case "Normal":
                    default:
                        SabotageBlocking = false;
                        setInt?.Invoke(options, new object[] { 0, 2 });
                        setFloat?.Invoke(options, new object[] { 1, 15f });
                        ActiveModeName = "Normal (Roles Galore)";
                        break;
                }

                var syncMethod = AccessTools.Method(gomType, "SyncGameOptions") ?? AccessTools.Method(gomType, "Dirty");
                syncMethod?.Invoke(gom, null);
                AUGLModPlugin.Log?.LogInfo($"Preset '{name}' applied successfully.");
            }
            catch (Exception ex)
            {
                AUGLModPlugin.Log?.LogWarning($"ApplyPreset exception: {ex.Message}");
            }
        }

        private static void SetRoleOptionValue(object options, string roleName, int count, int chance, float param1, float param2)
        {
            try
            {
                var roleOptProp = AccessTools.Property(options.GetType(), "RoleOptions")
                               ?? AccessTools.Property(options.GetType(), "roleOptionsCollectionV10")
                               ?? AccessTools.Property(options.GetType(), "RoleOptionsCollection");
                var roleCollection = roleOptProp?.GetValue(options) ?? options;

                foreach (var prop in roleCollection.GetType().GetProperties())
                {
                    if (prop.Name.ToLower().Contains(roleName.ToLower()))
                    {
                        var roleData = prop.GetValue(roleCollection);
                        if (roleData != null)
                        {
                            ReflectionUtils.SetMemberValue(roleData, "Count", count);
                            ReflectionUtils.SetMemberValue(roleData, "Chance", chance);

                            foreach (var f in roleData.GetType().GetFields())
                            {
                                string fName = f.Name.ToLower();
                                if (fName.Contains("cooldown")) f.SetValue(roleData, param1);
                                else if (fName.Contains("duration") || fName.Contains("time") || fName.Contains("dissolve")) f.SetValue(roleData, param2);
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private static void ZeroOutOtherRoles(object options, string[] keepRoles)
        {
            try
            {
                var roleOptProp = AccessTools.Property(options.GetType(), "RoleOptions")
                               ?? AccessTools.Property(options.GetType(), "roleOptionsCollectionV10")
                               ?? AccessTools.Property(options.GetType(), "RoleOptionsCollection");
                var roleCollection = roleOptProp?.GetValue(options) ?? options;

                string[] allRoles = { "Scientist", "Engineer", "GuardianAngel", "Shapeshifter", "Tracker", "Noisemaker", "Phantom", "Viper", "Detective" };
                foreach (var r in allRoles)
                {
                    bool keep = false;
                    foreach (var k in keepRoles) if (k.Equals(r, StringComparison.OrdinalIgnoreCase)) { keep = true; break; }
                    if (!keep)
                    {
                        SetRoleOptionValue(options, r, 0, 0, 0, 0);
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
            }
            catch (Exception ex) { AUGLModPlugin.Log?.LogWarning($"SabotageBlockPatch error: {ex.Message}"); }
        }

        private static bool Prefix(Il2CppSystem.Object __instance, ref Il2CppSystem.Object systemType, ref byte amount)
        {
            try
            {
                if (!GameModePresetManager.SabotageBlocking) return true;
                int sysType = systemType.Unbox<int>();
                if (sysType != 14) return false;
                return true;
            }
            catch { return true; }
        }
    }

    public static class DoorBlockPatch
    {
        public static void ApplySafe(Harmony harmony)
        {
            try
            {
                var type = AccessTools.TypeByName("ShipStatus");
                var method = AccessTools.Method(type, "RpcCloseDoorsOfType");
                if (method != null)
                {
                    harmony.Patch(method, prefix: new HarmonyMethod(typeof(DoorBlockPatch).GetMethod("Prefix", BindingFlags.NonPublic | BindingFlags.Static)));
                    AUGLModPlugin.Log?.LogInfo("DoorBlockPatch applied.");
                }
            }
            catch (Exception ex) { AUGLModPlugin.Log?.LogWarning($"DoorBlockPatch error: {ex.Message}"); }
        }

        private static bool Prefix()
        {
            return !GameModePresetManager.SabotageBlocking;
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
                if (Input.GetKeyDown(AUGLMenuGUI.ToggleKey))
                {
                    AUGLMenuGUI.ToggleOpen();
                }

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
                AUGLModPlugin.Log?.LogWarning($"Draw error: {ex.Message}");
            }
        }
    }

    // ================= Static GUI Class =================
    public static class AUGLMenuGUI
    {
        private static bool _open;
        public static bool IsOpen => _open;
        private static int _tab;
        private static Vector2 _scroll;
        private static List<GlitchedCodeResponse> _codes = new List<GlitchedCodeResponse>();
        private static string _status = "Loading...";
        private static string _killCdInput = "0";
        private static string _angelInput = "30";
        private static string _angelCdInput = "0";
        private static string _levelInput = "999";
        private static string _searchQuery = "";
        private static string _focusedField = null;
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
        private static string _eggB64 = "R0lGODlhgACAAPf7AAEBAYKAOYqQmUNDFr6/VUxNUoiAb73Iw2hjLCYjDqijRKKvtGVkX9reby8sK4aDVEVCNL6/e4+TcOXl5XBucZiTPIyeo7DAv8nNWnlyLjc0FFZUMsnZ1neChaKhobGzTDk2LltZVRoYB1ZRKnyJjISDQ6m1u0RDQ8C/bHd0UiMiHXt5Qr7N0nV1dpSUV4yWhBQUFEVDJGZmZTs6Op2dRMLBwXBrLKKhXKu0rT87HYqJhX5+gcTHUM/QbaShqpmbleTjgNDRfJelqYV9L1FbYYuKRUpJRi8rHGxxZtrZ1U5LJYmXnFRTVIuLU1RRTrOzWn19foeSlR4bFa2sU+Tmevj4+MTT1I6MO83RziwyOkpLNbS0bpWTSTY0IsfFytPWcMLBZGdkOzQxL4SEhD89MD89Pba2tJqamllWV8vLyy4sFN7ce/Dw8JympHZyO19cNYKMk6q5vsfIboJ+R3p5epuaU46OkKqpqtvWkWNiY1FNSuLi4pSTlIqDOGZkNPDylrO1j5KPf3N3Z5+fa15bSbnHzc3PZKqyewwMDG9wV8jIgl9cK6urXqalpYCBfqWoe2xqOn18O6WjU4aHZMbIZp6bZrXEwWFhXiEeDtbWfbCvsJ6dntTU1N7ib3Jyc359V8bFxZqprePi37q6Yv///7y7d726ebe3udLO0WFeYVJGGurp6b6+vYF2L9Da2bi0VkI6K2BeXSAbCWFXK4qFRkpGRSomIoF4QZKRj0A7O6OdRnlvNJOOR3FvblJMLJaXmpKPV1FNMrCrq9DVZaWoaIF9fTgzM7q5ukpGH7/BXL7MyiUjE6akTNnbdYiGXUZCPr/ChJGUe5qVRK+/xHlxMzczG1ZSP8va23iFizk2NVxbXBgXC4KBS0ZGSCQjI3l3S3Z3eYiRjhoVGUlFKmppazY7QZuZTG5sM6q3sz47IoyMjcfHXM/Pc9/djsrPgIZ8NIyLSkxLTCwqJW1tbtzb20xKLIuZoU9UWY+OWbezZR0cGwAAAAAAAAAAAAAAAAAAACH/C05FVFNDQVBFMi4wAwEAAAAh+QQFAAD7ACwAAAAAgACAAAAI/wD3CRxIsKDBgwgTKlzIsKHDhxAjKvxFgoI2bUyYaIMisaPHjyAZnoFTLJVJJvFO5Mo1Y0a2l3pCypxJ86AdbBRM3rv3rKWYnz9fZjtxwphRYzFrKl3KUMAOcuSIEClQpupPBw6O5qrFlYkMGQL1lAGalKlZpTrgUKCQ517KckCxOnjJtVaIi0wQhsj1k8zZvyE97GBb4IRcuUZzxVscKxZEJ0BPAJ4MEY4PT90OO1iZS4+eiyHJYJ1BufRCTz7uyc1SNUSIZ0uNzZVsujbBAmMKeJtxggnsvy5f2h6eLVWBzWUnG/093PSMzA5sZ811qXnpEz672bYF0zpleUCH//9s6X2yg/C2x4phXt7sT3llmv+k3d69GHlGmssDkYtcffvy1KLfSwX8x9R7ucg3n4FLgYdVcy+R4ReDNYFnixjWkcEahTX9dKF1QnFI0zP3iZHXcCCkKOJMKtwnTzb6iUHaijJdFZ14YkxII0gXcqfgjDt+dJiAtvUVJEjvvTicA1UdCVI2PXYEw5RUwuCNN/pgqaU+Weojz5cAhPnlmPJcqY8D3iACAyJqOlmQGBfKA1GYAFRZpSd45ukJFFDg2VZjgN6jjaD35DFPC/Egog8MXE6p5ppr0hkmDEYZeFhDtgCAiC1jcFIFG1WEKioppJZKKifqbNIBDmmw4moNsML/egwfHnhAAgmg5JqrK2z06usdtXowD5uKMqpPmIoOp0+JC3mjKTmrmCotqRPQM+s8m3zKxgSccLLHHtOWukcaaewhKqhskCpqFeGSEuoqaXDCRx55TDlpad7Y4mNCx56Qbrts0MOGB7HUMEEa9JDKCh0y9GJGI42AGy4rZ5xhBJ6NarLuur72qMw84IHfARCx35HEGKausckeiiIQ5WTbgIXQsAHa0i/Id2ozR6x5myBCFGfRwe0AaNRxj9DF3JK30MQmT+u0e/6rLbrj0YGE1FhOUWq2pbKQxRsuI0LdUGQ8aBAMA2XjQLj1pmMEJyuBokoYmVvwgwxmseFCDGWY8/73HBIBP4EosnlwQrc2kbntHC2M0PsYZ3XbrgToS23zG2TCYJdqNBNWiKCuhmupKMa7ckXAa6pgxBit8sJJGMZscjjipoNihyeynNqKOOjW4ykrlpEzAhyZ7qHNyu6AwoSlTUNpi0LEkhG7qMXQk7oEMNVTRSONNz75uqXqYIT3ix+CAOymsyHCHzfRoGk9s+gqEtgMt6xF1uF43ssoZ8zRyf6m6CuC0QAWFRpxPHXRYhcYWODVSxCJr4QJFmOKjFLmo4CUzq4Urxjetw9EDClG7A5/oQIdLSIpODviKJ8YACtq9DXfkgAIHEZcGUDVQXDOo01IOw6U6gfB80lJHHv9OcEI6tSxMdujAV7IRJnKMwXuiGsMmSOU1x5HwinToQBpwl4ZjIaKCDphZmMbAtgD2SnpmqNg unfamiliar=";

        private static Rect _windowRect = new Rect(200, 150, 780, 520);
        private static bool _initialized;
        private static Texture2D _winBgTex;
        private static Texture2D _boxBgTex;
        private static Texture2D _btnNormTex;
        private static Texture2D _btnHoverTex;
        private static Texture2D _textFieldBgTex;
        private static Texture2D _activeTextFieldBgTex;
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

        private static void InitStyles()
        {
            if (_initialized && _winBgTex != null && _btnNormTex != null && _boxBgTex != null && _activeTextFieldBgTex != null)
            {
                return;
            }

            if (Screen.width > 0 && Screen.height > 0)
            {
                _windowRect = new Rect((Screen.width - 780) / 2, (Screen.height - 520) / 2, 780, 520);
            }

            _winBgTex = MakeTex(new Color(0.07f, 0.07f, 0.10f, 0.97f));
            _boxBgTex = MakeTex(new Color(0.13f, 0.13f, 0.17f, 0.85f));
            _btnNormTex = MakeTex(new Color(0.15f, 0.15f, 0.20f, 1f));
            _btnHoverTex = MakeTex(new Color(0.25f, 0.25f, 0.35f, 1f));
            _textFieldBgTex = MakeTex(new Color(0.05f, 0.05f, 0.07f, 1f));
            _activeTextFieldBgTex = MakeTex(new Color(0.12f, 0.22f, 0.40f, 1f));

            _winBgTex.hideFlags = HideFlags.DontSave;
            _boxBgTex.hideFlags = HideFlags.DontSave;
            _btnNormTex.hideFlags = HideFlags.DontSave;
            _btnHoverTex.hideFlags = HideFlags.DontSave;
            _textFieldBgTex.hideFlags = HideFlags.DontSave;
            _activeTextFieldBgTex.hideFlags = HideFlags.DontSave;

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
                _codes = await AUGLApiClient.FetchCodesAsync();
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

        public static string GetCurrentLobbyCode()
        {
            try
            {
                var clientType = AccessTools.TypeByName("AmongUsClient");
                var instProp = clientType?.GetProperty("Instance");
                var client = instProp?.GetValue(null);
                if (client == null) return null;

                var gameIdProp = clientType.GetProperty("GameId") ?? clientType.GetProperty("gameId");
                int gameId = (int)(gameIdProp?.GetValue(client) ?? 0);
                if (gameId == 0) return null;

                var gameCodeType = AccessTools.TypeByName("GameCode") ?? AccessTools.TypeByName("InnerNet.GameCode");
                var intToGameNameMethod = AccessTools.Method(gameCodeType, "IntToGameName", new Type[] { typeof(int) });
                if (intToGameNameMethod != null)
                {
                    return (string)intToGameNameMethod.Invoke(null, new object[] { gameId });
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

        public static void AutoJoinGlitchedLobby()
        {
            try
            {
                var glitched = GetGlitchedCodesList();
                if (glitched.Count == 0)
                {
                    _ = FetchCodes();
                    return;
                }

                var chosen = glitched[UnityEngine.Random.Range(0, glitched.Count)];
                string codeStr = chosen.Code.Trim().ToUpper();
                GUIUtility.systemCopyBuffer = codeStr;
                TriggerToast($"Connecting to {codeStr}...");

                var matchMakerType = AccessTools.TypeByName("MatchMaker");
                var mmInstProp = matchMakerType?.GetProperty("Instance");
                var mm = mmInstProp?.GetValue(null);

                if (mm != null)
                {
                    var joinStrMethod = AccessTools.Method(matchMakerType, "JoinGameFromCode", new Type[] { typeof(string) });
                    if (joinStrMethod != null)
                    {
                        joinStrMethod.Invoke(mm, new object[] { codeStr });
                        return;
                    }
                }

                var gameCodeType = AccessTools.TypeByName("GameCode") ?? AccessTools.TypeByName("InnerNet.GameCode");
                var gameNameToInt = AccessTools.Method(gameCodeType, "GameNameToInt", new Type[] { typeof(string) });
                if (gameNameToInt != null && mm != null)
                {
                    int intCode = (int)gameNameToInt.Invoke(null, new object[] { codeStr });
                    var joinIntMethod = AccessTools.Method(matchMakerType, "JoinGameFromCode", new Type[] { typeof(int) });
                    joinIntMethod?.Invoke(mm, new object[] { intCode });
                }
            }
            catch { }
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

        public static void DrawFpsPingHUD()
        {
            try
            {
                InitStyles();
                int fps = (int)(1.0f / Mathf.Max(0.0001f, Time.smoothDeltaTime));
                int ping = 0;
                var clientType = AccessTools.TypeByName("AmongUsClient");
                var inst = clientType?.GetProperty("Instance")?.GetValue(null);
                if (inst != null)
                {
                    var pingF = AccessTools.Field(clientType, "Ping");
                    var pingP = AccessTools.Property(clientType, "Ping");
                    if (pingF != null) ping = (int)(pingF.GetValue(inst) ?? 0);
                    else if (pingP != null) ping = (int)(pingP.GetValue(inst) ?? 0);
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
                var meetingType = AccessTools.TypeByName("MeetingHud");
                var instProp = meetingType?.GetProperty("Instance");
                var meeting = instProp?.GetValue(null);
                if (meeting == null || MeetingVoteRevealerPatch.LiveVotes.Count == 0) return;

                InitStyles();
                Rect hudRect = new Rect(10, 40, 240, 25 + (MeetingVoteRevealerPatch.LiveVotes.Count * 20));
                GUI.Box(hudRect, GUIContent.none, _boxStyle);
                GUI.Label(new Rect(hudRect.x + 8, hudRect.y + 4, hudRect.width - 16, 20), "🗳️ <b>Live Votes Tracker:</b>", _boldLabelStyle);

                int index = 0;
                var playerControlType = AccessTools.TypeByName("PlayerControl");
                var allPlayersField = AccessTools.Field(playerControlType, "AllPlayerControls");
                var allPlayersList = allPlayersField?.GetValue(null) as IEnumerable;

                string GetPlayerName(byte id)
                {
                    if (id == 254 || id == 255) return "Skipped Vote";
                    if (allPlayersList != null)
                    {
                        foreach (var p in allPlayersList)
                        {
                            var dataProp = AccessTools.Property(p.GetType(), "Data");
                            var data = dataProp?.GetValue(p);
                            if (data != null)
                            {
                                var pIdField = AccessTools.Field(data.GetType(), "PlayerId");
                                byte curId = (byte)(pIdField?.GetValue(data) ?? 255);
                                if (curId == id)
                                {
                                    var nameField = AccessTools.Field(data.GetType(), "PlayerName");
                                    return (string)(nameField?.GetValue(data) ?? $"Player {id}");
                                }
                            }
                        }
                    }
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

        public static void TriggerMedBayScan()
        {
            try
            {
                var localPlayerProp = AccessTools.Property(AccessTools.TypeByName("PlayerControl"), "LocalPlayer");
                var localPlayer = localPlayerProp?.GetValue(null);
                if (localPlayer == null) return;

                var setScannerMethod = AccessTools.Method(localPlayer.GetType(), "SetScanner") ?? AccessTools.Method(localPlayer.GetType(), "RpcSetScanner");
                setScannerMethod?.Invoke(localPlayer, new object[] { true });
                TriggerToast("MedBay Scan Activated Anywhere!");
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

            _windowRect = GUI.Window(0, _windowRect, (GUI.WindowFunction)DrawWindow, "AUGL Menu v1.9.21 (Ultimate Edition)", _windowStyle);
        }

        private static void DrawWindow(int id)
        {
            GUI.DragWindow(new Rect(0, 0, 780, 25));

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("About", _buttonStyle, GUILayout.Width(95), GUILayout.Height(26))) SwitchTab(0);
            if (GUILayout.Button("Active Codes", _buttonStyle, GUILayout.Width(95), GUILayout.Height(26))) SwitchTab(1);
            if (GUILayout.Button("Glitched Codes", _buttonStyle, GUILayout.Width(100), GUILayout.Height(26))) SwitchTab(2);
            if (GUILayout.Button("Unlockers", _buttonStyle, GUILayout.Width(90), GUILayout.Height(26))) SwitchTab(3);
            if (GUILayout.Button("Host & Modes", _buttonStyle, GUILayout.Width(105), GUILayout.Height(26))) SwitchTab(4);
            if (GUILayout.Button("Fun AddOns", _buttonStyle, GUILayout.Width(95), GUILayout.Height(26))) SwitchTab(5);
            if (GUILayout.Button("Troll", _buttonStyle, GUILayout.Width(75), GUILayout.Height(26))) SwitchTab(6);
            if (GUILayout.Button("Docs", _buttonStyle, GUILayout.Width(65), GUILayout.Height(26))) SwitchTab(7);
            GUILayout.EndHorizontal();
            GUILayout.Space(10);

            try
            {
                _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Width(750), GUILayout.Height(420));
                switch (_tab)
                {
                    case 0: DrawAbout(); break;
                    case 1: DrawCodes(false); break;
                    case 2: DrawCodes(true); break;
                    case 3: DrawUnlocker(); break;
                    case 4: DrawHostAndModes(); break;
                    case 5: DrawFunAddOns(); break;
                    case 6: DrawTrollTab(); break;
                    case 7: DrawDocs(); break;
                }
            }
            catch (Exception ex)
            {
                AUGLModPlugin.Log?.LogWarning($"Tab drawing exception: {ex.Message}");
            }
            finally
            {
                GUILayout.EndScrollView();
            }
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
            GUILayout.Label("Version: 01092026.v21 (Ultimate Edition)", _labelStyle);
            GUILayout.Label("Version Date: 01/09/2026", _labelStyle);
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
            
            if (GUILayout.Button("Refresh Codes Now", _buttonStyle, GUILayout.Width(150), GUILayout.Height(24)))
            {
                _ = FetchCodes();
            }

            Rect eggRect = new Rect(730 - 60, 520 - 50, 50, 20);
            if (GUI.Button(eggRect, _eggClicks >= _eggTexts.Length ? "🐱" : _eggTexts[_eggClicks], _buttonStyle))
            {
                _eggClicks = Mathf.Min(_eggClicks + 1, _eggTexts.Length);
                if (_eggClicks == _eggTexts.Length) LoadEgg();
            }
            if (_eggClicks >= _eggTexts.Length && _eggTex) GUI.DrawTexture(new Rect(730 - 70, 520 - 80, 60, 60), _eggTex);
        }

        private static void LoadEgg()
        {
            try { _eggTex = new Texture2D(2, 2); _eggTex.LoadImage(Convert.FromBase64String(_eggB64)); } catch { }
        }

        private static void DrawCodes(bool glitchedOnly)
        {
            GUILayout.Label(glitchedOnly ? "Glitched Codes" : "Active Codes", _boldLabelStyle);
            GUILayout.Space(4);

            GUILayout.BeginHorizontal();
            GUILayout.Label("🔍 Search Code:", _labelStyle, GUILayout.Width(100));
            _searchQuery = GUILayout.TextField(_searchQuery, GUILayout.Width(180));
            if (GUILayout.Button("Clear", _buttonStyle, GUILayout.Width(60))) _searchQuery = "";
            GUILayout.EndHorizontal();
            GUILayout.Space(6);

            List<GlitchedCodeResponse> list = new List<GlitchedCodeResponse>();
            string query = (_searchQuery ?? "").Trim().ToUpper();

            foreach (var c in _codes)
            {
                if (glitchedOnly && !c.Glitched) continue;
                if (!glitchedOnly && c.Glitched) continue;
                if (!string.IsNullOrEmpty(query) && !c.Code.ToUpper().Contains(query)) continue;
                list.Add(c);
            }

            if (glitchedOnly && list.Count > 0)
            {
                if (GUILayout.Button("🚀 Auto-Hop / Join Random Glitched Lobby", _buttonStyle, GUILayout.Width(300), GUILayout.Height(28)))
                {
                    AutoJoinGlitchedLobby();
                }
                GUILayout.Space(6);
            }

            if (list.Count == 0)
            {
                if (glitchedOnly) GUILayout.Label("⚠ No glitched code was found, please try again later.", _boldLabelStyle);
                else GUILayout.Label("No active codes available. Try refreshing from About tab.", _labelStyle);
                return;
            }

            float rowHeight = 34f;
            int firstVisible = Mathf.Max(0, (int)(_scroll.y / rowHeight));
            int visibleCount = (int)(400f / rowHeight) + 2;
            int lastVisible = Mathf.Min(list.Count, firstVisible + visibleCount);

            if (firstVisible > 0)
            {
                GUILayout.Space(firstVisible * rowHeight);
            }

            for (int i = firstVisible; i < lastVisible; i++)
            {
                var c = list[i];
                GUILayout.BeginHorizontal(_boxStyle, GUILayout.Height(30));
                if (c.Glitched) GUILayout.Label("[GLITCHED] " + c.Code, _glitchedLabelStyle, GUILayout.Width(250));
                else GUILayout.Label(c.Code, _labelStyle, GUILayout.Width(250));
                GUILayout.FlexibleSpace();
                GUILayout.Label("Port: " + c.Port, _labelStyle, GUILayout.Width(100));
                if (GUILayout.Button("Copy", _buttonStyle, GUILayout.Width(60), GUILayout.Height(22)))
                {
                    GUIUtility.systemCopyBuffer = c.Code;
                    TriggerToast($"Copied: {c.Code}");
                }
                GUILayout.EndHorizontal();
            }

            if (lastVisible < list.Count)
            {
                GUILayout.Space((list.Count - lastVisible) * rowHeight);
            }
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
            if (GUILayout.Button("Apply Host Options", _buttonStyle, GUILayout.Width(150), GUILayout.Height(26)))
            {
                HostQoLManager.ApplyHostSettings();
            }

            GUILayout.Space(16);
            GUILayout.Label("Remote Door Controls (Host-Only):", _boldLabelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Lock Cafeteria", _buttonStyle, GUILayout.Width(110), GUILayout.Height(24))) HostQoLManager.TriggerRemoteDoor(14, true);
            if (GUILayout.Button("Lock MedBay", _buttonStyle, GUILayout.Width(100), GUILayout.Height(24))) HostQoLManager.TriggerRemoteDoor(14, true);
            if (GUILayout.Button("Lock Electrical", _buttonStyle, GUILayout.Width(110), GUILayout.Height(24))) HostQoLManager.TriggerRemoteDoor(14, true);
            if (GUILayout.Button("Lock Storage", _buttonStyle, GUILayout.Width(100), GUILayout.Height(24))) HostQoLManager.TriggerRemoteDoor(14, true);
            GUILayout.EndHorizontal();

            GUILayout.Space(16);
            GUILayout.Label("Game Mode Presets", _boldLabelStyle);
            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Normal / Roles Galore", _buttonStyle, GUILayout.Width(160), GUILayout.Height(28))) GameModePresetManager.ApplyPreset("Normal");
            if (GUILayout.Button("SNS (Shift & Seek)", _buttonStyle, GUILayout.Width(160), GUILayout.Height(28))) GameModePresetManager.ApplyPreset("SNS");
            if (GUILayout.Button("Shields (Viper vs Angels)", _buttonStyle, GUILayout.Width(170), GUILayout.Height(28))) GameModePresetManager.ApplyPreset("Shields");
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
            if (GUILayout.Button("Teleport Out of Bounds", _buttonStyle, GUILayout.Width(180), GUILayout.Height(26))) ChatCommandsPatch.TeleportLocalPlayer(new Vector2(9999f, 9999f));
            if (GUILayout.Button("Teleport to Center", _buttonStyle, GUILayout.Width(150), GUILayout.Height(26))) ChatCommandsPatch.TeleportLocalPlayer(Vector2.zero);
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
                            "• <b>/autojoin</b> : Hop directly into an active glitched lobby\n" +
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
            if (GUILayout.Button("Inject AUGL Region Now", _buttonStyle, GUILayout.Width(180), GUILayout.Height(24)))
            {
                RegionInstaller.Inject();
            }
        }

        private static Texture2D MakeTex(Color col)
        {
            var t = new Texture2D(1, 1); t.SetPixel(0, 0, col); t.Apply(); return t;
        }
    }
}
