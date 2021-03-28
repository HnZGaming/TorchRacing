using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Torch.Utils;
using Utils.General;
using VRageMath;

namespace TorchRacing.Core
{
    public sealed class RaceSafeZoneCollection
    {
        public interface IConfig
        {
            string DefaultSafeZoneColor { get; }
            string DefaultSafeZoneTexture { get; }
        }

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly IConfig _config;
        readonly List<NullOr<MySafeZone>> _safezones;

        public RaceSafeZoneCollection(IConfig config)
        {
            _config = config;
            _safezones = new List<NullOr<MySafeZone>>();
        }

        public IEnumerable<long> GetSafezoneIds()
        {
            return _safezones.Select(szr => szr.TryGet(out var s) ? s.EntityId : 0L);
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

        public void FindOrCreateAndAdd(long id, Vector3D position, float radius)
        {
            var safezone = FindOrCreate(id, position, radius);
            _safezones.Add(NullOr.NotNull(safezone));
        }

        public void CreateAndAdd(Vector3D position, float radius, bool useSafezone)
        {
            if (!useSafezone)
            {
                _safezones.Add(NullOr.Null<MySafeZone>());
                return;
            }

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

        MySafeZone FindOrCreate(long id, Vector3D position, float radius)
        {
            if (MyEntities.TryGetEntityById<MySafeZone>(id, out var s)) return s;
            var safezone = CreateSafezone(position, radius);
            return safezone;
        }

        MySafeZone CreateSafezone(Vector3D playerPos, float radius)
        {
            return (MySafeZone) MySessionComponentSafeZones.CrateSafeZone(
                MatrixD.CreateWorld(playerPos),
                MySafeZoneShape.Sphere,
                MySafeZoneAccess.Blacklist,
                null, null, radius, true,
                color: ColorUtils.TranslateColor(_config.DefaultSafeZoneColor),
                visualTexture: _config.DefaultSafeZoneTexture ?? "");
        }

        static void DeleteSafezone(MySafeZone safezone)
        {
            MySessionComponentSafeZones.RemoveSafeZone(safezone);
            safezone.Close();
            MyEntities.Remove(safezone);
        }
    }
}