using System;
using Dalamud.Game.Text.SeStringHandling;
using Newtonsoft.Json;

namespace PeepingTom.Ipc {
    public class SeStringConverter : JsonConverter<SeString> {
        public override void WriteJson(JsonWriter writer, SeString? value, JsonSerializer serializer) {
            if (value == null) {
                writer.WriteNull();
                return;
            }

            var bytes = value.Encode();
            writer.WriteValue(Convert.ToBase64String(bytes));
        }

        public override SeString? ReadJson(JsonReader reader, Type objectType, SeString? existingValue, bool hasExistingValue, JsonSerializer serializer) {
            var base64 = (string?) reader.Value;
            if (base64 == null) {
                return null;
            }

            var bytes = Convert.FromBase64String(base64);
            return SeString.Parse(bytes);
        }
    }
}
