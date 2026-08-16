using AnimalCafe.UI.Feedback;
using NUnit.Framework;

namespace AnimalCafe.Tests.Phase5
{
    public sealed class ToastQueueTests
    {
        private float unscaledTime;
        private ToastQueue queue;

        [SetUp]
        public void SetUp()
        {
            unscaledTime = 10f;
            queue = new ToastQueue(() => unscaledTime);
        }

        [Test]
        public void AT018_EnqueueNormalToast_WhenQueueIsEmpty_MakesItCurrent()
        {
            var message = NormalInfo("Coffee beans saved");

            var result = queue.Enqueue(message);

            Assert.That(result, Is.EqualTo(ToastEnqueueResult.Accepted));
            Assert.That(queue.TryGetCurrent(out var current), Is.True);
            Assert.That(current.Message, Is.SameAs(message));
            Assert.That(current.MergeCount, Is.EqualTo(1));
        }

        [Test]
        public void AT019_EnqueueConsecutiveDuplicate_MergesAndRefreshesExpiry()
        {
            queue.Enqueue(NormalInfo("Coffee beans saved"));
            Assert.That(queue.TryGetCurrent(out var original), Is.True);
            var originalExpiry = original.ExpiresAtUnscaledTime;
            unscaledTime += 1f;

            var result = queue.Enqueue(NormalInfo("Coffee beans saved"));

            Assert.That(result, Is.EqualTo(ToastEnqueueResult.Merged));
            Assert.That(queue.TryGetCurrent(out var merged), Is.True);
            Assert.That(merged.MergeCount, Is.EqualTo(2));
            Assert.That(merged.ExpiresAtUnscaledTime, Is.GreaterThan(originalExpiry));
        }

        [Test]
        public void AT020_CompleteCurrent_WithDifferentQueuedToast_UsesFifoOrder()
        {
            queue.Enqueue(NormalInfo("First"));
            queue.Enqueue(new ToastMessage(ToastType.Success, "Second", ToastPriority.Normal, 3f));
            queue.Enqueue(new ToastMessage(ToastType.Warning, "Third", ToastPriority.Normal, 3f));

            Assert.That(queue.TryGetCurrent(out var first), Is.True);
            Assert.That(first.Message.Content, Is.EqualTo("First"));

            queue.CompleteCurrent();
            Assert.That(queue.TryGetCurrent(out var second), Is.True);
            Assert.That(second.Message.Content, Is.EqualTo("Second"));

            queue.CompleteCurrent();
            Assert.That(queue.TryGetCurrent(out var third), Is.True);
            Assert.That(third.Message.Content, Is.EqualTo("Third"));
        }

        [Test]
        public void AT021_TryGetCurrent_AfterNormalToastExpires_DiscardsExpiredItem()
        {
            queue.Enqueue(new ToastMessage(ToastType.Info, "Stale", ToastPriority.Normal, 2f));
            unscaledTime += 2.01f;

            var hasCurrent = queue.TryGetCurrent(out var current);

            Assert.That(hasCurrent, Is.False);
            Assert.That(current, Is.Null);
        }

        [Test]
        public void AT022_EnqueueImportantError_RejectsTransientToast()
        {
            var importantError = new ToastMessage(
                ToastType.Error,
                "Coffee bean selection is required",
                ToastPriority.Important,
                3f);

            var result = queue.Enqueue(importantError);

            Assert.That(result, Is.EqualTo(ToastEnqueueResult.RequiresPersistentFeedback));
            Assert.That(queue.TryGetCurrent(out _), Is.False);
        }

        [Test]
        public void SameContentWithDifferentType_DoesNotMerge()
        {
            queue.Enqueue(new ToastMessage(ToastType.Info, "Ready", ToastPriority.Normal, 3f));

            var result = queue.Enqueue(
                new ToastMessage(ToastType.Success, "Ready", ToastPriority.Normal, 3f));

            Assert.That(result, Is.EqualTo(ToastEnqueueResult.Accepted));
            Assert.That(queue.TryGetCurrent(out var first), Is.True);
            Assert.That(first.MergeCount, Is.EqualTo(1));
            Assert.That(first.Message.Type, Is.EqualTo(ToastType.Info));
            queue.CompleteCurrent();
            Assert.That(queue.TryGetCurrent(out var second), Is.True);
            Assert.That(second.Message.Type, Is.EqualTo(ToastType.Success));
        }

        [Test]
        public void SameTypeWithDifferentContent_DoesNotMerge()
        {
            queue.Enqueue(NormalInfo("First"));

            var result = queue.Enqueue(NormalInfo("Second"));

            Assert.That(result, Is.EqualTo(ToastEnqueueResult.Accepted));
            queue.CompleteCurrent();
            Assert.That(queue.TryGetCurrent(out var second), Is.True);
            Assert.That(second.Message.Content, Is.EqualTo("Second"));
            Assert.That(second.MergeCount, Is.EqualTo(1));
        }

        [Test]
        public void MatchingMessagesSeparatedByAnotherMessage_DoNotMerge()
        {
            queue.Enqueue(NormalInfo("A"));
            queue.Enqueue(NormalInfo("B"));

            var result = queue.Enqueue(NormalInfo("A"));

            Assert.That(result, Is.EqualTo(ToastEnqueueResult.Accepted));
            queue.CompleteCurrent();
            Assert.That(queue.TryGetCurrent(out var second), Is.True);
            Assert.That(second.Message.Content, Is.EqualTo("B"));
            queue.CompleteCurrent();
            Assert.That(queue.TryGetCurrent(out var third), Is.True);
            Assert.That(third.Message.Content, Is.EqualTo("A"));
            Assert.That(third.MergeCount, Is.EqualTo(1));
        }

        [Test]
        public void ConsecutiveDuplicateQueuedBehindCurrent_MergesBeforeBecomingCurrent()
        {
            queue.Enqueue(NormalInfo("Current"));
            queue.Enqueue(NormalInfo("Queued"));

            var result = queue.Enqueue(NormalInfo("Queued"));

            Assert.That(result, Is.EqualTo(ToastEnqueueResult.Merged));
            queue.CompleteCurrent();
            Assert.That(queue.TryGetCurrent(out var queued), Is.True);
            Assert.That(queued.Message.Content, Is.EqualTo("Queued"));
            Assert.That(queued.MergeCount, Is.EqualTo(2));
        }

        [Test]
        public void ImportantErrorMatchingCurrentTransient_RemainsRejectedWithoutMerge()
        {
            queue.Enqueue(new ToastMessage(
                ToastType.Error,
                "Machine requires attention",
                ToastPriority.Normal,
                3f));

            var result = queue.Enqueue(new ToastMessage(
                ToastType.Error,
                "Machine requires attention",
                ToastPriority.Important,
                3f));

            Assert.That(result, Is.EqualTo(ToastEnqueueResult.RequiresPersistentFeedback));
            Assert.That(queue.TryGetCurrent(out var current), Is.True);
            Assert.That(current.MergeCount, Is.EqualTo(1));
            Assert.That(current.Message.Priority, Is.EqualTo(ToastPriority.Normal));
        }

        private static ToastMessage NormalInfo(string content)
        {
            return new ToastMessage(ToastType.Info, content, ToastPriority.Normal, 3f);
        }
    }
}
