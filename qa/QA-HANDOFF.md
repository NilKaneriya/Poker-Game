# Final QA handoff

Build for the last round of testing. The previous round (2026-11-23, desktop Chrome, up to 7
accounts) found 10 issues; all are fixed in this build and need a re-test. The full
requirement check against the task sheet is in
[reports/task-compliance.md](reports/task-compliance.md).

## What to test

| | |
|---|---|
| Game (WebGL) | https://webgl-sooty.vercel.app |
| Server | https://poker-game-zw79.onrender.com (`/health` should answer `{"status":"ok"}`) |
| Source | branch `develop` on GitHub |
| Platform | desktop browser, landscape. Chrome first, then Firefox or Edge if time allows |
| Server note | free host: the first request after a quiet period can take up to a minute |

Before starting, open the server `/health` link once so it is awake.

## Setup for multiplayer

- One account per player. Register in each tab, and leave *Remember me* **unticked**.
- Use 3 tabs for the main hand test; 6 tabs (or 3 humans + bots) for a full table.
- Usernames: 3-16 letters, numbers or `_` (no hyphens).

## Re-test the 10 issues from the last round

| # | Issue | How to check it is fixed |
|---|---|---|
| 1 | Same account in two tabs broke silently | Sit at a table in tab A. Open the game in tab B with the same account. Tab A must return to login saying "You signed in from another tab or device". Log out in B: no stuck "Reconnecting..." anywhere |
| 2 | Leaving player's bet taken from the pot | Heads-up. Big blind leaves before the small blind acts. Small blind should win 30 (stack 1,020) |
| 3 | Stack reset to 1,000 on every join | Leave a table with a stack other than 1,000, join again: you keep your stack. Only at 0 chips do you get 1,000 again |
| 4 | Typed raise amount ignored until Enter | Type 300 in the amount box: the button must read "Raise to 300" while typing, and clicking Raise sends 300 |
| 5 | Input focus lag | Click a text field and type straight away: text must go into the clicked field. The room code field must not accept spaces or symbols |
| 6 | Turn timer hidden behind pop-ups | On your turn open Hand history: a "Your turn Ns" counter stays visible on top |
| 7 | Leave blocked by result pop-up | While "You win / Not this time" shows, click Leave: it must work |
| 8 | Wrong labels | First bet on the flop reads BET (not RAISE). Hand history says "You post / You fold / You check" |
| 9 | Old result pop-up on a new table | Finish a hand and switch tables quickly: no old "You win" on the new table |
| 10 | Layout glitches | Emote panel has no large empty gap. (Known and not fixed: a dealt card can briefly cover a name plate) |

Also check the login screen: labels sit above their fields without overlapping, and a long
error message stays inside the card.

## Regression: please run the rest of the plan

[manual/test-plan.md](manual/test-plan.md) lists the cases. Priorities:

1. **A full hand with 3 players** to showdown, and a 4-way all-in with different stacks
   (side pots; chips must add up).
2. **Hidden cards:** nobody sees another player's cards before showdown.
3. **Disconnect:** close a tab mid-hand; it folds after the timeout; reopening within 2 minutes
   returns to the same seat and chips.
4. **Out of chips:** lose everything: "Out of chips" screen offers *Restart with 1,000* and
   *Back to lobby*; nothing to buy anywhere.
5. **Cheating attempts** are rejected with a message (out of turn, raise below the minimum or
   above the stack).
6. **Break reminder:** toggle exists in the lobby and settings. To see the reminder itself,
   lower `Wellbeing.FirstReminderMinutes` in `Constants.cs` for a test build.

## Not covered by this build

- Mobile layout and portrait (desktop landscape only).

## Reporting

Log every failure in [bugs.md](bugs.md) with the test ID, steps, expected and actual result,
browser, and a screenshot. Mark severity High (wrong game result or lost chips), Medium
(visible to players) or Low (cosmetic).
