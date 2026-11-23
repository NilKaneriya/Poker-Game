# Task sheet compliance

Every line of "Task: Build a Multiplayer Poker Sample" checked against the code, the
automated tests and the browser test (WebGL build, up to 7 accounts at once, 2026-11-23).
**Result:** every required item is done. Items that could not be fully verified are marked
and listed at the bottom.

Legend: **Done** = implemented and tested. **Done (manual)** = implemented, checked by hand
or in the browser test, no automated test.

## Core assumptions

| Sheet says | Status | Evidence |
|---|---|---|
| Texas Hold'em, 2 to 6 players per table | Done | Table size 2-6 (`config.table`); a 6-player table is tested over real sockets (`sixPlayers.integration.test.js`) |
| Virtual chips, each player starting at 1,000 | Done | `config.table.startingChips = 1000`, every join starts with 1,000 (or the stack you left with, see Known issues) |
| No in-app purchases, loot boxes or real-money wagering | Done | Nothing is sold or charged anywhere; the lobby says "Virtual chips only • No real money • No purchases" |

## 1. Multiplayer and networking

| Requirement | Status | Evidence |
|---|---|---|
| Create a table, join by code or list, from different devices | Done | Lobby: create, 5-letter code, open-tables list, invite links; browser test joined by code and from the list |
| Server-authoritative shuffle, deal, winner | Done | `cards.js` (crypto shuffle), `HoldemHand.js`, `handEvaluator.js`; clients send only action and amount |
| No player sees opponents' cards; client never sends a result | Done | `playerView.js`; tests: public snapshot has no hole cards, bots cannot peek, and every state received by 6 clients is checked for leaks |
| Real-time sync with secrets hidden per role | Done | Per-player `tableState` after every change; browser test: 3-4 tabs always matched; one account in two tabs now signs the first out (BUG-10) |
| 30 s turn timer: fold, or check if allowed | Done | `TurnTimer.js`, `actOnTimeout`; browser test: folded at exactly 30 s, auto-checked when checking was free |
| Players joining and leaving mid-hand | Done | Joiners watch ("Dealt in next hand"); leavers are folded and their bets stay in the pot (BUG-11, tested) |

## 2. Poker logic

| Requirement | Status | Evidence |
|---|---|---|
| Deal, blinds, dealer button moving | Done | Tested heads-up, 3-handed and 6-player; button moves every hand |
| Pre-flop, flop, turn, river, showdown | Done | Every stage seen on a 6-player table in a test |
| Fold, Check, Call, Raise, All-in; illegal ones disabled | Done | `getLegalActions` (server) and `ActionBar` (client) |
| Correct rankings, Royal Flush to High Card; ties | Done | `handEvaluator.test.js`: every rank, kickers, wheel, 5 of 7, board plays. (The browser test only reached full house; the other ranks are covered by unit tests) |
| Side pots with different all-ins (mandatory) | Done | `pots.test.js`; browser test: 4-way all-in, main pot plus 2 side pots, uncalled chips returned, total exactly 4,000 |

## 3. User interface

| Requirement | Status | Evidence |
|---|---|---|
| Clear layout, mobile or desktop stated | Done | README: desktop landscape (also runs on landscape phones) |
| Cards, board, pot, chips, turn, last action | Done (manual) | Browser test; BET/RAISE labels corrected (BUG-13) |
| Show winning hands at the end | Done (manual) | Result pop-up with winner, hand name and the best 5 cards |
| Understandable without a manual | Done (manual) | Only legal buttons enabled, hand-rankings chart, street banners |

## 4. Betting

| Requirement | Status | Evidence |
|---|---|---|
| Min bet = big blind; min raise >= previous raise | Done | `holdemHand.test.js`; browser: flop min bet 20, re-raise minimum 580 after a raise to 300 |
| Half pot / pot buttons and custom amount | Done | Browser: 128 and 255 on a 255 pot. The Raise button now follows typing (BUG-13) |
| Chips below the call are automatically all-in | Done | Tested; browser: a 1,180 stack pressing Call vs a 1,290 all-in went all-in for 1,180 |

## 5. Computer opponents

| Requirement | Status | Evidence |
|---|---|---|
| Bots fill a table with fewer than 2 humans | Done | Bots tables fill empty seats; a friend can join by code and replaces a bot |
| 2-3 levels with different behaviour | Done | Easy (calls a lot), normal (by strength), hard (tight, bigger raises, bluffs); `bots.test.js` (200 bot-only hands, legal actions only, no peeking) |

## 6. Edge cases

| Requirement | Status | Evidence |
|---|---|---|
| Split pot; odd chip to the player closest to the button | Done | `pots.test.js` "odd chip goes to the tied winner closest to the left of the button"; browser: two even splits paid exactly |
| Out of chips: spectator, restart with 1,000, no purchase | Done | "Out of chips" screen with Restart (free) and Back to lobby; a fake rebuy while holding chips is rejected |
| Disconnect mid-hand: auto fold, reconnect keeps state | Done | Seat shown offline, folded after the timeout; reconnect returned the same seat, chips and cards; seat freed after 2 minutes |
| Server disconnection: clear message; wagers explained in README | Done | Banner "Connection lost. Reconnecting..."; README Known issues explains what happens to bets in the hand in progress |
| Cheating attempts rejected | Done | Browser test: check facing a bet, raise below min or above stack, negative, decimal, text amounts, unknown action, out of turn, rebuy with chips: all rejected with a reason |

## 7. Responsible retention

| Requirement | Status | Evidence |
|---|---|---|
| One feature only (statistics) | Done | Hands played, hands won, biggest pot, best hand; updates after each hand |
| No pressure counters, timed rewards or excess-play mechanics | Done | None. The only timer is the required 30 s turn timer |
| Notification after a long session, with an off switch | Done (manual) | Break reminder after 60 minutes (`Wellbeing.FirstReminderMinutes`) with a "Remind me" toggle; the toggle was seen, the reminder itself was not waited for. To see it quickly, lower that constant |

## Submission items

| Item | Status |
|---|---|
| Link to run the game | WebGL: https://webgl-sooty.vercel.app with the server on Render (free host: first request can be slow) |
| Full source on GitHub | Yes |
| README: how to run, how to test multiplayer, design decisions, known issues | Yes (sections 1-5) |
| Short video of a full 3-player hand | Not made yet (optional) |
| Networking service documented | Socket.IO, in the README table |

## Optional extras (not scored)

In-table text chat, sound effects and card-deal animation, automated tests for ranking and
pot distribution: all present (see README).

## What could not be fully verified

- The long-session reminder firing (needs a 60 minute session or the constant lowered).
- Mobile layout, other browsers, real separate devices (everything was tested in desktop Chrome).
- A real server outage (only client-side drops were simulated).
- Bot behaviour differences are by design and unit-tested for legality, not measured over many hands.

## Known issues kept out of scope

- Chips are kept between tables per server run (leave with 710, sit with 710); a busted player restarts with 1,000.
- The "dealt card briefly covers a name plate" glitch from the browser test is cosmetic and not fixed.
- Text can land in the previously focused box in WebGL if you type within about a second of clicking; mitigated, not proven fixed.
