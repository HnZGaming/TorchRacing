using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using Utils.General;
using VRage.Game.ModAPI;
using VRageMath;

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
        readonly StupidDb<SerializedRace> _db;
        readonly List<RaceCheckpoint> _checkpoints;
        readonly RaceSafeZoneCollection _safezones;
        Race _race;

        public RacingServer(IConfig config, string dbPath)
        {
            _config = config;
            _db = new StupidDb<SerializedRace>(dbPath);
            _checkpoints = new List<RaceCheckpoint>();
            _safezones = new RaceSafeZoneCollection(config);
        }

        public void Initialize()
        {
            _db.Read();

            if (_db.TryQuery(DefaultRaceId, out var race))
            {
                for (var i = 0; i < race.Checkpoints.Length; i++)
                {
                    var checkpoint = race.Checkpoints[i];
                    _checkpoints.Add(checkpoint);

                    var safezone = race.CheckpointSafezones[i];
                    _safezones.FindOrCreateAndAdd(safezone, checkpoint.Position, checkpoint.Radius);
                }
            }
        }

        public void Dispose()
        {
            _race?.Dispose();
        }

        public void Update()
        {
            _safezones.Update();
            _race?.Update();
        }

        public void AddCheckpoint(IMyPlayer player, float radius)
        {
            var position = player.GetPosition();
            var checkpoint = new RaceCheckpoint(position, radius);
            _checkpoints.Add(checkpoint);
            _safezones.CreateAndAdd(position, radius);

            WriteToDb();
        }

        public void RemoveCheckpoint(IMyPlayer player)
        {
            if (!TryGetNearestCheckpoint(player.GetPosition(), out var checkpointIndex))
            {
                throw new Exception("No checkpoints found in the range");
            }

            _checkpoints.RemoveAt(checkpointIndex);
            _safezones.RemoveAt(checkpointIndex);

            WriteToDb();
        }

        bool TryGetNearestCheckpoint(Vector3D position, out int nearestCheckpointIndex)
        {
            nearestCheckpointIndex = -1;

            if (!_checkpoints.Any()) return false;

            var minDistance = double.MaxValue;
            for (var i = 0; i < _checkpoints.Count; i++)
            {
                var checkpoint = _checkpoints[i];
                var checkpointPos = checkpoint.Position;
                var distance = Vector3D.Distance(position, checkpointPos);
                if (distance > _config.SearchRadius) continue;

                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestCheckpointIndex = i;
                }
            }

            return nearestCheckpointIndex >= 0;
        }

        public void RemoveAllCheckpoints()
        {
            _checkpoints.Clear();
            _safezones.Clear();

            WriteToDb();
        }

        void WriteToDb()
        {
            _db.Clear();
            _db.Insert(new SerializedRace
            {
                RaceId = DefaultRaceId,
                Checkpoints = _checkpoints.ToArray(),
                CheckpointSafezones = _safezones.GetAllNames().ToArray(),
            });
            _db.Write();
        }

        public void InitializeRace(IMyPlayer player, int lapCount)
        {
            _race?.Dispose();
            _race = new Race(_checkpoints, player.IdentityId, lapCount);
            _race.AddRacer(player);
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

        public async Task StartRace(IMyPlayer player, int countdown)
        {
            _race.ThrowIfNull("race not initialized");
            await _race.Start(player.IdentityId, countdown);
        }

        public void EndRace(IMyPlayer player)
        {
            _race.ThrowIfNull("race not initialized");
            _race.End(player.IdentityId);
        }

        public void CancelRace(IMyPlayer player)
        {
            _race.ThrowIfNull("race not initialized");
            _race.Cancel(player.IdentityId);
        }

        public void ResetRace(IMyPlayer player)
        {
            _race.ThrowIfNull("race not initialized");
            _race.Reset(player.IdentityId);
        }

        public override string ToString()
        {
            var builder = new StringBuilder();

            builder.Append("Checkpoints: ");
            builder.AppendLine();
            foreach (var checkpoint in _checkpoints)
            {
                builder.Append(checkpoint);
                builder.AppendLine();
            }

            builder.Append(_race?.ToString() ?? "Not initialized");
            return builder.ToString();
        }
    }
}