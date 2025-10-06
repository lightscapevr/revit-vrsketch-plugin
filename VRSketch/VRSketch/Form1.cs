using System;
using System.Linq;
using System.Windows.Forms;

using UIApplication = Autodesk.Revit.UI.UIApplication;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;


namespace VRSketch
{
    public partial class Form1 : Form
    {
        UIApplication uiapp;

        public Form1(UIApplication uiapp)
        {
            this.uiapp = uiapp;
            InitializeComponent();

            questIdBox.Text = VRSketchApp.last_quest_id;
            comboBox1.SelectedIndex = VRSketchApp.level_of_detail / 3;
        }

        private void closeBtn_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void sendPCBtn_Click(object sender, EventArgs e)
        {
            VRSketchCommand.WrapErrors(() =>
                {
                    VRSketchCommand.GetInstance().SendToVR(uiapp);
                    Close();
                },
                "A failure occurred when sending the model to VR on this PC.");
        }

        private void sendQuestBtn_Click(object sender, EventArgs e)
        {
            string quest_id = questIdBox.Text.Trim();
            if (quest_id.Length != 6 || !quest_id.All(c => '0' <= c && c <= '9'))
            {
                TaskDialog.Show("VR Sketch", "You need to enter a 6-digits code. See the tooltip for more information.");
                return;
            }
            if (quest_id != VRSketchApp.last_quest_id)
            {
                VRSketchApp.WriteRegistry("quest_uid_history", quest_id);
                VRSketchApp.last_quest_id = quest_id;
            }

            VRSketchCommand.WrapErrors(() =>
                {
                    VRSketchCommand.GetInstance().SendToVR(uiapp, quest_id);
                    Close();
                },
                "A failure occurred when sending the model to VR on Quest/Pico Neo headset.");
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            int new_level_of_detail = comboBox1.SelectedIndex * 3;
            if (new_level_of_detail != VRSketchApp.level_of_detail)
            {
                VRSketchApp.level_of_detail = new_level_of_detail;
                VRSketchApp.WriteRegistry("revit_lod", new_level_of_detail.ToString());
            }
        }
    }
}
