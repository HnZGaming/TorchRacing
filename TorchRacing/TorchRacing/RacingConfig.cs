using System.Xml.Serialization;
using Torch;
using Torch.Views;
using TorchRacing.Core;
using Utils.Torch;
using VRage.Game.ModAPI;

namespace TorchRacing
{
    public sealed class RacingConfig : ViewModel, RacingServer.IConfig, RaceGpsCollection.IConfig
    {
        const string SafeZoneGroupName = "Checkpoint Safe Zone";
        double _searchRadius = 100;
        string _safeZoneColor = "#ffffff";
        string _safeZoneTexture = "SafeZone_Texture_Default";
        string _gpsColor = "#00ffff";

        [XmlElement]
        [Display(Name = "Search radius", Description = "Radius to search for race checkpoints for commands")]
        [ConfigProperty(MyPromoteLevel.Moderator)]
        public double SearchRadius
        {
            get => _searchRadius;
            set => SetValue(ref _searchRadius, value);
        }

        [XmlElement]
        [Display(Name = "Default safe zone color", GroupName = SafeZoneGroupName)]
        [ConfigProperty(MyPromoteLevel.Moderator)]
        public string DefaultSafeZoneColor
        {
            get => _safeZoneColor;
            set => SetValue(ref _safeZoneColor, value);
        }

        [XmlElement]
        [Display(Name = "Default safe zone texture", GroupName = SafeZoneGroupName)]
        [ConfigProperty(MyPromoteLevel.Moderator)]
        public string DefaultSafeZoneTexture
        {
            get => _safeZoneTexture;
            set => SetValue(ref _safeZoneTexture, value);
        }

        [XmlElement]
        [Display(Name = "Checkpoint GPS color")]
        [ConfigProperty(MyPromoteLevel.Moderator)]
        public string GpsColor
        {
            get => _gpsColor;
            set => SetValue(ref _gpsColor, value);
        }
    }
}