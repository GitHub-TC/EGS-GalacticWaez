﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eleon.Modding;
using SectorCoordinates = Eleon.Modding.VectorInt3;

namespace GalacticWaez.Navigation
{
    public class Navigator
    {
        public delegate void DoneCallback(string message);

        private Task<string> navigation = null;

        public int playerFactionId { get; private set; }

        private int playerId;
        private DoneCallback doneCallback;
        private readonly IModApi modApi;
        private readonly Galaxy galaxy;
        private readonly SaveGameDB db;

        public Navigator(IModApi modApi, Galaxy galaxy)
        {
            this.modApi = modApi;
            this.galaxy = galaxy;
            db = new SaveGameDB(modApi);
        }

        public void HandlePathRequest(string request, int playerFactionId, int playerId, DoneCallback doneCallback)
        {
            this.playerFactionId = playerFactionId;
            this.playerId = playerId;
            this.doneCallback = doneCallback;
            navigation = Task<string>.Factory.StartNew(Navigate, request);
            modApi.Application.Update += OnUpdateDuringNavigation;
        }
        
        private string Navigate(Object obj)
        {
            var startCoords = StartCoords();
            LYCoordinates goalCoords;
            bool goalIsBookmark;
            if (!GoalCoordinates((string)obj, out goalCoords, out goalIsBookmark))
            {
                return "No bookmark or known star by that name.";
            }
            if (goalCoords.Equals(startCoords))
            {
                return "It appears you are already there.";
            }
            float range = new SaveGameDB(modApi).GetLocalPlayerWarpRange();
            var path = GetPath(startCoords, goalCoords, range);
            if (path == null)
            {
                return "No path found.";
            }
            if (path.Count() == 1)
            {
                return "Are you already there?";
            }
            if (path.Count() == 2 && goalIsBookmark)
            {
                return "It appears you are already in warp range.";
            }
            return SetWaypoints(path.Skip(1), goalIsBookmark);
        }

        private string SetWaypoints(IEnumerable<LYCoordinates> path, bool goalIsBookmark)
        {
            if (goalIsBookmark)
            {
                path = path.Take(path.Count() - 1);
            }
            var sectorsPath = path.Select(n => n.ToSectorCoordinates());
            int added = db.InsertBookmarks(sectorsPath, playerFactionId, playerId);
            return $"Path found; {added}/{path.Count()} waypoints added.";
        }

        private IEnumerable<LYCoordinates> GetPath(LYCoordinates start, LYCoordinates goal, float range)
            => AstarPathfinder.FindPath(galaxy.GetNode(start), galaxy.GetNode(goal), range);

        private LYCoordinates StartCoords()
        {
            return new LYCoordinates(modApi.ClientPlayfield.SolarSystemCoordinates);

            var currentPlayfield = modApi.Application.GetPlayerDataFor(playerId).Value.PlayfieldName;
            //return new LYCoordinates(modApi.Application.GetAllPlayfields().First(P => P.PlayfieldName == currentPlayfield)., );
        }

        private bool GoalCoordinates(string goalName, out LYCoordinates goalCoords, out bool isBookmark)
        {
            isBookmark = false;
            SectorCoordinates sc;
            if (db.GetBookmarkVector(goalName, out sc))
            {
                goalCoords = new LYCoordinates(sc);
                isBookmark = true;
                return true;
            }
            if (db.GetSolarSystemCoordinates(goalName, out sc))
            {
                goalCoords = new LYCoordinates(sc);
                return true;
            }
            goalCoords = default;
            return false;
        }

        private void OnUpdateDuringNavigation()
        {
            if (!navigation.IsCompleted) return;

            modApi.Application.Update -= OnUpdateDuringNavigation;
            doneCallback(navigation.Result);
        }
    }
}
