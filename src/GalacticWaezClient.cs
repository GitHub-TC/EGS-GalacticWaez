﻿using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Mono.Data.Sqlite;
using Eleon;
using Eleon.Modding;

namespace GalacticWaez
{
    public class GalacticWaezClient : IMod
    {
        IModApi modApi;
        string saveGameDir = null;

        public void Init(IModApi modApi)
        {
            this.modApi = modApi;
            modApi.GameEvent += OnGameEvent;
            modApi.Log("GalacticWaezClient attached.");
        }

        public void Shutdown()
        {
            modApi.GameEvent -= OnGameEvent;
            modApi.Application.ChatMessageSent -= OnChatMessageSent;
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
                    if (modApi.Application.Mode == ApplicationMode.SinglePlayer)
                    {
                        saveGameDir = modApi.Application.GetPathFor(AppFolder.SaveGame);
                        modApi.Application.ChatMessageSent += OnChatMessageSent;
                        modApi.Log("Listening for commands.");
                    }
                    break;

                case GameEventType.GameEnded:
                    saveGameDir = null;
                    modApi.Application.ChatMessageSent -= OnChatMessageSent;
                    modApi.Log("Stopped listening for commands.");
                    break;
            }
        }

        void OnChatMessageSent(MessageData messageData)
        {
            if (messageData.Text.StartsWith(CommandToken.Introducer))
            {
                string command = messageData.Text.Substring(CommandToken.Introducer.Length).Trim();
                if (command.Equals(CommandToken.Init))
                {
                    modApi.Log("Initializing galactic highway map...");
                    // TODO: create Initializer class to handle this
                    // and other necessary business for building the map
                    var scsb = new SqliteConnectionStringBuilder();
                    scsb.DataSource = $"{saveGameDir}\\global.db";
                    scsb.Add("Mode", "ReadOnly");
                    SqliteConnection dbConn = new SqliteConnection(scsb.ToString());
                    dbConn.Open();
                    SqliteCommand dbCommand = dbConn.CreateCommand();
                    dbCommand.CommandText = "select sectorx, sectory, sectorz from SolarSystems limit 1;";
                    IDataReader reader = dbCommand.ExecuteReader();
                    reader.Read();
                    var knownPosition = new VectorInt3(reader.GetInt32(0),
                        reader.GetInt32(1), reader.GetInt32(2));
                    reader.Dispose();
                    dbCommand.Dispose();
                    dbConn.Dispose();
                    var finder = new StarFinder(knownPosition);
                    finder.Search();
                    modApi.Log($"Found {finder.StarsFound} stars.");

                }
            }
        }
    }
}
