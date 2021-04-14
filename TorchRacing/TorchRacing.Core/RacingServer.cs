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
    public sealed class RacingServer : IDisposable
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
        Race _race;

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
            _race = new Race(_chatManager, _gpss, _checkpoints, 0, 3);

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

        public void Dispose()
        {
            _race?.Dispose();
        }

        public void Update()
        {
            _race?.Update();
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
            _race.ThrowIfNull("race not initialized");
            _race.AddRacer(player);
        }

        public void ExitRace(IMyPlayer player)
        {
            _race.ThrowIfNull("race not initialized");
            _race.RemoveRacer(player);
        }

        public async Task StartRace(IMyPlayer player)
        {
            _race.ThrowIfNull("race not initialized");
            await _race.Start(player.SteamUserId);
        }

        public void ResetRace(IMyPlayer player)
        {
            _race.ThrowIfNull("race not initialized");
            _race.Reset(player.SteamUserId);
        }

        public string ToString(bool debug)
        {
            var builder = new StringBuilder();

            if (debug)
            {
                builder.Append("Checkpoints: ");
                builder.AppendLine();
                foreach (var checkpoint in _checkpoints)
                {
                    builder.Append(checkpoint);
                    builder.AppendLine();
                }
            }

            builder.Append(_race?.ToString(debug) ?? "Not initialized");
            return builder.ToString();
        }

        public override string ToString()
        {
            return ToString(true);
        }
    }
}