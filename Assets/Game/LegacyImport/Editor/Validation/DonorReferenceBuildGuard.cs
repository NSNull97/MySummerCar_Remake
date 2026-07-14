using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace MSC.LegacyImport.Editor.Validation
{
    public sealed class DonorReferenceBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            var issues = DonorPipelineProjectValidator.ValidateBuildSafety();
            if (DonorPipelineProjectValidator.HasErrors(issues))
            {
                throw new BuildFailedException(
                    "Build blocked: donor reference/provenance validation contains errors. " +
                    "Run Tools > My Summer Car > Legacy Import > Validate Donor Pipeline.");
            }
        }
    }
}
