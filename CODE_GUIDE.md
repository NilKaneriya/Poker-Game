# Code Guide: Multiplayer Texas Hold'em

How the project is built, what every part does, and where it stands today.

---

## 1. The project in 30 seconds

A multiplayer Texas Hold'em game played with **virtual chips only** (no real money, no purchases).

- **Server**: Node.js + Express + Socket.IO. It holds **all the game rules**. Clients never decide anything.
- **Client**: Unity 2022.2 (WebGL, Windows, Android). It **only displays** what the server sends and sends the player's *intentions* ("I want to raise to 80") back.
- Players can play **against bots** or **with friends** at tables of **2 to 6 seats**. Friends join with a 5-letter room code or an invite link, wait in a waiting room, and the game starts automatically when the table is full.

**One sentence to remember:** *the server is the single source of truth; the client is a view of the server's state.*

---

## 2. Architecture

```
 ┌─────────────────────── Unity client ────────────────────────┐        ┌──────────────── Node server ─────────────────┐
 │                                                             │  HTTP  │                                               │
 │  SplashController ──► AuthManager ──► ApiClient ────────────┼───────►│  authRoutes ──► AccountStore (accounts.json) │
 │                                                             │        │                                               │
 │  LobbyController ─┐                                         │ Socket │  socketServer (checks login token)            │
 │  GameController ──┼──► NetworkManager (Socket.IO) ◄─────────┼──.IO──►│    ├─ lobbyHandlers  create/join/leave/start  │
 │                   │        ▲ events: TableStateReceived...  │        │    └─ gameHandlers   actions, rebuy           │
 │  Views/Presenters ◄┘       │                                │        │  TableManager ──► Table ──► HoldemHand        │
 │  (seats, board, panels)    │                                │        │  TableRunner (timers, bots, when to deal)     │
 │                            │                                │        │  playerView (what each player may see)        │
 └────────────────────────────┴────────────────────────────────┘        └───────────────────────────────────────────────┘
```

**Two channels**
- **HTTP** for login/register/logout. Returns a login token.
- **Socket.IO** for everything real-time. The token is checked when the socket connects.

**One main message:** after any change, the server sends each player a `tableState`: a full snapshot of the table *as that player is allowed to see it*. The client redraws from it. Snapshots (not small "diff" events) make the client simple and make reconnecting trivial: you just receive the latest snapshot again.

---

## 3. What happens during a game (end to end)

1. **Splash**: checks `GET /health`, then logs in (or auto-logs in with a saved "Remember me" token), then opens the socket.
2. **Server accepts the socket** only with a valid token → sends `session`. If the player was already seated, it puts them back at their table (reconnect).
3. **Lobby**: the player picks **Play vs bots / Play with friends** and **2-6 players**, then `createTable`. Or joins with a code / invite link / the open-tables list.
4. **Server seats the player** and sends `tableState` → the client switches to the Game scene.
5. **Friends table**: `started = false` → the **waiting room** shows the room code and share buttons. Every join sends a new snapshot to everyone. When the **last seat is taken**, the server sets `started = true` and deals → everyone's waiting room closes at the same moment. The host may press **Start now** with 2+ players.
   **Bots table**: empty seats are filled with bots → starts at once. A friend who joins by code mid-hand goes on the table's **waiting list** (`you.waiting = true`, no seat, no cards) and watches. When the hand ends, `TableRunner.seatWaitingPlayers()` stands up the bot with the fewest chips and seats the friend before the next deal.
6. **A hand**: server posts blinds, deals, and runs a **30-second turn timer**. On your turn the snapshot contains your **legal actions**, so the action bar can only offer legal moves. You click → `playerAction` → the server validates it in the rules engine → new snapshots for everyone.
7. **Bots** act through the **same rule checks** as humans and only see what a human in their seat would see.
8. **End of hand**: showdown or everyone else folds → result (winners, hand names, best cards) → win/lose screen → the next hand starts after a short pause.

