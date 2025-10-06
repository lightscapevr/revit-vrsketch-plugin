using System;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using System.Linq;


namespace VRSketch
{
    public class VRSketchApp : IExternalApplication
    {
        // this should be the version of the VRSketch.exe included with the package
        public const string VRSKETCH_EXE_VERSION = "25.0.0b1";


        const int DEFAULT_LEVEL_OF_DETAIL = 9;
        internal static string last_quest_id;
        internal static int level_of_detail = -1;
#if false
        ComboBox lodBox;
#endif

        static string _pid;

        public static string GetRandomProcessUid()
        {
            if (_pid == null)
            {
                var r = new Random();
                char[] array = new char[15];
                for (int i = 0; i < array.Length; i++)
                    array[i] = (char)r.Next(97, 97 + 26);
                _pid = new string(array);
            }
            return _pid;
        }

        public static BitmapImage GetImage(string name)
        {
            Assembly a = Assembly.GetExecutingAssembly();
            return new BitmapImage(new Uri(Path.Combine(Path.GetDirectoryName(a.Location), name)));
        }

        public Result OnStartup(UIControlledApplication app)
        {
#if false
            /* app.CreateRibbonTab("VR Sketch"); */

            var thisAssembly = Assembly.GetExecutingAssembly().Location;
            var sendToVRHigh = new PushButtonData("sendToVRHigh", "Send to VR", thisAssembly, "VRSketch.VRSketchCommand");
            
            //var sendToVRMid = new PushButtonData("sendToVRMid", "Send to VR (Mid)", thisAssembly, "VRSketch.VRSketchSendMid");
            //sendToVRMid.LargeImage = GetImage("vr32x32.bmp");
            ////sendToVRMid.Image = new BitmapImage(new Uri(@"Y:\VRSketch\bin\Debug\vr32x32.bmp"));
            //var sendToVRLow = new PushButtonData("sendToVRLow", "Send to VR (Low)", thisAssembly, "VRSketch.VRSketchSendLow");
            //sendToVRLow.LargeImage = GetImage("vr32x32.bmp");
            ////sendToVRLow.Image = new BitmapImage(new Uri(@"Y:\VRSketch\bin\Debug\vr32x32.bmp"));

            var sendToVR = new SplitButtonData("sendToVR", "Send to VR");
            sendToVR.Image = GetImage("vr32x32.bmp");
            sendToVR.LargeImage = GetImage("vr32x32.bmp");
            var panel = app.CreateRibbonPanel(/*"VR Sketch",*/ "VR Sketch");
            //var sendToQuest = new PushButtonData("sendToQuest", "Send to Quest", thisAssembly, "VRSketch.VRSketchQuest");

            var sendItem = panel.AddItem(sendToVR) as SplitButton;
            sendItem.Image = GetImage("vr32x32.bmp");
            sendItem.LargeImage = GetImage("vr32x32.bmp");

            var but = sendItem.AddPushButton(sendToVRHigh);
            but.LargeImage = GetImage("vr32x32.bmp");
            but.Image = GetImage("vr32x32.bmp");
            //sendItem.AddPushButton(sendToVRMid);
            //sendItem.AddPushButton(sendToVRLow);
            sendItem.LargeImage = GetImage("vr32x32.bmp");
            sendItem.Image = GetImage("vr32x32.bmp");

            //var text = panel.AddItem(new TextBoxData("Quest no")) as TextBox;
            //text.ToolTip = "6 digit Quest number";
            //panel.AddItem(sendToQuest);
#endif

            var thisAssembly = Assembly.GetExecutingAssembly().Location;

            /* make a smallish panel inside the default "Add-Ins" ribbon */
            var panel = app.CreateRibbonPanel("VR Sketch");

            var btn = new PushButtonData("VRSketch", "VR Sketch", thisAssembly,
                                         typeof(VRSketchButton).FullName);
            btn.LargeImage = GetImage("vr32x32.bmp");
            btn.Image = GetImage("vr32x32.bmp");
            panel.AddItem(btn);

#if false
            /* add a single square space for two stacked controls: the "Send to VR on this PC"
             * button and the "Send to VR on Quest" text box */
            var stackedItems = panel.AddStackedItems(
                new PushButtonData("SendToVR", "Send to PC VR", thisAssembly,
                                   typeof(SendToVRCommand).FullName),
                new TextBoxData("QuestId"),
                new ComboBoxData("Details"));

            var sendToVRBtn = (PushButton)stackedItems[0];
            sendToVRBtn.Image = GetImage("vr16x16.bmp");
            sendToVRBtn.ToolTip = "Opens or updates the model in \"tethered\" VR on this PC";
            sendToVRBtn.LongDescription =
                "This assumes that you have installed on this PC one of:\n" +
                "- SteamVR (various headsets supported); or\n" +
                "- the Meta Quest application (to use with Meta Quest Link).\n\n" +
                "This requires a powerful graphic card in your PC. It can be used to " +
                "view models with a large number of faces. The exact limit depends on " +
                "your hardware, but is many millions of faces.\n\n" +
                "In the current version, the model is not updated in VR if you make " +
                "changes in Revit. Click again to reload it.";

            /* ...and the "Send to VR on Quest" button */
            var sendToQuestText = (TextBox)stackedItems[1];
            sendToQuestText.Image = GetImage("vr16x16.bmp");
            sendToQuestText.ShowImageAsButton = true;
            sendToQuestText.PromptText = "Send to Quest#";
            sendToQuestText.SelectTextOnFocus = true;
            sendToQuestText.Width = 120;
            sendToQuestText.ToolTip = "Opens or updates the model in VR on a standalone headset (Quest or Pico Neo)";
            sendToQuestText.EnterPressed += SendToQuest_Enter;
            sendToQuestText.LongDescription =
                "This assumes that you have installed the 'VR Sketch' application in your " +
                "standalone headset. In that application, look up the \"Quest #\" 6-digits " +
                "code, and copy it here. It appears at the top of the start-up dialog box.\n\n" +
                "This will send the model data to the Quest or Pico Neo device, either " +
                "directly if the Quest is connected to the same local network, or else by " +
                "using our servers for relay. (We do not capture or store any information " +
                "sent in this way, but please be aware of that.)\n\n" +
                "This is limited to models with up to 500'000 to 800'000 faces. Consider " +
                "using the tethered solution if this is a problem.\n\n" +
                "In the current version, the model is not updated in VR if you make " +
                "changes in Revit. Click again to reload it.";

            last_quest_id = ReadRegistry("quest_uid_history");
            sendToQuestText.Value = last_quest_id;

            /* the "Level of Detail" selection with 6 values, from 0 to 5, mapping to
               the Revit values from 0 to 15 */
            lodBox = (ComboBox)stackedItems[2];
            lodBox.ItemText = "Level of detail";
            lodBox.ToolTip = "Choose the level of detail. Lower it for large models.";
            lodBox.LongDescription =
                "Higher values will use more triangles to represent rounded surfaces, " +
                "while lower values will simplify the geometry down to a more angular " +
                "approximation. The total number of faces roughly determines how " +
                "smoothly the GPU can compute each frame. If a model is too complex to " +
                "view, or can be viewed but with a bad frames-per-second rate, then " +
                "one possibility is adjusting this value and trying again.\n\n" +
                "Standalone headsets start to struggle above 500'000 to 800'000 faces. " +
                "Tethered headsets can go much higher, depending on your PC's GPU " +
                "performance, but typically the number is 5 to 15 millions.\n\n" +
                "After changing this value, you need to send your model to VR again.\n\n" +
                "For reference, with the Basic Sample Project we get:\n" +
                "Details 0/5:     207'000\n" +
                "Details 1/5:     211'000\n" +
                "Details 2/5:     328'000\n" +
                "Details 3/5:     406'000 (default level)\n" +
                "Details 4/5:   2'500'000\n" +
                "Details 5/5:   5'700'000";
            var lodMembers = lodBox.AddItems(new ComboBoxMemberData[]
            {
                new ComboBoxMemberData("0", "Details 0/5 (min)"),
                new ComboBoxMemberData("3", "Details 1/5"),
                new ComboBoxMemberData("6", "Details 2/5"),
                new ComboBoxMemberData("9", "Details 3/5 (default)"),
                new ComboBoxMemberData("12", "Details 4/5"),
                new ComboBoxMemberData("15", "Details 5/5 (max)")
            });
            if (!int.TryParse(ReadRegistry("revit_lod"), out level_of_detail) ||
                    level_of_detail < 0 || level_of_detail > 15)
                level_of_detail = DEFAULT_LEVEL_OF_DETAIL;
            lodBox.Current = lodMembers[(level_of_detail + 1) / 3];
            lodBox.CurrentChanged += LodBox_CurrentChanged;
#endif

            return Result.Succeeded;
        }

#if false
        private void LodBox_CurrentChanged(object sender, ComboBoxCurrentChangedEventArgs e)
        {
            level_of_detail = int.Parse(lodBox.Current.Name);
            WriteRegistry("revit_lod", lodBox.Current.Name);
        }
#endif

