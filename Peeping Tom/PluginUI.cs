using Dalamud.Game.Chat;
using Dalamud.Game.Chat.SeStringHandling;
using Dalamud.Game.Chat.SeStringHandling.Payloads;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Actors.Types;
using Dalamud.Plugin;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;

namespace PeepingTom {
    class PluginUI : IDisposable {
        private readonly PeepingTomPlugin plugin;
        private readonly Configuration config;
        private readonly DalamudPluginInterface pi;
        private readonly TargetWatcher watcher;

        private Optional<Actor> previousFocus = new Optional<Actor>();

        private bool visible = false;
        public bool Visible {
            get { return this.visible; }
            set { this.visible = value; }
        }

        private bool settingsVisible = false;
        public bool SettingsVisible {
            get { return this.settingsVisible; }
            set { this.settingsVisible = value; }
        }

        public PluginUI(PeepingTomPlugin plugin, Configuration config, DalamudPluginInterface pluginInterface, TargetWatcher watcher) {
            this.plugin = plugin;
            this.config = config;
            this.pi = pluginInterface;
            this.watcher = watcher;
        }

        public void Dispose() {
            this.Visible = false;
            this.SettingsVisible = false;
        }

        public void Draw() {
            if (this.SettingsVisible) {
                ShowSettings();
            }

            bool inCombat = this.pi.ClientState.Condition[ConditionFlag.InCombat];
            bool inInstance = this.pi.ClientState.Condition[ConditionFlag.BoundByDuty]
                || this.pi.ClientState.Condition[ConditionFlag.BoundByDuty56]
                || this.pi.ClientState.Condition[ConditionFlag.BoundByDuty95];
            bool inCutscene = this.pi.ClientState.Condition[ConditionFlag.WatchingCutscene]
                || this.pi.ClientState.Condition[ConditionFlag.WatchingCutscene78]
                || this.pi.ClientState.Condition[ConditionFlag.OccupiedInCutSceneEvent];

            // FIXME: this could just be a boolean expression
            bool shouldBeShown = this.Visible;
            if (inCombat && !this.config.ShowInCombat) {
                shouldBeShown = false;
            } else if (inInstance && !this.config.ShowInInstance) {
                shouldBeShown = false;
            } else if (inCutscene && !this.config.ShowInCutscenes) {
                shouldBeShown = false;
            }

            if (shouldBeShown) {
                ShowMainWindow();
            }

            if (this.config.MarkTargeted) {
                MarkPlayer(GetCurrentTarget(), this.config.TargetedColour, this.config.TargetedSize);
            }

            if (this.config.MarkTargeting) {
                PlayerCharacter player = this.pi.ClientState.LocalPlayer;
                if (player != null) {
                    IReadOnlyCollection<PlayerCharacter> targeting = this.watcher.CurrentTargeters;
                    foreach (PlayerCharacter targeter in targeting) {
                        MarkPlayer(targeter, this.config.TargetingColour, this.config.TargetingSize);
                    }
                }
            }
        }

