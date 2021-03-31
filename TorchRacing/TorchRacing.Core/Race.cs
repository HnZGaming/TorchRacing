using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
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
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly IChatManagerServer _chatManager;
        readonly Dictionary<ulong, Racer> _racers;
        readonly IReadOnlyList<RaceCheckpoint> _checkpoints;
        readonly RaceGpsCollection _gpss;
        readonly ulong _hostId; // will user later when we check id for certain commands
        readonly List<ulong> _finishedRacerIds;
        readonly List<(ulong, string)> _tmpRemovedRacers;
        readonly int _totalLapCount;
        bool _isRacing;
        DateTime _startTime;

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

            SendMessage($"{player.DisplayName} joined the race!");

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

            SendMessage($"{player.DisplayName} left the race");

            _gpss.ShowGpss(player.IdentityId, Enumerable.Empty<Vector3D>());
        }

        public async Task Start(ulong playerId)
        {
            ThrowIfNotHostOrAdmin(playerId);
            Reset(playerId);

            if (_racers.Count == 0)
            {
                throw new Exception("no racers joined");
            }

            for (var i = 0; i < 5; i++)
            {
                SendMessage($"Starting race in {5 - i} seconds...", toServer: true);

                await Task.Delay(1.Seconds());
                await GameLoopObserver.MoveToGameLoop();
            }

            _isRacing = true;
            _startTime = DateTime.UtcNow;

            // show the first gps for all racers
            foreach (var (_, racer) in _racers)
            {
                var gpsPositions = new[] {_checkpoints[0].Position};
                _gpss.ShowGpss(racer.IdentityId, gpsPositions);
            }

            SendMessage("GO!", toServer: true);
        }

        public void Reset(ulong playerId)
        {
            ThrowIfNotHostOrAdmin(playerId);

            foreach (var (_, racer) in _racers)
            {
                racer.Reset();

                _gpss.ShowGpss(racer.IdentityId, new Vector3D[0]);
            }

            _finishedRacerIds.Clear();

            _isRacing = false;

            SendMessage("Race has been reset");
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
                SendMessage($"{racerName} left the race");
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
                var checkpointIndex = ((racer.LastCheckpoint ?? -1) + 1) % _checkpoints.Count;
                var checkpoint = _checkpoints[checkpointIndex];

                if (racer.HasChecked(checkpointIndex)) continue; // no duplicate
                if (!checkpoint.Test(racer.Position)) continue; // proximity test

                racer.Check(checkpointIndex);

                //show the next checkpoint gps
                var nextCheckpointIndex = (checkpointIndex + 1) % _checkpoints.Count;
                var nextGpsPosition = _checkpoints[nextCheckpointIndex].Position;
                _gpss.ShowGpss(racer.IdentityId, new[] {nextGpsPosition});

                if (racer.CheckCount < _checkpoints.Count) continue; // still kicking the lap

                racer.IncrementLap();

                if (racer.LapCount < _totalLapCount)
                {
                    var order = LangUtils.OrderToString(racer.LapCount);
                    SendMessage($"{racer.Name} has finished the {order} lap!");

                    var lapIndex = racer.LapCount - 1;
                    if (!TryGetLapTime(racer, lapIndex, out var lapTime))
                    {
                        throw new Exception($"lap time of a finished lap not found; lap: {lapIndex}, racer:\n{racer}");
                    }

                    SendMessage($"Lap time: {FormatLapTime(lapTime)}");
                    continue; // still doing some more laps
                }

                _finishedRacerIds.Add(racerId);

                var place = LangUtils.OrderToString(_finishedRacerIds.Count);
                SendMessage($"{racer.Name} FINISH! {place} place!");

                if (!TryGetTotalTime(racer, out var totalTime))
                {
                    throw new Exception($"total time of finished racer not found; racer:\n{racer}");
                }

                SendMessage($"Total time: {FormatLapTime(totalTime)}");

                _gpss.ShowGpss(racer.IdentityId, new Vector3D[0]);
            }

            if (_racers.Values.All(r => r.LapCount >= _totalLapCount)) // race finished
            {
                _isRacing = false;

                foreach (var (_, racer) in _racers)
                {
                    _gpss.ShowGpss(racer.IdentityId, new Vector3D[0]);
                }

                var resultText = new StringBuilder();
                resultText.AppendLine("ALL FINISH!");

                var rank = 1;
                foreach (var finishedRacerId in _finishedRacerIds)
                {
                    if (!_racers.TryGetValue(finishedRacerId, out var rankedRacer))
                    {
                        // shouldn't happen
                        throw new Exception($"racer not found; id: {finishedRacerId}");
                    }

                    if (!TryGetTotalTime(rankedRacer, out var finishedTotalTime))
                    {
                        throw new Exception($"total time of finished racer not found; racer:\n{rankedRacer}");
                    }

                    var rankStr = LangUtils.OrderToString(rank);
                    resultText.AppendLine($" {rankStr}: {rankedRacer.Name} {FormatLapTime(finishedTotalTime)}");
                    rank += 1;
                }

                resultText.AppendLine("Type `!race start` to start the new race");

                SendMessage(resultText.ToString());
            }
        }

        bool TryGetLapTime(Racer racer, int index, out TimeSpan lapTime)
        {
            if (!racer.TryGetLapTimestampAt(index, out var lapTimestamp))
            {
                lapTime = TimeSpan.Zero;
                return false;
            }

            if (index == 0)
            {
                lapTime = lapTimestamp - _startTime;
                return true;
            }

            if (!racer.TryGetLapTimestampAt(index - 1, out var lastLapTimestamp))
            {
                // this shouldn't happen
                throw new Exception($"last lap not complete; index: {index} racer: \n{racer}");
            }

            lapTime = lapTimestamp - lastLapTimestamp;
            return true;
        }

        bool TryGetTotalTime(Racer racer, out TimeSpan totalTime)
        {
            totalTime = TimeSpan.Zero;
            for (var i = 0; i < _totalLapCount; i++)
            {
                if (!TryGetLapTime(racer, i, out var lapTime))
                {
                    // racer isn't finished all the laps yet
                    return false;
                }

                totalTime += lapTime;
            }

            return true;
        }

        static string FormatLapTime(TimeSpan timeSpan)
        {
            return $"{timeSpan.Hours:0}:{timeSpan.Minutes:0}:{timeSpan.Seconds:0}:{timeSpan.Milliseconds / 10:00}";
        }

        void SendMessage(string message, bool toServer = false)
        {
            if (toServer)
            {
                _chatManager.SendMessage(RaceServer, 0, message);
                return;
            }

            foreach (var (racerId, _) in _racers)
            {
                _chatManager.SendMessage(RaceServer, racerId, message);
            }
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

        public string ToString(bool debug)
        {
            var builder = new StringBuilder();

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