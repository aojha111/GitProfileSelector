using GitProfile.Core;
using GitProfile.Tests.Fakes;

namespace GitProfile.Tests;

public class ProfileServiceTests : IDisposable
{
    private const string PinKey = "credential.https://github.com.username";
    private const string Repo = "C:/repos/app";

    private readonly GitProfileHarness _h = new();

    public void Dispose()
    {
        if (File.Exists(_h.IdentityFile)) File.Delete(_h.IdentityFile);
    }

    private ProfileService Service => _h.Service;

    private void PinAsDefault(string login) => Service.Apply(Service.PlanSetDefault(login));

    private void PinInRepo(string login) => Service.Apply(Service.PlanPin(login, Repo), Repo);

    [Fact]
    public void SetDefault_pins_github_pushes_to_the_chosen_account()
    {
        PinAsDefault("abhijitojha7");

        Assert.Equal("abhijitojha7", _h.GlobalConfig[PinKey]);
    }

    [Fact]
    public void SetDefault_also_signs_commits_as_that_profile()
    {
        _h.SetIdentity("abhijitojha7", "Abhijit Ojha", "abhijitojha7@users.noreply.github.com");

        PinAsDefault("abhijitojha7");

        Assert.Equal("Abhijit Ojha", _h.GlobalConfig["user.name"]);
        Assert.Equal("abhijitojha7@users.noreply.github.com", _h.GlobalConfig["user.email"]);
    }

    [Fact]
    public void SetDefault_leaves_commit_identity_alone_when_nothing_is_stored_for_the_profile()
    {
        PinAsDefault("abhijitojha7");

        Assert.False(_h.GlobalConfig.ContainsKey("user.name"));
        Assert.False(_h.GlobalConfig.ContainsKey("user.email"));
    }

    [Fact]
    public void SetDefault_applies_whichever_half_of_an_identity_is_known()
    {
        _h.SetIdentity("abhijitojha7", "Abhijit Ojha", email: null);

        PinAsDefault("abhijitojha7");

        Assert.Equal("Abhijit Ojha", _h.GlobalConfig["user.name"]);
        Assert.False(_h.GlobalConfig.ContainsKey("user.email"));
    }

    [Fact]
    public void SetDefault_rejects_an_account_that_is_not_signed_in()
    {
        Assert.Throws<ProfileNotFoundException>(() => Service.PlanSetDefault("stranger"));
    }

    [Fact]
    public void Pin_writes_at_repository_scope()
    {
        _h.RepoRoot = Repo;

        PinInRepo("abhijitojha7");

        Assert.Equal("abhijitojha7", _h.LocalConfig[PinKey]);
    }

    [Fact]
    public void Pin_refuses_a_path_that_is_not_a_repository()
    {
        _h.RepoRoot = null;

        Assert.Throws<NotInRepositoryException>(() => Service.PlanPin("abhijitojha7", @"C:\Users\me"));
    }

    [Fact]
    public void Pinning_a_repository_leaves_the_user_default_untouched()
    {
        _h.RepoRoot = Repo;
        PinAsDefault("aojha111");

        PinInRepo("abhijitojha7");

        Assert.Equal("aojha111", _h.GlobalConfig[PinKey]);
        Assert.Equal("abhijitojha7", _h.LocalConfig[PinKey]);
    }

    [Fact]
    public void Pin_signs_commits_of_that_repository_only()
    {
        _h.RepoRoot = Repo;
        _h.SetIdentity("abhijitojha7", "Abhijit Ojha", "abhijitojha7@users.noreply.github.com");

        PinInRepo("abhijitojha7");

        Assert.Equal("abhijitojha7@users.noreply.github.com", _h.LocalConfig["user.email"]);
        Assert.False(_h.GlobalConfig.ContainsKey("user.email"));
    }

