using Newtonsoft.Json;
using Utils.Torch;
using VRageMath;

namespace TorchRacing.Core
{
    // don't store stateful data
    public sealed class RaceCheckpoint
    {
        [JsonConstructor]
        RaceCheckpoint()
        {
        }

        public RaceCheckpoint(Vector3D position, float radius, string safeZoneName)
        {
            Position = position;
            Radius = radius;
            SafeZoneName = safeZoneName;
        }

        [JsonProperty("position")]
        public Vector3D Position { get; set; }

        [JsonProperty("radius")]
        public float Radius { get; set; }

        [JsonProperty("safezone")]
        public string SafeZoneName { get; set; }

        public bool TryCheck(Vector3D position)
        {
            return Vector3D.Distance(position, Position) < Radius;
        }

        public override string ToString()
        {
            return $"{Position.ToShortString()} ({Radius:0.0}m)";
        }
    }
}