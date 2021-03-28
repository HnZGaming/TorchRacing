using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Torch.Utils;
using Utils.General;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Components;
using VRageMath;

namespace TorchRacing.Core
{
    public sealed class RacingServer : IDisposable
    {
        public interface IConfig
        {
            bool AllowActionsInSafeZone { get; }
            string SafeZoneColor { get; }
            string SafeZoneTexture { get; }
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

            if (!_db.TryQuery(DefaultRaceId, out var race)) return;

            _checkpoints.AddRange(race.Checkpoints);

            //gather all the safe zones
            for (var i = 0; i < _checkpoints.Count; i++)
            {
                var checkpoint = _checkpoints[i];
                var safezoneName = checkpoint.SafeZoneName ?? "";
                if (!MyEntities.TryGetEntityByName<MySafeZone>(safezoneName, out var safezone))
                {
                    Log.Warn("Safe zone not found for checkpoint");
                    safezone = CreateSafeZone(checkpoint.Position, checkpoint.Radius);
                    checkpoint.SafeZoneName = safezone.Name;
                }

                _checkpointSafezones.AddOrInsert(safezone, i);
            }

            WriteToDb();
        }

        public void Dispose()
        {
            _race?.Dispose();
        }

        public void Update()
        {
            foreach (var safezone in _checkpointSafezones)
            {
                var lastAllowedActions = safezone.AllowedActions;
                safezone.AllowedActions = _config.AllowActionsInSafeZone ? MySafeZoneAction.All : 0;
                var safezoneAllowedActionsChanged = safezone.AllowedActions != lastAllowedActions;

                if (safezoneAllowedActionsChanged)
                {
                    var builder = (MyObjectBuilder_SafeZone) safezone.GetObjectBuilder();
                    MySessionComponentSafeZones.RequestUpdateSafeZone(builder);
                    Log.Info("safe zone updated");
                }
            }

            _race?.Update();
        }

        public void AddCheckpoint(IMyPlayer player, float radius)
        {
            var playerPos = player.GetPosition();
            var safezone = CreateSafeZone(playerPos, radius);
            var checkpoint = new RaceCheckpoint(playerPos, radius, safezone.Name);
            _checkpoints.Add(checkpoint);
            _checkpointSafezones.Add(safezone);

            WriteToDb();
        }

        MySafeZone CreateSafeZone(Vector3D playerPos, float radius)
        {
            return (MySafeZone) MySessionComponentSafeZones.CrateSafeZone(
                MatrixD.CreateWorld(playerPos),
                MySafeZoneShape.Sphere,
                MySafeZoneAccess.Blacklist,
                null, null, radius, true,
                color: ColorUtils.TranslateColor(_config.SafeZoneColor),
                visualTexture: _config.SafeZoneTexture ?? "");
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
            MyEntities.Remove(removedSafezone);

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
            foreach (var removedSafezone in _checkpointSafezones)
            {
                MySessionComponentSafeZones.RemoveSafeZone(removedSafezone);
                removedSafezone.Close();
                MyEntities.Remove(removedSafezone);
            }

            WriteToDb();
        }

        void WriteToDb()
        {
            _db.Clear();
            _db.Insert(new SerializedRace
            {
                RaceId = DefaultRaceId,
                Checkpoints = _checkpoints.ToArray(),
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