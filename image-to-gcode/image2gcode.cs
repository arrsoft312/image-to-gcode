using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using Microsoft.Win32;

[assembly: AssemblyTitle(image2gcode.AppTitle)]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany(image2gcode.AppAuthor)]
[assembly: AssemblyProduct(image2gcode.AppTitle)]
[assembly: AssemblyFileVersion(image2gcode.AppVersion)]
[assembly: AssemblyCopyright(image2gcode.AppCopyright)]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

[assembly: AssemblyVersion(image2gcode.AppVersion)]
[assembly: ComVisible(false)]

partial class image2gcode:Form {
    public const string AppTitle = "image2gcode";
    public const string AppVersion = "3.2.0";
    public const string AppVersionBuild = "2022-12-17";
    public const string AppAuthor = "Artur Kurpukov";
    public const string AppCopyright = "Copyright (C) 2017-2023 Artur Kurpukov";
    private const string SettingsVersion = "3.2";
    
    private const float PI = 3.1415926535897931F;
    private const float MmPerInch = 25.4F;
    
    private const int BezierSegmentsCount = 1000;
    
    private const byte ImColorUltraWhite = 255;
    private const byte ImColorBlack = 0;
    private const byte ImColorStep = 5;
    private const byte ImColorWhite = (254/ImColorStep*ImColorStep);
    private const int ImColorCount = (ImColorWhite/ImColorStep+1);
    
    private const int PresetCount = 14;
    
    private readonly object imResizeLock = new object();
    
    private readonly float[] linearGraph = new float[] { -1F, -1F, -1F, 0.35F, 0.35F, 0.65F, 0.65F, };
    
    private readonly ResourceManager resources = new ResourceManager(typeof(I2GResources));
    private readonly EventWaitHandle bWorkerWaitHandle = new EventWaitHandle(true, EventResetMode.AutoReset, null);
    
    private readonly Font regularFont = new Font("Segoe UI", 9F, FontStyle.Regular);
    private readonly Font boldFont = new Font("Segoe UI", 9F, FontStyle.Bold);
    
    private RegistryKey settings;
    
    private char[] invalidFileNameChars;
    private ImageCodecInfo bmpEncoder;
    
    private Image[] presetIcons = new Image[PresetCount];
    private Image presetCheckedIcon;
    
    private CultureInfo invariantCulture;
    private CultureInfo culture;
    
    private string comPort;
    private int baudRate;
    
    private float imDpiX = 72F;
    private float imDpiY = 72F;
    private bool imLockDpiY = false;
    
    private float imLeft;
    private float imTop;
    
    private int imWidth;
    private int imHeight;
    
    private bool imLockAspectRatio = true;
    private float imAspectRatio;
    
    private StreamReader customGraphs;
    
    private int prevPreset = -1;
    private int activePreset;
    
    private bool bWorker2SendToDevice;
    private bool bWorker2ReadFromFile;
    
    private bool bWorkerIsBusy = true;
    
    [Flags]
    private enum BWorkerFlags {
        ImageChanged = 0x2000,
        CalcJobTime = 0x4000,
        RedrawPreview = 0x200000,
        RedrawOrigin = 0x100000,
        Exit = 0x1000,
    }
    
    private BWorkerFlags _bWorkerFlags = 0;
    private BWorkerFlags bWorkerFlags {
        set
        {
            if (value == 0) {
                return;
            }
            
            _bWorkerFlags |= value;
            bWorkerWaitHandle.Set();
        }
    }
    
