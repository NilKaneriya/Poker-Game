# Manual test plan

What the automated tests can't cover: the Unity client, the look and feel, and real
people on real devices. Run it before every submission or release and fill in the
**Result** column (PASS / FAIL / BLOCKED) and **Notes**. Log every FAIL in
[../bugs.md](../bugs.md) with the test ID.

## How to set up
1. Start the server: `cd poker-server && npm start` (or use the deployed link).
2. In Unity: **Tools > Poker > Build Scenes**, then a WebGL build into
   `poker-server/public` (or play in the Editor + a Windows build).
3. For multiplayer: two or three clients (browser windows / Editor + build), **one
   account each**, with *Remember me* **unticked** so they don't share a login.
4. Useful: `http://localhost:3000/metrics` shows tables, players and hands live.

## Session log
| Date | Tester | Build / commit | Devices | Passed | Failed | Blocked |
|---|---|---|---|---|---|---|
| | | | | | | |

---

## 1. Login and account
| ID | Steps | Expected | Result | Notes |
|---|---|---|---|---|
| L-01 | Register a new account | Lands in the Lobby, profile card shows the name | | |
| L-02 | Register the same username again | Clear error, no crash | | |
| L-03 | Log in with a wrong password | Clear error | | |
| L-04 | Log in with *Remember me*, close and reopen | Signed in automatically | | |
| L-05 | Log out, reopen | Login screen, not signed in | | |
| L-06 | 11 wrong logins within a minute | "Too many attempts" message | | |

## 2. Lobby
| ID | Steps | Expected | Result | Notes |
|---|---|---|---|---|
| LB-01 | Look at the lobby after login | Profile card, stats card, create/join cards, open tables list | | |
| LB-02 | Another client creates a friends table | It appears in **Open tables** without refreshing | | |
| LB-03 | Join with a wrong / short code | Clear message, stays in the lobby | | |
| LB-04 | Stop the server | "Connection lost. Reconnecting..." banner; reconnects when it is back | | |

## 3. Tables and waiting room
| ID | Steps | Expected | Result | Notes |
|---|---|---|---|---|
| T-01 | **Play with friends**, 3 players, Create | Waiting room with a 5-letter code and invite link | | |
| T-02 | Copy code / copy link / share | Copied (toast), link opens the game and joins (WebGL) | | |
| T-03 | Second and third player join by code | Seats fill for everyone; game starts when the table is full | | |
| T-04 | Host presses **Start now** with 2 of 3 players | Game starts for both | | |
| T-05 | Non-host tries to start | Not possible | | |
| T-06 | **Play vs bots** with 6 seats | Table fills with bots, hand starts at once | | |
| T-07 | A friend joins your bots table by code mid-hand | They watch ("waiting for a seat"); after the hand a bot stands up and they sit | | |

## 4. Poker rules (play real hands)
| ID | Steps | Expected | Result | Notes |
|---|---|---|---|---|
| G-01 | Play a hand to showdown | Correct winner and hand name; pot moves to the winner | | |
| G-02 | Everyone folds to one player | That player wins, no cards shown | | |
| G-03 | Blinds and button over several hands | Button and blinds move clockwise | | |
| G-04 | Heads-up (2 players) | Button posts the small blind and acts first pre-flop | | |
| G-05 | Split pot (same hand) | Pot split, "Split pot" shown | | |
| SP-01 | Two players all-in for different amounts, a third covers | Main pot + side pot, each paid to the right player | | |
| SP-02 | Short all-in raise | Players who already acted can only call, not re-raise | | |
| G-06 | Lose all chips | Spectator with "Restart with 1,000" | | |

## 5. Turn timer and betting helpers
| ID | Steps | Expected | Result | Notes |
|---|---|---|---|---|
| TM-01 | Don't act on your turn | Ring counts 30 s; you check if free, else fold | | |
| TM-02 | Last seconds of your turn | Urgent sound / colour | | |
| BH-01 | **½ Pot** and **Pot** buttons | Raise amount set to half / full pot (after calling) | | |
| BH-02 | Drag the slider, type an amount | Stays between min and max raise; button label updates | | |
| BH-03 | Type more than you have | Clamped to the maximum | | |
| BH-04 | **All-in** | Whole stack goes in; badge shows ALL-IN | | |
| BH-05 | Only legal buttons enabled | E.g. no Check when there is a bet to call | | |

## 6. Bots
| ID | Steps | Expected | Result | Notes |
|---|---|---|---|---|
| B-01 | Play 10 hands vs bots | Bots act after a short "think", never out of turn | | |
| B-02 | Watch bot levels (easy / normal / hard) | Hard bots raise strong hands more, easy ones call more | | |

## 7. Edge cases
| ID | Steps | Expected | Result | Notes |
|---|---|---|---|---|
| E-01 | Close a client mid-hand | Seat shows OFFLINE, their turns time out | | |
| E-02 | Reopen within 2 minutes | Back in the same seat with the same chips | | |
| E-03 | Stay away more than 2 minutes | Seat freed | | |
| E-04 | Leave mid-hand | Folded; chips already bet stay in the pot | | |
| E-05 | Restart the server mid-hand | Clients told the table closed, back to the lobby | | |
| E-06 | Double-click action buttons quickly | Only one action counts | | |
| E-07 | Same account in two tabs | Both stay in sync; seat not marked offline while one is open | | |

