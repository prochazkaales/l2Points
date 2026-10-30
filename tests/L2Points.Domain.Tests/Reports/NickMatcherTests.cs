using L2Points.Domain.Reports;

namespace L2Points.Domain.Tests.Reports;

public class NickMatcherTests
{
    private static NickCandidate[] Candidates(params string[] nicks) =>
        nicks.Select((n, i) => new NickCandidate($"u{i}", n)).ToArray();

    [Fact]
    public void Exact_ignores_case_and_whitespace()
    {
        var result = NickMatcher.Match([" hubkins1010 "], Candidates("Hubkins1010"));

        var match = Assert.Single(result.Matches);
        Assert.Equal("hubkins1010", match.ReadNick);
        Assert.Equal("Hubkins1010", match.Nick);
        Assert.Equal("u0", match.UserId);
        Assert.Equal(NickMatchKind.Exact, match.Kind);
        Assert.Empty(result.Unknown);
    }

    [Fact]
    public void Zero_and_letter_O_are_similar()
    {
        var result = NickMatcher.Match(["V0lume"], Candidates("Volume"));

        var match = Assert.Single(result.Matches);
        Assert.Equal("Volume", match.Nick);
        Assert.Equal(NickMatchKind.Similar, match.Kind);
    }

    [Fact]
    public void Digit_one_and_letter_l_are_similar()
    {
        var result = NickMatcher.Match(["1inaya"], Candidates("Linaya"));

        var match = Assert.Single(result.Matches);
        Assert.Equal("Linaya", match.Nick);
        Assert.Equal(NickMatchKind.Similar, match.Kind);
    }

    [Fact]
    public void Short_nick_allows_distance_one()
    {
        var result = NickMatcher.Match(["Mayu"], Candidates("Mayo"));

        var match = Assert.Single(result.Matches);
        Assert.Equal("Mayo", match.Nick);
        Assert.Equal(NickMatchKind.Similar, match.Kind);
    }

    [Fact]
    public void Short_nick_rejects_distance_two()
    {
        var result = NickMatcher.Match(["Mayu"], Candidates("Mara"));

        Assert.Empty(result.Matches);
        Assert.Equal(["Mayu"], result.Unknown);
    }

    [Fact]
    public void Long_nick_allows_distance_two()
    {
        var result = NickMatcher.Match(["Bosorka12"], Candidates("Bosorkaxy"));

        var match = Assert.Single(result.Matches);
        Assert.Equal("Bosorkaxy", match.Nick);
        Assert.Equal(NickMatchKind.Similar, match.Kind);
    }

    [Fact]
    public void Tie_is_unknown()
    {
        var result = NickMatcher.Match(["Asapo"], Candidates("Asapa", "Asape"));

        Assert.Empty(result.Matches);
        Assert.Equal(["Asapo"], result.Unknown);
    }

    [Fact]
    public void Exact_wins_over_earlier_similar()
    {
        var result = NickMatcher.Match(["V0lume", "Volume"], Candidates("Volume"));

        var match = Assert.Single(result.Matches);
        Assert.Equal("Volume", match.ReadNick);
        Assert.Equal(NickMatchKind.Exact, match.Kind);
        Assert.Equal(["V0lume"], result.Unknown);
    }

    [Fact]
    public void Duplicates_collapse()
    {
        var result = NickMatcher.Match(["Mayu", "MAYU"], Candidates("Mayu"));

        var match = Assert.Single(result.Matches);
        Assert.Equal("Mayu", match.ReadNick);
        Assert.Empty(result.Unknown);
    }

    [Fact]
    public void Empty_read_nicks_are_ignored()
    {
        var result = NickMatcher.Match(["", "  "], Candidates("Mayu"));

        Assert.Empty(result.Matches);
        Assert.Empty(result.Unknown);
    }
}
