![ReCap logo and title](/info/branding/logo.png)

[WIP] ReCap for short. A small local server to play Darkspore offline

Join our [Discord server](https://discord.gg/btfTw62) to keep in touch with the latest updates and/or to help with the project.

Just want to test it in your computer? See [setup instructions](/info/HOWTOSETUP.md).

## Introduction
The focus is creating a local server in order to make Darkspore work again. The game has been dead since 2016 (you can literally buy a new physical copy by 2.99£ in Amazon.com). Since the servers shutdown, the game discs became useless pieces of plastic. This project aims to create a localhost server, which is going to make Darkspore work like if it was the original server, but much faster and private.

This project is intended to be used by people who own an original copy of the game.
- If you bought the game on Steam, you will use Steam to play.
- If you bought the game in Origin, you will use Origin to play.
- If you bought the game on disc, you will use your disc to play.

## Overall progress
- [ ] Make the game playable offline
	- [ ] Redirect Darkspore requests to localhost
	- [X] Make Darkspore believe that the server is online (Error code 102)
	- [X] Make Darkspore open after the Play button has been pressed (Error 3001)
	- [ ] Make the login screen appear properly (Network connection was lost / Error 73000)
	- [ ] Make it possible to access the arsenal
	- [ ] Make it possible to access the hero editor
	- [ ] Make it possible to unlock any of the Heroes in the arsenal
	- [ ] Make it possible to unlock any of the parts for Heroes
	- [ ] Make hero profiles work
	- [ ] Make the chat work
	- [ ] ?????
- [ ] Make the game playable on LAN
- [ ] Creating a server-client mechanic so servers can be hosted
- [ ] Fix the original game issues, including connectivity issues

## FAQ

### With that, will Darkspore work exactly like it did before?
No. There are two factors that most likely will change; a positive one and a negative one. The positive one is that there will be no server instability (for obvious reason); still, if someone uses that to create an open private server, it may experience similar instabilities, unless we fix them.

The negative one is that, at this moment, there is no sign of packet logs from the original server. That means that, most likely, the game procedures won't be exactly like the original Darkspore ones. That includes damage calculation, drop chance, spawn frequency, experience calculation, among other things.

### How long it will take to be finished?
I have no idea.

### Which programs do I need to modify the project?
- .NET 8.0

## Credits
???

## Special Thanks
???

## How can I help?
We can't accept any form of income in the project, but if you want to help Darkspore to be released one day without an internet requirement, endorse the Darkspore wish in GOG's wishlist. That's the best we can do:
- https://www.gog.com/wishlist/games/darkspore