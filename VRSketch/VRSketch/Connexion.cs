using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;


namespace VRSketch
{
    using RetryWebSocket;


    public static class MyListExtension
    {
        public static T Pop<T>(this List<T> list)
        {
            T result = list[list.Count - 1];
            list.RemoveAt(list.Count - 1);
            return result;
        }

        public static T Last<T>(this List<T> list)
        {
            return list[list.Count - 1];
        }

        public static IEnumerable<(int, T)> Enumerate<T>(IEnumerable<T> enumerable)
        {
            int i = 0;
            foreach (var x in enumerable)
            {
                yield return (i, x);
                i += 1;
            }
        }

        public static IEnumerable<(T1, T2)> Zip<T1, T2>(IEnumerable<T1> t1, IEnumerable<T2> t2)
        {
            using (var t1e = t1.GetEnumerator())
            using (var t2e = t2.GetEnumerator())
                while (t1e.MoveNext() && t2e.MoveNext())
                    yield return (t1e.Current, t2e.Current);
        }

        public static IEnumerable<(T1, T2, T3)> Zip<T1, T2, T3>(IEnumerable<T1> t1, IEnumerable<T2> t2, IEnumerable<T3> t3)
        {
            using (var t1e = t1.GetEnumerator())
            using (var t2e = t2.GetEnumerator())
            using (var t3e = t3.GetEnumerator())
                while (t1e.MoveNext() && t2e.MoveNext() && t3e.MoveNext())
                    yield return (t1e.Current, t2e.Current, t3e.Current);
        }
    }

    public class Connection
    {
        static Connection instance;
        Socket sock;
        RetryWebSocket ws;
        public Interaction interaction;

        //public static Connection Instance => instance;

        public static void CloseCurrentConnection()
        {
            if (instance != null)
            {
                VRSketchCommand._WriteLog("CloseCurrentConnection()\n");
                instance.ShutdownSocket();
                instance.Close();
                instance = null;
            }
        }

        public static Connection CreateNewConnection()
        {
            CloseCurrentConnection();
            instance = new Connection();
            return instance;
        }

        public bool IsCurrentConnection => instance == this;

        public void SetDirectSocket(Socket sock)
        {
            this.sock = sock;
        }

        public void SetWebSocket(RetryWebSocket ws)
        {
            this.ws = ws;
        }

        void _SendToSock(byte[] bytes)
        {
            while (bytes.Length > 0)
            {
                /* avoid using Send() with an arbitrary ArraySegment.
                 * At least SendAsync seems to be bugged in some way with
                 * it, so maybe Send too? */

                int count = sock.Send(bytes);
                if (count >= bytes.Length)
                    return;
                if (count <= 0)
                    throw new Exception("Connection closed");

                byte[] tail = new byte[bytes.Length - count];
                Array.Copy(bytes, count, tail, 0, tail.Length);
                bytes = tail;
            }
        }

        void _LockedSendAll(byte[] bytes)
        {
            if (sock != null)
                _SendToSock(bytes);
            else if (ws != null)
                ws.SendInPieces(bytes);
        }

        object send_lock = new object();

        public void Send(QCommand cmd)
        {
            byte[] data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(cmd) + "\0");
            lock (send_lock)
                _LockedSendAll(data);
        }

        public void Send(QCommandWithLength cmd, BytesData rawData)
        {
            rawData.GetBytes(out byte[] raw, out int length);
            Send(cmd, raw, length);
        }

