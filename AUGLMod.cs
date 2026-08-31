using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.InteropTypes;
using BepInEx.Unity.IL2CPP;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace AUGLMod
{
    [BepInPlugin("com.al3x4nderr.auglmod", "AUGL Menu", "1.9.18")]
    [BepInProcess("Among Us.exe")]
    public class AUGLModPlugin : BasePlugin
    {
        public static new BepInEx.Logging.ManualLogSource Log;

        public override void Load()
        {
            Log = base.Log;

            // 1. Register custom MonoBehaviour into IL2CPP domain & instantiate persistent GameObject
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

            // 2. Safe Harmony Patching
            var harmony = new Harmony("com.al3x4nderr.auglmod");
            
            GameOptionsPatch.ApplySafe(harmony);
            SabotageBlockPatch.ApplySafe(harmony);
            DoorBlockPatch.ApplySafe(harmony);

            // 3. Fetch online codes asynchronously
            _ = AUGLMenuGUI.FetchCodes();

            Log.LogInfo("AUGL Mod loaded successfully with scoped patches!");
        }
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
            return JsonSerializer.Deserialize<List<GlitchedCodeResponse>>(
                await Client.GetStringAsync(Endpoint), opts) ?? new List<GlitchedCodeResponse>();
        }
        catch { return new List<GlitchedCodeResponse>(); }
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
        var type = AccessTools.TypeByName("PlatformSpecificData");
        if (type == null) return;
        var method = AccessTools.Method(type, "Serialize");
        if (method == null) return;
        _harmony = new Harmony("com.augl.spoof");
        _harmony.Patch(method, prefix: new HarmonyMethod(typeof(PlatformSpoofManager).GetMethod("Prefix", BindingFlags.NonPublic | BindingFlags.Static)));
        _active = true;
    }

    public static void Disable()
    {
        if (!_active) return;
        _harmony?.UnpatchSelf();
        _active = false;
    }

    private static void Prefix(Il2CppSystem.Object __instance)
    {
        if (__instance == null) return;
        var field = AccessTools.Field(__instance.GetType(), "Platform") ?? AccessTools.Field(__instance.GetType(), "platform");
        if (field != null) field.SetValue(__instance, (int)10); // Playstation = 10
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
            AUGLMod.AUGLModPlugin.Log?.LogInfo("Regions injected");
        }
        catch (Exception ex) { AUGLMod.AUGLModPlugin.Log?.LogError($"Region error: {ex.Message}"); }
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

            // 1. SetFloat / SetInt across all possible option IDs
            var setFloat = AccessTools.Method(optType, "SetFloat", new Type[] { typeof(int), typeof(float) })
                        ?? AccessTools.Method(optType, "SetFloat");

            var setInt = AccessTools.Method(optType, "SetInt", new Type[] { typeof(int), typeof(int) })
                      ?? AccessTools.Method(optType, "SetInt");

            if (setFloat != null)
            {
                // Kill Cooldown (Option Key 1)
                setFloat.Invoke(options, new object[] { 1, KillCooldown });

                // Guardian Angel Protection Duration (Option Key 1100 / 0x44C)
                setFloat.Invoke(options, new object[] { 1100, AngelDuration });
                setFloat.Invoke(options, new object[] { 0x44C, AngelDuration });

                // Guardian Angel Cooldown (Option Keys: 1098, 1099, 1101, 1096, 1097, 0x44A, 0x44B, 0x44D)
                int[] angelCdKeys = new int[] { 1098, 1099, 1101, 1096, 1097, 0x44A, 0x44B, 0x44D };
                foreach (int key in angelCdKeys)
                {
                    setFloat.Invoke(options, new object[] { key, AngelCooldown });
                    setInt?.Invoke(options, new object[] { key, (int)AngelCooldown });
                }
            }

            // 2. Direct property scan for Angel Cooldown
            foreach (var prop in optType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                string pName = prop.Name.ToLower();
                if ((pName.Contains("angel") || pName.Contains("guardian")) && pName.Contains("cooldown"))
                {
                    if (prop.PropertyType == typeof(float)) prop.SetValue(options, AngelCooldown);
                    else if (prop.PropertyType == typeof(int)) prop.SetValue(options, (int)AngelCooldown);
                }
            }

            // 3. Direct field scan for Angel Cooldown
            foreach (var field in optType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                string fName = field.Name.ToLower();
                if ((fName.Contains("angel") || fName.Contains("guardian")) && fName.Contains("cooldown"))
                {
                    if (field.FieldType == typeof(float)) field.SetValue(options, AngelCooldown);
                    else if (field.FieldType == typeof(int)) field.SetValue(options, (int)AngelCooldown);
                }
            }

            AUGLMod.AUGLModPlugin.Log?.LogInfo($"Unlocked options synced: KillCD={KillCooldown}s, AngelDur={AngelDuration}s, AngelCD={AngelCooldown}s");

            var syncMethod = AccessTools.Method(gomType, "SyncGameOptions") ?? AccessTools.Method(gomType, "Dirty");
            syncMethod?.Invoke(gom, null);
        }
        catch (Exception ex)
        {
            AUGLMod.AUGLModPlugin.Log?.LogWarning($"SyncWithGame exception: {ex.Message}");
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
                        AUGLMod.AUGLModPlugin.Log?.LogInfo($"GameOptionsPatch successfully hooked {name}.ToBytes!");
                        hooked = true;
                        break;
                    }
                }
            }

            if (!hooked)
            {
                AUGLMod.AUGLModPlugin.Log?.LogWarning("GameOptionsPatch: ToBytes method not found across candidate types.");
            }
        }
        catch (Exception ex)
        {
            AUGLMod.AUGLModPlugin.Log?.LogWarning($"GameOptionsPatch error: {ex.Message}");
        }
    }

    private static void Postfix(Il2CppSystem.Object __instance)
    {
        if (!UnlockerManager.ShouldApply) return;
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
}

