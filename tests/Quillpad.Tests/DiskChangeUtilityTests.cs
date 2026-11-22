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
    }
}
