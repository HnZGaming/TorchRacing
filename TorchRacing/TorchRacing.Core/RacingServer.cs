using System;
using System.Collections.Generic;
using System.Linq;
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
        }

        public void Update()
        {
            foreach (var (_, lobby) in _lobbies)
            {
                lobby.Update();
            }

            _gpss.WriteIfNecessary();
        }

        public void CreateTrack(IMyPlayer player, string raceId)
        {
            if (_db.Contains(raceId))
            {
                throw new Exception($"Race exists: {raceId}");
            }

            var emptyRace = new SerializedRace
            {
                RaceId = raceId,
                OwnerSteamId = player.SteamUserId,
                Checkpoints = new RaceCheckpoint[0],
                CheckpointSafezones = new long[0],
            };

            var lobby = new RacingLobby(_config, _chatManager, _gpss, emptyRace);
            _lobbies[raceId] = lobby;
        }

        public void AddCheckpoint(IMyPlayer player, float radius, bool useSafezone)
        {
            var lobby = GetLobbyOfPlayerOrThrow(player.SteamUserId);
            lobby.AddCheckpoint(player, radius, useSafezone);
            WriteToDb();
        }

        public void RemoveCheckpoint(IMyPlayer player)
        {
            var lobby = GetLobbyOfPlayerOrThrow(player.SteamUserId);
            lobby.RemoveCheckpoint(player);
            WriteToDb();
        }

        public void RemoveAllCheckpoints(IMyPlayer player)
        {
            var lobby = GetLobbyOfPlayerOrThrow(player.SteamUserId);
            lobby.RemoveAllCheckpoints(player);
            WriteToDb();
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
            await lobby.Start(player.SteamUserId, lapCount);
        }

        public void ResetRace(IMyPlayer player)
        {
            var lobby = GetLobbyOfPlayerOrThrow(player.SteamUserId);
            lobby.Reset(player.SteamUserId);
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
    }
}