﻿using FastColoredTextBoxNS;
using ReCT.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using DiscordRPC;
using System.Threading;

namespace ReCT_IDE
{
    public partial class Form1 : Form
    {
        public string openFile = "";
        public bool fileChanged = false;
        public ReCT_Compiler rectCompCheck = new ReCT_Compiler();
        public ReCT_Compiler rectCompBuild = new ReCT_Compiler();
        public Error errorBox;
        public Process running;
        string[] standardAC;

        Discord dc;
        RichPresence presence;

        Settings settings;

        bool tabSwitch = false;

        public Button TabPrefab;

        public Image[] icons = new Image[7];

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool FlashWindow(IntPtr hwnd, bool bInvert);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool ShowWindow(System.IntPtr hWnd, int cmdShow);


        string standardMsg = "//ReCT IDE ";

        List<Tab> tabs = new List<Tab>();
        int currentTab = 0;

        public Form1()
        {
            Thread t = new Thread(new ThreadStart(SplashScreen));
            t.Start();
            Thread.Sleep(2000);

            InitializeComponent();
        }

        void SplashScreen()
        {
            Application.Run(new Startup());
        }

        private void fileToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void Menu_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {

        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Activate();

            Menu.Renderer = new MenuRenderer();
            standardMsg += ReCT.info.Version;
            errorBox = new Error();
            SetCodeBoxColors();
            fileChanged = false;
            //updateWindowTitle();

            icons[0] = Play.BackgroundImage;
            icons[1] = Stop.BackgroundImage;
            icons[2] = Build.BackgroundImage;

            icons[3] = Image.FromFile("res/playIconHL.png");
            icons[4] = Image.FromFile("res/literally_just_a_fukin_SquareIconHL.png");
            icons[5] = Image.FromFile("res/gearIconHL.png");

            icons[6] = Image.FromFile("res/playIconLoad.png");

            standardAC = ReCTAutoComplete.Items;

            TabPrefab = (Button)CtrlClone.ControlFactory.CloneCtrl(Tab);
            Tab.Dispose();
            Controls.Remove(Tab);

            var tab = makeNewTab();
            tabs.Add(tab);

            settings = new Settings(this);
            settings.Hide();

            settings.autosave.SelectedIndex = Properties.Settings.Default.Autosave;
            settings.maximize.SelectedIndex = Properties.Settings.Default.Maximize ? 1 : 0;
            settings.maximizeRect.SelectedIndex = Properties.Settings.Default.MaximizeRect ? 1 : 0;         

            dc = new Discord();
            dc.Initialize();

            presence = new RichPresence()
            {
                Details = "Working on Untitled...",
                Timestamps = new Timestamps()
                {
                    Start = DateTime.UtcNow
                },
                Assets = new Assets()
                {
                    LargeImageKey = "rect",
                    LargeImageText = "ReCT IDE",
                }
            };

            presence.Details = "Working on " + tabs[currentTab].name + "...";
            dc.client.SetPresence(presence);

            OrderTabs();
        }

        public void startAllowed(bool allowed)
        {
            if(!allowed)
                Play.BackgroundImage = icons[6];
            else
                Play.BackgroundImage = icons[0];
        }

        public void changeIcon(PictureBox box, int id, bool mode)
        {
            box.BackgroundImage = icons[id + (mode ? 3 : 0)];
        }

        private class MenuRenderer : ToolStripProfessionalRenderer
        {
            public MenuRenderer() : base(new MenuColors()) { }
        }

        #region CodeBoxColors

        public void SetCodeBoxColors()
        {
            CodeBox.CaretColor = Color.White;
            CodeBox.LineNumberColor = Color.White;
            CodeBox.PaddingBackColor = Color.FromArgb(255, 25, 25, 25);
            CodeBox.IndentBackColor  = Color.FromArgb(255, 25, 25, 25);
            CodeBox.CurrentLineColor = Color.FromArgb(255, 41, 41, 41);
            CodeBox.ServiceLinesColor = Color.FromArgb(255, 25, 25, 25);
            CodeBox.SelectionColor = Color.Red;
            CodeBox.Font = new Font("Liberation Mono", 20);
            CodeBox.Text = standardMsg;
            CodeBox.AutoCompleteBrackets = true;
            CodeBox.AutoIndent = true;
            CodeBox.LineNumberStartValue = 0;
        }

