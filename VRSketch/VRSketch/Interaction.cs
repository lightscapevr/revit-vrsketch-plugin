using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;


namespace VRSketch
{
    public class KCommand
    {
        readonly JObject jobj;
        public byte[] rawdata;

        public KCommand(JObject jobj)
        {
            this.jobj = jobj;
        }

        public int GetInt(string name) => (int)GetDouble(name);

        public double GetDouble(string name)
        {
            if (!jobj.TryGetValue(name, out var value))
                return 0;
            double? maybe_double = (double?)value;
            if (maybe_double == null)
                return 0;
            return maybe_double.Value;
        }

        public string GetStr(string name)
        {
            if (!jobj.TryGetValue(name, out var value))
                return null;
            return (string)value;
        }
    }

    public class MiniQueue<T>
    {
        /* similar to a thread-safe Queue or ConcurrentQueue, but simpler.
         */
        readonly List<T> pending_list = new List<T>();

        public void Enqueue(T item, out bool must_signal)
        {
            /* sets 'must_signal' if the calling thread should arrange for the reader thread
             * to eventually call FetchItems(). */
            lock (pending_list)
            {
                pending_list.Add(item);
                must_signal = pending_list.Count == 1;
            }
        }

        public T[] FetchItems()
        {
            /* fetch and clear all enqueued items. */
            T[] result;
            lock (pending_list)
            {
                result = pending_list.ToArray();
                pending_list.Clear();
            }
            return result;
        }
    }

    public class EditableRefs
    {
        public readonly Dictionary<ElementId, int> ds_map = new Dictionary<ElementId, int>();
        public readonly Dictionary<int, ElementId> ds_rev_map = new Dictionary<int, ElementId>();

        public void Register(DirectShape ds, int top_cdef_id)
        {
            ds_map[ds.Id] = top_cdef_id;
            ds_rev_map[top_cdef_id] = ds.Id;
        }
    }

    public class Interaction : IExternalEventHandler
    {
        readonly public Document doc;
        readonly public Connection con;
        readonly public EditableRefs editable_refs;
        readonly public List<Material> materials_by_index;

        readonly MiniQueue<KCommand> in_queue = new MiniQueue<KCommand>();
        bool warned_readonly;

        public Interaction(Document doc, Connection con, EditableRefs editable_refs,
                           List<Material> materials_by_index)
        {
            this.doc = doc;
            this.con = con;
            this.editable_refs = editable_refs;
            this.materials_by_index = materials_by_index;

            /* make an ExternalEvent that, when Raise() is called from any thread,
             * eventually calls back ((IExternalEventHandler)this).Execute() on the
             * main thread. */
            ExternalEvent exEvent = ExternalEvent.Create(this);

            Start(() => Th_Run(exEvent));
        }

        void IExternalEventHandler.Execute(UIApplication app)
        {
            var pending_commands = in_queue.FetchItems();

            foreach (var command in pending_commands)
            {
                switch (command.GetStr("cmd"))
                {
                    case "apply":
                        if (!warned_readonly)
                        {
                            con.Send(new QTextOnController("read\nonly",
                                "Read-only",
                                "The Revit model is entirely read-only in this version of " +
                                "VR Sketch. All edit commands have no effect. Please follow " +
                                "https://forum.vrksetch.eu for progress and new versions."));
                            warned_readonly = true;
                        }
                        else
                            con.Send(new QTextOnController("read\nonly"));
                        break;

                    case "feedback":
                        con.Send(new QSimpleCommand("cancelwait"));
                        break;

                    //default:
                    //    Log($"unsupported command in the Revit plugin: '{command.GetStr("cmd")}'");
                    //    break;
                }
            }
        }

#if false
        bool TryFindDirectShape(int cdef_id, out DirectShape ds)
        {
            if (editable_refs.ds_rev_map.TryGetValue(cdef_id, out var elementId) &&
                doc.GetElement(elementId) is DirectShape ds1 &&
                ds1.ApplicationId == VRSketchCommand.APPLICATION_ID)
            {
                ds = ds1;
                return true;
            }
            else
            {
                ds = null;
                return false;
            }
        }

        string RunTransactionForDSStore(KCommand cmd)
        {
            int ds_cdef_id = cmd.GetInt("cdef_id");
            string name = cmd.GetStr("name");

            using (var transaction = new Transaction(doc, "VR Sketch edit"))
            {
                if (transaction.Start() != TransactionStatus.Started)
                    return "cannot start transaction";

                if (!TryFindDirectShape(ds_cdef_id, out var ds))
                {
                    ElementId categoryId = new ElementId(BuiltInCategory.OST_GenericModel);
                    ds = DirectShape.CreateElement(doc, categoryId);
                    ds.ApplicationId = VRSketchCommand.APPLICATION_ID;
                }
                if (!string.IsNullOrWhiteSpace(name))
                    ds.Name = name;

                ds.ApplicationDataId = Convert.ToBase64String(cmd.rawdata);

                /* decode the binary string into approximative geometrical shapes for Revit.
                 * This binary string contains a further sequence of KCommands */
                int start = 0;
                int end = cmd.rawdata.Length;
                while (start < end)
                {
                    bool stop = false;
                    KCommand command = Th_ProcessIncomingData(cmd.rawdata, ref start, end, ref stop, null);

                }

                

                ds.SetShape(new GroupDecoder(materials_by_index).DecodeGeomObjs(cmd.rawdata));
                editable_refs.Register(ds, ds_cdef_id);

                if (transaction.Commit() != TransactionStatus.Committed)
                    return "cannot commit transaction";
            }
            return null;
        }
#endif

