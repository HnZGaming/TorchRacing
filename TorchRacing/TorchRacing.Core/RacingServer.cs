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
        public interface IConfig : RaceSafeZoneCollection.IConfig
        {
            double SearchRadius { get; }
        }

        const string DefaultRaceId = "default";
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly IConfig _config;
        readonly RaceGpsCollection _gpss;
        readonly IChatManagerServer _chatManager;
        readonly StupidDb<SerializedRace> _db;
        readonly List<RaceCheckpoint> _checkpoints;
        readonly RaceSafeZoneCollection _safezones;
        RacingLobby _racingLobby;

        public RacingServer(IConfig config, RaceGpsCollection gpss, IChatManagerServer chatManager, string dbPath)
        {
            _config = config;
            _gpss = gpss;
            _chatManager = chatManager;
            _db = new StupidDb<SerializedRace>(dbPath);
            _checkpoints = new List<RaceCheckpoint>();
            _safezones = new RaceSafeZoneCollection(config);
        }

        public void Initialize()
        {
            _racingLobby = new RacingLobby(_chatManager, _gpss, _checkpoints);

            _db.Read();

            if (!_db.TryQuery(DefaultRaceId, out var race)) return;

            for (var i = 0; i < race.Checkpoints.Length; i++)
            {
                var checkpoint = race.Checkpoints[i];
                _checkpoints.Add(checkpoint);

                var safezone = race.CheckpointSafezones[i];
                _safezones.FindOrCreateAndAdd(safezone, checkpoint.Position, checkpoint.Radius);
            }

            WriteToDb();
        }

        public void Update()
        {
            _racingLobby?.Update();
            _gpss.WriteIfNecessary();
        }

        public void AddCheckpoint(IMyPlayer player, float radius, bool useSafezone)
        {
            var position = player.GetPosition();
            var checkpoint = new RaceCheckpoint(position, radius);
            _checkpoints.Add(checkpoint);
            _safezones.CreateAndAdd(position, radius, useSafezone);

            WriteToDb();
        }

        public void RemoveCheckpoint(IMyPlayer player)
        {
            var position = player.GetPosition();
            var maxRadius = _config.SearchRadius;
            if (!_checkpoints.TryGetNearestPositionIndex(position, maxRadius, out var checkpointIndex))
            {
                throw new Exception("No checkpoints found in the range");
            }

            _checkpoints.RemoveAt(checkpointIndex);
            _safezones.RemoveAt(checkpointIndex);

            WriteToDb();
        }

        public void RemoveAllCheckpoints()
        {
            _checkpoints.Clear();
            _safezones.Clear();

            WriteToDb();
        }

        void WriteToDb()
        {
            var serializedRace = new SerializedRace
            {
                RaceId = DefaultRaceId,
                Checkpoints = _checkpoints.ToArray(),
                CheckpointSafezones = _safezones.GetSafezoneIds().ToArray(),
            };

            _db.Clear();
            _db.Insert(serializedRace);
            _db.Write();
        }

        public void JoinRace(IMyPlayer player)
        {
            _racingLobby.ThrowIfNull("race not initialized");
            _racingLobby.AddRacer(player);
        }

        public void ExitRace(IMyPlayer player)
        {
            _racingLobby.ThrowIfNull("race not initialized");
            _racingLobby.RemoveRacer(player);
        }

        public async Task StartRace(IMyPlayer player, int lapCount)
        {
            _racingLobby.ThrowIfNull("race not initialized");
            await _racingLobby.Start(player.SteamUserId, lapCount);
        }

        public void ResetRace(IMyPlayer player)
        {
            _racingLobby.ThrowIfNull("race not initialized");
            _racingLobby.Reset(player.SteamUserId);
        }

        public bool TryGetLobbyOfPlayer(ulong steamId, out RacingLobby lobby)
        {
            if (_racingLobby.ContainsPlayer(steamId))
            {
                lobby = _racingLobby;
                return true;
            }

            lobby = null;
            return false;
        }
    }
}