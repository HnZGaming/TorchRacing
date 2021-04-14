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
    public sealed class RacingLobby : IDisposable
    {
        readonly RacingBroadcaster _chatManager;
        readonly RaceGpsCollection _gpss;
        readonly IReadOnlyList<RaceCheckpoint> _checkpoints;
        readonly Dictionary<ulong, Racer> _racers;
        readonly List<(ulong, string)> _tmpRemovedRacers;
        RacingGame _game;

        public RacingLobby(IChatManagerServer chatManager, RaceGpsCollection gpss, IReadOnlyList<RaceCheckpoint> checkpoints)
        {
            _gpss = gpss;
            _checkpoints = checkpoints;
            _racers = new Dictionary<ulong, Racer>();
            _chatManager = new RacingBroadcaster(chatManager, _racers.Keys);
            _tmpRemovedRacers = new List<(ulong, string)>();
        }

        public void Dispose()
        {
            _racers.Clear();
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
                Reset(0);
                return;
            }

            _game?.Update();
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

            _game = new RacingGame(_chatManager, _gpss, _checkpoints, _racers, lapCount);

            // show the first gps for all racers
            foreach (var (_, racer) in _racers)
            {
                var gpsPositions = new[] {_checkpoints[0].Position};
                _gpss.ShowGpss(racer.IdentityId, gpsPositions);
            }

            _chatManager.SendMessage("GO!", toServer: true);
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