# Palantir-Rebirth
[![part of Typo ecosystem](https://img.shields.io/badge/Typo%20ecosystem-PalantirRebirth-blue?style=flat&logo=data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAIAAAACACAYAAADDPmHLAAACV0lEQVR4nO3dPUrDYByA8UQ8g15AI+gsOOnmrufoIBT0DAUFB+/R3bFTobOCwQvoJSouNcObhHyZ9n2eHwiirW3Th79J2iaJJEmSJEmSJIC06iGu1+vgz9M0Df9CY6t8PkP2fMrYDADOAOAMAM4A4OrWGl3bj0Pp8+wEgDMAuP2uD//w7I6+DEf19fbc6eadAHAGAGcAcAYAZwBwnbcCTrIj+jL8Fx/55yA34wSAMwA4A4AzADgDgDMAOAOAMwC4zjuCzi+uN9+fZgeNrvuefw+69FfL10H/fgycAHAGAGcAcAYAZwBwnbcCioZeq2+quIVS5NbBHycAnAHARffRsOksr71Ml38Bi/mk9XVH5EfDFGYAcHVbAWWjw08NbyePEaRmDADOAOAMAM4A4Fq9FjCd5cG1zaeHrPeleXnzsvl+MZ802vooe4fSatn9ftUILp/iYxlCm51UTgA4A4Dr9eXgsv3wtJdfhx71fXICwBkAXGUAv+cLCH0pHk4AOAOAMwA4A4AzALhedwRpXBVneSu9X04AOAOAMwA4A4AzADgDgDMAOAOAMwA4A4AzADgDgDMAOAOAMwA4A4AzALio3xG0bUcu3UZOADgDgDMAOAOAMwC4qLcCRjxG0M5wAsAZAJwBwBkAnAHAGQCcAcAZAJwBwBkAnAHA+Y4gOCcAnAHAGQCcAcAZAFyrrYDH++NGl7+6ZZ0yZpc4AeAMAC66HUFDnLwyZk4AOAOAKz+QfMXx58dScdz7se5o8A7t0HJzAtAZAJwBwBkAnAFIkiRJkiRJUtySJPkBweNXgRaWkYQAAAAASUVORK5CYII=)](https://github.com/topics/skribbl-typo)  

### Still WIP
Palantir-Rebirth is/will be a refactor of the current Palantir Bot.  
It consists of multiple components that can be deployed individually for scalability and maintainability (blah blah).  
Palantir-Rebirth follows the new separation of business+data from application layer and uses toobeeh/Valmar as grpc backend.

The components are split to Core, Commands and Lobbies modules.

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
Commands will use a DSharpPlus Bot to bring the functionality of the current Palantir Bot to Discord, using the gRPC backend.
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
- [ ] `>event redeem [amount] [sprite]` - Redeem event league credit for a sprite's event drop

Needed grpc services:
- getEvent(id:int) - event details
- getEvents() - list of events
- giftEventDrop(login:int, target:int, amount:int, spriteId:int) - return loss
- redeemEventDrop(login:int, amount:int, spriteId:int) - return redeemed amount
- getLossRate(login:int, eventId:int, eventDrop:int) - return lossrate
- getEventCredits(login:int, eventId:int) - return list of event credits

### League
- [x] `>league (month) (year)` - View the league overview for a month
- [x] `>league board (month) (year)` - View the league board for a month
- [x] `>league rank (month) (year)` - View the own league stats for a month

### Patreon
- [ ] `>card` - View the user card
- [ ] `>card customize [headercolor] [lighttext] [darktext] [bgopacity] [headeropacity] (imgurid)` - Customize the user card
- [ ] `>patronize (user-id)` - Patronize a user or view/remove the patronized user
- [ ] `>patronemoji (emoji)` - Set the patronemoji or view/remove the patronized user

### Splits
- [x] `>boost inv` - Show an overview of the user splits
- [x] `>boost (factor) (cooldown) (duration) (now)` - Start a new dropboost
- [x] `>boost rate` - Show the current droprate and the users current boosts
- [x] `>boost upgrade` - Upgrade one of the user's boosts (may also be in cooldown)
- [x] `>boost cooldown` - View a member's current split cooldowns

### Awards
- [x] `>awards` / `>awards inventory` - Show an overview of the user's given/received awards'
- [x] `>awards gallery` - Show the gallery of received awards
- [x] `>awards view [id]` - View an received award with image

### Admin
(not necessary for first release)

### Misc
- [ ] `>help (command)` - Show the help
- [ ] `>about` - Show some infos/stats
- [ ] `>calc [mode] [amount]` - Calculate the time to reach a goal
- [x] `>leaderboard (bubbles / drops)` - View the server leaderboard of bubbles or drops
- [x] `>stat (day/week/month)` - View the bubble stat graph for month/week/day
- [x] `>inventory` - View the inventory of the user (no sprites/list) with stats of drops, bubbles, flags, current combo, patronemoji, splits

## Lobbies
Most likely there will be a separate Discord bot for lobbies to prevent ratelimit issues.  
This might be deployed on-premise and per server, or at least in some way.
