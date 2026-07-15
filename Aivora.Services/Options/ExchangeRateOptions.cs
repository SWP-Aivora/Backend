namespace Aivora.Services.Options;

public class ExchangeRateOptions
{
    public Dictionary<string, decimal> ToAicoin { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AICOIN"] = 1m,
        ["USD"] = 25m,
        ["VND"] = 0.001m
    };
}
