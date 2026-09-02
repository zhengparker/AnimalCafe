namespace AnimalCafe.Decoration
{
    public enum SurfaceSessionFailure
    {
        None,
        NoActivePreview,
        ActivePreviewMustFinish,
        UnknownTarget,
        UnknownStyle,
        WrongStyleKind
    }

    public readonly struct SurfaceSessionResult
    {
        public bool Succeeded { get; }
        public SurfaceSessionFailure FailureReason { get; }

        private SurfaceSessionResult(
            bool succeeded,
            SurfaceSessionFailure failureReason)
        {
            Succeeded = succeeded;
            FailureReason = failureReason;
        }

        internal static SurfaceSessionResult Success()
        {
            return new SurfaceSessionResult(true, SurfaceSessionFailure.None);
        }

        internal static SurfaceSessionResult Failure(SurfaceSessionFailure reason)
        {
            return new SurfaceSessionResult(false, reason);
        }
    }
}