---

## 4. Design principles used

| Principle | How it shows in the code |
|---|---|
| **Server-authoritative** | All rules live in `poker-server/src/engine`. The client never computes who wins. The client's own `HandEvaluator` is **display only** ("you have One Pair"). |
| **Hidden information can't leak** | `playerView.js` is the *only* place that turns state into a client payload. Other players' hole cards are added only at showdown. Bots get the same view as humans. |
| **Single responsibility** | Each class has one job (see sections 5 and 6). `GameController` only coordinates; seats, board, panels, presenters and animators each own one part of the screen. |
| **Static data in one place** | All constants, texts, colours, timings, layouts and the hand chart live in `Constants.cs`. All data shapes live in `DataModel.cs`. No magic values spread across classes. |
| **Names match on both sides** | Event names (`createTable`, `tableState`...), action ids, stages, table modes and hand names are identical on server and client, and each file points to its counterpart. |
| **Pure engine, thin network layer** | `Table` and `HoldemHand` have no sockets or timers, so they are unit-tested directly. `TableRunner` decides *when* things happen; the engine decides *what* is allowed. |

---

## 5. Server (`poker-server/`)

### `src/engine/`: the poker rules (pure logic, unit-tested)
| File | Responsibility |
|---|---|
| `constants.js` | Stages, action ids, **table modes** (`bots` / `friends`), hand ranks and names. |
| `cards.js` | Cards as 2-char strings (`"Ah"`, `"Td"`), deck creation, Fisher-Yates shuffle with `crypto.randomInt` (unpredictable). |
| `handEvaluator.js` | Best 5-card hand from 5-7 cards: tries all 21 combinations, returns rank, name, tie-break score and the 5 cards. |
| `pots.js` | Main pot and **side pots** for all-ins; splits pots between tied winners. |
| `HoldemHand.js` | One hand from blinds to showdown: legal actions, betting rules (min-raise, all-in), streets, timeouts, fold-wins, showdown. |
| `Table.js` | Seats, chip stacks, dealer button, starting hands, rebuy. Also holds lobby metadata: `mode`, `started`, `hostId` (host passes on if they leave). |

### `src/network/`: Socket.IO layer
| File | Responsibility |
|---|---|
| `socketServer.js` | Rejects sockets without a valid token; sends `session` (with the table you are at, or none after a restart) and your stats; puts reconnecting players back at their table; starts the grace period on disconnect. |
| `handlers/lobbyHandlers.js` | `createTable` (validates mode and seat count 2-6), `joinTable` (bots tables: joins the waiting list, seated after the current hand), `leaveTable`, `listTables`, `startGame` (host only). |
| `handlers/gameHandlers.js` | `playerAction` (validated by the engine), `rebuy`. |
| `TableRunner.js` | Runs a table over time: **waiting room** (starts when full or when the host starts), turn timer, bot moves, result pause, next hand. Fills bots tables with bots; between hands swaps a bot out for each waiting human. |
| `playerView.js` | Builds what one specific player may see. The single gate for hidden information. |
| `broadcast.js` | Sends every seated human their own view; sends the table list. |
| `TurnTimer.js` | One countdown per table; exposes time left so clients draw their own ring. |
| `seating.js` | Takes a player off a table (fold if in a hand, pass on host, close empty tables). Used by Leave and by the reconnect grace. |
| `reconnectGrace.js` | Keeps a disconnected player's seat for 2 minutes, then frees it. |
| `webglHosting.js` | Serves a Unity WebGL build from the server (one address for game and server). |
| `authRoutes.js` | Register, login, `/auth/me`, logout; simple rate limit against brute force. |
| `events.js` | Every event name, in one place. |

