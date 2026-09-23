using GitProfile.Core;

namespace GitProfile.Tests;

public class IdentityStoreTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"gitprofile-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        if (File.Exists(_file)) File.Delete(_file);
    }

    private IdentityStore Store => new(_file);

    [Fact]
    public void Load_is_empty_before_anything_is_saved()
    {
        Assert.Empty(Store.Load());
    }

    [Fact]
    public void Saved_identity_is_read_back()
    {
        Store.Save("aojha111", "Abhijit Ojha", "aojha111@users.noreply.github.com");

        var identity = Assert.Single(Store.Load());
        Assert.Equal("aojha111", identity.Login);
        Assert.Equal("Abhijit Ojha", identity.Name);
        Assert.Equal("aojha111@users.noreply.github.com", identity.Email);
    }

    [Fact]
    public void Saving_one_login_leaves_the_other_profiles_alone()
    {
        Store.Save("aojha111", "One", "one@example.com");
        Store.Save("abhijitojha7", "Two", "two@example.com");

        var all = Store.Load().ToDictionary(i => i.Login, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("One", all["aojha111"].Name);
        Assert.Equal("Two", all["abhijitojha7"].Name);
    }

    [Fact]
    public void Saving_the_same_login_again_replaces_it_rather_than_duplicating()
    {
        Store.Save("aojha111", "Old name", "old@example.com");
        Store.Save("aojha111", "New name", "new@example.com");

        var identity = Assert.Single(Store.Load());
        Assert.Equal("New name", identity.Name);
        Assert.Equal("new@example.com", identity.Email);
    }

    [Fact]
    public void Lookups_ignore_the_case_of_the_login()
    {
        Store.Save("aojha111", "One", "one@example.com");

        Assert.NotNull(Store.Find("AOJHA111"));
    }

    [Fact]
    public void Find_returns_null_for_an_unknown_login()
    {
        Assert.Null(Store.Find("nobody"));
    }

    [Fact]
    public void A_missing_file_is_not_an_error()
    {
        Store.Save("aojha111", "One", "one@example.com");
        File.Delete(_file);

        Assert.Empty(Store.Load());
    }

    [Fact]
    public void Corrupt_file_is_reported_rather_than_silently_ignored()
    {
        File.WriteAllText(_file, "{ not json");

        Assert.Throws<IdentityStoreCorruptException>(Store.Load);
    }

    [Fact]
    public void A_login_without_a_stored_identity_gets_the_noreply_email()
    {
        Assert.Equal("abhijitojha7@users.noreply.github.com", GitHubIdentity.SuggestedEmail("abhijitojha7"));
    }
}
