using VRSketch;
using Newtonsoft.Json;
using Autodesk.Revit.UI;


namespace RetryWebSocket
{
    public partial class RetryWebSocket
    {
        ExternalEvent close_signal;

        void AfterConnecting()
        {
            close_signal = VRSketchCommand.MakeSignalFromAnyThread(InvokeErrorAndClose);
        }

        void AfterDisconnectingInSomeThread()
        {
            close_signal?.Raise();
        }

        static void DebugLog(string msg)
        {
            VRSketchCommand._WriteLog(msg);
        }

        static JMessage ParseJMessage(string json)
        {
            return JsonConvert.DeserializeObject<JMessage>(json);
        }
    }
}
