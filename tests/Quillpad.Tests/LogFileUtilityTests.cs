namespace Quillpad.Tests
{
    using System.Linq;
    using Notepads.Utilities;
    using Xunit;

    public class LogFileUtilityTests
    {
        [Theory]
        [InlineData("Error", false, true)]
        [InlineData("Warning", false, true)]
        [InlineData("Info", false, false)]
        [InlineData("Info", true, true)]
        [InlineData("Error", true, true)]
        public void WritesInfoOnlyWhenAsked(string level, bool includeInfo, bool expected)
        {
            Assert.Equal(expected, LogFileUtility.ShouldWriteToFile(level, includeInfo));
        }

        [Fact]
        public void KeepsTheNewestFiles()
        {
            var names = new[]
            {
                "20261120T080000.log",
                "20261122T061500.log",
                "20261121T235959.log",
                "20261119T120000.log",
            };

            var toDelete = LogFileUtility.GetFileNamesToDelete(names, filesToKeep: 2);

            Assert.Equal(new[] { "20261120T080000.log", "20261119T120000.log" }, toDelete);
        }

        [Fact]
        public void DeletesNothingWhenThereAreFewFiles()
        {
            var names = new[] { "20261122T061500.log", "20261121T061500.log" };

            Assert.Empty(LogFileUtility.GetFileNamesToDelete(names, filesToKeep: 10));
        }

        [Fact]
        public void LeavesOtherFilesAlone()
        {
            var names = new[]
            {
                "20261122T061500.log",
                "notes.txt",
                "20261122T061500.log.bak",
                "crash.log",
                "20261101T000000.log",
            };

            var toDelete = LogFileUtility.GetFileNamesToDelete(names, filesToKeep: 1);

            Assert.Equal(new[] { "20261101T000000.log" }, toDelete);
        }

        [Fact]
        public void KeepZeroDeletesEveryLogFile()
        {
            var names = Enumerable.Range(10, 5).Select(day => $"202611{day}T000000.log").ToArray();

            Assert.Equal(5, LogFileUtility.GetFileNamesToDelete(names, filesToKeep: 0).Count);
        }
    }
}
