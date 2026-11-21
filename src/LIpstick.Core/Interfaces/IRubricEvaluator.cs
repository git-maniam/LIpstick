using LIpstick.Core.Models;

namespace LIpstick.Core.Interfaces;

public interface IRubricEvaluator
{
    RubricDefinition Definition { get; }
    RubricEvaluationResult EvaluateProfile(LinkedInProfile profile);
}
