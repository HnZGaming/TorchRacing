using System.Collections.Generic;
using Torch.API.Managers;
using Utils.Torch;

namespace TorchRacing.Core
{
    public sealed class RacingBroadcaster
    {
        const string RaceServer = "Race";

        readonly IChatManagerServer _chatManager;
        readonly string _raceId;
        readonly IEnumerable<ulong> _racerSteamIds;

        public RacingBroadcaster(IChatManagerServer chatManager, string raceId, IEnumerable<ulong> racerSteamIds)
        {
            _chatManager = chatManager;
            _raceId = raceId;
            _racerSteamIds = racerSteamIds;
        }

        public void SendMessage(string message, bool toServer = false)
        {
            _chatManager.SendMessage(RaceServer, 0, $"{_raceId}: {message}");
#if false
            if (toServer)
            {
                _chatManager.SendMessage(RaceServer, 0, message);
                return;
            }

            foreach (var racerId in _racerSteamIds)
            {
                _chatManager.SendMessage(RaceServer, racerId, message);
            }
#endif
        }
    }
}