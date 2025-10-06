using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;


namespace RetryWebSocket
{
    public partial class RetryWebSocket
    {
        /* the OnOpen and OnMessage events are called in a random thread with the
         * internal lock held, so don't do too much in them.  For simplicity,
         * the OnError and OnClose events are always called in Unity's main thread. */
        public Action<object, EventArgs> OnOpen;
        public Action<object, CloseEventArgs> OnClose;
        public Action<object, ErrorEventArgs> OnError;
        public Action<object, MessageEventArgs> OnMessage;

        
        /* Start connecting.  You can call the .Send() methods immediately afterwards,
         * which will queue messages if the connection is not done yet or when
         * reconnecting later.  Should be called from Unity's main thread. */
        public void Connect(string url_base)
        {
            if (OnMessage == null)
                throw new InvalidOperationException("RetryWebSocket.OnMessage should be set");

            rws_url = url_base + (url_base.Contains("?") ? "&" : "?") + "rws=";

            lock (my_lock)
            {
                if (status != Status.AttemptInitial || current_ws != null || out_queue != null)
                    throw new InvalidOperationException("RetryWebSocket already started");
                LockedOpenWs();
                out_queue = new Queue<Msg>();
                AfterConnecting();
            }
        }

        /* Send text data.  The first character must not be '@'.  Can be called from any
         * thread after we know that Connect() was called.  If the connection is already
         * closed, the message is silently dropped.  This is to be consistent with what
         * occurs if the connection is not closed yet, but will soon close and this
         * message won't be sent. */
        public void Send(string data)
        {
            SendMessage(Encoding.UTF8.GetBytes(data), WebSocketMessageType.Text);
        }

        /* Send one or several binary messages, splitting it up in pieces if it's big.
         * Can be called from any thread after we know that Connect() was called.
         * There is internal locking to guarantee that the pieces are not interleaved
         * with messages send from another thread. */
        public void SendInPieces(byte[] rawdata)
        {
            /* I suspect that various timeouts are reached if we send
               too large packets.  That's nonsense, but at least it
               doesn't cause any issue if we send it in pieces. */
            if (rawdata.Length <= 30 * 1024)
            {
                SendMessage(rawdata, WebSocketMessageType.Binary);
            }
            else
            {
                /* I also suspect that there is a bug in ClientWebSocket.SendAsync()
                 * with a non-trivial ArraySegment.  We're seeing messages that are
                 * 20 bytes off immediately after the 32768 mark (the second segment
                 * repeats the last 20 bytes of the first segment).  Trying to do block
                 * copying instead, also in 24KB slices instead of 32KB.  This could be
                 * the cause of many of the various error reports on Sentry. */
                int start = 0;
                byte[] block = new byte[24 * 1024];
                lock (my_lock)
                {
                    while (start < rawdata.Length)
                    {
                        int size1 = rawdata.Length - start;
                        if (size1 >= 24 * 1024)
                            size1 = 24 * 1024;
                        else
                            block = new byte[size1];
                        Buffer.BlockCopy(rawdata, start, block, 0, size1);
                        SendMessage(block, WebSocketMessageType.Binary);
                        start += size1;
                    }
                }
            }
        }

        /* Mark the end of the connection.  Can be called at any time from any thread.
         * If Connect() was called, then OnClose() will be called once.  We do this
         * even if OnOpen() was never called because the connection was attempted but
         * never succeeded. */
        public void Close()
        {
            CloseDefinitely("close()", error: false);
        }

        public bool IsClosed => status == Status.Closed;


        /********************************************************************/

        
        const string VERSION_INFO = "1";
        const int TIMEOUT = 10;   /* seconds, approximately */

        struct Msg { public byte[] rawdata; public WebSocketMessageType type; }

        string rws_url;
#if UNITY_EDITOR
        internal WebSocket current_ws;
#else
        WebSocket current_ws;
#endif

