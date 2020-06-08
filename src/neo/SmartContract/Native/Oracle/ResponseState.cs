using Neo.IO;
using Neo.VM;
using Neo.VM.Types;
using System.Collections.Generic;
using System.Numerics;

namespace Neo.SmartContract.Native.Tokens
{
    public class ResponseState : IInteroperable
    {

        public Dictionary<UInt160, OracleResponse> ResponseHashAndResponseMapping = new Dictionary<UInt160, OracleResponse>();

        public Dictionary<UInt160, UInt160> NodeAndResponseHashMapping = new Dictionary<UInt160, UInt160>();


        public virtual void FromStackItem(StackItem stackItem)
        {
            int responseCount = int.Parse(((Struct)stackItem)[0].GetBigInteger().ToString());
            ResponseHashAndResponseMapping = new Dictionary<UInt160, OracleResponse>();
            for (int i = 1; i < responseCount + 1; i += 2)
            {
                UInt160 responseHash = ((Struct)stackItem)[i].GetSpan().ToArray().AsSerializable<UInt160>();
                OracleResponse response = ((Struct)stackItem)[i + 1].GetSpan().ToArray().AsSerializable<OracleResponse>();
                ResponseHashAndResponseMapping.Add(responseHash, response);
            }
            int nodeCount = int.Parse(((Struct)stackItem)[responseCount + 1].GetBigInteger().ToString());
            ResponseHashAndResponseMapping = new Dictionary<UInt160, OracleResponse>();
            for (int i = responseCount + 2; i < responseCount + nodeCount + 2; i += 2)
            {
                UInt160 node = ((Struct)stackItem)[i].GetSpan().ToArray().AsSerializable<UInt160>();
                UInt160 responseHash = ((Struct)stackItem)[i + 1].GetSpan().ToArray().AsSerializable<UInt160>();
                NodeAndResponseHashMapping.Add(responseHash, responseHash);
            }
        }

        public virtual StackItem ToStackItem(ReferenceCounter referenceCounter)
        {
            Struct @struct = new Struct(referenceCounter);

            BigInteger responseCount = ResponseHashAndResponseMapping.Count;
            @struct.Add(responseCount);
            foreach (KeyValuePair<UInt160, OracleResponse> keyValuePair in ResponseHashAndResponseMapping)
            {
                @struct.Add(keyValuePair.Key.ToArray());
                @struct.Add(keyValuePair.Value.ToArray());
            }
            BigInteger nodeCount = NodeAndResponseHashMapping.Count;
            @struct.Add(nodeCount);
            foreach (KeyValuePair<UInt160, UInt160> keyValuePair in NodeAndResponseHashMapping)
            {
                @struct.Add(keyValuePair.Key.ToArray());
                @struct.Add(keyValuePair.Value.ToArray());
            }
            return @struct;
        }

        public OracleResponse GetConsensusResponse(int MinVote)
        {
            Dictionary<UInt160, int> votes = new Dictionary<UInt160, int>();
            foreach (KeyValuePair<UInt160, UInt160> keyValuePair in NodeAndResponseHashMapping)
            {
                if (votes.ContainsKey(keyValuePair.Value))
                {
                    votes[keyValuePair.Value] = votes[keyValuePair.Value] + 1;
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
            return ResponseHashAndResponseMapping[sortvotes[sortvotes.Count - 1].Key];
        }

        public UInt160[] GetIncentiveAccount(int MinVote)
        {
            OracleResponse response = GetConsensusResponse(MinVote);
            if (response is null) return null;
            UInt160 responseHash = response.Hash;
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
    }
}
