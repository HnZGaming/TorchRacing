using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox.Game.World;
using Utils.General;
using VRage.Game.ModAPI;
using VRageMath;

namespace TorchRacing.Core
{
    public sealed class Racer
    {
        readonly IMyPlayer _player;
        readonly HashSet<int> _testedCheckpoints;
        readonly List<DateTime> _lapTimestamps;

        public Racer(IMyPlayer player)
        {
            _player = player;
            _testedCheckpoints = new HashSet<int>();
            _lapTimestamps = new List<DateTime>();
        }

        public Vector3D Position => _player.GetPosition();
        public int CheckCount => _testedCheckpoints.Count;
        public int LapCount { get; private set; }
        public bool IsOnline => MySession.Static.Players.IsPlayerOnline(_player.IdentityId);
        public int? LastCheckpoint { get; private set; }
        public string Name => _player.DisplayName;
        public long IdentityId => _player.IdentityId;

        public bool TryGetLapTimestampAt(int index, out DateTime lapTimestamp)
        {
            return _lapTimestamps.TryGetElementAt(index, out lapTimestamp);
        }

        public void Reset()
        {
            _testedCheckpoints.Clear();
            LastCheckpoint = null;
            LapCount = 0;
            _lapTimestamps.Clear();
        }

        public bool HasChecked(int checkpointIndex)
        {
            return _testedCheckpoints.Contains(checkpointIndex);
        }

        public void Check(int checkpointIndex)
        {
            _testedCheckpoints.Add(checkpointIndex);
            LastCheckpoint = checkpointIndex;
        }

        public void IncrementLap()
        {
            _testedCheckpoints.Clear();
            LastCheckpoint = null;
            LapCount += 1;
            _lapTimestamps.Add(DateTime.UtcNow);
        }

        public override string ToString()
        {
            var builder = new StringBuilder();

            builder.Append(_player.DisplayName);

            builder.Append(' ');
            builder.Append(_player.IdentityId);

            builder.AppendLine();
            builder.Append("-- Lap count: ");
            builder.Append(LapCount);
            builder.AppendLine();
            builder.Append("-- Checkpoints: ");
            builder.Append(_testedCheckpoints.OrderBy(c => c).ToStringSeq());
            builder.AppendLine();
            builder.Append("-- Lap timestamps:");
            foreach (var lapTimestamp in _lapTimestamps)
            {
                builder.AppendLine();
                builder.Append($"---- {lapTimestamp:hh:mm:ss}");
            }

            builder.AppendLine();
            builder.Append("-- Last checkpoint: ");
            builder.Append(LastCheckpoint ?? -1);

            return builder.ToString();
        }
    }
}