        public void Send(QCommandWithLength cmd, byte[] raw, int length)
        {
            cmd.rawlength = length;
            byte[] data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(cmd) + "\0");
            if (raw.Length > length)
            {
                byte[] raw1 = new byte[length];
                Buffer.BlockCopy(raw, 0, raw1, 0, length);
                raw = raw1;
            }
            lock (send_lock)
            {
                _LockedSendAll(data);
                _LockedSendAll(raw);
            }
        }

        public static void SendError(QCommand cmd)
        {
            try
            {
                instance?.Send(cmd);
            }
            catch
            {
            }
        }

        public int Receive(byte[] input, int start, int stop)
        {
            if (sock != null)
                return ReceiveSock(input, start, stop);
            if (ws != null)
                return ReceiveWs(input, start, stop);
            return -1;
        }

        int ReceiveSock(byte[] input, int start, int stop)
        {
            try
            {
                /* Workaround from VRSketch's own BaseNetwork.cs, see comments there */
                while (true)
                {
                    var a = new ArrayList { sock };
                    Socket.Select(a, new ArrayList(), new ArrayList(), 5000000);
                    if (a.Count > 0)
                        break;
                }

                return sock.Receive(input, start, stop - start, SocketFlags.None);
            }
            catch //(SocketException e)
            {
                //if (e.SocketErrorCode != SocketError.Shutdown)
                //    Debug.LogWarning(e);
                return -1;
            }
        }

        public class TwoEndedQueue<T>
        {
            /* similar to a thread-safe Queue or the non-available ConcurrentQueue, but simplier:
             * multiple threads can call Enqueue(), but we assume that only one thread calls
             * BlockDequeue() and TryPeek().
             */
            readonly List<T> in_list = new List<T>();
            readonly List<T> out_list = new List<T>();

            public void Enqueue(T item)
            {
                lock (in_list)
                {
                    in_list.Add(item);
                    if (in_list.Count == 1)
                        Monitor.Pulse(in_list);
                }
            }

            public T BlockDequeue()
            {
                if (out_list.Count == 0)
                {
                    lock (in_list)
                    {
                        while (in_list.Count == 0)
                            Monitor.Wait(in_list);

                        for (int i = in_list.Count - 1; i >= 0; --i)
                            out_list.Add(in_list[i]);
                        in_list.Clear();
                    }
                }
                return out_list.Pop();
            }

            public bool TryPeek(out T result)
            {
                if (out_list.Count == 0)
                {
                    lock (in_list)
                    {
                        if (in_list.Count == 0)
                        {
                            result = default;
                            return false;
                        }
                        for (int i = in_list.Count - 1; i >= 0; --i)
                            out_list.Add(in_list[i]);
                        in_list.Clear();
                    }
                }
                result = out_list.Last();
                return true;
            }
        }

        readonly TwoEndedQueue<byte[]> ws_incoming = new TwoEndedQueue<byte[]>();
        ArraySegment<byte>? ws_remaining_part;

        int ReceiveWs(byte[] input, int start, int stop)
        {
            ArraySegment<byte> seg;
            if (ws_remaining_part == null)
            {
                byte[] rawdata = ws_incoming.BlockDequeue();
                if (rawdata == null)
                {
                    ws_incoming.Enqueue(null);
                    return -1;
                }
                seg = new ArraySegment<byte>(rawdata);
            }
            else
                seg = ws_remaining_part.Value;

            int pcount = stop - start;
            if (seg.Count <= pcount)
            {
                Array.Copy(seg.Array, seg.Offset, input, start, seg.Count);
                ws_remaining_part = null;
                return seg.Count;
            }
            else
            {
                Array.Copy(seg.Array, seg.Offset, input, start, pcount);
                ws_remaining_part = new ArraySegment<byte>(seg.Array, seg.Offset + pcount, seg.Count - pcount);
                return pcount;
            }
        }

        public void PushIncomingBinaryData(byte[] rawdata)
        {
            if (rawdata.Length > 0)
                ws_incoming.Enqueue(rawdata);
        }

        public void Close()
        {
            VRSketchCommand._WriteLog("Connexion.Close()\n");
            sock?.Close();
            ws?.Close();
            ws_incoming.Enqueue(null);
        }

        void ShutdownSocket()
        {
            try
            {
                sock?.Shutdown(SocketShutdown.Both);
            }
            catch (SocketException)
            {
                /* ignore exceptions here */
            }
        }

        public bool ConnectionWasClosed()
        {
            if (sock != null)
                return !sock.Connected;
            else if (ws != null)
                return ws.IsClosed;
            else
                return true;
        }
    }

    public class QCommand
    {
    }

    public class QCommandWithLength : QCommand
    {
        public int rawlength;
    }

    public class QSimpleCommand : QCommand
    {
        public string cmd;

        public QSimpleCommand(string _cmd)
        {
            cmd = _cmd;
        }
    }

    public class QDebug : QCommand
    {
        public string cmd = "debug";
        public string name;

        public QDebug(string _name)
        {
            name = _name;
        }
    }

    public class QTextOnController : QCommand
    {
        public string cmd = "text_on_controller";
        public string name;
        public string[] names;

        public QTextOnController(string ctrl_text, string dlg_title = "Info", string dlg_text = null)
        {
            name = ctrl_text;
            if (dlg_text != null)
                names = new string[] { dlg_title, dlg_text };
        }
    }

    public class QClearModel : QCommand
    {
        public string cmd = "clearmodel";
        public int id;
        public int[] v;
        public float[] transform;

        const int ESF_OPEN_SKETCHUP_GROUP = 0x01;
        const int ESF_EDIT_NATIVE_GROUP = 0x02;
        const int ESF_SKETCHUP_ON_MACOSX = 0x04;
        const int ESF_NO_FIX_BBOX = 0x08;

        public QClearModel(Vector3d[] bbox = null)
        {
            id = 220003;
            int extra_flags = ESF_EDIT_NATIVE_GROUP | ESF_NO_FIX_BBOX;
            v = new int[] { 0, 0, 0, 0, extra_flags };
            if (bbox != null)
            {
                double[] dbl = Serializer.Convert(bbox);
                transform = dbl.Select(x => (float)x).ToArray();
            }
            else
                transform = new float[] { -1, -1, 0, 1, 1, 1 };
        }
    }

    public class QMaterials : QCommand
    {
        public string cmd = "materials";
        public string[] names;
        public double[] transform;

        public QMaterials(string[] materials)
        {
            names = materials;
            transform = new double[] { 1.0, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 0.0, 1.0 };
        }
    }

    public class QSetEntities : QCommandWithLength
    {
        public string cmd = "setentities";
        public int id;
        public string name;

        public QSetEntities(int _id, string _name = null)
        {
            id = _id;
            name = _name;
        }
    }

    public class QAddEntities : QCommandWithLength
    {
        public string cmd = "addentities";
        public int id;

        public QAddEntities(int _id)
        {
            id = _id;
        }
    }

    public class QStartUpdate : QCommand
    {
        public string cmd = "start_update";
        public int[] id_list;
        public int[] v;
        public double[] transform;

        public QStartUpdate()
        {
            id_list = new int[1] { 0 };
            v = new int[0];
            transform = new double[] { 1.0, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 0.0, 1.0 };
        }
    }

    public class QModelComplete : QCommand
    {
        public string cmd = "modelcomplete";
        public int[] v;

        public QModelComplete(int texture_count)
        {
            v = new int[] { texture_count };
        }
    }
    public class QUpdateComplete : QCommand
    {
        public string cmd = "updatecomplete";

        public QUpdateComplete()
        {
        }
    }

    public class QTexturesComplete : QCommand
    {
        public string cmd = "texturescomplete";

        public QTexturesComplete()
        {
        }
    }

    public class QSetConfig : QCommand
    {
        public string cmd = "setconfig";
        public string name;
        public int[] v;

        public QSetConfig()
        {
            name = "allowed_actions";
            v = new int[] { 1, 1, 1, 0, 0, 0, 0, 0, 1 };
        }
    }

    public class QMaterialDef : QCommand
    {
        public string display_name;
        public int color;
        public float alpha;
        public double width = -1.0;
        public double height;

        /* these are not meant to be serialized */
        internal int material_index { get; set; }
        internal string texture_filename { get; set; }
        internal double[] texture_uvmap { get; set; }
        internal float[] color_transform { get; set; }   /* see QTexture.transform */

        public QMaterialDef(string displayName, int color, float alpha)
        {
            display_name = displayName;
            this.color = color;
            this.alpha = alpha;
        }
    }

    public class QMaterial : QCommand
    {
        public string cmd = "material";
        public string name;
        public QMaterialDef material_def;

        public QMaterial(string name, QMaterialDef material_def)
        {
            this.name = name;
            this.material_def = material_def;
        }
    }

    public class QTexture : QCommandWithLength
    {
        public string cmd = "texture";
        public string name;
        public float[] transform;  /* color transform: 3x4 matrix [RGB1] => [RGB] */

        public QTexture(string name, float[] transform = null)
        {
            this.name = name;
            this.transform = transform;
        }
    }

    /*public class QRevitResult : QCommand
    {
        public string cmd = "revit_result";
        public int id;
        public string error;

        public QRevitResult(int id, string error)
        {
            this.id = id;
            this.error = error;
        }
    }*/
}
