using System;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using ImGuiNET;

namespace NoClippy
{
    public partial class Configuration
    {
        public bool EnableEncounterStats = false;
        public bool EnableEncounterStatsLoggingReport = false;
        public int EnableEncounterStatsLoggingReportMinSeconds = 30;
        public bool EnableEncounterStatsLoggingReportInSeconds = true;
        public bool EnableEncounterStatsLoggingClip = true;
        public bool EnableEncounterStatsLoggingWaste = false;
    }
}

namespace NoClippy.Modules
{
    public class Stats : Module
    {
        public override bool IsEnabled
        {
            get => NoClippy.Config.EnableEncounterStats;
            set => NoClippy.Config.EnableEncounterStats = value;
        }

        public override int DrawOrder => 5;

        private DateTime begunEncounter = DateTime.MinValue;
        private ushort lastDetectedClip = 0;
        private float currentWastedGCD = 0;
        private float encounterTotalClip = 0;
        private float encounterTotalWaste = 0;

        private void BeginEncounter()
        {
            begunEncounter = DateTime.Now;
            encounterTotalClip = 0;
            encounterTotalWaste = 0;
            currentWastedGCD = 0;
        }

        private void EndEncounter()
        {
            if (NoClippy.Config.EnableEncounterStatsLoggingReport)
            {
                var span = DateTime.Now - begunEncounter;
                if (span.TotalSeconds < NoClippy.Config.EnableEncounterStatsLoggingReportMinSeconds) return;
                if (encounterTotalClip == 0 && encounterTotalWaste == 0) return;

                var formattedTime = NoClippy.Config.EnableEncounterStatsLoggingReportInSeconds ?
                    $"{span.TotalSeconds:00.0}" :
                    $"{Math.Floor(span.TotalMinutes):00}m{span.Seconds:00}s";
                NoClippy.PrintLog($"in {formattedTime}, clipped: {encounterTotalClip:0.00}, wasted: {encounterTotalWaste:0.00}");
            }
            begunEncounter = DateTime.MinValue;
        }

        private unsafe void DetectClipping()
        {
            var animationLock = Game.actionManager->animationLock;
            if (lastDetectedClip == Game.actionManager->currentSequence || Game.actionManager->isGCDRecastActive || animationLock <= 0) return;

            if (animationLock != 0.1f) // TODO need better way of detecting cast tax, IsCasting is not reliable here, additionally, this will detect LB
            {
                encounterTotalClip += animationLock;
                if (NoClippy.Config.EnableEncounterStatsLoggingClip)
                    NoClippy.PrintLog($"clipped: {NoClippy.F2MS(animationLock)} ms");
            }

            lastDetectedClip = Game.actionManager->currentSequence;
        }

        private unsafe void DetectWastedGCD()
        {
            if (!Game.actionManager->isGCDRecastActive && !Game.actionManager->isQueued)
            {
                if (Game.actionManager->animationLock > 0) return;
                currentWastedGCD += ImGui.GetIO().DeltaTime;
            }
            else if (currentWastedGCD > 0)
            {
                encounterTotalWaste += currentWastedGCD;
                if (NoClippy.Config.EnableEncounterStatsLoggingWaste)
                    NoClippy.PrintLog($"wasted: {NoClippy.F2MS(currentWastedGCD)} ms");
                currentWastedGCD = 0;
            }
        }

        private void Update()
        {
            if (DalamudApi.Condition[ConditionFlag.InCombat])
            {
                if (begunEncounter == DateTime.MinValue)
                    BeginEncounter();

                DetectClipping();
                DetectWastedGCD();
            }
            else if (begunEncounter != DateTime.MinValue)
            {
                EndEncounter();
            }
        }

        public override void DrawConfig()
        {
            if (ImGui.Checkbox("Enable Encounter Stats", ref NoClippy.Config.EnableEncounterStats))
                NoClippy.Config.Save();
            PluginUI.SetItemTooltip("Tracks clips and wasted GCD time while in combat, and logs the total afterwards.");

            if (NoClippy.Config.EnableEncounterStats)
            {
                ImGui.Columns(2, null, false);

                if (ImGui.Checkbox("Show GCD clip", ref NoClippy.Config.EnableEncounterStatsLoggingClip))
                    NoClippy.Config.Save();
                PluginUI.SetItemTooltip("Show individual encounter GCD clip.");
                ImGui.NextColumn();

                if (ImGui.Checkbox("Show GCD waste", ref NoClippy.Config.EnableEncounterStatsLoggingWaste))
                    NoClippy.Config.Save();
                PluginUI.SetItemTooltip("Show individual encounter GCD waste.");
                ImGui.Columns(1);

                if (ImGui.Checkbox("Show summary after combat", ref NoClippy.Config.EnableEncounterStatsLoggingReport))
                    NoClippy.Config.Save();
                PluginUI.SetItemTooltip("Show GCD clip and waste report after a combat.");
                // ImGui.NextColumn();
                if (NoClippy.Config.EnableEncounterStatsLoggingReport)
                {
                    ImGui.Text(" ┗ But no shorter than");
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(125);
                    if (ImGui.InputInt("seconds", ref NoClippy.Config.EnableEncounterStatsLoggingReportMinSeconds))
                        NoClippy.Config.Save();
                    PluginUI.SetItemTooltip("Show report only for combat longer than this seconds.");

                    ImGui.Text(" ┗ ");
                    ImGui.SameLine();
                    if (ImGui.Checkbox("Show encounter total time in seconds", ref NoClippy.Config.EnableEncounterStatsLoggingReportInSeconds))
                        NoClippy.Config.Save();
                    PluginUI.SetItemTooltip("Show encounter total time in seconds in report after combat.");
                }
            }
        }

        public override void Enable() => Game.OnUpdate += Update;
        public override void Disable() => Game.OnUpdate -= Update;
    }
}
