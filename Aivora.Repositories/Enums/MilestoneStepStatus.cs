using System.Text.Json.Serialization;

namespace Aivora.Repositories.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MilestoneStepStatus
{
    PENDING,
    IN_PROGRESS,
    COMPLETED,
    SKIPPED
}
