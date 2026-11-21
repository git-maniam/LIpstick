namespace LIpstick.Core.Models;

public record RubricItem(
    string Id,
    string Name,
    int Points,
    string FullMarks
);

public record RubricDefinition(
    string Version,
    int Total,
    string Note,
    IReadOnlyList<RubricItem> Items
);

public record RubricCategoryScore(
    string CategoryId,
    string CategoryName,
    int PointsAwarded,
    int MaxPoints,
    string EvaluationNotes,
    IReadOnlyList<string> MissingSignals,
    bool FixFirst
);

public record RubricEvaluationResult(
    int TotalScore,
    int MaxPossibleScore,
    IReadOnlyList<RubricCategoryScore> Categories,
    IReadOnlyList<RubricCategoryScore> FixFirstCategories
);
