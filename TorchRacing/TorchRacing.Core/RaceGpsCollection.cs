using System.Collections.Generic;
using System.Linq;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.World;
using Utils.General;
using Utils.Torch;
using Utils.Torch.Patches;
using VRageMath;

namespace TorchRacing.Core
{
    public sealed class RaceGpsCollection
    {
        const string DefaultTableName = "default";
        readonly StupidDb<SerializedGpsHashSet> _db;
        readonly Dictionary<long, HashSet<int>> _gpsHashes;
        bool _isDirty;

        public RaceGpsCollection(string dbPath)
        {
            _db = new StupidDb<SerializedGpsHashSet>(dbPath);
            _gpsHashes = new Dictionary<long, HashSet<int>>();
        }

        public void Initialize()
        {
            _db.Read();

            // delete GPSs from the last session
            var gpsHashes = _db.QueryOrDefault(DefaultTableName)?.GpsHashes.ToSet() ?? new HashSet<int>();
            foreach (var (identityId, gps) in MySession.Static.Gpss.GetAllGpss())
            {
                if (gpsHashes.Contains(gps.Hash))
                {
                    MySession.Static.Gpss.SendDelete(identityId, gps.Hash);
                }
            }

            _db.Clear();
            _db.Write();
        }

        public void ShowGpss(long playerId, IEnumerable<Vector3D> positions)
        {
            _isDirty = true;

            // delete last set of GPSs
            if (_gpsHashes.TryGetValue(playerId, out var gpsHashes))
            {
                foreach (var gpsHash in gpsHashes)
                {
                    MySession.Static.Gpss.SendDelete(playerId, gpsHash);
                }
            }

            foreach (var position in positions)
            {
                var gps = new MyGps();
                gps.Coords = position;
                gps.DisplayName = "CHECKPOINT";
                gps.UpdateHash();

                _gpsHashes.Add(playerId, gps.Hash);
                MySession.Static.Gpss.SendAddGps(playerId, gps, true);
            }
        }

        public void WriteIfNecessary()
        {
            if (!_isDirty) return;

            var gpsHashes = new SerializedGpsHashSet();
            gpsHashes.GpsHashes = _gpsHashes.Values.Flatten().ToSet().ToArray();

            _db.Clear();
            _db.Insert(gpsHashes);
            _db.Write();
        }
    }
}