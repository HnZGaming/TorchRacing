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

        public Racer(IMyPlayer player)
        {
            _player = player;
            _testedCheckpoints = new HashSet<int>();
        }

        public Vector3D Position => _player.GetPosition();
        public int CheckCount => _testedCheckpoints.Count;
        public int LapCount { get; private set; }
        public bool IsOnline => MySession.Static.Players.IsPlayerOnline(_player.IdentityId);
        public int? LastCheckpoint { get; private set; }

        public string Name => _player.DisplayName;

        public long IdentityId => _player.IdentityId;

        public void Reset()
        {
            _testedCheckpoints.Clear();
            LastCheckpoint = null;
            LapCount = 0;
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

        public void ClearChecks()
        {
            _testedCheckpoints.Clear();
            LastCheckpoint = null;
        }

        public void IncrementLap()
        {
            LapCount += 1;
        }

        public string ToString(bool debug)
        {
            var builder = new StringBuilder();

            builder.Append(_player.DisplayName);

            if (debug)
            {
                builder.Append(' ');
                builder.Append(_player.IdentityId);
            }

            builder.AppendLine();
            builder.Append("-- Lap count: ");
            builder.Append(LapCount);
            builder.AppendLine();
            builder.Append("-- Checkpoints: ");
            builder.Append(_testedCheckpoints.OrderBy(c => c).ToStringSeq());

            if (debug)
            {
                builder.AppendLine();
                builder.Append("-- Last checkpoint: ");
                builder.Append(LastCheckpoint ?? -1);
            }

            return builder.ToString();
        }
    }
}