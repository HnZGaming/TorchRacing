using System.Windows.Controls;
using NLog;
using Torch;
using Torch.API;
using Torch.API.Plugins;
using TorchRacing.Core;
using Utils.General;
using Utils.Torch;

namespace TorchRacing
{
    public sealed class RacingPlugin : TorchPluginBase, IWpfPlugin
    {
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        Persistent<RacingConfig> _config;
        UserControl _userControl;
        RacingServer _racingServer;

        public RacingConfig Config => _config.Data;
        public RacingServer Server => _racingServer;

        public UserControl GetControl()
        {
            return _config.GetOrCreateUserControl(ref _userControl);
        }

        public override void Init(ITorchBase torch)
        {
            base.Init(torch);
            this.ListenOnGameLoaded(OnGameLoad);
            this.ListenOnGameUnloading(OnGameUnloading);

            GameLoopObserverManager.Add(Torch);

            var configPath = this.MakeConfigFilePath();
            _config = Persistent<RacingConfig>.Load(configPath);
        }

        void OnGameLoad()
        {
            var dbPath = this.MakeFilePath($"{nameof(RacingPlugin)}.json");
            _racingServer = new RacingServer(Config, dbPath);
            _racingServer.Initialize();
        }

        public override void Update()
        {
            base.Update();
            _racingServer.Update();
        }

        void OnGameUnloading()
        {
            _racingServer.Dispose();
        }
    }
}