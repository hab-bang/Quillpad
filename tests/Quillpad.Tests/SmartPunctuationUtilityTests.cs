namespace Quillpad.Tests
{
    using Notepads.Utilities;
    using Xunit;

    public class SmartPunctuationUtilityTests
    {
        // Types the text one character at a time, the way the editor applies replacements
        private static string Type(string text, string prefix = "")
        {
            var document = prefix;
            foreach (var c in text)
            {
                document += c;
                var edit = SmartPunctuationUtility.GetReplacement(document, document.Length);
                if (edit.HasValue)
                {
                    document = document.Substring(0, edit.Value.Start) + edit.Value.Replacement +
                               document.Substring(edit.Value.Start + edit.Value.Length);
                }
            }

            return document;
        }

        [Theory]
        [InlineData("\"Run,\" she said.", "“Run,” she said.")]
        [InlineData("He said, \"don't.\"", "He said, “don’t.”")]
        [InlineData("'Hi,' I said", "‘Hi,’ I said")]
        [InlineData("(\"quoted\")", "(“quoted”)")]
        [InlineData("*\"Run!\"*", "*“Run!”*")]
        [InlineData("\"'Nested,' he said\"", "“‘Nested,’ he said”")]
        public void QuotesCurlTheRightWay(string typed, string expected)
        {
            Assert.Equal(expected, Type(typed));
        }

        [Theory]
        [InlineData("wait--no", "wait—no")]
        [InlineData("Wait...", "Wait…")]
        [InlineData("Wait....", "Wait….")]
        [InlineData("a - b", "a - b")]
        [InlineData("1990-1995", "1990-1995")]
        public void DashesAndDotsJoin(string typed, string expected)
        {
            Assert.Equal(expected, Type(typed));
        }

        [Theory]
        [InlineData("---")]
        [InlineData("  --")]
        [InlineData("| --- | --- |")]
        [InlineData("<!-- note -->")]
        public void MarkdownRulesStayAsTyped(string typed)
        {
            Assert.Equal(typed, Type(typed));
        }

        [Theory]
        [InlineData("use `a--b` and `\"x\"`")]
        [InlineData("``code with ` and \"quotes\"``")]
        public void CodeSpansStayAsTyped(string typed)
        {
            Assert.Equal(typed, Type(typed));
        }

        [Fact]
        public void TextAfterAClosedCodeSpanIsReplacedAgain()
        {
            Assert.Equal("`x` “y”", Type("`x` \"y\""));
        }

        [Fact]
        public void OnlyTheCurrentLineCounts()
        {
            // An open backtick on the line above does not make this line code
            Assert.Equal("`open\r“quote”", Type("\"quote\"", "`open\r"));
        }

        [Fact]
        public void TheCaretGoesAfterTheReplacement()
        {
            var edit = SmartPunctuationUtility.GetReplacement("a--", 3).Value;
            Assert.Equal(1, edit.Start);
            Assert.Equal(2, edit.Length);
            Assert.Equal(2, edit.SelectionStart);
            Assert.Equal(0, edit.SelectionLength);
        }
    }
}
