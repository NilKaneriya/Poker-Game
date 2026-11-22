const { compare } = require('./handEvaluator');

/**
 * Pot building and distribution, including side pots.
 *
 * Side pots in one sentence: a player can only win, from each opponent, as much as
 * they put in themselves. So the money is sliced into layers at every all-in level.
 *
 * Example: A all-in 50, B all-in 200, C calls 200.
 *   Main pot  = 50 x 3 = 150   -> A, B, C can win it
 *   Side pot  = 150 x 2 = 300  -> only B and C can win it
 */

/**
 * @param {{ playerId: string, amount: number, folded: boolean }[]} contributions
 *        total chips each player put in during the hand
 * @returns {{ amount: number, eligible: string[] }[]} main pot first, then side pots
 */
function buildPots(contributions) {
  const levels = [...new Set(contributions.map((c) => c.amount).filter((a) => a > 0))].sort((a, b) => a - b);
  const pots = [];
  let previousLevel = 0;

  for (const level of levels) {
    // Everyone pays into this layer up to its height (folded players' chips count too).
    let amount = 0;
    for (const c of contributions) amount += Math.max(0, Math.min(c.amount, level) - previousLevel);

    // Only players still in the hand who reached this level can win it.
    const eligible = contributions.filter((c) => !c.folded && c.amount >= level).map((c) => c.playerId);
    previousLevel = level;

    const last = pots[pots.length - 1];
    if (eligible.length === 0 && last) {
      last.amount += amount; // chips above everyone still in (from folded players) stay in the pot below
    } else if (last && sameMembers(last.eligible, eligible)) {
      last.amount += amount; // same contenders: one pot, not two
    } else {
      pots.push({ amount, eligible });
    }
  }
  return pots;
}

/**
 * @param {{ amount: number, eligible: string[] }[]} pots from buildPots
 * @param {Map<string, object>} hands evaluated hand per player still in (see handEvaluator)
 * @param {string[]} seatOrderFromButton player ids clockwise starting left of the button
 * @returns {{ amount: number, winners: { playerId: string, share: number }[] }[]}
 *
 * Ties split the pot equally. Chips that can't be split evenly go to the winner
 * closest to the left of the button.
 */
function distributePots(pots, hands, seatOrderFromButton) {
  return pots.map((pot) => {
    const contenders = pot.eligible.filter((id) => hands.has(id));
    // If nobody has a hand to show (everyone else folded), the eligible player takes it.
    const candidates = contenders.length > 0 ? contenders : pot.eligible;

    let winners = [];
    for (const id of candidates) {
      if (winners.length === 0) {
        winners = [id];
        continue;
      }
      const hand = hands.get(id);
      const diff = hand ? compare(hand, hands.get(winners[0])) : 0;
      if (diff > 0) winners = [id];
      else if (diff === 0) winners.push(id);
    }

    // Order winners by seat so the odd chips go to the one closest to the button.
    winners.sort((a, b) => seatOrderFromButton.indexOf(a) - seatOrderFromButton.indexOf(b));
    const share = Math.floor(pot.amount / winners.length);
    const remainder = pot.amount - share * winners.length;

    return {
      amount: pot.amount,
      winners: winners.map((playerId, i) => ({ playerId, share: share + (i === 0 ? remainder : 0) })),
    };
  });
}

function sameMembers(a, b) {
  return a.length === b.length && a.every((id) => b.includes(id));
}

module.exports = { buildPots, distributePots };
