using Neo.IO.Caching;
using Neo.IO.Data.LevelDB;
using Neo.IO.Wrappers;
using Neo.Ledger;
using System;
using System.Reflection;

namespace Neo.Persistence.LevelDB
{
    public class LevelDBStore : Store, IDisposable
    {
        private readonly DB db;
        private bool changeTheme = false;
        public DbCache<UInt256, TrimmedBlock> blocks;
        public DbCache<UInt160, ContractState> contracts;
        public DbCache<StorageKey, StorageItem> storages;
        public DbCache<UInt256, TransactionState> transactions;
        public DbCache<UInt32Wrapper, HeaderHashList> headerHashList;
        private DbMetaDataCache<HashIndexState> blockHashIndex;
        private DbMetaDataCache<HashIndexState> headerHashIndex;

        public LevelDBStore(string path)
        {
            this.db = DB.Open(path, new Options { CreateIfMissing = true });
            if (db.TryGet(ReadOptions.Default, SliceBuilder.Begin(Prefixes.SYS_Version), out Slice value) && Version.TryParse(value.ToString(), out Version version) && version >= Version.Parse("2.9.1"))
                return;
            WriteBatch batch = new WriteBatch();
            ReadOptions options = new ReadOptions { FillCache = false };
            using (Iterator it = db.NewIterator(options))
            {
                for (it.SeekToFirst(); it.Valid(); it.Next())
                {
                    batch.Delete(it.Key());
                }
            }
            db.Put(WriteOptions.Default, SliceBuilder.Begin(Prefixes.SYS_Version), Assembly.GetExecutingAssembly().GetName().Version.ToString());
            db.Write(WriteOptions.Default, batch);

            if (changeTheme)
            {
                blocks = new DbCache<UInt256, TrimmedBlock>(db, null, null, Prefixes.DATA_Block);
                contracts = new DbCache<UInt160, ContractState>(db, null, null, Prefixes.ST_Contract);
                storages = new DbCache<StorageKey, StorageItem>(db, null, null, Prefixes.ST_Storage);
                transactions = new DbCache<UInt256, TransactionState>(db, null, null, Prefixes.DATA_Transaction);
                headerHashList = new DbCache<UInt32Wrapper, HeaderHashList>(db, null, null, Prefixes.IX_HeaderHashList);
                blockHashIndex = new DbMetaDataCache<HashIndexState>(db, null, null, Prefixes.IX_CurrentBlock);
                headerHashIndex = new DbMetaDataCache<HashIndexState>(db, null, null, Prefixes.IX_CurrentHeader);
            }
        }

        public void Dispose()
        {
            db.Dispose();
        }

        public override byte[] Get(byte[] key)
        {
            if (!db.TryGet(ReadOptions.Default, key, out Slice slice))
                return null;
            return slice.ToArray();
        }

        public override DataCache<UInt256, TrimmedBlock> GetBlocks()
        {
            if (changeTheme)
                return blocks;
            else
                return new DbCache<UInt256, TrimmedBlock>(db, null, null, Prefixes.DATA_Block);
        }

        public override DataCache<UInt160, ContractState> GetContracts()
        {
            if (changeTheme)
                return contracts;
            else
                return new DbCache<UInt160, ContractState>(db, null, null, Prefixes.ST_Contract);
        }

        public override Snapshot GetSnapshot()
        {
            return new DbSnapshot(db);
        }

        public override DataCache<StorageKey, StorageItem> GetStorages()
        {
            if (changeTheme)
                return storages;
            else
                return new DbCache<StorageKey, StorageItem>(db, null, null, Prefixes.ST_Storage);
        }

        public override DataCache<UInt256, TransactionState> GetTransactions()
        {
            if (changeTheme)
                return transactions;
            else
                return new DbCache<UInt256, TransactionState>(db, null, null, Prefixes.DATA_Transaction);
        }

        public override DataCache<UInt32Wrapper, HeaderHashList> GetHeaderHashList()
        {
            if (changeTheme)
                return headerHashList;
            else
                return new DbCache<UInt32Wrapper, HeaderHashList>(db, null, null, Prefixes.IX_HeaderHashList);
        }

        public override MetaDataCache<HashIndexState> GetBlockHashIndex()
        {
            if (changeTheme)
                return blockHashIndex;
            else
                return new DbMetaDataCache<HashIndexState>(db, null, null, Prefixes.IX_CurrentBlock);
        }

        public override MetaDataCache<HashIndexState> GetHeaderHashIndex()
        {
            if (changeTheme)
                return headerHashIndex;
            else
                return new DbMetaDataCache<HashIndexState>(db, null, null, Prefixes.IX_CurrentHeader);
        }

        public override void Put(byte[] key, byte[] value)
        {
            db.Put(WriteOptions.Default, key, value);
        }

        public override void PutSync(byte[] key, byte[] value)
        {
            db.Put(new WriteOptions { Sync = true }, key, value);
        }
    }
}