        enum Status { AttemptInitial, Connected, AttemptReconnect, Closed }
        Status status = Status.AttemptInitial;
        string retry_id = VERSION_INFO;
        int timer_remaining, reconnect_attempts, max_reconnect_attempts;
        int out_base;
        Queue<Msg> out_queue;
        int in_recv = 0;

        readonly object my_lock = new object();

        
        void Th_RunTimer()
        {
            while (true)
            {
                Thread.Sleep(2000);

                lock (my_lock)
                {
                    if (!(status == Status.Connected || status == Status.AttemptReconnect))
                        break;

                    timer_remaining -= 2;
                    if (timer_remaining <= 2)
                    {
                        timer_remaining = TIMEOUT;
                        LockedCloseWs();
#if UNITY_EDITOR
                        stopSending = stopReceiving = false;
#endif
                        if (status == Status.Connected)
                        {
                            status = Status.AttemptReconnect;
                            reconnect_attempts = 1;
                        }
                        else
                        {
                            reconnect_attempts += 1;
                            if (reconnect_attempts > max_reconnect_attempts)
                            {
                                CloseDefinitely($"give up auto-reconnecting after {max_reconnect_attempts} attempts", error: true);
                                return;
                            }
                        }
                        try
                        {
                            LockedOpenWs();
                        }
                        catch (Exception e)
                        {
                            DebugLog($"reconnection failure, will try again: {e}");
                            timer_remaining -= 2;
                        }
                    }
                    else if (status == Status.Connected && !stopSending)
                    {
                        try
                        {
                            current_ws.Send($"@{in_recv}");
                        }
                        catch
                        {
                            /* can't send the ACK, ignore and assume it's because the websocket
                             * is half-dead and we'll try to reconnect anyway */
                        }
                    }
                }
            }
        }

        void LockedCloseWs()
        {
            current_ws?.CloseAsync();
            current_ws = null;
        }

        void LockedGotAck(int n)
        {
            if (n < out_base)
            {
                CloseDefinitely("bogus ACK (too small)", error: true);
                return;
            }
            while (n > out_base)
            {
                try
                {
                    out_queue.Dequeue();
                }
                catch (InvalidOperationException)
                {
                    CloseDefinitely("bogus ACK (too large)", error: true);
                    return;
                }
                out_base += 1;
            }
        }

        void LockedOpenWs()
        {
            string url = rws_url + retry_id;
            var ws = new WebSocket(url);
            DebugLog($"RetryWebSocket connecting to {url}");
            current_ws = ws;

            void OnClose(object _, CloseEventArgs args)
            {
                if (current_ws == ws)
                {
                    current_ws = null;
                    if (status == Status.Connected)
                    {
                        status = Status.AttemptReconnect;
                        timer_remaining = 0;   /* try to reconnect quickly */
                        reconnect_attempts = 0;
                    }
                    else if (status != Status.AttemptReconnect)
                    {
                        CloseDefinitely("cannot connect to server", error: true);
                    }
                    else
                    {
                        DebugLog("websocket reconnecting failed, will continue to try");
                    }
                }
            }
            ws.OnClose = OnClose;

            void OnMessage(object _, MessageEventArgs args)
            {
                if (stopReceiving)
                {
                    if (args.Type == Opcode.Text)
                        DebugLog($"dropped incoming message: {args.Data}");
                    else
                        DebugLog($"dropped incoming message of size {args.RawDataSize}");
                    return;
                }
                lock (my_lock)
                {
                    if (current_ws == ws)
                    {
                        timer_remaining = TIMEOUT;
                        if (args.Type == Opcode.Text && args.Data.StartsWith("@"))
                        {
                            string msg = args.Data.Substring(1);
                            if (int.TryParse(msg, out int n))
                                LockedGotAck(n);
                            else
                                LockedHandleMetaMessage(msg);
                        }
                        else
                        {
                            in_recv += 1;
                            this.OnMessage(this, args);
                        }
                    }
                }
            }
            ws.OnMessage = OnMessage;

            ws.ConnectAsync();
        }

