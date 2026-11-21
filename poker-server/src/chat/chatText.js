/**
 * Cleans what a player typed before anyone else sees it. Pure functions, no state.
 *
 *  - only strings; control characters and line breaks removed; spaces collapsed
 *  - cut to the maximum length
 *  - a short list of swear words masked with *** (a friendly table, not a full filter)
 */
const BLOCKED_WORDS = ['fuck', 'shit', 'bitch', 'cunt', 'asshole', 'bastard', 'whore', 'slut', 'retard'];
const BLOCKED_RE = new RegExp(String.raw`\b(${BLOCKED_WORDS.join('|')})\w*`, 'gi');

/** @returns {string} the cleaned text ('' if nothing is left to send) */
function cleanChatText(value, maxLength) {
  if (typeof value !== 'string') return '';
  const text = value
    .normalize('NFC')
    .replace(/[\p{Cc}\p{Cf}\u2028\u2029]/gu, ' ') // control/format characters, line breaks
    .replace(/\s+/g, ' ')
    .trim();
  return [...text].slice(0, maxLength).join('').trim() // cut by characters, not UTF-16 halves
    .replace(BLOCKED_RE, (word) => '*'.repeat(word.length));
}

module.exports = { cleanChatText };
