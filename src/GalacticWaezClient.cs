using Eleon.Modding;
using GalacticWaez.Command;

namespace GalacticWaez
{
    public class GalacticWaezClient : IMod
    {
        IModApi modApi;
        CommandHandler commandHandler = null;

        public void Init(IModApi modApi)
        {
            this.modApi = modApi;
            modApi.GameEvent += OnGameEvent;
            modApi.Log("GalacticWaezClient attached.");
            InitCommandHandler();
            //modApi.Application.GameEntered += Application_GameEntered;
        }

        private void Application_GameEntered(bool hasEntered)
        {
            InitCommandHandler();
        }

        private void InitCommandHandler()
        {
            if (commandHandler != null) return;

            commandHandler = new CommandHandler(modApi);

            commandHandler.Initialize(null);
            modApi.Application.ChatMessageSent += commandHandler.HandleChatCommand;
            modApi.Log("Listening for commands.");
        }

        public void Shutdown()
        {
            modApi.GameEvent -= OnGameEvent;
            modApi.Log("GalacticWaezClient detached.");
        }

        void OnGameEvent(GameEventType type,
                        object arg1 = null,
                        object arg2 = null,
                        object arg3 = null,
                        object arg4 = null,
                        object arg5 = null
        ) {
            switch (type)
            {
                case GameEventType.GameStarted:
                    //if (modApi.Application.Mode == ApplicationMode.SinglePlayer)
                    //{
                        commandHandler = new CommandHandler(modApi);
                        modApi.Application.ChatMessageSent += commandHandler.HandleChatCommand;
                        modApi.Application.Update += OnWorldVisibleOnce;
                        modApi.Log("Listening for commands.");
                    //}
                    break;

                case GameEventType.GameEnded:
                    modApi.Application.ChatMessageSent -= commandHandler.HandleChatCommand;
                    modApi.Application.Update -= OnWorldVisibleOnce;
                    commandHandler = null;
                    modApi.Log("Stopped listening for commands.");
                    break;
            }
        }

        void OnWorldVisibleOnce()
        {
            if (modApi.GUI.IsWorldVisible
                && commandHandler.Status.Equals(CommandHandler.State.Uninitialized))
            {
                commandHandler.Initialize(null);
                modApi.Application.Update -= OnWorldVisibleOnce;
            }
        }
    }
}