### `src/services/`
| File | Responsibility |
|---|---|
| `TableManager.js` | Creates tables with unique, easy-to-read 5-letter codes (no 0/O/1/I); finds a player's table; lists **friends** tables only. |
| `AccountStore.js` | Accounts in a JSON file. Passwords as **scrypt** hashes with salt; tokens stored only as **SHA-256**. |
| `StatsService.js` | **Player statistics** (the one retention feature): hands played, hands won, biggest pot, best hand shown. Recorded after every hand, saved to `data/stats.json`, sent only to that player. |

### `src/bots/BotPlayer.js`
Three personalities (easy / normal / hard). Each one compares **hand strength** with the **price of calling**, raises with strong hands and sometimes bluffs.

### `src/chat/`: table chat (a separate module)
Chat never imports game code (a unit test enforces this). `src/index.js` calls `attachChat(io, { tableOf, roomOf, gameEvents })`, which gives chat everything it needs.

| File | Responsibility |
|---|---|
| `index.js` | `attachChat`: registers chat sockets and turns game events into system messages. |
| `chatEvents.js` | Chat's own event names (`chatSend`, `getChat`, `chatMessage`, `chatHistory`, `chatError`). |
| `chatHandlers.js` | Send (table members only, cleaned, rate limited, name from login) and history. |
| `chatText.js` | Cleans text: strips control characters, collapses spaces, cuts to 140, masks swear words. |
| `RateLimiter.js` | At most 5 messages per 10 s per player. |
| `ChatStore.js` | Last 50 messages per table, in memory; cleared when the table closes. |
| `systemMessages.js` | Wording for "Bob joined the table", "Ava won 300 with Flush"... |

### `src/reactions/`: emote reactions (a stand-alone module)
Reactions import nothing outside their own folder (a unit test enforces this), so the folder can be copied into any Socket.IO server. `src/index.js` calls `attachReactions(io, { groupOf, roomOf, isInGroup })`; here a "group" is a table. A test runs the module on a bare Socket.IO server with no poker code at all.

| File | Responsibility |
|---|---|
| `index.js` | `attachReactions`: one socket handler; broadcasts to the group's room, errors only to the sender. Optional `whoIs(socket)` for other login systems. |
| `ReactionService.js` | The rules, with no networking: sender must be in a group, emote must be known, target must be in the same group, 4 per 6 s per player. |
| `emotes.js` | The 12 emote ids (`laugh`, `wow`, `cool`, ... `gg`). Clients draw them with their own art. |
| `reactionEvents.js` | Event names: `reactionSend`, `reaction`, `reactionError`. |
| `RateLimiter.js` | The module's own copy, so it has no dependencies. |

Reactions are not stored: they are a moment at the table, not history.

The game side only publishes neutral events through `network/gameEvents.js` (player joined / waiting / seated / left, bot replaced, hand finished, table closed). It does not know who listens.

### `src/storage/`: player data (MongoDB or JSON files)
| File | Responsibility |
|---|---|
| `index.js` | `createStorage({ mongoUri, mongoDb, dataDir })`: MongoDB when `MONGODB_URI` is set (creates the unique `usernameLower` index and a TTL index that deletes expired tokens), JSON files otherwise. Splits an old `accounts.json` once. |
| `MongoCollection.js` | `get / findOne / set / update / delete` on a MongoDB collection (`_id` = id). |
| `JsonCollection.js` | The same API on one JSON file (or in memory for tests). |

Services get collections through `attach(storage)` at start-up (`src/index.js`), so tests can hand them an in-memory store.

### Profile and settings (`src/services/`, `src/network/`)
| File | Responsibility |
|---|---|
| `profileRules.js` | Pure rules: display name, avatar (0-11), bio (80 chars), settings schema with types, ranges and defaults. |
| `ProfileService.js` | Read the profile, save edits, merge settings. Never returns the password hash or salt. |
| `profileRoutes.js` | `GET /profile/me`, `PUT /profile`, `PUT /profile/settings` (login token required). |
| `profileSync.js` | After an edit: update the player's sockets (chat name) and their seat at the table at once. |

