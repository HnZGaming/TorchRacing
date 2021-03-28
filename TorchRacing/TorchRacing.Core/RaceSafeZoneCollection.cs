using System.Collections.Generic;
using NLog;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Torch.Utils;
using Utils.General;
using VRage.Game.ObjectBuilders.Components;
using VRageMath;

namespace TorchRacing.Core
{
    public sealed class RaceSafeZoneCollection
    {
        public interface IConfig
        {
            bool AllowActionsInSafeZone { get; }
            string SafeZoneColor { get; }
            string SafeZoneTexture { get; }
        }

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly IConfig _config;
        readonly List<NullOr<MySafeZone>> _safezones;

        public RaceSafeZoneCollection(IConfig config)
        {
            _config = config;
            _safezones = new List<NullOr<MySafeZone>>();
        }

        public IEnumerable<string> GetAllNames()
        {
            foreach (var safezoneOrNull in _safezones)
            {
                if (safezoneOrNull.TryGet(out var safezone))
                {
                    yield return safezone.Name;
                }
            }
        }

        public void Clear()
        {
            foreach (var safezoneOrNull in _safezones)
            {
                if (safezoneOrNull.TryGet(out var safezone))
                {
                    DeleteSafezone(safezone);
                }
            }

            _safezones.Clear();
        }

        public void Update()
        {
            foreach (var safezoneRef in _safezones)
            {
                if (!safezoneRef.TryGet(out var safezone)) continue;

                var lastAllowedActions = safezone.AllowedActions;
                safezone.AllowedActions = _config.AllowActionsInSafeZone ? MySafeZoneAction.All : 0;

                if (safezone.AllowedActions != lastAllowedActions)
                {
                    var builder = (MyObjectBuilder_SafeZone) safezone.GetObjectBuilder();
                    MySessionComponentSafeZones.UpdateSafeZone(builder);
                    Log.Info("safe zone config(s) updated");
                }
            }
        }

        public void FindOrCreateAndAdd(string name, Vector3D position, float radius)
        {
            var safezone = FindOrCreate(name, position, radius);
            _safezones.Add(NullOr.NotNull(safezone));
        }

        public void CreateAndAdd(Vector3D position, float radius)
        {
            var safezone = CreateSafezone(position, radius);
            _safezones.Add(NullOr.NotNull(safezone));
        }

        public void RemoveAt(int index)
        {
            var removedSafezone = _safezones[index];
            _safezones.RemoveAt(index);

            if (removedSafezone.TryGet(out var safezone))
            {
                DeleteSafezone(safezone);
            }
        }

        MySafeZone FindOrCreate(string name, Vector3D position, float radius)
        {
            if (MyEntities.TryGetEntityByName<MySafeZone>(name, out var s)) return s;
            return CreateSafezone(position, radius);
        }

        MySafeZone CreateSafezone(Vector3D playerPos, float radius)
        {
            return (MySafeZone) MySessionComponentSafeZones.CrateSafeZone(
                MatrixD.CreateWorld(playerPos),
                MySafeZoneShape.Sphere,
                MySafeZoneAccess.Blacklist,
                null, null, radius, true,
                color: ColorUtils.TranslateColor(_config.SafeZoneColor),
                visualTexture: _config.SafeZoneTexture ?? "");
        }

        static void DeleteSafezone(MySafeZone safezone)
        {
            MySessionComponentSafeZones.RemoveSafeZone(safezone);
            safezone.Close();
            MyEntities.Remove(safezone);
        }
    }
}