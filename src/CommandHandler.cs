﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eleon;
using Eleon.Modding;
using static GalacticWaez.CommandToken;

namespace GalacticWaez
{
    class CommandToken
    {
        public const string Introducer = "/waez";
        public const string Init = "init";
    }

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
        IModApi modApi;
        Task<InitializationResult> task = null;

        public CommandHandler(IModApi modApi)
        {
            this.modApi = modApi;
        }

        public void HandleChatCommand(MessageData messageData)
        {
            if (messageData.Text.StartsWith(Introducer))
            {
                string commandText = messageData.Text.Remove(0, Introducer.Length).Trim();
                if (commandText.Equals(Init))
                {
                    HandleInit();
                }
            }
        }

        /***********************************************************************
         *
         * Initialization/Map-Building stuff
         *
         **********************************************************************/

        void HandleInit()
        {
            task = Task<InitializationResult>.Factory.StartNew(BuildGalaxyMap);
            modApi.Application.Update += OnUpdateDuringInit;
        }

        InitializationResult BuildGalaxyMap()
        {
            var db = new SaveGameDB(modApi);
            var playerData = db.GetPlayerData();
            var stopWatch = Stopwatch.StartNew();
            var starPositions = new StarFinder(db.GetFirstKnownStarPosition()).Search();
            var galaxy = Galaxy.CreateNew(starPositions, playerData.WarpRange);
            stopWatch.Stop();

            // surely this won't take so long we actually lose data with this downcast :P
            return new InitializationResult(galaxy, (int)stopWatch.ElapsedMilliseconds);
        }

        void OnUpdateDuringInit()
        {
            if (task.IsCompleted)
            {
                var result = task.Result;
                modApi.Log("Constructing galactic highway map "
                        + $"({result.galaxy.StarCount} stars and "
                        + $"{result.galaxy.WarpLines} warp lines) "
                        + $"took {(float)result.elapsedMillis / 1000,0:F3}s.");
                modApi.Application.Update -= OnUpdateDuringInit;
                modApi.GUI.ShowGameMessage("Waez ready.");
            }
        }
    }
}