    [STAThread]
    internal static void Main() {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new image2gcode());
    }
    
    public image2gcode() {
        this.Font = new Font("Segoe UI", 9F);
        InitializeComponent();
        
        openFileDialog1.Title = AppTitle;
        openFileDialog2.Title = AppTitle;
        saveFileDialog1.Title = AppTitle;
        saveFileDialog2.Title = AppTitle;
        openFileDialog3.Title = AppTitle;
        saveFileDialog3.Title = AppTitle;
        
        defaultToolStripMenuItem1.Tag = linearGraph;
        defaultToolStripMenuItem2.Tag = linearGraph;
        
        x4axisToolStripMenuItem.Tag = new float[] { 4000F, 2820F, 350F, 0.206F, 0.279F, 0.168F, 0.912F, };
        
        rotate270ToolStripMenuItem.Tag = RotateFlipType.Rotate270FlipNone;
        rotate90ToolStripMenuItem.Tag = RotateFlipType.Rotate90FlipNone;
        flipXYToolStripMenuItem.Tag = RotateFlipType.RotateNoneFlipXY;
        flipXToolStripMenuItem.Tag = RotateFlipType.RotateNoneFlipX;
        flipYToolStripMenuItem.Tag = RotateFlipType.RotateNoneFlipY;
        
        panel1.Tag = gcSpeedGraph;
        panel2.Tag = gcPowerGraph;
        
        this.Text = (AppTitle + " v" + AppVersion);
    }
    
    private void Image2gcodeLoad(object sender, EventArgs e) {
        for (int i = 0; i < ImColorCount; i++) {
            gvBrushes[i] = new SolidBrush(Color.FromArgb(-16777216 + i*ImColorStep*65536 + i*ImColorStep*256 + i*ImColorStep));
        }
        
        settings = Registry.CurrentUser.CreateSubKey("SOFTWARE\\" + AppTitle + "\\v" + SettingsVersion + "\\Settings");
        
        this.SuspendLayout2();
        this.Left = settings.GetInt32("WindowLeft", this.Left);
        this.Top = settings.GetInt32("WindowTop", this.Top);
        this.Width = settings.GetInt32("WindowWidth", this.Width);
        this.Height = settings.GetInt32("WindowHeight", this.Height);
        if (settings.GetInt32("WindowMaximized", 0) != 0) {
            this.WindowState = FormWindowState.Maximized;
        }
        this.ResumeLayout2();
        
        invariantCulture = CultureInfo.InvariantCulture;
        culture = new CultureInfo(settings.GetInt32("UICulture", CultureInfo.InstalledUICulture.LCID), false);
        
        toolStripStatusLabel1.Text = resources.GetString("Status_AppIsLoading", culture);
    }
    
    private unsafe void Image2gcodeShown(object sender, EventArgs e) {
        comPort = settings.GetString("ComPort", "COM1");
        baudRate = settings.GetInt32("BaudRate", 230400);
        
        invalidFileNameChars = Path.GetInvalidFileNameChars();
        
        Guid bmpFormatID = ImageFormat.Bmp.Guid;
        foreach (ImageCodecInfo imageEncoder in ImageCodecInfo.GetImageEncoders()) {
            if (imageEncoder.FormatID == bmpFormatID) {
                bmpEncoder = imageEncoder;
                break;
            }
        }
        
        checkBox1.Checked = (settings.GetInt32("Image1bitPalette", 1) != 0);
        comboBox1.SelectedItem = (ImageDithering)settings.GetInt32("ImageDithering", (int)ImageDithering.Jarvis);
        
        imColorTable = (byte*)Marshal.AllocHGlobal((IntPtr)256);
        
        Parallel.For(0, 256, (i) => {
            byte color = ImColorUltraWhite;
            
            int j = ((i-color)*(i-color));
            for (int n = 0; n <= ImColorWhite; n += ImColorStep) {
                int k = ((i-n)*(i-n));
                if (k < j) {
                    color = (byte)n;
                    j = k;
                }
            }
            
            imColorTable[i] = color;
        });
        
        for (int i = 255; i >= 0; i--) {
            if (imColorTable[i] != ImColorUltraWhite) {
                trackBar1.Maximum = i;
                break;
            }
        }
        
        Control_ValueChanged(trackBar1, EventArgs.Empty);
        Control_ValueChanged(trackBar6, EventArgs.Empty);
        Control_ValueChanged(trackBar2, EventArgs.Empty);
        Control_ValueChanged(trackBar3, EventArgs.Empty);
        Control_ValueChanged(trackBar4, EventArgs.Empty);
        
        comboBox2.Text = settings.GetSingle("ImageDpiX", 200F).ToString();
        comboBox3.Text = settings.GetSingle("ImageDpiY", 200F).ToString();
        checkBox4.Checked = (settings.GetInt32("ImageLockDpiY", 1) != 0);
        
        checkBox5.CheckedChanged += (sender2, e2) => {
            ((CheckBox)sender2).ImageIndex = (((CheckBox)sender2).Checked ? 1 : 0);
        };
        
        comboBox4.SelectedItem = (InterpolationMode)settings.GetInt32("ImageInterpolation", (int)InterpolationMode.HighQualityBicubic);
        
        presetCheckedIcon = (Image)resources.GetObject("PresetCheckedIcon", invariantCulture);
        
        using (Font font = new Font(FontFamily.GenericMonospace, 9F, FontStyle.Bold, GraphicsUnit.Pixel)) {
            RectangleF rectF = new RectangleF(0F, 1F, 16F, 14F);
            StringFormat format = new StringFormat((StringFormatFlags)0, 0);
            
            format.Alignment = StringAlignment.Center;
            format.LineAlignment = StringAlignment.Center;
            
            for (int i = 0; i < PresetCount; i++) {
                string idx = (1+i).ToString("00", invariantCulture);
                
                preset[i] = Registry.CurrentUser.CreateSubKey("SOFTWARE\\" + AppTitle + "\\v" + SettingsVersion + "\\Preset" + idx);
                
                tableLayoutPanel31.Controls[i].Text = idx;
                tableLayoutPanel31.Controls[i].Tag = i;
                
                presetIcons[i] = new Bitmap(16, 16, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(presetIcons[i])) {
                    g.PageUnit = GraphicsUnit.Pixel;
                    using (Brush brush = new SolidBrush(tableLayoutPanel31.Controls[i].BackColor)) {
                        g.FillRectangle(brush, 0, 1, 16, 14);
                    }
                    using (Brush brush = new SolidBrush(tableLayoutPanel31.Controls[i].ForeColor)) {
                        g.DrawString(idx, font, brush, rectF, format);
                    }
                }
                
                ToolStripMenuItem toolStripItem1 = new ToolStripMenuItem();
                toolStripItem1.Image = presetIcons[i];
                toolStripItem1.Tag = i;
                
                ToolStripMenuItem toolStripItem2 = new ToolStripMenuItem();
                toolStripItem2.Image = presetIcons[i];
                toolStripItem2.Tag = i;
                
                ToolStripMenuItem toolStripItem3 = new ToolStripMenuItem();
                toolStripItem3.Tag = i;
                toolStripItem3.Click += PresetButtonClick;
                
                loadPresetToolStripMenuItem.DropDownItems.Add(toolStripItem1);
                savePresetToolStripMenuItem.DropDownItems.Add(toolStripItem2);
                presetToolStripMenuItem.DropDownItems.Add(toolStripItem3);
            }
        }
        
        try {
            customGraphs = new StreamReader("CustomGraphs.txt", Encoding.ASCII, false);
        } catch {
            customGraphs = StreamReader.Null;
        }
        
        Func<float, float> constrain_0_1 = (n) => {
            if (n >= 1F) {
                return 1F;
            }
            if (n <= 0F) {
                return 0F;
            }
            return n;
        };
        
        while (!customGraphs.EndOfStream) {
            string line = customGraphs.ReadLine();
            if (line == "") {
                continue;
            }
            
            string[] s = line.Split(new string[] { ",", }, 10, StringSplitOptions.None);
            if (s.Length != 9) {
                continue;
            }
            
            float[] tag = new float[7];
            try {
                tag[0] = Single.Parse(s[2], invariantCulture);
                tag[1] = Single.Parse(s[3], invariantCulture);
                tag[2] = Single.Parse(s[4], invariantCulture);
                tag[3] = constrain_0_1(Single.Parse(s[5], invariantCulture));
                tag[4] = constrain_0_1(Single.Parse(s[6], invariantCulture));
                tag[5] = constrain_0_1(Single.Parse(s[7], invariantCulture));
                tag[6] = constrain_0_1(Single.Parse(s[8], invariantCulture));
            } catch {
                continue;
            }
            
            ToolStripMenuItem toolStripItem = new ToolStripMenuItem();
            toolStripItem.DisplayStyle = ToolStripItemDisplayStyle.Text;
            toolStripItem.Tag = tag;
            toolStripItem.Text = s[1];
            
            if (s[0] == "F" || s[0] == "f") {
                resetSpeedGraphToolStripMenuItem.DropDownItems.Add(toolStripItem);
            } else if (s[0] == "S" || s[0] == "s") {
                resetPowerGraphToolStripMenuItem.DropDownItems.Add(toolStripItem);
            }
        }
        
        toolStripSeparator14.Visible = (resetSpeedGraphToolStripMenuItem.DropDownItems.Count > 3);
        toolStripSeparator15.Visible = (resetPowerGraphToolStripMenuItem.DropDownItems.Count > 2);
        
        activePreset = settings.GetInt32("ActivePreset", -1);
        if (activePreset < 0 || activePreset >= PresetCount) {
            activePreset = 0;
        }
        
        PresetButtonClick(tableLayoutPanel31.Controls[activePreset], EventArgs.Empty);
        
        graphView1 = new GraphView();
        graphView1.Width = settings.GetInt32("GraphViewWidth", graphView1.Width);
        graphView1.Height = settings.GetInt32("GraphViewHeight", graphView1.Height);
        graphView1.Left = settings.GetInt32("GraphViewLeft", (this.Left + (this.Width - graphView1.Width)/2));
        graphView1.Top = settings.GetInt32("GraphViewTop", (this.Top + (this.Height - graphView1.Height)/2));
        
        graphView1.Load += GraphViewLoad;
        graphView1.Deactivate += GraphViewDeactivate;
        graphView1.Paint += GraphViewPaint;
        graphView1.MouseMove += GraphViewMouseMove;
        
        ditherEmptyTable = (byte*)Marshal.AllocHGlobal((IntPtr)65536);
        
        ditherTable_7_16 = (byte*)Marshal.AllocHGlobal((IntPtr)65536);
        ditherTable_3_16 = (byte*)Marshal.AllocHGlobal((IntPtr)65536);
        ditherTable_5_16 = (byte*)Marshal.AllocHGlobal((IntPtr)65536);
        ditherTable_1_16 = (byte*)Marshal.AllocHGlobal((IntPtr)65536);
        
        ditherTable_7_48 = (byte*)Marshal.AllocHGlobal((IntPtr)65536);
        ditherTable_5_48 = (byte*)Marshal.AllocHGlobal((IntPtr)65536);
        ditherTable_3_48 = (byte*)Marshal.AllocHGlobal((IntPtr)65536);
        ditherTable_1_48 = (byte*)Marshal.AllocHGlobal((IntPtr)65536);
        
        ditherTable_8_42 = (byte*)Marshal.AllocHGlobal((IntPtr)65536);
        ditherTable_4_42 = (byte*)Marshal.AllocHGlobal((IntPtr)65536);
        ditherTable_2_42 = (byte*)Marshal.AllocHGlobal((IntPtr)65536);
        ditherTable_1_42 = (byte*)Marshal.AllocHGlobal((IntPtr)65536);
        
        ditherTable_1_8 = (byte*)Marshal.AllocHGlobal((IntPtr)65536);
        
        ditherTable_8_32 = (byte*)Marshal.AllocHGlobal((IntPtr)65536);
        ditherTable_4_32 = (byte*)Marshal.AllocHGlobal((IntPtr)65536);
        ditherTable_2_32 = (byte*)Marshal.AllocHGlobal((IntPtr)65536);
        
        ditherTable_5_32 = (byte*)Marshal.AllocHGlobal((IntPtr)65536);
        ditherTable_3_32 = (byte*)Marshal.AllocHGlobal((IntPtr)65536);
        
        Func<int, byte> constrain_0_255 = (n) => {
            if (n >= 255) {
                return 255;
            }
            if (n <= 0) {
                return 0;
            }
            return (byte)n;
        };
        
        Parallel.For(0, 256, (i) => {
            for (int j = 0; j < 256; j++) {
                ditherEmptyTable[i*256+j] = (byte)i;
                
                int grayError = j;
                if (j > 127) {
                    grayError -= 255;
                }
                
                ditherTable_7_16[i*256+j] = constrain_0_255(i + grayError*7/16);
                ditherTable_3_16[i*256+j] = constrain_0_255(i + grayError*3/16);
                ditherTable_5_16[i*256+j] = constrain_0_255(i + grayError*5/16);
                ditherTable_1_16[i*256+j] = constrain_0_255(i + grayError*1/16);
                
                ditherTable_7_48[i*256+j] = constrain_0_255(i + grayError*7/48);
                ditherTable_5_48[i*256+j] = constrain_0_255(i + grayError*5/48);
                ditherTable_3_48[i*256+j] = constrain_0_255(i + grayError*3/48);
                ditherTable_1_48[i*256+j] = constrain_0_255(i + grayError*1/48);
                
                ditherTable_8_42[i*256+j] = constrain_0_255(i + grayError*8/42);
                ditherTable_4_42[i*256+j] = constrain_0_255(i + grayError*4/42);
                ditherTable_2_42[i*256+j] = constrain_0_255(i + grayError*2/42);
                ditherTable_1_42[i*256+j] = constrain_0_255(i + grayError*1/42);
                
                ditherTable_1_8[i*256+j] = constrain_0_255(i + grayError*1/8);
                
                ditherTable_8_32[i*256+j] = constrain_0_255(i + grayError*8/32);
                ditherTable_4_32[i*256+j] = constrain_0_255(i + grayError*4/32);
                ditherTable_2_32[i*256+j] = constrain_0_255(i + grayError*2/32);
                
                ditherTable_5_32[i*256+j] = constrain_0_255(i + grayError*5/32);
                ditherTable_3_32[i*256+j] = constrain_0_255(i + grayError*3/32);
            }
        });
        
        progressForm1 = new ProgressForm();
        progressForm1.Shown += (sender2, e2) => {
            backgroundWorker2.RunWorkerAsync(null);
        };
        progressForm1.FormClosing += (sender2, e2) => {
            backgroundWorker2.CancelAsync();
            while (backgroundWorker2.IsBusy) {
                Application.DoEvents();
            }
        };
        
        progressForm3 = new ProgressForm2(resources.GetString("PF_RetrievingGrblSettings", culture));
        progressForm3.Load += (sender2, e2) => {
            ((Form)sender2).Left = (this.Left + (this.Width - ((Form)sender2).Width)/2);
            ((Form)sender2).Top = (this.Top + (this.Height - ((Form)sender2).Height)/2);
        };
        
        grblSettings1 = new GrblSettingsForm();
        grblSettings1.button1.Click += GrblSettingsWrite;
        grblSettings1.button3.Click += GrblSettingsWrite;
        
        foreach (CultureInfo culture2 in CultureInfo.GetCultures(CultureTypes.NeutralCultures)) {
            if (!File.Exists(culture2.Name + "\\image2gcode.resources.dll")) {
                continue;
            }
            
            ToolStripMenuItem toolStripItem = new ToolStripMenuItem();
            toolStripItem.DisplayStyle = ToolStripItemDisplayStyle.Text;
            toolStripItem.Tag = culture2.LCID;
            toolStripItem.Text = culture2.EnglishName;
            
            languageToolStripMenuItem.DropDownItems.Add(toolStripItem);
        }
        
        ApplyTranslation();
        
        menuStrip1.Enabled = true;
        toolStrip1.Enabled = true;
        tableLayoutPanel1.Enabled = true;
        statusStrip1.Enabled = true;
        
        toolStripStatusLabel1.Text = resources.GetString("Status_Ready", culture);
        
        string[] argv = Environment.GetCommandLineArgs();
        if (argv.Length != 2) {
            return;
        }
        
        if (LoadImage(argv[1])) {
            openFileDialog1.FileName = argv[1];
            string fileName = Path.GetFileNameWithoutExtension(argv[1]);
            saveFileDialog1.FileName = (fileName + ".nc");
            saveFileDialog2.FileName = (fileName + ".bmp");
        }
    }
    
    private unsafe void Image2gcodeClosing(object sender, FormClosingEventArgs e) {
        menuStrip1.Enabled = false;
        toolStrip1.Enabled = false;
        tableLayoutPanel1.Enabled = false;
        statusStrip1.Enabled = false;
        
        bWorkerFlags = BWorkerFlags.Exit;
        while (backgroundWorker1.IsBusy) {
            Application.DoEvents();
        }
        
        toolStripStatusLabel1.Text = resources.GetString("Status_AppIsClosing", culture);
        
        customGraphs.Close();
        
        Marshal.FreeHGlobal((IntPtr)imColorTable);
        
        Marshal.FreeHGlobal((IntPtr)ditherEmptyTable);
        
        Marshal.FreeHGlobal((IntPtr)ditherTable_7_16);
        Marshal.FreeHGlobal((IntPtr)ditherTable_3_16);
        Marshal.FreeHGlobal((IntPtr)ditherTable_5_16);
        Marshal.FreeHGlobal((IntPtr)ditherTable_1_16);
        
        Marshal.FreeHGlobal((IntPtr)ditherTable_7_48);
        Marshal.FreeHGlobal((IntPtr)ditherTable_5_48);
        Marshal.FreeHGlobal((IntPtr)ditherTable_3_48);
        Marshal.FreeHGlobal((IntPtr)ditherTable_1_48);
        
        Marshal.FreeHGlobal((IntPtr)ditherTable_8_42);
        Marshal.FreeHGlobal((IntPtr)ditherTable_4_42);
        Marshal.FreeHGlobal((IntPtr)ditherTable_2_42);
        Marshal.FreeHGlobal((IntPtr)ditherTable_1_42);
        
        Marshal.FreeHGlobal((IntPtr)ditherTable_1_8);
        
        Marshal.FreeHGlobal((IntPtr)ditherTable_8_32);
        Marshal.FreeHGlobal((IntPtr)ditherTable_4_32);
        Marshal.FreeHGlobal((IntPtr)ditherTable_2_32);
        
        Marshal.FreeHGlobal((IntPtr)ditherTable_5_32);
        Marshal.FreeHGlobal((IntPtr)ditherTable_3_32);
        
        Marshal.FreeHGlobal(ibScan0);
        
        FormWindowState windowState = this.WindowState;
        settings.SetInt32("WindowMaximized", (windowState == FormWindowState.Maximized));
        if (windowState == FormWindowState.Normal) {
            settings.SetInt32("WindowWidth", this.Width);
            settings.SetInt32("WindowHeight", this.Height);
            settings.SetInt32("WindowLeft", this.Left);
            settings.SetInt32("WindowTop", this.Top);
        }
        
        settings.SetInt32("UICulture", culture.LCID);
        
        settings.SetString("ComPort", comPort);
        settings.SetInt32("BaudRate", baudRate);
        
        settings.SetInt32("Image1bitPalette", im1bitPalette);
        settings.SetInt32("ImageDithering", imDithering);
        settings.SetSingle("ImageDpiX", imDpiX);
        settings.SetSingle("ImageDpiY", imDpiY);
        settings.SetInt32("ImageLockDpiY", imLockDpiY);
        settings.SetInt32("ImageInterpolation", imInterpolation);
        
        settings.SetInt32("ActivePreset", activePreset);
        SavePreset(activePreset);
        
        settings.SetInt32("GraphViewWidth", graphView1.Width);
        settings.SetInt32("GraphViewHeight", graphView1.Height);
        settings.SetInt32("GraphViewLeft", graphView1.Left);
        settings.SetInt32("GraphViewTop", graphView1.Top);
        
        settings.Flush();
    }
    
    private void ImageBoxDragEnter(object sender, DragEventArgs e) {
        string[] fileNames = (string[])e.Data.GetData(DataFormats.FileDrop, false);
        if (fileNames.Length == 1) {
            e.Effect = DragDropEffects.All;
        } else {
            e.Effect = DragDropEffects.None;
        }
    }
    
    private void ImageBoxDragDrop(object sender, DragEventArgs e) {
        string[] fileNames = (string[])e.Data.GetData(DataFormats.FileDrop, false);
        if (fileNames.Length != 1) {
            return;
        }
        
        if (LoadImage(fileNames[0])) {
            openFileDialog1.FileName = fileNames[0];
            string fileName = Path.GetFileNameWithoutExtension(fileNames[0]);
            saveFileDialog1.FileName = (fileName + ".nc");
            saveFileDialog2.FileName = (fileName + ".bmp");
        }
    }
    
    private void OpenToolStripMenuItemClick(object sender, EventArgs e) {
        string prevFileName = openFileDialog1.FileName;
        if (openFileDialog1.ShowDialog(this) != DialogResult.OK) {
            return;
        }
        
        if (LoadImage(openFileDialog1.FileName)) {
            string fileName = Path.GetFileNameWithoutExtension(openFileDialog1.FileName);
            saveFileDialog1.FileName = (fileName + ".nc");
            saveFileDialog2.FileName = (fileName + ".bmp");
        } else {
            openFileDialog1.FileName = prevFileName;
        }
    }
    
    private void ReloadToolStripMenuItemClick(object sender, EventArgs e) {
        LoadImage(openFileDialog1.FileName);
    }
    
    private void ClipboardToolStripMenuItemClick(object sender, EventArgs e) {
        IDataObject clipboardDataObject = Clipboard.GetDataObject();
        if (clipboardDataObject == null) {
            return;
        }
        
        Bitmap image = (Bitmap)clipboardDataObject.GetData(DataFormats.Bitmap, true);
        if (image == null) {
            return;
        }
        
        LoadImage(null, image);
        
        openFileDialog1.FileName = null;
        saveFileDialog1.FileName = "*.nc";
        saveFileDialog2.FileName = "*.bmp";
    }
    
    private void FileToolStripMenuItemDropDownOpening(object sender, EventArgs e) {
        int lcid = culture.LCID;
        foreach (ToolStripMenuItem toolStripItem in languageToolStripMenuItem.DropDownItems) {
            toolStripItem.Checked = ((int)toolStripItem.Tag == lcid);
        }
    }
    
    private void LanguageToolStripMenuItemDropDownItemClicked(object sender, ToolStripItemClickedEventArgs e) {
        int newLCID = (int)e.ClickedItem.Tag;
        if (newLCID == culture.LCID) {
            return;
        }
        
        culture = new CultureInfo(newLCID, false);
        ApplyTranslation();
    }
    
    private void ExitToolStripMenuItemClick(object sender, EventArgs e) {
        this.Close();
    }
    
    private void ImageToolStripMenuItemDropDownOpening(object sender, EventArgs e) {
        IDataObject clipboardDataObject = Clipboard.GetDataObject();
        if (clipboardDataObject != null) {
            clipboardToolStripMenuItem.Enabled = clipboardDataObject.GetDataPresent(DataFormats.Bitmap, true);
        } else {
            clipboardToolStripMenuItem.Enabled = false;
        }
    }
    
    private void ImageToolStripMenuItemDropDownClosed(object sender, EventArgs e) {
        clipboardToolStripMenuItem.Enabled = true;
    }
    
    private void SaveToolStripMenuItemClick(object sender, EventArgs e) {
        if (bWorkerIsBusy) {
            return;
        }
        
        if (saveFileDialog2.ShowDialog(this) != DialogResult.OK) {
            return;
        }
        
        using (Bitmap image = new Bitmap(imWidth, imHeight, ((imWidth+3) / 4 * 4), PixelFormat.Format8bppIndexed, imDest)) {
            ColorPalette palette = image.Palette;
            for (int i = 0; i < 256; i++) {
                palette.Entries[i] = Color.FromArgb(-16777216 + i*65536 + i*256 + i);
            }
            image.Palette = palette;
            
            image.SetResolution(imDpiX, imDpiY);
            try {
                using (FileStream outFile = new FileStream(saveFileDialog2.FileName, FileMode.Create, FileAccess.Write, FileShare.None)) {
                    image.Save(outFile, bmpEncoder, null);
                }
            } catch (Exception ex) {
                MessageBox.Show(this, ex.Message, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }
    }
    
    private void PresetToolStripMenuItemDropDownOpening(object sender, EventArgs e) {
        loadPresetToolStripMenuItem.DropDownItems[2+activePreset].Visible = false;
        savePresetToolStripMenuItem.DropDownItems[2+activePreset].Visible = false;
        
        presetToolStripMenuItem.DropDownItems[13+activePreset].Image = presetCheckedIcon;
        presetToolStripMenuItem.DropDownItems[13+activePreset].Text = presetName;
        
        for (int i = 0; i < PresetCount; i++) {
            if (i == activePreset) {
                continue;
            }
            
            string text = preset[i].GetString("PresetName", ("Preset" + (1+i).ToString(invariantCulture)));
            
            loadPresetToolStripMenuItem.DropDownItems[2+i].Text = text;
            loadPresetToolStripMenuItem.DropDownItems[2+i].Visible = true;
            
            savePresetToolStripMenuItem.DropDownItems[2+i].Text = text;
            savePresetToolStripMenuItem.DropDownItems[2+i].Visible = true;
            
            presetToolStripMenuItem.DropDownItems[13+i].Image = presetIcons[i];
            presetToolStripMenuItem.DropDownItems[13+i].Text = text;
        }
        
        resetSpeedGraphToolStripMenuItem.Enabled = !im1bitPalette;
        resetPowerGraphToolStripMenuItem.Enabled = !im1bitPalette;
        
        machineTypeToolStripMenuItem.Text = String.Format(culture, resources.GetString("Menu_MachineType", culture), machineType);
        foreach (ToolStripMenuItem toolStripItem in machineTypeToolStripMenuItem.DropDownItems) {
            toolStripItem.Checked = ((MachineType)toolStripItem.Tag == machineType);
        }
        
        G0SpdToolStripMenuItem.Text = String.Format(culture, resources.GetString("Menu_G0Spd", culture), gcG0Speed);
        foreach (ToolStripMenuItem toolStripItem in G0SpdToolStripMenuItem.DropDownItems) {
            toolStripItem.Checked = ((float)toolStripItem.Tag == gcG0Speed);
        }
        
        rotarySpeedToolStripMenuItem.Enabled = gcWrappedOutput;
        rotarySpeedToolStripMenuItem.Text = String.Format(culture, resources.GetString("Menu_RotarySpeed", culture), gcRotarySpeed);
        foreach (ToolStripMenuItem toolStripItem in rotarySpeedToolStripMenuItem.DropDownItems) {
            toolStripItem.Checked = ((float)toolStripItem.Tag == gcRotarySpeed);
        }
        
        accelToolStripMenuItem.Text = String.Format(culture, resources.GetString("Menu_Accel", culture), gcAccel);
        foreach (ToolStripMenuItem toolStripItem in accelToolStripMenuItem.DropDownItems) {
            toolStripItem.Checked = ((float)toolStripItem.Tag == gcAccel);
        }
        
        nichromeOnOffCommandToolStripMenuItem.Text = String.Format(culture, resources.GetString("Menu_NichromeOnOffCommand", culture), gcNichromeOnOffCommand);
        foreach (ToolStripMenuItem toolStripItem in nichromeOnOffCommandToolStripMenuItem.DropDownItems) {
            toolStripItem.Checked = ((NichromeControl)toolStripItem.Tag == gcNichromeOnOffCommand);
        }
        
        burnFromBottomToTopToolStripMenuItem.Checked = gcBurnFromBottomToTop;
        doNotReturnYToolStripMenuItem.Checked = gcDontReturnY;
    }
    
    private void LoadPresetToolStripMenuItemDropDownItemClicked(object sender, ToolStripItemClickedEventArgs e) {
        int idx = (int)e.ClickedItem.Tag;
        if (idx < 0) {
            return;
        }
        LoadPreset(idx);
    }
    
    private void SavePresetToolStripMenuItemDropDownItemClicked(object sender, ToolStripItemClickedEventArgs e) {
        int idx = (int)e.ClickedItem.Tag;
        if (idx < 0) {
            return;
        }
        SavePreset(idx);
    }
    
    private void ResetSpeedGraphToolStripMenuItemDropDownItemClicked(object sender, ToolStripItemClickedEventArgs e) {
        float[] tag = (float[])e.ClickedItem.Tag;
        if (tag == null) {
            return;
        }
        
        if (tag != linearGraph) {
            textBox10.Text = tag[0].ToString();
            textBox11.Text = tag[1].ToString();
            textBox12.Text = tag[2].ToString();
        }
        gcSpeedGraph[2] = tag[3];
        gcSpeedGraph[3] = tag[4];
        gcSpeedGraph[4] = tag[5];
        gcSpeedGraph[5] = tag[6];
        panel1.Invalidate(false);
        
        bWorkerFlags = BWorkerFlags.CalcJobTime;
    }
    
    private void ResetPowerGraphToolStripMenuItemDropDownItemClicked(object sender, ToolStripItemClickedEventArgs e) {
        float[] tag = (float[])e.ClickedItem.Tag;
        if (tag == null) {
            return;
        }
        
        if (tag != linearGraph) {
            textBox13.Text = tag[1].ToString();
            textBox14.Text = tag[2].ToString();
        }
        gcPowerGraph[2] = tag[3];
        gcPowerGraph[3] = tag[4];
        gcPowerGraph[4] = tag[5];
        gcPowerGraph[5] = tag[6];
        panel2.Invalidate(false);
    }
    
    private void MachineTypeToolStripMenuItemDropDownItemClicked(object sender, ToolStripItemClickedEventArgs e) {
        machineType = (MachineType)e.ClickedItem.Tag;
        LoadPreset(-2);
    }
    
    private void G0SpdToolStripMenuItemDropDownItemClicked(object sender, ToolStripItemClickedEventArgs e) {
        gcG0Speed = (float)e.ClickedItem.Tag;
        bWorkerFlags = BWorkerFlags.CalcJobTime;
    }
    
    private void RotarySpeedToolStripMenuItemDropDownItemClicked(object sender, ToolStripItemClickedEventArgs e) {
        gcRotarySpeed = (float)e.ClickedItem.Tag;
    }
    
    private void AccelToolStripMenuItemDropDownItemClicked(object sender, ToolStripItemClickedEventArgs e) {
        gcAccel = (float)e.ClickedItem.Tag;
        bWorkerFlags = BWorkerFlags.CalcJobTime;
    }
    
    private void NichromeOnOffCommandToolStripMenuItemDropDownItemClicked(object sender, ToolStripItemClickedEventArgs e) {
        gcNichromeOnOffCommand = (NichromeControl)e.ClickedItem.Tag;
        
        label22.Enabled = (gcNichromeOnOffCommand < NichromeControl.OUT2_M8_M9);
        textBox6.Enabled = (gcNichromeOnOffCommand < NichromeControl.OUT2_M8_M9);
    }
    
    private void BurnFromBottomToTopToolStripMenuItemClick(object sender, EventArgs e) {
        gcBurnFromBottomToTop = !gcBurnFromBottomToTop;
        bWorkerFlags = (BWorkerFlags.CalcJobTime|BWorkerFlags.RedrawOrigin);
    }
    
    private void DoNotReturnYToolStripMenuItemClick(object sender, EventArgs e) {
        gcDontReturnY = !gcDontReturnY;
    }
    
    private void MachineToolStripMenuItemDropDownOpening(object sender, EventArgs e) {
        string[] portNames = SerialPort.GetPortNames();
        
        portToolStripMenuItem.Text = String.Format(culture, resources.GetString("Menu_SerialPort", culture), comPort);
        if (portNames.Length > 0) {
            portToolStripMenuItem.Enabled = true;
        } else {
            portToolStripMenuItem.Enabled = false;
            portNames = new string[] { "COM1", };
        }
        
        portToolStripMenuItem.DropDownItems.Clear();
        foreach (string portName in portNames) {
            ToolStripMenuItem toolStripItem = new ToolStripMenuItem();
            toolStripItem.DisplayStyle = ToolStripItemDisplayStyle.Text;
            toolStripItem.Text = portName;
            toolStripItem.Checked = (portName == comPort);
            
            portToolStripMenuItem.DropDownItems.Add(toolStripItem);
        }
        
        baudToolStripMenuItem.Text = String.Format(culture, resources.GetString("Menu_BaudRate", culture), baudRate);
        foreach (ToolStripMenuItem toolStripItem in baudToolStripMenuItem.DropDownItems) {
            toolStripItem.Checked = ((int)toolStripItem.Tag == baudRate);
        }
    }
    
    private void PortToolStripMenuItemDropDownItemClicked(object sender, ToolStripItemClickedEventArgs e) {
        comPort = e.ClickedItem.Text;
    }
    
    private void BaudToolStripMenuItemDropDownItemClicked(object sender, ToolStripItemClickedEventArgs e) {
        baudRate = (int)e.ClickedItem.Tag;
    }
    
    private void ExportToolStripMenuItemClick(object sender, EventArgs e) {
        if (bWorkerIsBusy) {
            return;
        }
        
        if (saveFileDialog1.ShowDialog(this) != DialogResult.OK) {
            return;
        }
        
        progressForm1.label1.Text = resources.GetString("PF_GeneratingGCode", culture);
        progressForm1.progressBar1.Value = 0;
        
        bWorker2SendToDevice = false;
        bWorker2ReadFromFile = false;
        
        progressForm1.ShowDialog(this);
    }
    
    private void SendToolStripMenuItemClick(object sender, EventArgs e) {
        if (bWorkerIsBusy) {
            return;
        }
        
        progressForm1.label1.Text = resources.GetString("PF_Initializing", culture);
        progressForm1.progressBar1.Value = 0;
        
        bWorker2SendToDevice = true;
        bWorker2ReadFromFile = false;
        
        progressForm1.ShowDialog(this);
    }
    
    private void UploadToolStripMenuItemClick(object sender, EventArgs e) {
        openFileDialog2.FileName = null;
        if (openFileDialog2.ShowDialog(this) != DialogResult.OK) {
            return;
        }
        
        progressForm1.label1.Text = resources.GetString("PF_Initializing", culture);
        progressForm1.progressBar1.Value = 0;
        
        bWorker2SendToDevice = true;
        bWorker2ReadFromFile = true;
        
        progressForm1.ShowDialog(this);
    }
    
    private void UrlToolStripMenuItemClick(object sender, EventArgs e) {
        Process process = new Process();
        process.StartInfo = new ProcessStartInfo((string)((ToolStripItem)sender).Tag);
        process.Start();
    }
    
    private void AboutToolStripMenuItemClick(object sender, EventArgs e) {
        string s = (AppTitle + " v" + AppVersion + " (" + AppVersionBuild + ")\n\n" + AppCopyright + "\n");
        //s += ("\n" + resources.GetString("About_MakeASmallDonation", culture) + "\n");
        
        string s2 = resources.GetString("TranslationAuthor", culture);
        if (s2 != null) {
            s += ("\n" + culture.EnglishName + " translation by " + s2 + "\n");
        }
        
        MessageBox.Show(this, s, (resources.GetString("About_Title", culture) + " " + AppTitle), MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    
    private void ApplyTranslation() {
        this.SuspendLayout2();
        this.fileToolStripMenuItem.Text = resources.GetString("Menu_File", culture);
        this.openToolStripMenuItem.Text = resources.GetString("Menu_Open", culture);
        this.reloadToolStripMenuItem.Text = resources.GetString("Menu_Reload", culture);
        this.exportToolStripMenuItem.Text = resources.GetString("Menu_SaveGCode", culture);
        this.closeToolStripMenuItem.Text = resources.GetString("Menu_Close", culture);
        this.languageToolStripMenuItem.Text = resources.GetString("Menu_Language", culture);
        this.exitToolStripMenuItem.Text = resources.GetString("Menu_Exit", culture);
        this.imageToolStripMenuItem.Text = resources.GetString("Menu_Image", culture);
        this.clipboardToolStripMenuItem.Text = resources.GetString("Menu_OpenClipboard", culture);
        this.saveToolStripMenuItem.Text = resources.GetString("Menu_SaveImage", culture);
        this.rotate270ToolStripMenuItem.Text = resources.GetString("Menu_Rotate270", culture);
        this.rotate90ToolStripMenuItem.Text = resources.GetString("Menu_Rotate90", culture);
        this.flipXYToolStripMenuItem.Text = resources.GetString("Menu_Rotate180", culture);
        this.flipXToolStripMenuItem.Text = resources.GetString("Menu_FlipX", culture);
        this.flipYToolStripMenuItem.Text = resources.GetString("Menu_FlipY", culture);
        this.cropToolStripMenuItem.Text = resources.GetString("Menu_CropBorder", culture);
        this.presetToolStripMenuItem.Text = resources.GetString("Menu_Preset", culture);
        this.loadPresetToolStripMenuItem.Text = resources.GetString("Menu_PresetLoad", culture);
        this.fileToolStripMenuItem1.Text = resources.GetString("Menu_PresetFile", culture);
        this.savePresetToolStripMenuItem.Text = resources.GetString("Menu_PresetSave", culture);
        this.fileToolStripMenuItem2.Text = resources.GetString("Menu_PresetFile", culture);
        this.resetSpeedGraphToolStripMenuItem.Text = resources.GetString("Menu_SpeedGraphReset", culture);
        this.defaultToolStripMenuItem1.Text = resources.GetString("Menu_GraphLinear", culture);
        this.x4axisToolStripMenuItem.Text = resources.GetString("Menu_GraphDefault", culture);
        this.resetPowerGraphToolStripMenuItem.Text = resources.GetString("Menu_PowerGraphReset", culture);
        this.defaultToolStripMenuItem2.Text = resources.GetString("Menu_GraphLinear", culture);
        this.burnFromBottomToTopToolStripMenuItem.Text = resources.GetString("Menu_BurnFromBottomToTop", culture);
        this.doNotReturnYToolStripMenuItem.Text = resources.GetString("Menu_DoNotReturnY", culture);
        this.machineToolStripMenuItem.Text = resources.GetString("Menu_Machine", culture);
        this.sendToolStripMenuItem.Text = resources.GetString("Menu_Send", culture);
        this.settingsToolStripMenuItem.Text = resources.GetString("Menu_Configuration", culture);
        this.uploadToolStripMenuItem.Text = resources.GetString("Menu_SendFile", culture);
        this.helpToolStripMenuItem.Text = resources.GetString("Menu_Help", culture);
        this.websiteToolStripMenuItem.Text = resources.GetString("Menu_Website", culture);
        this.CH341SERToolStripMenuItem.Text = resources.GetString("Menu_DownloadCH340Driver", culture);
        this.aboutToolStripMenuItem.Text = resources.GetString("Menu_About", culture);
        this.openToolStripButton.Text = resources.GetString("Btn_Open", culture);
        this.exportToolStripButton.Text = resources.GetString("Btn_SaveGCode", culture);
        this.sendToolStripButton.Text = resources.GetString("Btn_Send", culture);
        this.checkBox1.Text = resources.GetString("Im_1BitPalette", culture);
        this.label1.Text = resources.GetString("Im_Dithering", culture);
        this.label2.Text = resources.GetString("Im_BlackThreshold", culture);
        this.checkBox2.Text = resources.GetString("Im_InvertColors", culture);
        this.label4.Text = resources.GetString("Im_Brightness", culture);
        this.label6.Text = resources.GetString("Im_Contrast", culture);
        this.label8.Text = resources.GetString("Im_Gamma", culture);
        this.label14.Text = resources.GetString("Im_SharpenForce", culture);
        this.label10.Text = resources.GetString("Im_Resolution", culture);
        this.label11.Text = resources.GetString("Im_DpiX", culture);
        this.label12.Text = resources.GetString("Im_DpiY", culture);
        this.checkBox4.Text = resources.GetString("Im_SameAsX", culture);
        this.label15.Text = resources.GetString("Im_Left", culture);
        this.label16.Text = resources.GetString("Im_Top", culture);
        this.label17.Text = resources.GetString("Im_Width", culture);
        this.label18.Text = resources.GetString("Im_Height", culture);
        this.label19.Text = resources.GetString("Im_Interpolation", culture);
        this.label21.Text = resources.GetString("Gc_Speed", culture);
        this.label22.Text = resources.GetString("Gc_Power", culture);
        this.label24.Text = resources.GetString("Gc_NumberOfPasses", culture);
        this.label25.Text = resources.GetString("Gc_HeatDelay", culture);
        this.checkBox12.Text = resources.GetString("Gc_AirAssist", culture);
        this.checkBox6.Text = resources.GetString("Gc_ChangeSpeedOnWhite", culture);
        this.label26.Text = resources.GetString("Gc_WhiteSpeed", culture);
        this.label44.Text = resources.GetString("Gc_WhiteDistance", culture);
        this.label27.Text = resources.GetString("Gc_MaxMinSpeed", culture);
        this.button29.Text = resources.GetString("Gc_EditGraph", culture);
        this.label29.Text = resources.GetString("Gc_MinMaxPower", culture);
        this.button30.Text = resources.GetString("Gc_EditGraph", culture);
        this.checkBox7.Text = resources.GetString("Gc_Bidirectional", culture);
        this.label31.Text = resources.GetString("Gc_CleaningStrategy", culture);
        this.label32.Text = resources.GetString("Gc_CleaningRowsCount", culture);
        this.label35.Text = resources.GetString("Gc_StripWidth", culture);
        this.label36.Text = resources.GetString("Gc_StripSpeed", culture);
        this.label37.Text = resources.GetString("Gc_CleaningField", culture);
        this.label38.Text = resources.GetString("Gc_CleaningSpd", culture);
        this.label39.Text = resources.GetString("Gc_CleaningCycles", culture);
        this.checkBox3.Text = resources.GetString("Gc_RotaryOutput", culture);
        this.label13.Text = resources.GetString("Gc_MmPerRevolution", culture);
        this.label23.Text = resources.GetString("Gc_CylinderDiameter", culture);
        this.ResumeLayout2();
        
        progressForm1.SuspendLayout2();
        progressForm1.button1.Text = resources.GetString("PF_Abort", culture);
        progressForm1.ResumeLayout2();
        
        grblSettings1.SuspendLayout2();
        grblSettings1.Text = resources.GetString("GrblSet_Title", culture);
        grblSettings1.label1.Text = resources.GetString("GrblSet_XAxis", culture);
        grblSettings1.label2.Text = resources.GetString("GrblSet_YAxis", culture);
        grblSettings1.label4.Text = resources.GetString("GrblSet_StepsPerMM", culture);
        grblSettings1.label5.Text = resources.GetString("GrblSet_MaxVelocity", culture);
        grblSettings1.label6.Text = resources.GetString("GrblSet_MaxAccel", culture);
        grblSettings1.label8.Text = resources.GetString("GrblSet_FastJog", culture);
        grblSettings1.label9.Text = resources.GetString("GrblSet_SlowJog", culture);
        grblSettings1.label7.Text = resources.GetString("GrblSet_MaxTravel", culture);
        grblSettings1.checkBox1.Text = resources.GetString("GrblSet_InvertStepPin", culture);
        grblSettings1.checkBox2.Text = resources.GetString("GrblSet_InvertStepPin", culture);
        grblSettings1.checkBox4.Text = resources.GetString("GrblSet_InvertDirectionPin", culture);
        grblSettings1.checkBox5.Text = resources.GetString("GrblSet_InvertDirectionPin", culture);
        grblSettings1.checkBox3.Text = resources.GetString("GrblSet_InvertHomePin", culture);
        grblSettings1.checkBox6.Text = resources.GetString("GrblSet_InvertHomePin", culture);
        grblSettings1.checkBox14.Text = resources.GetString("GrblSet_InvertJogKeys", culture);
        grblSettings1.checkBox15.Text = resources.GetString("GrblSet_InvertJogKeys", culture);
        grblSettings1.checkBox7.Text = resources.GetString("GrblSet_HomingDirInvert", culture);
        grblSettings1.checkBox8.Text = resources.GetString("GrblSet_HomingDirInvert", culture);
        grblSettings1.label3.Text = resources.GetString("GrblSet_HomingCycle1", culture);
        grblSettings1.label10.Text = resources.GetString("GrblSet_HomingCycle2", culture);
        grblSettings1.label16.Text = resources.GetString("GrblSet_HomingSeekRate", culture);
        grblSettings1.label19.Text = resources.GetString("GrblSet_FastGrid", culture);
        grblSettings1.label20.Text = resources.GetString("GrblSet_SlowGrid", culture);
        grblSettings1.label11.Text = resources.GetString("GrblSet_PWMFrequency", culture);
        grblSettings1.label22.Text = resources.GetString("GrblSet_MarkerPower", culture);
        grblSettings1.label12.Text = resources.GetString("GrblSet_FrameSpeed", culture);
        grblSettings1.label13.Text = resources.GetString("GrblSet_FramePower", culture);
        grblSettings1.checkBox12.Text = resources.GetString("GrblSet_InvertStEnablePin", culture);
        grblSettings1.checkBox17.Text = resources.GetString("GrblSet_InvertLaserENPin", culture);
        grblSettings1.checkBox20.Text = resources.GetString("GrblSet_InvertEStopPin", culture);
        grblSettings1.checkBox18.Text = resources.GetString("GrblSet_InvertLaserPWMPin", culture);
        grblSettings1.checkBox19.Text = resources.GetString("GrblSet_PWMAlwaysOn", culture);
        grblSettings1.checkBox23.Text = resources.GetString("GrblSet_ServoControl", culture);
        grblSettings1.checkBox16.Text = resources.GetString("GrblSet_CoreXY", culture);
        grblSettings1.checkBox22.Text = resources.GetString("GrblSet_HBridgeControl", culture);
        grblSettings1.checkBox25.Text = resources.GetString("GrblSet_SwapJogKeys", culture);
        grblSettings1.checkBox24.Text = resources.GetString("GrblSet_PSUControl", culture);
        grblSettings1.checkBox21.Text = resources.GetString("GrblSet_DisableBuzzer", culture);
        grblSettings1.button1.Text = resources.GetString("Btn_OK", culture);
        grblSettings1.button2.Text = resources.GetString("Btn_Cancel", culture);
        grblSettings1.button3.Text = resources.GetString("Btn_Apply", culture);
        grblSettings1.ResumeLayout2();
    }
    
    private void Control_ValueChanged(object sender, EventArgs e) {
        int tag = (int)((Control)sender).Tag;
        
        bool isComboBox = ((tag & 0x0380) == 128);
        bool isCheckBox = ((tag & 0x0380) == 256);
        bool isTrackBar = ((tag & 0x0380) == 384);
        //bool isRadioButton = ((tag & 0x0380) == 512);
        //bool isButton = ((tag & 0x0380) == 640);
        //bool isTabControl = ((tag & 0x0380) == 768);
        //bool isTextBox = ((tag & 0x0380) == 896);
        
        bool valueAffectUI = ((tag & 0x8000) != 0);
        
        int idx = (tag & 0x7F);
        
        float value = 0F;
        int int_value = 0;
        
        object selectedItem = null;
        
        string string_value = null;
        bool bool_value = false;
        
        if (isCheckBox) {
            bool_value = ((CheckBox)sender).Checked;
        } else if (isComboBox) {
            selectedItem = ((ComboBox)sender).SelectedItem;
        } else if (isTrackBar) {
            int_value = ((TrackBar)sender).Value;
        } else {
            bool inptFlagString = ((tag & 0x080000) != 0);
            bool inptFlagInteger = ((tag & 0x0400) != 0);
            bool inptFlagUnsigned = ((tag & 0x0800) == 0);
            bool inptFlagNonZero = ((tag & 0x1000) != 0);
            
            int backColor = (tag & 0x070000);
            
            try {
                if (inptFlagString) {
                    string_value = ((Control)sender).Text;
                    if (inptFlagNonZero) {
                        if (string_value == "") {
                            throw new FormatException();
                        }
                    }
                } else if (inptFlagInteger) {
                    int_value = Int32.Parse(((Control)sender).Text);
                    if (inptFlagUnsigned) {
                        if (int_value < 0) {
                            throw new FormatException();
                        }
                    }
                    if (inptFlagNonZero) {
                        if (int_value == 0) {
                            throw new FormatException();
                        }
                    }
                } else {
                    value = Single.Parse(((Control)sender).Text);
                    if (inptFlagUnsigned) {
                        if (value < 0F) {
                            throw new FormatException();
                        }
                    }
                    if (inptFlagNonZero) {
                        if (value == 0F) {
                            throw new FormatException();
                        }
                    }
                }
                
                switch (idx) {
                    case 14:
                        int_value = (int)(value*imDpiX / MmPerInch + 0.5F);
                        if (int_value < MinImageSize || int_value > MaxImageSize) {
                            throw new FormatException();
                        }
                        break;
                    case 15:
                        int_value = (int)(value*imDpiY / MmPerInch + 0.5F);
                        if (int_value < MinImageSize || int_value > MaxImageSize) {
                            throw new FormatException();
                        }
                        break;
                    case 9: case 10:
                        if (value < MinImageDpi || value > MaxImageDpi) {
                            throw new FormatException();
                        }
                        break;
                    case 20: case 32: case 39:
                        if (int_value > 255) {
                            throw new FormatException();
                        }
                        break;
                    case 18: case 24: case 36: case 38: case 25: case 26:
                        if (value > MaxGcSpeed) {
                            throw new FormatException();
                        }
                        break;
                    case 19: case 27: case 28:
                        if (value > MaxGcPower) {
                            throw new FormatException();
                        }
                        break;
                    case 22:
                        if (value > MaxGcDwell) {
                            throw new FormatException();
                        }
                        break;
                    case 35: case 37:
                        if (value > MaxStripWidth) {
                            throw new FormatException();
                        }
                        break;
                    case 47:
                        foreach (char ch in string_value) {
                            foreach (char badChar in invalidFileNameChars) {
                                if (ch == badChar) {
                                    throw new FormatException();
                                }
                            }
                        }
                        break;
                }
            } catch {
                ((Control)sender).BackColor = Color.DarkSalmon;
                if (backColor != 0) {
                    ((Control)sender).ForeColor = SystemColors.WindowText;
                }
                return;
            }
            
            switch (backColor) {
                case 65536:
                    ((Control)sender).BackColor = SystemColors.Control;
                    ((Control)sender).ForeColor = SystemColors.WindowText;
                    break;
                case 131072:
                    ((Control)sender).BackColor = Color.White;
                    ((Control)sender).ForeColor = Color.Black;
                    break;
                case 196608:
                    ((Control)sender).BackColor = Color.GhostWhite;
                    ((Control)sender).ForeColor = Color.Black;
                    break;
                case 262144:
                    ((Control)sender).BackColor = Color.Black;
                    ((Control)sender).ForeColor = Color.GhostWhite;
                    break;
                case 327680:
                case 393216:
                case 458752:
                    ((Control)sender).BackColor = SystemColors.Window;
                    ((Control)sender).ForeColor = SystemColors.WindowText;
                    break;
                default:
                    ((Control)sender).BackColor = Color.Empty;
                    break;
            }
        }
        
        switch (idx) {
            case 1: im1bitPalette = bool_value; break;
            case 2: imDithering = (ImageDithering)selectedItem; break;
            case 4: imInvertColors = bool_value; break;
            case 12: imLeft = value; break;
            case 13: imTop = value; break;
            case 16: imInterpolation = (InterpolationMode)selectedItem; break;
            
            case 18: gcSpeed = value; break;
            case 19: gcPower = value; break;
            case 20: gcNumberOfPasses = int_value; break;
            case 22: gcHeatDelay = value; break;
            case 51: gcAirAssist = bool_value; break;
            case 23: gcSkipWhite = bool_value; break;
            case 24: gcWhiteSpeed = value; break;
            case 49: gcWhiteDistance = value; break;
            case 29: gcBidirectional = bool_value; break;
            case 31: gcCleaningStrategy = (CleaningStrategy)selectedItem; break;
            case 32: gcCleaningRowsCount = int_value; break;
            case 35: gcStripWidth = value; break;
            case 36: gcStripSpeed = value; break;
            case 37: gcCleaningFieldWidth = value; break;
            case 38: gcCleaningFieldSpeed = value; break;
            case 39: gcNumberOfCleaningCycles = int_value; break;
            case 40: gcWrappedOutput = bool_value; break;
            case 41: mmPerRevolution = value; break;
            case 42: cylinderDiameter = value; break;
            
            case 25: case 26:
            gcSpeedGraph[26-idx] = value;
            panel1.Invalidate(false);
            break;
            
            case 27: case 28:
            gcPowerGraph[28-idx] = value;
            panel2.Invalidate(false);
            break;
            
            case 47: presetName = string_value; break;
            
            case 3:
            imBlackThreshold = int_value;
            label3.Text = int_value.ToString("0", culture);
            break;
            
            case 6:
            imBrightness = int_value;
            label5.Text = int_value.ToString("0", culture);
            break;
            
            case 7:
            imContrast = ((100 + int_value) / 100F);
            label7.Text = int_value.ToString("0", culture);
            break;
            
            case 8:
            imGamma = (int_value / 100F);
            label9.Text = (int_value / 100F).ToString("0.00", culture);
            break;
            
            case 5:
            imSharpenForce = int_value;
            label41.Text = (int_value / 10F).ToString("0.0", culture);
            break;
            
            case 9:
            imDpiX = value;
            imAspectRatio = ((imWidth/value) / (imHeight/imDpiY));
            if (imLockDpiY) {
                comboBox3.Text = ((Control)sender).Text;
            }
            textBox3.Text = (imWidth/value * MmPerInch).ToString("0.0##");
            break;
            
            case 10:
            imDpiY = value;
            imAspectRatio = ((imWidth/imDpiX) / (imHeight/value));
            textBox4.Text = (imHeight/value * MmPerInch).ToString("0.0##");
            break;
            
            case 11:
            imLockDpiY = bool_value;
            if (bool_value) {
                comboBox3.Text = comboBox2.Text;
            }
            break;
            
            case 14:
            if (!(((Control)sender).Focused || textBox4.Focused)) {
                return;
            }
            if (!backgroundWorker1.IsBusy) {
                return;
            }
            imWidth = int_value;
            if (imLockAspectRatio) {
                if (!textBox4.Focused) {
                    textBox4.Text = (value/imAspectRatio).ToString("0.0##");
                }
            }
            break;
            
            case 15:
            if (!(((Control)sender).Focused || textBox3.Focused)) {
                return;
            }
            if (!backgroundWorker1.IsBusy) {
                return;
            }
            imHeight = int_value;
            if (imLockAspectRatio) {
                if (!textBox3.Focused) {
                    textBox3.Text = (value*imAspectRatio).ToString("0.0##");
                }
            }
            break;
            
            case 17:
            imLockAspectRatio = bool_value;
            imAspectRatio = ((imWidth/imDpiX) / (imHeight/imDpiY));
            break;
        }
        
        if (valueAffectUI) {
            tableLayoutPanel16.Enabled = im1bitPalette;
            tableLayoutPanel28.Enabled = (imDithering == ImageDithering.Threshold);
            comboBox3.Enabled = !imLockDpiY;
            
            bool isNichromeBurner = (machineType == MachineType.NichromeBurner);
            bool isImpactGraver = (machineType == MachineType.ImpactGraver);
            //bool isLaserEngraver = !(isNichromeBurner || isImpactGraver);
            
            label21.Enabled = (im1bitPalette || isImpactGraver);
            textBox5.Enabled = (im1bitPalette || isImpactGraver);
            if (isNichromeBurner) {
                label22.Enabled = (gcNichromeOnOffCommand < NichromeControl.OUT2_M8_M9);
                textBox6.Enabled = (gcNichromeOnOffCommand < NichromeControl.OUT2_M8_M9);
            } else {
                label22.Enabled = im1bitPalette;
                textBox6.Enabled = im1bitPalette;
            }
            tableLayoutPanel19.Enabled = (gcSkipWhite || isNichromeBurner);
            tableLayoutPanel32.Visible = !(im1bitPalette || gcSpeedGraph[1] == gcSpeedGraph[0]);
            tableLayoutPanel20.Enabled = !im1bitPalette;
            tableLayoutPanel33.Visible = !(im1bitPalette || gcPowerGraph[1] == gcPowerGraph[0]);
            tableLayoutPanel21.Enabled = !im1bitPalette;
            tableLayoutPanel25.Enabled = (gcCleaningStrategy != CleaningStrategy.None);
            label32.Enabled = (gcCleaningStrategy == CleaningStrategy.AfterNRows);
            comboBox6.Enabled = (gcCleaningStrategy == CleaningStrategy.AfterNRows);
            tableLayoutPanel27.Enabled = gcWrappedOutput;
        }
        
        bWorkerFlags = (BWorkerFlags)(tag & 0x306000);
    }
    
    private void ImOriginButtonClick(object sender, EventArgs e) {
        int tag = (int)((Control)sender).Tag;
        if (gcBurnFromBottomToTop) {
            tag ^= 4;
        }
        
        float left = 0F;
        if ((tag & 2) != 0) {
            left = (-imWidth/imDpiX * MmPerInch / 2F);
        } else if ((tag & 1) != 0) {
            left = (-imWidth/imDpiX * MmPerInch);
        }
        
        float top = 0F;
        if ((tag & 8) != 0) {
            top = (-imHeight/imDpiY * MmPerInch / 2F);
        } else if ((tag & 4) != 0) {
            top = (-imHeight/imDpiY * MmPerInch);
        }
        
        textBox1.Text = left.ToString("0.###");
        textBox2.Text = top.ToString("0.###");
    }
    
    private void ImOriginButtonPaint(object sender, PaintEventArgs e) {
        Size clientSize = ((Control)sender).ClientSize;
        e.Graphics.FillRectangle(SystemBrushes.ControlDarkDark, clientSize.Width/2-3, clientSize.Height/2-3, 6, 6);
    }
    
    private void EditGraphButton1Click(object sender, EventArgs e) {
        graphView1.Tag = gcSpeedGraph;
        graphView1.ShowDialog(this);
        panel1.Invalidate(false);
        bWorkerFlags = BWorkerFlags.CalcJobTime;
    }
    
    private void EditGraphButton2Click(object sender, EventArgs e) {
        graphView1.Tag = gcPowerGraph;
        graphView1.ShowDialog(this);
        panel2.Invalidate(false);
    }
    
    private void PresetButtonClick(object sender, EventArgs e) {
        if (sender is ToolStripItem) {
            activePreset = (int)((ToolStripItem)sender).Tag;
        } else {
            activePreset = (int)((Control)sender).Tag;
        }
        if (activePreset == prevPreset) {
            return;
        }
        
        if (prevPreset != -1) {
            ((Button)tableLayoutPanel31.Controls[prevPreset]).FlatAppearance.BorderSize = 0;
            ((Button)tableLayoutPanel31.Controls[prevPreset]).Font = regularFont;
            
            SavePreset(prevPreset);
        }
        
        prevPreset = activePreset;
        
        ((Button)tableLayoutPanel31.Controls[activePreset]).FlatAppearance.BorderSize = 1;
        ((Button)tableLayoutPanel31.Controls[activePreset]).Font = boldFont;
        
        LoadPreset(activePreset);
    }
}
