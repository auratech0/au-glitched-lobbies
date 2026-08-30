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
using Il2CppInterop.Runtime.Injections;
using UnityEngine;

namespace AUGL_Menu
{
    [BepInPlugin("com.augl.mod", "AUGL Menu", "01092026.v18.AUGLMod")]
    [BepInProcess("Among Us.exe")]
    public class AUGLModPlugin : BasePlugin
    {
        internal static ManualLogSource Log;
        private Harmony _harmony;

        public override void Load()
        {
            Log = base.Log;
            _harmony = new Harmony("com.augl.mod");
            _harmony.PatchAll();

            ClassInjector.RegisterTypeInIl2cpp<AUGLMenuController>();
            var go = new GameObject("AUGL_Menu_Root");
            go.AddComponent<AUGLMenuController>();
            UnityEngine.Object.DontDestroyOnLoad(go);

            Log.LogInfo("AUGL Menu loaded");
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
            var method = AccessTools.Method(type, "Serialize");
            _harmony = new Harmony("com.augl.spoof");
            _harmony.Patch(method, prefix: new HarmonyMethod(typeof(PlatformSpoofManager).GetMethod("Prefix")));
            _active = true;
        }

        public static void Disable()
        {
            if (!_active) return;
            _harmony?.UnpatchAll("com.augl.spoof");
            _active = false;
        }

        private static void Prefix(Il2CppSystem.Object __instance)
        {
            var field = AccessTools.Field(__instance.GetType(), "Platform");
            if (field != null) field.SetValue(__instance, (int)10); // Playstation = 10
        }
    }

