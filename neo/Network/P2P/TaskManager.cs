using Akka.Actor;
using Akka.Configuration;
using Neo.IO.Actors;
using Neo.IO.Caching;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Neo.Network.P2P
{
    internal class TaskManager : UntypedActor
    {
        public class Register { public VersionPayload Version; }
        public class NewTasks { public InvPayload Payload; }
        public class TaskCompleted { public UInt256 Hash; }
        public class HeaderTaskCompleted { }
        public class RestartTasks { public InvPayload Payload; }
        private class Timer { }

        private static readonly TimeSpan TimerInterval = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan TaskTimeout = TimeSpan.FromMinutes(1);

        private readonly NeoSystem system;
        private const int MaxConncurrentTasks = 3;
        private readonly FIFOSet<UInt256> knownHashes;
        private readonly Dictionary<UInt256, int> globalTasks = new Dictionary<UInt256, int>();
        private readonly Dictionary<IActorRef, TaskSession> sessions = new Dictionary<IActorRef, TaskSession>();
        private readonly ICancelable timer = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(TimerInterval, TimerInterval, Context.Self, new Timer(), ActorRefs.NoSender);

        private readonly UInt256 HeaderTaskHash = UInt256.Zero;
        private bool HasHeaderTask => globalTasks.ContainsKey(HeaderTaskHash);
        public static int invTxCount = 0;
        public static int txReceiptFinishCount = 0;
        public static System.Diagnostics.Stopwatch stopwatch1 = new System.Diagnostics.Stopwatch();
        public static System.Diagnostics.Stopwatch stopwatch2 = new System.Diagnostics.Stopwatch();
        public static System.Diagnostics.Stopwatch stopwatch3 = new System.Diagnostics.Stopwatch();
        public static System.Diagnostics.Stopwatch stopwatch4 = new System.Diagnostics.Stopwatch();
        public static System.Diagnostics.Stopwatch stopwatch5 = new System.Diagnostics.Stopwatch();
        public static System.Diagnostics.Stopwatch stopwatch6 = new System.Diagnostics.Stopwatch();
        public static System.Diagnostics.Stopwatch stopwatch7 = new System.Diagnostics.Stopwatch();
        public static System.Diagnostics.Stopwatch stopwatch8 = new System.Diagnostics.Stopwatch();
        public static System.Diagnostics.Stopwatch stopwatch9 = new System.Diagnostics.Stopwatch();
        public static System.Diagnostics.Stopwatch stopwatch10 = new System.Diagnostics.Stopwatch();

        public TaskManager(NeoSystem system)
        {
            this.system = system;
            this.knownHashes = new FIFOSet<UInt256>(Blockchain.Singleton.MemPool.Capacity * 2);
        }

        private void OnHeaderTaskCompleted()
        {
            if (!sessions.TryGetValue(Sender, out TaskSession session))
                return;
            session.Tasks.Remove(HeaderTaskHash);
            DecrementGlobalTask(HeaderTaskHash);
            RequestTasks(session);
        }

        private void OnNewTasks(InvPayload payload)
        {
            if (payload.Type == InventoryType.TX) invTxCount++;
            try
            {
                stopwatch1.Start();
                if (!sessions.TryGetValue(Sender, out TaskSession session))
                    return;
                stopwatch1.Stop();
                stopwatch2.Start();
                if (payload.Type == InventoryType.TX && Blockchain.Singleton.Height < Blockchain.Singleton.HeaderHeight)
                {
                    RequestTasks(session);
                    return;
                }
                stopwatch2.Stop();
                stopwatch3.Start();
                HashSet<UInt256> hashes = new HashSet<UInt256>(payload.Hashes);
                stopwatch3.Stop();
                stopwatch4.Start();
                hashes.RemoveWhere(q => knownHashes.Contains(q));
                //hashes.ExceptWith(knownHashes);
                
                if (payload.Type == InventoryType.Block)
                    session.AvailableTasks.UnionWith(hashes.Where(p => globalTasks.ContainsKey(p)));


                //hashes.ExceptWith(globalTasks.Keys);
                hashes.RemoveWhere(q => globalTasks.Keys.Contains(q));
                if (hashes.Count == 0)
                {
                    RequestTasks(session);
                    return;
                }
                stopwatch4.Stop();
                stopwatch5.Start();

                foreach (UInt256 hash in hashes)
                {
                    IncrementGlobalTask(hash);
                    session.Tasks[hash] = DateTime.UtcNow;
                }
                stopwatch5.Stop();
                stopwatch6.Start();

                foreach (InvPayload group in InvPayload.CreateGroup(payload.Type, hashes.ToArray()))
                    Sender.Tell(Message.Create(MessageCommand.GetData, group));
                stopwatch6.Stop();
            }
            finally
            {
                stopwatch1.Stop();
                stopwatch2.Stop();
                stopwatch3.Stop();
                stopwatch4.Stop();
                stopwatch5.Stop();
                stopwatch6.Stop();
            }
        }

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case Register register:
                    OnRegister(register.Version);
                    break;
                case NewTasks tasks:
                    OnNewTasks(tasks.Payload);
                    break;
                case TaskCompleted completed:
                    OnTaskCompleted(completed.Hash);
                    break;
                case HeaderTaskCompleted _:
                    OnHeaderTaskCompleted();
                    break;
                case RestartTasks restart:
                    OnRestartTasks(restart.Payload);
                    break;
                case Timer _:
                    OnTimer();
                    break;
                case Terminated terminated:
                    OnTerminated(terminated.ActorRef);
                    break;
            }
        }

        private void OnRegister(VersionPayload version)
        {
            Context.Watch(Sender);
            TaskSession session = new TaskSession(Sender, version);
            sessions.Add(Sender, session);
            RequestTasks(session);
        }

        private void OnRestartTasks(InvPayload payload)
        {
            knownHashes.ExceptWith(payload.Hashes);
            foreach (UInt256 hash in payload.Hashes)
                globalTasks.Remove(hash);
            foreach (InvPayload group in InvPayload.CreateGroup(payload.Type, payload.Hashes))
                system.LocalNode.Tell(Message.Create(MessageCommand.GetData, group));
        }

        private void OnTaskCompleted(UInt256 hash)
        {
            txReceiptFinishCount++;
            stopwatch7.Start();
            knownHashes.Add(hash);
            globalTasks.Remove(hash);
            stopwatch7.Stop();
            stopwatch8.Start();
            foreach (TaskSession ms in sessions.Values)
                ms.AvailableTasks.Remove(hash);
            stopwatch8.Stop();
            if (sessions.TryGetValue(Sender, out TaskSession session))
            {
                stopwatch9.Start();
                session.Tasks.Remove(hash);
                stopwatch9.Stop();
                stopwatch10.Start();
                RequestTasks(session);
                stopwatch10.Stop();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecrementGlobalTask(UInt256 hash)
        {
            if (globalTasks.ContainsKey(hash))
            {
                if (globalTasks[hash] == 1)
                    globalTasks.Remove(hash);
                else
                    globalTasks[hash]--;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IncrementGlobalTask(UInt256 hash)
        {
            if (!globalTasks.ContainsKey(hash))
            {
                globalTasks[hash] = 1;
                return true;
            }
            if (globalTasks[hash] >= MaxConncurrentTasks)
                return false;

            globalTasks[hash]++;

            return true;
        }

        private void OnTerminated(IActorRef actor)
        {
            if (!sessions.TryGetValue(actor, out TaskSession session))
                return;
            sessions.Remove(actor);
            foreach (UInt256 hash in session.Tasks.Keys)
                DecrementGlobalTask(hash);
        }

        private void OnTimer()
        {
            foreach (TaskSession session in sessions.Values)
                foreach (var task in session.Tasks.ToArray())
                    if (DateTime.UtcNow - task.Value > TaskTimeout)
                    {
                        if (session.Tasks.Remove(task.Key))
                            DecrementGlobalTask(task.Key);
                    }
            foreach (TaskSession session in sessions.Values)
                RequestTasks(session);
        }

        protected override void PostStop()
        {
            timer.CancelIfNotNull();
            base.PostStop();
        }

        public static Props Props(NeoSystem system)
        {
            return Akka.Actor.Props.Create(() => new TaskManager(system)).WithMailbox("task-manager-mailbox");
        }

        private void RequestTasks(TaskSession session)
        {
            if (session.HasTask) return;
            if (session.AvailableTasks.Count > 0)
            {
                //session.AvailableTasks.ExceptWith(knownHashes);
                session.AvailableTasks.RemoveWhere(q => knownHashes.Contains(q));
                session.AvailableTasks.RemoveWhere(p => Blockchain.Singleton.ContainsBlock(p));
                HashSet<UInt256> hashes = new HashSet<UInt256>(session.AvailableTasks);
                if (hashes.Count > 0)
                {
                    foreach (UInt256 hash in hashes.ToArray())
                    {
                        if (!IncrementGlobalTask(hash))
                            hashes.Remove(hash);
                    }
                    session.AvailableTasks.ExceptWith(hashes);
                    foreach (UInt256 hash in hashes)
                        session.Tasks[hash] = DateTime.UtcNow;
                    foreach (InvPayload group in InvPayload.CreateGroup(InventoryType.Block, hashes.ToArray()))
                        session.RemoteNode.Tell(Message.Create(MessageCommand.GetData, group));
                    return;
                }
            }
            if ((!HasHeaderTask || globalTasks[HeaderTaskHash] < MaxConncurrentTasks) && Blockchain.Singleton.HeaderHeight < session.StartHeight)
            {
                session.Tasks[HeaderTaskHash] = DateTime.UtcNow;
                IncrementGlobalTask(HeaderTaskHash);
                session.RemoteNode.Tell(Message.Create(MessageCommand.GetHeaders, GetBlocksPayload.Create(Blockchain.Singleton.CurrentHeaderHash)));
            }
            else if (Blockchain.Singleton.Height < session.StartHeight)
            {
                UInt256 hash = Blockchain.Singleton.CurrentBlockHash;
                for (uint i = Blockchain.Singleton.Height + 1; i <= Blockchain.Singleton.HeaderHeight; i++)
                {
                    hash = Blockchain.Singleton.GetBlockHash(i);
                    if (!globalTasks.ContainsKey(hash))
                    {
                        hash = Blockchain.Singleton.GetBlockHash(i - 1);
                        break;
                    }
                }
                session.RemoteNode.Tell(Message.Create(MessageCommand.GetBlocks, GetBlocksPayload.Create(hash)));
            }
        }
    }

    internal class TaskManagerMailbox : PriorityMailbox
    {
        public TaskManagerMailbox(Akka.Actor.Settings settings, Config config)
            : base(settings, config)
        {
        }

        internal protected override bool IsHighPriority(object message)
        {
            switch (message)
            {
                case TaskManager.Register _:
                case TaskManager.RestartTasks _:
                    return true;
                case TaskManager.NewTasks tasks:
                    if (tasks.Payload.Type == InventoryType.Block || tasks.Payload.Type == InventoryType.Consensus)
                        return true;
                    return false;
                default:
                    return false;
            }
        }
    }
}
