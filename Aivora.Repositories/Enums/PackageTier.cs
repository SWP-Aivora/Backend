using System.Text.Json.Serialization;

namespace Aivora.Repositories.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PackageTier
{
    BASIC,
    STANDARD,
    PREMIUM
}
