using System;
using System.Collections.Generic;

namespace PeepingTom.Ipc.From {
    [Serializable]
    public class AllTargetersMessage : IFromMessage {
        public List<(Targeter targeter, bool currentlyTargeting)> Targeters { get; }

        public AllTargetersMessage(List<(Targeter, bool)> targeters) {
            this.Targeters = targeters;
        }
    }
}
