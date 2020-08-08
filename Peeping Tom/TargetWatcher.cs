using Dalamud.Game.Chat;
using Dalamud.Game.Chat.SeStringHandling;
using Dalamud.Game.Chat.SeStringHandling.Payloads;
using Dalamud.Game.ClientState.Actors.Types;
using Dalamud.Game.Internal;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PeepingTom {
    class TargetWatcher : IDisposable {
        private readonly DalamudPluginInterface pi;
        private readonly Configuration config;

        private long soundLastPlayed = 0;
        private int lastTargetAmount = 0;

        public readonly Mutex currentMutex = new Mutex();
        private PlayerCharacter[] current = Array.Empty<PlayerCharacter>();
        public IReadOnlyCollection<PlayerCharacter> CurrentTargeters {
            get {
                this.currentMutex.WaitOne();
                PlayerCharacter[] current = (PlayerCharacter[])this.current.Clone();
                this.currentMutex.ReleaseMutex();
                return current;
            }
        }

        public readonly Mutex previousMutex = new Mutex();
        private readonly List<Targeter> previousTargeters = new List<Targeter>();
        public IReadOnlyCollection<Targeter> PreviousTargeters {
            get {
                this.previousMutex.WaitOne();
                Targeter[] previous = this.previousTargeters.ToArray();
                this.previousMutex.ReleaseMutex();
                return previous;
            }
        }

        public TargetWatcher(DalamudPluginInterface pi, Configuration config) {
            this.pi = pi ?? throw new ArgumentNullException(nameof(pi), "DalamudPluginInterface cannot be null");
            this.config = config ?? throw new ArgumentNullException(nameof(config), "Configuration cannot be null");
        }

        public Out WithCurrent<Out>(Func<IReadOnlyCollection<PlayerCharacter>, Out> func) {
            this.currentMutex.WaitOne();
            Out output = func(this.current);
            this.currentMutex.ReleaseMutex();
            return output;
        }

        public void OnFrameworkUpdate(Framework framework) {
            PlayerCharacter player = this.pi.ClientState.LocalPlayer;
            if (player == null) {
                return;
            }

            // block until lease
            this.currentMutex.WaitOne();

            // get targeters and set a copy so we can release the mutex faster
            PlayerCharacter[] current = this.GetTargeting(player);
            this.current = (PlayerCharacter[])current.Clone();

            // release
            this.currentMutex.ReleaseMutex();

            this.HandleHistory(current);

            // play sound if necessary
            if (this.config.PlaySoundOnTarget && this.current.Length > this.lastTargetAmount && this.CanPlaySound()) {
                this.soundLastPlayed = Stopwatch.GetTimestamp();
                this.PlaySound();
            }
            this.lastTargetAmount = this.current.Length;
        }

        private void HandleHistory(PlayerCharacter[] targeting) {
            if (!this.config.KeepHistory) {
                return;
            }

            foreach (PlayerCharacter targeter in targeting) {
                // add the targeter to the previous list
                if (this.previousTargeters.Any(old => old.ActorId == targeter.ActorId)) {
                    this.previousTargeters.RemoveAll(old => old.ActorId == targeter.ActorId);
                }
                this.previousTargeters.Insert(0, new Targeter(targeter));
            }

            // only keep the configured number of previous targeters (ignoring ones that are currently targeting)
            while (this.previousTargeters.Where(old => targeting.All(actor => actor.ActorId != old.ActorId)).Count() > this.config.NumHistory) {
                this.previousTargeters.RemoveAt(this.previousTargeters.Count - 1);
            }
        }

        private PlayerCharacter[] GetTargeting(Actor player) {
            return this.pi.ClientState.Actors
                .Where(actor => actor.TargetActorID == player.ActorId && actor is PlayerCharacter)
                .Select(actor => actor as PlayerCharacter)
                .Where(actor => this.config.LogParty || this.pi.ClientState.PartyList.All(member => member.Actor?.ActorId != actor.ActorId))
                .Where(actor => this.config.LogAlliance || !this.InAlliance(actor))
                .Where(actor => this.config.LogInCombat || !this.InCombat(actor))
                .Where(actor => this.config.LogSelf || actor.ActorId != player.ActorId)
                .ToArray();
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
            using (player) {
                try {
                    player.Play();
                } catch (FileNotFoundException e) {
                    this.SendError($"Could not play sound: {e.Message}");
                } catch (InvalidOperationException e) {
                    this.SendError($"Could not play sound: {e.Message}");
                }
            }
        }

        private void SendError(string message) {
            Payload[] payloads = { new TextPayload($"[Who's Looking] {message}") };
            this.pi.Framework.Gui.Chat.PrintChat(new XivChatEntry {
                MessageBytes = new SeString(payloads).Encode(),
                Type = XivChatType.ErrorMessage
            });
        }

        public void Dispose() {
            this.currentMutex.Dispose();
            this.previousMutex.Dispose();
        }
    }
}
