using NAudio.Wave;
using Resourcer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Dalamud.Game;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text;
using Dalamud.Logging;
using PeepingTom.Ipc;
using PeepingTom.Resources;

namespace PeepingTom {
    internal class TargetWatcher : IDisposable {
        private PeepingTomPlugin Plugin { get; }

        private Stopwatch UpdateWatch { get; } = new();
        private Stopwatch? SoundWatch { get; set; }
        private int LastTargetAmount { get; set; }

        private Targeter[] Current { get; set; } = Array.Empty<Targeter>();

        public IReadOnlyCollection<Targeter> CurrentTargeters => this.Current;

        private List<Targeter> Previous { get; } = new();

        public IReadOnlyCollection<Targeter> PreviousTargeters => this.Previous;

        public TargetWatcher(PeepingTomPlugin plugin) {
            this.Plugin = plugin;
            this.UpdateWatch.Start();

            this.Plugin.Framework.Update += this.OnFrameworkUpdate;
        }

        public void Dispose() {
            this.Plugin.Framework.Update -= this.OnFrameworkUpdate;
        }

        public void ClearPrevious() {
            this.Previous.Clear();
        }

        private void OnFrameworkUpdate(Framework framework) {
            if (this.Plugin.InPvp) {
                return;
            }

            if (this.UpdateWatch.Elapsed > TimeSpan.FromMilliseconds(this.Plugin.Config.PollFrequency)) {
                this.Update();
            }
        }

        private void Update() {
            var player = this.Plugin.ClientState.LocalPlayer;
            if (player == null) {
                return;
            }

            // get targeters and set a copy so we can release the mutex faster
            var newCurrent = this.GetTargeting(this.Plugin.ObjectTable, player);

            foreach (var newTargeter in newCurrent.Where(t => this.Current.All(c => c.ObjectId != t.ObjectId))) {
                try {
                    this.Plugin.IpcManager.SendNewTargeter(newTargeter);
                } catch (Exception ex) {
                    PluginLog.LogError(ex, "Failed to send IPC message");
                }
            }

            foreach (var stopped in this.Current.Where(t => newCurrent.All(c => c.ObjectId != t.ObjectId))) {
                try {
                    this.Plugin.IpcManager.SendStoppedTargeting(stopped);
                } catch (Exception ex) {
                    PluginLog.LogError(ex, "Failed to send IPC message");
                }
            }

            this.Current = newCurrent;

            this.HandleHistory(this.Current);

            // play sound if necessary
            if (this.CanPlaySound()) {
                this.SoundWatch?.Restart();
                this.PlaySound();
            }

            this.LastTargetAmount = this.Current.Length;
        }

        private void HandleHistory(Targeter[] targeting) {
            if (!this.Plugin.Config.KeepHistory || !this.Plugin.Config.HistoryWhenClosed && !this.Plugin.Ui.Visible) {
                return;
            }

            foreach (var targeter in targeting) {
                // add the targeter to the previous list
                if (this.Previous.Any(old => old.ObjectId == targeter.ObjectId)) {
                    this.Previous.RemoveAll(old => old.ObjectId == targeter.ObjectId);
                }

                this.Previous.Insert(0, targeter);
            }

            // only keep the configured number of previous targeters (ignoring ones that are currently targeting)
            while (this.Previous.Count(old => targeting.All(actor => actor.ObjectId != old.ObjectId)) > this.Plugin.Config.NumHistory) {
                this.Previous.RemoveAt(this.Previous.Count - 1);
            }
        }

        private Targeter[] GetTargeting(IEnumerable<GameObject> objects, GameObject player) {
            return objects
                .Where(obj => obj.TargetObjectId == player.ObjectId && obj is PlayerCharacter)
                // .Where(obj => Marshal.ReadByte(obj.Address + ActorOffsets.PlayerCharacterTargetActorId + 4) == 0)
                .Cast<PlayerCharacter>()
                .Where(actor => this.Plugin.Config.LogParty || !InParty(actor))
                .Where(actor => this.Plugin.Config.LogAlliance || !InAlliance(actor))
                .Where(actor => this.Plugin.Config.LogInCombat || !InCombat(actor))
                .Where(actor => this.Plugin.Config.LogSelf || actor.ObjectId != player.ObjectId)
                .Select(actor => new Targeter(actor))
                .ToArray();
        }

        private static byte GetStatus(GameObject actor) {
            var statusPtr = actor.Address + 0x1980; // updated 5.4
            return Marshal.ReadByte(statusPtr);
        }

        private static bool InCombat(GameObject actor) => (GetStatus(actor) & 2) > 0;

        private static bool InParty(GameObject actor) => (GetStatus(actor) & 16) > 0;

        private static bool InAlliance(GameObject actor) => (GetStatus(actor) & 32) > 0;

        private bool CanPlaySound() {
            if (!this.Plugin.Config.PlaySoundOnTarget) {
                return false;
            }

            if (this.Current.Length <= this.LastTargetAmount) {
                return false;
            }

            if (!this.Plugin.Config.PlaySoundWhenClosed && !this.Plugin.Ui.Visible) {
                return false;
            }

            if (this.SoundWatch == null) {
                this.SoundWatch = new Stopwatch();
                return true;
            }

            var secs = this.SoundWatch.Elapsed.TotalSeconds;
            return secs >= this.Plugin.Config.SoundCooldown;
        }

        private void PlaySound() {
            var soundDevice = this.Plugin.Config.SoundDevice;
            if (soundDevice < -1 || soundDevice > WaveOut.DeviceCount) {
                soundDevice = -1;
            }

            new Thread(() => {
                WaveStream reader;
                try {
                    if (this.Plugin.Config.SoundPath == null) {
                        reader = new WaveFileReader(Resource.AsStream("Resources/target.wav"));
                    } else {
                        reader = new AudioFileReader(this.Plugin.Config.SoundPath);
                    }
                } catch (Exception e) {
                    var error = string.Format(Language.SoundChatError, e.Message);
                    this.SendError(error);
                    return;
                }

                using var channel = new WaveChannel32(reader) {
                    Volume = this.Plugin.Config.SoundVolume,
                    PadWithZeroes = false,
                };

                using (reader) {
                    using var output = new WaveOutEvent {
                        DeviceNumber = soundDevice,
                    };
                    output.Init(channel);
                    output.Play();

                    while (output.PlaybackState == PlaybackState.Playing) {
                        Thread.Sleep(500);
                    }
                }
            }).Start();
        }

        private void SendError(string message) {
            this.Plugin.ChatGui.PrintChat(new XivChatEntry {
                Message = $"[{this.Plugin.Name}] {message}",
                Type = XivChatType.ErrorMessage,
            });
        }
    }
}
