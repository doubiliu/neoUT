using Akka.Actor;
using Akka.Event;
using Akka.IO;
using System;
using System.Net;
using System.Net.WebSockets;
using System.Threading;

namespace Neo.Network.P2P
{
    public abstract class Connection : UntypedActor
    {
        internal class Timer { public static Timer Instance = new Timer(); }
        internal class Ack : Tcp.Event { public static Ack Instance = new Ack(); }

        /// <summary>
        /// connection initial timeout (in seconds) before any package has been accepted
        /// </summary>
        private const int connectionTimeoutLimitStart = 10;
        /// <summary>
        /// connection timeout (in seconds) after every `OnReceived(ByteString data)` event
        /// </summary>
        private const int connectionTimeoutLimit = 60;

        public IPEndPoint Remote { get; }
        public IPEndPoint Local { get; }

        public static long totalTcpReceiveCount = 0;
        public static long totalTcpSendCount = 0;
        public long tcpReceiveCount = 0;
        public long tcpSendCount = 0;
        protected ILoggingAdapter Log { get; } = Context.GetLogger();
        private ICancelable tps_timer;
        internal class TPSTimer { }

        private ICancelable timer;
        private readonly IActorRef tcp;
        private readonly WebSocket ws;
        private bool disconnected = false;
        protected Connection(object connection, IPEndPoint remote, IPEndPoint local)
        {
            this.Remote = remote;
            this.Local = local;
            this.timer = Context.System.Scheduler.ScheduleTellOnceCancelable(TimeSpan.FromSeconds(connectionTimeoutLimitStart), Self, Timer.Instance, ActorRefs.NoSender);
            this.tps_timer = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(15), Self, new TPSTimer(), Self);
            switch (connection)
            {
                case IActorRef tcp:
                    this.tcp = tcp;
                    break;
                case WebSocket ws:
                    this.ws = ws;
                    WsReceive();
                    break;
            }
        }

        private void WsReceive()
        {
            byte[] buffer = new byte[512];
            ArraySegment<byte> segment = new ArraySegment<byte>(buffer);
            ws.ReceiveAsync(segment, CancellationToken.None).PipeTo(Self,
                success: p =>
                {
                    switch (p.MessageType)
                    {
                        case WebSocketMessageType.Binary:
                            return new Tcp.Received(ByteString.FromBytes(buffer, 0, p.Count));
                        case WebSocketMessageType.Close:
                            return Tcp.PeerClosed.Instance;
                        default:
                            ws.Abort();
                            return Tcp.Aborted.Instance;
                    }
                },
                failure: ex => new Tcp.ErrorClosed(ex.Message));
        }

        public void Disconnect(bool abort = false)
        {
            disconnected = true;
            if (tcp != null)
            {
                tcp.Tell(abort ? (Tcp.CloseCommand)Tcp.Abort.Instance : Tcp.Close.Instance);
            }
            else
            {
                ws.Abort();
            }
            Context.Stop(Self);
        }

        protected virtual void OnAck()
        {
        }

        protected abstract void OnData(ByteString data);

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case Timer _:
                    Disconnect(true);
                    break;
                case Ack _:
                    OnAck();
                    break;
                case Tcp.Received received:
                    totalTcpReceiveCount++;
                    tcpReceiveCount++;
                    OnReceived(received.Data);
                    break;
                case Tcp.ConnectionClosed _:
                    Context.Stop(Self);
                    break;
                case TPSTimer _:
                    ReportTCPTps();
                    break;
            }
        }

        private void ReportTCPTps()
        {
            Log.Info($"tcp receive count of single connection in this 15s: {tcpReceiveCount}, TPS = {tcpReceiveCount / 15}");
            Log.Info($"tcp send count of single connection in this 15s: {tcpSendCount}, TPS = {tcpSendCount / 15}");
            tcpReceiveCount = 0;
            tcpSendCount = 0;

        }

        private void OnReceived(ByteString data)
        {
            timer.CancelIfNotNull();
            timer = Context.System.Scheduler.ScheduleTellOnceCancelable(TimeSpan.FromSeconds(connectionTimeoutLimit), Self, Timer.Instance, ActorRefs.NoSender);
            try
            {
                OnData(data);
            }
            catch
            {
                Disconnect(true);
            }
        }

        protected override void PostStop()
        {
            if (!disconnected)
                tcp?.Tell(Tcp.Close.Instance);
            timer.CancelIfNotNull();
            ws?.Dispose();
            base.PostStop();
        }

        protected void SendData(ByteString data)
        {
            if (tcp != null)
            {
                tcp.Tell(Tcp.Write.Create(data, Ack.Instance));
                totalTcpSendCount++;
                tcpSendCount++;
            }
            else
            {
                ArraySegment<byte> segment = new ArraySegment<byte>(data.ToArray());
                ws.SendAsync(segment, WebSocketMessageType.Binary, true, CancellationToken.None).PipeTo(Self,
                    success: () => Ack.Instance,
                    failure: ex => new Tcp.ErrorClosed(ex.Message));
            }
        }
    }
}
