using System.Text.Json.Serialization;

namespace Aivora.Repositories.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SkillLevel
{
    BEGINNER,
    INTERMEDIATE,
    ADVANCED,
    EXPERT
}
