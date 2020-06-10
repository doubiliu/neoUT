using Neo.IO;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract.Iterators;
using Neo.VM;
using Neo.VM.Types;
using System.Collections.Generic;
using System.Numerics;

namespace Neo.SmartContract.Native.Tokens
{
    public class ResponseState : IInteroperable
    {
        public Dictionary<UInt160, UInt256> NodeAndTxIdHashMapping = new Dictionary<UInt160, UInt256>();
        public Dictionary<UInt160, UInt160> NodeAndResponseHashMapping = new Dictionary<UInt160, UInt160>();

        public virtual void FromStackItem(StackItem stackItem)
        {
            Struct @struct = (Struct)stackItem;
            MapWrapper NodeAndTxIdHash = new MapWrapper((Map)@struct[0]);
            while (NodeAndTxIdHash.Next()) {
                NodeAndTxIdHashMapping.Add(NodeAndTxIdHash.Key().GetSpan().AsSerializable<UInt160>(), NodeAndTxIdHash.Value().GetSpan().AsSerializable<UInt256>());
            }
            MapWrapper NodeAndResponseHash = new MapWrapper((Map)@struct[1]);
            while (NodeAndResponseHash.Next())
            {
                NodeAndResponseHashMapping.Add(NodeAndResponseHash.Key().GetSpan().AsSerializable<UInt160>(), NodeAndResponseHash.Value().GetSpan().AsSerializable<UInt160>());
            }
        }

        public virtual StackItem ToStackItem(ReferenceCounter referenceCounter)
        {
            Struct @struct = new Struct(referenceCounter);
            Map NodeAndTxIdHash = new Map(referenceCounter);
            foreach (KeyValuePair<UInt160, UInt256> entry in NodeAndTxIdHashMapping)
            {
                NodeAndTxIdHash[new ByteString(entry.Key.ToArray())]= new ByteString(entry.Value.ToArray());
            }
            @struct.Add(NodeAndTxIdHash);
            Map NodeAndResponseHash = new Map(referenceCounter);
            foreach (KeyValuePair<UInt160, UInt160> entry in NodeAndResponseHashMapping)
            {
                NodeAndTxIdHash[new ByteString(entry.Key.ToArray())]=new ByteString(entry.Value.ToArray());
            }
            @struct.Add(NodeAndResponseHash);
            return @struct;
        }

        public UInt160 GetConsensusResponseHash(int MinVote)
        {
            Dictionary<UInt160, int> votes = new Dictionary<UInt160, int>();
            foreach (KeyValuePair<UInt160, UInt160> keyValuePair in NodeAndResponseHashMapping)
            {
                if (votes.ContainsKey(keyValuePair.Value))
                {
                    votes[keyValuePair.Value] += 1;
                }
                else
                {
                    votes.Add(keyValuePair.Value, 1);
                }
            }
            List<KeyValuePair<UInt160, int>> sortvotes = new List<KeyValuePair<UInt160, int>>(votes);
            sortvotes.Sort((s1, s2) => { return s1.Value.CompareTo(s2.Value); });
            if (sortvotes.Count == 0) return null;
            if (sortvotes[sortvotes.Count - 1].Value < MinVote) return null;

            return sortvotes[sortvotes.Count - 1].Key;
        }

        public UInt160[] GetIncentiveAccount(UInt160 responseHash)
        {
            List<UInt160> accounts = new List<UInt160>();
            foreach (KeyValuePair<UInt160, UInt160> keyValuePair in NodeAndResponseHashMapping)
            {
                if (keyValuePair.Value == responseHash)
                {
                    accounts.Add(keyValuePair.Key);
                }
            }
            return accounts.ToArray();
        }

        public UInt256 GetTransactionId(UInt160 responseHash)
        {
            foreach(KeyValuePair<UInt160, UInt160> keyValuePair in NodeAndResponseHashMapping) {
                if (keyValuePair.Value == responseHash)
                {
                    return NodeAndTxIdHashMapping[keyValuePair.Key];
                }
            }
            return null;
        }
    }
}
