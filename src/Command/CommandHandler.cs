using Eleon;
using Eleon.Modding;
using GalacticWaez.Navigation;
using static System.Net.Mime.MediaTypeNames;
using System.Runtime.Remoting.Channels;

namespace GalacticWaez.Command
{
    public class CommandHandler
    {
        public enum State
        {
            Uninitialized,
            Initializing,
            Ready,
            Busy
        }

        private readonly IModApi modApi;
        private readonly SaveGameDB saveGameDB;
        private Galaxy galaxy = null;

        private State status;

        public State Status { get => status; }

        public CommandHandler(IModApi modApi)
        {
            this.modApi = modApi;
            status = State.Uninitialized;
            saveGameDB = new SaveGameDB(modApi);
        }

        public void HandleChatCommand(MessageData messageData)
        {
            if (messageData.Text.StartsWith(CommandToken.Introducer))
            {
                string commandText = messageData.Text.Remove(0, CommandToken.Introducer.Length).Trim();
                if (commandText.Equals(CommandToken.Init))
                {
                    Initialize(messageData);
                    return;
                }
                if (commandText.Equals(CommandToken.GetStatus))
                {
                    HandleStatusRequest(messageData);
                    return;
                }
                if (commandText.Equals(CommandToken.Help))
                {
                    HandleHelpRequest(messageData);
                    return;
                }
                if (commandText.Equals(CommandToken.Clear))
                {
                    HandleClearRequest(messageData);
                    return;
                }
                if (commandText.Equals(CommandToken.Restart))
                {
                    HandleRestartRequest(messageData);
                    return;
                }
                string[] tokens = commandText.Split(separator: new[] { ' ' }, count: 2);
                if (tokens.Length == 2 && tokens[0].Equals(CommandToken.To))
                {
                    HandleNavRequest(messageData, tokens[1]);
                    return;
                }
                modApi.Application.SendChatMessage(
                    new MessageData()
                    {
                        SenderType          = SenderType.System,
                        SenderNameOverride  = "Waez",
                        Channel             = MsgChannel.Global,
                        RecipientEntityId   = messageData.SenderEntityId,
                        RecipientFaction    = messageData.SenderFaction,
                        Text                = "Invalid Command",
                        IsTextLocaKey       = false,
                    });
            }
        }

        private void HandleStatusRequest(MessageData messageData)
        {
            string message = status.ToString();
            modApi.Application.SendChatMessage(ChatMessage.Create(message, messageData));
        }

        private const string HelpText = "Waez commands:\n"
            + "to [mapmarker]: plot a course to [mapmarker] and add mapmarkers for each step\n"
            + "status: find out what Waez is up to\n"
            + "init: initialize Waez. this should happen automatically\n"
            + "clear: remove all map markers that start with Waez_\n"
            + "help: get this help message\n";

        private void HandleHelpRequest(MessageData messageData) => modApi.Application
            .SendChatMessage(ChatMessage.Create(HelpText, messageData));

        private void HandleClearRequest(MessageData messageData)
        {
            string message = $"Removed "
                + saveGameDB.ClearPathMarkers(messageData.SenderEntityId)
                + " map markers.";
            modApi.Application.SendChatMessage(ChatMessage.Create(message, messageData));
        }

        public void Initialize(MessageData messageData)
        {
            if (status != State.Uninitialized)
            {
                string message = "Cannot init because Waez is " + status.ToString();
                modApi.Application.SendChatMessage(ChatMessage.Create(message, messageData));
                return;
            }
            DoInit(messageData);
        }

        private void HandleRestartRequest(MessageData messageData)
        {
            if (status != State.Ready)
            {
                if(messageData != null) modApi.Application.SendChatMessage(ChatMessage.Create($"Cannot restart because Waez is {status}", messageData));
                return;
            }
            DoInit(messageData);
        }

        private void DoInit(MessageData messageData)
        {
            status = State.Initializing;
            new Initializer(modApi).Initialize((galaxy, response) =>
            {
                this.galaxy = galaxy;
                status = State.Ready;
                modApi.Log(response);
                if(messageData != null) modApi.Application.SendChatMessage(ChatMessage.Create("Waez is ready.", messageData));
            });
        }

        private void HandleNavRequest(MessageData messageData, string bookmarkName)
        {
            if (status != State.Ready)
            {
                string message = "Unable: Waez is " + status.ToString();
                modApi.Application.SendChatMessage(ChatMessage.Create(message, messageData));
                return;
            }
            status = State.Busy;
            new Navigator(modApi, galaxy)
                .HandlePathRequest(bookmarkName, messageData.SenderFaction.Id, messageData.SenderEntityId,
                response =>
                {
                    status = State.Ready;
                    modApi.Application.SendChatMessage(ChatMessage.Create(response, messageData));
                });
        }
    }
}
