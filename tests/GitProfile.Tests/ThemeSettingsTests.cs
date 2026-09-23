using GitProfile.Core;

namespace GitProfile.Tests;

public class ThemeSettingsTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"gitprofile-theme-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        if (File.Exists(_file)) File.Delete(_file);
    }

    private ThemeSettings Settings => new(_file);

    [Fact]
    public void Load_falls_back_to_system_before_anything_is_saved()
    {
        Assert.Equal(AppTheme.System, Settings.Load());
    }

    [Theory]
    [InlineData(AppTheme.Light)]
    [InlineData(AppTheme.Dark)]
    [InlineData(AppTheme.System)]
    public void Saved_mode_is_read_back(AppTheme mode)
    {
        Settings.Save(mode);

        Assert.Equal(mode, Settings.Load());
    }

    [Fact]
    public void Saving_a_new_mode_replaces_the_old_one()
    {
        Settings.Save(AppTheme.Light);
        Settings.Save(AppTheme.Dark);

        Assert.Equal(AppTheme.Dark, Settings.Load());
    }

    [Fact]
    public void Corrupt_file_falls_back_to_system_rather_than_throwing()
    {
        File.WriteAllText(_file, "{ not json");

        Assert.Equal(AppTheme.System, Settings.Load());
    }

    [Fact]
    public void An_unrecognised_stored_value_falls_back_to_system()
    {
        File.WriteAllText(_file, """{"theme":"hotdog"}""");

        Assert.Equal(AppTheme.System, Settings.Load());
    }

    [Fact]
    public void A_hand_written_file_is_read_whatever_its_property_casing()
    {
        File.WriteAllText(_file, """{"Theme":"dark"}""");

        Assert.Equal(AppTheme.Dark, Settings.Load());
    }
}