        private class MenuColors : ProfessionalColorTable
        {
            public MenuColors()
            {
                base.UseSystemColors = false;
            }

            public override Color MenuItemSelected => Color.FromArgb(200, 145, 7, 7);
            public override Color MenuItemSelectedGradientBegin => Color.FromArgb(100, 186, 66, 60);
            public override Color MenuItemSelectedGradientEnd => Color.FromArgb(100, 120, 54, 50);
            public override Color MenuItemBorder => Color.FromArgb(255, 26, 26, 26);
            public override Color MenuItemPressedGradientBegin => Color.Transparent;
            public override Color MenuItemPressedGradientEnd => Color.FromArgb(200, 120, 54, 50);
            public override Color MenuBorder => Color.FromArgb(255, 26, 26, 26);
            public override Color ToolStripBorder => Color.FromArgb(255, 26, 26, 26);
            public override Color ToolStripPanelGradientBegin => Color.FromArgb(255, 26, 26, 26);
        }
        #endregion

        #region Highlighting

        Style StringStyle = new TextStyle(new SolidBrush(Color.FromArgb(92, 227, 61)), null, FontStyle.Regular);
        Style VarStyle = new TextStyle(new SolidBrush(Color.FromArgb(0, 157, 227)), null, FontStyle.Bold);
        Style StatementStyle = new TextStyle(new SolidBrush(Color.FromArgb(227, 85, 75)), null, FontStyle.Bold);
        Style AttachStyle = new TextStyle(new SolidBrush(Color.FromArgb(232, 128, 121)), null, FontStyle.Regular);
        Style TypeStyle = new TextStyle(new SolidBrush(Color.FromArgb(24, 115, 163)), null, FontStyle.Regular);
        Style NumberStyle = new TextStyle(new SolidBrush(Color.FromArgb(9, 170, 179)), null, FontStyle.Regular);
        Style SystemFunctionStyle = new TextStyle(new SolidBrush(Color.FromArgb(255, 131, 7)), null, FontStyle.Regular);
        Style UserFunctionStyle = new TextStyle(new SolidBrush(Color.FromArgb(25, 189, 93)), null, FontStyle.Regular);
        Style VariableStyle = new TextStyle(new SolidBrush(Color.FromArgb(255, 212, 125)), null, FontStyle.Regular);
        Style TypeFunctionStyle = new TextStyle(new SolidBrush(Color.FromArgb(159, 212, 85)), null, FontStyle.Regular);
        Style CommentStyle = new TextStyle(new SolidBrush(Color.FromArgb(100, 100, 100)), null, FontStyle.Regular);
        Style WhiteStyle = new TextStyle(Brushes.White, null, FontStyle.Regular);