        private void ShowSettings() {
            // 700x250 if setting a size
            ImGui.SetNextWindowSize(new Vector2(700, 250));
            if (ImGui.Begin($"{this.plugin.Name} settings", ref this.settingsVisible)) {
                if (ImGui.BeginTabBar("##settings-tabs")) {
                    if (ImGui.BeginTabItem("Markers")) {
                        bool markTargeted = this.config.MarkTargeted;
                        if (ImGui.Checkbox("Mark your target", ref markTargeted)) {
                            this.config.MarkTargeted = markTargeted;
                            this.config.Save();
                        }

                        Vector4 targetedColour = this.config.TargetedColour;
                        if (ImGui.ColorEdit4("Target mark colour", ref targetedColour)) {
                            this.config.TargetedColour = targetedColour;
                            this.config.Save();
                        }

                        float targetedSize = this.config.TargetedSize;
                        if (ImGui.DragFloat("Target mark size", ref targetedSize, 0.01f, 0f, 15f)) {
                            targetedSize = Math.Max(0f, targetedSize);
                            this.config.TargetedSize = targetedSize;
                            this.config.Save();
                        }

                        ImGui.Spacing();

                        bool markTargeting = this.config.MarkTargeting;
                        if (ImGui.Checkbox("Mark targeting you", ref markTargeting)) {
                            this.config.MarkTargeting = markTargeting;
                            this.config.Save();
                        }

                        Vector4 targetingColour = this.config.TargetingColour;
                        if (ImGui.ColorEdit4("Targeting mark colour", ref targetingColour)) {
                            this.config.TargetingColour = targetingColour;
                            this.config.Save();
                        }

                        float targetingSize = this.config.TargetingSize;
                        if (ImGui.DragFloat("Targeting mark size", ref targetingSize, 0.01f, 0f, 15f)) {
                            targetingSize = Math.Max(0f, targetingSize);
                            this.config.TargetingSize = targetingSize;
                            this.config.Save();
                        }

                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Filters")) {
                        bool showParty = this.config.LogParty;
                        if (ImGui.Checkbox("Log party members", ref showParty)) {
                            this.config.LogParty = showParty;
                            this.config.Save();
                        }

                        bool logAlliance = this.config.LogAlliance;
                        if (ImGui.Checkbox("Log alliance members", ref logAlliance)) {
                            this.config.LogAlliance = logAlliance;
                            this.config.Save();
                        }

                        bool logInCombat = this.config.LogInCombat;
                        if (ImGui.Checkbox("Log targeters engaged in combat", ref logInCombat)) {
                            this.config.LogInCombat = logInCombat;
                            this.config.Save();
                        }

                        bool logSelf = this.config.LogSelf;
                        if (ImGui.Checkbox("Log yourself", ref logSelf)) {
                            this.config.LogSelf = logSelf;
                            this.config.Save();
                        }

                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Behaviour")) {
                        bool focusTarget = this.config.FocusTargetOnHover;
                        if (ImGui.Checkbox("Focus target on hover", ref focusTarget)) {
                            this.config.FocusTargetOnHover = focusTarget;
                            this.config.Save();
                        }

                        bool playSound = this.config.PlaySoundOnTarget;
                        if (ImGui.Checkbox("Play sound when targeted", ref playSound)) {
                            this.config.PlaySoundOnTarget = playSound;
                            this.config.Save();
                        }

                        string path = this.config.SoundPath ?? "";
                        if (ImGui.InputText("Path to WAV file", ref path, 1_000)) {
                            path = path.Trim();
                            this.config.SoundPath = path.Length == 0 ? null : path;
                            this.config.Save();
                        }

                        ImGui.Text("Leave this blank to use a built-in sound.");

                        float soundCooldown = this.config.SoundCooldown;
                        if (ImGui.DragFloat("Cooldown for sound (seconds)", ref soundCooldown, .01f, 0f, 30f)) {
                            soundCooldown = Math.Max(0f, soundCooldown);
                            this.config.SoundCooldown = soundCooldown;
                            this.config.Save();
                        }

                        bool playWhenClosed = this.config.PlaySoundWhenClosed;
                        if (ImGui.Checkbox("Play sound when window is closed", ref playWhenClosed)) {
                            this.config.PlaySoundWhenClosed = playWhenClosed;
                            this.config.Save();
                        }

                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Window")) {
                        bool openOnLogin = this.config.OpenOnLogin;
                        if (ImGui.Checkbox("Open on login", ref openOnLogin)) {
                            this.config.OpenOnLogin = openOnLogin;
                            this.config.Save();
                        }

                        bool allowMovement = this.config.AllowMovement;
                        if (ImGui.Checkbox("Allow moving the main window", ref allowMovement)) {
                            this.config.AllowMovement = allowMovement;
                            this.config.Save();
                        }

                        ImGui.Spacing();

                        bool showInCombat = this.config.ShowInCombat;
                        if (ImGui.Checkbox("Show window while in combat", ref showInCombat)) {
                            this.config.ShowInCombat = showInCombat;
                            this.config.Save();
                        }

                        bool showInInstance = this.config.ShowInInstance;
                        if (ImGui.Checkbox("Show window while in instance", ref showInInstance)) {
                            this.config.ShowInInstance = showInInstance;
                            this.config.Save();
                        }

                        bool showInCutscenes = this.config.ShowInCutscenes;
                        if (ImGui.Checkbox("Show window while in cutscenes", ref showInCutscenes)) {
                            this.config.ShowInCutscenes = showInCutscenes;
                            this.config.Save();
                        }

                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("History")) {
                        bool keepHistory = this.config.KeepHistory;
                        if (ImGui.Checkbox("Show previous targeters", ref keepHistory)) {
                            this.config.KeepHistory = keepHistory;
                            this.config.Save();
                        }

                        bool historyWhenClosed = this.config.HistoryWhenClosed;
                        if (ImGui.Checkbox("Record history when window is closed", ref historyWhenClosed)) {
                            this.config.HistoryWhenClosed = historyWhenClosed;
                            this.config.Save();
                        }

                        int numHistory = this.config.NumHistory;
                        if (ImGui.InputInt("Number of previous targeters to keep", ref numHistory)) {
                            numHistory = Math.Max(0, Math.Min(10, numHistory));
                            this.config.NumHistory = numHistory;
                            this.config.Save();
                        }

                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Debug")) {
                        bool debugMarkers = this.config.DebugMarkers;
                        if (ImGui.Checkbox("Debug markers", ref debugMarkers)) {
                            this.config.DebugMarkers = debugMarkers;
                            this.config.Save();
                        }

                        ImGui.Separator();

                        if (ImGui.Button("Log targeting you")) {
                            PlayerCharacter player = this.pi.ClientState.LocalPlayer;
                            if (player != null) {
                                // loop over all players looking at the current player
                                var actors = this.pi.ClientState.Actors
                                    .Where(actor => actor.TargetActorID == player.ActorId && actor is PlayerCharacter)
                                    .Select(actor => actor as PlayerCharacter);
                                foreach (PlayerCharacter actor in actors) {
                                    PlayerPayload payload = new PlayerPayload(this.pi.Data, actor.Name, actor.HomeWorld.Id);
                                    Payload[] payloads = { payload };
                                    this.pi.Framework.Gui.Chat.PrintChat(new XivChatEntry {
                                        MessageBytes = new SeString(payloads).Encode()
                                    });
                                }
                            }
                        }

                        if (ImGui.Button("Log your target")) {
                            PlayerCharacter target = GetCurrentTarget();

                            if (target != null) {
                                PlayerPayload payload = new PlayerPayload(this.pi.Data, target.Name, target.HomeWorld.Id);
                                Payload[] payloads = { payload };
                                this.pi.Framework.Gui.Chat.PrintChat(new XivChatEntry {
                                    MessageBytes = new SeString(payloads).Encode()
                                });
                            }
                        }

                        if (this.pi.ClientState.LocalPlayer != null) {
                            PlayerCharacter player = this.pi.ClientState.LocalPlayer;
                            IntPtr statusPtr = this.pi.TargetModuleScanner.ResolveRelativeAddress(player.Address, 0x1901);
                            byte status = Marshal.ReadByte(statusPtr);
                            ImGui.Text($"Status: {status}");
                        }

                        ImGui.EndTabItem();
                    }

                    ImGui.EndTabBar();
                }

                ImGui.End();
            }
        }

