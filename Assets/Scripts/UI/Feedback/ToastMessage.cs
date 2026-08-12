using System;

namespace AnimalCafe.UI.Feedback
{
    public enum ToastType
    {
        Info,
        Success,
        Warning,
        Error,
    }

    public enum ToastPriority
    {
        Normal,
        Important,
    }

    public enum ToastEnqueueResult
    {
        Accepted,
        Merged,
        RequiresPersistentFeedback,
    }

    /// <summary>
    /// Immutable data requested for a transient Toast.
    /// 一条短暂 Toast 的不可变数据；不包含任何 gameplay logic。
    /// </summary>
    public sealed class ToastMessage
    {
        public ToastMessage(
            ToastType type,
            string content,
            ToastPriority priority,
            float durationSeconds)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new ArgumentException("Toast content must not be empty.", nameof(content));
            }

            if (durationSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(durationSeconds),
                    "Toast duration must be greater than zero.");
            }

            Type = type;
            Content = content;
            Priority = priority;
            DurationSeconds = durationSeconds;
        }

        public ToastType Type { get; }

        public string Content { get; }

        public ToastPriority Priority { get; }

        public float DurationSeconds { get; }

        internal bool HasSameIdentity(ToastMessage other)
        {
            return other != null
                && Type == other.Type
                && string.Equals(Content, other.Content, StringComparison.Ordinal);
        }
    }
}
