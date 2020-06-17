using Akka.Actor;
using Akka.Event;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Plugins;

namespace Neo.Oracle
{
    public class OracleService : UntypedActor
    {
        private NeoSystem system;

        public OracleService(NeoSystem system)
        {
            this.system = system;
            Context.System.EventStream.Subscribe<Blockchain.RelayResult>(Self);
        }

        /// <summary>
        /// Receive AKKA Messages
        /// </summary>
        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case Blockchain.RelayResult rr when rr.Result == VerifyResult.Succeed:
                    switch (rr.Inventory)
                    {
                        case OraclePayload msg:
                            foreach (IP2PPlugin plugin in Plugin.P2PPlugins)
                                if (!plugin.OnOracleMessage(msg))
                                    return;
                            break;
                    }
                    break;
            }
        }

        public static Props Props(NeoSystem system)
        {
            return Akka.Actor.Props.Create(() => new OracleService(system));
        }
    }
}
