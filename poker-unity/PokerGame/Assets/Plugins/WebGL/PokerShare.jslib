// Browser helpers for Poker.Core.ShareUtil (WebGL builds only).
mergeInto(LibraryManager.library, {
  PokerCopyText: function (textPtr) {
    var text = UTF8ToString(textPtr);
    function fallback() {
      var area = document.createElement('textarea');
      area.value = text;
      area.style.position = 'fixed';
      area.style.opacity = '0';
      document.body.appendChild(area);
      area.select();
      try { document.execCommand('copy'); } catch (e) {}
      document.body.removeChild(area);
    }
    if (navigator.clipboard && window.isSecureContext) navigator.clipboard.writeText(text).catch(fallback);
    else fallback();
  },

  // Returns 1 if the share sheet was opened, 0 if the browser has none.
  PokerShareText: function (titlePtr, textPtr, urlPtr) {
    var data = { title: UTF8ToString(titlePtr), text: UTF8ToString(textPtr) };
    var url = UTF8ToString(urlPtr);
    if (url) data.url = url;
    if (!navigator.share || (navigator.canShare && !navigator.canShare(data))) return 0;
    navigator.share(data).catch(function () {}); // closing the sheet is not an error
    return 1;
  },

  PokerRemoveUrlParam: function (keyPtr) {
    try {
      var url = new URL(window.location.href);
      url.searchParams.delete(UTF8ToString(keyPtr));
      window.history.replaceState(null, '', url.toString());
    } catch (e) {}
  },
});
