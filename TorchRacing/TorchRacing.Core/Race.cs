using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Game.World;
using Utils.General;
using Utils.Torch;
using VRage.Game.ModAPI;

namespace TorchRacing.Core
{
    public sealed class Race : IDisposable
    {
        readonly Dictionary<long, Racer> _racers;
        readonly IReadOnlyList<RaceCheckpoint> _checkpoints;
        readonly long _hostPlayerId;
        readonly int _totalLapCount;
        bool _isRacing;

        public Race(IReadOnlyList<RaceCheckpoint> checkpoints, long hostPlayerId, int lapCount)
        {
            _checkpoints = checkpoints;
            _hostPlayerId = hostPlayerId;
            _totalLapCount = lapCount;
            _racers = new Dictionary<long, Racer>();
        }

        public void Dispose()
        {
            _racers.Clear();
        }

        public void AddRacer(IMyPlayer player)
        {
            if (_racers.ContainsKey(player.IdentityId))
            {
                throw new Exception("Already joined the race");
            }

            var racer = new Racer(player);
            _racers[player.IdentityId] = racer;

            //TODO broadcast
        }

        public void RemoveRacer(IMyPlayer player)
        {
            if (!_racers.Remove(player.IdentityId))
            {
                throw new Exception("Not joined the race");
            }

            //TODO broadcast
        }

        public async Task Start(long playerId, int countdown)
        {
            ThrowIfNotHostPlayer(playerId);

            for (var i = 0; i < countdown; i++)
            {
                //TODO broadcast countdown

                await Task.Delay(1.Seconds());
                await GameLoopObserver.MoveToGameLoop();
            }

            _isRacing = true;

            //TODO broadcast
            //TODO start showing gps and stuff
        }

        public void End(long playerId)
        {
            ThrowIfNotHostPlayer(playerId);
            _isRacing = false;

            //TODO broadcast
        }

        public void Cancel(long playerId)
        {
            ThrowIfNotHostPlayer(playerId);
            _isRacing = false;

            //TODO broadcast
        }

        public void Reset(long playerId)
        {
            ThrowIfNotHostPlayer(playerId);
            foreach (var (_, racer) in _racers)
            {
                racer.Reset();
            }
        }

        public void Update()
        {
            if (!_isRacing) return;

            // cancel if host is offline
            if (!MySession.Static.Players.IsPlayerOnline(_hostPlayerId))
            {
                _isRacing = false;
                return;
            }

            // remove offline players from the race
            foreach (var (id, racer) in _racers.ToArray())
            {
                if (!racer.IsOnline)
                {
                    _racers.Remove(id);

                    //TODO broadcast
                }
            }

            // cancel if there's no racers
            if (!_racers.Any())
            {
                _isRacing = false;
                return;
            }

            foreach (var (_, racer) in _racers)
            {
                for (var i = 0; i < _checkpoints.Count; i++)
                {
                    var checkpoint = _checkpoints[i];
                    if (racer.HasChecked(i)) continue;
                    if (racer.LastCheckpoint == i) continue;
                    if (!checkpoint.TryCheck(racer.Position)) continue;

                    racer.Check(i);

                    if (racer.CheckCount < _checkpoints.Count) continue;

                    racer.ClearChecks();
                    racer.IncrementLap();

                    //TODO broadcast

                    if (racer.LapCount < _totalLapCount) continue;

                    // TODO remove gps for this player

                    if (!_racers.Values.All(r => r.LapCount > _totalLapCount)) continue;

                    _isRacing = false;

                    // TODO broadcast
                }
            }
        }

        void ThrowIfNotHostPlayer(long playerId)
        {
            if (playerId != _hostPlayerId)
            {
                throw new Exception("Not a host player");
            }
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