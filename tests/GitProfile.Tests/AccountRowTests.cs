using GitProfile.Core;
using GitProfile.Tests.Fakes;

namespace GitProfile.Tests;

/// <summary>
/// The window draws one row per account, so each row has to carry its own label, colour and role -
/// computed here rather than in a template, so a row cannot silently render as an empty strip.
/// </summary>
public class AccountRowTests : IDisposable
{
    private const string Repo = "C:/repos/app";

    private readonly GitProfileHarness _h = new();

    public void Dispose()
    {
        if (File.Exists(_h.IdentityFile)) File.Delete(_h.IdentityFile);
    }

    private AccountPickerModel Model() => new(_h.Service, _h.AccountsSource, _h.Identities, Repo);

    [Fact]
    public void Every_signed_in_account_gets_a_row_in_order()
    {
        var rows = Model().Rows;

        Assert.Equal(new[] { "aojha111", "abhijitojha7" }, rows.Select(r => r.Login));
    }

    [Fact]
    public void The_default_accounts_row_says_it_is_the_default()
    {
        _h.Service.Apply(_h.Service.PlanSetDefault("abhijitojha7"));

        var row = Model().Rows.Single(r => r.Login == "abhijitojha7");

        Assert.True(row.IsDefault);
        Assert.Equal("default account", row.Role);
    }

    [Fact]
    public void The_pinned_accounts_row_says_it_is_pinned_here()
    {
        _h.RepoRoot = Repo;
        _h.Service.Apply(_h.Service.PlanPin("abhijitojha7", Repo), Repo);

        var row = Model().Rows.Single(r => r.Login == "abhijitojha7");

        Assert.True(row.IsPinned);
        Assert.Equal("pinned to this repository", row.Role);
    }

    [Fact]
    public void An_account_that_is_both_default_and_pinned_says_both()
    {
        _h.RepoRoot = Repo;
        _h.Service.Apply(_h.Service.PlanSetDefault("abhijitojha7"));
        _h.Service.Apply(_h.Service.PlanPin("abhijitojha7", Repo), Repo);

        var row = Model().Rows.Single(r => r.Login == "abhijitojha7");

        Assert.Equal("default account, pinned here", row.Role);
    }

    [Fact]
    public void An_unused_account_admits_that_nothing_has_chosen_it()
    {
        var row = Model().Rows.Single(r => r.Login == "aojha111");

        Assert.Equal("signed in, but nothing chosen yet", row.Role);
    }

    [Fact]
    public void An_unused_account_beside_a_default_says_who_it_belongs_to()
    {
        _h.Service.Apply(_h.Service.PlanSetDefault("abhijitojha7"));

        var row = Model().Rows.Single(r => r.Login == "aojha111");

        Assert.Equal("signed in on this machine", row.Role);
    }

    [Fact]
    public void Outside_a_repository_only_the_default_counts()
    {
        _h.Service.Apply(_h.Service.PlanSetDefault("abhijitojha7"));

        var model = new AccountPickerModel(_h.Service, _h.AccountsSource, _h.Identities, Repo);

        Assert.False(model.InRepository);
        Assert.Equal("default account", model.Rows.Single(r => r.Login == "abhijitojha7").Role);
    }

    [Fact]
    public void Rows_reappear_when_an_account_is_added()
    {
        var model = Model();

        _h.Accounts.Add("third-account");
        model.Refresh();

        Assert.Contains("third-account", model.Rows.Select(r => r.Login));
    }

    [Theory]
    [InlineData("aojha111", "AO")]
    [InlineData("a", "A")]
    [InlineData("7lives", "7L")]
    public void A_row_shows_initials_from_the_login(string login, string initials)
    {
        _h.Accounts.Add(login);

        Assert.Equal(initials, Model().Rows.Single(r => r.Login == login).Initials);
    }

    [Fact]
    public void The_same_login_always_gets_the_same_avatar_colour()
    {
        var first = Model().Rows.Single(r => r.Login == "aojha111").Avatar;
        var second = Model().Rows.Single(r => r.Login == "aojha111").Avatar;

        Assert.Equal(first, second);
        Assert.Matches("^#[0-9A-F]{6}$", first);
    }

    [Fact]
    public void Different_logins_get_different_avatar_colours()
    {
        var rows = Model().Rows;

        Assert.NotEqual(rows[0].Avatar, rows[1].Avatar);
    }
}
