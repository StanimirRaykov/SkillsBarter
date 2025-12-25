using System.Text.Json;
using System.Text.Json.Serialization;

namespace SkillsBarter.Models.Converters;

public class DisputeReasonCodeConverter : JsonConverter<DisputeReasonCode>
{
    public override DisputeReasonCode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("Expected string for dispute reason");

        var value = reader.GetString()?.Trim() ?? string.Empty;
        var normalized = value.Replace("-", string.Empty).Replace("_", string.Empty).Replace(" ", string.Empty).ToLowerInvariant();

        return normalized switch
        {
            "nodelivery" => DisputeReasonCode.NoDelivery,
            "nondelivery" => DisputeReasonCode.NonDelivery,
            "worknotdelivered" => DisputeReasonCode.WorkNotDelivered,
            "latedelivery" => DisputeReasonCode.LateDelivery,
            "deadline" or "deadlinemissed" => DisputeReasonCode.DeadlineMissed,
            "poorquality" => DisputeReasonCode.PoorQuality,
            "qualityissue" or "qualityissues" => DisputeReasonCode.QualityIssues,
            "worknotasdescribed" => DisputeReasonCode.WorkNotAsDescribed,
            "communication" or "communicationissues" => DisputeReasonCode.CommunicationIssues,
            "other" => DisputeReasonCode.Other,
            _ when Enum.TryParse<DisputeReasonCode>(value, true, out var parsed) => parsed,
            _ => throw new JsonException($"Unsupported dispute reason: {value}")
        };
    }

    public override void Write(Utf8JsonWriter writer, DisputeReasonCode value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

