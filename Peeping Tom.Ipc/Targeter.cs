using System;
using System.Linq;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Text.SeStringHandling;
using Newtonsoft.Json;

namespace PeepingTom.Ipc {
    [Serializable]
    public class Targeter {
        [JsonConverter(typeof(SeStringConverter))]
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

        [JsonConstructor]
        public Targeter(SeString name, uint homeWorldId, uint objectId, DateTime when) {
            this.Name = name;
            this.HomeWorldId = homeWorldId;
            this.ObjectId = objectId;
            this.When = when;
        }

        public PlayerCharacter? GetPlayerCharacter(ObjectTable objectTable) {
            return objectTable.FirstOrDefault(actor => actor.ObjectId == this.ObjectId && actor is PlayerCharacter) as PlayerCharacter;
        }
    }
}
