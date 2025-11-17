using System.Collections.Generic;

namespace UltrasharpTools.Tools.Services;

public record ClassSimilarityResult(
    List<ClassSemanticFeatures> SimilarClasses,
    double AverageSimilarityScore
);
