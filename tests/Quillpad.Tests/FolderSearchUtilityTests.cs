namespace Quillpad.Tests
{
    using System.Linq;
    using Notepads.Controls.FindAndReplace;
    using Notepads.Utilities;
    using Xunit;

    public class FolderSearchUtilityTests
    {
        [Fact]
        public void FindsOneResultPerLineWithItsPosition()
        {
            var matches = FolderSearchUtility.FindInText("Alice ran.\r\nBob and alice, alice.\nNobody.", "alice", matchCase: false, maxResults: 10);

            Assert.Equal(new[] { (1, 1), (2, 9) }, matches.Select(m => (m.Line, m.Column)));
            Assert.Equal("Bob and alice, alice.", matches[1].Snippet);
        }

        [Fact]
        public void MatchCaseIsOptional()
        {
            Assert.Single(FolderSearchUtility.FindInText("Alice\nalice", "alice", matchCase: true, maxResults: 10));
        }

        [Fact]
        public void StopsAtTheLimit()
        {
            var text = string.Join("\r", Enumerable.Repeat("word", 50));
            Assert.Equal(5, FolderSearchUtility.FindInText(text, "word", false, 5).Count);
        }

        [Fact]
        public void LongLinesAreCutAroundTheMatch()
        {
            var line = new string('a', 200) + "needle" + new string('b', 200);
            var snippet = FolderSearchUtility.FindInText(line, "needle", false, 1).Single().Snippet;

            Assert.Contains("needle", snippet);
            Assert.StartsWith("…", snippet);
            Assert.EndsWith("…", snippet);
            Assert.True(snippet.Length <= FolderSearchUtility.SnippetLength + 2);
        }

        [Fact]
        public void NothingForAnEmptyQuery()
        {
            Assert.Empty(FolderSearchUtility.FindInText("text", "", false, 10));
        }

        [Fact]
        public void CountsMatchesAndFindsTheSelectedOne()
        {
            var text = "one two one three one";
            var context = new SearchContext("one");

            Assert.Equal(3, FindReplaceUtility.CountMatches(text, context, 8, 3, out var current));
            Assert.Equal(2, current);

            Assert.Equal(3, FindReplaceUtility.CountMatches(text, context, 4, 3, out current));
            Assert.Equal(0, current); // "two" is selected, not a match
        }
    }
}
