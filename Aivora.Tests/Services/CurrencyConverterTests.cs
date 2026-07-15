using Aivora.Services.AIJobAssistantService;
using Aivora.Services.AIJobAssistantService.Parsing;
using Aivora.Services.Exceptions;
using FluentAssertions;
using Xunit;

namespace Aivora.Tests.Services;

public class CurrencyConverterTests
{
    private static readonly Dictionary<string, decimal> Rates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AICOIN"] = 1m,
        ["USD"] = 25m,
        ["VND"] = 0.001m
    };

    [Fact]
    public void ConvertToAicoin_WithAicoin_ReturnsUnchanged()
    {
        var milestones = new List<Response.SuggestedMilestone> { new() { Title = "M1", Amount = 100, DueDays = 1 } };

        var (currency, min, max, result) = CurrencyConverter.ConvertToAicoin("AICOIN", 100, 200, milestones, Rates);

        currency.Should().Be("AICOIN");
        min.Should().Be(100);
        max.Should().Be(200);
        result.Should().BeSameAs(milestones);
    }

    [Fact]
    public void ConvertToAicoin_WithUsd_MultipliesByRate()
    {
        var milestones = new List<Response.SuggestedMilestone> { new() { Title = "M1", Amount = 10, DueDays = 1 } };

        var (currency, min, max, result) = CurrencyConverter.ConvertToAicoin("USD", 100, 200, milestones, Rates);

        currency.Should().Be("AICOIN");
        min.Should().Be(100 * 25);
        max.Should().Be(200 * 25);
        result.Single().Amount.Should().Be(10 * 25);
    }

    [Fact]
    public void ConvertToAicoin_WithVnd_MultipliesByRate()
    {
        var (currency, min, max, _) = CurrencyConverter.ConvertToAicoin("VND", 100000, 200000, new List<Response.SuggestedMilestone>(), Rates);

        currency.Should().Be("AICOIN");
        min.Should().Be(100000 * 0.001m);
        max.Should().Be(200000 * 0.001m);
    }

    [Fact]
    public void ConvertToAicoin_DoesNotMutateInputMilestoneList()
    {
        var milestones = new List<Response.SuggestedMilestone> { new() { Title = "M1", Amount = 10, DueDays = 1 } };

        CurrencyConverter.ConvertToAicoin("USD", 100, 200, milestones, Rates);

        milestones.Single().Amount.Should().Be(10);
    }

    [Fact]
    public void ConvertToAicoin_WithUnsupportedCurrency_ThrowsValidationException()
    {
        var act = () => CurrencyConverter.ConvertToAicoin("EUR", 100, 200, new List<Response.SuggestedMilestone>(), Rates);

        act.Should().Throw<ValidationException>()
            .WithMessage("*EUR*");
    }
}
