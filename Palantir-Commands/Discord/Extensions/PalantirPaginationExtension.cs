using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Extensions;

namespace Palantir_Commands.Discord.Extensions;

public static class PalantirPaginationExtension
{
    public static async Task RespondPalantirPaginationAsync(this CommandContext context, List<DiscordEmbedBuilder> pages, string pageListingName = "Page", int? startPage = null)
    {
        var interactivity = context.Client.GetInteractivity();

        var currentPage = startPage - 1 ?? 0;
        const string nextPageId = "nextPage";
        const string prevPageId = "prevPage";
        DiscordComponentEmoji nextPageEmoji = new DiscordComponentEmoji { Id = 1217564955729985586, Name = "tt_award_stonks"};
        DiscordComponentEmoji prevPageEmoji = new DiscordComponentEmoji { Id = 1218616453864099871, Name = "tt_award_notstonks"};

        DiscordMessageBuilder BuildPage(int page, bool enablePagination = true)
        {
            var isLastPage = page == pages.Count - 1;
            var isFirstPage = page == 0;

            var prevBtn = new DiscordButtonComponent(DiscordButtonStyle.Secondary, prevPageId, "",
                !enablePagination || isFirstPage, prevPageEmoji);
            var infoBtn = new DiscordButtonComponent(DiscordButtonStyle.Secondary, "label",
                $"Navigate {pageListingName} ({page + 1}/{pages.Count})", true, null);
            var nextBtn = new DiscordButtonComponent(DiscordButtonStyle.Secondary, nextPageId, "",
                !enablePagination || isLastPage, nextPageEmoji);

            return new DiscordMessageBuilder()
                .AddEmbed(pages[page].Build())
                .AddComponents(prevBtn, infoBtn, nextBtn);
        }

        // send first page as response
        await context.RespondAsync(BuildPage(currentPage));
        var response = await context.GetResponseAsync();

        // create navigation flow for user
        var buttonEvent = await interactivity.WaitForButtonAsync(response, context.User, TimeSpan.FromMinutes(1));
        while(!buttonEvent.TimedOut)
        {
            await buttonEvent.Result.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage);
            
            var buttonId = buttonEvent.Result.Id;
            currentPage += buttonId == nextPageId ? 1 : -1;

            var newPage = BuildPage(currentPage);
            await response.ModifyAsync(newPage);
            buttonEvent = await interactivity.WaitForButtonAsync(response, context.User, TimeSpan.FromMinutes(1));
        }
        
        // disable navigation after timeout
        await response.ModifyAsync(BuildPage(currentPage, false));
    }
}