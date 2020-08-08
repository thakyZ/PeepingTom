using Dalamud.Game.Chat;
using Dalamud.Game.Chat.SeStringHandling;
using Dalamud.Game.Chat.SeStringHandling.Payloads;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Actors.Types;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;

namespace PeepingTom {
    class PluginUI : IDisposable {
        private readonly PeepingTomPlugin plugin;

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

        public PluginUI(PeepingTomPlugin plugin) {
            this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin), "PeepingTomPlugin cannot be null");
        }

        public void Dispose() {
            this.Visible = false;
            this.SettingsVisible = false;
        }

        public void Draw() {
            if (this.SettingsVisible) {
                ShowSettings();
            }

            bool inCombat = this.plugin.Interface.ClientState.Condition[ConditionFlag.InCombat];
            bool inInstance = this.plugin.Interface.ClientState.Condition[ConditionFlag.BoundByDuty]
                || this.plugin.Interface.ClientState.Condition[ConditionFlag.BoundByDuty56]
                || this.plugin.Interface.ClientState.Condition[ConditionFlag.BoundByDuty95];
            bool inCutscene = this.plugin.Interface.ClientState.Condition[ConditionFlag.WatchingCutscene]
                || this.plugin.Interface.ClientState.Condition[ConditionFlag.WatchingCutscene78]
                || this.plugin.Interface.ClientState.Condition[ConditionFlag.OccupiedInCutSceneEvent];

            // FIXME: this could just be a boolean expression
            bool shouldBeShown = this.Visible;
            if (inCombat && !this.plugin.Config.ShowInCombat) {
                shouldBeShown = false;
            } else if (inInstance && !this.plugin.Config.ShowInInstance) {
                shouldBeShown = false;
            } else if (inCutscene && !this.plugin.Config.ShowInCutscenes) {
                shouldBeShown = false;
            }

            if (shouldBeShown) {
                ShowMainWindow();
            }

            if (this.plugin.Config.MarkTargeted) {
                MarkPlayer(GetCurrentTarget(), this.plugin.Config.TargetedColour, this.plugin.Config.TargetedSize);
            }

            if (this.plugin.Config.MarkTargeting) {
                PlayerCharacter player = this.plugin.Interface.ClientState.LocalPlayer;
                if (player != null) {
                    PlayerCharacter[] targeting = this.plugin.Watcher.CurrentTargeters
                        .Select(targeter => this.plugin.Interface.ClientState.Actors.FirstOrDefault(actor => actor.ActorId == targeter.ActorId))
                        .Where(targeter => targeter != null)
                        .Select(targeter => targeter as PlayerCharacter)
                        .ToArray();
                    foreach (PlayerCharacter targeter in targeting) {
                        MarkPlayer(targeter, this.plugin.Config.TargetingColour, this.plugin.Config.TargetingSize);
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
                        bool markTargeted = this.plugin.Config.MarkTargeted;
                        if (ImGui.Checkbox("Mark your target", ref markTargeted)) {
                            this.plugin.Config.MarkTargeted = markTargeted;
                            this.plugin.Config.Save();
                        }

                        Vector4 targetedColour = this.plugin.Config.TargetedColour;
                        if (ImGui.ColorEdit4("Target mark colour", ref targetedColour)) {
                            this.plugin.Config.TargetedColour = targetedColour;
                            this.plugin.Config.Save();
                        }

                        float targetedSize = this.plugin.Config.TargetedSize;
                        if (ImGui.DragFloat("Target mark size", ref targetedSize, 0.01f, 0f, 15f)) {
                            targetedSize = Math.Max(0f, targetedSize);
                            this.plugin.Config.TargetedSize = targetedSize;
                            this.plugin.Config.Save();
                        }

                        ImGui.Spacing();

                        bool markTargeting = this.plugin.Config.MarkTargeting;
                        if (ImGui.Checkbox("Mark targeting you", ref markTargeting)) {
                            this.plugin.Config.MarkTargeting = markTargeting;
                            this.plugin.Config.Save();
                        }

                        Vector4 targetingColour = this.plugin.Config.TargetingColour;
                        if (ImGui.ColorEdit4("Targeting mark colour", ref targetingColour)) {
                            this.plugin.Config.TargetingColour = targetingColour;
                            this.plugin.Config.Save();
                        }

                        float targetingSize = this.plugin.Config.TargetingSize;
                        if (ImGui.DragFloat("Targeting mark size", ref targetingSize, 0.01f, 0f, 15f)) {
                            targetingSize = Math.Max(0f, targetingSize);
                            this.plugin.Config.TargetingSize = targetingSize;
                            this.plugin.Config.Save();
                        }

                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Filters")) {
                        bool showParty = this.plugin.Config.LogParty;
                        if (ImGui.Checkbox("Log party members", ref showParty)) {
                            this.plugin.Config.LogParty = showParty;
                            this.plugin.Config.Save();
                        }

                        bool logAlliance = this.plugin.Config.LogAlliance;
                        if (ImGui.Checkbox("Log alliance members", ref logAlliance)) {
                            this.plugin.Config.LogAlliance = logAlliance;
                            this.plugin.Config.Save();
                        }

                        bool logInCombat = this.plugin.Config.LogInCombat;
                        if (ImGui.Checkbox("Log targeters engaged in combat", ref logInCombat)) {
                            this.plugin.Config.LogInCombat = logInCombat;
                            this.plugin.Config.Save();
                        }

                        bool logSelf = this.plugin.Config.LogSelf;
                        if (ImGui.Checkbox("Log yourself", ref logSelf)) {
                            this.plugin.Config.LogSelf = logSelf;
                            this.plugin.Config.Save();
                        }

                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Behaviour")) {
                        bool focusTarget = this.plugin.Config.FocusTargetOnHover;
                        if (ImGui.Checkbox("Focus target on hover", ref focusTarget)) {
                            this.plugin.Config.FocusTargetOnHover = focusTarget;
                            this.plugin.Config.Save();
                        }

                        bool playSound = this.plugin.Config.PlaySoundOnTarget;
                        if (ImGui.Checkbox("Play sound when targeted", ref playSound)) {
                            this.plugin.Config.PlaySoundOnTarget = playSound;
                            this.plugin.Config.Save();
                        }

                        string path = this.plugin.Config.SoundPath ?? "";
                        if (ImGui.InputText("Path to WAV file", ref path, 1_000)) {
                            path = path.Trim();
                            this.plugin.Config.SoundPath = path.Length == 0 ? null : path;
                            this.plugin.Config.Save();
                        }

                        ImGui.Text("Leave this blank to use a built-in sound.");

                        float soundCooldown = this.plugin.Config.SoundCooldown;
                        if (ImGui.DragFloat("Cooldown for sound (seconds)", ref soundCooldown, .01f, 0f, 30f)) {
                            soundCooldown = Math.Max(0f, soundCooldown);
                            this.plugin.Config.SoundCooldown = soundCooldown;
                            this.plugin.Config.Save();
                        }

                        bool playWhenClosed = this.plugin.Config.PlaySoundWhenClosed;
                        if (ImGui.Checkbox("Play sound when window is closed", ref playWhenClosed)) {
                            this.plugin.Config.PlaySoundWhenClosed = playWhenClosed;
                            this.plugin.Config.Save();
                        }

                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Window")) {
                        bool openOnLogin = this.plugin.Config.OpenOnLogin;
                        if (ImGui.Checkbox("Open on login", ref openOnLogin)) {
                            this.plugin.Config.OpenOnLogin = openOnLogin;
                            this.plugin.Config.Save();
                        }

                        bool allowMovement = this.plugin.Config.AllowMovement;
                        if (ImGui.Checkbox("Allow moving the main window", ref allowMovement)) {
                            this.plugin.Config.AllowMovement = allowMovement;
                            this.plugin.Config.Save();
                        }

                        ImGui.Spacing();

                        bool showInCombat = this.plugin.Config.ShowInCombat;
                        if (ImGui.Checkbox("Show window while in combat", ref showInCombat)) {
                            this.plugin.Config.ShowInCombat = showInCombat;
                            this.plugin.Config.Save();
                        }

                        bool showInInstance = this.plugin.Config.ShowInInstance;
                        if (ImGui.Checkbox("Show window while in instance", ref showInInstance)) {
                            this.plugin.Config.ShowInInstance = showInInstance;
                            this.plugin.Config.Save();
                        }

                        bool showInCutscenes = this.plugin.Config.ShowInCutscenes;
                        if (ImGui.Checkbox("Show window while in cutscenes", ref showInCutscenes)) {
                            this.plugin.Config.ShowInCutscenes = showInCutscenes;
                            this.plugin.Config.Save();
                        }

                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("History")) {
                        bool keepHistory = this.plugin.Config.KeepHistory;
                        if (ImGui.Checkbox("Show previous targeters", ref keepHistory)) {
                            this.plugin.Config.KeepHistory = keepHistory;
                            this.plugin.Config.Save();
                        }

                        bool historyWhenClosed = this.plugin.Config.HistoryWhenClosed;
                        if (ImGui.Checkbox("Record history when window is closed", ref historyWhenClosed)) {
                            this.plugin.Config.HistoryWhenClosed = historyWhenClosed;
                            this.plugin.Config.Save();
                        }

                        int numHistory = this.plugin.Config.NumHistory;
                        if (ImGui.InputInt("Number of previous targeters to keep", ref numHistory)) {
                            numHistory = Math.Max(0, Math.Min(10, numHistory));
                            this.plugin.Config.NumHistory = numHistory;
                            this.plugin.Config.Save();
                        }

                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Advanced")) {
                        int pollFrequency = this.plugin.Config.PollFrequency;
                        if (ImGui.DragInt("Poll frequency in milliseconds", ref pollFrequency, .1f, 1, 1600)) {
                            this.plugin.Config.PollFrequency = pollFrequency;
                            this.plugin.Config.Save();
                        }

                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Debug")) {
                        bool debugMarkers = this.plugin.Config.DebugMarkers;
                        if (ImGui.Checkbox("Debug markers", ref debugMarkers)) {
                            this.plugin.Config.DebugMarkers = debugMarkers;
                            this.plugin.Config.Save();
                        }

                        ImGui.Separator();

                        if (ImGui.Button("Log targeting you")) {
                            PlayerCharacter player = this.plugin.Interface.ClientState.LocalPlayer;
                            if (player != null) {
                                // loop over all players looking at the current player
                                var actors = this.plugin.Interface.ClientState.Actors
                                    .Where(actor => actor.TargetActorID == player.ActorId && actor is PlayerCharacter)
                                    .Select(actor => actor as PlayerCharacter);
                                foreach (PlayerCharacter actor in actors) {
                                    PlayerPayload payload = new PlayerPayload(this.plugin.Interface.Data, actor.Name, actor.HomeWorld.Id);
                                    Payload[] payloads = { payload };
                                    this.plugin.Interface.Framework.Gui.Chat.PrintChat(new XivChatEntry {
                                        MessageBytes = new SeString(payloads).Encode()
                                    });
                                }
                            }
                        }

                        if (ImGui.Button("Log your target")) {
                            PlayerCharacter target = GetCurrentTarget();

                            if (target != null) {
                                PlayerPayload payload = new PlayerPayload(this.plugin.Interface.Data, target.Name, target.HomeWorld.Id);
                                Payload[] payloads = { payload };
                                this.plugin.Interface.Framework.Gui.Chat.PrintChat(new XivChatEntry {
                                    MessageBytes = new SeString(payloads).Encode()
                                });
                            }
                        }

                        if (this.plugin.Interface.ClientState.LocalPlayer != null) {
                            PlayerCharacter player = this.plugin.Interface.ClientState.LocalPlayer;
                            IntPtr statusPtr = this.plugin.Interface.TargetModuleScanner.ResolveRelativeAddress(player.Address, 0x1901);
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
            IReadOnlyCollection<Targeter> targeting = this.plugin.Watcher.CurrentTargeters;
            IReadOnlyCollection<Targeter> previousTargeters = this.plugin.Config.KeepHistory ? this.plugin.Watcher.PreviousTargeters : null;

            // to prevent looping over a subset of the actors repeatedly when multiple people are targeting,
            // create a dictionary for O(1) lookups by actor id
            Dictionary<int, Actor> actors = null;
            if (targeting.Count + (previousTargeters?.Count ?? 0) > 1) {
                actors = this.plugin.Interface.ClientState.Actors
                    // only take into account players
                    .Where(actor => actor.ObjectKind == Dalamud.Game.ClientState.Actors.ObjectKind.Player)
                    .ToDictionary(actor => actor.ActorId);
            }
            
            ImGuiWindowFlags flags = ImGuiWindowFlags.AlwaysAutoResize;
            if (!this.plugin.Config.AllowMovement) {
                flags |= ImGuiWindowFlags.NoMove;
            }
            if (ImGui.Begin(this.plugin.Name, ref this.visible, flags)) {
                ImGui.Text("Targeting you");
                bool anyHovered = false;
                if (ImGui.ListBoxHeader("##targeting", targeting.Count, 5)) {
                    // add the two first players for testing
                    //foreach (PlayerCharacter p in this.plugin.Interface.ClientState.Actors
                    //    .Where(actor => actor is PlayerCharacter)
                    //    .Skip(1)
                    //    .Select(actor => actor as PlayerCharacter)
                    //    .Take(2)) {
                    //    this.AddEntry(new Targeter(p), p, ref anyHovered);
                    //}
                    foreach (Targeter targeter in targeting) {
                        Actor actor = null;
                        actors?.TryGetValue(targeter.ActorId, out actor);
                        this.AddEntry(targeter, actor, ref anyHovered);
                    }
                    if (this.plugin.Config.KeepHistory) {
                        // get a list of the previous targeters that aren't currently targeting
                        Targeter[] previous = previousTargeters
                            .Where(old => targeting.All(actor => actor.ActorId != old.ActorId))
                            .Take(this.plugin.Config.NumHistory)
                            .ToArray();
                        // add previous targeters to the list
                        foreach (Targeter oldTargeter in previous) {
                            Actor actor = null;
                            actors?.TryGetValue(oldTargeter.ActorId, out actor);
                            this.AddEntry(oldTargeter, actor, ref anyHovered, ImGuiSelectableFlags.Disabled);
                        }
                    }
                    ImGui.ListBoxFooter();
                }
                if (this.plugin.Config.FocusTargetOnHover && !anyHovered && this.previousFocus.Get(out Actor previousFocus)) {
                    if (previousFocus == null) {
                        this.plugin.Interface.ClientState.Targets.SetFocusTarget(null);
                    } else {
                        Actor actor = this.plugin.Interface.ClientState.Actors.FirstOrDefault(a => a.ActorId == previousFocus.ActorId);
                        // either target the actor if still present or target nothing
                        this.plugin.Interface.ClientState.Targets.SetFocusTarget(actor);
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
                actor = this.plugin.Interface.ClientState.Actors
                    .Where(a => a.ActorId == targeter.ActorId)
                    .FirstOrDefault();
            }

            // don't count as hovered if the actor isn't here (clears focus target when hovering missing actors)
            if (actor != null) {
                anyHovered |= hover;
            }

            if (this.plugin.Config.FocusTargetOnHover && hover && actor != null) {
                if (!this.previousFocus.Present) {
                    this.previousFocus = new Optional<Actor>(this.plugin.Interface.ClientState.Targets.FocusTarget);
                }
                this.plugin.Interface.ClientState.Targets.SetFocusTarget(actor);
            }

            if (left) {
                PlayerPayload payload = new PlayerPayload(this.plugin.Interface.Data, targeter.Name, targeter.HomeWorld.Id);
                Payload[] payloads = { payload };
                this.plugin.Interface.Framework.Gui.Chat.PrintChat(new XivChatEntry {
                    MessageBytes = new SeString(payloads).Encode()
                });
            } else if (right && actor != null) {
                this.plugin.Interface.ClientState.Targets.SetCurrentTarget(actor);
            }
        }

        private void MarkPlayer(PlayerCharacter player, Vector4 colour, float size) {
            if (player == null) {
                return;
            }

            if (!this.plugin.Interface.Framework.Gui.WorldToScreen(player.Position, out SharpDX.Vector2 screenPos)) {
                return;
            }

            ImGuiWindowFlags flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoFocusOnAppearing;
            if (this.plugin.Config.DebugMarkers) {
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
            PlayerCharacter player = this.plugin.Interface.ClientState.LocalPlayer;
            if (player == null) {
                return null;
            }

            int targetId = player.TargetActorID;
            if (targetId <= 0) {
                return null;
            }

            return this.plugin.Interface.ClientState.Actors
                .Where(actor => actor.ActorId == targetId && actor is PlayerCharacter)
                .Select(actor => actor as PlayerCharacter)
                .FirstOrDefault();
        }
    }
}
