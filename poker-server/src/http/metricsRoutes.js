const express = require('express');
const { monitorEventLoopDelay } = require('perf_hooks');

/**
 * GET /metrics: live numbers about this server, as JSON, for load tests and a quick health
 * look in production. Nothing private: counts, memory and event-loop delay only.
 *
 *   tables, seated humans, bots, people waiting for a seat, connected sockets,
 *   hands finished since start (and per minute over the last minute),
 *   event-loop delay (how late timers run: the first sign of an overloaded server)
 *
 * @param {{ io, tables, gameEvents }} deps  tables = TableManager, gameEvents = game event bus
 */
function createMetricsRoutes({ io, tables, gameEvents }) {
  const router = express.Router();
  const startedAt = Date.now();
  const loopDelay = monitorEventLoopDelay({ resolution: 10 });
  loopDelay.enable();

  let handsFinished = 0;
  const recentHands = []; // finish times inside the last minute
  gameEvents.on(gameEvents.GAME_EVENTS.HAND_FINISHED, () => {
    handsFinished++;
    recentHands.push(Date.now());
  });

  router.get('/', (req, res) => {
    const now = Date.now();
    while (recentHands.length && now - recentHands[0] > 60_000) recentHands.shift();

    let humans = 0;
    let bots = 0;
    let waiting = 0;
    let handsRunning = 0;
    for (const table of tables.tables.values()) {
      for (const seat of table.players) seat.isBot ? bots++ : humans++;
      waiting += table.waiting.length;
      if (table.hand && !table.hand.isComplete) handsRunning++;
    }

    const memory = process.memoryUsage();
    const ms = (ns) => Math.round(ns / 1e4) / 100; // ns -> ms, 2 decimals
    res.json({
      uptimeSec: Math.round((now - startedAt) / 1000),
      sockets: io.engine.clientsCount,
      tables: tables.tables.size,
      players: { humans, bots, waiting },
      hands: { running: handsRunning, finished: handsFinished, lastMinute: recentHands.length },
      eventLoopDelayMs: { p50: ms(loopDelay.percentile(50)), p99: ms(loopDelay.percentile(99)), max: ms(loopDelay.max) },
      memoryMb: { rss: Math.round(memory.rss / 1048576), heapUsed: Math.round(memory.heapUsed / 1048576) },
    });
    loopDelay.reset(); // each read covers the time since the last one
  });

  return router;
}

module.exports = { createMetricsRoutes };
