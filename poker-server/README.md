# poker-server

Server-authoritative Texas Hold'em server (Node + Socket.IO v4).
See the root README for the overall project.

## Scripts

| Command | What it does |
|---|---|
| `npm start` | Run the server |
| `npm run dev` | Run with auto-restart on file changes |
| `npm test` | Unit tests (`test/*.test.js`, node:test) |
| `npm run test:client` | Smoke test against a running server |

Settings come from `.env` (see `.env.example`): port, CORS, blinds, turn timeout,
reconnect grace period, where accounts are saved (`data/accounts.json`, git-ignored).

## Layout

```
src/
  index.js               HTTP + Socket.IO bootstrap
  config.js              Settings from .env
  engine/                Pure game rules, no I/O
    constants.js         Stages, actions, hand ranks
    cards.js             Card format, deck, crypto shuffle
    handEvaluator.js     Best 5 of 7, hand comparison
    pots.js              Main/side pots and payouts
    HoldemHand.js        One hand: blinds -> streets -> showdown
    Table.js             Seats, stacks, dealer button
  network/
    authRoutes.js        Register / login / auto-login / logout (HTTP)
    socketServer.js      Token check on connect, reconnect to seat
    events.js            Event names (mirrored in Unity PokerEvents.cs)
    playerView.js        What each player is allowed to see
    broadcast.js         Sending state to tables/players
    TurnTimer.js         Per-turn countdown
    handlers/            Lobby and in-game event handlers
  services/
    TableManager.js      Tables by join code
    AccountStore.js      Accounts (scrypt passwords) and login tokens
    StatsService.js      Player statistics
  bots/BotPlayer.js      Computer opponents
```

## Login (HTTP)

| Method | Path | Body / header | Response |
|---|---|---|---|
| POST | `/auth/register` | `{ username, password, remember }` | `{ token, playerId, username }` |
| POST | `/auth/login` | `{ username, password, remember }` | `{ token, playerId, username }` |
| GET | `/auth/me` | `Authorization: Bearer <token>` | `{ playerId, username }` (auto-login check) |
| POST | `/auth/logout` | `Authorization: Bearer <token>` | `{ ok: true }` (token revoked) |

Errors come back as `{ error }` with status 400/401/429. Login and register are
limited to 10 attempts per minute per IP.

## Socket protocol

Connect with `auth: { token }` in the Socket.IO handshake; connections without a
valid token are refused with `unauthorized`.

| Direction | Event | Payload |
|---|---|---|
| C → S | `createTable` | `{ tableName }` |
| C → S | `joinTable` | `{ code }` |
| C → S | `leaveTable` | – |
| C → S | `listTables` | – |
| C → S | `playerAction` | `{ action: fold\|check\|call\|raise\|allIn, amount? }` (raise amount = total to raise to) |
| C → S | `rebuy` | – (out of chips: restart with the starting stack) |
| S → C | `session` | `{ playerId, username }` |
| S → C | `tableList` | `[{ code, name, players, maxSeats, blinds, inHand }]` |
| S → C | `tableState` | table snapshot built for the receiving player |
| S → C | `actionRejected` | `{ reason }` |
| S → C | `errorMessage` | `{ message }` |

Player names come from the account, not from client input.

Cards are 2-character strings: rank `2-9 T J Q K A` + suit `h d c s` (e.g. `Ah`, `Td`).
