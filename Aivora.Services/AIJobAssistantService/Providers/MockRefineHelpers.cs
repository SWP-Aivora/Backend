using System.Globalization;
using System.Text.RegularExpressions;
using Aivora.Repositories.Enums;

namespace Aivora.Services.AIJobAssistantService.Providers;

// Phan match/parse keyword dung chung giua 2 mock refine provider
// (AIJobAssistantService va AIJobRefinementService) - chi phan nhan dien,
// tung Mock tu quyet dinh set field nao vao draft cua minh va dat ten
// changedFields theo domain rieng.
public static class MockRefineHelpers
{
    public static bool IsAdvisory(string lower)
    {
        var markers = new[] { "why", "what is", "what are", "should", "recommend", "explain", "compare", "advice", "tu van", "giai thich", "co nen" };
        return markers.Any(lower.Contains);
    }

    public static (decimal Min, decimal Max)? TryParseBudget(string lower)
    {
        if (!lower.Contains("budget") && !lower.Contains("ngan sach"))
        {
            return null;
        }

        var numbers = Regex.Matches(lower, @"\d+(?:\.\d+)?")
            .Select(m => decimal.Parse(m.Value, CultureInfo.InvariantCulture))
            .ToList();
        if (numbers.Count == 0)
        {
            return null;
        }

        return (numbers[0], numbers.Count > 1 ? numbers[1] : numbers[0]);
    }

    public static int? TryParseTimelineDays(string lower)
    {
        if (!lower.Contains("day") && !lower.Contains("timeline") && !lower.Contains("ngay"))
        {
            return null;
        }

        var match = Regex.Match(lower, @"\d+");
        return match.Success ? int.Parse(match.Value, CultureInfo.InvariantCulture) : null;
    }

    // Cac dieu kien khong phai else-if trong ban goc - neu message khop
    // nhieu tu khoa, gia tri sau cung se de gia tri truoc. Giu nguyen thu
    // tu nay (beginner -> intermediate -> advanced -> expert).
    public static SkillLevel? TryParseExperienceLevel(string lower)
    {
        SkillLevel? level = null;
        if (lower.Contains("beginner") || lower.Contains("entry") || lower.Contains("junior")) level = SkillLevel.BEGINNER;
        if (lower.Contains("intermediate") || lower.Contains("middle")) level = SkillLevel.INTERMEDIATE;
        if (lower.Contains("advanced") || lower.Contains("senior")) level = SkillLevel.ADVANCED;
        if (lower.Contains("expert")) level = SkillLevel.EXPERT;
        return level;
    }

    public static BudgetType? TryParseBudgetType(string lower)
    {
        if (lower.Contains("hourly") || lower.Contains("theo gio")) return BudgetType.HOURLY;
        if (lower.Contains("fixed") || lower.Contains("tron goi")) return BudgetType.FIXED;
        return null;
    }

    // Khong phai else-if trong ban goc - aicoin thang neu message chua ca
    // vnd/usd lan aicoin (kiem tra sau cung, de gia tri truoc).
    public static string? TryParseCurrency(string lower)
    {
        string? currency = null;
        if (lower.Contains("vnd")) currency = "VND";
        if (lower.Contains("usd")) currency = "USD";
        if (lower.Contains("aicoin")) currency = "AICOIN";
        return currency;
    }

    public static string? TryParseSkill(string message)
    {
        var match = Regex.Match(message, @"(?:add skill|them ky nang)\s*[:=\-]?\s*(.+)", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return null;
        }

        var skill = match.Groups[1].Value.Trim();
        return string.IsNullOrWhiteSpace(skill) ? null : skill;
    }
}
