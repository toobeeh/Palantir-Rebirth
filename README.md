# Palantir-Rebirth
[![part of Typo ecosystem](https://img.shields.io/badge/Typo%20ecosystem-PalantirRebirth-blue?style=flat&logo=data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAIAAAACACAYAAADDPmHLAAACV0lEQVR4nO3dPUrDYByA8UQ8g15AI+gsOOnmrufoIBT0DAUFB+/R3bFTobOCwQvoJSouNcObhHyZ9n2eHwiirW3Th79J2iaJJEmSJEmSJIC06iGu1+vgz9M0Df9CY6t8PkP2fMrYDADOAOAMAM4A4OrWGl3bj0Pp8+wEgDMAuP2uD//w7I6+DEf19fbc6eadAHAGAGcAcAYAZwBwnbcCTrIj+jL8Fx/55yA34wSAMwA4A4AzADgDgDMAOAOAMwC4zjuCzi+uN9+fZgeNrvuefw+69FfL10H/fgycAHAGAGcAcAYAZwBwnbcCioZeq2+quIVS5NbBHycAnAHARffRsOksr71Ml38Bi/mk9XVH5EfDFGYAcHVbAWWjw08NbyePEaRmDADOAOAMAM4A4Fq9FjCd5cG1zaeHrPeleXnzsvl+MZ802vooe4fSatn9ftUILp/iYxlCm51UTgA4A4Dr9eXgsv3wtJdfhx71fXICwBkAXGUAv+cLCH0pHk4AOAOAMwA4A4AzALhedwRpXBVneSu9X04AOAOAMwA4A4AzADgDgDMAOAOAMwA4A4AzADgDgDMAOAOAMwA4A4AzALio3xG0bUcu3UZOADgDgDMAOAOAMwC4qLcCRjxG0M5wAsAZAJwBwBkAnAHAGQCcAcAZAJwBwBkAnAHA+Y4gOCcAnAHAGQCcAcAZAFyrrYDH++NGl7+6ZZ0yZpc4AeAMAC66HUFDnLwyZk4AOAOAKz+QfMXx58dScdz7se5o8A7t0HJzAtAZAJwBwBkAnAFIkiRJkiRJUtySJPkBweNXgRaWkYQAAAAASUVORK5CYII=)](https://github.com/topics/skribbl-typo)  

### Very WIP 
Palantir-Rebirth will be a refactor of the current Palantir Bot.  
It will consist of multiple components that can be deployed individually for scalability and maintainability (blah blah).  
Palantir-Rebirth follows the new separation of business+persistence from application layer and uses Valmar as backend.

The components will be most likely one split to Core, Commands and Lobbies.

## Core
Core will contain features that are vital for the complete typo ecosystem, like dispatching drops, cleaning database, fetching patrons, and everything that happens on a scheduled basis.

Tasks to be integrated:
- [ ] Update patrons & boosters and write to user flags (grpc: setFlag(flag: number, exclusive: bool, ids: long[]))
- [ ] Dispatch drops (get online count, get active event, send drop request and calculate next timeout)
- [ ] Clear volatile data (split into multiple grpc calls)

## Commands
Commands will use a DSharpPlus Bot to bring the functionality of the current Palantir Bot to Discord, using the gRPC backend.

## Lobbies
Most likely there will be a separate Discord bot for lobbies to prevent ratelimit issues.  
This might be deployed on-premise and per server, or at least in some way.
