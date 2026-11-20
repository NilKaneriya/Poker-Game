const { evaluate } = require('../engine/handEvaluator');
const { rankValue, suitOf } = require('../engine/cards');
const { ACTIONS } = require('../engine/constants');

/**
 * Computer opponent used to fill tables with fewer than 2 humans.
 * A bot only sees what a human in its seat would see (its own cards + public
 * state) and its moves go through the same rule checks as players' moves.
 *
 * Every level uses the same idea - "how strong is my hand (0..1) vs. how much
 * does it cost to continue" - with different personalities:
 *   easy   - calls almost anything, hardly ever raises, never bluffs
 *   normal - plays by hand strength, folds weak hands to big bets
 *   hard   - tighter calls, raises bigger, bluffs now and then
 */
const STYLES = {
  easy: { callBar: 0.05, raiseBar: 0.92, bluff: 0, raiseSize: 0.5 },
  normal: { callBar: 0.3, raiseBar: 0.72, bluff: 0.04, raiseSize: 0.6 },
  hard: { callBar: 0.4, raiseBar: 0.62, bluff: 0.12, raiseSize: 0.9 },
};
const LEVELS = Object.keys(STYLES);

class BotPlayer {
  constructor({ playerId, name, level = 'normal' }) {
    this.playerId = playerId;
    this.name = name;
    this.level = STYLES[level] ? level : 'normal';
  }

  /**
   * @param {object} view    the bot's own player view (same shape a client gets)
   * @param {object} legal   hand.getLegalActions(playerId)
   * @param {() => number} [random]  injectable for tests
   * @returns {{ action: string, amount?: number }}
   */
  decide(view, legal, random = Math.random) {
    const style = STYLES[this.level];
    const hand = view.hand;
    const can = (action) => legal.actions.includes(action);
    const strength = handStrength(hand.yourCards, hand.board);

    // Strong hand (or a bluff): raise by a share of the pot.
    if ((strength >= style.raiseBar || random() < style.bluff) && can(ACTIONS.RAISE)) {
      const target = legal.minRaiseTo + Math.round(hand.pot * style.raiseSize);
      return { action: ACTIONS.RAISE, amount: Math.min(Math.max(target, legal.minRaiseTo), legal.maxRaiseTo) };
    }

    if (can(ACTIONS.CHECK)) return { action: ACTIONS.CHECK };

    // Facing a bet: the bigger the bet compared to the pot, the stronger the hand must be.
    const price = legal.toCall / (hand.pot + legal.toCall);
    const needed = style.callBar + price * 0.6;
    if (strength >= needed) {
      if (can(ACTIONS.CALL)) return { action: ACTIONS.CALL };
      if (can(ACTIONS.ALL_IN)) return { action: ACTIONS.ALL_IN }; // calling would put us all-in
    }
    return { action: ACTIONS.FOLD };
  }
}

/**
 * Rough hand strength from 0 (hopeless) to 1 (the nuts). Simple on purpose.
 * Pre-flop: pairs and high, suited, connected cards score higher.
 * After the flop: based on the made hand's rank.
 */
function handStrength(holeCards, board) {
  const [a, b] = holeCards.map(rankValue).sort((x, y) => y - x);

  if (board.length === 0) {
    if (a === b) return 0.5 + a / 28; // 22 = 0.57 ... AA = 1.0
    let score = ((a + b) / 30) * 0.7;
    if (suitOf(holeCards[0]) === suitOf(holeCards[1])) score += 0.06;
    if (a - b <= 2) score += 0.04;
    return Math.min(score, 0.9);
  }

  const RANK_STRENGTH = [0.15, 0.42, 0.62, 0.72, 0.8, 0.84, 0.9, 0.96, 0.99, 1];
  const made = evaluate([...holeCards, ...board]);
  let strength = RANK_STRENGTH[made.rank];
  if (made.rank === 0 && a >= 13) strength += 0.1; // ace or king high

  // If the board alone is just as good, our cards add nothing.
  if (board.length === 5 && evaluate(board).rank >= made.rank) strength = Math.min(strength, 0.25);
  return strength;
}

module.exports = { BotPlayer, LEVELS, handStrength };
