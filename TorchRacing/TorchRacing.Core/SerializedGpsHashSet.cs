using Newtonsoft.Json;
using Utils.General;

namespace TorchRacing.Core
{
    public sealed class SerializedGpsHashSet
    {
        [JsonProperty("id"), StupidDbId]
        public string Id { get; set; }

        [JsonProperty("hashes")]
        public int[] GpsHashes { get; set; } = new int[0];
    }
}