## 8. Chat
| ID | Steps | Expected | Result | Notes |
|---|---|---|---|---|
| C-01 | Send a message | Everyone at the table sees it, with the sender's name | | |
| C-02 | Quick phrases | Sent with one tap | | |
| C-03 | Chat closed, other player writes | Unread badge counts up | | |
| C-04 | Send 6 messages fast | "Slow down" message after 5 | | |
| C-05 | Type `<color=red>hi</color>` | Shown as typed, not red | | |
| C-06 | Join a table late | Recent history shown, including "X won Y with Z" lines | | |
| C-07 | Turn chat off in Settings | Chat button and window hidden | | |

## 9. Emote reactions
| ID | Steps | Expected | Result | Notes |
|---|---|---|---|---|
| R-01 | **Emotes** button > Everyone > an emote | Emote pops over your seat for everyone | | |
| R-02 | Click another player's name plate > emote | Picker says "Send to <name>"; emote flies to their seat | | |
| R-03 | Emote button inside the chat window | Same picker opens | | |
| R-04 | Send 5 emotes fast | Toast "Easy! Wait a moment..." after 4 | | |
| R-05 | Escape / click outside the picker | Picker closes | | |

## 10. Hand history
| ID | Steps | Expected | Result | Notes |
|---|---|---|---|---|
| H-01 | Play 3 hands, open **History** at the table | 3 hands, newest first, your cards and +/- chips | | |
| H-02 | Pick a hand that went to showdown | Board, every street's actions, showdown cards, winners | | |
| H-03 | Pick a hand won without showdown | Opponents' cards **not** shown | | |
| H-04 | A hand where you folded | Row says "Folded"; your cards still shown to you | | |
| H-05 | Open it from the Lobby stats card | Same hands | | |
| H-06 | Four-colour deck on | Mini cards use four colours | | |

## 11. Profile, settings and stats
| ID | Steps | Expected | Result | Notes |
|---|---|---|---|---|
| P-01 | Edit display name, bio, avatar; Save | Saved; profile card updates | | |
| P-02 | Rename while seated | New name on your seat and in chat for everyone | | |
| P-03 | Invalid name (2 letters, symbols) | Clear error, not saved | | |
| P-04 | Change every setting, log in on another device | Same settings there | | |
| P-05 | Music / sound volume sliders | Volume changes at once | | |
| P-06 | Hand-strength hint off | No "One Pair" pill under your chips | | |
| S-01 | Play hands, look at **Your stats** | Hands played / won / biggest pot / best hand update | | |

## 12. Responsible play
| ID | Steps | Expected | Result | Notes |
|---|---|---|---|---|
| W-01 | Play for 60 minutes (or lower `Wellbeing.FirstReminderMinutes` for the test) | Break reminder appears, can be dismissed | | |
| W-02 | Turn break reminders off | No reminder | | |
| W-03 | Look for any real-money wording or purchase | No real money anywhere; the shop says it is a demo and nothing is charged | | |

## 12b. Chip wallet and shop
The server rules for these are covered by `npm test` (see `qa/reports/economy/`); this checks the screens.

| ID | Steps | Expected | Result | Notes |
|---|---|---|---|---|
| C-01 | Register a new account, open the lobby | Top bar shows **10,000 chips +** | | |
| C-02 | Create a table (any mode) | Wallet drops by 1,000 (9,000); you sit with 1,000 | | |
| C-03 | Leave the table | Wallet is back to 10,000 (plus or minus what you won or lost) | | |
| C-04 | Click the wallet button | Shop opens: three items, prices say "(demo)", note says nothing is charged | | |
| C-05 | Click **Get** on the first item | Green message, wallet +5,000; buttons are locked for about a second | | |
| C-06 | Click **Get** twice very fast | Only one purchase happens | | |
| C-07 | Lose all chips at a table, click **Buy in again** | Wallet drops by 1,000, you are dealt in next hand | | |
| C-08 | Have under 200 chips in the wallet, try to sit down | Toast: "You need at least 200 chips ... Visit the shop."; no table opens | | |
| C-09 | Same account in a second tab | First tab returns to login with "You signed in from another tab or device" | | |
| C-10 | Close the tab mid-hand, come back within 2 minutes | Same seat and chips; wallet unchanged | | |
| C-11 | Close the tab, wait over 2 minutes, log in | Not seated; wallet shows the stack you left with | | |

## 13. Look and feel
| ID | Steps | Expected | Result | Notes |
|---|---|---|---|---|
| U-01 | Open every pop-up (settings, profile, history, rankings, emotes) | Fades and scales in; closes with X, outside click, Escape | | |
| U-02 | Buttons | Grow on hover, squeeze when pressed, click sound | | |
| U-03 | Deal, bet, win | Cards fly in, chips slide to the pot and to the winner | | |
| U-04 | 1920x1080 and a smaller window | Nothing overlaps or is cut off | | |
| U-05 | Long names (16 characters) | Fit on seats, chat and history | | |

## 14. Platforms
| ID | Steps | Expected | Result | Notes |
|---|---|---|---|---|
| X-01 | WebGL in Chrome | Loads, plays, audio after the first click | | |
| X-02 | WebGL in Firefox / Edge | Same | | |
| X-03 | Two different devices on the deployed link | Play the same table | | |
| X-04 | Landscape phone (optional) | Playable, text small | | |
