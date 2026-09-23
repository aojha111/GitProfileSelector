using GitProfile.Core;

namespace GitProfile.Tests;

public class CommandLineTests
{
    private static ParseOutcome Parse(params string[] args) => CommandLine.Parse(args);

    [Fact]
    public void An_empty_command_line_asks_for_help()
    {
        Assert.IsType<ParseOutcome.Help>(Parse());
    }

    [Fact]
    public void help_asks_for_help()
    {
        var outcome = Parse("help");
        var help = Assert.IsType<ParseOutcome.Help>(outcome);
        Assert.Null(help.Verb);
    }

    [Fact]
    public void use_names_the_account_to_make_the_user_default()
    {
        var request = Assert.IsType<ParseOutcome.Request>(Parse("use", "aojha111"));

        Assert.Equal("use", request.Verb);
        Assert.Equal("aojha111", request.Account);
    }

    [Fact]
    public void pin_without_a_path_pins_the_current_directory()
    {
        var request = Assert.IsType<ParseOutcome.Request>(Parse("pin", "abhijitojha7"));

        Assert.Equal("abhijitojha7", request.Account);
        Assert.Null(request.Path);
    }

    [Fact]
    public void pin_accepts_an_explicit_repository_path()
    {
        var request = Assert.IsType<ParseOutcome.Request>(Parse("pin", "abhijitojha7", @"C:\repos\app"));

        Assert.Equal(@"C:\repos\app", request.Path);
    }

    [Fact]
    public void unpin_needs_no_account()
    {
        Assert.Equal("unpin", Assert.IsType<ParseOutcome.Request>(Parse("unpin")).Verb);
    }

    [Fact]
    public void status_is_another_name_for_current()
    {
        Assert.Equal("current", Assert.IsType<ParseOutcome.Request>(Parse("status")).Verb);
    }

    [Fact]
    public void dry_run_shows_what_would_happen_without_applying_it()
    {
        Assert.True(Assert.IsType<ParseOutcome.Request>(Parse("use", "aojha111", "--dry-run")).DryRun);
        Assert.False(Assert.IsType<ParseOutcome.Request>(Parse("use", "aojha111")).DryRun);
    }

    [Fact]
    public void set_identity_carries_a_name_and_an_email()
    {
        var request = Assert.IsType<ParseOutcome.Request>(
            Parse("set-identity", "aojha111", "--name", "Abhijit Ojha", "--email", "a@example.com"));

        Assert.Equal("aojha111", request.Account);
        Assert.Equal("Abhijit Ojha", request.Name);
        Assert.Equal("a@example.com", request.Email);
    }

    [Fact]
    public void A_verb_needing_an_account_says_so_when_it_is_missing()
    {
        var rejected = Assert.IsType<ParseOutcome.Rejected>(Parse("use"));

        Assert.Contains("account", rejected.Reason);
    }

    [Fact]
    public void An_unknown_verb_is_rejected_with_the_valid_ones()
    {
        var rejected = Assert.IsType<ParseOutcome.Rejected>(Parse("frobnicate"));

        Assert.Contains("frobnicate", rejected.Reason);
        Assert.Contains("use", rejected.Reason);
    }

    [Fact]
    public void An_unknown_option_is_rejected()
    {
        Assert.IsType<ParseOutcome.Rejected>(Parse("use", "aojha111", "--frobnicate"));
    }

    [Fact]
    public void Extra_positional_arguments_are_rejected()
    {
        Assert.IsType<ParseOutcome.Rejected>(Parse("use", "aojha111", "abhijitojha7"));
    }

    [Fact]
    public void The_help_text_documents_every_verb()
    {
        foreach (var verb in CommandLine.Verbs)
            Assert.Contains(verb, CommandLine.HelpText);
    }

    [Theory]
    [InlineData("list")]
    [InlineData("current")]
    [InlineData("add")]
    [InlineData("remove")]
    [InlineData("set-identity")]
    [InlineData("pin")]
    [InlineData("unpin")]
    [InlineData("use")]
    public void Every_documented_verb_parses(string verb)
    {
        string[] args = verb is "remove" or "set-identity" or "pin" or "use"
            ? [verb, "someone"]
            : [verb];

        Assert.IsType<ParseOutcome.Request>(CommandLine.Parse(args));
    }
}
