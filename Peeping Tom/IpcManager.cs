using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Ipc;
using PeepingTom.Ipc;
using PeepingTom.Ipc.From;
using PeepingTom.Ipc.To;

namespace PeepingTom {
    internal class IpcManager : IDisposable {
        private PeepingTomPlugin Plugin { get; }

        private ICallGateProvider<IFromMessage, object> Provider { get; }
        private ICallGateSubscriber<IToMessage, object> Subscriber { get; }

        internal IpcManager(PeepingTomPlugin plugin) {
            this.Plugin = plugin;

            this.Provider = this.Plugin.Interface.GetIpcProvider<IFromMessage, object>(IpcInfo.FromRegistrationName);
            this.Subscriber = this.Plugin.Interface.GetIpcSubscriber<IToMessage, object>(IpcInfo.ToRegistrationName);

            this.Subscriber.Subscribe(this.ReceiveMessage);
        }

        public void Dispose() {
            this.Subscriber.Unsubscribe(this.ReceiveMessage);
        }

        internal void SendAllTargeters() {
            var targeters = new List<(Targeter, bool)>();
            targeters.AddRange(this.Plugin.Watcher.CurrentTargeters.Select(t => (t, true)));
            targeters.AddRange(this.Plugin.Watcher.PreviousTargeters.Select(t => (t, false)));

            this.Provider.SendMessage(new AllTargetersMessage(targeters));
        }

        internal void SendNewTargeter(Targeter targeter) {
            this.Provider.SendMessage(new NewTargeterMessage(targeter));
        }

        internal void SendStoppedTargeting(Targeter targeter) {
            this.Provider.SendMessage(new StoppedTargetingMessage(targeter));
        }

        private void ReceiveMessage(IToMessage message) {
            switch (message) {
                case RequestTargetersMessage: {
                    this.SendAllTargeters();
                    break;
                }
            }
        }
    }
}
