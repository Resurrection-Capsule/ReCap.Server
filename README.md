![alt ReCap logo and title](https://raw.githubusercontent.com/Resurrection-Capsule/recap/main/logo.png)

[WIP] ReCap for short. A small local server to play Darkspore offline

Join our [Discord](https://discord.gg/btfTw62) to keep in touch with the latest updates and/or to help with the project.

Just want to test it in your computer? Check [HOW_I_RUN_IT.md](HOW_I_RUN_IT.md).

## Overall progress
- [ ] Making the game playable offline;
- [ ] Making the game playable on LAN;
- [ ] Creating a server-client mechanic so servers can be hosted;
- [ ] Fix the original game issues, including connectivity issues.

## _Making the game playable offline_ progress
- [ ] Redirect Darkspore requests to the localhost;
- [X] Make Darkspore believe that the server is online (Error code 102);
- [X] Make Darkspore open after the Play button has been pressed (Error 3001).
- [ ] Make the login screen appear properly (Network connection was lost / Error 73000).
- [ ] Make it possible to access the hangar.
- [ ] Make it possible to access the hero editor.
- [ ] Make it possible to unlock any of the creatures in the hangar.
- [ ] Make it possible to unlock any of the parts for creatures.
- [ ] Make hero profiles work
- [ ] Make the chat work
- [ ] ?

## Introduction
The focus is creating a local server in order to make Darkspore work again. The game has been dead since 2016 (you can literally buy a new physical copy by 2.99£ in Amazon.com). Since the servers shutdown, the game discs became useless pieces of plastic. This project aims to create a localhost server, which is going to make Darkspore work like if it was the original server, but much faster and private.

This project is inteded to be used by people who an original copy of the game. If you bought the game in Steam, you will use Steam to play it. If you bought the game in Origin, you will use Origin to play it. And if you bought the game disc, you will use your disc to play it.

## FAQ

### With that, will Darkspore work exactly like it did before?
No. There are two factors that most likely will change; a positive one and a negative one. The positive one is that there will be no server instability (for obvious reason); still, if someone uses that to create an open private server, it may experience similar instabilities, unless we fix them.

The negative one is that, at this moment, there is no sign of packet logs from the original server. That means that, most likely, the game procedures won't be exactly like the original Darkspore ones. That includes damage calculation, drop chance, spawn frequency, experience calculation, among other things.

### How long it will take to be finished?
I have no idea.

### Which programs do I need to modify the project?
- .NET 8.0

## Actual state
???

## Credits
???

## Special Thanks
???

## How can I help?
We can't accept any form of income in the project, but if want to help Darkspore to be released one day without an internet requirement, endorse the Darkspore wish in GOG's wishlist. That's the best we can do:
- https://www.gog.com/wishlist/games/darkspore