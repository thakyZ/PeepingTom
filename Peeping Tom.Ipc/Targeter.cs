using System;
using System.Linq;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Text.SeStringHandling;

namespace PeepingTom.Ipc {
    [Serializable]
    public class Targeter {
        public SeString Name { get; }
        public uint HomeWorldId { get; }
        public uint ObjectId { get; }
        public DateTime When { get; }

        public Targeter(PlayerCharacter character) {
            this.Name = character.Name;
            this.HomeWorldId = character.HomeWorld.Id;
            this.ObjectId = character.ObjectId;
            this.When = DateTime.UtcNow;
        }

        public PlayerCharacter? GetPlayerCharacter(ObjectTable objectTable) {
            return objectTable.FirstOrDefault(actor => actor.ObjectId == this.ObjectId && actor is PlayerCharacter) as PlayerCharacter;
        }
    }
}
