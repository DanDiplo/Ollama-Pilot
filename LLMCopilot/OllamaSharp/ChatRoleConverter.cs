using System;
using Newtonsoft.Json;
using OllamaSharp.Models.Chat;

namespace OllamaSharp.Models.Chat.Converter
{
    public class ChatRoleConverter : JsonConverter<ChatRole>
    {
        public override ChatRole ReadJson(JsonReader reader, Type objectType, ChatRole existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            string value = reader.Value?.ToString();
            return new ChatRole(value);
        }

        public override void WriteJson(JsonWriter writer, ChatRole value, JsonSerializer serializer)
        {
            writer.WriteValue(value.ToString());
        }

        public override bool CanRead => true;
        public override bool CanWrite => true;
    }
}
