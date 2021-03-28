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
        const string SafeZoneGroupName = "Checkpoint Safe Zone";
        bool _allowActionsInSafeZone;
        double _searchRadius = 100;
        string _safeZoneColor = "#ffffff";
        string _safeZoneTexture = "SafeZone_Texture_Default";

        [XmlElement]
        [Display(Name = "Allow actions in safe zone", GroupName = SafeZoneGroupName, Description = "Allow actions inside checkpoint safe zones")]
        [ConfigProperty(MyPromoteLevel.Moderator)]
        public bool AllowActionsInSafeZone
        {
            get => _allowActionsInSafeZone;
            set => SetValue(ref _allowActionsInSafeZone, value);
        }

        [XmlElement]
        [Display(Name = "Search radius", Description = "Radius to search for race checkpoints for commands")]
        [ConfigProperty(MyPromoteLevel.Moderator)]
        public double SearchRadius
        {
            get => _searchRadius;
            set => SetValue(ref _searchRadius, value);
        }

        [XmlElement]
        [Display(Name = "Safe zone color", GroupName = SafeZoneGroupName, Description = "Default color of checkpoint safe zones")]
        [ConfigProperty(MyPromoteLevel.Moderator)]
        public string SafeZoneColor
        {
            get => _safeZoneColor;
            set => SetValue(ref _safeZoneColor, value);
        }

        [XmlElement]
        [Display(Name = "Safe zone texture", GroupName = SafeZoneGroupName, Description = "Default texture of checkpoint safe zones")]
        [ConfigProperty(MyPromoteLevel.Moderator)]
        public string SafeZoneTexture
        {
            get => _safeZoneTexture;
            set => SetValue(ref _safeZoneTexture, value);
        }
    }
}