        private void ShowMainWindow() {
            IReadOnlyCollection<PlayerCharacter> targeting = this.watcher.CurrentTargeters;
            
            ImGuiWindowFlags flags = ImGuiWindowFlags.AlwaysAutoResize;
            if (!this.config.AllowMovement) {
                flags |= ImGuiWindowFlags.NoMove;
            }
            if (ImGui.Begin(this.plugin.Name, ref this.visible, flags)) {
                ImGui.Text("Targeting you");
                bool anyHovered = false;
                if (ImGui.ListBoxHeader("##targeting", targeting.Count, 5)) {
                    // add the two first players for testing
                    //foreach (PlayerCharacter p in this.pi.ClientState.Actors
                    //    .Where(actor => actor is PlayerCharacter)
                    //    .Skip(1)
                    //    .Select(actor => actor as PlayerCharacter)
                    //    .Take(2)) {
                    //    this.AddEntry(new Targeter(p), p, ref anyHovered);
                    //}
                    foreach (PlayerCharacter targeter in targeting) {
                        this.AddEntry(new Targeter(targeter), targeter, ref anyHovered);
                    }
                    if (this.config.KeepHistory) {
                        // get a list of the previous targeters that aren't currently targeting
                        Targeter[] previous = this.watcher.PreviousTargeters
                            .Where(old => targeting.All(actor => actor.ActorId != old.ActorId))
                            .Take(this.config.NumHistory)
                            .ToArray();
                        // add previous targeters to the list
                        foreach (Targeter oldTargeter in previous) {
                            this.AddEntry(oldTargeter, null, ref anyHovered, ImGuiSelectableFlags.Disabled);
                        }
                    }
                    ImGui.ListBoxFooter();
                }
                if (this.config.FocusTargetOnHover && !anyHovered && this.previousFocus.Get(out Actor previousFocus)) {
                    if (previousFocus == null) {
                        this.pi.ClientState.Targets.SetFocusTarget(null);
                    } else {
                        Actor actor = this.pi.ClientState.Actors.FirstOrDefault(a => a.ActorId == previousFocus.ActorId);
                        // either target the actor if still present or target nothing
                        this.pi.ClientState.Targets.SetFocusTarget(actor);
                    }
                    this.previousFocus = new Optional<Actor>();
                }
                ImGui.Text("Click to link or right click to target.");
                ImGui.End();
            }
        }

