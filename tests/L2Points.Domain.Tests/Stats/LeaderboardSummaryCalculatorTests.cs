using L2Points.Domain.Stats;

namespace L2Points.Domain.Tests.Stats;

public class LeaderboardSummaryCalculatorTests
{
    private static readonly DateOnly ActiveFrom = new(2026, 10, 26);

    private static LeaderboardRow Row(string id, int overallPoints, int weekPoints = 0, CategoryStat? total = null) =>
        new(
            new PlayerInfo(id, id, true, ActiveFrom, null),
            new PlayerSummary(overallPoints, Stats(total ?? default)),
            new PlayerSummary(weekPoints, ParticipationStats.Empty));

    private static ParticipationStats Stats(CategoryStat normal) =>
        new(new Dictionary<StatCategory, CategoryStat> { [StatCategory.Normal] = normal });

    [Fact]
    public void Ranks_share_a_place_on_equal_points()
    {
        var summary = LeaderboardSummaryCalculator.Calculate([Row("A", 50), Row("B", 30), Row("C", 30), Row("D", 10)], null);

        Assert.Equal(1, summary.Ranks["A"]);
        Assert.Equal(2, summary.Ranks["B"]);
        Assert.Equal(2, summary.Ranks["C"]);
        Assert.Equal(4, summary.Ranks["D"]);
    }

    [Fact]
    public void Ranks_by_week_points_ignore_row_order()
    {
        var ranks = LeaderboardSummaryCalculator.Ranks([Row("A", 90, 5), Row("B", 50, 20), Row("C", 10, 5)], r => r.Week.Points);

        Assert.Equal(1, ranks["B"]);
        Assert.Equal(2, ranks["A"]);
        Assert.Equal(2, ranks["C"]);
    }

    [Fact]
    public void Party_attendance_sums_attended_and_expected_of_all_rows()
    {
        var summary = LeaderboardSummaryCalculator.Calculate(
            [Row("A", 0, total: new CategoryStat(8, 10)), Row("B", 0, total: new CategoryStat(1, 30))], null);

        Assert.Equal(new CategoryStat(9, 40), summary.PartyAttendance);
    }

    [Fact]
    public void Week_points_are_summed()
    {
        var summary = LeaderboardSummaryCalculator.Calculate([Row("A", 0, 20), Row("B", 0, 15)], null);

        Assert.Equal(35, summary.WeekPoints);
    }

    [Fact]
    public void Players_of_week_are_all_with_the_most_week_points()
    {
        var summary = LeaderboardSummaryCalculator.Calculate([Row("A", 90, 10), Row("B", 50, 20), Row("C", 10, 20)], null);

        Assert.Equal(["B", "C"], summary.PlayersOfWeek.Select(p => p.UserId));
        Assert.Equal(20, summary.PlayersOfWeekPoints);
    }

    [Fact]
    public void No_player_of_week_without_week_points()
    {
        var summary = LeaderboardSummaryCalculator.Calculate([Row("A", 90), Row("B", 50)], null);

        Assert.Empty(summary.PlayersOfWeek);
    }

    [Fact]
    public void Podium_takes_first_three_rows_with_points()
    {
        var summary = LeaderboardSummaryCalculator.Calculate([Row("A", 90), Row("B", 50), Row("C", 40), Row("D", 30)], null);

        Assert.Equal(["A", "B", "C"], summary.Podium.Select(r => r.Player.UserId));
    }

    [Fact]
    public void Podium_skips_players_without_points()
    {
        var summary = LeaderboardSummaryCalculator.Calculate([Row("A", 90), Row("B", 0), Row("C", 0)], null);

        Assert.Equal(["A"], summary.Podium.Select(r => r.Player.UserId));
    }

    [Fact]
    public void Standing_of_leader_shows_lead_over_next_player()
    {
        var summary = LeaderboardSummaryCalculator.Calculate([Row("A", 90), Row("B", 50)], "A");

        Assert.Equal(new PlayerStanding(1, false, null, 40), summary.Me);
    }

    [Fact]
    public void Standing_of_chaser_shows_gap_to_nearest_higher_score()
    {
        var summary = LeaderboardSummaryCalculator.Calculate([Row("A", 90), Row("B", 70), Row("C", 70), Row("D", 20)], "D");

        Assert.Equal(new PlayerStanding(4, false, 50, null), summary.Me);
    }

    [Fact]
    public void Standing_on_shared_place()
    {
        var summary = LeaderboardSummaryCalculator.Calculate([Row("A", 90), Row("B", 90), Row("C", 20)], "B");

        Assert.Equal(new PlayerStanding(1, true, null, 70), summary.Me);
    }

    [Fact]
    public void Standing_is_null_for_player_not_in_rows()
    {
        var summary = LeaderboardSummaryCalculator.Calculate([Row("A", 90)], "X");

        Assert.Null(summary.Me);
    }
}
