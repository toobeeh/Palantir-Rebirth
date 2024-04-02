using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.TextCommands.Attributes;
using DSharpPlus.Commands.Trees.Attributes;
using DSharpPlus.Entities;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using MoreLinq;
using Palantir_Commands.Discord.Extensions;
using Valmar;

namespace Palantir_Commands.Discord.Commands;

[Command("league")]
[TextAlias("lg")]
public class LeagueCommands(
    ILogger<SceneCommands> logger, 
    Leagues.LeaguesClient leaguesClient,
    Members.MembersClient membersClient)
{
    /// <summary>
    /// View the overview of a drop league season
    /// </summary>
    /// <param name="context"></param>
    /// <param name="seasonMonth">The month (1-12) of the season</param>
    /// <param name="seasonYear">The year (eg 2023) of the season</param>
    /// <exception cref="Exception"></exception>
    [DefaultGroupCommand]
    [Command("season")]
    public async Task ViewSeasonOverview(CommandContext context, int? seasonMonth = null, int? seasonYear = null)
    {
        logger.LogTrace("ViewSeasonOverview(context, {seasonMonth}, {seasonYear})", seasonMonth, seasonYear);

        LeagueSeasonEvaluationReply season;
        if(seasonMonth is not null && seasonYear is not null)
        {
            season = await leaguesClient.EvaluateLeagueSeasonAsync(new EvaluateSeasonRequest
            {
                Month = seasonMonth.Value,
                Year = seasonYear.Value
            });
        }
        else if(seasonMonth is null && seasonYear is null)
        {
            season = await leaguesClient.EvaluateCurrentLeagueSeasonAsync(new Empty());
        }
        else
        {
            await context.RespondAsync(new DiscordEmbedBuilder().WithPalantirErrorPresets(context,
                "You must provide both a month and a year to view a specific league season, or neither to view the current season."));
            return;
        }

        var embedBuilder = new DiscordEmbedBuilder()
                .WithPalantirPresets(context)
                .WithAuthor("Viewing a Drop League season summary")
                .WithTitle($"{season.SeasonStart.ToDateTimeOffset():MMMM yyyy} Season")
                .WithDescription(
                    $"Drop Leagues are a monthly competition, where the very fastest catchers rank against each other.\nYou can use `/league rank` to view your own stats, or `/league board` to view the complete ranking.\n_ _\n" +
                    $"➜ {season.ScoreRanking.Count} participants in this season\n" +
                    $"➜ Season lasts from {Formatter.Timestamp(season.SeasonStart.ToDateTimeOffset(), TimestampFormat.ShortDate)} to {Formatter.Timestamp(season.SeasonEnd.ToDateTimeOffset(), TimestampFormat.ShortDate)}\n" +
                    $"_ _\n_ _\n")
                .WithThumbnail(
                    "https://media.discordapp.net/attachments/910894527261327370/983025068214992948/challenge.gif?ex=661bf1ef&is=66097cef&hm=9b14da7ca272f84b70b040e31292a36ebb213ab48d90dd950abd078329a52e8c&")
            ;

        void AddTopRank(int index)
        {
            string[] rankEmotes =
            [
                "<a:league_rnk1:987699431350632518>", "<a:league_rnk2:987710613893566515>",
                "<a:league_rnk3:987716889352470528>"
            ];
            var name = season.ScoreRanking[index].Name;
            var id = season.ScoreRanking[index].UserId;
            var title = $"{rankEmotes[index]}  **{name}**";
            var description = $"  ```\n#{index + 1} - {season.ScoreRanking[index].Score:0.#}dw```\n" +
                              $"> **{season.CountRanking.First(rnk => rnk.UserId == id).CaughtDrops}** drops\n" +
                              $"> **{season.WeightRanking.First(rnk => rnk.UserId == id).AverageWeight * 100 :0}%** avg.weight\n" +
                              $"> **{season.TimeRanking.First(rnk => rnk.UserId == id).AverageTime:0}ms** avg.time\n" +
                              $"> **{season.StreakRanking.First(rnk => rnk.UserId == id).MaxStreak}** max.streak\n";
            embedBuilder.AddField(title, description, true);
        }

        void AddBriefRank(int indexStart, int indexEnd)
        {
            var title = $"<a:league_rnk4:987723143982514207> Ranks #{indexStart + 1} - #{indexEnd + 1}";
            var description = string.Join("\n", season.ScoreRanking.ToList().GetRange(indexStart, indexEnd - indexStart + 1)
                .Select((rnk, index) =>
                    $"{rnk.Name}:\n #{indexStart + index + 1} - {rnk.Score:0.#}dw / {season.WeightRanking.First(subrnk => subrnk.Name == rnk.Name).AverageWeight * 100 :0}%"));

            embedBuilder.AddField(title, $"```py\n{description}```", true);
        }
        
        // add ranks
        if(season.ScoreRanking.Count == 0) embedBuilder.AddField("No participants", "No one has caught drops in this season yet.");
        else AddTopRank(0);
        if(season.ScoreRanking.Count > 1) AddTopRank(1);
        if(season.ScoreRanking.Count > 2) AddTopRank(2);
        if(season.ScoreRanking.Count > 3) AddBriefRank(3, 6);
        if(season.ScoreRanking.Count > 6) AddBriefRank(6, 9);
        
        // add category leaders
        embedBuilder.AddField("`⚔️` Category Leaders",
            $"```md\n" +
            $"> Overall:\n {season.ScoreRanking[0].Name} - {season.ScoreRanking[0].Score:0.#}dw\n" +
            $"> Average Weight:\n {season.WeightRanking[0].Name} - {season.WeightRanking[0].AverageWeight * 100 :0}%\n" +
            $"> Most Drops:\n {season.CountRanking[0].Name} - {season.CountRanking[0].CaughtDrops}\n" +
            $"> Longest Streak:\n {season.StreakRanking[0].Name} - {season.StreakRanking[0].MaxStreak}\n" +
            $"```");
        
        await context.RespondAsync(embedBuilder.Build());
    }
    
    /// <summary>
    /// View your own stats of a drop league season
    /// </summary>
    /// <param name="context"></param>
    /// <param name="seasonMonth">The month (1-12) of the season</param>
    /// <param name="seasonYear">The year (eg 2023) of the season</param>
    /// <exception cref="Exception"></exception>
    [Command("rank")]
    public async Task ViewSeasonRank(CommandContext context, int? seasonMonth = null, int? seasonYear = null)
    {
        logger.LogTrace("ViewSeasonRank(context, {seasonMonth}, {seasonYear})", seasonMonth, seasonYear);

        var user = await membersClient.GetMemberByDiscordIdAsync(new IdentifyMemberByDiscordIdRequest {Id = (long)context.User.Id});
        
        LeagueSeasonMemberEvaluationReply rank;
        LeagueSeasonEvaluationReply season;
        if(seasonMonth is not null && seasonYear is not null)
        {
            rank = await leaguesClient.EvaluateMemberLeagueSeasonAsync(new EvaluateMemberSeasonRequest
            {
                Month = seasonMonth.Value,
                Year = seasonYear.Value,
                Login = user.Login
            });
            season = await leaguesClient.EvaluateLeagueSeasonAsync(new EvaluateSeasonRequest
            {
                Month = seasonMonth.Value,
                Year = seasonYear.Value
            });
        }
        else if(seasonMonth is null && seasonYear is null)
        {
            rank = await leaguesClient.EvaluateMemberCurrentLeagueSeasonAsync(new EvaluateMemberCurrentSeasonRequest {Login = user.Login});
            season = await leaguesClient.EvaluateCurrentLeagueSeasonAsync(new Empty());
        }
        else
        {
            await context.RespondAsync(new DiscordEmbedBuilder().WithPalantirErrorPresets(context,
                "You must provide both a month and a year to view a specific league season, or neither to view the current season."));
            return;
        }

        var embedBuilder = new DiscordEmbedBuilder()
                .WithPalantirPresets(context)
                .WithAuthor("Viewing your Drop League season ranking")
                .WithTitle($"{rank.SeasonStart.ToDateTimeOffset():MMMM yyyy} Season")
                .WithDescription(
                    $"Drop Leagues are a monthly competition, where the very fastest catchers rank against each other.\nYou can use `/league season` to see a summary of the season, or `/league board` to view the complete ranking.\n_ _\n" +
                    $"➜ {season.ScoreRanking.Count} participants in this season\n" +
                    $"➜ Season lasts from {Formatter.Timestamp(rank.SeasonStart.ToDateTimeOffset(), TimestampFormat.ShortDate)} to {Formatter.Timestamp(rank.SeasonEnd.ToDateTimeOffset(), TimestampFormat.ShortDate)}\n" +
                    $"_ _\n_ _\n")
                .WithThumbnail(
                    "https://media.discordapp.net/attachments/910894527261327370/983025068214992948/challenge.gif?ex=661bf1ef&is=66097cef&hm=9b14da7ca272f84b70b040e31292a36ebb213ab48d90dd950abd078329a52e8c&")
            ;
        
        
        string[] rankEmotes =
        [
            "<a:league_rnk1:987699431350632518>", "<a:league_rnk2:987710613893566515>",
            "<a:league_rnk3:987716889352470528>", "<a:league_rnk4:987723143982514207>"
        ];

        var totalRank = season.ScoreRanking.ToList().FindIndex(rnk => rnk.UserId == user.DiscordId) + 1;
        var dropsRank = season.CountRanking.ToList().FindIndex(rnk => rnk.UserId == user.DiscordId) + 1;
        var streakRank = season.StreakRanking.ToList().FindIndex(rnk => rnk.UserId == user.DiscordId) + 1;
        var weightRank = season.WeightRanking.ToList().FindIndex(rnk => rnk.UserId == user.DiscordId) + 1;
        
        var rankHint = 
            totalRank == 1 ? "You are the leader of this season!" : 
            totalRank is 2 or 3 ? "You are in the top 3 of this season!" : 
            totalRank <= 10 ? "You are in the top 10 of this season!" : 
            "Catch more League Drops to be ranked below the top 10 players.";

        embedBuilder.AddField($"{rankEmotes[Math.Min(totalRank - 1, 3)]} Overall ranked as **#{totalRank}**", rankHint);
        
        
        // add category positions
        embedBuilder.AddField("`⚔️` Category Positions",
            $"```md\n" +
            $"> Overall:\n {rank.Score : 0.#}dw (#{totalRank})\n" +
            $"> Average Weight:\n {rank.AverageWeight * 100 : 0.}% (#{weightRank}) / {rank.AverageTime : 0}ms average\n" +
            $"> Most Drops:\n {rank.Count} caught (#{dropsRank})\n" +
            $"> Longest Streak:\n {rank.MaxStreak} max, {rank.CurrentStreak} current (#{streakRank})\n" +
            $"```");
        
        await context.RespondAsync(embedBuilder.Build());
    }
    
    /// <summary>
    /// View the entire league ranking board of a drop league season
    /// </summary>
    /// <param name="context"></param>
    /// <param name="seasonMonth">The month (1-12) of the season</param>
    /// <param name="seasonYear">The year (eg 2023) of the season</param>
    /// <exception cref="Exception"></exception>
    [Command("board")]
    public async Task ViewSeasonBoard(CommandContext context, int? seasonMonth = null, int? seasonYear = null)
    {
        logger.LogTrace("ViewSeasonBoard(context, {seasonMonth}, {seasonYear})", seasonMonth, seasonYear);
        
        LeagueSeasonEvaluationReply season;
        if(seasonMonth is not null && seasonYear is not null)
        {
            season = await leaguesClient.EvaluateLeagueSeasonAsync(new EvaluateSeasonRequest
            {
                Month = seasonMonth.Value,
                Year = seasonYear.Value
            });
        }
        else if(seasonMonth is null && seasonYear is null)
        {
            season = await leaguesClient.EvaluateCurrentLeagueSeasonAsync(new Empty());
        }
        else
        {
            await context.RespondAsync(new DiscordEmbedBuilder().WithPalantirErrorPresets(context,
                "You must provide both a month and a year to view a specific league season, or neither to view the current season."));
            return;
        }

        
        
        // batch ranks to 30 per page
        const int batchSize = 30;
        var pages = season.ScoreRanking.Batch(batchSize).Select((batch, idx) => new
        {
            Page = idx + 1,
            Ranks = batch
        }).Select(page =>
        {
            var rankStart = batchSize * (page.Page - 1) + 1;
            var embedBuilder = new DiscordEmbedBuilder()
                .WithPalantirPresets(context)
                .WithAuthor("Viewing the Drop League season board")
                .WithTitle($"{season.SeasonStart.ToDateTimeOffset():MMMM yyyy} Season");

            var rankHeader =
                $"#   " +
                $" {"Name", 22} " +
                $" {"Score", 6} " +
                $"  Ø {"Weight", 6} " +
                $" {"Streak", 6} ";
            var rankText = page.Ranks.Select((rnk, index) =>
                $"{rankStart + index, 3} " +
                $" {rnk.Name.Replace("_", "⎽").Replace("*", "*"), 22} " +
                $" {rnk.Score, 6:0.#} " +
                $" {season.WeightRanking.First(subrnk => subrnk.UserId == rnk.UserId).AverageWeight * 100, 6:0}% " +
                $" {season.StreakRanking.First(subrnk => subrnk.UserId == rnk.UserId).MaxStreak, 6} ")
                .ToList();

            embedBuilder.WithDescription($"This board shows the ranking of all season participants ordered by drop weight score.\n_ _\n" +
                                         $"```md\n{rankHeader}\n{string.Join("\n", rankText)}\n```");
            return embedBuilder;
        }).ToList();

        await context.RespondPalantirPaginationAsync(pages);
    }
}