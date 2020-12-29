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

        public Targeter(PlayerCharacter character) {
            if (character == null) {
                throw new ArgumentNullException(nameof(character), "PlayerCharacter cannot be null");
            }
            this.Name = character.Name;
            this.HomeWorld = character.HomeWorld;
            this.ActorId = character.ActorId;
        }

        public PlayerCharacter? GetPlayerCharacter(DalamudPluginInterface pi) {
            if (pi == null) {
                throw new ArgumentNullException(nameof(pi), "DalamudPluginInterface cannot be null");
            }
            return pi.ClientState.Actors.FirstOrDefault(actor => actor.ActorId == this.ActorId && actor is PlayerCharacter) as PlayerCharacter;
        }
    }
}
