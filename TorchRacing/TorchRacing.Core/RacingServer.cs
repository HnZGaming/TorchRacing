using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using Torch.API.Managers;
using Utils.General;
using VRage.Game.ModAPI;

namespace TorchRacing.Core
{
    public sealed class RacingServer
    {
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly RacingLobby.IConfig _config;
        readonly RaceGpsCollection _gpss;
        readonly IChatManagerServer _chatManager;
        readonly StupidDb<SerializedRace> _db;
        readonly Dictionary<string, RacingLobby> _lobbies;

        public RacingServer(RacingLobby.IConfig config, RaceGpsCollection gpss, IChatManagerServer chatManager, string dbPath)
        {
            _config = config;
            _gpss = gpss;
            _chatManager = chatManager;
            _db = new StupidDb<SerializedRace>(dbPath);
            _lobbies = new Dictionary<string, RacingLobby>();
        }

        public void Initialize()
        {
            _db.Read();

            foreach (var race in _db.QueryAll())
            {
                var lobby = new RacingLobby(_config, _chatManager, _gpss, race);
                _lobbies[race.RaceId] = lobby;
            }
        }

        public void Update()
        {
            foreach (var (_, lobby) in _lobbies)
            {
                lobby.Update();
            }

            _gpss.WriteIfNecessary();
        }

        public void AddTrack(IMyPlayer player, string raceId)
        {
            if (_db.Contains(raceId))
            {
                throw new Exception($"Race exists: {raceId}");
            }

            var emptyRace = SerializedRace.Make(raceId, player.SteamUserId);
            var lobby = new RacingLobby(_config, _chatManager, _gpss, emptyRace);
            _lobbies[raceId] = lobby;

            lobby.AddRacer(player);

            WriteToDb();
        }

        public void DeleteTrack(IMyPlayer player)
        {
            var lobby = GetLobbyOfPlayerOrThrow(player.SteamUserId);
            
            lobby.Clear(player.SteamUserId);
            _lobbies.Remove(lobby.RaceId);
        }

        public void AddCheckpoint(IMyPlayer player, float radius, bool useSafezone)
        {
            var lobby = GetLobbyOfPlayerOrThrow(player.SteamUserId);
            lobby.AddCheckpoint(player, radius, useSafezone);
            WriteToDb();
        }

        public void ReplaceCheckpoint(IMyPlayer player, int index, float radius, bool useSafezone)
        {
            var lobby = GetLobbyOfPlayerOrThrow(player.SteamUserId);
            lobby.ReplaceCheckpoint(player, index, radius, useSafezone);
            WriteToDb();
        }

        public void DeleteCheckpoint(IMyPlayer player)
        {
            var lobby = GetLobbyOfPlayerOrThrow(player.SteamUserId);
            lobby.DeleteCheckpoint(player);
            WriteToDb();
        }

        public void DeleteAllCheckpoints(IMyPlayer player)
        {
            var lobby = GetLobbyOfPlayerOrThrow(player.SteamUserId);
            lobby.DeleteAllCheckpoints(player.SteamUserId);
            WriteToDb();
        }

        public void ShowAllCheckpoints(IMyPlayer player)
        {
            var lobby = GetLobbyOfPlayerOrThrow(player.SteamUserId);
            lobby.ShowAllCheckpoints(player.IdentityId);
        }

        void WriteToDb()
        {
            _db.Clear();

            foreach (var (_, race) in _lobbies)
            {
                var serializedRace = race.Serialize();
                _db.Insert(serializedRace);
            }

            _db.Write();
        }

        public void JoinRace(IMyPlayer player, string raceId)
        {
            if (TryGetLobbyOfPlayer(player.SteamUserId, out _))
            {
                throw new Exception("Already in a lobby");
            }

            var lobby = GetLobbyOrThrow(raceId);
            lobby.AddRacer(player);
        }

        public void ExitRace(IMyPlayer player)
        {
            var lobby = GetLobbyOfPlayerOrThrow(player.SteamUserId);
            lobby.RemoveRacer(player);
        }

        public async Task StartRace(IMyPlayer player, int lapCount)
        {
            var lobby = GetLobbyOfPlayerOrThrow(player.SteamUserId);
            await lobby.Start(lapCount);
        }

        public void ResetRace(IMyPlayer player)
        {
            var lobby = GetLobbyOfPlayerOrThrow(player.SteamUserId);
            lobby.Reset();
        }

        RacingLobby GetLobbyOrThrow(string raceId)
        {
            if (!_lobbies.TryGetValue(raceId, out var lobby))
            {
                throw new Exception($"Race not found: {raceId}");
            }

            return lobby;
        }

        public bool TryGetLobbyOfPlayer(ulong steamId, out RacingLobby foundLobby)
        {
            foreach (var (_, lobby) in _lobbies)
            {
                if (lobby.ContainsPlayer(steamId))
                {
                    foundLobby = lobby;
                    return true;
                }
            }

            foundLobby = null;
            return false;
        }

        RacingLobby GetLobbyOfPlayerOrThrow(ulong steamId)
        {
            if (!TryGetLobbyOfPlayer(steamId, out var lobby))
            {
                throw new Exception("Not in a lobby");
            }

            return lobby;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            foreach (var (raceId, lobby) in _lobbies)
            {
                sb.Append(raceId);
                sb.Append(": ");
                sb.Append(lobby.RacerCount);
                sb.Append(" racers");
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}