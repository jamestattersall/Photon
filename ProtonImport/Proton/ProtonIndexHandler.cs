namespace ProtonConsole2.Proton
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


        public int GetEntityId(short entityTypeId, string key)
        {
            var entInfo = KeyEntityInfos[entityTypeId];
            if (_index.MoveToPage(entInfo.StartIndexId))
            {
                _index.SetBlockLength(entInfo.KeyLength);

                while (string.Compare(_index.KeyText, key, true) < 0)
                {
                    if (!_index.MoveToNextPage()) return 0;
                }

                _index.MoveToPreviousPage();

                while (string.Compare(_index.KeyText, key, true) < 0)
                {
                    if (!_index.MoveToNextBlock()) return 0;
                }
                if (_index.KeyText.Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    return _index.EntityId;
                }
            }            

            return 0;
        }

        public void Dispose()
        {
            _index.Dispose();
        }
    }
}


