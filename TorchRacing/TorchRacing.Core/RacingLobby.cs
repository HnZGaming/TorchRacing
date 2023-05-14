using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Game.World;
using Torch;
using Torch.API.Managers;
using Utils.General;
using Utils.Torch;
using VRage.Game.ModAPI;
using VRageMath;

namespace TorchRacing.Core
{
    public sealed class RacingLobby
    {
        public interface IConfig : RaceSafeZoneCollection.IConfig
        {
            double SearchRadius { get; }
        }

        readonly IConfig _config;
        readonly RacingBroadcaster _chatManager;
        readonly RaceGpsCollection _gpss;
        readonly List<RaceCheckpoint> _checkpoints;
        readonly RaceSafeZoneCollection _safezones;
        readonly Dictionary<ulong, Racer> _racers;
        readonly List<(ulong, string)> _tmpRemovedRacers;
        readonly ulong _ownerId;
        RacingGame _game;

        public RacingLobby(
            IConfig config,
            IChatManagerServer chatManager,
            RaceGpsCollection gpss,
            SerializedRace serializedRace)
        {
            _checkpoints = new List<RaceCheckpoint>();
            _safezones = new RaceSafeZoneCollection(config);
            _racers = new Dictionary<ulong, Racer>();
            _tmpRemovedRacers = new List<(ulong, string)>();

            _config = config;
            _gpss = gpss;
            RaceId = serializedRace.RaceId;
            _ownerId = serializedRace.OwnerSteamId;
            _chatManager = new RacingBroadcaster(chatManager, RaceId, _racers.Keys);

            for (var i = 0; i < serializedRace.Checkpoints.Length; i++)
            {
                var checkpoint = serializedRace.Checkpoints[i];
                _checkpoints.Add(checkpoint);

                var safezone = serializedRace.CheckpointSafezones[i];
                _safezones.FindOrCreateAndAdd(safezone, checkpoint.Position, checkpoint.Radius);
            }
        }

        public string RaceId { get; }
        public int RacerCount => _racers.Count;

        public SerializedRace Serialize() => new SerializedRace
        {
            RaceId = RaceId,
            Checkpoints = _checkpoints.ToArray(),
            CheckpointSafezones = _safezones.GetSafezoneIds().ToArray(),
        };

        public void AddCheckpoint(IMyPlayer player, float radius, bool useSafezone)
        {
            ThrowIfNotHostOrAdmin(player.SteamUserId);

            var position = player.GetPosition();
            var checkpoint = new RaceCheckpoint(position, radius);
            _checkpoints.Add(checkpoint);
            _safezones.CreateAndAdd(position, radius, useSafezone);

            _chatManager.SendMessage($"Checkpoint <{_checkpoints.Count}> added");
        }

        public void ReplaceCheckpoint(IMyPlayer player, int index, float radius, bool useSafezone)
        {
            ThrowIfNotHostOrAdmin(player.SteamUserId);

            var checkpoint = _checkpoints[index];
            var position = player.GetPosition();
            checkpoint.Position = position;
            checkpoint.Radius = radius;
            _safezones.Replace(index, position, radius, useSafezone);

            var positions = _checkpoints.Select(c => c.Position);
            foreach (var (_, racer) in _racers)
            {
                _gpss.ReplaceGpss(racer.IdentityId, positions);
            }

            _chatManager.SendMessage($"Checkpoint <{index + 1}> replaced");
        }

        public void DeleteCheckpoint(IMyPlayer player)
        {
            ThrowIfNotHostOrAdmin(player.SteamUserId);

            var position = player.GetPosition();
            var maxRadius = _config.SearchRadius;
            if (!_checkpoints.TryGetNearestPositionIndex(position, maxRadius, out var checkpointIndex))
            {
                throw new Exception("No checkpoints found in the range");
            }

            _checkpoints.RemoveAt(checkpointIndex);
            _safezones.RemoveAt(checkpointIndex);

            _chatManager.SendMessage($"Checkpoint <{checkpointIndex + 1}> deleted");
        }

        public void DeleteAllCheckpoints(ulong playerId)
        {
            ThrowIfNotHostOrAdmin(playerId);

            _checkpoints.Clear();
            _safezones.Clear();

            _chatManager.SendMessage($"All checkpoints deleted");
        }

        public void ShowAllCheckpoints(long playerId)
        {
            _gpss.ReplaceGpss(playerId, _checkpoints.Select(p => p.Position));
        }

