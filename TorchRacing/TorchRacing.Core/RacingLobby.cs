using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        readonly string _raceId;
        readonly ulong ownerId;
        RacingGame _game;

        public RacingLobby(
            IConfig config,
            IChatManagerServer chatManager,
            RaceGpsCollection gpss,
            SerializedRace serializedRace)
        {
            _config = config;
            _gpss = gpss;
            _chatManager = new RacingBroadcaster(chatManager, _racers.Keys);
            _checkpoints = new List<RaceCheckpoint>();
            _safezones = new RaceSafeZoneCollection(config);
            _racers = new Dictionary<ulong, Racer>();
            _tmpRemovedRacers = new List<(ulong, string)>();
            _raceId = serializedRace.RaceId;
            ownerId = serializedRace.OwnerSteamId;

            for (var i = 0; i < serializedRace.Checkpoints.Length; i++)
            {
                var checkpoint = serializedRace.Checkpoints[i];
                _checkpoints.Add(checkpoint);

                var safezone = serializedRace.CheckpointSafezones[i];
                _safezones.FindOrCreateAndAdd(safezone, checkpoint.Position, checkpoint.Radius);
            }
        }

        public SerializedRace Serialize() => new SerializedRace
        {
            RaceId = _raceId,
            Checkpoints = _checkpoints.ToArray(),
            CheckpointSafezones = _safezones.GetSafezoneIds().ToArray(),
        };

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
                Reset(0);
                return;
            }

            // skip if the track doesnt exist
            // NOTE this is silent because we dont wanna spam the log
            if (_checkpoints.Count <= 1) return;

            _game?.Update();
        }

        public void AddCheckpoint(IMyPlayer player, float radius, bool useSafezone)
        {
            var position = player.GetPosition();
            var checkpoint = new RaceCheckpoint(position, radius);
            _checkpoints.Add(checkpoint);
            _safezones.CreateAndAdd(position, radius, useSafezone);
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
        }

        public void RemoveAllCheckpoints(IMyPlayer player)
        {
            _checkpoints.Clear();
            _safezones.Clear();
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
                : new[] {_checkpoints[0].Position};

            _gpss.ShowGpss(player.IdentityId, gpsPositions);
        }

        public void RemoveRacer(IMyPlayer player)
        {
            if (!_racers.Remove(player.SteamUserId))
            {
                throw new Exception("Not joined the race");
            }

            _chatManager.SendMessage($"{player.DisplayName} left the race");

            _gpss.ShowGpss(player.IdentityId, Enumerable.Empty<Vector3D>());
        }

        public async Task Start(ulong playerId, int lapCount)
        {
            ThrowIfNotHostOrAdmin(playerId);

            if (lapCount <= 0)
            {
                throw new Exception("Lap count cannot be zero");
            }

            if (!_checkpoints.Any())
            {
                throw new Exception("No checkpoints in the race");
            }

            Reset(playerId);

            if (_racers.Count == 0)
            {
                throw new Exception("no racers joined");
            }

            _chatManager.SendMessage($"Racers: {_racers.Select(r => r.Value.Name).ToStringSeq()}");

            for (var i = 0; i < 5; i++)
            {
                _chatManager.SendMessage($"Starting race in {5 - i} seconds...", toServer: true);

                await Task.Delay(1.Seconds());
                await GameLoopObserver.MoveToGameLoop();
            }

            // show the first gps for all racers
            foreach (var (_, racer) in _racers)
            {
                var gpsPositions = new[] {_checkpoints[0].Position};
                _gpss.ShowGpss(racer.IdentityId, gpsPositions);
            }

            _chatManager.SendMessage("GO!", toServer: true);

            _game = new RacingGame(_chatManager, _gpss, _checkpoints, _racers, lapCount);
        }

        public void Reset(ulong playerId)
        {
            if (_game == null) return;

            ThrowIfNotHostOrAdmin(playerId);

            foreach (var (_, racer) in _racers)
            {
                racer.Reset();

                _gpss.ShowGpss(racer.IdentityId, new Vector3D[0]);
            }

            _game = null;

            _chatManager.SendMessage("Race has been reset");
        }

        void ThrowIfNotHostOrAdmin(ulong steamId)
        {
            // if (steamId == 0) return;
            // if (steamId == _hostId) return;
            //
            // var player = (IMyPlayer) MySession.Static.Players.TryGetPlayerBySteamId(steamId);
            // if (player?.PromoteLevel >= MyPromoteLevel.Moderator) return;
            //
            // throw new Exception("not a host");
        }

        public bool ContainsPlayer(ulong steamId)
        {
            return _racers.ContainsKey(steamId);
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

            builder.Append("Racers: ");
            builder.AppendLine();
            foreach (var (_, racer) in _racers)
            {
                builder.Append(racer.ToString(debug));
                builder.AppendLine();
            }

            return builder.ToString();
        }

        public override string ToString()
        {
            return ToString(true);
        }
    }
}