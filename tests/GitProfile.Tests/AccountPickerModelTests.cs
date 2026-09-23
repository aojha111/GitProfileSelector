using GitProfile.Core;
using GitProfile.Tests.Fakes;

namespace GitProfile.Tests;

/// <summary>
/// The picker model is what the window shows and what the buttons do. These tests are the reason
/// a control can no longer end up invisible: whether Apply is available is model state, not layout.
/// </summary>
public class AccountPickerModelTests : IDisposable
{
    private const string Repo = "C:/repos/app";

    private readonly GitProfileHarness _h = new();

    public void Dispose()
    {
        if (File.Exists(_h.IdentityFile)) File.Delete(_h.IdentityFile);
    }

    private AccountPickerModel Model(string workingDirectory = Repo) =>
        new(_h.Service, _h.AccountsSource, _h.Identities, workingDirectory);

    [Fact]
    public void Opens_with_the_signed_in_accounts_listed()
    {
        var model = Model();

        Assert.Equal(new[] { "aojha111", "abhijitojha7" }, model.Accounts);
        Assert.True(model.HasAccounts);
    }

    [Fact]
    public void Opens_with_nothing_selected_when_nothing_is_configured_yet()
    {
        Assert.Null(Model().Selected);
    }

    [Fact]
    public void Opens_on_the_account_git_would_actually_use()
    {
        _h.Service.Apply(_h.Service.PlanSetDefault("abhijitojha7"));

        Assert.Equal("abhijitojha7", Model().Selected);
    }

    [Fact]
    public void The_default_choice_is_pre_ticked_for_the_account_that_is_the_default()
    {
        _h.Service.Apply(_h.Service.PlanSetDefault("abhijitojha7"));
        var model = Model();

        model.Selected = "abhijitojha7";

        Assert.True(model.AsDefault);
    }

    [Fact]
    public void The_repository_choice_is_pre_ticked_for_the_account_pinned_here()
    {
        _h.RepoRoot = Repo;
        _h.Service.Apply(_h.Service.PlanPin("abhijitojha7", Repo), Repo);
        var model = Model();

        model.Selected = "abhijitojha7";

        Assert.True(model.PinRepository);
    }

    [Fact]
    public void Choosing_a_different_account_resets_the_ticked_choices()
    {
        _h.Service.Apply(_h.Service.PlanSetDefault("abhijitojha7"));
        var model = Model();

        model.Selected = "aojha111";

        Assert.False(model.AsDefault);
        Assert.False(model.PinRepository);
    }

    [Fact]
    public void Nothing_can_be_applied_until_a_choice_is_ticked()
    {
        var model = Model();
        model.Selected = "aojha111";

        Assert.False(model.CanApply);
    }

    [Fact]
    public void Ticking_a_choice_makes_apply_available()
    {
        var model = Model();
        model.Selected = "aojha111";

        model.AsDefault = true;

        Assert.True(model.CanApply);
    }

    [Fact]
    public void The_preview_names_the_exact_commands_before_anything_runs()
    {
        var model = Model();
        model.Selected = "aojha111";
        model.AsDefault = true;

        Assert.Equal([$"git config --global {GitConfigKeys.GitHubAccount} aojha111"], model.Preview);
    }

    [Fact]
    public void Ticking_both_choices_previews_both_scopes()
    {
        _h.RepoRoot = Repo;
        var model = Model();
        model.Selected = "abhijitojha7";
        model.AsDefault = true;
        model.PinRepository = true;

        Assert.Collection(model.Preview,
            line => Assert.Contains("--global", line),
            line => Assert.Contains("--local", line));
    }

    [Fact]
    public void With_nothing_ticked_there_is_nothing_to_preview()
    {
        var model = Model();
        model.Selected = "aojha111";

        Assert.Empty(model.Preview);
    }

    [Fact]
    public void Apply_makes_the_selected_account_the_user_default()
    {
        var model = Model();
        model.Selected = "abhijitojha7";
        model.AsDefault = true;

        model.Apply();

        Assert.Equal("abhijitojha7", _h.GlobalConfig[GitConfigKeys.GitHubAccount]);
        Assert.Equal("abhijitojha7", model.DefaultLogin);
    }

