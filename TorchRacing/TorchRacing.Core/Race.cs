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
    public sealed class Race : IDisposable
    {
        const string RaceServer = "Race";
        readonly IChatManagerServer _chatManager;
        readonly Dictionary<ulong, Racer> _racers;
        readonly IReadOnlyList<RaceCheckpoint> _checkpoints;
        readonly RaceGpsCollection _gpss;
        readonly ulong _hostId;
        readonly List<ulong> _finishedRacerIds;
        readonly List<(ulong, string)> _tmpRemovedRacers;
        readonly int _totalLapCount;
        bool _isRacing;

        public Race(IChatManagerServer chatManager,
            RaceGpsCollection gpss,
            IReadOnlyList<RaceCheckpoint> checkpoints,
            ulong hostId,
            int lapCount)
        {
            _chatManager = chatManager;
            _checkpoints = checkpoints;
            _gpss = gpss;
            _hostId = hostId;
            _totalLapCount = lapCount;
            _racers = new Dictionary<ulong, Racer>();
            _finishedRacerIds = new List<ulong>();
            _tmpRemovedRacers = new List<(ulong, string)>();
        }

        public void Dispose()
        {
            _racers.Clear();
        }

        public void AddRacer(IMyPlayer player)
        {
            if (_racers.ContainsKey(player.SteamUserId))
            {
                throw new Exception("Already joined the race");
            }

            var racer = new Racer(player);
            _racers[player.SteamUserId] = racer;

            SendMessageToAllRacers($"{player.DisplayName} joined the race!");

            // show gpss to this player

            var gpsPositions = _isRacing
                ? new[] {_checkpoints[0].Position}
                : _checkpoints.Select(c => c.Position);

            _gpss.ShowGpss(player.IdentityId, gpsPositions);
        }

        public void RemoveRacer(IMyPlayer player)
        {
            if (!_racers.Remove(player.SteamUserId))
            {
                throw new Exception("Not joined the race");
            }

            SendMessageToAllRacers($"{player.DisplayName} left the race");

            _gpss.ShowGpss(player.IdentityId, Enumerable.Empty<Vector3D>());
        }

        public async Task Start(ulong playerId, int countdown)
        {
            ThrowIfNotHostOrAdmin(playerId);
            Reset(playerId);

            for (var i = 0; i < countdown; i++)
            {
                SendMessageToAllRacers($"Starting race in {countdown - i} seconds...");

                await Task.Delay(1.Seconds());
                await GameLoopObserver.MoveToGameLoop();
            }

            _isRacing = true;

            // show the first gps for all racers
            foreach (var (_, racer) in _racers)
            {
                var gpsPositions = new[] {_checkpoints[0].Position};
                _gpss.ShowGpss(racer.IdentityId, gpsPositions);
            }

            SendMessageToAllRacers("GO!");
        }

        public void Reset(ulong playerId)
        {
            ThrowIfNotHostOrAdmin(playerId);

            foreach (var (_, racer) in _racers)
            {
                racer.Reset();
            }

            _finishedRacerIds.Clear();

            _isRacing = false;

            SendMessageToAllRacers("Race has been reset");
        }

        public void Update() // NOTE this is called EVERY frame
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
                SendMessageToAllRacers($"{racerName} left the race");
            }

            if (!_isRacing) return;

            // cancel if there's no racers
            if (_racers.Count == 0)
            {
                Reset(0);
                return;
            }

            foreach (var (racerId, racer) in _racers)
            {
                for (var i = 0; i < _checkpoints.Count; i++)
                {
                    var checkpoint = _checkpoints[i];

                    if (!checkpoint.Test(racer.Position)) continue; // proximity
                    if (racer.HasChecked(i)) continue; // no duplicate

                    // prevent checking the final checkpoint at the beginning of the race
                    // (make sure the player has stepped on the last checkpoint)
                    if ((racer.LastCheckpoint ?? -1) + 1 != i) continue;

                    racer.Check(i);

                    //show the next checkpoint gps
                    var nextGpsPosition = _checkpoints[(i + 1) % _checkpoints.Count].Position;
                    _gpss.ShowGpss(racer.IdentityId, new[] {nextGpsPosition});

                    if (racer.CheckCount < _checkpoints.Count) continue;

                    racer.ClearChecks();
                    racer.IncrementLap();

                    if (racer.LapCount < _totalLapCount)
                    {
                        var order = LangUtils.OrderToString(racer.LapCount);
                        var remainingLapCount = _totalLapCount - racer.LapCount;
                        SendMessageToAllRacers($"{racer.Name} has finished the {order} lap! {remainingLapCount} laps to go");
                        continue;
                    }

                    _finishedRacerIds.Add(racerId);

                    var position = LangUtils.OrderToString(_finishedRacerIds.Count);
                    SendMessageToAllRacers($"{racer.Name} GOAL! {position} position!");

                    _gpss.ShowGpss(racer.IdentityId, new Vector3D[0]);

                    if (!_racers.Values.All(r => r.LapCount >= _totalLapCount)) continue;

                    _isRacing = false;

                    SendMessageToAllRacers("Everyone finished the race! Type `!race start` to start the new race");
                }
            }
        }

        void SendMessageToAllRacers(string message)
        {
            foreach (var (racerId, _) in _racers)
            {
                _chatManager.SendMessage(RaceServer, racerId, message);
            }
        }

        void ThrowIfNotHostOrAdmin(ulong steamId)
        {
            if (steamId == 0) return;
            if (steamId == _hostId) return;

            var player = (IMyPlayer) MySession.Static.Players.TryGetPlayerBySteamId(steamId);
            if (player?.PromoteLevel >= MyPromoteLevel.Moderator) return;

            throw new Exception("not a host");
        }

        public override string ToString()
        {
            var builder = new StringBuilder();

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