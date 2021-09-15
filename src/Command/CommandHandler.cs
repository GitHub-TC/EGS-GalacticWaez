﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eleon;
using Eleon.Modding;
using SectorCoordinates = Eleon.Modding.VectorInt3;

namespace GalacticWaez.Command
{
    struct InitializationResult
    {
        // TODO: replace with galaxy class when written
        public readonly Galaxy galaxy;
        public readonly int elapsedMillis;
        public InitializationResult(Galaxy galaxy, int millis)
        {
            this.galaxy = galaxy;
            elapsedMillis = millis;
        }
    }

    class CommandHandler
    {
        public enum State
        {
            Uninitialized,
            Initializing,
            Ready,
            Busy
        }

        IModApi modApi;
        SaveGameDB saveGameDB;
        Task<InitializationResult> initializer = null;
        Task<string> navigator = null;
        Galaxy galaxy = null;

        private State status;
        private PlayerData localPlayerData;

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
                    Initialize();
                    return;
                }
                if (commandText.Equals(CommandToken.GetStatus))
                {
                    HandleStatusRequest();
                    return;
                }
                if (commandText.Equals(CommandToken.Help))
                {
                    HandleHelpRequest();
                    return;
                }
                if (commandText.Equals(CommandToken.Clear))
                {
                    HandleClearRequest();
                    return;
                }
                string[] tokens = commandText.Split(separator: new[] { ' ' }, count: 2);
                if (tokens.Length == 2 && tokens[0].Equals(CommandToken.To))
                {
                    HandleNavRequest(tokens[1]);
                    return;
                }
                modApi.Application.SendChatMessage(new ChatMessage("Invalid Command", localPlayerData.Entity));
            }
        }

        void HandleStatusRequest()
        {
            string message = status.ToString();
            modApi.Application.SendChatMessage(new ChatMessage(message, localPlayerData.Entity));
        }

        const string HelpText = "Waez commands:\n"
            + "to [mapmarker]: plot a course to [mapmarker] and add mapmarkers for each step\n"
            + "status: find out what Waez is up to\n"
            + "init: initialize Waez. this should happen automatically\n"
            + "clear: remove all map markers that start with Waez_\n"
            + "help: get this help message\n";

        void HandleHelpRequest()
        {
            modApi.Application.SendChatMessage(new ChatMessage(HelpText, localPlayerData.Entity));
        }

        void HandleClearRequest()
        {
            string message = $"Removed "
                + saveGameDB.ClearPathMarkers(localPlayerData.Entity.Id)
                + " map markers.";
            modApi.Application.SendChatMessage(new ChatMessage(message, localPlayerData.Entity));
        }

        /***********************************************************************
         *
         * Initialization/Map-Building stuff
         *
         **********************************************************************/

        public void Initialize()
        {
            if (status == State.Uninitialized)
            {
                status = State.Initializing;
                initializer = Task<InitializationResult>.Factory.StartNew(BuildGalaxyMap);
                modApi.Application.Update += OnUpdateDuringInit;
            }
            else
            {
                string message = "Cannot init because Waez is " + status.ToString();
                modApi.Application.SendChatMessage(new ChatMessage(message, localPlayerData.Entity));
            }
        }

        InitializationResult BuildGalaxyMap()
        {
            try
            {
                localPlayerData = saveGameDB.GetPlayerData();
                var stopWatch = Stopwatch.StartNew();
                var starPositions = new StarFinder(saveGameDB.GetFirstKnownStarPosition()).Search();
                var galaxy = Galaxy.CreateNew(starPositions, localPlayerData.WarpRange);
                stopWatch.Stop();
                
                // surely this won't take so long we actually lose data with this downcast :P
                return new InitializationResult(galaxy, (int)stopWatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                modApi.LogError(ex.Message);
                status = State.Uninitialized;
            }

            return default;
        }

        void OnUpdateDuringInit()
        {
            if (initializer.IsCompleted)
            {
                modApi.Application.Update -= OnUpdateDuringInit;
                galaxy = initializer.Result.galaxy;
                modApi.Log("Constructing galactic highway map "
                        + $"({initializer.Result.galaxy.StarCount} stars and "
                        + $"{initializer.Result.galaxy.WarpLines} warp lines) "
                        + $"took {(float)initializer.Result.elapsedMillis / 1000,0:F3}s.");
                status = State.Ready;
                if (modApi.GUI.IsWorldVisible)
                {
                    modApi.GUI.ShowGameMessage("Waez ready.");
                }
            }
        }

        /***********************************************************************
         *
         * Pathfinding and Bookmarks
         *
         * The navigation command "/waez to ..." gets handled here.
         * HandleNavRequest gets called by the interpreter, starts another
         * thread to execute NavigateTo, and adds OnUpdateDuringNavigation to
         * the Application.Update delegate to poll that other thread every frame
         * til it's done.
         * 
         * DO NOT call NavigateTo or OnUpdateDuringNavigation
         * I think this section needs to be its own class.
         *
         **********************************************************************/

        void HandleNavRequest(string bookmarkName)
        {
            if (status != State.Ready)
            {
                string message = "Unable: Waez is " + status.ToString();
                modApi.Application.SendChatMessage(new ChatMessage(message, localPlayerData.Entity));
                return;
            }
            status = State.Busy;
            navigator = Task<string>.Factory.StartNew(function: NavigateTo, state: bookmarkName);
            modApi.Application.Update += OnUpdateDuringNavigation;
        }

        string NavigateTo(Object state)
        {
            // you have no idea how happy i am not to have to fight the game
            // to get these coordinates :D yaaaaaaaaaas!
            var startCoords = new LYCoordinates(
                modApi.ClientPlayfield.SolarSystemCoordinates);

            string bookmarkName = (string)state;
            SectorCoordinates goalSectorCoords;
            if (!saveGameDB.GetBookmarkVector(bookmarkName, out goalSectorCoords))
            { 
                return "I don't see that bookmark.";
            }
            var goalCoords = new LYCoordinates(goalSectorCoords);
            if (goalCoords.Equals(startCoords))
            {
                return "It appears you are already there.";
            }

            var path = AstarPathfinder.FindPath(
                galaxy.GetNode(startCoords),
                galaxy.GetNode(goalCoords));

            if (path == null)
            {
                return "No path found.";
            }
            if (path.Count() == 1)
            {   // should never happen because of the check up there ^^
                return "It appears you are already there.";
            }
            if (path.Count() == 2)
            {
                return "It appears you are already in warp range.";
            }

            var sectorCoords = new List<SectorCoordinates>(path.Count() - 1);
            foreach (var coord in path.Skip(1).Take(path.Count() - 2))
            {
                sectorCoords.Add(coord.ToSectorCoordinates());
            }

            int steps = saveGameDB.InsertBookmarks(sectorCoords, localPlayerData);

            var message = new StringBuilder();
            message.AppendLine("Found path:");
            foreach (var coord in path.Skip(1))
            {
                message.AppendLine(coord.ToString());
            }

            if (steps == sectorCoords.Count)
            {
                message.Append($"Added {steps} bookmarks to Galaxy Map.");
            }
            else
            {
                message.Append("Failed to add some or all bookmarks.");
            }

            return message.ToString();
        }

        void OnUpdateDuringNavigation()
        {
            if (navigator.IsCompleted)
            {
                modApi.Application.Update -= OnUpdateDuringNavigation;
                modApi.Log(navigator.Result);
                status = State.Ready;
                modApi.GUI.ShowGameMessage(navigator.Result);
                modApi.Application.SendChatMessage(new ChatMessage(navigator.Result, localPlayerData.Entity));
            }
        }
    }
}