        private void AddEntry(Targeter targeter, Actor actor, ref bool anyHovered, ImGuiSelectableFlags flags = ImGuiSelectableFlags.None) {
            ImGui.Selectable(targeter.Name, false, flags);
            bool hover = ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled);
            bool left = hover && ImGui.IsMouseClicked(0);
            bool right = hover && ImGui.IsMouseClicked(1);
            if (actor == null) {
                actor = this.pi.ClientState.Actors
                    .Where(a => a.ActorId == targeter.ActorId)
                    .FirstOrDefault();
            }

            // don't count as hovered if the actor isn't here (clears focus target when hovering missing actors)
            if (actor != null) {
                anyHovered |= hover;
            }

            if (this.config.FocusTargetOnHover && hover && actor != null) {
                if (!this.previousFocus.Present) {
                    this.previousFocus = new Optional<Actor>(this.pi.ClientState.Targets.FocusTarget);
                }
                this.pi.ClientState.Targets.SetFocusTarget(actor);
            }

            if (left) {
                PlayerPayload payload = new PlayerPayload(this.pi.Data, targeter.Name, targeter.HomeWorld.Id);
                Payload[] payloads = { payload };
                this.pi.Framework.Gui.Chat.PrintChat(new XivChatEntry {
                    MessageBytes = new SeString(payloads).Encode()
                });
            } else if (right && actor != null) {
                this.pi.ClientState.Targets.SetCurrentTarget(actor);
            }
        }

        private void MarkPlayer(PlayerCharacter player, Vector4 colour, float size) {
            if (player == null) {
                return;
            }

            if (!this.pi.Framework.Gui.WorldToScreen(player.Position, out SharpDX.Vector2 screenPos)) {
                return;
            }

            ImGuiWindowFlags flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoFocusOnAppearing;
            if (this.config.DebugMarkers) {
                flags &= ~ImGuiWindowFlags.NoBackground;
            }

            // smallest window size is 32x32
            if (ImGui.Begin($"Targeting Marker: {player.Name}@{player.HomeWorld}", flags)) {
                // determine the window size, giving it lots of space
                float winSize = Math.Max(32, size * 10);
                ImGui.SetWindowPos(new Vector2(screenPos.X - winSize / 2, screenPos.Y - winSize / 2));
                ImGui.SetWindowSize(new Vector2(winSize, winSize));
                ImGui.GetWindowDrawList().AddCircleFilled(
                    new Vector2(screenPos.X, screenPos.Y),
                    size,
                    ImGui.GetColorU32(colour),
                    100
                );
                ImGui.End();
            }
        }

        private PlayerCharacter GetCurrentTarget() {
            PlayerCharacter player = this.pi.ClientState.LocalPlayer;
            if (player == null) {
                return null;
            }

            int targetId = player.TargetActorID;
            if (targetId <= 0) {
                return null;
            }

            return this.pi.ClientState.Actors
                .Where(actor => actor.ActorId == targetId && actor is PlayerCharacter)
                .Select(actor => actor as PlayerCharacter)
                .FirstOrDefault();
        }
    }
}
