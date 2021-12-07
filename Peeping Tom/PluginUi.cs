using ImGuiNET;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface;
using PeepingTom.Ipc;
using PeepingTom.Resources;

namespace PeepingTom {
    internal class PluginUi : IDisposable {
        private PeepingTomPlugin Plugin { get; }

        private uint? PreviousFocus { get; set; } = new();

        private bool _wantsOpen;

        public bool WantsOpen {
            get => this._wantsOpen;
            set => this._wantsOpen = value;
        }

        public bool Visible { get; private set; }

        private bool _settingsOpen;

        public bool SettingsOpen {
            get => this._settingsOpen;
            set => this._settingsOpen = value;
        }

        public PluginUi(PeepingTomPlugin plugin) {
            this.Plugin = plugin;
        }

        public void Dispose() {
            this.WantsOpen = false;
            this.SettingsOpen = false;
        }

        public void Draw() {
            if (this.Plugin.InPvp) {
                return;
            }

            if (this.SettingsOpen) {
                this.ShowSettings();
            }

            var inCombat = this.Plugin.Condition[ConditionFlag.InCombat];
            var inInstance = this.Plugin.Condition[ConditionFlag.BoundByDuty]
                             || this.Plugin.Condition[ConditionFlag.BoundByDuty56]
                             || this.Plugin.Condition[ConditionFlag.BoundByDuty95];
            var inCutscene = this.Plugin.Condition[ConditionFlag.WatchingCutscene]
                             || this.Plugin.Condition[ConditionFlag.WatchingCutscene78]
                             || this.Plugin.Condition[ConditionFlag.OccupiedInCutSceneEvent];

            // FIXME: this could just be a boolean expression
            var shouldBeShown = this.WantsOpen;
            if (inCombat && !this.Plugin.Config.ShowInCombat) {
                shouldBeShown = false;
            } else if (inInstance && !this.Plugin.Config.ShowInInstance) {
                shouldBeShown = false;
            } else if (inCutscene && !this.Plugin.Config.ShowInCutscenes) {
                shouldBeShown = false;
            }

            this.Visible = shouldBeShown;

            if (shouldBeShown) {
                this.ShowMainWindow();
            }

            const ImGuiWindowFlags flags = ImGuiWindowFlags.NoBackground
                                           | ImGuiWindowFlags.NoTitleBar
                                           | ImGuiWindowFlags.NoNav
                                           | ImGuiWindowFlags.NoNavInputs
                                           | ImGuiWindowFlags.NoFocusOnAppearing
                                           | ImGuiWindowFlags.NoNavFocus
                                           | ImGuiWindowFlags.NoInputs
                                           | ImGuiWindowFlags.NoMouseInputs
                                           | ImGuiWindowFlags.NoSavedSettings
                                           | ImGuiWindowFlags.NoDecoration
                                           | ImGuiWindowFlags.NoScrollWithMouse;
            ImGuiHelpers.ForceNextWindowMainViewport();
            if (!ImGui.Begin("Peeping Tom targeting indicator dummy window", flags)) {
                ImGui.End();
                return;
            }

            if (this.Plugin.Config.MarkTargeted) {
                this.MarkPlayer(this.GetCurrentTarget(), this.Plugin.Config.TargetedColour, this.Plugin.Config.TargetedSize);
            }

            if (!this.Plugin.Config.MarkTargeting) {
                goto EndDummy;
            }

            var player = this.Plugin.ClientState.LocalPlayer;
            if (player == null) {
                goto EndDummy;
            }

            var targeting = this.Plugin.Watcher.CurrentTargeters
                .Select(targeter => this.Plugin.ObjectTable.FirstOrDefault(obj => obj.ObjectId == targeter.ObjectId))
                .Where(targeter => targeter is PlayerCharacter)
                .Cast<PlayerCharacter>()
                .ToArray();
            foreach (var targeter in targeting) {
                this.MarkPlayer(targeter, this.Plugin.Config.TargetingColour, this.Plugin.Config.TargetingSize);
            }

            EndDummy:
            ImGui.End();
        }

