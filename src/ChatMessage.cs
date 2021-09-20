using Eleon;
using Eleon.Modding;

namespace GalacticWaez
{
    public static class ChatMessage
    {
        public static MessageData Create(string message, MessageData playerAsk)
            => new MessageData()
            {
                SenderType = SenderType.System,
                SenderNameOverride = "Waez",
                Channel = MsgChannel.Global,
                RecipientEntityId = playerAsk.SenderEntityId,
                RecipientFaction = playerAsk.SenderFaction,
                Text = message,
                IsTextLocaKey = false,
            };
    }
}
