namespace Quillpad.Tests
{
    using Notepads.Utilities;
    using Xunit;

    public class NewInstallUtilityTests
    {
        private const string Marker = "Marker";
        private static readonly string[] Startup = { "ActiveInstanceIdStr" };

        [Fact]
        public void EmptyStoreIsANewInstall()
        {
            Assert.True(NewInstallUtility.IsNewInstall(new string[0], Marker, Startup));
        }

        [Fact]
        public void KeysFromThisStartAloneAreANewInstall()
        {
            Assert.True(NewInstallUtility.IsNewInstall(new[] { "ActiveInstanceIdStr" }, Marker, Startup));
        }

        [Fact]
        public void AnyOtherSettingMeansAnEarlierRun()
        {
            Assert.False(NewInstallUtility.IsNewInstall(new[] { "ActiveInstanceIdStr", "RequestedThemeStr" }, Marker, Startup));
        }

        [Fact]
        public void TheMarkerMeansTheDefaultsWereHandledBefore()
        {
            Assert.False(NewInstallUtility.IsNewInstall(new[] { Marker }, Marker, Startup));
        }
    }
}