    [Fact]
    public void ClearPin_removes_the_repository_overrides()
    {
        _h.RepoRoot = Repo;
        _h.SetIdentity("abhijitojha7", "Abhijit Ojha", "abhijitojha7@example.com");
        PinInRepo("abhijitojha7");

        Service.Apply(Service.PlanClearPin(Repo), Repo);

        Assert.Empty(_h.LocalConfig);
    }

    [Fact]
    public void ClearPin_is_safe_when_the_repository_was_never_pinned()
    {
        _h.RepoRoot = Repo;

        Service.Apply(Service.PlanClearPin(Repo), Repo);

        Assert.Empty(_h.LocalConfig);
    }

    [Fact]
    public void ClearPin_does_not_disturb_the_user_default()
    {
        _h.RepoRoot = Repo;
        PinAsDefault("aojha111");
        PinInRepo("abhijitojha7");

        Service.Apply(Service.PlanClearPin(Repo), Repo);

        Assert.Equal("aojha111", _h.GlobalConfig[PinKey]);
    }

    [Fact]
    public void Status_reports_the_user_default_and_the_repository_pin_separately()
    {
        _h.RepoRoot = Repo;
        PinAsDefault("aojha111");
        PinInRepo("abhijitojha7");

        var status = Service.Status(Repo);

        Assert.Equal("aojha111", status.DefaultLogin);
        Assert.Equal("abhijitojha7", status.PinnedLogin);
    }

    [Fact]
    public void Status_reports_no_repository_when_the_path_is_not_a_repo()
    {
        _h.RepoRoot = null;

        var status = Service.Status(@"C:\Users\me");

        Assert.Null(status.RepoRoot);
        Assert.Null(status.PinnedLogin);
    }

    [Fact]
    public void Status_names_the_owner_of_the_origin_remote_as_a_hint()
    {
        _h.RepoRoot = Repo;
        _h.Origin = "https://github.com/abhijitojha7/knowledge-hub.git";

        var status = Service.Status(Repo);

        Assert.Equal("https://github.com/abhijitojha7/knowledge-hub.git", status.OriginUrl);
        Assert.Equal("abhijitojha7", status.OriginOwner);
    }

    [Fact]
    public void The_effective_account_prefers_the_repository_pin()
    {
        _h.RepoRoot = Repo;
        PinAsDefault("aojha111");
        PinInRepo("abhijitojha7");

        Assert.Equal("abhijitojha7", Service.Status(Repo).EffectiveLogin);
    }

    [Fact]
    public void The_effective_account_falls_back_to_the_user_default()
    {
        _h.RepoRoot = Repo;
        PinAsDefault("aojha111");

        Assert.Equal("aojha111", Service.Status(Repo).EffectiveLogin);
    }

    [Fact]
    public void Each_planned_edit_describes_itself_for_the_preview_dialog()
    {
        _h.SetIdentity("abhijitojha7", "Abhijit Ojha", "abhijitojha7@example.com");

        var lines = Service.PlanSetDefault("abhijitojha7").Select(e => e.Describe()).ToList();

        Assert.Collection(lines.OrderBy(l => l),
            l => Assert.Equal("git config --global credential.https://github.com.username abhijitojha7", l),
            l => Assert.Equal("git config --global user.email abhijitojha7@example.com", l),
            l => Assert.Equal("git config --global user.name Abhijit Ojha", l));
    }

    [Fact]
    public void A_planned_removal_describes_itself_as_an_unset()
    {
        Assert.Equal("git config --local --unset credential.https://github.com.username",
            new ConfigEdit(ConfigScope.Local, PinKey, null).Describe());
    }

    [Fact]
    public void The_profile_list_comes_from_the_credential_manager()
    {
        Assert.Equal(new[] { "aojha111", "abhijitojha7" }, Service.ListProfiles());
    }

    [Fact]
    public void Applying_edits_never_touches_the_repository_store_from_a_user_edit()
    {
        PinAsDefault("aojha111");

        Assert.Empty(_h.LocalConfig);
    }
}