        [Serializable]
        class JMessage
        {
            public string cmd, rws, msg;
            public double gtimeout;
            public int ack;
        }

        void LockedHandleMetaMessage(string json)
        {
            DebugLog($"RetryWebSocket internal message: {json}");
            var cmd = ParseJMessage(json);
            switch (cmd.cmd)
            {
                case "hello":
                    if (status != Status.AttemptInitial)
                    {
                        CloseDefinitely("unexpected 'hello'", error: true);
                        return;
                    }
                    if (string.IsNullOrEmpty(cmd.rws))
                    {
                        CloseDefinitely("'hello' message with missing 'rws'", error: true);
                        return;
                    }
                    retry_id = cmd.rws;
                    max_reconnect_attempts = (int)(cmd.gtimeout / TIMEOUT);
                    out_base = 0;
                    in_recv = 0;
                    foreach (var msg in out_queue)
                        current_ws.Send(msg.rawdata, msg.type);
                    timer_remaining = TIMEOUT;
                    status = Status.Connected;

                    var th = new Thread(Th_RunTimer) { IsBackground = true };
                    th.Start();

                    this.OnOpen?.Invoke(this, new EventArgs { });
                    break;

                case "reconnect":
                    if (status != Status.AttemptReconnect)
                    {
                        CloseDefinitely("unexpected 'reconnect'", error: true);
                        return;
                    }
                    LockedGotAck(cmd.ack);
                    current_ws.Send($"@{in_recv}");
                    foreach (var msg in out_queue)
                        current_ws.Send(msg.rawdata, msg.type);
                    timer_remaining = TIMEOUT;
                    status = Status.Connected;
                    break;

                case "close":
                    CloseDefinitely($"server-side close ({cmd.msg})", error: false);
                    break;
            }
        }

        string _got_error_msg;

        void CloseDefinitely(string msg, bool error)
        {
            lock (my_lock)
            {
                if (status == Status.Closed)
                    return;

                _got_error_msg = error ? (msg ?? "error") : null;
                current_ws?.Send("@-1");
                status = Status.Closed;
                DebugLog($"RetryWebSocket closing: {msg}");
                LockedCloseWs();
                out_queue = null;
                AfterDisconnectingInSomeThread();
            }
        }

        void InvokeErrorAndClose()
        {
            string msg = _got_error_msg;
            if (msg != null)
                this.OnError?.Invoke(this, new ErrorEventArgs { Message = msg });
            this.OnClose?.Invoke(this, new CloseEventArgs { });
        }

        void ApplicationIsQuitting()
        {
            lock (my_lock)
            {
                current_ws?.Send("@-1");
                current_ws?.CloseAsync();
                status = Status.Closed;
            }
        }

        /* Direct interface to send one websocket message.  Can be used if you already have
         * utf-8-encoded text, for example. */
        public void SendMessage(byte[] rawdata, WebSocketMessageType type)
        {
            if (type == WebSocketMessageType.Text && rawdata.Length > 0 && rawdata[0] == (byte)'@')
                throw new InvalidOperationException("cannot Send() a text message starting with '@'");

            /* synchronous send */
            lock (my_lock)
            {
                switch (status)
                {
                    case Status.Connected:
                        if (!stopSending)
                            current_ws.Send(rawdata, type);
                        break;

                    case Status.Closed:
                        return;

                    case Status.AttemptInitial:
                        if (out_queue == null)
                            throw new InvalidOperationException("Send() called before Connect()");
                        break;
                }
                out_queue.Enqueue(new Msg { rawdata = rawdata, type = type });
            }
        }

#if UNITY_EDITOR
        public bool stopSending, stopReceiving;
#else
        const bool stopSending = false;
        const bool stopReceiving = false;
#endif
    }
}
