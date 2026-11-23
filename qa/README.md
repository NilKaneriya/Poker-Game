# QA

Everything about testing the poker game in one place: what is tested and how, the tools,
the manual test plan, the bug log and every report.

```
qa/
  README.md                  this page: strategy, how to run, latest results
  bugs.md                    every defect found, how, the fix and its regression test
  manual/test-plan.md        manual test cases (Unity client, multiplayer, look and feel)
  load-test/load-test.js     load test: hundreds of players and bots at once
  scripts/run-all.js         runs every automated check and saves a report
  reports/automated/         one report per run of run-all.js
  reports/task-compliance.md every line of the task sheet checked against code, tests and the browser test
  reports/load-test/         one report per load test run (before / after fixes)
  package.json               npm run test:all, npm run load-test
```

## Test strategy

| Level | What it covers | Where | Runs in CI |
|---|---|---|---|
| **Unit tests** (115) | Poker rules: hand ranking, side pots, pot splits, betting rules, timeouts; table and waiting list; bots; stats; hand history records (hidden cards stay hidden); chat; reactions; profile rules; storage on memory, JSON files and MongoDB; concurrent saves | `poker-server/test/` | yes |
| **End-to-end** | A real server and real Socket.IO clients: login, tables, join by code, waiting room, hands, rejected cheating, chat, profile, stats, hand history, disconnect and reconnect | `poker-server/scripts/test-client.js` via `npm run test:e2e` (JSON) and `test:e2e:mongo` | yes |
| **Lint** | ESLint (real mistakes) + Prettier (formatting) | `poker-server/eslint.config.js` | yes |
| **CI** | Lint, unit, MongoDB and both end-to-end runs on Linux, on every push and PR | `.github/workflows/server.yml` | - |
| **Load test** | 200 tables, 1,200 players and bots at ~10x real speed: latency, throughput, event-loop delay, memory | [`load-test/load-test.js`](load-test/load-test.js) | no (run before a release) |
| **Manual** | The Unity client, multiplayer on real devices, animations, sound, layout | [`manual/test-plan.md`](manual/test-plan.md) | no |

The server decides every game result, so the rules are tested where they live (unit tests
on the pure engine) and over the real network (end-to-end). The Unity client only shows
state, so it is covered by the manual plan.

## How to run

```bash
cd qa
npm install                                   # once (socket.io-client for the load test)

npm run test:all                              # every automated check -> reports/automated/<date>.md
npm run test:all -- --skip-mongo              # same without MongoDB (no download)

npm run load-test                             # 50 tables, 60 s
npm run load-test -- --tables=200 --seconds=60 --report   # -> reports/load-test/<date>-200t-60s.md
```

Single checks run from `poker-server/`: `npm test`, `npm run test:mongo`,
`npm run test:e2e`, `npm run test:e2e:mongo`, `npm run lint`. `GET /metrics` on a running
server shows tables, players, hands per minute, event-loop delay and memory.

## Latest results

| Check | Result | Report |
|---|---|---|
| All automated checks (2026-11-23) | **PASS**: lint, 115 unit, 21 MongoDB storage, end-to-end on JSON and MongoDB | [2026-11-23_00-17.md](reports/automated/2026-11-23_00-17.md) |
| Load test, 200 tables, 60 s | 2,369 hands/min, action answered p50 5.5 ms / p99 35 ms, 0 errors | [2026-11-23-200t-60s.md](reports/load-test/2026-11-23-200t-60s.md) |
| Load test before the storage fix | 1,466 hands/min, p99 545 ms (event loop blocked) | [2026-11-22-200t-40s-before-fix.md](reports/load-test/2026-11-22-200t-40s-before-fix.md) |
| Manual test plan | to be run before submission | [manual/test-plan.md](manual/test-plan.md) |

## What testing found

9 defects so far, all fixed; the biggest came from the load test, the MongoDB end-to-end
run and the first CI run on Linux, not from reading code. Details in [bugs.md](bugs.md).

- **Lost updates** (High): two hands of one player ending together could erase each
  other's stats / history. Fixed with a per-player save queue; proven by a test with a
  deliberately slow database.
- **Blocking JSON writes** (Medium): the event loop stalled up to 0.7 s under load. Saves
  are now batched; p99 latency went from 545 ms to 34 ms.

## Known limits of the testing
- The Unity client has no automated tests (manual plan only).
- The load test runs on one machine, so client and server share the CPU; real numbers on
  a server will be better. On Windows the event-loop delay has a 15-25 ms floor from the
  OS timer.
- No soak test (hours of play) yet; memory is bounded by design (30 hands per player).
