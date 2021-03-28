using VRage.Game.ModAPI;

namespace TorchRacing.Core
{
    public class RacingServer
    {
        public interface IConfig
        {
        }

        readonly IConfig _config;

        public RacingServer(IConfig config)
        {
            _config = config;
        }

        public void Initialize()
        {
            throw new System.NotImplementedException();
        }

        public void Clear()
        {
            throw new System.NotImplementedException();
        }

        public void Update()
        {
            throw new System.NotImplementedException();
        }

        public void AddCheckpoint(IMyPlayer contextPlayer)
        {
            throw new System.NotImplementedException();
        }

        public void RemoveCheckpoint(IMyPlayer contextPlayer)
        {
            throw new System.NotImplementedException();
        }

        public void InitializeRace(IMyPlayer contextPlayer)
        {
            throw new System.NotImplementedException();
        }

        public void JoinRace(IMyPlayer contextPlayer)
        {
            throw new System.NotImplementedException();
        }

        public void StartRace(IMyPlayer contextPlayer, int countdown)
        {
            throw new System.NotImplementedException();
        }

        public void EndRace(IMyPlayer contextPlayer)
        {
            throw new System.NotImplementedException();
        }

        public void CancelRace(IMyPlayer contextPlayer)
        {
            throw new System.NotImplementedException();
        }
    }
}