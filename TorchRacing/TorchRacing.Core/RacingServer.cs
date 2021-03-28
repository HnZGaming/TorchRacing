using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Utils.General;
using VRage.Game.ModAPI;
using VRageMath;

namespace TorchRacing.Core
{
    public sealed class RacingServer : IDisposable
    {
        public interface IConfig
        {
            bool EnableSafeZones { get; }
            double SearchRadius { get; }
        }

        const string DefaultRaceId = "default";
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly IConfig _config;
        readonly StupidDb<SerializedRace> _db;
        readonly List<RaceCheckpoint> _checkpoints;
        readonly List<MySafeZone> _checkpointSafezones;
        Race _race;

        public RacingServer(IConfig config, StupidDb<SerializedRace> db)
        {
            _config = config;
            _db = db;
            _checkpoints = new List<RaceCheckpoint>();
            _checkpointSafezones = new List<MySafeZone>();
        }

        public void Initialize()
        {
            _db.Read();
            if (_db.TryQuery(DefaultRaceId, out var race))
            {
                _checkpoints.AddRange(race.Checkpoints);
            }
        }

        public void Dispose()
        {
            _race?.Dispose();
        }

        public void Update()
        {
            foreach (var safezone in _checkpointSafezones)
            {
                safezone.Enabled = _config.EnableSafeZones;
            }

            _race?.Update();
        }

        public void AddCheckpoint(IMyPlayer player, float radius)
        {
            var playerPos = player.GetPosition();
            var checkpoint = new RaceCheckpoint(playerPos, radius);
            _checkpoints.Add(checkpoint);

            var safezone = new MySafeZone();
            safezone.PositionComp.SetPosition(playerPos);
            safezone.Radius = radius;
            safezone.Shape = MySafeZoneShape.Sphere;
            safezone.AccessTypeFactions = MySafeZoneAccess.Blacklist;
            safezone.AccessTypeGrids = MySafeZoneAccess.Blacklist;
            safezone.AccessTypePlayers = MySafeZoneAccess.Blacklist;
            safezone.AccessTypeFloatingObjects = MySafeZoneAccess.Blacklist;

            _checkpointSafezones.Add(safezone);
            MySessionComponentSafeZones.AddSafeZone(safezone);

            UpdateDb();
        }

        public void RemoveCheckpoint(IMyPlayer player)
        {
            if (!TryGetNearestCheckpoint(player.GetPosition(), out var checkpointIndex))
            {
                throw new Exception("No checkpoints found in the range");
            }

            _checkpoints.RemoveAt(checkpointIndex);

            var removedSafezone = _checkpointSafezones[checkpointIndex];
            MySessionComponentSafeZones.RemoveSafeZone(removedSafezone);
            removedSafezone.Close();

            UpdateDb();
        }

        void UpdateDb()
        {
            _db.Clear();
            _db.Insert(new SerializedRace
            {
                RaceId = DefaultRaceId,
                Checkpoints = _checkpoints.ToArray(),
            });
            _db.Write();
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
            foreach (var safezone in _checkpointSafezones)
            {
                MySessionComponentSafeZones.RemoveSafeZone(safezone);
                safezone.Close();
            }
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

        public override string ToString()
        {
            return _race?.ToString() ?? "Not initialized";
        }
    }
}