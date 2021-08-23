using System;

namespace PeepingTom.Ipc.From {
    [Serializable]
    public class NewTargeterMessage : IFromMessage {
        public Targeter Targeter { get; }

        public NewTargeterMessage(Targeter targeter) {
            this.Targeter = targeter;
        }
    }
}