// ================= Game Modes =================
public static class GameModePresetManager
{
    public static bool SabotageBlocking = false;

    public static void ApplyPreset(string name)
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

        switch (name)
        {
            case "Normal":
                SabotageBlocking = false;
                setInt?.Invoke(options, new object[] { (int)1, 1 });
                setFloat?.Invoke(options, new object[] { (int)1, 10f });
                break;
            case "SNS":
                SabotageBlocking = true;
                setInt?.Invoke(options, new object[] { (int)1, 2 });
                setFloat?.Invoke(options, new object[] { (int)1, 12.5f });
                break;
            case "Shields":
                SabotageBlocking = false;
                setInt?.Invoke(options, new object[] { (int)1, 1 });
                setFloat?.Invoke(options, new object[] { (int)1, 0f });
                break;
        }
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
                AUGLMod.AUGLModPlugin.Log?.LogInfo("SabotageBlockPatch applied.");
            }
        }
        catch (Exception ex) { AUGLMod.AUGLModPlugin.Log?.LogWarning($"SabotageBlockPatch error: {ex.Message}"); }
    }

    private static bool Prefix(Il2CppSystem.Object __instance, ref Il2CppSystem.Object systemType, ref byte amount)
    {
        if (!GameModePresetManager.SabotageBlocking) return true;
        int sysType = systemType.Unbox<int>();
        if (sysType != 14) return false;
        return true;
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
                AUGLMod.AUGLModPlugin.Log?.LogInfo("DoorBlockPatch applied.");
            }
        }
        catch (Exception ex) { AUGLMod.AUGLModPlugin.Log?.LogWarning($"DoorBlockPatch error: {ex.Message}"); }
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

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F7))
        {
            AUGLMenuGUI.ToggleOpen();
        }
    }

    private void OnGUI()
    {
        try { AUGLMenuGUI.Draw(); }
        catch (Exception ex) { AUGLMod.AUGLModPlugin.Log?.LogWarning($"Draw error: {ex.Message}"); }
    }
}

// ================= Static GUI Class =================
public static class AUGLMenuGUI
{
    private static bool _open;
    private static int _tab;
    private static Vector2 _scroll;
    private static List<GlitchedCodeResponse> _codes = new List<GlitchedCodeResponse>();
    private static string _status = "Loading...";
    private static string _killCdInput = "0";
    private static string _angelInput = "30";
    private static string _angelCdInput = "0";
    private static string _focusedField = null;
    private static bool _spoofToggle;
    private static bool _regionToggle = true;
    private static int _eggClicks;
    private static string[] _eggTexts = { "sus", "hi", "meow", "prrr", "S- Senpai", "please dont" };
    private static Texture2D _eggTex;
    private static string _eggB64 = "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 unfamiliar=";

    // Persistent Window & Styling State
    private static Rect _windowRect = new Rect(200, 150, 750, 500);
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

