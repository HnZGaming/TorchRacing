using System.Collections.Generic;
using System.Linq;
using VRageMath;

namespace TorchRacing.Core
{
    public interface HasPosition
    {
        Vector3D Position { get; }
    }

    public static class HasPositionUtils
    {
        public static bool TryGetNearestPositionIndex(this IReadOnlyList<HasPosition> self, Vector3D position, double radius, out int nearestIndex)
        {
            nearestIndex = -1;

            if (!self.Any()) return false;

            var minDistance = double.MaxValue;
            for (var i = 0; i < self.Count; i++)
            {
                var checkpoint = self[i];
                var checkpointPos = checkpoint.Position;
                var distance = Vector3D.Distance(position, checkpointPos);
                if (distance > radius) continue;

                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestIndex = i;
                }
            }

            return nearestIndex >= 0;
        }
    }
}