    // ================= Region Installer =================
    public static class RegionInstaller
    {
        // Full region JSON (AUGL Codes + official + modded)
        public const string RegionJson = @"{
            ""CurrentRegionIdx"": 1,
            ""Regions"": [
                { ""$type"": ""StaticHttpRegionInfo, Assembly-CSharp"", ""Name"": ""AUGL Codes"", ""PingServer"": ""augl.net"", ""Servers"": [ { ""Name"": ""Http-1"", ""Ip"": ""augl.net"", ""Port"": 443, ""UseDtls"": false } ], ""TargetServer"": null, ""TranslateName"": 1003 },
                { ""$type"": ""StaticHttpRegionInfo, Assembly-CSharp"", ""Name"": ""North America"", ""PingServer"": ""matchmaker.among.us"", ""Servers"": [ { ""Name"": ""Http-1"", ""Ip"": ""https://matchmaker.among.us"", ""Port"": 443, ""UseDtls"": false } ], ""TargetServer"": null, ""TranslateName"": 289 },
                { ""$type"": ""StaticHttpRegionInfo, Assembly-CSharp"", ""Name"": ""Europe"", ""PingServer"": ""matchmaker-eu.among.us"", ""Servers"": [ { ""Name"": ""Http-1"", ""Ip"": ""https://matchmaker-eu.among.us"", ""Port"": 443, ""UseDtls"": false } ], ""TargetServer"": null, ""TranslateName"": 290 },
                { ""$type"": ""StaticHttpRegionInfo, Assembly-CSharp"", ""Name"": ""Asia"", ""PingServer"": ""matchmaker-as.among.us"", ""Servers"": [ { ""Name"": ""Http-1"", ""Ip"": ""https://matchmaker-as.among.us"", ""Port"": 443, ""UseDtls"": false } ], ""TargetServer"": null, ""TranslateName"": 291 },
                { ""$type"": ""StaticHttpRegionInfo, Assembly-CSharp"", ""Name"": ""Modded EU (MEU)"", ""PingServer"": ""https://au-eu.duikbo.at"", ""Servers"": [ { ""Name"": ""Http-1"", ""Ip"": ""https://au-eu.duikbo.at"", ""Port"": 443, ""UseDtls"": false } ], ""TargetServer"": null, ""TranslateName"": 1003 },
                { ""$type"": ""StaticHttpRegionInfo, Assembly-CSharp"", ""Name"": ""Modded NA (MNA)"", ""PingServer"": ""https://aumods.org"", ""Servers"": [ { ""Name"": ""Http-1"", ""Ip"": ""https://aumods.org"", ""Port"": 443, ""UseDtls"": false } ], ""TargetServer"": null, ""TranslateName"": 1003 },
                { ""$type"": ""StaticHttpRegionInfo, Assembly-CSharp"", ""Name"": ""Modded Asia (MAS)"", ""PingServer"": ""https://au-as.duikbo.at"", ""Servers"": [ { ""Name"": ""Http-1"", ""Ip"": ""https://au-as.duikbo.at"", ""Port"": 443, ""UseDtls"": false } ], ""TargetServer"": null, ""TranslateName"": 1003 }
            ]
        }";

        public static bool Enabled = true;

        public static void Inject()
        {
            if (!Enabled) return;
            try
            {
                var smType = AccessTools.TypeByName("ServerManager");
                var instanceProp = smType.GetProperty("Instance");
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
                            addMethod.Invoke(servers, new[] { server });
                        }
                        serversField.SetValue(newRegion, servers);
                    }
                    list.Add(newRegion);
                }
                AUGLModPlugin.Log.LogInfo("Regions injected");
            }
            catch (Exception ex) { AUGLModPlugin.Log.LogError($"Region error: {ex.Message}"); }
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
        public static float AngelDuration = 900f;

        public static void ApplyKillCd(float cd) { KillCooldown = cd; ShouldApply = true; }
        public static void ApplyAngel(float d) { AngelDuration = d; ShouldApply = true; }
        public static void Reset() { ShouldApply = false; KillCooldown = 0f; AngelDuration = 900f; }
    }

    [HarmonyPatch]
    public static class GameOptionsPatch
    {
        static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("GameOptionsData");
            return AccessTools.Method(type, "ToBytes");
        }

        static void Postfix(Il2CppSystem.Object __instance)
        {
            if (!UnlockerManager.ShouldApply) return;
            var clientType = AccessTools.TypeByName("AmongUsClient");
            var instanceProp = clientType.GetProperty("Instance");
            var client = instanceProp?.GetValue(null);
            if (client == null) return;
            var amHostProp = clientType.GetProperty("AmHost");
            bool amHost = (bool)amHostProp?.GetValue(client);
            var isPublicField = AccessTools.Field(clientType, "_IsGamePublic_k__BackingField");
            bool isPublic = (bool)isPublicField?.GetValue(client);
            if (!amHost || isPublic) return;

            var setFloat = AccessTools.Method(__instance.GetType(), "SetFloat", new Type[] { typeof(int), typeof(float) });
            setFloat?.Invoke(__instance, new object[] { (int)1, UnlockerManager.KillCooldown });
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
            var instanceProp = clientType.GetProperty("Instance");
            var client = instanceProp?.GetValue(null);
            if (client == null) return;
            var amHostProp = clientType.GetProperty("AmHost");
            bool amHost = (bool)amHostProp?.GetValue(client);
            var isPublicField = AccessTools.Field(clientType, "_IsGamePublic_k__BackingField");
            bool isPublic = (bool)isPublicField?.GetValue(client);
            if (!amHost || isPublic) return;

            var gomType = AccessTools.TypeByName("GameOptionsManager");
            var gomInstanceProp = gomType.GetProperty("Instance");
            var gom = gomInstanceProp?.GetValue(null);
            var currentOptionsProp = gomType.GetProperty("CurrentGameOptions");
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

    [HarmonyPatch]
    public static class SabotageBlockPatch
    {
        static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("ShipStatus");
            return AccessTools.Method(type, "RpcUpdateSystem");
        }

        static bool Prefix(Il2CppSystem.Object __instance, ref Il2CppSystem.Object systemType, ref byte amount)
        {
            if (!GameModePresetManager.SabotageBlocking) return true;
            int sysType = (int)systemType;
            if (sysType != 14) return false;
            return true;
        }
    }

    [HarmonyPatch]
    public static class DoorBlockPatch
    {
        static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("ShipStatus");
            return AccessTools.Method(type, "RpcCloseDoorsOfType");
        }

        static bool Prefix()
        {
            return !GameModePresetManager.SabotageBlocking;
        }
    }

    // ================= GUI Controller =================
    public class AUGLMenuController : MonoBehaviour
    {
        private bool _open = false;
        private int _tab = 0;
        private Vector2 _scroll = Vector2.zero;
        private List<GlitchedCodeResponse> _codes = new List<GlitchedCodeResponse>();
        private string _status = "Loading...";
        private string _killCdInput = "0";
        private string _angelInput = "900";
        private bool _spoofToggle;
        private bool _regionToggle = true;
        private int _eggClicks;
        private string[] _eggTexts = { "sus", "hi", "meow", "prrr", "S- Senpai", "please dont" };
        private Texture2D _eggTex;
        // Full base64 for the troll-face cart (paste here)
        private const string EggB64 = "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";

        void Start()
        {
            Task.Run(LoadCodes);
            RegionInstaller.Inject();
        }

        async Task LoadCodes()
        {
            _status = "Fetching...";
            _codes = await AUGLApiClient.FetchCodesAsync();
            _status = $"Loaded {_codes.Count} codes";
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F7))
                _open = !_open;
        }

        void OnGUI()
        {
            if (!_open) return;
            GUI.skin.window.normal.background = MakeTex(new Color(0.07f, 0.07f, 0.09f));
            GUI.skin.window.normal.textColor = Color.white;
            GUI.skin.button.normal.background = MakeTex(new Color(0.12f, 0.12f, 0.155f));
            GUI.skin.button.normal.textColor = Color.white;
            GUI.skin.button.hover.background = MakeTex(new Color(0.22f, 0.22f, 0.3f));
            GUI.skin.button.hover.textColor = Color.green;
            GUI.skin.label.normal.textColor = Color.white;

            GUI.Window(0, new Rect((Screen.width - 750) / 2, (Screen.height - 500) / 2, 750, 500), DrawWindow, "AUGL Menu");
        }

        void DrawWindow(int id)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("About", GUILayout.Width(110))) _tab = 0;
            if (GUILayout.Button("Active Codes", GUILayout.Width(110))) _tab = 1;
            if (GUILayout.Button("Glitched Codes", GUILayout.Width(110))) _tab = 2;
            if (GUILayout.Button("UnlockerS", GUILayout.Width(110))) _tab = 3;
            if (GUILayout.Button("Game Modes", GUILayout.Width(110))) _tab = 4;
            if (GUILayout.Button("Docs & Extras", GUILayout.Width(110))) _tab = 5;
            GUILayout.EndHorizontal();
            GUILayout.Space(10);

            switch (_tab)
            {
                case 0: DrawAbout(); break;
                case 1: DrawCodes(false); break;
                case 2: DrawCodes(true); break;
                case 3: DrawUnlocker(); break;
                case 4: DrawGameModes(); break;
                case 5: DrawDocs(); break;
            }
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        void DrawAbout()
        {
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Width(700), GUILayout.Height(400));
            GUILayout.Label("<b>AUGL Menu</b>");
            GUILayout.Label("Version: 01092026.v18");
            GUILayout.Label("Version Date: 01/09/2026");
            GUILayout.Label("Creators: sparxist (original), auratech0 (menu)");
            GUILayout.Label("Discord: discord.gg/HeNGYArCkY (AUGL), discord.gg/rj3fWwrc8Q (Tech Lounge)");
            GUILayout.Label("GitHub: github.com/auratech0/au-glitched-lobbies");
            GUILayout.Label("Website: augl.net");
            GUILayout.Label($"Status: {_status}");
            GUILayout.EndScrollView();

            Rect eggRect = new Rect(700 - 60, 500 - 40, 50, 20);
            if (GUI.Button(eggRect, _eggClicks >= _eggTexts.Length ? "🐱" : _eggTexts[_eggClicks]))
            {
                _eggClicks = Mathf.Min(_eggClicks + 1, _eggTexts.Length);
                if (_eggClicks == _eggTexts.Length) LoadEgg();
            }
            if (_eggClicks >= _eggTexts.Length && _eggTex) GUI.DrawTexture(new Rect(700 - 70, 500 - 70, 60, 60), _eggTex);
        }

        void LoadEgg()
        {
            try { _eggTex = new Texture2D(2, 2); _eggTex.LoadImage(Convert.FromBase64String(EggB64)); } catch { }
        }

        void DrawCodes(bool glitchedOnly)
        {
            GUILayout.Label(glitchedOnly ? "<b>Glitched Codes</b>" : "<b>Active Codes</b>");
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Width(700), GUILayout.Height(400));
            bool any = false;
            foreach (var c in _codes)
            {
                if (glitchedOnly && !c.Glitched) continue;
                if (!glitchedOnly && c.Glitched) continue;
                any = true;
                GUILayout.BeginHorizontal(GUI.skin.box);
                if (c.Glitched) GUILayout.Label("[GLITCHED] " + c.Code, new GUIStyle(GUI.skin.label) { normal = new GUIStyleState { textColor = new Color(0, 1, 0.33f) }, fontStyle = FontStyle.Bold });
                else GUILayout.Label(c.Code);
                GUILayout.FlexibleSpace();
                GUILayout.Label("Port: " + c.Port);
                if (GUILayout.Button("Copy", GUILayout.Width(60))) GUIUtility.systemCopyBuffer = c.Code;
                GUILayout.EndHorizontal();
            }
            if (!any)
            {
                if (glitchedOnly) GUILayout.Label("⚠ No glitched code was found, please try again later.", new GUIStyle(GUI.skin.label) { normal = new GUIStyleState { textColor = Color.yellow } });
                else GUILayout.Label("No active codes available. Try refreshing from About tab.");
            }
            GUILayout.EndScrollView();
        }

        void DrawUnlocker()
        {
            GUILayout.Label("<b>UnlockerS</b>");
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Width(700), GUILayout.Height(400));
            GUILayout.Label("PlayStation Spoofing:");
            _spoofToggle = GUILayout.Toggle(_spoofToggle, "Enable");
            if (_spoofToggle != PlatformSpoofManager.IsActive) { if (_spoofToggle) PlatformSpoofManager.Enable(); else PlatformSpoofManager.Disable(); }
            if (GUILayout.Button("Revert")) { _spoofToggle = false; PlatformSpoofManager.Disable(); }
            GUILayout.Space(10);
            GUILayout.Label("Kill Cooldown (seconds):");
            _killCdInput = GUILayout.TextField(_killCdInput, GUILayout.Width(100));
            GUILayout.Label("Angel Protection Duration (seconds):");
            _angelInput = GUILayout.TextField(_angelInput, GUILayout.Width(100));
            if (GUILayout.Button("Apply") && float.TryParse(_killCdInput, out float cd)) UnlockerManager.ApplyKillCd(cd);
            if (GUILayout.Button("Apply Angel") && float.TryParse(_angelInput, out float d)) UnlockerManager.ApplyAngel(d);
            if (GUILayout.Button("Reset")) UnlockerManager.Reset();
            GUILayout.EndScrollView();
        }

        void DrawGameModes()
        {
            GUILayout.Label("<b>Game Modes</b>");
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Width(700), GUILayout.Height(400));
            if (GUILayout.Button("Normal")) GameModePresetManager.ApplyPreset("Normal");
            if (GUILayout.Button("SNS")) GameModePresetManager.ApplyPreset("SNS");
            if (GUILayout.Button("Shields")) GameModePresetManager.ApplyPreset("Shields");
            GUILayout.EndScrollView();
        }

        void DrawDocs()
        {
            GUILayout.Label("<b>Documentation & Extras</b>");
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Width(700), GUILayout.Height(400));
            GUILayout.Label("Glitched lobby info...");
            GUILayout.Space(5);
            _regionToggle = GUILayout.Toggle(_regionToggle, "Enable Region Install");
            RegionInstaller.Enabled = _regionToggle;
            if (GUILayout.Button("Inject Now")) RegionInstaller.Inject();
            GUILayout.EndScrollView();
        }

        Texture2D MakeTex(Color col)
        {
            var t = new Texture2D(1, 1); t.SetPixel(0, 0, col); t.Apply(); return t;
        }
    }
}
