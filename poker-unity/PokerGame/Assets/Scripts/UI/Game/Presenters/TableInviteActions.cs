using Poker.Platform;
using static Poker.Core.Constants;
using static Poker.Network.DataModel;

namespace Poker.UI.Game
{
    /// <summary>The table's invite buttons: copy the room code, copy the invite link, share the invite.</summary>
    public class TableInviteActions
    {
        private readonly ToastView toast;

        public TableInviteActions(ToastView toast) => this.toast = toast;

        public void CopyCode(TableState table)
        {
            if (table == null) return;
            ShareService.Copy(table.code);
            toast.Show(string.Format(GameTexts.CodeCopied, table.code));
        }

        public void CopyLink(TableState table)
        {
            if (table == null) return;
            TableInvite invite = InviteLinkService.CreateInvite(table);
            if (invite.Link == null) { CopyCode(table); return; }
            ShareService.Copy(invite.Link);
            toast.Show(GameTexts.LinkCopied);
        }

        public void Share(TableState table)
        {
            if (table == null) return;
            TableInvite invite = InviteLinkService.CreateInvite(table);
            bool sheetOpened = ShareService.Share(GameTexts.InviteTitle, invite.Message, invite.Link);
            if (!sheetOpened) toast.Show(GameTexts.InviteCopied);
        }
    }
}
