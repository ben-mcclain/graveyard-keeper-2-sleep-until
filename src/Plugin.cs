using System;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using FlowCanvas;
using GK2.FlowCanvasNodes;
using HarmonyLib;
using LazyBearTechnology;
using UnityEngine;
using UnityEngine.UI;

namespace SleepUntil
{
    [BepInPlugin(Id, "Sleep Until", "0.1.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Id = "mcclains.gk2.sleepuntil";
        internal static Plugin Instance;
        private Harmony harmony;
        private SleepMenu menu;
        private bool replaying;
        private int? armedWeekday;
        private bool armed;
        private ActiveSleep active;
        private ConfigEntry<KeyboardShortcut> wakeKey;
        private static readonly MethodInfo GoToSleep = AccessTools.Method(typeof(Flow_GoToSleep), "GoToSleep");
        private static readonly MethodInfo ResolveAnim = AccessTools.Method(typeof(Flow_GoToSleep), "ResolveAnimType");
        private static readonly FieldInfo Remaining = AccessTools.Field(typeof(EnergySystem), "remainingSleepTime");
        private static readonly FieldInfo FinishedPort = AccessTools.Field(typeof(Flow_GoToSleep), "onFinishedIfNoNeedToSleep");
        private static readonly FieldInfo SuccessfulPort = AccessTools.Field(typeof(Flow_GoToSleep), "onFinished");
        private static readonly FieldInfo OutPort = AccessTools.Field(typeof(Flow_GoToSleep), "out");

        private sealed class ActiveSleep
        {
            public EnergySystem Owner;
            public GameSave Save;
            public SleepSchedule Schedule;
            public Action OnFinished;
            public string Label;
        }

        private void Awake()
        {
            Instance = this;
            wakeKey = Config.Bind("Controls", "WakeEarly", new KeyboardShortcut(KeyCode.F8), "Wake early from scheduled sleep using the normal wake/save path.");
            try
            {
                if (GoToSleep == null || ResolveAnim == null || Remaining == null || FinishedPort == null || SuccessfulPort == null || OutPort == null)
                    throw new MissingMemberException("This game build has different sleep hooks.");
                harmony = new Harmony(Id);
                harmony.PatchAll(typeof(Plugin).Assembly);
                MainGame.OnGoToMainMenu += ResetSession;
                Logger.LogInfo("Sleep Until 0.1.0 loaded. Normal sleep is unchanged; scheduled sleep wakes at 06:00.");
            }
            catch (Exception ex)
            {
                harmony?.UnpatchSelf();
                Logger.LogError("Sleep Until disabled; vanilla sleep retained. " + ex);
                enabled = false;
            }
        }

        private bool OfferMenu(Flow_GoToSleep node, Flow flow)
        {
            if (replaying || !enabled || MainGame.Instance == null || MainGame.PlayerData == null ||
                node.sleepWithoutSavingGame || MainGame.PlayerData.energySystem.IsSleeping ||
                MainGame.PlayerData.energySystem.IsInTransitionBetweenSleep ||
                LazyWindowsStackController.HasAnyModalWindowOpened) return true;
            try
            {
                // Leave non-bed/scripted fades and story-only sleeps alone.
                var animation = (SleepAnimType)ResolveAnim.Invoke(node, null);
                if (animation != SleepAnimType.RegularBed && animation != SleepAnimType.StrawBed) return true;
                var env = EnvironmentEngine.Instance.Data;
                var save = MainGame.Instance.GameSave;
                var days = LazyConsts.ConstDefs.AllDays.Select(key => new
                {
                    Number = ConstDef.Get(key).IntValue,
                    Name = key.Substring("day_".Length)
                }).OrderBy(day => day.Number).ToArray();
                if (days.Length != 6 || days.Select(d => d.Number).Distinct().Count() != 6 || days.Any(d => d.Number < 1 || d.Number > 6))
                    throw new InvalidOperationException("Unrecognized weekday definitions.");
                string Title(string value) => char.ToUpperInvariant(value[0]) + value.Substring(1);
                string Estimate(int? weekday)
                {
                    double hours = SleepSchedule.PreviewDays(env.Day, env.TimeOfDay, env.GetDiffInDaysBetweenCurrentAndNext(), weekday) * 24;
                    int totalMinutes = (int)Math.Ceiling(hours * 60);
                    return $"about {totalMinutes / 60}h {totalMinutes % 60:00}m";
                }
                var labels = new[] { "Normal: usual sleep", "Till morning (6 a.m.): " + Estimate(null) }
                    .Concat(days.Select(d => $"Until {Title(d.Name)} morning: {Estimate(d.Number)}")).ToArray();
                if (menu == null)
                {
                    var go = new GameObject("Sleep Until Menu", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(GamepadNavigationController));
                    go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                    var blocker = new GameObject("Block background clicks", typeof(RectTransform), typeof(Image));
                    blocker.transform.SetParent(go.transform, false);
                    var rect = (RectTransform)blocker.transform;
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = rect.offsetMax = Vector2.zero;
                    blocker.GetComponent<Image>().color = Color.clear;
                    blocker.GetComponent<Image>().raycastTarget = true;
                    DontDestroyOnLoad(go);
                    menu = go.AddComponent<SleepMenu>();
                    menu.Init();
                }
                menu.Open(new SleepMenuData
                {
                    CurrentTime = $"Day {env.Day}, {Title(days.Single(d => d.Number == env.CurrentDayNumber).Name)}  {Clock(env.TimeOfDay)}",
                    Labels = labels,
                    Choose = choice =>
                    {
                        if (MainGame.Instance == null || MainGame.Instance.GameSave != save) return;
                        armed = choice != 0;
                        armedWeekday = choice >= 2 ? (int?)days[choice - 2].Number : null;
                        Replay(node, flow);
                    },
                    Cancel = () =>
                    {
                        if (MainGame.Instance == null || MainGame.Instance.GameSave != save) return;
                        // Same flow continuation used by a rejected vanilla sleep, without starting/saving.
                        try
                        {
                            var cancelled = (FlowOutput)FinishedPort.GetValue(node) ?? (FlowOutput)SuccessfulPort.GetValue(node);
                            cancelled?.Call(flow);
                            ((FlowOutput)OutPort.GetValue(node))?.Call(flow);
                        }
                        catch (Exception ex) { Logger.LogError("Cancel continuation failed: " + ex); }
                    }
                });
                return false;
            }
            catch (Exception ex)
            {
                menu?.Abandon();
                armed = false;
                Logger.LogError("Could not open sleep choices; using vanilla sleep. " + ex);
                return true;
            }
        }

        private void Replay(Flow_GoToSleep node, Flow flow)
        {
            replaying = true;
            try { GoToSleep.Invoke(node, new object[] { flow }); }
            catch (Exception ex) { ClearActive(); Logger.LogError("Bed interaction failed: " + ex); }
            finally { replaying = false; armed = false; }
        }

        private void Arm(EnergySystem owner, Action onFinished, ref bool sleepWithMaxEnergy)
        {
            ClearActive();
            if (!armed) return;
            var env = EnvironmentEngine.Instance.Data;
            active = new ActiveSleep
            {
                Owner = owner,
                Save = MainGame.Instance.GameSave,
                Schedule = new SleepSchedule(env.Day, env.TimeOfDay, armedWeekday),
                OnFinished = onFinished,
                Label = armedWeekday.HasValue ? "Sleeping until the selected weekday's morning" : "Sleeping until morning"
            };
            sleepWithMaxEnergy = true;
            Logger.LogInfo($"Scheduled sleep started on day {env.Day} at {Clock(env.TimeOfDay)}; target weekday {armedWeekday?.ToString() ?? "next dawn"}.");
        }

        private void Tick()
        {
            if (active == null) return;
            if (MainGame.Instance == null || MainGame.Instance.GameSave != active.Save) { ClearActive(); return; }
            if (!active.Owner.IsSleeping) return;
            try
            {
                var env = EnvironmentEngine.Instance.Data;
                if (active.Schedule.Observe(env.Day, env.TimeOfDay)) FinishSleep();
            }
            catch (Exception ex) { Logger.LogError("Scheduled sleep cancelled: " + ex); FinishSleep(); }
        }

        private void FinishSleep()
        {
            var sleep = active;
            if (sleep == null || !sleep.Owner.IsSleeping || sleep.Owner.IsInTransitionBetweenSleep) return;
            ClearActive(); // Clear BEFORE callbacks, which can start another sleep or change scenes.
            sleep.Owner.StopSleeping(); // Native fade, speed restoration, controls and save.
            try { sleep.OnFinished?.Invoke(); }
            finally { GlobalEventsSystem.FireTrigger(GlobalEventsSystem.Event.Type.AfterSleep); }
            Logger.LogInfo("Scheduled sleep finished via normal wake/save path.");
        }

        private void ClearActive()
        {
            if (active != null) Remaining?.SetValue(active.Owner, 0f);
            active = null;
        }

        private void ResetSession()
        {
            armed = false;
            ClearActive();
            menu?.Abandon();
        }

        private void Update()
        {
            if (active == null) return;
            if (MainGame.Instance == null || MainGame.Instance.GameSave != active.Save) { ResetSession(); return; }
            if (!active.Owner.IsSleeping && !active.Owner.IsInTransitionBetweenSleep) { ClearActive(); return; }
            if (active.Owner.IsSleeping && !MainGame.IsGamePaused && wakeKey.Value.IsDown()) FinishSleep();
        }

        private void OnGUI()
        {
            if (active == null || !active.Owner.IsSleeping || MainGame.IsGamePaused) return;
            var oldDepth = GUI.depth;
            GUI.depth = -900;
            float width = Mathf.Min(480, Screen.width - 20);
            GUI.Box(new Rect((Screen.width - width) / 2, 16, width, 78), active.Label + " (6 a.m.)");
            bool wake = GUI.Button(new Rect((Screen.width - 240) / 2, 49, 240, 32), "Wake now (" + wakeKey.Value + ")");
            GUI.depth = oldDepth;
            if (wake) FinishSleep();
        }

        private void OnDestroy()
        {
            MainGame.OnGoToMainMenu -= ResetSession;
            ResetSession();
            harmony?.UnpatchSelf();
            if (menu != null) Destroy(menu.gameObject);
            if (Instance == this) Instance = null;
        }

        private static string Clock(float value)
        {
            int minutes = Mathf.FloorToInt(value * 1440);
            return $"{minutes / 60:00}:{minutes % 60:00}";
        }

        [HarmonyPatch(typeof(Flow_GoToSleep), "GoToSleep")]
        private static class BedPatch
        {
            private static bool Prefix(Flow_GoToSleep __instance, Flow flow) => Instance == null || Instance.OfferMenu(__instance, flow);
        }

        [HarmonyPatch(typeof(EnergySystem), nameof(EnergySystem.StartSleeping))]
        private static class StartPatch
        {
            private static void Prefix(EnergySystem __instance, Action onSleepEndedByEnergyCallback, ref bool sleepWithMaxEnergy)
                => Instance?.Arm(__instance, onSleepEndedByEnergyCallback, ref sleepWithMaxEnergy);
        }

        [HarmonyPatch(typeof(EnergySystem), "RestoreEnergyWhileSleeping")]
        private static class HoldWakePatch
        {
            private static void Prefix(EnergySystem __instance, ref float ___remainingSleepTime)
            {
                if (Instance?.active?.Owner == __instance) ___remainingSleepTime = float.MaxValue;
            }
        }

        [HarmonyPatch(typeof(EnvironmentEngine), nameof(EnvironmentEngine.CustomUpdate))]
        private static class ClockPatch
        {
            // After the COMPLETE tick: midnight rollover, vendors and day events already processed.
            private static void Postfix() => Instance?.Tick();
        }

        [HarmonyPatch(typeof(EnergySystem), nameof(EnergySystem.StopSleeping))]
        private static class StopPatch
        {
            private static void Prefix(EnergySystem __instance)
            {
                if (Instance?.active?.Owner == __instance) Instance.ClearActive();
            }
        }
    }
}
