using Dalamud.Game.ClientState.Actors.Resolvers;
using Dalamud.Game.ClientState.Actors.Types;
using Dalamud.Plugin;
using System;
using System.Linq;

namespace PeepingTom {
    public class Targeter {
        public string Name { get; }
        public World HomeWorld { get; }
        public int ActorId { get; }
        public DateTime When { get; }

        public Targeter(PlayerCharacter character) {
            this.Name = character.Name;
            this.HomeWorld = character.HomeWorld;
            this.ActorId = character.ActorId;
            this.When = DateTime.UtcNow;
        }

        public PlayerCharacter? GetPlayerCharacter(DalamudPluginInterface pi) {
            return pi.ClientState.Actors.FirstOrDefault(actor => actor.ActorId == this.ActorId && actor is PlayerCharacter) as PlayerCharacter;
        }
    }
}
