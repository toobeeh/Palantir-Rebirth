# Palantir-Rebirth
[![part of Typo ecosystem](https://img.shields.io/badge/Typo%20ecosystem-PalantirRebirth-blue?style=flat&logo=data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAIAAAACACAYAAADDPmHLAAACV0lEQVR4nO3dPUrDYByA8UQ8g15AI+gsOOnmrufoIBT0DAUFB+/R3bFTobOCwQvoJSouNcObhHyZ9n2eHwiirW3Th79J2iaJJEmSJEmSJIC06iGu1+vgz9M0Df9CY6t8PkP2fMrYDADOAOAMAM4A4OrWGl3bj0Pp8+wEgDMAuP2uD//w7I6+DEf19fbc6eadAHAGAGcAcAYAZwBwnbcCTrIj+jL8Fx/55yA34wSAMwA4A4AzADgDgDMAOAOAMwC4zjuCzi+uN9+fZgeNrvuefw+69FfL10H/fgycAHAGAGcAcAYAZwBwnbcCioZeq2+quIVS5NbBHycAnAHARffRsOksr71Ml38Bi/mk9XVH5EfDFGYAcHVbAWWjw08NbyePEaRmDADOAOAMAM4A4Fq9FjCd5cG1zaeHrPeleXnzsvl+MZ802vooe4fSatn9ftUILp/iYxlCm51UTgA4A4Dr9eXgsv3wtJdfhx71fXICwBkAXGUAv+cLCH0pHk4AOAOAMwA4A4AzALhedwRpXBVneSu9X04AOAOAMwA4A4AzADgDgDMAOAOAMwA4A4AzADgDgDMAOAOAMwA4A4AzALio3xG0bUcu3UZOADgDgDMAOAOAMwC4qLcCRjxG0M5wAsAZAJwBwBkAnAHAGQCcAcAZAJwBwBkAnAHA+Y4gOCcAnAHAGQCcAcAZAFyrrYDH++NGl7+6ZZ0yZpc4AeAMAC66HUFDnLwyZk4AOAOAKz+QfMXx58dScdz7se5o8A7t0HJzAtAZAJwBwBkAnAFIkiRJkiRJUtySJPkBweNXgRaWkYQAAAAASUVORK5CYII=)](https://github.com/topics/skribbl-typo)  

### Still WIP
Palantir-Rebirth is/will be a refactor of the current Palantir Bot.  
It consists of multiple components that can be deployed individually for scalability and maintainability (blah blah).  
Palantir-Rebirth follows the new separation of business+persistence from application layer and uses Valmar as backend.

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

### Sprites
- [ ] `>sprite [id]` - View a sprite
- [ ] `>sprite buy [id]` - Buy a sprite
- [ ] `>sprite use [id] (slot)` - Use a sprite on a slot
- [ ] `>sprite combo (...ids)` - Use a combo of sprites
- [ ] `>sprite color [id] (color shift)` - Use a rainbow shift color on a sprite
- [ ] `>sprite inv` - View all bought sprites and total value

Needed grpc services:
- getSprite(id:int)
- useSpriteCombo(login:int, clearOther:bool, slots:Array<{slot:int, spriteId:int}>)
- setSpriteColor(login:int, spriteId:int, colorShift:int?)
- buySprite(login:int, spriteId:int)
- getSpriteInventory(login:int)

### Scenes 
- [ ] `>scene [id]` - View a scene
- [ ] `>scene buy [id]` - Buy a scene
- [ ] `>scene use [id]` - Use a scene
- [ ] `>scene inv` - View all bought scenes and the next scene price

Needed grpc services:
- getScene(id:int)
- buyScene(login:int, sceneId:int)
- useScene(login:int, sceneId:int)
- getSceneInventory(login:int)

### Outfits
- [ ] `>outfits` - View all outfits
- [ ] `>outfits save [name]` - Save the current sprite/scene/rainbow combo as outfit
- [ ] `>outfits use [name]` - Use a saved outfit
- [ ] `>outfits delete [name]` - Delete a saved outfit

Needed grpc services:
- getOutfits(login:int)
- saveOutfit(login:int, name:string, spriteSlots:Array<{slot:int, spriteId:int}>, sceneId:int, colorShift:int?)
- useOutfit(login:int, name:string)
- deleteOutfit(login:int, name:string)

### Events 
- [ ] `>event (id)` - View an event
- [ ] `>event list` - Show all events
- [ ] `>gift [user] [amount] [sprite]` - Gift a user event drops used for a event sprite
- [ ] `>redeem [amount] [sprite]` - Redeem event league credit for a sprite's event drop

Needed grpc services:
- getEvent(id:int) - event details
- getEvents() - list of events
- giftEventDrop(login:int, target:int, amount:int, spriteId:int) - return loss
- redeemEventDrop(login:int, amount:int, spriteId:int) - return redeemed amount
- getLossRate(login:int, eventId:int, eventDrop:int) - return lossrate
- getEventCredits(login:int, eventId:int) - return list of event credits

### League
- [ ] `>league (month) (year)` - View the league overview for a month
- [ ] `>league board (month) (year)` - View the league board for a month
- [ ] `>league rank (month) (year)` - View the own league stats for a month

### Patreon
- [ ] `>card` - View the user card
- [ ] `>card customize [headercolor] [lighttext] [darktext] [bgopacity] [headeropacity] (imgurid)` - Customize the user card
- [ ] `>patronize (user-id)` - Patronize a user or view/remove the patronized user
- [ ] `>patronemoji (emoji)` - Set the patronemoji or view/remove the patronized user

### Splits
- [ ] `>splits` - Show an overview of the user splits
- [ ] `>splits boost (boost) (cooldown) (duration) (now)` - Start a new dropboost
- [ ] `>droprate` - Show the current droprate

### Awards
- [ ] `>awards` - Show an overview of the user's given/received awards'
- [ ] `>awards gallery` - Show the gallery of received awards
- [ ] `>awards view [id]` - View an received award with image

### Leaderboard
- [ ] `>leaderboard (mode)` - View the server leaderboard of bubbles or drops

### Inventory
- [ ] `>inventory` - View the inventory of the user (no sprites/list) with stats of drops, bubbles, flags, current combo, patronemoji, splits
- [ ] `>stat (mode)` - View the bubble stat graph for month/week/day

### Admin
- [ ] `>add sprite ` - Admin commands

### Misc
- [ ] `>help (command)` - Show the help
- [ ] `>about` - Show some infos/stats
- [ ] `>calc [mode] [amount]` - Calculate the time to reach a goal
- 

## Lobbies
Most likely there will be a separate Discord bot for lobbies to prevent ratelimit issues.  
This might be deployed on-premise and per server, or at least in some way.
