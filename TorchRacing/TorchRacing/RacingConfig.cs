using System.Xml.Serialization;
using Torch;
using Torch.Views;
using TorchRacing.Core;
using Utils.Torch;
using VRage.Game.ModAPI;

namespace TorchRacing
{
    public sealed class RacingConfig : ViewModel, RacingServer.IConfig
    {
        bool _enableSafeZones = true;
        double _searchRadius = 100;

        [XmlElement]
        [Display(Name = "Enable safe zones", Description = "Enable safe zone at race checkpoint for visual clue and no collision damage")]
        [ConfigProperty(MyPromoteLevel.Moderator)]
        public bool EnableSafeZones
        {
            get => _enableSafeZones;
            set => SetValue(ref _enableSafeZones, value);
        }

        [XmlElement]
        [Display(Name = "Search radius", Description = "Radius to search for race checkpoints for commands")]
        [ConfigProperty(MyPromoteLevel.Moderator)]
        public double SearchRadius
        {
            get => _searchRadius;
            set => SetValue(ref _searchRadius, value);
        }
    }
}