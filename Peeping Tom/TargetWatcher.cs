using Dalamud.Game.Chat;
using Dalamud.Game.Chat.SeStringHandling;
using Dalamud.Game.Chat.SeStringHandling.Payloads;
using Dalamud.Game.ClientState.Actors.Types;
using Dalamud.Game.Internal;
using Dalamud.Plugin;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace PeepingTom {
    class TargetWatcher : IDisposable {
        private readonly PeepingTomPlugin plugin;

        private Stopwatch watch = null;
        private int lastTargetAmount = 0;

        private volatile bool stop = false;
        private volatile bool needsUpdate = true;
        private Thread thread;

        private readonly object dataMutex = new object();
        private TargetThreadData data;

        private readonly Mutex currentMutex = new Mutex();
        private Targeter[] current = Array.Empty<Targeter>();
        public IReadOnlyCollection<Targeter> CurrentTargeters {
            get {
                this.currentMutex.WaitOne();
                Targeter[] current = this.current.ToArray();
                this.currentMutex.ReleaseMutex();
                return current;
            }
        }

        private readonly Mutex previousMutex = new Mutex();
        private readonly List<Targeter> previousTargeters = new List<Targeter>();
        public IReadOnlyCollection<Targeter> PreviousTargeters {
            get {
                this.previousMutex.WaitOne();
                Targeter[] previous = this.previousTargeters.ToArray();
                this.previousMutex.ReleaseMutex();
                return previous;
            }
        }

        public TargetWatcher(PeepingTomPlugin plugin) {
            this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin), "PeepingTomPlugin cannot be null");
        }

        public void ClearPrevious() {
            this.previousMutex.WaitOne();
            this.previousTargeters.Clear();
            this.previousMutex.ReleaseMutex();
        }

        public void StartThread() {
            this.thread = new Thread(new ThreadStart(() => {
                while (!this.stop) {
                    this.Update();
                    this.needsUpdate = true;
                    Thread.Sleep(this.plugin.Config.PollFrequency);
                }
            }));
            this.thread.Start();
        }

        public void WaitStopThread() {
            this.stop = true;
            this.thread?.Join();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "delegate")]
        public void OnFrameworkUpdate(Framework framework) {
            if (!this.needsUpdate) {
                return;
            }

            lock (this.dataMutex) {
                this.data = new TargetThreadData(this.plugin.Interface);
            }
            this.needsUpdate = false;
        }

        private void Update() {
            lock (this.dataMutex) {
                if (this.data == null) {
                    return;
                }

                PlayerCharacter player = this.data.localPlayer;
                if (player == null) {
                    return;
                }

                // block until lease
                this.currentMutex.WaitOne();

                // get targeters and set a copy so we can release the mutex faster
                Targeter[] current = this.GetTargeting(this.data.actors, player);
                this.current = (Targeter[])current.Clone();

                // release
                this.currentMutex.ReleaseMutex();
            }

            this.HandleHistory(current);

            // play sound if necessary
            if (this.CanPlaySound()) {
                this.watch.Restart();
                this.PlaySound();
            }
            this.lastTargetAmount = this.current.Length;
        }

        private void HandleHistory(Targeter[] targeting) {
            if (!this.plugin.Config.KeepHistory || (!this.plugin.Config.HistoryWhenClosed && !this.plugin.Ui.Visible)) {
                return;
            }

            this.previousMutex.WaitOne();

            foreach (Targeter targeter in targeting) {
                // add the targeter to the previous list
                if (this.previousTargeters.Any(old => old.ActorId == targeter.ActorId)) {
                    this.previousTargeters.RemoveAll(old => old.ActorId == targeter.ActorId);
                }
                this.previousTargeters.Insert(0, targeter);
            }

            // only keep the configured number of previous targeters (ignoring ones that are currently targeting)
            while (this.previousTargeters.Where(old => targeting.All(actor => actor.ActorId != old.ActorId)).Count() > this.plugin.Config.NumHistory) {
                this.previousTargeters.RemoveAt(this.previousTargeters.Count - 1);
            }

            this.previousMutex.ReleaseMutex();
        }

        private Targeter[] GetTargeting(Actor[] actors, Actor player) {
            return actors
                .Where(actor => actor.TargetActorID == player.ActorId && actor is PlayerCharacter)
                .Select(actor => actor as PlayerCharacter)
                .Where(actor => this.plugin.Config.LogParty || !InParty(actor))
                .Where(actor => this.plugin.Config.LogAlliance || !InAlliance(actor))
                .Where(actor => this.plugin.Config.LogInCombat || !InCombat(actor))
                .Where(actor => this.plugin.Config.LogSelf || actor.ActorId != player.ActorId)
                .Select(actor => new Targeter(actor))
                .ToArray();
        }

        private static byte GetStatus(Actor actor) {
            IntPtr statusPtr = actor.Address + 0x1906; // updated 5.3
            return Marshal.ReadByte(statusPtr);
        }

        private static bool InCombat(Actor actor) => (GetStatus(actor) & 2) > 0;

        private static bool InParty(Actor actor) => (GetStatus(actor) & 16) > 0;

        private static bool InAlliance(Actor actor) => (GetStatus(actor) & 32) > 0;

        private bool CanPlaySound() {
            if (!this.plugin.Config.PlaySoundOnTarget) {
                return false;
            }

            if (this.current.Length <= this.lastTargetAmount) {
                return false;
            }

            if (!this.plugin.Config.PlaySoundWhenClosed && !this.plugin.Ui.Visible) {
                return false;
            }

            if (this.watch == null) {
                this.watch = new Stopwatch();
                return true;
            }

            double secs = this.watch.Elapsed.TotalSeconds;
            return secs >= this.plugin.Config.SoundCooldown;
        }

        private void PlaySound() {
            int soundDevice = this.plugin.Config.SoundDevice;
            if (soundDevice < -1 || soundDevice > WaveOut.DeviceCount) {
                soundDevice = -1;
            }

            new Thread(new ThreadStart(() => {
                WaveStream reader;
                try {
                    if (this.plugin.Config.SoundPath == null) {
                        reader = new WaveFileReader(Properties.Resources.Target);
                    } else {
                        reader = new AudioFileReader(this.plugin.Config.SoundPath);
                    }
#pragma warning disable CA1031 // Do not catch general exception types
                } catch (Exception e) {
#pragma warning restore CA1031 // Do not catch general exception types
                    this.SendError($"Could not play sound file: {e.Message}");
                    return;
                }

                WaveChannel32 channel = new WaveChannel32(reader) {
                    Volume = this.plugin.Config.SoundVolume,
                };

                using (reader) {
                    using (var output = new WaveOutEvent() { DeviceNumber = soundDevice }) {
                        output.Init(channel);
                        output.Play();

                        while (output.PlaybackState == PlaybackState.Playing) {
                            Thread.Sleep(500);
                        }
                    }
                }
            })).Start();
        }

        private void SendError(string message) {
            Payload[] payloads = { new TextPayload($"[{this.plugin.Name}] {message}") };
            this.plugin.Interface.Framework.Gui.Chat.PrintChat(new XivChatEntry {
                MessageBytes = new SeString(payloads).Encode(),
                Type = XivChatType.ErrorMessage,
            });
        }

        public void Dispose() {
            this.currentMutex.Dispose();
            this.previousMutex.Dispose();
        }
    }

    class TargetThreadData {
        public PlayerCharacter localPlayer;
        public Actor[] actors;

        public TargetThreadData(DalamudPluginInterface pi) {
            this.localPlayer = pi.ClientState.LocalPlayer;
            this.actors = pi.ClientState.Actors.ToArray();
        }
    }
}