        public void AddRacer(IMyPlayer player)
        {
            if (_racers.ContainsKey(player.SteamUserId))
            {
                throw new Exception("Already joined the race");
            }

            var racer = new Racer(player);
            _racers[player.SteamUserId] = racer;

            _chatManager.SendMessage($"{player.DisplayName} joined the race!");

            // show gpss to this player

            var gpsPositions = _game == null
                ? _checkpoints.Select(c => c.Position)
                : new[] { _checkpoints[0].Position };

            _gpss.ReplaceGpss(player.IdentityId, gpsPositions);
        }

        public void RemoveRacer(IMyPlayer player)
        {
            if (!_racers.Remove(player.SteamUserId))
            {
                throw new Exception("Not joined the race");
            }

            _chatManager.SendMessage($"{player.DisplayName} left the race");

            _gpss.ReplaceGpss(player.IdentityId, Enumerable.Empty<Vector3D>());
        }

        public void Clear(ulong steamId)
        {
            ThrowIfNotHostOrAdmin(steamId);

            Reset();

            _gpss.ClearGpss(_racers.Values.Select(r => r.IdentityId));

            _chatManager.SendMessage("All racers ejected from the race");
            _racers.Clear();

            _safezones.Clear();
        }

        public async Task Start(int lapCount)
        {
            if (lapCount <= 0)
            {
                throw new Exception("Lap count cannot be zero");
            }

            if (!_checkpoints.Any())
            {
                throw new Exception("No checkpoints in the race");
            }

            Reset();

            if (_racers.Count == 0)
            {
                throw new Exception("no racers joined");
            }

            _chatManager.SendMessage($"Racers: {_racers.Select(r => r.Value.Name).ToStringSeq()}");

            for (var i = 0; i < 5; i++)
            {
                _chatManager.SendMessage($"Starting race in {5 - i} seconds...", toServer: true);

                await Task.Delay(1.Seconds());
                await VRageUtils.MoveToGameLoop();
            }

            // show the first gps for all racers
            foreach (var (_, racer) in _racers)
            {
                var gpsPositions = new[] { _checkpoints[0].Position };
                _gpss.ReplaceGpss(racer.IdentityId, gpsPositions);
            }

            _chatManager.SendMessage("GO!", toServer: true);

            _game = new RacingGame(_chatManager, _gpss, _checkpoints, _racers, lapCount);
        }

        public void Update()
        {
            // remove offline players from the race
            _tmpRemovedRacers.Clear();
            foreach (var (id, racer) in _racers)
            {
                if (!racer.IsOnline)
                {
                    _tmpRemovedRacers.Add((id, racer.Name));
                }
            }

            // cont. removing offline players
            foreach (var (removedRacerId, racerName) in _tmpRemovedRacers)
            {
                _racers.Remove(removedRacerId);
                _chatManager.SendMessage($"{racerName} left the race");
            }

            // cancel if there's no racers
            if (_racers.Count == 0)
            {
                Reset();
                return;
            }

            // skip if the track doesnt exist
            // NOTE this is silent because we dont wanna spam the log
            if (_checkpoints.Count <= 1) return;

            _game?.Update();
        }

        public void Reset()
        {
            if (_game == null) return;

            foreach (var (_, racer) in _racers)
            {
                racer.Reset();

                _gpss.ReplaceGpss(racer.IdentityId, new Vector3D[0]);
            }

            _game = null;

            _chatManager.SendMessage("Race has been reset");
        }

        void ThrowIfNotHostOrAdmin(ulong steamId)
        {
            if (steamId == 0) return;
            if (steamId == _ownerId) return;

            var player = (IMyPlayer)MySession.Static.Players.TryGetPlayerBySteamId(steamId);
            if (player?.PromoteLevel >= MyPromoteLevel.Moderator) return;

            throw new Exception("not a host");
        }

        public bool ContainsPlayer(ulong steamId)
        {
            return _racers.ContainsKey(steamId);
        }

        public override string ToString()
        {
            var builder = new StringBuilder();

            var ownerName = MySession.Static.Players.TryGetIdentityNameFromSteamId(_ownerId);
            builder.Append($"Owner: {ownerName.OrNull() ?? $"<{_ownerId}>"}");
            builder.AppendLine();

            builder.Append("Checkpoints: ");
            builder.AppendLine();
            foreach (var checkpoint in _checkpoints)
            {
                builder.Append(checkpoint);
                builder.AppendLine();
            }

            builder.Append("Racers: ");
            builder.AppendLine();
            foreach (var (_, racer) in _racers)
            {
                builder.Append(racer);
                builder.AppendLine();
            }

            return builder.ToString();
        }
    }
}