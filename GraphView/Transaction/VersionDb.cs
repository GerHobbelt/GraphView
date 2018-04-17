﻿
namespace GraphView.Transaction
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// This part is for DDL operators
    /// </summary>
    public abstract partial class VersionDb
    {
        public static readonly long RETURN_ERROR_CODE = -2L;

        /// <summary>
        /// Define a delegate type to specify the partition rules.
        /// Which can be re-assigned outside the version db
        /// </summary>
        /// <param name="recordKey">The record key need to be operated</param>
        /// <returns></returns>
        public delegate int PartitionByKeyDelegate(object recordKey);

        /// <summary>
        /// Define a delegate method to specify the partition rules.
        /// </summary>
        public PartitionByKeyDelegate PhysicalPartitionByKey { get; set; }

        /// <summary>
        /// Define the global LogicalParitionByKey function to determine its partition
        /// It's not a method belonging to version table or version db, which shoule be a global partition function
        /// </summary>
        public static PartitionByKeyDelegate LogicalPartitionByKey { get; set; }

        public VersionDb()
        {
            
        }

        /// <summary>
        /// Get a version table instance by tableId, which has different implementations
        /// in different storages, like loading from meta-data database
        /// </summary>
        /// <returns></returns>
        internal virtual VersionTable GetVersionTable(string tableId)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Create a version table if the table doesn't exist, it will be called when the version
        /// insertion finds that the table with tableId doesn't exist
        /// </summary>
        /// <param name="tableId">The specify tabldId</param>
        /// <param name="redisDbIndex">It's only from redis kv storage</param>
        /// <returns>a version table instance</returns>
        internal virtual VersionTable CreateVersionTable(string tableId, long redisDbIndex = 0)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Delete a version table by tableId, it will be called by sql delete
        /// </summary>
        /// <param name="tableId"></param>
        /// <returns></returns>
        internal virtual bool DeleteTable(string tableId)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// This part is the implemetation of version table interfaces
    /// </summary>
    public abstract partial class VersionDb
    {
        internal virtual IEnumerable<VersionEntry> GetVersionList(string tableId, object recordKey)
        {
            VersionTable versionTable = this.GetVersionTable(tableId);
            if (versionTable == null)
            {
                return null;
            }

            return versionTable.GetVersionList(recordKey);
        }

        /// <summary>
        /// Get the version entries by a batch of keys in a version table
        /// </summary>
        /// <param name="batch">A batch of record keys and version keys</returns>
        internal virtual IDictionary<VersionPrimaryKey, VersionEntry> GetVersionEntryByKey(
            string tableId, IEnumerable<VersionPrimaryKey> batch)
        {
            Dictionary<VersionPrimaryKey, VersionEntry> versionDict = 
                new Dictionary<VersionPrimaryKey, VersionEntry>();

            VersionTable versionTable = this.GetVersionTable(tableId);
            if (versionTable == null)
            {
                return null;
            }
            return versionTable.GetVersionEntryByKey(batch);
        }

        internal virtual VersionEntry ReplaceVersionEntry(string tableId, object recordKey, long versionKey,
            long beginTimestamp, long endTimestamp, long txId, long readTxId, long expectedEndTimestamp)
        {
            VersionTable versionTable = this.GetVersionTable(tableId);
            if (versionTable == null)
            {
                return null;
            }

            return versionTable.ReplaceVersionEntry(recordKey, versionKey, 
                beginTimestamp, endTimestamp, txId, readTxId, expectedEndTimestamp);
        }

        internal virtual bool ReplaceWholeVersionEntry(string tableId, object recordKey, long versionKey,
            VersionEntry versionEntry)
        {
            VersionTable versionTable = this.GetVersionTable(tableId);
            if (versionTable == null)
            {
                return false;
            }

            return versionTable.ReplaceWholeVersionEntry(recordKey, versionKey, versionEntry);
        }

        internal virtual bool UploadNewVersionEntry(string tableId, object recordKey, long versionKey, VersionEntry versionEntry)
        {
            VersionTable versionTable = this.GetVersionTable(tableId);
            if (versionTable == null)
            {
                return false;
            }

            return versionTable.UploadNewVersionEntry(recordKey, versionKey, versionEntry);
        }

        internal virtual VersionEntry UpdateVersionMaxCommitTs(string tableId, object recordKey, long versionKey, long commitTime)
        {
            VersionTable versionTable = this.GetVersionTable(tableId);
            if (versionTable == null)
            {
                return null;
            }

            return versionTable.UpdateVersionMaxCommitTs(recordKey, versionKey, commitTime);
        }

        internal virtual VersionEntry GetVersionEntryByKey(string tableId, object recordKey, long versionKey)
        {
            VersionTable versionTable = this.GetVersionTable(tableId);
            if (versionTable == null)
            {
                return null;
            }

            return versionTable.GetVersionEntryByKey(recordKey, versionKey);
        }

        internal virtual bool DeleteVersionEntry(string tableId, object recordKey, long versionKey)
        {
            VersionTable versionTable = this.GetVersionTable(tableId);
            if (versionTable == null)
            {
                return false;
            }

            return versionTable.DeleteVersionEntry(recordKey, versionKey);
        }
        
        internal virtual IEnumerable<VersionEntry> InitializeAndGetVersionList(string tableId, object recordKey)
        {
            VersionTable versionTable = this.GetVersionTable(tableId);
            if (versionTable == null) 
            {
                return null;
            }

            return versionTable.InitializeAndGetVersionList(recordKey);
        }
    }

    public abstract partial class VersionDb
    {
        /// <summary>
        /// Generate an unique txId in the current transaction table and store the initial states in transaction
        /// table entry
        /// It will return the unique txId
        /// </summary>
        /// <returns>transaction id</returns>
        internal virtual long InsertNewTx()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get an TxTableEntey(txId, status, commitTime, commitLowerBound) by TxId
        /// </summary>
        /// <returns>a TxTableEntry</returns>
        internal virtual TxTableEntry GetTxTableEntry(long txId)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Update the transaction's status, set from ongoing => committed or ongoing => aborted
        /// </summary>
        internal virtual void UpdateTxStatus(long txId, TxStatus status)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Try to set the tx's commitTime and return the commitTime
        /// If the proposedCommitTime is greater than or equal to the commitLowerBound, set its commitTime as proposedCommitTime
        /// and return the commitTime
        /// Otherwise return -1 which means the proposedCommitTime is invalid
        /// </summary>
        /// <param name="txId"></param>
        /// <param name="proposalTs"></param>
        /// <returns>-1 or proposedCommitTime</returns>
        internal virtual long SetAndGetCommitTime(long txId, long proposedCommitTime)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Update the commitLowerBound to push Tx has greater commitTime than the lowerBound
        /// lowerBound should be the value commitTime + 1
        /// </summary>
        /// <param name="lowerBound">The current transaction's commit time + 1</param>
        /// <returns>
        /// -2 means there are some errors during the execution phase
        /// -1 means the txId hasn't gotten its commitTime and set the lowerBound successfully
        /// Other values mean txId has already got the commitTime and it returns tx's commitTime
        /// </returns>
        internal virtual long UpdateCommitLowerBound(long txId, long lowerBound)
        {
            throw new NotImplementedException();
        }
    }

    public abstract partial class VersionDb
    {
        /// <summary>
        /// Generate a random long type in the range of [min, max]
        /// </summary>
        /// <returns></returns>
        protected long RandomLong(long min, long max, Random rand)
        {
            byte[] buf = new byte[8];
            rand.NextBytes(buf);
            long longRand = BitConverter.ToInt64(buf, 0);

            return (Math.Abs(longRand % (max - min)) + min);
        }
    }
}
