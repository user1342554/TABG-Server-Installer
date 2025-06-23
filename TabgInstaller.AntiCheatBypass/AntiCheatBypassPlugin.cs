using System;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace TabgInstaller.AntiCheatBypass
{
    // The GUID can be any unique string. Keep it stable between versions.
    [BepInPlugin("tabginstaller.anticheatbypass", "TABG Anti-Cheat Bypass", "1.0.0")]
    public class AntiCheatBypassPlugin : BaseUnityPlugin
    {
        private Harmony _harmony;
        private bool _patchedEasyAc;
        private bool _patchedEos;

        private void Awake()
        {
            _harmony = new Harmony("tabginstaller.anticheatbypass");
            // Some assemblies may already be loaded at this point. Attempt immediate patch.
            TryPatchExisting();

            // Hook into further AssemblyLoad events in case the relevant assemblies are loaded later.
            AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
            Logger.LogInfo("[AntiCheatBypass] Registered AssemblyLoad hook");
        }

        private void OnDestroy()
        {
            // Clean up event handler when plugin is destroyed (e.g., on game exit)
            AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
        }

        private void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            TryPatchAssembly(args.LoadedAssembly);
        }

        private void TryPatchExisting()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                TryPatchAssembly(asm);
            }
        }

        private void TryPatchAssembly(Assembly asm)
        {
            // 1) Easy_AC_Server.InitEpic -----------------------------------------------------
            if (!_patchedEasyAc)
            {
                var easyAcType = asm.GetType("Easy_AC_Server");
                if (easyAcType != null)
                {
                    // There should only be one InitEpic, but play safe and patch *all* we find.
                    foreach (var mi in easyAcType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
                    {
                        if (mi.Name == "InitEpic")
                        {
                            _harmony.Patch(mi, prefix: new HarmonyMethod(typeof(AntiCheatBypassPlugin).GetMethod(nameof(SkipEasyAc))));
                            _patchedEasyAc = true;
                            Logger.LogInfo("[AntiCheatBypass] Patched Easy_AC_Server.InitEpic (" + mi + ")");
                        }
                    }
                }
            }

            // 2) Epic.OnlineServices.Platform.PlatformInterface.Initialize ------------------
            if (!_patchedEos)
            {
                var eosPiType = asm.GetType("Epic.OnlineServices.Platform.PlatformInterface");
                if (eosPiType != null)
                {
                    foreach (var mi in eosPiType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                    {
                        if (mi.Name == "Initialize")
                        {
                            _harmony.Patch(mi, prefix: new HarmonyMethod(typeof(AntiCheatBypassPlugin).GetMethod(nameof(SkipEosInit))));
                            _patchedEos = true;
                            Logger.LogInfo("[AntiCheatBypass] Patched EOS PlatformInterface.Initialize (" + mi + ")");
                        }
                    }
                }
            }
        }

        // The methods referenced by HarmonyMethod must be static and return bool.
        public static bool SkipEasyAc()
        {
            Debug.Log("[AntiCheatBypass] Suppressed Easy_AC_Server.InitEpic()");
            return false; // Skip original
        }

        public static bool SkipEosInit(ref object __options)
        {
            Debug.Log("[AntiCheatBypass] Suppressed EOS PlatformInterface.Initialize()");
            return false; // Skip original implementation completely
        }
    }
} 