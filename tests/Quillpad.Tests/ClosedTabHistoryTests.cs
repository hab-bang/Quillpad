namespace Quillpad.Tests
{
    using Notepads.Utilities;
    using Xunit;

    public class ClosedTabHistoryTests
    {
        private static ClosedTabHistory<string> Create(int capacity = 20) => new ClosedTabHistory<string>(capacity, path => path);

        [Fact]
        public void ReopensTheLastClosedFirst()
        {
            var history = Create();
            history.Push(@"C:\a.md");
            history.Push(@"C:\b.md");

            Assert.True(history.TryPop(out var first));
            Assert.Equal(@"C:\b.md", first);
            Assert.True(history.TryPop(out var second));
            Assert.Equal(@"C:\a.md", second);
            Assert.False(history.TryPop(out _));
        }

        [Fact]
        public void AFileClosedTwiceIsKeptOnce()
        {
            var history = Create();
            history.Push(@"C:\a.md");
            history.Push(@"C:\b.md");
            history.Push(@"c:\A.MD");

            Assert.Equal(2, history.Count);
            Assert.True(history.TryPop(out var newest));
            Assert.Equal(@"c:\A.MD", newest);
        }

        [Fact]
        public void ForgetsTheOldestPastTheLimit()
        {
            var history = Create(capacity: 2);
            history.Push("1");
            history.Push("2");
            history.Push("3");

            Assert.Equal(2, history.Count);
            history.TryPop(out _);
            history.TryPop(out var oldest);
            Assert.Equal("2", oldest);
        }
    }
}