    private static void InitStyles()
    {
        if (_initialized && _winBgTex != null && _btnNormTex != null && _boxBgTex != null && _activeTextFieldBgTex != null)
        {
            return;
        }

        if (Screen.width > 0 && Screen.height > 0)
        {
            _windowRect = new Rect((Screen.width - 750) / 2, (Screen.height - 500) / 2, 750, 500);
        }

        _winBgTex = MakeTex(new Color(0.08f, 0.08f, 0.11f, 0.96f));
        _boxBgTex = MakeTex(new Color(0.14f, 0.14f, 0.18f, 0.8f));
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

        _windowStyle = new GUIStyle(GUI.skin.window)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold
        };
        _windowStyle.normal.background = _winBgTex;
        _windowStyle.normal.textColor = Color.cyan;
        _windowStyle.onNormal.background = _winBgTex;
        _windowStyle.focused.background = _winBgTex;
        _windowStyle.active.background = _winBgTex;

        _buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 12
        };
        _buttonStyle.normal.background = _btnNormTex;
        _buttonStyle.normal.textColor = Color.white;
        _buttonStyle.hover.background = _btnHoverTex;
        _buttonStyle.hover.textColor = Color.green;
        _buttonStyle.active.background = _btnHoverTex;
        _buttonStyle.active.textColor = Color.yellow;

        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12
        };
        _labelStyle.normal.textColor = Color.white;

        _boldLabelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold
        };
        _boldLabelStyle.normal.textColor = Color.white;

        _glitchedLabelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold
        };
        _glitchedLabelStyle.normal.textColor = new Color(0f, 1f, 0.4f);

        _boxStyle = new GUIStyle(GUI.skin.box);
        _boxStyle.normal.background = _boxBgTex;

        _textFieldStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleLeft
        };
        _textFieldStyle.normal.background = _textFieldBgTex;
        _textFieldStyle.normal.textColor = Color.white;

        _activeTextFieldStyle = new GUIStyle(_textFieldStyle);
        _activeTextFieldStyle.normal.background = _activeTextFieldBgTex;
        _activeTextFieldStyle.normal.textColor = Color.yellow;

        _initialized = true;
    }

    public static void ToggleOpen()
    {
        _open = !_open;
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

    public static void Draw()
    {
        if (!_open) return;

        InitStyles();

        _windowRect = GUI.Window(0, _windowRect, (GUI.WindowFunction)DrawWindow, "AUGL Menu", _windowStyle);
    }

    private static void DrawWindow(int id)
    {
        GUI.DragWindow(new Rect(0, 0, 750, 25));

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("About", _buttonStyle, GUILayout.Width(110), GUILayout.Height(26))) SwitchTab(0);
        if (GUILayout.Button("Active Codes", _buttonStyle, GUILayout.Width(110), GUILayout.Height(26))) SwitchTab(1);
        if (GUILayout.Button("Glitched Codes", _buttonStyle, GUILayout.Width(110), GUILayout.Height(26))) SwitchTab(2);
        if (GUILayout.Button("UnlockerS", _buttonStyle, GUILayout.Width(110), GUILayout.Height(26))) SwitchTab(3);
        if (GUILayout.Button("Game Modes", _buttonStyle, GUILayout.Width(110), GUILayout.Height(26))) SwitchTab(4);
        if (GUILayout.Button("Docs & Extras", _buttonStyle, GUILayout.Width(110), GUILayout.Height(26))) SwitchTab(5);
        GUILayout.EndHorizontal();
        GUILayout.Space(10);

        try
        {
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Width(710), GUILayout.Height(400));
            switch (_tab)
            {
                case 0: DrawAbout(); break;
                case 1: DrawCodes(false); break;
                case 2: DrawCodes(true); break;
                case 3: DrawUnlocker(); break;
                case 4: DrawGameModes(); break;
                case 5: DrawDocs(); break;
            }
        }
        catch (Exception ex)
        {
            AUGLMod.AUGLModPlugin.Log?.LogWarning($"Tab drawing exception: {ex.Message}");
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
            Rect r = GUILayoutUtility.GetRect(120f, 22f, GUILayout.ExpandWidth(false));
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
        GUILayout.Label("Version: 01092026.v18", _labelStyle);
        GUILayout.Label("Version Date: 01/09/2026", _labelStyle);
        GUILayout.Label("Creators: sparxist (original), auratech0 (menu)", _labelStyle);
        GUILayout.Label("Discord: discord.gg/HeNGYArCkY (AUGL), discord.gg/rj3fWwrc8Q (Tech Lounge)", _labelStyle);
        GUILayout.Label("GitHub: github.com/auratech0/au-glitched-lobbies", _labelStyle);
        GUILayout.Label("Website: augl.net", _labelStyle);
        GUILayout.Label($"Status: {_status}", _labelStyle);
        
        if (GUILayout.Button("Refresh Codes", _buttonStyle, GUILayout.Width(120), GUILayout.Height(24)))
        {
            _ = FetchCodes();
        }

        Rect eggRect = new Rect(700 - 60, 500 - 40, 50, 20);
        if (GUI.Button(eggRect, _eggClicks >= _eggTexts.Length ? "🐱" : _eggTexts[_eggClicks], _buttonStyle))
        {
            _eggClicks = Mathf.Min(_eggClicks + 1, _eggTexts.Length);
            if (_eggClicks == _eggTexts.Length) LoadEgg();
        }
        if (_eggClicks >= _eggTexts.Length && _eggTex) GUI.DrawTexture(new Rect(700 - 70, 500 - 70, 60, 60), _eggTex);
    }

    private static void LoadEgg()
    {
        try { _eggTex = new Texture2D(2, 2); _eggTex.LoadImage(Convert.FromBase64String(_eggB64)); } catch { }
    }

    private static void DrawCodes(bool glitchedOnly)
    {
        GUILayout.Label(glitchedOnly ? "Glitched Codes" : "Active Codes", _boldLabelStyle);
        
        List<GlitchedCodeResponse> list = new List<GlitchedCodeResponse>();
        foreach (var c in _codes)
        {
            if (glitchedOnly && !c.Glitched) continue;
            if (!glitchedOnly && c.Glitched) continue;
            list.Add(c);
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
        GUILayout.Label("UnlockerS", _boldLabelStyle);
        GUILayout.Space(5);
        
        GUILayout.Label("PlayStation Spoofing:", _labelStyle);
        _spoofToggle = SafeToggle(_spoofToggle, "Enable");
        if (_spoofToggle != PlatformSpoofManager.IsActive) { if (_spoofToggle) PlatformSpoofManager.Enable(); else PlatformSpoofManager.Disable(); }
        if (GUILayout.Button("Revert", _buttonStyle, GUILayout.Width(100), GUILayout.Height(24))) { _spoofToggle = false; PlatformSpoofManager.Disable(); }

        GUILayout.Space(12);
        GUILayout.Label("Kill Cooldown (seconds):", _labelStyle);
        _killCdInput = DrawCustomInputField("killCd", _killCdInput, 180f);

        GUILayout.Space(6);
        if (GUILayout.Button("Apply", _buttonStyle, GUILayout.Width(100), GUILayout.Height(24)))
        {
            if (TryParseHugeFloat(_killCdInput, out float cd))
            {
                UnlockerManager.ApplyKillCd(cd);
            }
        }

        GUILayout.Space(14);
        GUILayout.Label("Angel Protection Duration (seconds):", _labelStyle);
        _angelInput = DrawCustomInputField("angelDur", _angelInput, 180f);

        GUILayout.Space(6);
        if (GUILayout.Button("Apply Angel", _buttonStyle, GUILayout.Width(100), GUILayout.Height(24)))
        {
            if (TryParseHugeFloat(_angelInput, out float d))
            {
                UnlockerManager.ApplyAngel(d);
            }
        }

        GUILayout.Space(14);
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
        if (GUILayout.Button("Reset", _buttonStyle, GUILayout.Width(100), GUILayout.Height(24)))
        {
            UnlockerManager.Reset();
            _killCdInput = "0";
            _angelInput = "30";
            _angelCdInput = "0";
        }
    }

    private static void DrawGameModes()
    {
        GUILayout.Label("Game Modes", _boldLabelStyle);
        GUILayout.Space(5);
        if (GUILayout.Button("Normal", _buttonStyle, GUILayout.Width(150), GUILayout.Height(30))) GameModePresetManager.ApplyPreset("Normal");
        GUILayout.Space(5);
        if (GUILayout.Button("SNS", _buttonStyle, GUILayout.Width(150), GUILayout.Height(30))) GameModePresetManager.ApplyPreset("SNS");
        GUILayout.Space(5);
        if (GUILayout.Button("Shields", _buttonStyle, GUILayout.Width(150), GUILayout.Height(30))) GameModePresetManager.ApplyPreset("Shields");
    }

    private static void DrawDocs()
    {
        GUILayout.Label("Documentation & Extras", _boldLabelStyle);
        GUILayout.Space(5);
        GUILayout.Label("Glitched lobby info...", _labelStyle);
        GUILayout.Space(5);
        _regionToggle = SafeToggle(_regionToggle, "Enable Region Install");
        RegionInstaller.Enabled = _regionToggle;
        if (GUILayout.Button("Inject Now", _buttonStyle, GUILayout.Width(100), GUILayout.Height(24))) RegionInstaller.Inject();
    }

    private static Texture2D MakeTex(Color col)
    {
        var t = new Texture2D(1, 1); t.SetPixel(0, 0, col); t.Apply(); return t;
    }
}