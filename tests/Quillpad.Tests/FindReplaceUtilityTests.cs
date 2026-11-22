namespace Quillpad.Tests
{
    using System;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Notepads.Controls.FindAndReplace;
    using Notepads.Utilities;
    using Xunit;

    public class FindReplaceUtilityTests
    {
        private static string ReplaceAll(string text, SearchContext context, string replaceText)
        {
            return FindReplaceUtility.Apply(text, FindReplaceUtility.FindAll(text, context, replaceText));
        }

        [Theory]
        [InlineData("Ana met ana. ANA left.", false, false, "Bea met Bea. Bea left.")]
        [InlineData("Ana met ana. ANA left.", true, false, "Bea met ana. ANA left.")]
        [InlineData("Ana and Anabel", false, true, "Bea and Anabel")]
        [InlineData("Ana and Anabel", false, false, "Bea and Beabel")]
        public void PlainReplaceAll(string text, bool matchCase, bool wholeWord, string expected)
        {
            Assert.Equal(expected, ReplaceAll(text, new SearchContext("Ana", matchCase, wholeWord), "Bea"));
        }

        [Fact]
        public void ReplacementThatContainsTheSearchTextIsNotReplacedAgain()
        {
            Assert.Equal("aab aab", ReplaceAll("ab ab", new SearchContext("ab"), "aab"));
        }

        [Fact]
        public void NoMatchGivesNoReplacements()
        {
            Assert.Empty(FindReplaceUtility.FindAll("text", new SearchContext("zzz"), "y"));
            Assert.Empty(FindReplaceUtility.FindAll("text", new SearchContext(""), "y"));
        }

        [Fact]
        public void RegexReplaceAllExpandsGroups()
        {
            var context = new SearchContext(@"(\w+) (\w+)", useRegex: true);
            Assert.Equal("b a\rd c", ReplaceAll("a b\rc d", context, "$2 $1"));
        }

        [Fact]
        public void SingleRegexReplacementExpandsGroups()
        {
            var match = FindReplaceUtility.CreateRegex(new SearchContext(@"Mr\. (\w+)", useRegex: true)).Match("Mr. Darcy");
            Assert.Equal("Darcy", FindReplaceUtility.ExpandRegexReplacement(match, "$1"));
        }

        [Fact]
        public void ReplacementsThatKeepTheTextAreDropped()
        {
            // Rewriting a match with the same text would still reset its format in the editor
            var text = "*one* and *two* and *three*";
            var context = new SearchContext(@"\*(\w+)\*", useRegex: true);
            Assert.Empty(FindReplaceUtility.WithoutUnchanged(text, FindReplaceUtility.FindAll(text, context, "*$1*")));

            var replacements = FindReplaceUtility.FindAll(text, new SearchContext(@"\*(o\w+|t\w+)\*", useRegex: true), "*one*");
            var changed = FindReplaceUtility.WithoutUnchanged(text, replacements);
            Assert.Equal(new[] { 10, 20 }, changed.Select(r => r.Start));
            Assert.Equal("*one* and *one* and *one*", FindReplaceUtility.Apply(text, changed));
        }

        [Theory]
        [InlineData(6, 5, "<world>")] // Find next selects "world"
        [InlineData(6, 2, "<wo>")]    // Find previous from "wo|rld" selects "wo" (right to left)
        public void SelectedMatchIsExpandedInBothDirections(int start, int length, string expected)
        {
            var context = new SearchContext(@"\w+", useRegex: true);
            Assert.Equal(expected, FindReplaceUtility.ExpandReplacementForSelection("hello world", context, start, length, "<$0>"));
        }

        [Fact]
        public void SelectionThatIsNotAMatchIsNotExpanded()
        {
            var context = new SearchContext(@"\w+", useRegex: true);
            Assert.Null(FindReplaceUtility.ExpandReplacementForSelection("hello world", context, 5, 1, "<$0>"));
        }

        [Fact]
        public void SingleReplaceStopsAtTheTimeout()
        {
            var text = new string('a', 40) + "b";
            Assert.Throws<RegexMatchTimeoutException>(() =>
                FindReplaceUtility.ExpandReplacementForSelection(text, new SearchContext("(a+)+$", useRegex: true), 0, 41, "x"));
        }

        [Fact]
        public void RegexSeesLineEndsAndTypedLineBreaksBecomeEditorLineBreaks()
        {
            var context = new SearchContext(@"end$", useRegex: true);
            Assert.Equal("END\rline\rEND", ReplaceAll("end\rline\rend", context, @"END"));
            Assert.Equal("a\rb", ReplaceAll("a, b", new SearchContext(", ", useRegex: true), @"\n"));
            Assert.Equal("a\tb", ReplaceAll("a b", new SearchContext(" ", useRegex: true), @"\t"));
        }

        [Fact]
        public void RegexCaseFollowsMatchCase()
        {
            Assert.Equal("x x", ReplaceAll("A a", new SearchContext("a", matchCase: false, useRegex: true), "x"));
            Assert.Equal("A x", ReplaceAll("A a", new SearchContext("a", matchCase: true, useRegex: true), "x"));
        }

        [Fact]
        public void EmptyRegexMatchesInsertText()
        {
            Assert.Equal("> a\r> b", ReplaceAll("a\rb", new SearchContext("^", useRegex: true), "> "));
        }

        [Fact]
        public void BadPatternThrows()
        {
            Assert.ThrowsAny<ArgumentException>(() => FindReplaceUtility.FindAll("text", new SearchContext("(", useRegex: true), "x"));
        }

        [Fact]
        public void RunawayPatternStopsAtTheTimeout()
        {
            var text = new string('a', 40) + "b";
            Assert.Throws<RegexMatchTimeoutException>(() => FindReplaceUtility.FindAll(text, new SearchContext("(a+)+$", useRegex: true), "x"));
        }

        [Theory]
        [InlineData(0, 0)]   // Before the first match
        [InlineData(4, 4)]   // Right at a match start
        [InlineData(5, 4)]   // Inside a match: moves to the start of the new text
        [InlineData(7, 9)]   // Just after the first match (3 chars became 5)
        [InlineData(12, 14)] // At the second match start
        [InlineData(16, 20)] // The end of the text
        public void MapsPositionsThroughReplacements(int position, int expected)
        {
            // "The Ana and Ana." -> "The Beata and Beata."
            var replacements = FindReplaceUtility.FindAll("The Ana and Ana.", new SearchContext("Ana"), "Beata");
            Assert.Equal(expected, FindReplaceUtility.MapPosition(position, replacements));
        }

        [Fact]
        public void MapsPositionsWhenTheTextShrinks()
        {
            var replacements = FindReplaceUtility.FindAll("xxxx yy xxxx", new SearchContext("xxxx"), "z");
            Assert.Equal(2, FindReplaceUtility.MapPosition(5, replacements)); // The "y"s keep their place
            Assert.Equal(6, FindReplaceUtility.MapPosition(12, replacements)); // The end of the text
        }
    }
}