### Tests and scripts
- `npm test`: **54 unit tests** for cards, evaluator, pots, hands, table and bots.
- `npm test`: also covers **player stats**.
- `npm run test:e2e`: starts its own server and runs the end-to-end test: auth, join, reconnect, waiting room, host-only start, auto start when full, joining a bots table mid-hand and taking a bot's seat after it, seat-count validation, **stats after a real hand, seat freed after the grace period, session reporting a closed table**.
- `npm run play:bots`: plays real hands against bots and checks no hidden cards leak.

---

## 6. Unity client (`poker-unity/PokerGame/Assets/Scripts/`)

### Folder map
```
Core/        Constants (all static data), ServerConfig, AppServices
Network/     DataModel (all data shapes), Api/ (HTTP)
Managers/    AuthManager, NetworkManager, AudioManager (long-lived services)
Audio/       SoundLibrary (which clip plays for each sound)
Platform/    InviteLinkService, ShareService (browser / Android specific)
Gameplay/    HandEvaluator (pure logic, no UI)
Utils/       CardFormat (text formatting)
UI/Common/   reusable widgets (toast, connection status, option buttons, tweens)
UI/Splash/   login scene
UI/Lobby/    lobby scene
UI/Game/     game scene: GameController + Table/ Panels/ Presenters/ Animation/ Preview/
Editor/      tool that builds the scenes, UI art and placeholder sounds from code
```

### Core
| Class | Responsibility |
|---|---|
| `Constants` | **Every value and static table**: scene names, API routes, event names, prefs keys, table modes, stages and their labels, hand ranks and names, the **hand chart and rules text**, seat layouts per table size, animation timings, colour palettes, all UI texts. |
| `ServerConfig` | ScriptableObject in `Resources/`: server URL, invite base URL, logging switch. Changeable per build without code. |
| `AppServices` | Creates the long-lived `AuthManager` + `NetworkManager` once (`DontDestroyOnLoad`), so any scene can be played directly. |