        private void ShowSettings() {
            ImGui.SetNextWindowSize(new Vector2(700, 250));
            var windowTitle = string.Format(Language.SettingsTitle, this.Plugin.Name);
            if (!ImGui.Begin($"{windowTitle}###ptom-settings", ref this._settingsOpen, ImGuiWindowFlags.NoResize)) {
                ImGui.End();
                return;
            }

            if (ImGui.BeginTabBar("##settings-tabs")) {
                if (ImGui.BeginTabItem($"{Language.SettingsMarkersTab}###markers-tab")) {
                    var markTargeted = this.Plugin.Config.MarkTargeted;
                    if (ImGui.Checkbox(Language.SettingsMarkersMarkTarget, ref markTargeted)) {
                        this.Plugin.Config.MarkTargeted = markTargeted;
                        this.Plugin.Config.Save();
                    }

                    var targetedColour = this.Plugin.Config.TargetedColour;
                    if (ImGui.ColorEdit4(Language.SettingsMarkersMarkTargetColour, ref targetedColour)) {
                        this.Plugin.Config.TargetedColour = targetedColour;
                        this.Plugin.Config.Save();
                    }

                    var targetedSize = this.Plugin.Config.TargetedSize;
                    if (ImGui.DragFloat(Language.SettingsMarkersMarkTargetSize, ref targetedSize, 0.01f, 0f, 15f)) {
                        targetedSize = Math.Max(0f, targetedSize);
                        this.Plugin.Config.TargetedSize = targetedSize;
                        this.Plugin.Config.Save();
                    }

                    ImGui.Spacing();

                    var markTargeting = this.Plugin.Config.MarkTargeting;
                    if (ImGui.Checkbox(Language.SettingsMarkersMarkTargeting, ref markTargeting)) {
                        this.Plugin.Config.MarkTargeting = markTargeting;
                        this.Plugin.Config.Save();
                    }

                    var targetingColour = this.Plugin.Config.TargetingColour;
                    if (ImGui.ColorEdit4(Language.SettingsMarkersMarkTargetingColour, ref targetingColour)) {
                        this.Plugin.Config.TargetingColour = targetingColour;
                        this.Plugin.Config.Save();
                    }

                    var targetingSize = this.Plugin.Config.TargetingSize;
                    if (ImGui.DragFloat(Language.SettingsMarkersMarkTargetingSize, ref targetingSize, 0.01f, 0f, 15f)) {
                        targetingSize = Math.Max(0f, targetingSize);
                        this.Plugin.Config.TargetingSize = targetingSize;
                        this.Plugin.Config.Save();
                    }

                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem($"{Language.SettingsFilterTab}###filters-tab")) {
                    var showParty = this.Plugin.Config.LogParty;
                    if (ImGui.Checkbox(Language.SettingsFilterLogParty, ref showParty)) {
                        this.Plugin.Config.LogParty = showParty;
                        this.Plugin.Config.Save();
                    }

                    var logAlliance = this.Plugin.Config.LogAlliance;
                    if (ImGui.Checkbox(Language.SettingsFilterLogAlliance, ref logAlliance)) {
                        this.Plugin.Config.LogAlliance = logAlliance;
                        this.Plugin.Config.Save();
                    }

                    var logInCombat = this.Plugin.Config.LogInCombat;
                    if (ImGui.Checkbox(Language.SettingsFilterLogCombat, ref logInCombat)) {
                        this.Plugin.Config.LogInCombat = logInCombat;
                        this.Plugin.Config.Save();
                    }

                    var logSelf = this.Plugin.Config.LogSelf;
                    if (ImGui.Checkbox(Language.SettingsFilterLogSelf, ref logSelf)) {
                        this.Plugin.Config.LogSelf = logSelf;
                        this.Plugin.Config.Save();
                    }

                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem($"{Language.SettingsBehaviourTab}###behaviour-tab")) {
                    var focusTarget = this.Plugin.Config.FocusTargetOnHover;
                    if (ImGui.Checkbox(Language.SettingsBehaviourFocusHover, ref focusTarget)) {
                        this.Plugin.Config.FocusTargetOnHover = focusTarget;
                        this.Plugin.Config.Save();
                    }

                    var openExamine = this.Plugin.Config.OpenExamine;
                    if (ImGui.Checkbox(Language.SettingsBehaviourExamineEnabled, ref openExamine)) {
                        this.Plugin.Config.OpenExamine = openExamine;
                        this.Plugin.Config.Save();
                    }

                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem($"{Language.SettingsSoundTab}###sound-tab")) {
                    var playSound = this.Plugin.Config.PlaySoundOnTarget;
                    if (ImGui.Checkbox(Language.SettingsSoundEnabled, ref playSound)) {
                        this.Plugin.Config.PlaySoundOnTarget = playSound;
                        this.Plugin.Config.Save();
                    }

                    var path = this.Plugin.Config.SoundPath ?? "";
                    if (ImGui.InputText(Language.SettingsSoundPath, ref path, 1_000)) {
                        path = path.Trim();
                        this.Plugin.Config.SoundPath = path.Length == 0 ? null : path;
                        this.Plugin.Config.Save();
                    }

                    ImGui.Text(Language.SettingsSoundPathHelp);

                    var volume = this.Plugin.Config.SoundVolume * 100f;
                    if (ImGui.DragFloat(Language.SettingsSoundVolume, ref volume, .1f, 0f, 100f, "%.1f%%")) {
                        this.Plugin.Config.SoundVolume = Math.Max(0f, Math.Min(1f, volume / 100f));
                        this.Plugin.Config.Save();
                    }

                    var soundDevice = this.Plugin.Config.SoundDevice;
                    string name;
                    if (soundDevice == -1) {
                        name = Language.SettingsSoundDefaultDevice;
                    } else if (soundDevice > -1 && soundDevice < WaveOut.DeviceCount) {
                        var caps = WaveOut.GetCapabilities(soundDevice);
                        name = caps.ProductName;
                    } else {
                        name = Language.SettingsSoundInvalidDevice;
                    }

                    if (ImGui.BeginCombo($"{Language.SettingsSoundOutputDevice}###sound-output-device-combo", name)) {
                        if (ImGui.Selectable(Language.SettingsSoundDefaultDevice)) {
                            this.Plugin.Config.SoundDevice = -1;
                            this.Plugin.Config.Save();
                        }

                        ImGui.Separator();

                        for (var deviceNum = 0; deviceNum < WaveOut.DeviceCount; deviceNum++) {
                            var caps = WaveOut.GetCapabilities(deviceNum);
                            if (!ImGui.Selectable(caps.ProductName)) {
                                continue;
                            }

                            this.Plugin.Config.SoundDevice = deviceNum;
                            this.Plugin.Config.Save();
                        }

                        ImGui.EndCombo();
                    }

                    var soundCooldown = this.Plugin.Config.SoundCooldown;
                    if (ImGui.DragFloat(Language.SettingsSoundCooldown, ref soundCooldown, .01f, 0f, 30f)) {
                        soundCooldown = Math.Max(0f, soundCooldown);
                        this.Plugin.Config.SoundCooldown = soundCooldown;
                        this.Plugin.Config.Save();
                    }

                    var playWhenClosed = this.Plugin.Config.PlaySoundWhenClosed;
                    if (ImGui.Checkbox(Language.SettingsSoundPlayWhenClosed, ref playWhenClosed)) {
                        this.Plugin.Config.PlaySoundWhenClosed = playWhenClosed;
                        this.Plugin.Config.Save();
                    }

                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem($"{Language.SettingsWindowTab}###window-tab")) {
                    var openOnLogin = this.Plugin.Config.OpenOnLogin;
                    if (ImGui.Checkbox(Language.SettingsWindowOpenLogin, ref openOnLogin)) {
                        this.Plugin.Config.OpenOnLogin = openOnLogin;
                        this.Plugin.Config.Save();
                    }

                    var allowMovement = this.Plugin.Config.AllowMovement;
                    if (ImGui.Checkbox(Language.SettingsWindowAllowMovement, ref allowMovement)) {
                        this.Plugin.Config.AllowMovement = allowMovement;
                        this.Plugin.Config.Save();
                    }

                    var allowResizing = this.Plugin.Config.AllowResize;
                    if (ImGui.Checkbox(Language.SettingsWindowAllowResize, ref allowResizing)) {
                        this.Plugin.Config.AllowResize = allowResizing;
                        this.Plugin.Config.Save();
                    }

                    ImGui.Spacing();

                    var showInCombat = this.Plugin.Config.ShowInCombat;
                    if (ImGui.Checkbox(Language.SettingsWindowShowCombat, ref showInCombat)) {
                        this.Plugin.Config.ShowInCombat = showInCombat;
                        this.Plugin.Config.Save();
                    }

                    var showInInstance = this.Plugin.Config.ShowInInstance;
                    if (ImGui.Checkbox(Language.SettingsWindowShowInstance, ref showInInstance)) {
                        this.Plugin.Config.ShowInInstance = showInInstance;
                        this.Plugin.Config.Save();
                    }

                    var showInCutscenes = this.Plugin.Config.ShowInCutscenes;
                    if (ImGui.Checkbox(Language.SettingsWindowShowCutscene, ref showInCutscenes)) {
                        this.Plugin.Config.ShowInCutscenes = showInCutscenes;
                        this.Plugin.Config.Save();
                    }

                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem($"{Language.SettingsHistoryTab}###history-tab")) {
                    var keepHistory = this.Plugin.Config.KeepHistory;
                    if (ImGui.Checkbox(Language.SettingsHistoryEnabled, ref keepHistory)) {
                        this.Plugin.Config.KeepHistory = keepHistory;
                        this.Plugin.Config.Save();
                    }

                    var historyWhenClosed = this.Plugin.Config.HistoryWhenClosed;
                    if (ImGui.Checkbox(Language.SettingsHistoryRecordClosed, ref historyWhenClosed)) {
                        this.Plugin.Config.HistoryWhenClosed = historyWhenClosed;
                        this.Plugin.Config.Save();
                    }

                    var numHistory = this.Plugin.Config.NumHistory;
                    if (ImGui.InputInt(Language.SettingsHistoryAmount, ref numHistory)) {
                        numHistory = Math.Max(0, Math.Min(50, numHistory));
                        this.Plugin.Config.NumHistory = numHistory;
                        this.Plugin.Config.Save();
                    }

                    var showTimestamps = this.Plugin.Config.ShowTimestamps;
                    if (ImGui.Checkbox(Language.SettingsHistoryTimestamps, ref showTimestamps)) {
                        this.Plugin.Config.ShowTimestamps = showTimestamps;
                        this.Plugin.Config.Save();
                    }

                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem($"{Language.SettingsAdvancedTab}###advanced-tab")) {
                    var pollFrequency = this.Plugin.Config.PollFrequency;
                    if (ImGui.DragInt(Language.SettingsAdvancedPollFrequency, ref pollFrequency, .1f, 1, 1600)) {
                        this.Plugin.Config.PollFrequency = pollFrequency;
                        this.Plugin.Config.Save();
                    }

                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }

            ImGui.End();
        }

        private void ShowMainWindow() {
            var targeting = this.Plugin.Watcher.CurrentTargeters;
            var previousTargeters = this.Plugin.Config.KeepHistory ? this.Plugin.Watcher.PreviousTargeters : null;

            // to prevent looping over a subset of the actors repeatedly when multiple people are targeting,
            // create a dictionary for O(1) lookups by actor id
            Dictionary<uint, GameObject>? objects = null;
            if (targeting.Count + (previousTargeters?.Count ?? 0) > 1) {
                var dict = new Dictionary<uint, GameObject>();
                foreach (var obj in this.Plugin.ObjectTable) {
                    if (dict.ContainsKey(obj.ObjectId) || obj.ObjectKind != ObjectKind.Player) {
                        continue;
                    }

                    dict.Add(obj.ObjectId, obj);
                }

                objects = dict;
            }

            var flags = ImGuiWindowFlags.None;
            if (!this.Plugin.Config.AllowMovement) {
                flags |= ImGuiWindowFlags.NoMove;
            }

            if (!this.Plugin.Config.AllowResize) {
                flags |= ImGuiWindowFlags.NoResize;
            }

            ImGui.SetNextWindowSize(new Vector2(290, 195), ImGuiCond.FirstUseEver);
            if (!ImGui.Begin(this.Plugin.Name, ref this._wantsOpen, flags)) {
                ImGui.End();
                return;
            }

            {
                ImGui.Text(Language.MainTargetingYou);
                ImGui.SameLine();
                HelpMarker(this.Plugin.Config.OpenExamine
                    ? Language.MainHelpExamine
                    : Language.MainHelpNoExamine);

                var height = ImGui.GetContentRegionAvail().Y;
                height -= ImGui.GetStyle().ItemSpacing.Y;

                var anyHovered = false;
                if (ImGui.BeginListBox("##targeting", new Vector2(-1, height))) {
                    // add the two first players for testing
                    // foreach (var p in this.Plugin.Interface.ClientState.Actors
                    //     .Where(actor => actor is PlayerCharacter)
                    //     .Skip(1)
                    //     .Select(actor => actor as PlayerCharacter)
                    //     .Take(2)) {
                    //     this.AddEntry(new Targeter(p), p, ref anyHovered);
                    // }

                    foreach (var targeter in targeting) {
                        GameObject? obj = null;
                        objects?.TryGetValue(targeter.ObjectId, out obj);
                        this.AddEntry(targeter, obj, ref anyHovered);
                    }

                    if (this.Plugin.Config.KeepHistory) {
                        // get a list of the previous targeters that aren't currently targeting
                        var previous = (previousTargeters ?? new List<Targeter>())
                            .Where(old => targeting.All(actor => actor.ObjectId != old.ObjectId))
                            .Take(this.Plugin.Config.NumHistory);
                        // add previous targeters to the list
                        foreach (var oldTargeter in previous) {
                            GameObject? obj = null;
                            objects?.TryGetValue(oldTargeter.ObjectId, out obj);
                            this.AddEntry(oldTargeter, obj, ref anyHovered, ImGuiSelectableFlags.Disabled);
                        }
                    }

                    ImGui.EndListBox();
                }

                var previousFocus = this.PreviousFocus;
                if (this.Plugin.Config.FocusTargetOnHover && !anyHovered && previousFocus != null) {
                    if (previousFocus == uint.MaxValue) {
                        this.Plugin.TargetManager.FocusTarget = null;
                    } else {
                        var actor = this.Plugin.ObjectTable.FirstOrDefault(a => a.ObjectId == previousFocus);
                        // either target the actor if still present or target nothing
                        this.Plugin.TargetManager.FocusTarget = actor;
                    }

                    this.PreviousFocus = null;
                }

                ImGui.End();
            }
        }

        private static void HelpMarker(string text) {
            ImGui.TextDisabled("(?)");
            if (!ImGui.IsItemHovered()) {
                return;
            }

            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 20f);
            ImGui.TextUnformatted(text);
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }

