# Bug log

Every real defect found during development, how it was found, and what stops it coming
back. Newest first. Severity: **High** = wrong game result or lost data, **Medium** =
visible to players, **Low** = cosmetic or internal.

| ID | Title | Severity | Found by | Status |
|---|---|---|---|---|
| [BUG-08](#bug-08) | Stats and hand history lost updates when two hands ended together | High | End-to-end test on MongoDB | Fixed |
| [BUG-07](#bug-07) | JSON storage blocked the server for up to 0.7 s under load | Medium | Load test | Fixed |
| [BUG-06](#bug-06) | End-to-end stats check could read the wrong hand | Low (test) | End-to-end test on MongoDB | Fixed |
| [BUG-05](#bug-05) | Settings and profile pop-ups never cleaned up their animations | Low | Code review | Fixed |
| [BUG-04](#bug-04) | Chat showed a player's old name after they renamed | Medium | End-to-end test | Fixed |
| [BUG-03](#bug-03) | Swear-word filter did not mask anything | Medium | Unit test | Fixed |
| [BUG-02](#bug-02) | Profile end-to-end check timed out (chat rate limit) | Low (test) | End-to-end test | Fixed |
| [BUG-01](#bug-01) | Express error handler would be ignored if its unused argument was removed | Low | ESLint | Kept, documented |

---

### BUG-08
**Stats and hand history lost updates when two hands ended together** (High)
- **Found by:** `npm run test:e2e:mongo`. Alice's newest hand in the history was from a
  bots table she had left, and the hand she had just played was missing.
- **Cause:** `StatsService` and `HandHistoryService` read a player's document, change it
  and write it back. With real database round trips, two hands of the same player
  finishing close together interleaved: both read the old document, and the second
  write erased the first (a "lost update"). JSON storage hid it because its reads
  return at once.
- **Fix:** `poker-server/src/utils/KeyedQueue.js` runs those saves one at a time per
  player (other players still save in parallel). Commit `4adc81c`.
- **Regression test:** `poker-server/test/concurrency.test.js` with a deliberately slow
  collection. Without the fix: 20 hands at once were counted as 2, and 1 of 10 hands was
  kept. With the fix: all of them.

### BUG-07
**JSON storage blocked the server for up to 0.7 s under load** (Medium)
- **Found by:** load test, 200 tables ([before](reports/load-test/2026-11-22-200t-40s-before-fix.md),
  [after](reports/load-test/2026-11-23-200t-60s.md)).
- **Cause:** every save rewrote the whole JSON file synchronously, pretty-printed. The
  hand-history file reached several MB and was rewritten about 24 times a second.
- **Fix:** `JsonCollection` batches writes: at most one background write per second per
  file, through a temp file + rename; `storage.close()` writes what is pending on
  shutdown. Commit `6da1b00`. Production uses MongoDB, which never had this problem.
- **Result:** p99 action answer 545 ms -> 34 ms; hands per minute 1,466 -> 2,508.
- **Regression test:** `test/storage.test.js` "many quick changes become one file write,
  and nothing is lost"; re-run the load test.

### BUG-06
**End-to-end stats check could read the wrong hand** (Low, test only)
- **Found by:** `npm run test:e2e:mongo` failing after BUG-08 was fixed.
- **Cause:** the check waited for "hands played went up", but a hand at a table Alice
  had left earlier (finished when its turn timer ran out, 30 s later) also counts, so the
  check sometimes ran before the hand it meant to look at had ended.
- **Fix:** wait for the Stats table's own hand to end, then look for it anywhere in the
  history. Commit `4adc81c`. Then 4/4 JSON and 2/2 MongoDB runs passed in a row.

### BUG-05
**Settings and profile pop-ups never cleaned up their animations** (Low)
- **Found by:** code review while adding the hand history panel.
- **Cause:** `ProfilePanel` and `SettingsPanel` declared their own private `OnDestroy`,
  which hid `ModalPanel.OnDestroy`, so `DOTween.Kill` never ran for them.
- **Fix:** `ModalPanel.OnDestroy` is `protected virtual`; the panels override it and call
  `base.OnDestroy()`. Commit `a0c4bf1`.

### BUG-04
**Chat showed a player's old name after they renamed** (Medium)
- **Found by:** the profile end-to-end check ("live name at the table and in chat").
- **Cause:** chat read the sender's name once, when the socket connected.
- **Fix:** read `socket.data.name` for every message; `profileSync` updates it on rename.
- **Regression test:** `scripts/test-client.js` profile checks.

### BUG-03
**Swear-word filter did not mask anything** (Medium)
- **Found by:** unit test `chat text: swear words are masked`.
- **Cause:** the regex was built in a template literal, so `\b` became a backspace
  character instead of a word boundary.
- **Fix:** build it with `String.raw`.

### BUG-02
**Profile end-to-end check timed out** (Low, test only)
- **Cause:** Alice had already sent the maximum number of chat messages, so the rate
  limit (5 per 10 s) silently dropped the message the check waited for.
- **Fix:** Bob sends the message in the profile check.

### BUG-01
**Express error handler would be ignored if its unused argument was removed** (Low)
- **Found by:** ESLint (`no-unused-vars` on `next`).
- **Why it matters:** Express only treats a function as an error handler when it takes
  4 arguments. "Fixing" the warning by deleting `next` would have broken error replies.
- **Fix:** renamed to `_next` with a comment explaining why it stays. Commit `63785f2`.
