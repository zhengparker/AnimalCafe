namespace AnimalCafe.EditorTools.AssetPipeline
{
    public sealed class BenchmarkAssetValidationIssue
    {
        public BenchmarkAssetValidationIssue(
            BenchmarkAssetIssueCode code,
            string assetPath,
            string message)
        {
            Code = code;
            AssetPath = assetPath;
            Message = message;
        }

        public BenchmarkAssetIssueCode Code { get; }

        public string AssetPath { get; }

        public string Message { get; }
    }
}
