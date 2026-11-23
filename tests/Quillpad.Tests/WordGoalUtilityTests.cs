namespace Quillpad.Tests
{
    using System.Globalization;
    using Notepads.Utilities;
    using Xunit;

    public class WordGoalUtilityTests
    {
        [Theory]
        [InlineData("3000", 3000)]
        [InlineData(" 3,000 ", 3000)]
        [InlineData("3 000", 3000)]
        [InlineData("3.000", 3000)]
        [InlineData("1", 1)]
        public void ReadsGoalsWithGroupSeparators(string input, int expected)
        {
            Assert.True(WordGoalUtility.TryParseGoal(input, out var goal));
            Assert.Equal(expected, goal);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("0")]
        [InlineData("-5")]
        [InlineData("3k")]
        [InlineData("99999999999")]
        public void RejectsWhatIsNotAGoal(string input)
        {
            Assert.False(WordGoalUtility.TryParseGoal(input, out _));
        }

        [Theory]
        [InlineData(2752, 2340, 412)]
        [InlineData(2300, 2340, -40)]
        [InlineData(0, 0, 0)]
        public void CountsWordsAgainstTheFirstOpen(int now, int atOpen, int expected)
        {
            Assert.Equal(expected, WordGoalUtility.WordsAddedThisSession(now, atOpen));
        }

        [Fact]
        public void SignedNumbersShowTheirSign()
        {
            var culture = CultureInfo.GetCultureInfo("en-US");
            Assert.Equal("+1,412", WordGoalUtility.FormatSigned(1412, culture));
            Assert.Equal("-40", WordGoalUtility.FormatSigned(-40, culture));
            Assert.Equal("0", WordGoalUtility.FormatSigned(0, culture));
        }

        [Theory]
        [InlineData(2340, 3000, 78)]
        [InlineData(3000, 3000, 100)]
        [InlineData(100000, 10, 999)]
        [InlineData(10, 0, 0)]
        public void ProgressIsAWholePercent(int words, int goal, int expected)
        {
            Assert.Equal(expected, WordGoalUtility.ProgressPercent(words, goal));
        }

        [Fact]
        public void StoreKeysIgnoreCaseAndStayShort()
        {
            var key = WordGoalUtility.GetStoreKey(@"C:\Stories\Chapter 1.md");
            Assert.Equal(key, WordGoalUtility.GetStoreKey(@"c:\stories\chapter 1.MD"));
            Assert.NotEqual(key, WordGoalUtility.GetStoreKey(@"C:\Stories\Chapter 2.md"));
            Assert.True(WordGoalUtility.GetStoreKey(new string('x', 2000)).Length < 255);
        }
    }
}