        public void ReloadHightlighting(TextChangedEventArgs e)
        {
            e.ChangedRange.ClearFoldingMarkers();

            //set folding markers [DarkMode]
            e.ChangedRange.SetFoldingMarkers("{", "}");

            //Dev highlighting [lol]
            //e.ChangedRange.SetStyle(RedStyleDM, @"(ProfessorDJ|Realmy|RedCube)");

            //clear style of range [DarkMode]
            e.ChangedRange.ClearStyle(CommentStyle);

            //quotes
            e.ChangedRange.SetStyle(StringStyle, "\\\"(.*?)\\\"", RegexOptions.Singleline);

            //comment highlighting [DarkMode]
            e.ChangedRange.SetStyle(CommentStyle, @"//.*$", RegexOptions.Multiline);

            e.ChangedRange.SetStyle(AttachStyle, @"(#attach\b)", RegexOptions.Singleline);

            //clear style of range [DarkMode]
            e.ChangedRange.ClearStyle(SystemFunctionStyle);

            //system function highlighting
            e.ChangedRange.SetStyle(SystemFunctionStyle, @"(\bBeep\b|\bListenOnTCPPort\b|\bConnectTCPClient\b|\bGetDirsInDirectory\b|\bGetFilesInDirectory\b|\bGetCursorY\b|\bGetCursorX\b|\bCreateDirectory\b|\bDeleteDirectory\b|\bDeleteFile\b|\bDirectoryExists\b|\bFileExists\b|\bWriteFile\b|\bReadFile\b|\bFloor\b|\bCeil\b|\bInputAction\b|\bSetConsoleForeground\b|\bSetConsoleBackground\b|\bSetCursorVisible\b|\bThread\b|\bGetCursorVisible\b|\bPrint\b|\bInputKey\b|\bInput\b|\bRandom\b|\bVersion\b|\bClear\b|\bSetCursor\b|\bGetSizeX\b|\bGetSizeY\b|\bSetSize\b|\bWrite\b|\bSleep\b)");

            //types
            e.ChangedRange.SetStyle(TypeStyle, @"(\b\?\b|\btcpsocketArr\b|\btcplistenerArr\b|\btcpclientArr\b|\btcpsocket\b|\btcplistener\b|\btcpclient\b|\bany\b|\bbool\b|\bint\b|\bstring\b|\bvoid\b|\bfloat\b|\bthread\b|\banyArr\b|\bboolArr\b|\bintArr\b|\bstringArr\b|\bfloatArr\b|\bthreadArr\b)");

            //function highlighting [DarkMode]
            e.ChangedRange.SetStyle(VarStyle, @"(\bvar\b|\bset\b|\bif\b|\belse\b|\bfunction\b|\btrue\b|\bfalse\b|\bmake\b|\barray\b)", RegexOptions.Singleline);

            //variables
            e.ChangedRange.SetStyle(VariableStyle, @"(\w+(?=\s+<-))");
            e.ChangedRange.SetStyle(VariableStyle, @"(\w+(?=\s+->))");
            e.ChangedRange.SetStyle(VariableStyle, rectCompCheck.Variables);

            //functions
            e.ChangedRange.SetStyle(UserFunctionStyle, @"(?<=\bfunction\s)(\w+)");
            e.ChangedRange.SetStyle(UserFunctionStyle, rectCompCheck.Functions);

            //type functions
            e.ChangedRange.SetStyle(TypeFunctionStyle, @"(?<=\>>\s)(\w+)");

            //statements highlighting
            e.ChangedRange.SetStyle(StatementStyle, @"(\bbreak\b|\bcontinue\b|\bfor\b|\breturn\b|\bto\b|\bwhile\b|\bdo\b|\bdie\b|\bfrom\b)", RegexOptions.Singleline);

            //numbers
            e.ChangedRange.SetStyle(NumberStyle, @"(\b\d+\b)", RegexOptions.Multiline);

            //set standard text color
            e.ChangedRange.SetStyle(WhiteStyle, @".*", RegexOptions.Multiline);
        }

        Style ErrorMarker = new TextStyle(Brushes.White, Brushes.Red, FontStyle.Regular);
        void markError(TextChangedEventArgs e)
        {
            e.ChangedRange.SetStyle(ErrorMarker, ".*", RegexOptions.Multiline);
        }

        #endregion

        Tab makeNewTab()
        {
            var newTab = new Tab();
            newTab.button = (Button)CtrlClone.ControlFactory.CloneCtrl(TabPrefab);
            newTab.code = standardMsg;
            newTab.saved = true;
            newTab.button.Click += Tab_Click;
            newTab.button.FlatStyle = FlatStyle.Flat;
            newTab.button.FlatAppearance.BorderSize = 0;
            newTab.name = "Untitled";
            Controls.Add(newTab.button);
            return newTab;
        }

