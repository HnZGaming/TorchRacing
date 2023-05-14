using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;
using Utils.General;
using VRageMath;

namespace TorchRacing.Core
{
    public sealed class RacingGame
    {
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly RacingBroadcaster _chatManager;
        readonly IReadOnlyList<RaceCheckpoint> _checkpoints;
        readonly IReadOnlyDictionary<ulong, Racer> _racers;
        readonly RaceGpsCollection _gpss;
        readonly List<ulong> _finishedRacerIds;
        readonly int _totalLapCount;
        readonly DateTime _startTime;
        public bool Done { get; private set; }

        public RacingGame(RacingBroadcaster chatManager,
            RaceGpsCollection gpss,
            IReadOnlyList<RaceCheckpoint> checkpoints,
            IReadOnlyDictionary<ulong, Racer> racers,
            int lapCount)
        {
            _chatManager = chatManager;
            _checkpoints = checkpoints;
            _gpss = gpss;
            _totalLapCount = lapCount;
            _racers = racers;
            _finishedRacerIds = new List<ulong>();
            _startTime = DateTime.UtcNow;
        }

        public void Update() // NOTE this is called EVERY frame
        {
            if (Done) return;

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
                _gpss.ReplaceGpss(racer.IdentityId, new[] {nextGpsPosition}, nextCheckpointIndex);

                if (racer.CheckCount < _checkpoints.Count) continue; // still kicking the lap

                racer.IncrementLap();

                if (racer.LapCount < _totalLapCount)
                {
                    var order = LangUtils.OrderToString(racer.LapCount);
                    _chatManager.SendMessage($"{racer.Name} has finished the {order} lap!");

                    var lapIndex = racer.LapCount - 1;
                    if (!TryGetLapTime(racer, lapIndex, out var lapTime))
                    {
                        throw new Exception($"lap time of a finished lap not found; lap: {lapIndex}, racer:\n{racer}");
                    }

                    _chatManager.SendMessage($"Lap time: {FormatLapTime(lapTime)}");
                    continue; // still doing some more laps
                }

                _finishedRacerIds.Add(racerId);

                var place = LangUtils.OrderToString(_finishedRacerIds.Count);
                _chatManager.SendMessage($"{racer.Name} FINISH! {place} place!");

                if (!TryGetTotalTime(racer, out var totalTime))
                {
                    throw new Exception($"total time of finished racer not found; racer:\n{racer}");
                }

                _chatManager.SendMessage($"Total time: {FormatLapTime(totalTime)}");

                _gpss.ReplaceGpss(racer.IdentityId, Array.Empty<Vector3D>());
            }

            var allRacersFinished = _racers.Values.All(r => r.LapCount >= _totalLapCount);
            if (!allRacersFinished) return;

            Done = true;

            foreach (var (_, racer) in _racers)
            {
                _gpss.ReplaceGpss(racer.IdentityId, Array.Empty<Vector3D>());
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

            _chatManager.SendMessage(resultText.ToString());
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
    }
}