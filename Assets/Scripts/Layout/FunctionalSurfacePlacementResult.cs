namespace AnimalCafe.Layout
{
    public sealed class FunctionalSurfacePlacementResult
    {
        public bool Succeeded { get; }
        public FunctionalSurfacePlacementFailureReason FailureReason { get; }

        private FunctionalSurfacePlacementResult(
            bool succeeded,
            FunctionalSurfacePlacementFailureReason failureReason)
        {
            Succeeded = succeeded;
            FailureReason = failureReason;
        }

        public static FunctionalSurfacePlacementResult Success()
        {
            return new FunctionalSurfacePlacementResult(
                true,
                FunctionalSurfacePlacementFailureReason.None);
        }

        public static FunctionalSurfacePlacementResult Failure(
            FunctionalSurfacePlacementFailureReason reason)
        {
            return new FunctionalSurfacePlacementResult(false, reason);
        }
    }
}
