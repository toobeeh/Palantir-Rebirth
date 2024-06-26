# Palantir-Rebirth
[![part of Typo ecosystem](https://img.shields.io/badge/Typo%20ecosystem-PalantirRebirth-blue?style=flat&logo=data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAIAAAACACAYAAADDPmHLAAACV0lEQVR4nO3dPUrDYByA8UQ8g15AI+gsOOnmrufoIBT0DAUFB+/R3bFTobOCwQvoJSouNcObhHyZ9n2eHwiirW3Th79J2iaJJEmSJEmSJIC06iGu1+vgz9M0Df9CY6t8PkP2fMrYDADOAOAMAM4A4OrWGl3bj0Pp8+wEgDMAuP2uD//w7I6+DEf19fbc6eadAHAGAGcAcAYAZwBwnbcCTrIj+jL8Fx/55yA34wSAMwA4A4AzADgDgDMAOAOAMwC4zjuCzi+uN9+fZgeNrvuefw+69FfL10H/fgycAHAGAGcAcAYAZwBwnbcCioZeq2+quIVS5NbBHycAnAHARffRsOksr71Ml38Bi/mk9XVH5EfDFGYAcHVbAWWjw08NbyePEaRmDADOAOAMAM4A4Fq9FjCd5cG1zaeHrPeleXnzsvl+MZ802vooe4fSatn9ftUILp/iYxlCm51UTgA4A4Dr9eXgsv3wtJdfhx71fXICwBkAXGUAv+cLCH0pHk4AOAOAMwA4A4AzALhedwRpXBVneSu9X04AOAOAMwA4A4AzADgDgDMAOAOAMwA4A4AzADgDgDMAOAOAMwA4A4AzALio3xG0bUcu3UZOADgDgDMAOAOAMwC4qLcCRjxG0M5wAsAZAJwBwBkAnAHAGQCcAcAZAJwBwBkAnAHA+Y4gOCcAnAHAGQCcAcAZAFyrrYDH++NGl7+6ZZ0yZpc4AeAMAC66HUFDnLwyZk4AOAOAKz+QfMXx58dScdz7se5o8A7t0HJzAtAZAJwBwBkAnAFIkiRJkiRJUtySJPkBweNXgRaWkYQAAAAASUVORK5CYII=)](https://github.com/topics/skribbl-typo)  

Palantir Rebirth is the refactor of the Palantir Bot.  
It consists of multiple components that can be deployed individually for scalability and maintainability (blah blah).  
Palantir-Rebirth follows the new separation of business+data from application layer and uses toobeeh/Valmar as grpc backend.

The components are split to Core, Commands, Public and Lobbies modules.

## gRPC Clients
GRPC clients for Valmar and ImageGen are installed from their NuGet packages.  
For local development, the packages can be switched to a local version in the csproj files.
When pushing to the repository, the packages should be switched back to the NuGet version.
This is especially important to ensure nuget has the latest version of the packages.

## Core
Core contains features that are vital for the complete typo ecosystem, like dispatching drops, cleaning database, fetching patrons, and everything that happens on a scheduled basis.  
Core should not be replicated; can be considered rather as a client and performs non-cpu-heavy things.

Tasks of core module (planned and implemented):
- [x] Update patrons & boosters and write to user flags
- [x] Dispatch drops (get online count, get active event, send drop request and calculate next timeout)
- [x] Clear volatile data
- [x] Initiate bubble traces
- [x] Increment user bubbles in 10s interval
- [x] Set online items

## Commands
Commands is a class library and uses a DSharpPlus Bot to bring the functionality of the current Palantir Bot to Discord, using the gRPC backend.
Commands are split into following categories.
Checklist for implementation:

### Sprites
- [x] `>sprite view [id]` / `>sprite [id]` - View a sprite
- [x] `>sprite buy [id]` - Buy a sprite
- [x] `>sprite use [id] (slot)` - Use a sprite on a slot
- [x] `>sprite combo (...ids)` - Use a combo of sprites
- [x] `>sprite color [id] (color shift)` - Use a rainbow shift color on a sprite
- [x] `>sprite inventory` - View all bought sprites and total value
- [x] `>sprite list` - View a ranking of all sprites

### Scenes 
- [x] `>scene view [id]` / `>scene [id]` - View a scene
- [x] `>scene buy [id]` - Buy a scene
- [x] `>scene use (id)` - Use a scene
- [x] `>scene inv` - View all bought scenes and the next scene price
- [x] `>scene list` - View a ranking of all sprites

### Outfits
- [x] `>outfit` / `>outfits list` - View all outfits
- [x] `>outfit save [name]` - Save the current sprite/scene/rainbow combo as outfit
- [x] `>outfit use [name]` - Use a saved outfit
- [x] `>outfit delete [name]` - Delete a saved outfit
- [x] `>outfit view [name]` - View details of a saved outfit

### Events 
- [x] `>event (id)` / `>event view (id)` - View an event
- [x] `>event list` - Show all events
- [x] `>event gift [user] [amount] [eventId]` - Gift a user event drops of an event
- [ ] ~~`>event redeem [amount] [sprite]` - Redeem event league credit for a sprite's event drop~~ Removed

### League
- [x] `>league (month) (year)` - View the league overview for a month
- [x] `>league board (month) (year)` - View the league board for a month
- [x] `>league rank (month) (year)` - View the own league stats for a month

### Patreon
- [ ] `>card` / `>card view` - View the user card
- [ ] `>card customize (headercolor) (lighttext) (darktext) (bgopacity) (headeropacity) (imgurid) (templateName)` - Customize the user card
- [ ] `>patron gift (@user)` - Patronize a user or view/remove the patronized user
- [ ] `>patron emoji (emoji)` - Set the patronemoji or view/remove the patronized user

### Splits
- [x] `>boost inv` - Show an overview of the user splits
- [x] `>boost (factor) (cooldown) (duration) (now)` - Start a new dropboost
- [x] `>boost rate` - Show the current droprate and the users current boosts
- [x] `>boost upgrade` - Upgrade one of the user's boosts (may also be in cooldown)
- [x] `>boost cooldown` - View a member's current split cooldowns

### Awards
- [x] `>award` / `>awards inventory` - Show an overview of the user's given/received awards'
- [x] `>award gallery` - Show the gallery of received awards
- [x] `>award view [id]` - View an received award with image

### Misc
- [ ] `>help (command)` - Show the help
- [ ] `>about` - Show some infos/stats
- [x] `>calc [rank/bubbles] [amount]` - Calculate the time to reach a goal
- [x] `>leaderboard (bubbles / drops)` - View the server leaderboard of bubbles or drops
- [x] `>stat (day/week/month)` - View the bubble stat graph for month/week/day
- [x] `>inventory` - View the inventory of the user (no sprites/list) with stats of drops, bubbles, flags, current combo, patronemoji, splits

### Admin
(not necessary for first release)

## Public
The public module is a simple bot wrapper around the commands module, to deploy the bot.  
Anyone is able to add this bot to this server; the bot targets primary slash commands. 

## Lobbies
The Lobbies module is used to bring the lobbies feature to selected servers.  
A number of instances of the lobby module is deployed; each instance claims a dedicated discord bot from a database (distinct token/id).  
Patrons can claim the dedicated bots for their server.  
The Lobbies instance watches their claimed bot, joins servers, and refreshes lobbies each 20s if they are set up.
Furthermore, it provides the command functionality via text commands.  

Following graphic shows the schedule every instance is running on:  
![Lobbies Schedule](https://i.imgur.com/VkT9wO5.png)
