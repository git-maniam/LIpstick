using System.Text;
using LIpstick.Core.Models;

namespace LIpstick.Core.Services;

public static class PromptContextAssembler
{
    public static string AssemblePrompt(RecommendationRequest request)
    {
        var sb = new StringBuilder();

        // 1. System Consultant Instruction
        sb.AppendLine($"You are an elite LinkedIn profile polish consultant. Rewrite the following {request.SectionName} to be natural, human, and outcome-driven.");
        sb.AppendLine();

        // 2. Critical Guardrails
        sb.AppendLine("CRITICAL RULES:");
        sb.AppendLine("1. Include at least 1-2 concrete, verifiable numbers/metrics.");
        sb.AppendLine("2. Zero AI slop: do NOT use delve, testament, spearhead, seamless, passionate, in the realm of.");
        sb.AppendLine("3. Use active voice and natural sentence variation.");
        sb.AppendLine();

        // 3. Voice Profile Context
        if (!string.IsNullOrWhiteSpace(request.VoiceProfile))
        {
            sb.AppendLine("VOICE PROFILE:");
            sb.AppendLine(request.VoiceProfile.Trim());
            sb.AppendLine();
        }

        // 4. Rubric Criteria & Target Points
        if (request.RubricCriteria != null && request.RubricCriteria.Count > 0)
        {
            var criteriaItems = request.RubricCriteria
                .Where(kvp => !string.Equals(kvp.Key, "CustomBaseUrl", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (criteriaItems.Count > 0)
            {
                sb.AppendLine("RUBRIC CRITERIA & TARGET POINTS:");
                foreach (var kvp in criteriaItems)
                {
                    sb.AppendLine($"- {kvp.Key}: {kvp.Value}");
                }
                sb.AppendLine();
            }
        }

        // 5. Current Text
        sb.AppendLine("CURRENT TEXT:");
        sb.AppendLine(request.CurrentText);
        sb.AppendLine();

        // 6. Directive
        sb.Append("RETURN ONLY THE POLISHED REWRITE WITH NO CHATTER, QUOTES, OR PREAMBLE.");

        return sb.ToString();
    }
}
