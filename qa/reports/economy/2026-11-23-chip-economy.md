# Chip economy test report

Date: 2026-11-23 · Branch: `feature/chip-economy` · Scope: server (wallet, buy-in, shop)

## Result

All 197 server tests pass, including 78 for the economy: 46 wallet unit tests (each run on
both storage backends: in-memory and JSON files) and 32 integration tests that start a real
server and connect real Socket.IO clients. The integration suite was run 3 times in a
row without a failure. The Unity screens (wallet button, shop panel) are not covered by
automated tests; use section 12b of `qa/manual/test-plan.md`.

Run it: `cd poker-server && npm test` (about 25 s).

## How the economy works

- Each account has a wallet, starting at 10,000 chips, saved on the server.
- Sitting down moves a buy-in (1,000, or what the wallet holds, minimum 200) from the wallet to the seat.
- Leaving, or being removed after the 2-minute grace period, moves the stack back.
- Out of chips: "Buy in again" takes another buy-in from the wallet.
- The demo shop adds chips and charges nothing. It is the only source of chips besides the starting grant.

## What was tested

| Area | Checks |
|---|---|
| Starting chips | New account has 10,000; same after reconnecting; separate per account |
| Sit down / leave | Charged once; seat holds exactly the buy-in; stack returns on leaving |
| Conservation | Two players go all-in: after both leave, wallets add up to exactly 20,000. A player leaving mid-hand loses only the blind already posted; the opponent gains it; total unchanged |
| Rebuy | Paid from the wallet; triple-click charges once; refused (and free) while you still have chips or are not seated |
| Forged input | Extra fields (`chips`, `buyIn`, `startingChips`, `amount`, `balance`) on join and create are ignored. Events such as `setChips`, `cashOut`, `grant`, `admin` do nothing |
| Invalid amounts | 0, negatives, decimals, NaN, Infinity, strings, objects, arrays, huge numbers (2^60, 1e21) rejected for buy-in and cash-out; nothing changes |
| Double spend | 50 parallel buy-ins spend each chip at most once (balance never negative). Five quick create-table or join clicks charge once |
| Races | Two players for the last seat: one sits, the other is refunded in full. The same account in two tabs: the first is signed out, chips not duplicated. Moving across three tables keeps one stack |
| Disconnects | Short drop keeps seat and chips. Not returning in 2 minutes frees the seat and returns the chips. A bots-table waiting-list buy-in is held and refunded if you leave |
| Crash | Server killed while seated: chips are back on next login, exactly once (not twice) |
| Low balance | 150 chips: cannot sit, no empty table left behind, nothing charged. After a shop purchase the same player can sit |
| Shop | Each item adds exactly its catalog amount. Unknown ids, `constructor`, `__proto__`, wrong case, spaces, arrays, objects, non-object payloads, 100,000-character ids all refused. Forged price, amount and quantity fields ignored. 50 requests in a burst buy once; the same from two tabs still buys once. Wallet at its ceiling refuses. Accounts cannot spend or spoil each other's wallet |
| Access | No token, a fake token, an object token or a logged-out token cannot connect. No HTTP route changes chips; `/auth/me` does not carry chips |
| Ledger | Recent transactions are bounded to 30 per wallet and each records type, amount, balance after |
| Randomised | 400 random buy-in and cash-out operations on a slow (database-like) storage keep the balance a whole number at or above 0 and the table money tracked |

## Checks that the tests catch bugs

Temporarily removing the shop cooldown made 2 tests fail, as expected. Removing the
per-player lock did not make a test fail, because a second safeguard (the re-check after
payment, with a refund) still stops the double charge; both guards are kept.

## Not covered

- The Unity wallet button and shop panel (not compiled or played in this session).
- MongoDB for the wallet collection (same API as the JSON backend; `npm run test:e2e:mongo` exercises the other collections).
- A real payment provider: the shop is a demo by design.
- Chips won at a table before a server crash are not kept (the buy-in is refunded); documented in the README.
