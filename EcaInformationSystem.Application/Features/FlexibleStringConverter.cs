using System.Text.Json;
using System.Text.Json.Serialization;

namespace EcaInformationSystem.Application.Features
{
    public class FlexibleStringConverter : JsonConverter<string>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // If it's already a string, just return it
            if (reader.TokenType == JsonTokenType.String)
            {
                return reader.GetString();
            }

            // If it's a number, convert the raw numeric bytes into a string
            if (reader.TokenType == JsonTokenType.Number)
            {
                using var doc = JsonDocument.ParseValue(ref reader);
                return doc.RootElement.GetRawText();
            }

            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            // Fallback for any other type
            using (var fallbackDoc = JsonDocument.ParseValue(ref reader))
            {
                return fallbackDoc.RootElement.GetRawText();
            }
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }
    }
}
