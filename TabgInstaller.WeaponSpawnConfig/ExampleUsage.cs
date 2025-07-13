using BepInEx;
using HarmonyLib;
using System.Reflection;

namespace TABGExampleModWithWeaponConfig
{
    [BepInPlugin("yourusername.tabg.weaponconfigexample", "TABG Weapon Config Example", "1.0")]
    public class WeaponConfigExample : BaseUnityPlugin
    {
        private object _weaponSpawnConfigPlugin;
        
        public void Awake()
        {
            // Find the WeaponSpawnConfig plugin
            var pluginType = FindWeaponSpawnConfigPlugin();
            
            if (pluginType != null)
            {
                Logger.LogInfo("Found WeaponSpawnConfig plugin!");
                
                // Example: Get spawn rate for AK47
                float ak47Rate = GetWeaponSpawnRate("AK47");
                Logger.LogInfo($"AK47 spawn rate multiplier: {ak47Rate}");
                
                // Example: Get category multiplier for SMGs
                float smgCategoryRate = GetCategoryMultiplier("SMGs");
                Logger.LogInfo($"SMG category multiplier: {smgCategoryRate}");
                
                // Example: Get final spawn rate for P90 (includes all multipliers)
                float p90FinalRate = GetFinalSpawnRate("P90", "SMGs");
                Logger.LogInfo($"P90 final spawn rate: {p90FinalRate}");
            }
            else
            {
                Logger.LogWarning("WeaponSpawnConfig plugin not found. Using default spawn rates.");
            }
        }
        
        private System.Type FindWeaponSpawnConfigPlugin()
        {
            // Look for the plugin in all loaded assemblies
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType("TabgInstaller.WeaponSpawnConfig.WeaponSpawnConfigPlugin");
                if (type != null)
                {
                    // Find the plugin instance
                    var instanceProperty = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    if (instanceProperty != null)
                    {
                        _weaponSpawnConfigPlugin = instanceProperty.GetValue(null);
                        return type;
                    }
                    
                    // Alternative: Find through BepInEx plugin manager
                    foreach (var plugin in BepInEx.Bootstrap.Chainloader.PluginInfos.Values)
                    {
                        if (plugin.Instance?.GetType() == type)
                        {
                            _weaponSpawnConfigPlugin = plugin.Instance;
                            return type;
                        }
                    }
                }
            }
            return null;
        }
        
        private float GetWeaponSpawnRate(string weaponName)
        {
            if (_weaponSpawnConfigPlugin == null) return 1.0f;
            
            var method = _weaponSpawnConfigPlugin.GetType().GetMethod("GetSpawnRateMultiplier");
            if (method != null)
            {
                return (float)method.Invoke(_weaponSpawnConfigPlugin, new object[] { weaponName });
            }
            return 1.0f;
        }
        
        private float GetCategoryMultiplier(string categoryName)
        {
            if (_weaponSpawnConfigPlugin == null) return 1.0f;
            
            var method = _weaponSpawnConfigPlugin.GetType().GetMethod("GetCategoryMultiplier");
            if (method != null)
            {
                return (float)method.Invoke(_weaponSpawnConfigPlugin, new object[] { categoryName });
            }
            return 1.0f;
        }
        
        private float GetFinalSpawnRate(string weaponName, string categoryName)
        {
            if (_weaponSpawnConfigPlugin == null) return 1.0f;
            
            var method = _weaponSpawnConfigPlugin.GetType().GetMethod("GetFinalSpawnRate");
            if (method != null)
            {
                return (float)method.Invoke(_weaponSpawnConfigPlugin, new object[] { weaponName, categoryName });
            }
            return 1.0f;
        }
        
        // Example of using the spawn rates in your own spawn logic
        private bool ShouldSpawnWeapon(string weaponName, string categoryName)
        {
            float spawnRate = GetFinalSpawnRate(weaponName, categoryName);
            
            // If rate is 0, never spawn
            if (spawnRate <= 0f) return false;
            
            // Otherwise, use the rate as a probability multiplier
            float randomValue = UnityEngine.Random.Range(0f, 1f);
            float adjustedChance = 0.1f * spawnRate; // Base 10% chance multiplied by rate
            
            return randomValue < adjustedChance;
        }
    }
} 