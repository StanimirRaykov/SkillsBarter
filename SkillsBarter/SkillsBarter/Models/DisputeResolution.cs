using System.Text.Json.Serialization;
using SkillsBarter.Models.Converters;

namespace SkillsBarter.Models;

[JsonConverter(typeof(DisputeResolutionConverter))]
public enum DisputeResolution
{
    None,
    FavorsComplainer,
    FavorsRespondent,
    ModeratorDecision,
    MutualAgreement,
    Abandoned
}
