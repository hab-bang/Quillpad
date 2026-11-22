namespace Quillpad.Tests
{
    using System;
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

        [Fact]
        public void FileNameHasTheTimeAndTheProcessId()
        {
            var time = new DateTime(2026, 11, 22, 6, 15, 0, DateTimeKind.Utc);

            Assert.Equal("20261122T061500_4242.log", LogFileUtility.GetFileName(time, 4242));
        }

        [Fact]
        public void TwoWindowsInTheSameSecondGetTwoFiles()
        {
            var time = new DateTime(2026, 11, 22, 6, 15, 0, DateTimeKind.Utc);

            Assert.NotEqual(LogFileUtility.GetFileName(time, 100), LogFileUtility.GetFileName(time, 200));
        }

        [Fact]
        public void CleanupKnowsNewAndOldNames()
        {
            var names = new[]
            {
                "20261122T061500_4242.log",
                "20261121T061500.log",
                "20261120T061500_17.log",
                "20261122T061500_4242 (2).log",
            };

            var toDelete = LogFileUtility.GetFileNamesToDelete(names, filesToKeep: 1);

            Assert.Equal(new[] { "20261121T061500.log", "20261120T061500_17.log" }, toDelete);
        }

        [Fact]
        public void NewestFileSortsFirstWhatEverItsProcessId()
        {
            var names = new[] { "20261122T061500_9.log", "20261122T061501_10000.log" };

            Assert.Equal(new[] { "20261122T061500_9.log" }, LogFileUtility.GetFileNamesToDelete(names, filesToKeep: 1));
        }

        [Fact]
        public void AllLinesFitInAnEmptyFile()
        {
            var lines = new[] { "a", "bb", "ccc" };

            Assert.Equal(3, LogFileUtility.CountLinesThatFit(lines, bytesInFile: 0, maxBytes: 1000));
        }

        [Fact]
        public void LinesStopAtTheLimit()
        {
            var lines = new[] { "1234567890", "1234567890", "1234567890" }; // 12 bytes each with CR LF

            Assert.Equal(2, LogFileUtility.CountLinesThatFit(lines, bytesInFile: 0, maxBytes: 30));
            Assert.Equal(1, LogFileUtility.CountLinesThatFit(lines, bytesInFile: 10, maxBytes: 30));
            Assert.Equal(0, LogFileUtility.CountLinesThatFit(lines, bytesInFile: 30, maxBytes: 30));
        }

        [Fact]
        public void ByteCountIsUtf8PlusLineBreak()
        {
            Assert.Equal(3, LogFileUtility.GetByteCount("a"));
            Assert.Equal(6, LogFileUtility.GetByteCount("éé")); // 2 bytes each in UTF-8, plus CR LF
            Assert.Equal(2, LogFileUtility.GetByteCount(null));
        }
    }
}
