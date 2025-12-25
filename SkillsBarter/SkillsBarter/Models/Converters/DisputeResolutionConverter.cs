using System.Text.Json;
using System.Text.Json.Serialization;

namespace SkillsBarter.Models.Converters;

public class DisputeResolutionConverter : JsonConverter<DisputeResolution>
{
    public override DisputeResolution Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var num))
        {
            if (Enum.IsDefined(typeof(DisputeResolution), num))
                return (DisputeResolution)num;
        }

        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("Expected string or number for dispute resolution");

        var value = reader.GetString()?.Trim() ?? string.Empty;
        var normalized = value.Replace("-", string.Empty).Replace("_", string.Empty).Replace(" ", string.Empty).ToLowerInvariant();

        return normalized switch
        {
            "favorscomplainer" => DisputeResolution.FavorsComplainer,
            "favorsrespondent" => DisputeResolution.FavorsRespondent,
            "moderatordecision" => DisputeResolution.ModeratorDecision,
            "mutualagreement" => DisputeResolution.MutualAgreement,
            "abandoned" => DisputeResolution.Abandoned,
            "split" => DisputeResolution.MutualAgreement,
            "none" => DisputeResolution.None,
            _ when Enum.TryParse<DisputeResolution>(value, true, out var parsed) => parsed,
            _ => throw new JsonException($"Unsupported dispute resolution: {value}")
        };
    }

    public override void Write(Utf8JsonWriter writer, DisputeResolution value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

