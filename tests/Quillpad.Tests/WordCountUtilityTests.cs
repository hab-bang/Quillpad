namespace Quillpad.Tests
{
    using Notepads.Utilities;
    using Xunit;

    public class WordCountUtilityTests
    {
        private static int Words(string text) => WordCountUtility.Count(text).Words;

        [Theory]
        [InlineData("", 0)]
        [InlineData("one two  three\tfour\rfive", 5)]
        public void CountsWordsBetweenSpaces(string text, int expected)
        {
            Assert.Equal(expected, Words(text));
        }

        [Theory]
        [InlineData("She paused—then ran.", 4)]   // em dash
        [InlineData("pages 10–12", 3)]            // en dash
        [InlineData("She paused--then ran.", 4)]  // two hyphens typed for an em dash
        [InlineData("—Wait, she said.", 3)]
        public void DashesSeparateWords(string text, int expected)
        {
            Assert.Equal(expected, Words(text));
        }

        [Theory]
        [InlineData("a well-known face", 3)]
        [InlineData("twenty-one", 1)]
        [InlineData("- list item", 2)]
        [InlineData("---", 0)]
        public void SingleHyphensJoinWords(string text, int expected)
        {
            Assert.Equal(expected, Words(text));
        }

        [Theory]
        [InlineData("don't", 1)]
        [InlineData("don’t", 1)]
        [InlineData("the cats' toys", 3)]
        public void ApostrophesStayInsideAWord(string text, int expected)
        {
            Assert.Equal(expected, Words(text));
        }

        [Theory]
        [InlineData("# Chapter One", 2)]
        [InlineData("**bold** and *it*", 3)]
        [InlineData("> quoted line", 2)]
        [InlineData("* * *", 0)]
        [InlineData("~~~", 0)]
        [InlineData("[a link](https://example.com)", 2)]
        public void MarkdownSyntaxAloneIsNotAWord(string text, int expected)
        {
            Assert.Equal(expected, Words(text));
        }

        [Theory]
        [InlineData("日本語", 3)]
        [InlineData("東京 Tokyo", 3)]
        [InlineData("ひらがなカタカナ", 8)]
        [InlineData("漢字abc", 3)]
        public void EachCjkCharacterIsAWord(string text, int expected)
        {
            Assert.Equal(expected, Words(text));
        }

        [Fact]
        public void SurrogatePairIsOneCharacter()
        {
            var stats = WordCountUtility.Count("😀 ok");
            Assert.Equal(4, stats.Characters);
            Assert.Equal(3, stats.CharactersWithoutSpaces);
        }

        [Fact]
        public void CountsOnlyTheGivenRange()
        {
            Assert.Equal(2, WordCountUtility.Count("one two three", 4, 9).Words);
        }
    }
}
