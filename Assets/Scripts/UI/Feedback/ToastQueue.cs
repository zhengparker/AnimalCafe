using System;
using System.Collections.Generic;

namespace AnimalCafe.UI.Feedback
{
    /// <summary>
    /// Read-only presentation state for the current Toast.
    /// 当前 Toast 的只读显示状态。
    /// </summary>
    public sealed class ToastQueueEntry
    {
        internal ToastQueueEntry(ToastMessage message)
        {
            Message = message;
            MergeCount = 1;
        }

        public ToastMessage Message { get; }

        public int MergeCount { get; private set; }

        public float ExpiresAtUnscaledTime { get; private set; }

        internal void Activate(float unscaledTime)
        {
            ExpiresAtUnscaledTime = unscaledTime + Message.DurationSeconds;
        }

        internal void Merge(float unscaledTime, bool isCurrent)
        {
            MergeCount++;
            if (isCurrent)
            {
                Activate(unscaledTime);
            }
        }
    }

    /// <summary>
    /// Owns transient Toast ordering without depending on scaled game time.
    /// 管理短暂 Toast 顺序，并且不依赖游戏的 scaled time。
    /// </summary>
    public sealed class ToastQueue
    {
        private readonly Func<float> unscaledTimeProvider;
        private readonly List<ToastQueueEntry> entries = new List<ToastQueueEntry>();

        public ToastQueue(Func<float> unscaledTimeProvider)
        {
            this.unscaledTimeProvider = unscaledTimeProvider
                ?? throw new ArgumentNullException(nameof(unscaledTimeProvider));
        }

        public ToastEnqueueResult Enqueue(ToastMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (message.Type == ToastType.Error && message.Priority == ToastPriority.Important)
            {
                return ToastEnqueueResult.RequiresPersistentFeedback;
            }

            var now = unscaledTimeProvider();
            if (entries.Count > 0)
            {
                var lastIndex = entries.Count - 1;
                var last = entries[lastIndex];
                if (last.Message.HasSameIdentity(message))
                {
                    last.Merge(now, lastIndex == 0);
                    return ToastEnqueueResult.Merged;
                }
            }

            var entry = new ToastQueueEntry(message);
            entries.Add(entry);
            if (entries.Count == 1)
            {
                entry.Activate(now);
            }

            return ToastEnqueueResult.Accepted;
        }

        public bool TryGetCurrent(out ToastQueueEntry current)
        {
            RemoveExpiredCurrentItems();
            if (entries.Count == 0)
            {
                current = null;
                return false;
            }

            current = entries[0];
            return true;
        }

        public void CompleteCurrent()
        {
            if (entries.Count == 0)
            {
                return;
            }

            entries.RemoveAt(0);
            ActivateCurrentIfPresent();
        }

        private void RemoveExpiredCurrentItems()
        {
            if (entries.Count == 0 || unscaledTimeProvider() < entries[0].ExpiresAtUnscaledTime)
            {
                return;
            }

            entries.RemoveAt(0);
            ActivateCurrentIfPresent();
        }

        private void ActivateCurrentIfPresent()
        {
            if (entries.Count > 0)
            {
                entries[0].Activate(unscaledTimeProvider());
            }
        }
    }
}
