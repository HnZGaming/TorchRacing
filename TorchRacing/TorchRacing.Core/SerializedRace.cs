using Newtonsoft.Json;
using Utils.General;

namespace TorchRacing.Core
{
    public sealed class SerializedRace
    {
        [StupidDbId]
        [JsonProperty("id")]
        public string RaceId { get; set; }

        [JsonProperty("checkpoints")]
        public RaceCheckpoint[] Checkpoints { get; set; } = new RaceCheckpoint[0];

        [JsonProperty("checkpoint_safezones")]
        public long[] CheckpointSafezones { get; set; } = new long[0];
    }
}