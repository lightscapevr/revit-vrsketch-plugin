using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace RetryWebSocket
{
    public class CloseEventArgs : EventArgs { }
    public class ErrorEventArgs : EventArgs { public string Message; }

    public struct MessageEventArgs
    {
        public string Data;
        public byte[] RawData;
        public int RawDataSize;
        public Opcode Type => Data != null ? Opcode.Text : Opcode.Binary;
    }

    public enum Opcode
    {
        Text, Binary
    }


    public class WebSocket
    {
        public Action<object, EventArgs> OnOpen;
        public Action<object, CloseEventArgs> OnClose;
        public Action<object, ErrorEventArgs> OnError;
        public Action<object, MessageEventArgs> OnMessage;

        string url;
        CancellationTokenSource cts;
        volatile bool running;
        ClientWebSocket socket;


        public WebSocket(string url)
        {
            if (url == null)
                throw new ArgumentNullException("url");
            this.url = url;
        }

        public void ConnectAsync()
        {
            if (url == null)
                throw new InvalidOperationException("WebSocket was already connected once");

            socket = new ClientWebSocket();
            socket.Options.KeepAliveInterval = new TimeSpan(0, 10, 0);   /* 10 minutes */
            socket.Options.UseDefaultCredentials = true;
            cts = new CancellationTokenSource();
            var uri = new Uri(url);
            url = null;

            Task.Factory.StartNew(
                async () =>
                {
                    try
                    {
                        await socket.ConnectAsync(uri, cts.Token);
                        running = true;
                        OnOpen?.Invoke(this, new EventArgs());

                        /* note: as far as I can tell, these lists should not grow longer
                         * than a few elements, because the sender should not send messages
                         * longer than 32000.  But if ReceiveAsync returns it in smaller
                         * pieces, we'll need several pieces.  Let's do the general case
                         * here anyway. */
                        List<byte[]> rcv_pieces = new List<byte[]> { new byte[32768] };
                        List<int> rcv_lengths = new List<int> { 0 };

                        while (true)
                        {
                            int current_index = 0;
                            int message_size = 0;
                            WebSocketReceiveResult result;
                            while (true)
                            {
                                /* avoid using ReceiveAsync with an arbitrary ArraySegment.
                                 * At least SendAsync seems to be bugged in some way with
                                 * it, so maybe ReceiveAsync too? */

                                if (rcv_pieces.Count == current_index)
                                {
                                    rcv_lengths.Add(0);
                                    rcv_pieces.Add(new byte[16384]);
                                }

                                result = await socket.ReceiveAsync(
                                    new ArraySegment<byte>(rcv_pieces[current_index]),
                                    cts.Token);
                                if (result.CloseStatus != null)
                                    return;
                                int count = result.Count;
                                if (count <= 0)
                                    continue;
                                rcv_lengths[current_index] = count;
                                current_index += 1;
                                message_size += count;
                                if (result.EndOfMessage)
                                    break;
                                /* else: more data coming, loop back */
                            }

                            /* stitch togther the pieces */
                            byte[] rawdata = new byte[message_size];
                            int current_ofs = 0;
                            for (int i = 0; i < current_index; i++)
                            {
                                Buffer.BlockCopy(rcv_pieces[i], 0, rawdata, current_ofs,
                                                 rcv_lengths[i]);
                                current_ofs += rcv_lengths[i];
                            }

                            switch (result.MessageType)
                            {
                                case WebSocketMessageType.Binary:
                                    OnMessage(this, new MessageEventArgs { RawData = rawdata, RawDataSize = message_size });
                                    break;

                                case WebSocketMessageType.Text:
                                    string data = Encoding.UTF8.GetString(rawdata);
                                    OnMessage(this, new MessageEventArgs { /*RawData = rawdata,*/ Data = data, RawDataSize = message_size });
                                    break;
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (Exception e)
                    {
                        OnError?.Invoke(this, new ErrorEventArgs { Message = e.ToString() });
                    }
                    finally
                    {
                        running = false;
                        cts.Cancel();
                        OnClose?.Invoke(this, new CloseEventArgs());
                        cts = new CancellationTokenSource(new TimeSpan(0, 0, 5));   /* 5 seconds to close */
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closing", cts.Token);
                    }
                }, cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public bool IsOpen()
        {
            return running;
        }

        void WaitAsync(Task t)
        {
            try
            {
                t.Wait();
            }
            catch   // at least AggregateException or OperationCanceledException
            {
                CloseAsync();
            }
        }

        public void Send(string data)
        {
            byte[] rawdata = Encoding.UTF8.GetBytes(data);
            Send(rawdata, WebSocketMessageType.Text);
        }

        public void Send(byte[] rawdata, WebSocketMessageType type = WebSocketMessageType.Binary)
        {
            /* synchronous send.  If we get any error, we close the socket. */
            if (!running)
            {
                //UnityEngine.Debug.LogWarning("Send() on a closed WebSocket");
                return;
            }
            /* NOTE: we don't provide an API for passing an ArraySegment<byte>, because
             * the method socket.SendAsync() appears to be buggy when passed a segment
             * with only a slice of the array. */
            Task t = socket.SendAsync(new ArraySegment<byte>(rawdata), type, true, cts.Token);
            WaitAsync(t);
        }

        public void CloseAsync()
        {
            cts?.Cancel();
            running = false;
        }
    }
}