        private void AddEntry(Targeter targeter, GameObject? obj, ref bool anyHovered, ImGuiSelectableFlags flags = ImGuiSelectableFlags.None) {
            ImGui.BeginGroup();

            ImGui.Selectable(targeter.Name.TextValue, false, flags);

            if (this.Plugin.Config.ShowTimestamps) {
                var time = DateTime.UtcNow - targeter.When >= TimeSpan.FromDays(1)
                    ? targeter.When.ToLocalTime().ToString("dd/MM")
                    : targeter.When.ToLocalTime().ToString("t");
                ImGui.SameLine(ImGui.GetWindowContentRegionWidth() - ImGui.CalcTextSize(time).X);

                if (flags.HasFlag(ImGuiSelectableFlags.Disabled)) {
                    ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int) ImGuiCol.TextDisabled]);
                }

                ImGui.TextUnformatted(time);

                if (flags.HasFlag(ImGuiSelectableFlags.Disabled)) {
                    ImGui.PopStyleColor();
                }
            }

            ImGui.EndGroup();

            var hover = ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled);
            var left = hover && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
            var right = hover && ImGui.IsMouseClicked(ImGuiMouseButton.Right);

            obj ??= this.Plugin.ObjectTable.FirstOrDefault(a => a.ObjectId == targeter.ObjectId);

            // don't count as hovered if the actor isn't here (clears focus target when hovering missing actors)
            if (obj != null) {
                anyHovered |= hover;
            }

            if (this.Plugin.Config.FocusTargetOnHover && hover && obj != null) {
                this.PreviousFocus ??= this.Plugin.TargetManager.FocusTarget?.ObjectId ?? uint.MaxValue;
                this.Plugin.TargetManager.FocusTarget = obj;
            }

            if (left) {
                if (this.Plugin.Config.OpenExamine && ImGui.GetIO().KeyAlt) {
                    if (obj != null) {
                        this.Plugin.Common.Functions.Examine.OpenExamineWindow(obj);
                    } else {
                        var error = string.Format(Language.ExamineErrorToast, targeter.Name);
                        this.Plugin.ToastGui.ShowError(error);
                    }
                } else {
                    var payload = new PlayerPayload(targeter.Name.TextValue, targeter.HomeWorldId);
                    Payload[] payloads = { payload };
                    this.Plugin.ChatGui.PrintChat(new XivChatEntry {
                        Message = new SeString(payloads),
                    });
                }
            } else if (right && obj != null) {
                this.Plugin.TargetManager.Target = obj;
            }
        }

        private void MarkPlayer(GameObject? player, Vector4 colour, float size) {
            if (player == null) {
                return;
            }

            if (!this.Plugin.GameGui.WorldToScreen(player.Position, out var screenPos)) {
                return;
            }

            ImGui.PushClipRect(ImGuiHelpers.MainViewport.Pos, ImGuiHelpers.MainViewport.Pos + ImGuiHelpers.MainViewport.Size, false);

            ImGui.GetWindowDrawList().AddCircleFilled(
                ImGuiHelpers.MainViewport.Pos + new Vector2(screenPos.X, screenPos.Y),
                size,
                ImGui.GetColorU32(colour),
                100
            );

            ImGui.PopClipRect();
        }

        private PlayerCharacter? GetCurrentTarget() {
            var player = this.Plugin.ClientState.LocalPlayer;
            if (player == null) {
                return null;
            }

            var targetId = player.TargetObjectId;
            if (targetId <= 0) {
                return null;
            }

            return this.Plugin.ObjectTable
                .Where(actor => actor.ObjectId == targetId && actor is PlayerCharacter)
                .Select(actor => actor as PlayerCharacter)
                .FirstOrDefault();
        }
    }
}
