namespace Quillpad.Tests
{
    using System;
    using Notepads.Utilities;
    using Xunit;

    public class DiskChangeUtilityTests
    {
        private static readonly long LoadedAt = new DateTime(2026, 11, 22, 6, 15, 0, DateTimeKind.Utc).ToFileTime();

        [Fact]
        public void UnchangedFileIsSafeToWrite()
        {
            Assert.False(DiskChangeUtility.HasChangedOnDisk(LoadedAt, LoadedAt));
        }

        [Fact]
        public void NewerFileWasChangedByAnotherApp()
        {
            Assert.True(DiskChangeUtility.HasChangedOnDisk(LoadedAt, LoadedAt + TimeSpan.FromSeconds(2).Ticks));
        }

        [Fact]
        public void OlderFileCountsAsChanged()
        {
            // git checkout or a sync tool can put back an older copy with its old time
            Assert.True(DiskChangeUtility.HasChangedOnDisk(LoadedAt, LoadedAt - TimeSpan.FromDays(1).Ticks));
        }

        [Fact]
        public void UnknownLastTimeCountsAsChanged()
        {
            // A tab brought back from a backup newer than the session has no known file time (-1)
            Assert.True(DiskChangeUtility.HasChangedOnDisk(-1, LoadedAt));
        }

        [Fact]
        public void UnreadableFileTimeDoesNotBlockTheSave()
        {
            Assert.False(DiskChangeUtility.HasChangedOnDisk(LoadedAt, null));
        }

        [Fact]
        public void SameTextWithCrlfOnDiskIsTheSame()
        {
            // The editor keeps each line break as CR, the file has CR LF
            Assert.True(DiskChangeUtility.HasSameText("She left.\r\nThe door shut.", "She left.\rThe door shut."));
        }

        [Fact]
        public void SameTextWithLfOnDiskIsTheSame()
        {
            Assert.True(DiskChangeUtility.HasSameText("one\ntwo\n", "one\rtwo\r"));
        }

        [Fact]
        public void ChangedWordIsNotTheSame()
        {
            Assert.False(DiskChangeUtility.HasSameText("She left.\r\n", "She stayed.\r"));
        }

        [Fact]
        public void ExtraLineBreakIsNotTheSame()
        {
            Assert.False(DiskChangeUtility.HasSameText("one\r\ntwo\r\n", "one\rtwo"));
        }

        [Fact]
        public void CaseCounts()
        {
            Assert.False(DiskChangeUtility.HasSameText("she left", "She left"));
        }

        [Fact]
        public void MissingTextIsNeverTheSame()
        {
            Assert.False(DiskChangeUtility.HasSameText(null, "text"));
            Assert.False(DiskChangeUtility.HasSameText("text", null));
        }
    }
}
