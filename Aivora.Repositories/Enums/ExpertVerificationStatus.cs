using System.Text.Json.Serialization;

namespace Aivora.Repositories.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ExpertVerificationStatus
{
    APPROVED,
    REJECTED,
    NEEDS_REVIEW,
    ESCALATED
}
