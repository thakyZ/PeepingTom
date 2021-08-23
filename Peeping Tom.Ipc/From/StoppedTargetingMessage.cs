using System;

namespace PeepingTom.Ipc.From {
    [Serializable]
    public class StoppedTargetingMessage : IFromMessage {
        public Targeter Targeter { get; }

        public StoppedTargetingMessage(Targeter targeter) {
            this.Targeter = targeter;
        }
    }
}