        string IExternalEventHandler.GetName()
        {
            return "VRSketch Socket Connection Handler";
        }

#if false
        void Log(string msg)
        {
            con.Send(new QDebug(msg));
        }
#endif


        /* Start a new background thread. */
        Thread Start(ThreadStart function)
        {
            var th = new Thread(() =>
            {
                try
                {
                    function();
                }
                catch (ThreadAbortException)
                {
                    /* note: sometimes this exception is still logged, maybe from
                     * System.UnhandledExceptionEventHandler:Invoke() which appears to
                     * come from deeper in the stack */
                    throw;
                }
                catch (Exception e)
                {
                    VRSketchCommand._WriteLog(e.ToString());
                    throw;
                }
            });
            th.IsBackground = true;
            th.Start();
            return th;
        }

        class Demultiplexer
        {
            byte[] input = new byte[8192];
            int recv_start = 0, recv_end = 0;

            public byte[] PrepareInput()
            {
                if (recv_end + 512 > input.Length)
                {
                    if (recv_start < 256)
                        Array.Resize(ref input, input.Length * 2);
                    else
                    {
                        Array.Copy(input, recv_start, input, 0, recv_end - recv_start);
                        recv_end -= recv_start;
                        recv_start = 0;
                    }
                }
                return input;
            }

            public int Start { get { return recv_start; } set { recv_start = value; } }
            public int End { get { return recv_end; } set { recv_end = value; } }
        }

        void Th_Run(ExternalEvent ex_event)
        {
            Demultiplexer dm = new Demultiplexer();
            KCommand command;
            bool stop = false;

            while (!stop)
            {
                byte[] input = dm.PrepareInput();
                int count = con.Receive(input, dm.End, input.Length);
                if (count <= 0)
                    break;
                //total_bytes += count;
                dm.End += count;

                while (true)
                {
                    int recv_start = dm.Start;
                    command = Th_ProcessIncomingData(input, ref recv_start, dm.End, ref stop, con);
                    dm.Start = recv_start;
                    if (command == null)
                        break;
                    //total_raw_bytes += command.rawlength;

                    Th_Enqueue(ex_event, command);

                    if (dm.Start == dm.End)
                    {
                        dm.Start = dm.End = 0;
                        break;
                    }
                }
            }
            ex_event.Dispose();
            con.Close();
        }

        void Th_Enqueue(ExternalEvent ex_event, KCommand command)
        {
            in_queue.Enqueue(command, out bool must_signal);
            /* wake up Revit's main thread */
            if (must_signal)
                ex_event.Raise();
        }

        static KCommand Th_ProcessIncomingData(byte[] input, ref int recv_start_in_out, int recv_end, ref bool stop,
                                               Connection optional_con = null)
        {
            int recv_start = recv_start_in_out;
            int limit = Array.IndexOf<byte>(input, 0, recv_start, recv_end - recv_start);
            if (limit < 0 || limit >= recv_end)
                return null;

            string u = Encoding.UTF8.GetString(input, recv_start, limit - recv_start);
            var command = new KCommand(JObject.Parse(u));
            recv_start = limit + 1;
            int rawlength = command.GetInt("rawlength");
            if (rawlength > 0)
            {
                byte[] rawdata = new byte[rawlength];
                int length = recv_end - recv_start;
                if (length > rawlength)
                    length = rawlength;
                Array.Copy(input, recv_start, rawdata, 0, length);
                recv_start += length;
                while (length < rawlength)
                {
                    /* note: returning null here is always correct, but it is more efficient to
                     * continue reading directly into 'rawdata' if we can. */
                    if (optional_con == null)
                        return null;
                    int extra_count = optional_con.Receive(rawdata, length, rawlength);
                    if (extra_count <= 0)
                    {
                        stop = true;   /* don't call Receive() again after it returns 0 */
                        return null;
                    }
                    //total_bytes += extra_count;
                    length += extra_count;
                }
                if (length != rawlength)
                {
                    //Debug.LogAssertion("IBinaryConnection.Receive() got more data than asked for");
                    stop = true;
                    return null;
                }
                command.rawdata = rawdata;
            }
            else
                command.rawdata = Array.Empty<byte>();

            recv_start_in_out = recv_start;
            return command;
        }
    }
}
