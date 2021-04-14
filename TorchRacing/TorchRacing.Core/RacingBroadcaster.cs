using System.Collections.Generic;
using Torch.API.Managers;
using Utils.Torch;

namespace TorchRacing.Core
{
    public sealed class RacingBroadcaster
    {
        const string RaceServer = "Race";

        readonly IChatManagerServer _chatManager;
        readonly IEnumerable<ulong> _racerSteamIds;

        public RacingBroadcaster(IChatManagerServer chatManager, IEnumerable<ulong> racerSteamIds)
        {
            _chatManager = chatManager;
            _racerSteamIds = racerSteamIds;
        }

        public void SendMessage(string message, bool toServer = false)
        {
            _chatManager.SendMessage(RaceServer, 0, message);
            return;

            if (toServer)
            {
                _chatManager.SendMessage(RaceServer, 0, message);
                return;
            }

            foreach (var racerId in _racerSteamIds)
            {
                _chatManager.SendMessage(RaceServer, racerId, message);
            }
        }
    }
}