using System.Collections.Generic;

namespace SharpTools.Tools.Services;

public record ClassSimilarityResult(
    List<ClassSemanticFeatures> SimilarClasses,
    double AverageSimilarityScore
);