        void OrderTabs()
        {
            try
            {
                for (int i = 0; i < tabs.Count; i++)
                {
                    tabs[i].button.Location = new Point(5 + (100 * i), 35);
                    tabs[i].button.Text = tabs[i].name;

                    if (!tabs[i].saved)
                        tabs[i].button.Text += "*";

                    tabs[i].button.BackColor = Color.FromArgb(32, 32, 32);
                }
                tabs[currentTab].button.BackColor = Color.FromArgb(64, 41, 41);
            }
            catch { }

            presence.Details = "Working on " + tabs[currentTab].name + "...";
            dc.client.SetPresence(presence);
        }

        private void New_Click(object sender, EventArgs e)
        {
            tabs[currentTab].code = CodeBox.Text;
            tabs.Add(makeNewTab());
            switchTab(tabs.Count - 1);
            tabs[currentTab].name = "Untitled";
            OrderTabs();

            //if (!fileChanged)
            //{
            //    CodeBox.Text = standardMsg;
            //    CodeBox.ClearUndo();
            //    openFile = "";
            //}
            //else
            //{
            //    var result = MessageBox.Show("You have unsaved changes!\nAre you sure you want to create a new File?", "Warning!", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);https://pcpartpicker.com/user/RedCooooobe/saved/CdnKpg
            //    if (result == DialogResult.Yes)
            //    {
            //        CodeBox.Text = standardMsg;
            //        CodeBox.ClearUndo();
            //        fileChanged = false;
            //        openFile = "";
            //    }
            //}
            //updateWindowTitle();
        }

        private void Open_Click(object sender, EventArgs e)
        {
            //if(fileChanged)
            //{
            //    var result = MessageBox.Show("You have unsaved changes!\nAre you sure you want to open a File?", "Warning!", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
            //    if (result != DialogResult.Yes)
            //    {
            //        return;
            //    }
            //}

            openFileDialog1.Filter = "ReCT code files (*.rct)|*.rct|All files (*.*)|*.*";

            ((ToolStripMenuItem)sender).Owner.Hide();

            var res = openFileDialog1.ShowDialog();

            if (res != DialogResult.OK)
                return;

            if (tabs.Count != 1 || tabs[0].name != "Untitled" || !tabs[0].saved)
            {
                tabs[currentTab].code = CodeBox.Text;
                tabs.Add(makeNewTab());
                switchTab(tabs.Count - 1);
            }
                        

            using (StreamReader sr = new StreamReader(new FileStream(openFileDialog1.FileName, FileMode.Open)))
            {
                CodeBox.Text = sr.ReadToEnd();
                CodeBox.ClearUndo();
                sr.Close();
            }

            tabs[currentTab].name = Path.GetFileName(openFileDialog1.FileName);
            tabs[currentTab].path = openFileDialog1.FileName;
            tabs[currentTab].saved = true;
            OrderTabs();

            Properties.Settings.Default.LastOpenFile = openFileDialog1.FileName;
            Properties.Settings.Default.Save();
        }

        private void Save_Click(object sender, EventArgs e)
        {
            if(tabs[currentTab].path == "" || tabs[currentTab].path == null)
            {
                SaveAs_Click(sender, e);
                return;
            }

            using (StreamWriter sw = new StreamWriter(new FileStream(tabs[currentTab].path, FileMode.Create)))
            {
                sw.Write(CodeBox.Text);
                sw.Close();
            }

            tabs[currentTab].saved = true;

            OrderTabs();
        }

        private void SaveAs_Click(object sender, EventArgs e)
        {
            saveFileDialog1.Filter = "ReCT code files (*.rct)|*.rct|All files (*.*)|*.*";
            var res = saveFileDialog1.ShowDialog();

            if (res != DialogResult.OK)
                return;

            using (StreamWriter sw = new StreamWriter(new FileStream(saveFileDialog1.FileName, FileMode.Create)))
            {
                sw.Write(CodeBox.Text);
                sw.Close();
            }

            tabs[currentTab].path = saveFileDialog1.FileName;
            tabs[currentTab].name = Path.GetFileName(saveFileDialog1.FileName);
            tabs[currentTab].saved = true;
            OrderTabs();
        }


