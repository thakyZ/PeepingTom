﻿using Dalamud.Game.Chat;
using Dalamud.Game.Chat.SeStringHandling;
using Dalamud.Game.Chat.SeStringHandling.Payloads;
using Dalamud.Game.ClientState.Actors.Types;
using Dalamud.Game.Internal;
using Dalamud.Plugin;
using NAudio.Wave;
using Resourcer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace PeepingTom {
    internal class TargetWatcher : IDisposable {
        private PeepingTomPlugin Plugin { get; }

        private Stopwatch? Watch { get; set; }
        private int LastTargetAmount { get; set; }

        private volatile bool _stop;
        private volatile bool _needsUpdate = true;
        private Thread? Thread { get; set; }

        private readonly object _dataMutex = new object();
        private TargetThreadData? Data { get; set; }

        private readonly Mutex _currentMutex = new Mutex();
        private Targeter[] Current { get; set; } = Array.Empty<Targeter>();

        public IReadOnlyCollection<Targeter> CurrentTargeters {
            get {
                this._currentMutex.WaitOne();
                var current = this.Current.ToArray();
                this._currentMutex.ReleaseMutex();
                return current;
            }
        }

        private readonly Mutex _previousMutex = new Mutex();
        private List<Targeter> Previous { get; } = new List<Targeter>();

        public IReadOnlyCollection<Targeter> PreviousTargeters {
            get {
                this._previousMutex.WaitOne();
                var previous = this.Previous.ToArray();
                this._previousMutex.ReleaseMutex();
                return previous;
            }
        }

        public TargetWatcher(PeepingTomPlugin plugin) {
            this.Plugin = plugin ?? throw new ArgumentNullException(nameof(plugin), "PeepingTomPlugin cannot be null");
        }

        public void ClearPrevious() {
            this._previousMutex.WaitOne();
            this.Previous.Clear();
            this._previousMutex.ReleaseMutex();
        }

        public void StartThread() {
            this.Thread = new Thread(() => {
                while (!this._stop) {
                    this.Update();
                    this._needsUpdate = true;
                    Thread.Sleep(this.Plugin.Config.PollFrequency);
                }
            });
            this.Thread.Start();
        }

        public void WaitStopThread() {
            this._stop = true;
            this.Thread?.Join();
        }

        public void OnFrameworkUpdate(Framework framework) {
            if (!this._needsUpdate) {
                return;
            }

            lock (this._dataMutex) {
                this.Data = new TargetThreadData(this.Plugin.Interface);
            }

            this._needsUpdate = false;
        }

        private void Update() {
            lock (this._dataMutex) {
                var player = this.Data?.LocalPlayer;
                if (player == null) {
                    return;
                }

                // block until lease
                this._currentMutex.WaitOne();

                // get targeters and set a copy so we can release the mutex faster
                var current = this.GetTargeting(this.Data!.Actors, player);
                this.Current = (Targeter[]) current.Clone();

                // release
                this._currentMutex.ReleaseMutex();
            }

            this.HandleHistory(this.Current);

            // play sound if necessary
            if (this.CanPlaySound()) {
                this.Watch?.Restart();
                this.PlaySound();
            }

            this.LastTargetAmount = this.Current.Length;
        }

        private void HandleHistory(Targeter[] targeting) {
            if (!this.Plugin.Config.KeepHistory || (!this.Plugin.Config.HistoryWhenClosed && !this.Plugin.Ui.Visible)) {
                return;
            }

            this._previousMutex.WaitOne();

            foreach (var targeter in targeting) {
                // add the targeter to the previous list
                if (this.Previous.Any(old => old.ActorId == targeter.ActorId)) {
                    this.Previous.RemoveAll(old => old.ActorId == targeter.ActorId);
                }

                this.Previous.Insert(0, targeter);
            }

            // only keep the configured number of previous targeters (ignoring ones that are currently targeting)
            while (this.Previous.Count(old => targeting.All(actor => actor.ActorId != old.ActorId)) > this.Plugin.Config.NumHistory) {
                this.Previous.RemoveAt(this.Previous.Count - 1);
            }

            this._previousMutex.ReleaseMutex();
        }

        private Targeter[] GetTargeting(IEnumerable<Actor> actors, Actor player) {
            return actors
                .Where(actor => actor.TargetActorID == player.ActorId && actor is PlayerCharacter)
                .Cast<PlayerCharacter>()
                .Where(actor => this.Plugin.Config.LogParty || !InParty(actor))
                .Where(actor => this.Plugin.Config.LogAlliance || !InAlliance(actor))
                .Where(actor => this.Plugin.Config.LogInCombat || !InCombat(actor))
                .Where(actor => this.Plugin.Config.LogSelf || actor.ActorId != player.ActorId)
                .Select(actor => new Targeter(actor))
                .ToArray();
        }

        private static byte GetStatus(Actor actor) {
            var statusPtr = actor.Address + 0x1906; // updated 5.3
            return Marshal.ReadByte(statusPtr);
        }

        private static bool InCombat(Actor actor) => (GetStatus(actor) & 2) > 0;

        private static bool InParty(Actor actor) => (GetStatus(actor) & 16) > 0;

        private static bool InAlliance(Actor actor) => (GetStatus(actor) & 32) > 0;

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

            if (this.Watch == null) {
                this.Watch = new Stopwatch();
                return true;
            }

            var secs = this.Watch.Elapsed.TotalSeconds;
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
                    #pragma warning disable CA1031 // Do not catch general exception types
                } catch (Exception e) {
                    #pragma warning restore CA1031 // Do not catch general exception types
                    this.SendError($"Could not play sound file: {e.Message}");
                    return;
                }

                using WaveChannel32 channel = new WaveChannel32(reader) {
                    Volume = this.Plugin.Config.SoundVolume,
                };

                using (reader) {
                    using var output = new WaveOutEvent {DeviceNumber = soundDevice};
                    output.Init(channel);
                    output.Play();

                    while (output.PlaybackState == PlaybackState.Playing) {
                        Thread.Sleep(500);
                    }
                }
            }).Start();
        }

        private void SendError(string message) {
            Payload[] payloads = {
                new TextPayload($"[{this.Plugin.Name}] {message}"),
            };
            this.Plugin.Interface.Framework.Gui.Chat.PrintChat(new XivChatEntry {MessageBytes = new SeString(payloads).Encode(), Type = XivChatType.ErrorMessage,});
        }

        public void Dispose() {
            this._currentMutex.Dispose();
            this._previousMutex.Dispose();
        }
    }

    internal class TargetThreadData {
        public PlayerCharacter LocalPlayer { get; }
        public Actor[] Actors { get; }

        public TargetThreadData(DalamudPluginInterface pi) {
            this.LocalPlayer = pi.ClientState.LocalPlayer;
            this.Actors = pi.ClientState.Actors.ToArray();
        }
    }
}