    [Fact]
    public void Apply_reports_what_it_did_in_plain_words()
    {
        var model = Model();
        model.Selected = "abhijitojha7";
        model.AsDefault = true;

        model.Apply();

        Assert.Contains("default account is now abhijitojha7", model.Headline);
    }

    [Fact]
    public void Apply_writes_the_repository_pin_when_that_is_ticked()
    {
        _h.RepoRoot = Repo;
        var model = Model();
        model.Selected = "abhijitojha7";
        model.PinRepository = true;

        model.Apply();

        Assert.Equal("abhijitojha7", _h.LocalConfig[GitConfigKeys.GitHubAccount]);
        Assert.Equal("aojha111", _h.GlobalConfig.GetValueOrDefault(GitConfigKeys.GitHubAccount, "aojha111"));
    }

    [Fact]
    public void Apply_carries_the_typed_commit_identity_with_it()
    {
        var model = Model();
        model.Selected = "abhijitojha7";
        model.CommitName = "Abhijit Ojha";
        model.CommitEmail = "ojha@example.com";
        model.SaveIdentity();
        model.AsDefault = true;

        model.Apply();

        Assert.Equal("Abhijit Ojha", _h.GlobalConfig[GitConfigKeys.CommitName]);
        Assert.Equal("ojha@example.com", _h.GlobalConfig[GitConfigKeys.CommitEmail]);
    }

    [Fact]
    public void The_commit_fields_show_the_saved_identity_of_the_selected_account()
    {
        _h.SetIdentity("abhijitojha7", "Abhijit Ojha", "ojha@example.com");
        var model = Model();

        model.Selected = "abhijitojha7";

        Assert.Equal("Abhijit Ojha", model.CommitName);
        Assert.Equal("ojha@example.com", model.CommitEmail);
    }

    [Fact]
    public void The_commit_email_suggests_the_noreply_address_when_nothing_is_saved()
    {
        var model = Model();

        model.Selected = "abhijitojha7";

        Assert.Equal("abhijitojha7@users.noreply.github.com", model.CommitEmail);
        Assert.Equal("", model.CommitName);
    }

    [Fact]
    public void Saving_an_empty_commit_name_stores_nothing_rather_than_a_blank()
    {
        var model = Model();
        model.Selected = "abhijitojha7";
        model.CommitName = "   ";
        model.CommitEmail = "ojha@example.com";

        model.SaveIdentity();
        model.Selected = "aojha111";
        model.Selected = "abhijitojha7";

        Assert.Equal("", model.CommitName);
        Assert.Equal("ojha@example.com", model.CommitEmail);
    }

    [Fact]
    public void Outside_a_repository_the_picker_says_so_and_offers_no_pin()
    {
        _h.RepoRoot = null;

        var model = Model(@"C:\Users\me");

        Assert.False(model.InRepository);
        Assert.False(model.CanPinRepository);
    }

    [Fact]
    public void Inside_a_repository_the_picker_shows_which_account_owns_the_remote()
    {
        _h.RepoRoot = Repo;
        _h.Origin = "https://github.com/abhijitojha7/knowledge-hub.git";

        var model = Model();

        Assert.Equal("abhijitojha7", model.OriginOwner);
    }

    [Fact]
    public void Unpinning_returns_the_repository_to_the_default_account()
    {
        _h.RepoRoot = Repo;
        _h.Service.Apply(_h.Service.PlanSetDefault("aojha111"));
        _h.Service.Apply(_h.Service.PlanPin("abhijitojha7", Repo), Repo);
        var model = Model();

        model.Unpin();

        Assert.Null(model.PinnedLogin);
        Assert.Equal("aojha111", model.EffectiveLogin);
    }

    [Fact]
    public void The_headline_explains_why_being_asked_happens_at_all()
    {
        var model = Model();

        Assert.Contains("no default account", model.Headline);
    }

