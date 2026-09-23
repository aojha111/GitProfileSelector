using GitProfile.Core;

namespace GitProfile.Tests;

public class AppPathsTests
{
    [Fact]
    public void The_identity_file_sits_in_the_callers_app_data_folder()
    {
        var path = AppPaths.IdentityFile(@"C:\Users\me\AppData\Local");

        Assert.Equal(
            Path.Combine(@"C:\Users\me\AppData\Local", "GitProfileSelector", "profiles.json"),
            path);
    }

    [Fact]
    public void With_no_folder_given_it_uses_the_real_local_app_data()
    {
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GitProfileSelector",
            "profiles.json");

        Assert.Equal(expected, AppPaths.IdentityFile());
    }
}
