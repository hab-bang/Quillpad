namespace Quillpad.Tests
{
    using Notepads.Utilities;
    using Xunit;

    public class MarkdownEmphasisUtilityTests
    {
        // "[" and "]" mark the selection in the input and the expected output
        private static string Toggle(string marked, int marker)
        {
            var start = marked.IndexOf('[');
            var end = marked.IndexOf(']') - 1;
            var text = marked.Replace("[", "").Replace("]", "");

            var edit = MarkdownEmphasisUtility.Toggle(text, start, end, marker);
            var result = text.Substring(0, edit.Start) + edit.Replacement + text.Substring(edit.Start + edit.Length);
            return result.Insert(edit.SelectionStart + edit.SelectionLength, "]").Insert(edit.SelectionStart, "[");
        }

        [Theory]
        [InlineData("a [word] here", "a **[word]** here")]
        [InlineData("a **[word]** here", "a [word] here")]
        [InlineData("a [**word**] here", "a [word] here")]
        [InlineData("a *[word]* here", "a ***[word]*** here")]
        [InlineData("a ***[word]*** here", "a *[word]* here")]
        public void BoldWrapsAndUnwraps(string input, string expected)
        {
            Assert.Equal(expected, Toggle(input, MarkdownEmphasisUtility.Bold));
        }

        [Theory]
        [InlineData("a [word] here", "a *[word]* here")]
        [InlineData("a *[word]* here", "a [word] here")]
        [InlineData("a [*word*] here", "a [word] here")]
        [InlineData("a **[word]** here", "a ***[word]*** here")]
        [InlineData("a ***[word]*** here", "a **[word]** here")]
        public void ItalicWrapsAndUnwraps(string input, string expected)
        {
            Assert.Equal(expected, Toggle(input, MarkdownEmphasisUtility.Italic));
        }

        [Fact]
        public void SpacesAtTheEndsStayOutside()
        {
            // A double-click selects the word and the space after it
            Assert.Equal("a **[word]** here", Toggle("a [word ]here", MarkdownEmphasisUtility.Bold));
        }

        [Fact]
        public void EmptySelectionPutsTheCaretBetweenTheStars()
        {
            Assert.Equal("say **[]**", Toggle("say []", MarkdownEmphasisUtility.Bold));
            Assert.Equal("say []", Toggle("say **[]**", MarkdownEmphasisUtility.Bold));
        }

        [Fact]
        public void EachLineGetsItsOwnStars()
        {
            Assert.Equal("*[one*\r\r*two]*",Toggle("[one\r\rtwo]", MarkdownEmphasisUtility.Italic));
        }

        [Fact]
        public void TwiceGivesBackTheText()
        {
            var once = Toggle("He [never] said that.", MarkdownEmphasisUtility.Italic);
            Assert.Equal("He [never] said that.", Toggle(once, MarkdownEmphasisUtility.Italic));
        }
    }
}
