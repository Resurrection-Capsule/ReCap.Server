# Squad / Deck

A named slot holding three creature IDs that the player takes into a game session. Called
"Squad" in C++ source and the XML wire format; called "Deck" in C# source. Interchangeable.

> **Moved.** The deck/squad data model (field table, persistence, mapper, porting gaps) is now
> consolidated with the full cross-layer map (REST → runtime → C++ → client HUD → crash) in
> **[`../deck-system/DECK_SQUAD_SYSTEM.md`](../deck-system/DECK_SQUAD_SYSTEM.md)** — §1–§2 hold the
> data-model details previously kept here.
