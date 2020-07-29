using Dalamud.Game.Chat;
using Dalamud.Game.Chat.SeStringHandling;
using Dalamud.Game.Chat.SeStringHandling.Payloads;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Actors.Types;
using Dalamud.Plugin;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Media;
using System.Numerics;
using System.Runtime.InteropServices;

namespace PeepingTom {
    class PluginUI : IDisposable {
        private readonly PeepingTomPlugin plugin;
        private readonly Configuration config;
        private readonly DalamudPluginInterface pi;

        private readonly List<Targeter> previousTargeters = new List<Targeter>();
        private IntPtr? previousFocus = null;
        private long soundLastPlayed = 0;
        private int lastTargetAmount = 0;

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

        public PluginUI(PeepingTomPlugin plugin, Configuration config, DalamudPluginInterface pluginInterface) {
            this.plugin = plugin;
            this.config = config;
            this.pi = pluginInterface;
        }

        public void Dispose() {
            this.Visible = false;
            this.SettingsVisible = false;
            this.previousTargeters.Clear();
        }

        public void Draw() {
            if (this.SettingsVisible) {
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

                            ImGui.EndTabItem();
                        }

                        if (ImGui.BeginTabItem("Visibility")) {
                            // TODO: this needs somewhere better to live in the settings
                            bool showInCombat = this.config.ShowInCombat;
                            if (ImGui.Checkbox("Show window while in combat", ref showInCombat)) {
                                this.config.ShowInCombat = showInCombat;
                            }

                            bool showInInstance = this.config.ShowInInstance;
                            if (ImGui.Checkbox("Show window while in instance", ref showInInstance)) {
                                this.config.ShowInInstance = showInInstance;
                            }

                            bool showInCutscenes = this.config.ShowInCutscenes;
                            if (ImGui.Checkbox("Show window while in cutscenes", ref showInCutscenes)) {
                                this.config.ShowInCutscenes = showInCutscenes;
                            }

                            ImGui.EndTabItem();
                        }

                        if (ImGui.BeginTabItem("History")) {
                            bool keepHistory = this.config.KeepHistory;
                            if (ImGui.Checkbox("Show previous targeters", ref keepHistory)) {
                                this.config.KeepHistory = keepHistory;
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
                                        PlayerPayload payload = new PlayerPayload(actor.Name, actor.HomeWorld.Id);
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
                                    PlayerPayload payload = new PlayerPayload(target.Name, target.HomeWorld.Id);
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

            bool inCombat = this.pi.ClientState.Condition[ConditionFlag.InCombat];
            bool inInstance = this.pi.ClientState.Condition[ConditionFlag.BoundByDuty];
            bool inCutscene = this.pi.ClientState.Condition[ConditionFlag.WatchingCutscene];

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
                PlayerCharacter player = this.pi.ClientState.LocalPlayer;
                if (player != null) {
                    PlayerCharacter[] targeting = this.GetTargeting(player);
                    if (this.config.PlaySoundOnTarget && targeting.Length > this.lastTargetAmount && this.CanPlaySound()) {
                        this.soundLastPlayed = Stopwatch.GetTimestamp();
                        this.PlaySound();
                    }
                    this.lastTargetAmount = targeting.Length;
                    if (ImGui.Begin(this.plugin.Name, ref this.visible, ImGuiWindowFlags.AlwaysAutoResize)) {
                        ImGui.Text("Targeting you");
                        if (ImGui.ListBoxHeader("##targeting", targeting.Length, 5)) {
                            // add the two first players for testing
                            //foreach (PlayerCharacter p in this.pi.ClientState.Actors
                            //    .Where(actor => actor is PlayerCharacter)
                            //    .Skip(1)
                            //    .Select(actor => actor as PlayerCharacter)
                            //    .Take(2)) {
                            //    this.AddEntry(p);
                            //}
                            foreach (PlayerCharacter targeter in targeting) {
                                if (this.config.KeepHistory) {
                                    // add the targeter to the previous list
                                    if (this.previousTargeters.Any(old => old.ActorId == targeter.ActorId)) {
                                        this.previousTargeters.RemoveAll(old => old.ActorId == targeter.ActorId);
                                    }
                                    this.previousTargeters.Insert(0, new Targeter(targeter));
                                }
                                this.AddEntry(new Targeter(targeter), targeter.Address);
                            }
                            if (this.config.KeepHistory) {
                                // only keep the configured number of previous targeters (ignoring ones that are currently targeting)
                                while (this.previousTargeters.Where(old => targeting.All(actor => actor.ActorId != old.ActorId)).Count() > this.config.NumHistory) {
                                    this.previousTargeters.RemoveAt(this.previousTargeters.Count - 1);
                                }
                                // get a list of the previous targeters that aren't currently targeting
                                Targeter[] previous = this.previousTargeters
                                    .Where(old => targeting.All(actor => actor.ActorId != old.ActorId))
                                    .Take(this.config.NumHistory)
                                    .ToArray();
                                // add previous targeters to the list
                                foreach (Targeter oldTargeter in previous) {
                                    this.AddEntry(oldTargeter, null, ImGuiSelectableFlags.Disabled);
                                }
                            }
                            ImGui.ListBoxFooter();
                        }
                        if (this.config.FocusTargetOnHover && !ImGui.IsAnyItemHovered() && this.previousFocus != null) {
                            // old focus target still here
                            if (this.pi.ClientState.Actors.Any(actor => actor.Address == this.previousFocus)) {
                                this.FocusTarget((IntPtr)this.previousFocus);
                            } else {
                                this.FocusTarget(IntPtr.Zero);
                            }
                            this.previousFocus = null;
                        }
                        ImGui.Text("Click to link or right click to target.");
                        ImGui.End();
                    }
                }
            }

            if (this.config.MarkTargeted) {
                PlayerCharacter target = GetCurrentTarget();
                MarkPlayer(GetCurrentTarget(), this.config.TargetedColour, this.config.TargetedSize);
            }

            if (this.config.MarkTargeting) {
                PlayerCharacter player = this.pi.ClientState.LocalPlayer;
                if (player != null) {
                    PlayerCharacter[] targeting = this.GetTargeting(player);
                    foreach (PlayerCharacter targeter in targeting) {
                        MarkPlayer(targeter, this.config.TargetingColour, this.config.TargetingSize);
                    }
                }
            }
        }

        

        private void AddEntry(Targeter targeter, IntPtr? address, ImGuiSelectableFlags flags = ImGuiSelectableFlags.None) {
            ImGui.Selectable(targeter.Name, false, flags);
            bool hover = ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled);
            bool left = hover && ImGui.IsMouseClicked(0);
            bool right = hover && ImGui.IsMouseClicked(1);
            if (address == null) {
                address = this.pi.ClientState.Actors
                    .Where(actor => actor.ActorId == targeter.ActorId)
                    .FirstOrDefault()
                    ?.Address;
            }

            if (this.config.FocusTargetOnHover && hover && address != null) {
                if (this.previousFocus == null) {
                    this.previousFocus = this.GetRawFocusTarget();
                }
                this.FocusTarget(address.Value);
            }

            if (left) {
                PlayerPayload payload = new PlayerPayload(targeter.Name, targeter.HomeWorld.Id);
                Payload[] payloads = { payload };
                this.pi.Framework.Gui.Chat.PrintChat(new XivChatEntry {
                    MessageBytes = new SeString(payloads).Encode()
                });
            } else if (right && address != null) {
                this.Target(address.Value);
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

        private void Target(IntPtr actor) {
            IntPtr targetPtr = this.pi.TargetModuleScanner.ResolveRelativeAddress(this.pi.TargetModuleScanner.Module.BaseAddress, 0x1c64150);
            Marshal.WriteIntPtr(targetPtr, actor);
        }

        private void FocusTarget(IntPtr ptr) {
            IntPtr targetPtr = this.pi.TargetModuleScanner.ResolveRelativeAddress(this.pi.TargetModuleScanner.Module.BaseAddress, 0x1c641c8);
            Marshal.WriteIntPtr(targetPtr, ptr);
        }

        private IntPtr GetRawFocusTarget() {
            IntPtr targetPtr = this.pi.TargetModuleScanner.ResolveRelativeAddress(this.pi.TargetModuleScanner.Module.BaseAddress, 0x1c641c8);
            return Marshal.ReadIntPtr(targetPtr);
        }

        private bool CanPlaySound() {
            if (this.soundLastPlayed == 0) {
                return true;
            }

            long current = Stopwatch.GetTimestamp();
            long diff = current - this.soundLastPlayed;
            // only play every 10 seconds?
            float secs = (float)diff / Stopwatch.Frequency;
            return secs >= this.config.SoundCooldown;
        }

        private void PlaySound() {
            SoundPlayer player;
            if (this.config.SoundPath == null) {
                player = new SoundPlayer(Properties.Resources.Target);
            } else {
                player = new SoundPlayer(this.config.SoundPath);
            }
            try {
                player.Play();
            } catch (FileNotFoundException e) {
                this.SendError($"Could not play sound: {e.Message}");
            } catch (InvalidOperationException e) {
                this.SendError($"Could not play sound: {e.Message}");
            } finally {
                player.Dispose();
            }
        }

        private void SendError(string message) {
            Payload[] payloads = { new TextPayload($"[Who's Looking] {message}") };
            this.pi.Framework.Gui.Chat.PrintChat(new XivChatEntry {
                MessageBytes = new SeString(payloads).Encode(),
                Type = XivChatType.ErrorMessage
            });
        }

        private byte GetStatus(Actor actor) {
            IntPtr statusPtr = this.pi.TargetModuleScanner.ResolveRelativeAddress(actor.Address, 0x1901);
            return Marshal.ReadByte(statusPtr);
        }

        private bool InCombat(Actor actor) {
            return (GetStatus(actor) & 2) > 0;
        }

        private bool InAlliance(Actor actor) {
            return (GetStatus(actor) & 32) > 0;
        }

        private PlayerCharacter[] GetTargeting(Actor player) {
            return this.pi.ClientState.Actors
                .Where(actor => actor.TargetActorID == player.ActorId && actor is PlayerCharacter)
                .Select(actor => actor as PlayerCharacter)
                .Where(actor => this.config.LogParty || this.pi.ClientState.PartyList.All(member => member.Actor.ActorId != actor.ActorId))
                .Where(actor => this.config.LogAlliance || !this.InAlliance(actor))
                .Where(actor => this.config.LogInCombat || !this.InCombat(actor))
                .ToArray();
        }
    }
}
