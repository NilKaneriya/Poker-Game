# Multiplayer Texas Hold'em

A multiplayer Texas Hold'em prototype played with virtual chips only (no real money,
no purchases). Players join a table from different devices; all game logic runs on
the server.

| Part | Tech |
|---|---|
| Client | Unity 2022.2 (WebGL / Windows / Android), TextMeshPro, DOTween |
| Networking | Socket.IO v4 (Node server, Firesplash Socket.IO client in Unity) |
| Server | Node.js 18+, Express, Socket.IO |

> Status: work in progress. The rules engine is done and tested; live hands over the
> network, bots and the remaining features are next. Sections marked _TBD_ are filled
> in as features land.

## Repository layout

```
poker-server/              Node game server (server-authoritative)
  src/engine/              Pure poker rules: cards, hand evaluation, pots, hand/table state
  src/network/             Socket.IO handlers, per-player state views, turn timer
  src/services/            Tables, accounts + login tokens, player stats
  src/bots/                Computer opponents
  test/                    Unit tests (node:test)
poker-unity/PokerGame/     Unity client
  Assets/Scene/            Splash -> Lobby -> Game
  Assets/Scripts/
    Core/                  Scene names, server config, shared helpers
    Network/               Login (HTTP), real-time connection (Socket.IO), message models
    UI/                    One folder per scene + Common (toasts, animations, status)
    Editor/                Tools > Poker > Build Scenes (generates the scenes and UI art)
```

## How to run

### Server
```
cd poker-server
npm install
cp .env.example .env
npm start
```
The server listens on `http://localhost:3000` (`GET /health` to check it).

### Client
1. Open `poker-unity/PokerGame` with Unity 2022.2.2f1.
2. First time only: **Tools > Poker > Build Scenes**. This imports TextMeshPro's
   essentials if needed, generates the UI images, creates the Splash, Lobby and Game
   scenes and adds them to Build Settings.
3. Set the server address in `Assets/Resources/ServerConfig` (default
   `http://localhost:3000`).
4. Open `Assets/Scene/Splash` and press Play.

Scene flow:
- **Splash** checks the server, then shows the login form. With *Remember me* ticked,
  the next launch signs in automatically and goes straight to the Lobby.
- **Lobby** lets you create a table, join one with its 5-letter code, or pick one from
  the open tables list.
- **Game** is the table: seats, cards, pot, turn timer, action buttons and the
  win / lose / out-of-chips screens. Playing this scene without a server shows sample
  data, so the layout can be checked in the Editor.

## Testing multiplayer with two or more players

_TBD: full walkthrough._ Short version for now: run the server, then start two or
more clients (Editor + a build, or several WebGL browser tabs) and sign in with a
**different account in each**. One player creates a table and shares the 5-letter
code; the others join with the code or from the table list.

Untick *Remember me* when testing several clients on one machine: browser tabs (and
builds of the same app) share saved settings, so a remembered login would sign every
client in as the same player.

Automated checks:
```
cd poker-server
npm test               # unit tests
npm run test:client    # smoke test against a running server (login, join, reconnect)
```

## Design decisions

- **Server-authoritative.** Shuffling (crypto RNG), dealing, action validation and
  showdown happen only on the server. Clients send intentions (`fold`, `raise 120`)
  and receive state; they never send results.
- **Engine separated from networking.** `src/engine` has no sockets, timers or I/O,
  so poker rules can be unit-tested directly.
- **Per-player views.** Every state message is built per player in
  `network/playerView.js`, the single place where hidden information is filtered.
- **Accounts and login tokens.** Players register with a username and password
  (stored as scrypt hashes). Login returns a random token; the server keeps only its
  SHA-256, so a leaked accounts file can't be used to sign in. The token is required in
  the Socket.IO handshake, so a player's identity comes from the server, never from
  data the client sends. The same token lets a dropped player reconnect to their seat.
- **Remember me.** Only the token is saved on the device (never the password). It
  lasts 30 days with *Remember me*, 12 hours without, and logging out revokes it.
- **Client renders, server decides.** Unity scenes only draw the latest snapshot from
  the server and send the player's choice; illegal buttons are disabled for clarity,
  but every action is validated again on the server.
- _TBD: layout (mobile/desktop), bots, stats feature, session-length reminder._

## Poker rules implemented (server engine)

All in `poker-server/src/engine`, covered by unit tests (`npm test`, 51 tests).

- **Dealing and blinds.** Hole cards are dealt one at a time starting left of the button.
  Small and big blind sit left of the button; heads-up, the button posts the small blind,
  acts first pre-flop and last after the flop. A burn card precedes flop, turn and river.
- **Dealer button** moves one seat clockwise each hand, skipping empty seats and players
  with no chips (simplified: no "dead button" rule).
- **Betting.** Minimum bet is the big blind; a raise must be at least the size of the
  previous raise. Raise amounts are "raise to" totals. A player who can't cover a call
  is put all-in automatically. An all-in smaller than a full raise does not reopen
  betting for players who already acted. No raising when nobody is left to respond.
- **Uncalled bets** (the part of a bet nobody matched) go back to their owner.
- **All-in run-out.** When fewer than two players can still bet, the rest of the board
  is dealt and the hand goes to showdown.
- **Hand ranking** from Royal Flush down to High Card, best 5 of 7 cards, with kickers;
  A-2-3-4-5 counts as the lowest straight.
- **Side pots** at every all-in level; folded players' chips stay in the pot but they
  can't win it.
- **Split pots** are divided equally; chips that can't be split go to the winner
  closest to the left of the button.
- **Timeouts** check when checking is free, otherwise fold. Players leaving mid-hand are
  folded immediately and their bets stay in the pot.
- A 300-hand random simulation checks that chips are never created or lost.

## Known issues

_TBD._

## Not real money

Chips are virtual and have no value. There are no purchases, loot boxes or wagering
of real money anywhere in the game.
