using BepInEx;
using BepInEx.Configuration;
using System.Collections;
using UnityEngine;

namespace TabgInstaller.TestMod
{
    [BepInPlugin("tabginstaller.testmod", "Test Mod", "1.0.0")]
    public class TestModPlugin : BaseUnityPlugin
    {
        private ConfigEntry<string> _message;
        private ConfigEntry<float> _interval;
        private ConfigEntry<float> _gravityMult;
        private ConfigEntry<float> _timeScale;

        private void Awake()
        {
            _message = Config.Bind("General", "Message", "Test mod loaded!", "Message written to server log periodically");
            _interval = Config.Bind("General", "IntervalSeconds", 15f, "Seconds between messages");
            _gravityMult = Config.Bind("Gameplay", "GravityMultiplier", 0.5f, "Multiply world gravity by this factor (e.g., 0.5 = half gravity)");
            _timeScale = Config.Bind("Gameplay", "TimeScale", 0.5f, "Multiply game time scale (0.5 = slow motion)");

            Logger.LogInfo($"TestMod initialized. Message='{_message.Value}', Interval={_interval.Value}s");

            Time.timeScale = _timeScale.Value;
            Logger.LogInfo($"Time scale set to {_timeScale.Value}");

            StartCoroutine(Loop());
        }

        private IEnumerator Loop()
        {
            while (true)
            {
                Logger.LogInfo($"[TestMod] {_message.Value}");
                yield return new WaitForSeconds(_interval.Value);
            }
        }
    }
} 