// Phone keyboard for TMP input fields in WebGL builds (see Poker.Platform.MobileKeyboardBridge).
// The page template (WebGLTemplates/PokerTemplate) defines window.pokerOpenKeyboard.
mergeInto(LibraryManager.library, {
  PokerKeyboardIsTouch: function () {
    return window.pokerIsTouch ? 1 : 0;
  },

  PokerKeyboardOpen: function (goNamePtr, textPtr, labelPtr, secret, numeric, maxLength) {
    if (!window.pokerOpenKeyboard) return;
    window.pokerOpenKeyboard(UTF8ToString(goNamePtr), UTF8ToString(textPtr), UTF8ToString(labelPtr),
      secret === 1, numeric === 1, maxLength);
  },
});
