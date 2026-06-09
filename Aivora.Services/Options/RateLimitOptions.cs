namespace Aivora.Services.Options;

public class RateLimitOptions
{
    public const string SectionName = "RateLimit";

    public PolicyOptions Strict { get; set; } = new() { PermitLimit = 10, WindowInMinutes = 1 };
    public PolicyOptions AI { get; set; } = new() { PermitLimit = 20, WindowInMinutes = 1 };
    public PolicyOptions General { get; set; } = new() { PermitLimit = 100, WindowInMinutes = 1 };

    public class PolicyOptions
    {
        public int PermitLimit { get; set; }
        public int WindowInMinutes { get; set; }
    }
}