        void edited()
        {
            if (tabSwitch)
                return;

            try
            {
                if (tabs[currentTab].saved)
                {
                    tabs[currentTab].saved = false;
                    OrderTabs();
                }
            }
            catch{}
        }

        private void timer1_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                rectCompCheck.Variables = "";
                if (CodeBox.Text != "")
                {
                    rectCompCheck.Check(CodeBox.Text, this, tabs[currentTab].path);
                    CodeBox.ClearStylesBuffer();
                    ReloadHightlighting(new TextChangedEventArgs(CodeBox.Range));

                    List<string> ACItems = new List<string>();

                    foreach (string s in standardAC)
                    {
                        ACItems.Add(s);
                    }
                    foreach (ReCT.CodeAnalysis.Symbols.FunctionSymbol f in rectCompCheck.functions)
                    {
                        ACItems.Add(f.Name);
                    }
                    foreach (ReCT.CodeAnalysis.Symbols.VariableSymbol v in rectCompCheck.variables)
                    {
                        ACItems.Add(v.Name);
                    }

                    ReCTAutoComplete.Items = ACItems.ToArray();
                }
            }
            catch(Exception ee)
            {
                //Console.WriteLine(ee);
            }
        }

        private void autoFormatToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CodeBox.DoAutoIndent();
        }

        private void Build_Click(object sender, EventArgs e)
        {
            Typechecker.Enabled = false;
            errorBox.Hide();
            saveFileDialog1.Filter = "Launcher (*.cmd)|*.cmd|All files (*.*)|*.*";
            var res = saveFileDialog1.ShowDialog();

            if (res != DialogResult.OK)
                return;

            if (fileChanged)
                Save_Click(this, new EventArgs());

            ReCT_Compiler.CompileRCTBC (saveFileDialog1.FileName, tabs[currentTab].path, errorBox);

            System.Diagnostics.Process.Start("explorer.exe", string.Format("/select,\"{0}\"", saveFileDialog1.FileName));
            Typechecker.Enabled = true;
        }

        private void CodeBox_Chnaged(object sender, TextChangedEventArgs e)
        {
            ReloadHightlighting(e);

            edited();
        }

        private void Play_Click(object sender, EventArgs e)
        {
            errorBox.Hide();

            try
            {
                if(running != null)
                    KillProcessAndChildren(running.Id);
            }
            catch { }

            if (!tabs[currentTab].saved)
                Save_Click(this, new EventArgs());

            //clear Builder dir

            if (Directory.Exists("Builder"))
                ReCT_Compiler.ForceDeleteFilesAndFoldersRecursively("Builder");
            if (!Directory.Exists("Builder"))
                Directory.CreateDirectory("Builder");

            if (!ReCT_Compiler.CompileRCTBC("Builder/" + Path.GetFileNameWithoutExtension(tabs[currentTab].path) + ".cmd", tabs[currentTab].path, errorBox)) return;

            string strCmdText = $"/K cd \"{Path.GetFullPath($"Builder")}\" & cls & \"{Path.GetFileNameWithoutExtension(tabs[currentTab].path)}.cmd\"";

            running = new Process();
            running.StartInfo.FileName = "CMD.exe";
            running.StartInfo.Arguments = strCmdText;

            if(Properties.Settings.Default.Maximize)
                running.StartInfo.WindowStyle = ProcessWindowStyle.Maximized;

            running.Start();
        }

        private void Stop_Click(object sender, EventArgs e)
        {
            try
            {
                KillProcessAndChildren(running.Id);
            } catch {}
        }

        private static void KillProcessAndChildren(int pid)
        {
            if (pid == 0)
            {
                return;
            }
            ManagementObjectSearcher searcher = new ManagementObjectSearcher
                    ("Select * From Win32_Process Where ParentProcessID=" + pid);
            ManagementObjectCollection moc = searcher.Get();
            foreach (ManagementObject mo in moc)
            {
                KillProcessAndChildren(Convert.ToInt32(mo["ProcessID"]));
            }
            try
            {
                Process proc = Process.GetProcessById(pid);
                proc.Kill();
            }
            catch (ArgumentException)
            {
                // Process already exited.
            }
        }

        private void Play_MouseEnter(object sender, EventArgs e)
        {
            changeIcon(Play, 0, true);
        }

        private void Play_MouseLeave(object sender, EventArgs e)
        {
            changeIcon(Play, 0, false);
        }

        private void Stop_MouseEnter(object sender, EventArgs e)
        {
            changeIcon(Stop, 1, true);
        }

        private void Stop_MouseLeave(object sender, EventArgs e)
        {
            changeIcon(Stop, 1, false);
        }

        private void Build_MouseEnter(object sender, EventArgs e)
        {
            changeIcon(Build, 2, true);
        }

        private void Build_MouseLeave(object sender, EventArgs e)
        {
            changeIcon(Build, 2, false);
        }

        private void CodeBox_Load(object sender, EventArgs e)
        {

        }

        private void toolTip1_Popup(object sender, PopupEventArgs e)
        {

        }

        private void buildToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Play_Click(sender, e);
        }

        private void runToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Build_Click(sender, e);
        }

        private void Tab_Click(object sender, EventArgs e)
        {
            switchTab(findPressedTab(sender));
        }

        int findPressedTab(object button)
        {
            for(int i = 0; i < tabs.Count; i++)
            {
                if (tabs[i].button == (Button)button)
                    return i;
            }
            return 0;
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            if (tabs.Count == 1)
                return;

            if(!tabs[currentTab].saved)
            {
                var result = MessageBox.Show("WAIT!\nYou have some unsaved changes!\nDo you want to save them?", "Warning!", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning); https://pcpartpicker.com/user/RedCooooobe/saved/CdnKpg
                if (result == DialogResult.Yes)
                {
                    Save_Click(this, new EventArgs());
                }
                if (result == DialogResult.Cancel)
                {
                    return;
                }
            }

            int tabToDelete = currentTab;

            Controls.Remove(tabs[tabToDelete].button);
            tabs.RemoveAt(tabToDelete);

            switchTab(0);
            OrderTabs();
        }

        void switchTab(int tab)
        {
            presence.Details = "Working on " + tabs[currentTab].name + "...";
            dc.client.SetPresence(presence);

            tabSwitch = true;

            if (tabs.Count == 1)
                return;

            if (tab != currentTab)
            {
                if (currentTab < tabs.Count)
                {
                    tabs[currentTab].code = CodeBox.Text;
                    tabs[currentTab].button.BackColor = Color.FromArgb(32, 32, 32);
                }
                currentTab = tab;
                CodeBox.ClearUndo();
            }
            
            tabs[currentTab].button.BackColor = Color.FromArgb(64, 41, 41);
            CodeBox.Text = tabs[currentTab].code;

            tabswitchTimer.Start();
        }

        private void tabswitchTimer_Tick(object sender, EventArgs e)
        {
            tabswitchTimer.Stop();
            tabSwitch = false;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            foreach(Tab t in tabs)
            {
                if (!t.saved)
                {
                    var result = MessageBox.Show($"WAIT!\nYou have some unsaved changes in '{t.name}'!\nDo you want to save them?", "Warning!", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning); https://pcpartpicker.com/user/RedCooooobe/saved/CdnKpg
                    if (result == DialogResult.Yes)
                    {
                        switchTab(findPressedTab(t.button));
                        Save_Click(this, new EventArgs());
                    }
                    if (result == DialogResult.Cancel)
                    {
                        e.Cancel = true;
                        return;
                    }
                }
            }
        }

        public void updateFromSettings()
        {
            switch(Properties.Settings.Default.Autosave)
            {
                case 0:
                    Autosave.Stop();
                    break;
                case 1:
                    Autosave.Start();
                    Autosave.Interval = 60000;
                    break;
                case 2:
                    Autosave.Start();
                    Autosave.Interval = 60000 * 2;
                    break;
                case 3:
                    Autosave.Start();
                    Autosave.Interval = 60000 * 5;
                    break;
                case 4:
                    Autosave.Start();
                    Autosave.Interval = 60000 * 10;
                    break;
            }

            if (Properties.Settings.Default.MaximizeRect)
                this.WindowState = FormWindowState.Maximized;
        }

        private void settingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            settings.Show();
        }

        private void Autosave_Tick(object sender, EventArgs e)
        {
            Console.WriteLine("AutosaveTick!");
            if (tabs[currentTab].path != "" && tabs[currentTab].path != null)
                Save_Click(null, new EventArgs());
        }

        private void MaxTimer_Tick(object sender, EventArgs e)
        {
            ShowWindow(running.MainWindowHandle, 3);
            MaxTimer.Stop();
        }

        private void openLastFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Properties.Settings.Default.LastOpenFile != "")
            {
                if (tabs.Count != 1 || tabs[0].name != "Untitled" || !tabs[0].saved)
                {
                    tabs[currentTab].code = CodeBox.Text;
                    tabs.Add(makeNewTab());
                    switchTab(tabs.Count - 1);
                }


                using (StreamReader sr = new StreamReader(new FileStream(Properties.Settings.Default.LastOpenFile, FileMode.Open)))
                {
                    CodeBox.Text = sr.ReadToEnd();
                    CodeBox.ClearUndo();
                    sr.Close();
                }

                tabs[currentTab].name = Path.GetFileName(Properties.Settings.Default.LastOpenFile);
                tabs[currentTab].path = Properties.Settings.Default.LastOpenFile;
                tabs[currentTab].saved = true;
                OrderTabs();
            }
        }

        private void reloadHighlightingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var code = CodeBox.Text;
            var pos = CodeBox.Selection;
            CodeBox.Text = "";
            reload(code, pos);
        }

        private async Task reload(string c, Range p)
        {
            await Task.Delay(10);
            CodeBox.Text = c;
            CodeBox.Selection = p;
            CodeBox.Focus();
        }

        private void forceRunToolStripMenuItem_Click(object sender, EventArgs e)
        {
            forceRun();
        }

        private async Task forceRun()
        {
            bool res;
            int counter = 0;
            do
            {
                errorBox.Hide();
                await Task.Delay(10);
                counter++;

                if (counter > 10)
                    break;


                try
                {
                    if (running != null)
                        KillProcessAndChildren(running.Id);
                }
                catch { }

                if (!tabs[currentTab].saved)
                    Save_Click(this, new EventArgs());

                //clear Builder dir

                if (Directory.Exists("Builder"))
                    ReCT_Compiler.ForceDeleteFilesAndFoldersRecursively("Builder");
                if (!Directory.Exists("Builder"))
                    Directory.CreateDirectory("Builder");

                res = ReCT_Compiler.CompileRCTBC("Builder/" + Path.GetFileNameWithoutExtension(tabs[currentTab].path) + ".cmd", tabs[currentTab].path, errorBox);
                if (!res) continue;

                string strCmdText = $"/K cd \"{Path.GetFullPath($"Builder")}\" & cls & \"{Path.GetFileNameWithoutExtension(tabs[currentTab].path)}.cmd\"";

                running = new Process();
                running.StartInfo.FileName = "CMD.exe";
                running.StartInfo.Arguments = strCmdText;

                if (Properties.Settings.Default.Maximize)
                    running.StartInfo.WindowStyle = ProcessWindowStyle.Maximized;

                running.Start();

                return;
            } while (!res);
        }
    }

    class Tab
    {
        public Button button;
        public string code;
        public string name;
        public string path;
        public bool saved;
    }
}
