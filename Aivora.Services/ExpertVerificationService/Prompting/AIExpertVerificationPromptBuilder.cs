using Aivora.Services.ExpertVerificationService.Providers;

namespace Aivora.Services.ExpertVerificationService.Prompting;

public class AIExpertVerificationPromptBuilder
{
    public string Build(AnalyzeEvidenceRequest request)
    {
        return $$"""
        You are verifying a certificate/degree file submitted by a freelance expert as evidence of a claimed skill.

        Expert account name: {{request.ExpertFullName}}
        Claimed skill: {{request.ClaimedSkillName}}

        Steps:
        1. OCR the attached document. It may be written in Vietnamese, English, or another language.
        2. Extract the certificate holder's name, the certificate/skill name, the issuing organization, and the issue date (if present).
        3. Cross-check the extracted holder name against the expert account name above (allow for minor spelling/diacritic variation, but flag clear mismatches).
        4. Cross-check the extracted certificate content against the claimed skill above.
        5. Look for obvious signs of tampering or forgery (inconsistent fonts/formatting, missing basic issuance information). Do not attempt third-party verification against any external certificate-authority database.
        6. Decide exactly one outcome: "APPROVED" (clearly genuine and matching), "REJECTED" (clearly fake, forged, or does not match the expert/skill), or "NEEDS_REVIEW" (image unclear, information incomplete, or not confident enough to conclude either way).

        Respond with a single JSON object only, no markdown fences, in this exact shape:
        {
          "outcome": "APPROVED" | "REJECTED" | "NEEDS_REVIEW",
          "confidenceScore": number between 0 and 100,
          "reasoning": "explanation written in English, regardless of the certificate's language"
        }
        """;
    }
}
