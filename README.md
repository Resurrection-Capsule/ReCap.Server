# ![Resurrection Capsule logo and title](/branding/logo.png)

Resurrection Capsule (or "ReCap" for short) is an ongoing, work-in-progress effort to make Darkspore playable again.

At this time, ReCap's primary focus is to re-implement the server software for Darkspore, in order to make the game playable again. We don't want Darkspore to meet the same fate a second time around though, so we're primarily aiming to let you run this as a *local* server - no internet connection required, since everything lives *on your computer*...as all games should!

If you're interested, feel free to join our [Discord server](https://discord.gg/btfTw62) to keep up with the latest development progress (or if you're interested in helping out).

## Darkspore?

[Darkspore](https://wikipedia.org/wiki/Darkspore) is a space-themed, Diablo-like action-RPG, released by Maxis on April 26th, 2011, which notably offers a modified version of Spore's creature creator, with which to equip and customize a whole team of playable creatures known as "Living Weapons" or "Genetic Heroes".

Sadly, the entire game - singleplayer included - was rendered completely unplayableon March 1st, 2016, when its official servers were shut down.

...and that's where we come in.

## Where do I download?

Right here:

<a href="/docs/user/getting-started.md">
	<!--
	<object type="image/svg+xml" data="/docs/assets/download-button.svg"></object>
	-->
	<img alt="DOWNLOAD" src="/docs/assets/download-button.svg">
</a>

## Overall server development progress
- [ ] Make the game playable offline
	- [ ] Redirect Darkspore requests to localhost
	- [X] Make Darkspore believe that the server is online (Error code 102)
	- [X] Make Darkspore open after the Play button has been pressed (Error 3001)
	- [X] Make the login screen appear properly (Network connection was lost / Error 73000)
	- [ ] Make the arsenal completely functional
	- [ ] Make the hero editor completely functional
	- [ ] Make it possible to unlock any of the Heroes in the arsenal
	- [ ] Make it possible to unlock any of the parts for Heroes
	- [ ] Make hero profiles work
	- [ ] Make the chat work
	- [ ] ?????
- [ ] Make the game playable on LAN
- [ ] Creating a server-client mechanic so servers can be hosted
- [ ] Address whatever issues stem from the game client itself (e.g. connectivity problems)

---

## FAQ

### Will Darkspore behave any differently when played via Resurrection Capsule?

No. There are two factors that most likely will change; a positive one and a negative one. The positive one is that there will be no server instability (for obvious reason); still, if someone uses that to create an open private server, it may experience similar instabilities, unless we fix them.

The negative one is that, at this moment, there is no sign of packet logs from the original server. That means that, most likely, the game procedures won't be exactly like the original Darkspore ones. That includes damage calculation, drop chance, spawn frequency, experience calculation, among other things.

### How long will this project take?

I have no idea.

## I got Darkspore from...

This project is intended to be used by people who own an original copy of the game.
- If you bought the game on Steam, you will use Steam to play.
- If you bought the game in Origin, you will use Origin to play.
- If you bought the game on disc, you will use your disc to play.


## I'm *not* a programmer, can I still help?

We can't accept any form of financial support.
However, if you'd like Darkspore to be re-released without its online-only requirements someday, then please [vote for Darkspore on the GOG wishlist](https://www.gog.com/wishlist/games/darkspore).

## How can I contribute to the development of Resurrection Capsule?

See [Compiling from source](/docs/contributor/compiling-from-source.md).

---

## Credits

> @vitor251093
- Project leader
- Lead ReCap Server developer
- Responsible for...
	- Initial server prototyping
	- C# server migration

> @dalkon
- Major ReCap server contributor (pre-C# migration)
- Significant research contributor - singlehandedly figured out...
	- Login screen
	- Arsenal
	- Chat
	- Hero editor
	- Campaign/missions (incomplete)

> @Splitwirez
- Designer
- Lead ReCap Hub developer
- Source of knowledge regarding Darkspore's intended behaviour
- Source of knowledge regarding Spore-like aspects of Darkspore client internals
- Discord server Moderator

> @knoellle
- Source of knowledge regarding Darkspore's intended behaviour
- Discord server Moderator


## Special Thanks

- @emd4600
- haradons91 (on Discord)
- @nonchip
- "Otomachi Una" (on Discord)
- @plencka