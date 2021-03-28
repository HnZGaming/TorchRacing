using Newtonsoft.Json;
using Utils.Torch;
using VRageMath;

namespace TorchRacing.Core
{
    // don't store stateful data
    public sealed class RaceCheckpoint
    {
        [JsonProperty("position")]
        SerializableVector3 _position;

        [JsonProperty("radius")]
        float _radius;

        [JsonConstructor]
        RaceCheckpoint()
        {
        }

        public RaceCheckpoint(Vector3D position, float radius)
        {
            Position = position;
            _radius = radius;
        }

        public Vector3D Position
        {
            get => _position.ToVector3();
            private set => _position = new SerializableVector3(value);
        }

        public bool TryCheck(Vector3D position)
        {
            return Vector3D.Distance(position, Position) < _radius;
        }

        public override string ToString()
        {
            return $"{Position} ({_radius:0.0}m)";
        }
    }
}