### Network and Managers
| Class | Responsibility |
|---|---|
| `DataModel` | **Every data shape**: HTTP and socket payloads (fields named exactly like the server's JSON), plus client-only models (`HandChartEntry`, `TableInvite`). |
| `ApiClient` / `ApiFailure` | Small JSON-over-HTTP helper for login endpoints with readable errors. |
| `AuthManager` | Login, register, logout; "Remember me" saves **only the token** (never the password). |
| `AudioManager` | Plays sound effects from a pool of 16 voices (optionally delayed to match animations) and music with a crossfade between scenes. Music and sound can be switched off separately; the choice is saved. |
| `SessionClock` | Responsible play: measures the session and raises a *Time for a break?* reminder after 1 hour (then hourly); can be turned off (saved). |
| `SceneNotice` | A message carried to the next scene (e.g. "your table was closed"). |
| `SoundLibrary` | ScriptableObject at `Resources/SoundLibrary`: one entry per `SoundId` with clips (a random one plays), volume and a little random pitch so repeated sounds don't feel robotic. |
| `NetworkManager` | The Socket.IO connection. Scenes subscribe to C# events (`TableStateReceived`...) and call methods (`CreateTable`, `SendAction`, `StartGame`...). They never touch Socket.IO directly. Handles token rejection. |

### Platform
| Class | Responsibility |
|---|---|
| `InviteLinkService` | Builds `...?room=CODE` links and reads them at startup (WebGL page URL) or from deep links. Keeps the code **pending** through login; the lobby then joins automatically. |
| `ShareService` | Copy to clipboard and open the share sheet. WebGL uses `Plugins/WebGL/PokerShare.jslib` (browser clipboard + Web Share API); Android uses the "Share via" chooser; elsewhere it copies. |

### Gameplay and Utils
| Class | Responsibility |
|---|---|
| `HandEvaluator` | "Which hand do these 2-7 cards make?" (High Card ... Royal Flush). **Display only** for the "your hand" hints. Tested on 24 cases. |
| `CardFormat` | `"Td"` → `10 ♦`; chip amounts with separators. |

### UI/Common
| Class | Responsibility |
|---|---|
| `UIAnim` | Shared DOTween helpers: pop in, punch, flip, pulse, fade, count up. |
| `ToastView` | Short message that fades in and out. |
| `ConnectionStatusView` | Online / reconnecting indicator and "connection lost" banner. |
| `OptionButtonGroup` | A row of buttons where exactly one is selected (mode picker, seat-count picker). |
| `BreakReminderPanel` | The *Time for a break?* overlay: *Keep playing* or *Don't remind me*. |
| `SettingToggleButton` | On/off button for music, sound or break reminders. |
| `ButtonClickSound` | Click sound on a button (the scene builder adds it to every button). |
| `SceneMusic` | Starts the scene's music track. |
| `ButtonFeedback` | Every button grows a little on hover, squeezes when pressed and springs back (skipped while disabled). |
| `SwitchToggleView` | Draws a Toggle as a sliding pill switch (knob slides, track turns gold); follows the value however it is set. |
| `InputFocusHighlight` | Gold ring around a text field while typing. |

### UI/Splash and UI/Lobby
| Class | Responsibility |
|---|---|
| `SplashController` | Server check → auto-login or login form → connect → Lobby. |
| `LobbyController` | Create (mode + 2-6 seats, remembered), join by code, open-tables list, **auto-join from an invite link**. |
| `PlayerStatsView` | *Your stats* card in the lobby. |
| `TableListItemView` | One row of the open-tables list ("Waiting for players" / "Hand in progress"). |

### UI/Game: the table
`GameController` is the **coordinator**: it receives each snapshot and passes it on. It draws only the header.

| Folder / class | Responsibility |
|---|---|
| **Table/** `SeatsView` | All seats: chooses slots for the table size, rotates the table so **you are always at the bottom**, sets deal order. |
| `SeatLayout` | Pure mapping: server seat → screen slot; deal order from the button. |
| `SeatView` | One seat: avatar, name, chips, cards, bet, last action, dealer button, turn timer ring, **your-hand pill**. |
| `CardView` | One card: face / back / empty, flip on reveal, highlight, **deal fly-in**. |
| `BoardView` | Community cards, pot (counts up), street label, "next hand" message. |
| **Panels/** `ActionBar` | Fold / Check-Call / Raise / All-in, raise slider, ½-pot and pot buttons. Only legal moves are clickable. |
| `WaitingRoomPanel` | Friends tables before the start: players count, room code, copy code / link, share, host's **Start now**. |
| `ResultPanel` | Win / lose / out-of-chips overlay (restart with 1,000 free chips). |
| `HandRankingsPanel` | Hint chart: 10 hands with example cards, rules, and a mark on **your current hand**. |
| **Presenters/** `HandResultPresenter` | Decides which result screen to show (once per hand) and highlights the winning cards. |
| `MyHandPresenter` | Works out **your** current hand from your 2 cards + the board and shows it under your chips and in the chart. |
| `SeatWaitPresenter` | Joined a bots game mid-hand: tells the player they will sit when the hand ends, then that a bot gave up its seat. |
| `TableInviteActions` | Copy code, copy link, share invite (with toasts). |
| **Animation/** `TableChangeAnimator` | Compares the **previous and new snapshot** and decides what to animate: new bets, bets into the pot, street banner, pot to the winner. |
| `ChipAnimator` | *How* chips fly: pooled chip images with a jump arc (no allocations per hand). |
| `StageBannerView` | "PRE-FLOP / THE FLOP / THE TURN / THE RIVER / SHOWDOWN" banner. |
| **Audio/** `TableSoundCues` | The audio twin of `TableChangeAnimator`: compares two snapshots and plays deal, check / call / raise / all-in / fold, board flips, chips into the pot and to the winner, your turn, win / lose, player joined, game start. |
| `TurnTimerAlert` | Ticks once a second in the last 5 seconds of your turn (urgent in the last 3). |
| **Preview/** `GamePreviewData` | Sample snapshots so the Game scene can be checked in the Editor without a server. |

### Chat (`Assets/Scripts/Chat/`, namespace `Poker.Chat`): a separate module
| Class | Responsibility |
|---|---|
| `ChatConstants` | Chat event names, limits, texts and quick phrases (kept inside the module). |
| `ChatModels` | Chat payload shapes. |
| `ChatClient` | Chat connection. Uses the socket only through `NetworkManager.Listen/Send`. Knows nothing about poker. |
| `ChatMessageFormat` | One rich-text row per message; player text is shown literally (`<noparse>`), so typed tags can't change the UI. |
| `ChatPanel` | "Chat" button with unread badge, message list, quick phrases, text box. |
| `Editor/Chat/ChatPanelBuilder` | Builds the chat UI; the scene builder only calls `ChatPanelBuilder.Build(canvas, ...)`. |

### Reactions (`Assets/Scripts/Reactions/`, namespace `Poker.Reactions`): a stand-alone module
Uses only Unity, UI and TextMeshPro: no game, network or chat code. The host screen plugs it in through two interfaces.

| Class | Responsibility |
|---|---|
| `ReactionInterfaces` | `IReactionTransport` (send/listen over the host's socket, local user id) and `IReactionScene` (where each user is on screen, who can be targeted). |
| `ReactionClient` | Sends reactions and raises `Received` / `ErrorReceived`. One per transport, so scene reloads don't add listeners. |
| `ReactionLayer` | Full-screen overlay: an emote pops over the sender, or is thrown in an arc to the target and lands with a bump. Unscaled time, max 10 on screen. |
| `EmotePicker` | Pop-up: choose "Everyone" or one player, then tap one of the 12 emotes. Can be opened pre-aimed at a player. |
| `EmoteSet` | ScriptableObject: emote id -> sprite (`Assets/Art/Reactions/EmoteSet.asset`). |
| `ReactionConstants`, `ReactionModels` | Event names, emote ids, timings, texts; payload shapes. |
| `Editor/Reactions/ReactionUIBuilder` | Imports the emote art, builds the layer, the picker and the buttons. |

Game glue (`UI/Game/Reactions/`): `NetworkReactionTransport` adapts `NetworkManager`; `TableReactions` implements `IReactionScene` with the seats (anchor = name plate), opens the picker from the "Emotes" button, the button in the chat window, or a click on a player's plate (aimed at that player), and shows errors as a toast. Chat only offers an empty "tool slot" next to Send; it doesn't know what goes there.

### Profile and settings (client)
| Class | Responsibility |
|---|---|
| `Managers/SettingsManager` | The one place every setting lives: loads from the device, applies to Audio and SessionClock, saves on change, raises `Changed` / `ChangedByPlayer`. |
| `Managers/ProfileManager` | Loads the profile after each login, applies account settings (or uploads the device's the first time), saves profile edits, saves setting changes to the account after a 1 s pause. |
| `UI/Common/ModalPanel` | Shared open/close for pop-ups (fade, pop, backdrop click, Escape). |
| `UI/Common/SettingsPanel` | Music/sound on/off and volume, hand-strength hint, four-colour deck, chat on/off, break reminders. |
| `UI/Common/AvatarView` | Coloured circle + suit symbol for an avatar id. |
| `UI/Lobby/ProfileCardView` | Avatar and display name in the lobby top bar; opens the editor. |
| `UI/Lobby/ProfilePanel` | Edit avatar, display name and bio; sends only changed fields; shows the server's error. |
| `Editor/ProfileUIBuilder` | Builds all of the above. |

### Editor
| Class | Responsibility |
|---|---|
| `PokerSceneBuilder` | **Tools > Poker > Build Scenes** creates Splash, Lobby and Game scenes, prefabs and config, and wires every reference. The whole UI is reproducible from code. |
| `UIFactory` | Helpers to create styled UI objects from code. The visual system lives here: `MakeButton` (shine + depth + hover/press feedback), `Card` (raised panel: shadow, top light, thin border; `Panel` stays flat for small details), `MakeToggle` (pill switch), `MakeInput` (focus ring). Changing a style here restyles every scene. |
| `UISprites` | Generates every UI image as a PNG: rounded box, outline, gloss, circle, ring, glow, background gradient, vignette, and the table (drop shadow, padded rim with highlight, gold inlay, textured felt with a shadow under the rim). |
| `Theme` | Colours for building scenes. Shared colours come from `Constants.UIColors`. |
| `Audio/PlaceholderSoundGenerator` | **Tools > Poker > Generate Placeholder Sounds** (also run by Build Scenes) synthesizes every sound as WAV files and fills empty `SoundLibrary` entries. Never overwrites clips you assigned. |
| `Audio/SynthBuffer` | Tiny synthesizer: decaying tones with harmonics, slides, vibrato and tremolo, filtered noise; looping buffers for seamless music. |
| `Audio/WavWriter` | Saves samples as a 16-bit WAV file. |

---

## 7. Features and how they work

**Play vs bots / Play with friends, 2-6 seats.** The lobby sends `createTable { mode, maxSeats }`. The server validates both (bad values fall back to defaults). Bots tables are filled with bots and hidden from the list, but joinable by code: the joiner waits for the current hand to end, then a bot gives up its seat (`Table.waiting`, `TableRunner.seatWaitingPlayers`).

**Waiting room.** `Table.started` is `false` for friends tables. `TableRunner` doesn't deal until the table is full (or the host sends `startGame`). Every snapshot carries `started` and `hostId`, so every client shows or hides the waiting room on its own, at the same time.

**Invite links.** `?room=ABCDE` in the page URL is read at startup, kept through login, and joined from the lobby. The parameter is then removed from the address so a page reload doesn't join again.

**Seat layouts.** 2 players sit face to face, 3-5 are spread evenly. Positions come from `Constants.TableLayout`.

**Hand rankings hint.** One button opens the chart. The row of your current hand is marked, and the same hand name appears under your chips.

**Sound.** Music for lobby and table (crossfade), card deal and flip, chip bet / raise / all-in / collect / win, check knock, fold swish, your-turn chime, timer ticks, win / lose jingles, player joined, game start, button clicks, error buzz. Sounds are chosen by comparing snapshots (like the animations), so no server changes were needed. Every sound is a placeholder made in code; replace any of them by dropping a recording onto its entry in `Resources/SoundLibrary`.

**Animations.** Cards are dealt from the centre seat by seat. Chips fly for bets and blinds, slide into the pot at the end of each street and fly to the winner. Each street is announced. All of it is driven by comparing two snapshots, so **no extra server events were needed**.

---

## 8. Status

Every requirement in the task brief is mapped to its implementation in the README's **Requirements checklist**.

**Done and verified**
- Server: rules engine, bots, accounts, tables, waiting room, host start, **join a bots table by code (take a bot's seat after the hand)**, **player statistics**, **reconnect grace period**, **WebGL hosting**, **table chat (separate module)**, **emote reactions (stand-alone module)**, **profiles and settings saved in MongoDB (or JSON files)**. **102 unit tests pass**, the storage tests also pass on a real MongoDB (`npm run test:mongo`), and the **end-to-end test** passes on both backends (`npm run test:e2e`, `npm run test:e2e:mongo`).
- Client: all features are implemented, including stats in the lobby, the break reminder, split/side-pot results and the server-restart message. The code **compiles with 0 errors and 0 warnings** for Editor, WebGL and Android. The hand evaluator passes its **24 tests**, and all 20 generated sounds were checked.

**Before submitting**
- Run **Tools > Poker > Build Scenes**, then play-test in Unity (the latest client changes compile but have not been played end to end).
- Make the WebGL build, host it with the server (README section 1) and record the optional 3-player video.

**Known limits** (also in the README's Known issues)
- Tables live in memory: a server restart closes them and the hand in progress is lost (clients are told; accounts and stats survive).
- Chips belong to a table; a friends table never adds bots.
- Invite links open the installed **Android app** only after deep-link setup in the Android manifest; WebGL links work out of the box.

---

## 9. Questions an interviewer may ask

**Why is the server authoritative?** So nobody can cheat by changing the client. The client can only *ask* for an action; the engine checks turn order, bet sizes and chips.

**How do you stop players from seeing others' cards?** Each player gets their own snapshot built by `playerView.js`, the only code that creates client payloads. Hole cards of others are included only at showdown. Bots use the same view.

**Why send full snapshots instead of small events?** The client stays stateless and simple, reconnecting just means receiving the latest snapshot, and animations are derived by comparing two snapshots.

**How do side pots work?** Money is sliced into layers at each all-in level; each layer is won only by players who paid into it (`pots.js`).

**How does the waiting room start for everyone at once?** The server flips `started` and deals; every client receives the same snapshot and closes its waiting room.

**Why a second hand evaluator on the client?** Only to *show* "you have One Pair" instantly. It never decides a winner; the server's evaluator does.

**How is player data stored?** In MongoDB when `MONGODB_URI` is set, otherwise in JSON files. Both sit behind the same five-method collection interface (`src/storage`), so services are written once and the same tests run on both (`npm test` and `npm run test:mongo`). The username has a unique index; login tokens are stored as SHA-256 hashes with a TTL index so MongoDB deletes expired ones.

**How do settings sync?** Settings are applied and saved on the device at once (they work offline and before login). `ProfileManager` saves player changes to the account after a 1-second pause, so dragging a slider sends one request. On login the account's settings win, except the very first time, when the device's are uploaded (`settingsSaved` flag).

**How is chat separate from the game?** The game publishes events (`gameEvents.js`) and never imports chat. Chat gets three functions/objects from `index.js` (which table a player is at, its room, the events). A test fails if any chat file imports game code. In Unity, `Poker.Chat` has its own constants, models and client and uses the socket only through `NetworkManager.Listen/Send`. You could delete chat by removing two folders and two lines.

**How is chat kept safe?** The server takes the name from the login, not the message; only table members can send or read; text is cleaned and cut to 140 characters; 5 messages per 10 s; a short swear-word list is masked; the client shows player text literally so it can't inject rich-text tags.

**How did you keep the client code clean?** One responsibility per class, `GameController` only coordinates, all static data in `Constants`, all data shapes in `DataModel`, and folders named by role (Table, Panels, Presenters, Animation, Platform, Gameplay).

**How would you scale it?** Move accounts to a database, keep table state in one process per table (or sticky sessions), use the Socket.IO Redis adapter for broadcasting across instances.

**What happens if the server goes down?** Clients show "Connection lost. Reconnecting..." and reconnect automatically. Tables live in memory, so after a restart the server tells each client it has no table; the client explains that the table was closed and the hand in progress was not saved, and returns to the lobby. Accounts and stats are on disk and survive. In production I would keep table state in Redis or a database and refund unfinished hands.

**What happens if a player disconnects?** Their seat shows OFFLINE and their turns time out (check if free, otherwise fold). If they return within 2 minutes they get the same seat and chips; after that the seat is freed so the table isn't held up.

**How did you handle responsible play?** One retention feature only (stats: facts, no streaks or rewards), a "Time for a break?" reminder after an hour that can be turned off, free unlimited restarts and no shop.