    [Fact]
    public void A_git_failure_becomes_a_headline_instead_of_crashing_the_window()
    {
        _h.GitFailure = ("fatal: The configuration file has an invalid entry", 128);
        var model = Model();
        model.Selected = "abhijitojha7";
        model.AsDefault = true;

        model.Apply();

        Assert.Contains("invalid entry", model.Headline);
        Assert.False(model.CanApply);
    }

    [Fact]
    public void Signing_in_anew_account_appears_afterwards()
    {
        var model = Model();

        _h.Accounts.Add("third-account");
        model.Refresh();

        Assert.Contains("third-account", model.Accounts);
    }

    [Fact]
    public void Removing_the_selected_account_logs_it_out_of_the_credential_manager()
    {
        var model = Model();
        model.Selected = "abhijitojha7";

        model.RemoveSelectedAccount();

        Assert.Contains(_h.GcmCalls, call => call.Arguments.SequenceEqual(["github", "logout", "abhijitojha7"]));
    }

    [Fact]
    public void With_no_accounts_signed_in_the_picker_offers_to_sign_one_in()
    {
        _h.Accounts.Clear();

        var model = Model();

        Assert.False(model.HasAccounts);
        Assert.False(model.CanApply);
        Assert.Contains("no GitHub account is signed in", model.Headline);
    }

    [Fact]
    public void Pointing_the_window_at_another_repository_moves_the_pin_along()
    {
        _h.RepoRoot = Repo;
        _h.Service.Apply(_h.Service.PlanPin("abhijitojha7", Repo), Repo);
        var model = Model("C:/elsewhere");

        Assert.False(model.CanPinRepository);
        model.UseRepository(Repo);

        Assert.True(model.CanPinRepository);
        Assert.Equal("abhijitojha7", model.PinnedLogin);
    }

    [Fact]
    public void The_header_chip_only_names_an_account_once_one_is_chosen()
    {
        Assert.False(Model().HasEffectiveLogin);

        _h.Service.Apply(_h.Service.PlanSetDefault("abhijitojha7"));

        Assert.True(Model().HasEffectiveLogin);
    }

    [Fact]
    public void The_tray_can_make_an_account_the_default_without_a_selection()
    {
        var model = Model();

        model.MakeDefault("abhijitojha7");

        Assert.Equal("abhijitojha7", _h.GlobalConfig[GitConfigKeys.GitHubAccount]);
        Assert.Equal("abhijitojha7", model.DefaultLogin);
        Assert.Contains("default account is now abhijitojha7", model.Headline);
    }

    [Fact]
    public void A_tray_choice_that_git_rejects_is_reported_instead_of_thrown()
    {
        var model = Model();
        _h.GitFailure = ("fatal: cannot lock config", 128);

        model.MakeDefault("abhijitojha7");

        Assert.Contains("cannot lock config", model.Headline);
    }

    [Fact]
    public void Only_a_pinned_repository_offers_to_unpin()
    {
        _h.RepoRoot = Repo;
        var outside = Model();

        _h.Service.Apply(_h.Service.PlanPin("abhijitojha7", Repo), Repo);
        var pinned = Model();

        Assert.False(outside.CanUnpin);
        Assert.True(pinned.CanUnpin);
    }

    [Fact]
    public void The_commit_identity_card_needs_someone_selected()
    {
        var model = Model();

        Assert.False(model.HasSelection);
        model.Selected = "aojha111";
        Assert.True(model.HasSelection);
    }

    [Fact]
    public void Property_changes_are_announced_so_the_window_can_follow()
    {
        var model = Model();
        var raised = new List<string?>();
        ((System.ComponentModel.INotifyPropertyChanged)model).PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        model.Selected = "aojha111";

        Assert.Contains(nameof(AccountPickerModel.CanApply), raised);
        Assert.Contains(nameof(AccountPickerModel.Preview), raised);
        Assert.Contains(nameof(AccountPickerModel.AsDefault), raised);
        Assert.Contains(nameof(AccountPickerModel.HasSelection), raised);
        Assert.Contains(nameof(AccountPickerModel.CanUnpin), raised);
        Assert.Contains(nameof(AccountPickerModel.Rows), raised);
    }
}