        public static int GetLevelOfDetail()
        {
            return level_of_detail;
        }

#if false
        void SendToQuest_Enter(object sender, TextBoxEnterPressedEventArgs e)
        {
            TextBox sendToQuestText = (TextBox)sender;
            string quest_id = sendToQuestText.Value.ToString().Trim();
            if (quest_id.Length != 6 || !quest_id.All(c => '0' <= c && c <= '9'))
            {
                TaskDialog.Show("VR Sketch", "You need to enter a 6-digits code. See the tooltip for more information.");
                return;
            }
            if (quest_id != last_quest_id)
            {
                WriteRegistry("quest_uid_history", quest_id);
                last_quest_id = quest_id;
            }

            VRSketchCommand.WrapErrors(() =>
                VRSketchCommand.GetInstance().SendToVR(e.Application, quest_id),
                "A failure occurred when sending the model to VR on Quest/Pico Neo headset.");
        }
#endif

        public Result OnShutdown(UIControlledApplication app)
        {
            Connection.CloseCurrentConnection();
            return Result.Succeeded;
        }


        /*****  Registry  *****/

        const string REGISTRY_PATHNAME = "SOFTWARE\\VRSketch";

        public static string ReadRegistry(string key_name, string default_value = "")
        {
            string result = null;
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REGISTRY_PATHNAME))
                {
                    if (key != null)
                        result = key.GetValue(key_name) as string;
                }
            }
            catch (Exception)
            {
            }
            if (result == null)
                return default_value;
            return result;
        }

        public static void WriteRegistry(string key_name, string value)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(REGISTRY_PATHNAME))
                {
                    if (key != null)
                        key.SetValue(key_name, value);
                }
            }
            catch (Exception)
            {
            }
        }

        public static void LoadValuesFromRegistry()
        {
            if (last_quest_id == null)
                last_quest_id = ReadRegistry("quest_uid_history", "");

            if (level_of_detail < 0)
            {
                if (!int.TryParse(ReadRegistry("revit_lod"), out level_of_detail) ||
                                    level_of_detail < 0 || level_of_detail > 15)
                    level_of_detail = DEFAULT_LEVEL_OF_DETAIL;
            }
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class VRSketchButton : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            VRSketchApp.LoadValuesFromRegistry();

            var dc = new Form1(data.Application);
            dc.ShowDialog();

            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class SendToVRCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            VRSketchCommand.WrapErrors(() =>
                VRSketchCommand.GetInstance().SendToVR(data.Application),
                "A failure occurred when sending the model to VR on this PC.");
            return Result.Succeeded;
        }
    }
}
