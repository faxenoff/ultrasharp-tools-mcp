
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SharpTools.Tools.Interfaces {

    public interface ISemanticSimilarityService {
        Task<List<MethodSimilarityResult>> FindSimilarMethodsAsync(
            double similarityThreshold,
            CancellationToken cancellationToken);

        Task<List<ClassSimilarityResult>> FindSimilarClassesAsync(
            double similarityThreshold,
            CancellationToken cancellationToken);
    }
}
