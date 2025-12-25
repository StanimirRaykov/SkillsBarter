using System.Text.Json.Serialization;
using SkillsBarter.Models.Converters;

namespace SkillsBarter.Models;

[JsonConverter(typeof(DisputeReasonCodeConverter))]
public enum DisputeReasonCode
{
    WorkNotDelivered,
    WorkNotAsDescribed,
    QualityIssues,
    DeadlineMissed,
    CommunicationIssues,
    Other,
    NonDelivery,
    NoDelivery,
    LateDelivery,
    PoorQuality
}
