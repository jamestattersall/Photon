namespace ProtonImport.Proton
{
    
    internal class IndexReader : IDisposable
    {
        private readonly Proton.Index _index = new();
        private record IndexInfo(byte KeyLength, int StartIndexId );

        private readonly static Dictionary<short, IndexInfo> KeyEntityInfos  = [];
        private readonly static Dictionary<short, IndexInfo> IndexInfos = [];



        static IndexReader()
        {
            using Proton.EntityDef entityDef = new();
            using Proton.IndexDef indexDef = new();
            for(short i = 1; i<=indexDef.NPages; i++)
            {
                if (indexDef.MoveToPage(i))
                {
                    IndexInfos.Add(i, new IndexInfo(
                        indexDef.KeyLength,
                        indexDef.IndexIdStart
                        )
                    );
                }
            }

            for(short i = 1 ; i<=entityDef.NPages; i++)
            {
                if (entityDef.MoveToPage(i))
                {
                    var ed = entityDef.KeyIndexDefId;
                    if (IndexInfos.TryGetValue(ed, out IndexInfo? indexInfo))
                    {
                        KeyEntityInfos.Add(i, indexInfo);
                    }
                }
            }
        }


        // key -> entity id, per entity type; each built on first lookup for that type
        private readonly Dictionary<short, Dictionary<string, int>> _keyMaps = [];

        public int GetEntityId(short entityTypeId, string key)
        {
            if (!_keyMaps.TryGetValue(entityTypeId, out var map))
            {
                map = LoadKeyMap(entityTypeId);
                _keyMaps.Add(entityTypeId, map);
            }
            return map.GetValueOrDefault(key);
        }

        /// <summary>
        /// Reads the whole key index for an entity type once. The first entry for a key wins,
        /// matching the order Proton stores them in.
        /// </summary>
        private Dictionary<string, int> LoadKeyMap(short entityTypeId)
        {
            Dictionary<string, int> map = new(StringComparer.OrdinalIgnoreCase);
            if (KeyEntityInfos.TryGetValue(entityTypeId, out var entInfo) &&
                _index.MoveToPage(entInfo.StartIndexId))
            {
                _index.SetBlockLength(entInfo.KeyLength);
                while (_index.MoveToNextBlock())
                {
                    map.TryAdd(_index.KeyText, _index.EntityId);
                }
            }
            return map;
        }

        public void Dispose()
        {
            _index.Dispose();
        }
    }
}


