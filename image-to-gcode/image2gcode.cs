using System;
using System.Collections.Generic;
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

[assembly: AssemblyTitle(image2gcode.AppTitle + " v" + image2gcode.AppVersion)]
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
    public const string AppVersion = "3.1.1";
    public const string AppVersionBuild = "2021-10-03";
    public const string AppAuthor = "Artur Kurpukov";
    public const string AppCopyright = "Copyright (C) 2017-2021 Artur Kurpukov";
    private const string SettingsVersion = "3.1";
    
    private const float PI = 3.1415926535897931F;
    private const float MmPerInch = 25.4F;
    private const int NaN = -4194304;
    
    private const int BezierSegmentsCount = 1000;
    
    private const byte ImColorUltraWhite = 255;
    private const byte ImColorBlack = 0;
    private const byte ImColorStep = 5;
    private const byte ImColorWhite = (254/ImColorStep*ImColorStep);
    private const int ImColorCount = (ImColorWhite/ImColorStep+1);
    
    private const int PreviewCount = 5;
    private const int PresetCount = 14;
    
    private readonly object imResizeLock = new object();
    
    private ResourceManager resources = new ResourceManager(typeof(I2GResources));
    
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
    
    private Dictionary<string, Image> bitmapThumbnails = new Dictionary<string, Image>(0, null);
    private Dictionary<string, Stream> bitmapTextures = new Dictionary<string, Stream>(0, null);
    
    private int prevPreview = -2;
    private int activePreview;
    
    private int prevPreset = -1;
    private int activePreset;
    
    private int ibImageWidth = -1;
    private int ibImageHeight = -1;
    
    private IntPtr ibScan0;
    private Image ibImage;
    
    private string[] argv;
    
    private bool bWorker2SendToDevice;
    private bool bWorker2ReadFromFile;
    
    private EventWaitHandle bWorkerWaitHandle = new EventWaitHandle(true, EventResetMode.AutoReset, null);
    private bool bWorkerIsBusy = true;
    
    private const int BWorkerFlagDoWork = 0x4000;
    private const int BWorkerFlagImageChanged = 0x2000;
    private const int BWorkerFlagBackgroundChanged = 0x400000;
    private const int BWorkerFlagPreviewChanged = 0x200000;
    private const int BWorkerFlagRedrawOrigin = 0x100000;
    private const int BWorkerFlagExit = 0x1000;
    
    private int _bWorkerFlags = 0;
    private int bWorkerFlags {
        set
        {
            if (value == -1) {
                _bWorkerFlags = ~BWorkerFlagExit;
            } else {
                if (value == 0) {
                    return;
                }
                _bWorkerFlags |= value;
            }
            bWorkerWaitHandle.Set();
        }
    }
    
    [STAThread]
    internal static void Main(string[] argv) {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new image2gcode(argv));
    }
    
    public image2gcode(string[] argv) {
        InitializeComponent();
        
        openFileDialog1.Title = AppTitle;
        openFileDialog2.Title = AppTitle;
        saveFileDialog1.Title = AppTitle;
        saveFileDialog2.Title = AppTitle;
        openFileDialog3.Title = AppTitle;
        saveFileDialog3.Title = AppTitle;
        
        panel1.Tag = gcSpeedGraph;
        panel2.Tag = gcPowerGraph;
        
        this.Text = (AppTitle + " v" + AppVersion);
        this.argv = argv;
    }
    
    private void Image2gcodeLoad(object sender, EventArgs e) {
        for (int i = 0; i < ImColorCount; i++) {
            gvColors[i] = Color.FromArgb(-16777216 + i*ImColorStep*65536 + i*ImColorStep*256 + i*ImColorStep);
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
        
        comboBox4.SelectedItem = (InterpolationMode)settings.GetInt32("ImageInterpolation", (int)InterpolationMode.Bicubic);
        
        string[] sArray;
        try {
            sArray = Directory.GetFiles("BitmapTextures", "*.jpg", SearchOption.AllDirectories);
        } catch {
            sArray = new string[0];
        }
        
        foreach (string file in sArray) {
            try {
                string key = Path.GetFileNameWithoutExtension(file);
                FileStream inFile = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                
                try {
                    Image thumbnail;
                    using (Image image = Image.FromStream(inFile, false, true)) {
                        thumbnail = new Bitmap(32, 32, PixelFormat.Format24bppRgb);
                        using (Graphics g = Graphics.FromImage(thumbnail)) {
                            g.DrawImage(image, 0, 0, 32, 32);
                        }
                    }
                    
                    bitmapThumbnails[key] = thumbnail;
                    bitmapTextures[key] = inFile;
                } catch {
                    inFile.Close();
                }
            } catch {
            }
        }
        
        foreach (string key in bitmapTextures.Keys) {
            comboBox12.Items.Add(key);
        }
        
        for (int i = 0; i < PreviewCount; i++) {
            string idx = (1+i).ToString(invariantCulture);
            
            string background = settings.GetString(("PreviewBackground" + idx), "");
            if (bitmapTextures.ContainsKey(background)) {
                previewBackground[i] = background;
            }
            
            WrapMode wrapMode = (WrapMode)settings.GetInt32(("PreviewWrapMode" + idx), -1);
            if (wrapMode < WrapMode.Tile || wrapMode >= WrapMode.Clamp) {
                previewWrapMode[i] = WrapMode.TileFlipXY;
            } else {
                previewWrapMode[i] = wrapMode;
            }
            
            int scale = settings.GetInt32(("PreviewSize" + idx), 100);
            if (scale < 50 || scale > 200) {
                previewSize[i] = 100;
            } else {
                previewSize[i] = scale;
            }
            
            previewBgColor[i] = Color.FromArgb(settings.GetInt32(("PreviewBgColor" + idx), -1));
            previewDotColor[i] = Color.FromArgb(settings.GetInt32(("PreviewDotColor" + idx), -16777216));
        }
        
        activePreview = settings.GetInt32("ActivePreview", -2);
        if (activePreview < -1 || activePreview >= PreviewCount) {
            activePreview = -1;
        }
        
        PreviewButtonClick(tableLayoutPanel40.Controls[activePreview+1], EventArgs.Empty);
        
        presetCheckedIcon = (Image)resources.GetObject("PresetCheckedIcon", invariantCulture);
        
        using (Font font = new Font(FontFamily.GenericMonospace, 9F, FontStyle.Bold, GraphicsUnit.Pixel)) {
            RectangleF rectF = new RectangleF(0F, 1F, 16F, 14F);
            StringFormat format = new StringFormat((StringFormatFlags)0, 0);
            
            format.Alignment = StringAlignment.Center;
            format.LineAlignment = StringAlignment.Center;
            
            for (int i = 0; i < PresetCount; i++) {
                string idx = i.ToString("00", invariantCulture);
                
                preset[i] = Registry.CurrentUser.CreateSubKey("SOFTWARE\\" + AppTitle + "\\v" + SettingsVersion + "\\Preset" + idx);
                
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
            
            string[] s = line.Split(new string[] { ",", }, 7, StringSplitOptions.None);
            if (s.Length != 6) {
                continue;
            }
            
            float[] tag = new float[4];
            try {
                tag[0] = constrain_0_1(Single.Parse(s[2], invariantCulture));
                tag[1] = constrain_0_1(Single.Parse(s[3], invariantCulture));
                tag[2] = constrain_0_1(Single.Parse(s[4], invariantCulture));
                tag[3] = constrain_0_1(Single.Parse(s[5], invariantCulture));
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
        
        wrappedOutputDialog1 = new WrappedOutputDialog();
        wrappedOutputDialog1.Load += (sender2, e2) => {
            if (wrappingAxis == GcAxis.X) {
                ((WrappedOutputDialog)sender2).label5.Text = String.Format(culture, resources.GetString("WO_label5", culture), (imWidth/imDpiX*MmPerInch * 360F / (cylinderDiameter*PI)));
            } else {
                ((WrappedOutputDialog)sender2).label5.Text = String.Format(culture, resources.GetString("WO_label5", culture), (imHeight/imDpiY*MmPerInch * 360F / (cylinderDiameter*PI)));
            }
        };
        wrappedOutputDialog1.comboBox1.SelectedIndexChanged += Control_ValueChanged;
        wrappedOutputDialog1.textBox1.TextChanged += Control_ValueChanged;
        wrappedOutputDialog1.textBox2.TextChanged += Control_ValueChanged;
        wrappedOutputDialog1.textBox3.TextChanged += Control_ValueChanged;
        
        wrappedOutputDialog1.comboBox1.SelectedItem = (GcAxis)settings.GetInt32("WrappingAxis", (int)GcAxis.Y);
        wrappedOutputDialog1.textBox3.Text = settings.GetSingle("CylinderDiameter", 80F).ToString();
        
        activePreset = settings.GetInt32("ActivePreset", -1);
        if (activePreset < 0 || activePreset >= PresetCount) {
            activePreset = 0;
        }
        
        PresetButtonClick(tableLayoutPanel31.Controls[activePreset], EventArgs.Empty);
        
        checkBox10.Checked = (settings.GetInt32("GcodePrependFrame", 0) != 0);
        textBox15.Text = settings.GetSingle("GcodeFrameSpeed", 1000F).ToString();
        textBox21.Text = settings.GetSingle("GcodeFramePower", 0F).ToString();
        checkBox11.Checked = (settings.GetInt32("GcodeFrameWorkArea", 0) != 0);
        
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
        progressForm1.Load += (sender2, e2) => {
            ((Form)sender2).ClientSize = new Size(418, -1);
        };
        progressForm1.Shown += (sender2, e2) => {
            backgroundWorker2.RunWorkerAsync(null);
        };
        progressForm1.FormClosing += (sender2, e2) => {
            backgroundWorker2.CancelAsync();
            while (backgroundWorker2.IsBusy) {
                Application.DoEvents();
            }
        };
        
        progressForm1.trackBar1.Scroll += Control_ValueChanged;
        progressForm1.trackBar2.Scroll += Control_ValueChanged;
        progressForm1.button1.Click += (sender2, e2) => serialPort1.Write(new byte[] { (byte)'~', }, 0, 1);
        progressForm1.button2.Click += (sender2, e2) => serialPort1.Write(new byte[] { (byte)'!', }, 0, 1);
        
        //progressForm2 = new ProgressForm2(null);
        
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
        
        if (argv.Length != 1) {
            return;
        }
        
        if (LoadImage(argv[0])) {
            openFileDialog1.FileName = argv[0];
            string fileName = Path.GetFileNameWithoutExtension(argv[0]);
            saveFileDialog1.FileName = (fileName + ".nc");
            saveFileDialog2.FileName = (fileName + ".bmp");
        }
    }
    
    private unsafe void Image2gcodeClosing(object sender, FormClosingEventArgs e) {
        menuStrip1.Enabled = false;
        toolStrip1.Enabled = false;
        tableLayoutPanel1.Enabled = false;
        statusStrip1.Enabled = false;
        
        bWorkerFlags = BWorkerFlagExit;
        while (backgroundWorker1.IsBusy) {
            Application.DoEvents();
        }
        
        toolStripStatusLabel1.Text = resources.GetString("Status_AppIsClosing", culture);
        
        customGraphs.Close();
        foreach (Stream fileStream in bitmapTextures.Values) {
            fileStream.Close();
        }
        
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
        
        FormWindowState windowState = this.WindowState;
        settings.SetInt32("WindowMaximized", (windowState == FormWindowState.Maximized));
        if (windowState == FormWindowState.Normal) {
            settings.SetInt32("WindowWidth", this.Width);
            settings.SetInt32("WindowHeight", this.Height);
            settings.SetInt32("WindowLeft", this.Left);
            settings.SetInt32("WindowTop", this.Top);
        }
        
        settings.SetInt32("GraphViewWidth", graphView1.Width);
        settings.SetInt32("GraphViewHeight", graphView1.Height);
        settings.SetInt32("GraphViewLeft", graphView1.Left);
        settings.SetInt32("GraphViewTop", graphView1.Top);
        
        settings.SetInt32("UICulture", culture.LCID);
        
        settings.SetString("ComPort", comPort);
        settings.SetInt32("BaudRate", baudRate);
        
        settings.SetInt32("Image1bitPalette", im1bitPalette);
        settings.SetInt32("ImageDithering", imDithering);
        settings.SetSingle("ImageDpiX", imDpiX);
        settings.SetSingle("ImageDpiY", imDpiY);
        settings.SetInt32("ImageLockDpiY", imLockDpiY);
        settings.SetInt32("ImageInterpolation", imInterpolation);
        
        settings.SetInt32("ActivePreview", activePreview);
        for (int i = 0; i < PreviewCount; i++) {
            string idx = (1+i).ToString(invariantCulture);
            if (previewBackground[i] == null) {
                settings.SetString(("PreviewBackground" + idx), "");
            } else {
                settings.SetString(("PreviewBackground" + idx), previewBackground[i]);
            }
            settings.SetInt32(("PreviewWrapMode" + idx), previewWrapMode[i]);
            settings.SetInt32(("PreviewSize" + idx), previewSize[i]);
            settings.SetInt32(("PreviewBgColor" + idx), previewBgColor[i].ToArgb());
            settings.SetInt32(("PreviewDotColor" + idx), previewDotColor[i].ToArgb());
        }
        
        settings.SetInt32("ActivePreset", activePreset);
        SavePreset(activePreset);
        
        settings.SetInt32("GcodePrependFrame", gcPrependFrame);
        settings.SetSingle("GcodeFrameSpeed", gcFrameSpeed);
        settings.SetSingle("GcodeFramePower", gcFramePower);
        settings.SetInt32("GcodeFrameWorkArea", gcFrameWorkArea);
        
        settings.SetInt32("WrappingAxis", wrappingAxis);
        settings.SetSingle("CylinderDiameter", cylinderDiameter);
        
        settings.Flush();
    }
    
    private unsafe void ImageBox1Paint(object sender, PaintEventArgs e) {
        lock (imResizeLock) {
            int imIdx = ((ImageBox)sender).ImIdx;
            if (imIdx == -1) {
                return;
            }
            
            Size ibSize = ((Control)sender).ClientSize;
            int ibWidth = ibSize.Width;
            int ibHeight = ibSize.Height;
            
            Point scrollPosition = ((ScrollableControl)sender).AutoScrollPosition;
            
            float startPtX = ((ImageBox)sender).StartPointX;
            float startPtY = ((ImageBox)sender).StartPointY;
            int imWidth = ((ImageBox)sender).ImWidth;
            int imHeight = ((ImageBox)sender).ImHeight;
            
            int zoom = ((ImageBox)sender).Zoom;
            int scaledImWidth = (imWidth * zoom / 100);
            int scaledImHeight = (imHeight * zoom / 100);
            
            int hSpace = (ibWidth-scaledImWidth);
            if (hSpace < 0) {
                hSpace = 0;
            }
            
            int vSpace = (ibHeight-scaledImHeight);
            if (vSpace < 0) {
                vSpace = 0;
            }
            
            int destWidth = (ibWidth-hSpace);
            int destHeight = (ibHeight-vSpace);
            
            int srcLeft = (-scrollPosition.X * 100 / zoom);
            int srcTop = (-scrollPosition.Y * 100 / zoom);
            int srcWidth = (destWidth * 100 / zoom);
            int srcHeight = (destHeight * 100 / zoom);
            
            if (false) {
                int destScanWidth = ((destWidth*3+3) / 4 * 4);
                
                if (destWidth != ibImageWidth || destHeight != ibImageHeight) {
                    Marshal.FreeHGlobal(ibScan0);
                    ibScan0 = Marshal.AllocHGlobal((IntPtr)(destHeight*destScanWidth));
                    ibImage = new Bitmap(destWidth, destHeight, destScanWidth, PixelFormat.Format24bppRgb, ibScan0);
                    
                    ibImageWidth = destWidth;
                    ibImageHeight = destHeight;
                }
                
                float resizeFactorX = ((float)imWidth / scaledImWidth);
                float resizeFactorY = ((float)imHeight / scaledImHeight);
                
                if (imIdx == 1) {
                    int scanWidth = ((imWidth*3+3) / 4 * 4);
                    IntPtr imScan0 = (imPreview + srcTop*scanWidth + srcLeft*3);
                    
                    Parallel.For(0, destHeight, (y) => {
                        byte* dest = (byte*)(ibScan0 + y*destScanWidth);
                        
                        int floorY = (int)(y*resizeFactorY);
                        int ceilY = (floorY + 1);
                        if (ceilY >= srcHeight) {
                            ceilY = floorY;
                        }
                        float fractionY = (y*resizeFactorY - floorY);
                        float invFractionY = (1F - fractionY);
                        
                        byte* srcFloor = (byte*)(imScan0 + floorY*scanWidth);
                        byte* srcCeil = (byte*)(imScan0 + ceilY*scanWidth);
                        
                        for (int x = 0; x < destWidth; x++) {
                            int floorX = (int)(x*resizeFactorX);
                            int ceilX = (floorX + 1);
                            if (ceilX >= srcWidth) {
                                ceilX = floorX;
                            }
                            float fractionX = (x*resizeFactorX - floorX);
                            float invFractionX = (1F - fractionX);
                            
                            byte b1, b2;
                            
                            b1 = (byte)(invFractionX*srcFloor[floorX*3+2] + fractionX*srcFloor[ceilX*3+2]);
                            b2 = (byte)(invFractionX*srcCeil[floorX*3+2] + fractionX*srcCeil[ceilX*3+2]);
                            dest[x*3+2] = (byte)(invFractionY*b1 + fractionY*b2);
                            
                            b1 = (byte)(invFractionX*srcFloor[floorX*3+1] + fractionX*srcFloor[ceilX*3+1]);
                            b2 = (byte)(invFractionX*srcCeil[floorX*3+1] + fractionX*srcCeil[ceilX*3+1]);
                            dest[x*3+1] = (byte)(invFractionY*b1 + fractionY*b2);
                            
                            b1 = (byte)(invFractionX*srcFloor[floorX*3+0] + fractionX*srcFloor[ceilX*3+0]);
                            b2 = (byte)(invFractionX*srcCeil[floorX*3+0] + fractionX*srcCeil[ceilX*3+0]);
                            dest[x*3+0] = (byte)(invFractionY*b1 + fractionY*b2);
                        }
                    });
                } else {
                    int scanWidth = ((imWidth+3) / 4 * 4);
                    
                    IntPtr imScan0;
                    if (imIdx == 2) {
                        imScan0 = (imResized + srcTop*scanWidth + srcLeft);
                    } else {
                        imScan0 = (imDest + srcTop*scanWidth + srcLeft);
                    }
                    
                    Parallel.For(0, destHeight, (y) => {
                        byte* dest = (byte*)(ibScan0 + y*destScanWidth);
                        
                        int floorY = (int)(y*resizeFactorY);
                        int ceilY = (floorY + 1);
                        if (ceilY >= srcHeight) {
                            ceilY = floorY;
                        }
                        float fractionY = (y*resizeFactorY - floorY);
                        float invFractionY = (1F - fractionY);
                        
                        byte* srcFloor = (byte*)(imScan0 + floorY*scanWidth);
                        byte* srcCeil = (byte*)(imScan0 + ceilY*scanWidth);
                        
                        for (int x = 0; x < destWidth; x++) {
                            int floorX = (int)(x*resizeFactorX);
                            int ceilX = (floorX + 1);
                            if (ceilX >= srcWidth) {
                                ceilX = floorX;
                            }
                            float fractionX = (x*resizeFactorX - floorX);
                            float invFractionX = (1F - fractionX);
                            
                            byte b1 = (byte)(invFractionX*srcFloor[floorX] + fractionX*srcFloor[ceilX]);
                            byte b2 = (byte)(invFractionX*srcCeil[floorX] + fractionX*srcCeil[ceilX]);
                            byte gray = (byte)(invFractionY*b1 + fractionY*b2);
                            
                            dest[x*3+2] = gray;
                            dest[x*3+1] = gray;
                            dest[x*3+0] = gray;
                        }
                    });
                }
                
                e.Graphics.DrawImage(ibImage, hSpace/2, vSpace/2);
            } else {
                Image ibImage;
                if (imIdx == 1) {
                    int scanWidth = ((imWidth*3+3) / 4 * 4);
                    ibImage = new Bitmap(srcWidth, srcHeight, scanWidth, PixelFormat.Format24bppRgb, (imPreview + srcTop*scanWidth + srcLeft*3));
                } else {
                    int scanWidth = ((imWidth+3) / 4 * 4);
                    if (imIdx == 2) {
                        ibImage = new Bitmap(srcWidth, srcHeight, scanWidth, PixelFormat.Format8bppIndexed, (imResized + srcTop*scanWidth + srcLeft));
                    } else {
                        ibImage = new Bitmap(srcWidth, srcHeight, scanWidth, PixelFormat.Format8bppIndexed, (imDest + srcTop*scanWidth + srcLeft));
                    }
                    
                    ColorPalette palette = ibImage.Palette;
                    for (int i = 0; i <= ImColorWhite; i += ImColorStep) {
                        palette.Entries[i] = Color.FromArgb(-16777216 + i*65536 + i*256 + i);
                    }
                    palette.Entries[255] = Color.White;
                    
                    ibImage.Palette = palette;
                }
                
                e.Graphics.DrawImage(ibImage, hSpace/2, vSpace/2, destWidth, destHeight);
            }
            
            int a = (hSpace/2 + (int)((scaledImWidth-1)*(1F-startPtX)) + scrollPosition.X);
            int b = (vSpace/2 + (int)((scaledImHeight-1)*startPtY) + scrollPosition.Y);
            
            e.Graphics.DrawLine(Pens.Red, ibWidth-1, b, 0, b);
            e.Graphics.DrawLine(Pens.Red, a, 0, a, ibHeight-1);
        }
    }
    
    private void ImageBox1DragEnter(object sender, DragEventArgs e) {
        string[] fileNames = (string[])e.Data.GetData(DataFormats.FileDrop, false);
        if (fileNames.Length == 1) {
            e.Effect = DragDropEffects.All;
        } else {
            e.Effect = DragDropEffects.None;
        }
    }
    
    private void ImageBox1DragDrop(object sender, DragEventArgs e) {
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
        
        presetToolStripMenuItem.DropDownItems[12+activePreset].Image = presetCheckedIcon;
        presetToolStripMenuItem.DropDownItems[12+activePreset].Text = presetName;
        
        string defaultPresetName = resources.GetString("DefaultPresetName", culture);
        for (int i = 0; i < PresetCount; i++) {
            if (i == activePreset) {
                continue;
            }
            
            string text = preset[i].GetString("PresetName", defaultPresetName);
            
            loadPresetToolStripMenuItem.DropDownItems[2+i].Text = text;
            loadPresetToolStripMenuItem.DropDownItems[2+i].Visible = true;
            
            savePresetToolStripMenuItem.DropDownItems[2+i].Text = text;
            savePresetToolStripMenuItem.DropDownItems[2+i].Visible = true;
            
            presetToolStripMenuItem.DropDownItems[12+i].Image = presetIcons[i];
            presetToolStripMenuItem.DropDownItems[12+i].Text = text;
        }
        
        bool isNichromeBurner = (machineType == MachineType.NichromeBurner);
        bool isNotNichromeBurner = (machineType != MachineType.NichromeBurner);
        bool isNotImpactGraver = (machineType != MachineType.ImpactGraver);
        
        resetSpeedGraphToolStripMenuItem.Visible = isNotImpactGraver;
        resetSpeedGraphToolStripMenuItem.Enabled = !im1bitPalette;
        resetPowerGraphToolStripMenuItem.Visible = isNotNichromeBurner;
        resetPowerGraphToolStripMenuItem.Enabled = !im1bitPalette;
        
        machineTypeToolStripMenuItem.Text = String.Format(culture, resources.GetString("Menu_MachineType", culture), machineType);
        foreach (ToolStripMenuItem toolStripItem in machineTypeToolStripMenuItem.DropDownItems) {
            toolStripItem.Checked = ((MachineType)toolStripItem.Tag == machineType);
        }
        
        originToolStripMenuItem.Text = String.Format(culture, resources.GetString("Menu_MachineOrigin", culture), machineOrigin);
        foreach (ToolStripMenuItem toolStripItem in originToolStripMenuItem.DropDownItems) {
            toolStripItem.Checked = ((Origin)toolStripItem.Tag == machineOrigin);
        }
        
        G0SpdToolStripMenuItem.Text = String.Format(culture, resources.GetString("Menu_G0Spd", culture), gcG0Speed);
        foreach (ToolStripMenuItem toolStripItem in G0SpdToolStripMenuItem.DropDownItems) {
            toolStripItem.Checked = ((float)toolStripItem.Tag == gcG0Speed);
        }
        
        goToNextLineToolStripMenuItem.Text = String.Format(culture, resources.GetString("Menu_GoToNextLine", culture), gcGoToNextLine);
        foreach (ToolStripMenuItem toolStripItem in goToNextLineToolStripMenuItem.DropDownItems) {
            toolStripItem.Checked = ((GoToNextLineType)toolStripItem.Tag == gcGoToNextLine);
        }
        
        returnToOriginToolStripMenuItem.Visible = isNotNichromeBurner;
        returnToOriginToolStripMenuItem.Text = String.Format(culture, resources.GetString("Menu_ReturnToOrigin", culture), gcReturnToOrigin);
        foreach (ToolStripMenuItem toolStripItem in returnToOriginToolStripMenuItem.DropDownItems) {
            toolStripItem.Checked = ((ReturnToOriginType)toolStripItem.Tag == gcReturnToOrigin);
        }
        
        doNotReturnYToolStripMenuItem.Visible = isNichromeBurner;
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
        
        gcSpeedGraph[2] = tag[0];
        gcSpeedGraph[3] = tag[1];
        gcSpeedGraph[4] = tag[2];
        gcSpeedGraph[5] = tag[3];
        panel1.Invalidate(false);
        
        bWorkerFlags = BWorkerFlagDoWork;
    }
    
    private void ResetPowerGraphToolStripMenuItemDropDownItemClicked(object sender, ToolStripItemClickedEventArgs e) {
        float[] tag = (float[])e.ClickedItem.Tag;
        if (tag == null) {
            return;
        }
        
        gcPowerGraph[2] = tag[0];
        gcPowerGraph[3] = tag[1];
        gcPowerGraph[4] = tag[2];
        gcPowerGraph[5] = tag[3];
        panel2.Invalidate(false);
    }
    
    private void MachineTypeToolStripMenuItemDropDownItemClicked(object sender, ToolStripItemClickedEventArgs e) {
        machineType = (MachineType)e.ClickedItem.Tag;
        LoadPreset(-2);
    }
    
    private void OriginToolStripMenuItemDropDownItemClicked(object sender, ToolStripItemClickedEventArgs e) {
        machineOrigin = (Origin)e.ClickedItem.Tag;
    }
    
    private void G0SpdToolStripMenuItemDropDownItemClicked(object sender, ToolStripItemClickedEventArgs e) {
        gcG0Speed = (float)e.ClickedItem.Tag;
        bWorkerFlags = BWorkerFlagDoWork;
    }
    
    private void GoToNextLineToolStripMenuItemDropDownItemClicked(object sender, ToolStripItemClickedEventArgs e) {
        gcGoToNextLine = (GoToNextLineType)e.ClickedItem.Tag;
    }
    
    private void ReturnToOriginToolStripMenuItemDropDownItemClicked(object sender, ToolStripItemClickedEventArgs e) {
        gcReturnToOrigin = (ReturnToOriginType)e.ClickedItem.Tag;
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
        
        gcInverseTime = false;
        
        gcMultiplierX = 1F;
        gcMultiplierY = 1F;
        
        bool wrappedOutput = (bool)((ToolStripItem)sender).Tag;
        if (wrappedOutput) {
            if (wrappedOutputDialog1.ShowDialog(this) != DialogResult.OK) {
                return;
            }
            
            if (wrappingAxis == GcAxis.X) {
                gcInverseTime = true;
                gcMultiplierX = (mmPerRevolutionX / (cylinderDiameter*PI));
            } else {
                gcMultiplierY = (mmPerRevolutionY / (cylinderDiameter*PI));
            }
        }
        
        if (saveFileDialog1.ShowDialog(this) != DialogResult.OK) {
            return;
        }
        
        progressForm1.label1.Text = resources.GetString("PF_GeneratingGCode", culture);
        progressForm1.progressBar1.Value = 0;
        
        progressForm1.tableLayoutPanel2.Visible = false;
        progressForm1.button1.Visible = false;
        progressForm1.button2.Visible = false;
        
        bWorker2SendToDevice = false;
        bWorker2ReadFromFile = false;
        
        progressForm1.ShowDialog(this);
    }
    
    private void SendToolStripMenuItemClick(object sender, EventArgs e) {
        if (bWorkerIsBusy) {
            return;
        }
        
        gcInverseTime = false;
        
        gcMultiplierX = 1F;
        gcMultiplierY = 1F;
        
        bool wrappedOutput = (bool)((ToolStripItem)sender).Tag;
        if (wrappedOutput) {
            if (wrappedOutputDialog1.ShowDialog(this) != DialogResult.OK) {
                return;
            }
            
            if (wrappingAxis == GcAxis.X) {
                gcInverseTime = true;
                gcMultiplierX = (mmPerRevolutionX / (cylinderDiameter*PI));
            } else {
                gcMultiplierY = (mmPerRevolutionY / (cylinderDiameter*PI));
            }
        }
        
        progressForm1.label1.Text = resources.GetString("PF_Initializing", culture);
        progressForm1.progressBar1.Value = 0;
        
        progressForm1.tableLayoutPanel2.Visible = false;
        progressForm1.button1.Visible = true;
        progressForm1.button2.Visible = true;
        
        progressForm1.button1.Enabled = false;
        progressForm1.button2.Enabled = false;
        
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
        
        progressForm1.tableLayoutPanel2.Visible = false;
        progressForm1.button1.Visible = true;
        progressForm1.button2.Visible = true;
        
        progressForm1.button1.Enabled = false;
        progressForm1.button2.Enabled = false;
        
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
        s += ("\n" + resources.GetString("About_MakeASmallDonation", culture) + "\n");
        
        string s2 = resources.GetString("TranslationAuthor", culture);
        if (s2 != null) {
            s += ("\n" + culture.EnglishName + " translation by " + s2 + "\n");
        }
        
        MessageBox.Show(this, s, (resources.GetString("About_Title", culture) + " " + AppTitle), MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    
    private void ApplyTranslation() {
        this.SuspendLayout2();
        fileToolStripMenuItem.Text = resources.GetString("Menu_File", culture);
        openToolStripMenuItem.Text = resources.GetString("Menu_Open", culture);
        reloadToolStripMenuItem.Text = resources.GetString("Menu_Reload", culture);
        exportToolStripMenuItem.Text = resources.GetString("Menu_SaveGCode", culture);
        export2ToolStripMenuItem.Text = resources.GetString("Menu_SaveGCodeWO", culture);
        closeToolStripMenuItem.Text = resources.GetString("Menu_Close", culture);
        languageToolStripMenuItem.Text = resources.GetString("Menu_Language", culture);
        exitToolStripMenuItem.Text = resources.GetString("Menu_Exit", culture);
        imageToolStripMenuItem.Text = resources.GetString("Menu_Image", culture);
        clipboardToolStripMenuItem.Text = resources.GetString("Menu_OpenFromClipboard", culture);
        saveToolStripMenuItem.Text = resources.GetString("Menu_SaveImage", culture);
        cropToolStripMenuItem.Text = resources.GetString("Menu_CropBorder", culture);
        presetToolStripMenuItem.Text = resources.GetString("Menu_Preset", culture);
        loadPresetToolStripMenuItem.Text = resources.GetString("Menu_PresetLoad", culture);
        fileToolStripMenuItem1.Text = resources.GetString("Menu_PresetFile", culture);
        savePresetToolStripMenuItem.Text = resources.GetString("Menu_PresetSave", culture);
        fileToolStripMenuItem2.Text = resources.GetString("Menu_PresetFile", culture);
        resetSpeedGraphToolStripMenuItem.Text = resources.GetString("Menu_SpeedGraphReset", culture);
        defaultToolStripMenuItem1.Text = resources.GetString("Menu_GraphLinear", culture);
        x4axisToolStripMenuItem.Text = resources.GetString("Menu_GraphDefault", culture);
        resetPowerGraphToolStripMenuItem.Text = resources.GetString("Menu_PowerGraphReset", culture);
        defaultToolStripMenuItem2.Text = resources.GetString("Menu_GraphLinear", culture);
        doNotReturnYToolStripMenuItem.Text = resources.GetString("Menu_DoNotReturnY", culture);
        machineToolStripMenuItem.Text = resources.GetString("Menu_Machine", culture);
        sendToolStripMenuItem.Text = resources.GetString("Menu_Send", culture);
        send2ToolStripMenuItem.Text = resources.GetString("Menu_SendWO", culture);
        settingsToolStripMenuItem.Text = resources.GetString("Menu_Configuration", culture);
        uploadToolStripMenuItem.Text = resources.GetString("Menu_SendFile", culture);
        helpToolStripMenuItem.Text = resources.GetString("Menu_Help", culture);
        websiteToolStripMenuItem.Text = resources.GetString("Menu_Website", culture);
        CH341SERToolStripMenuItem.Text = resources.GetString("Menu_DownloadCH340Driver", culture);
        checkForUpdatesToolStripMenuItem.Text = resources.GetString("Menu_CheckForUpdates", culture);
        donateToolStripMenuItem.Text = resources.GetString("Menu_Donate", culture);
        aboutToolStripMenuItem.Text = resources.GetString("Menu_About", culture);
        openToolStripButton.Text = resources.GetString("Btn_Open", culture);
        exportToolStripButton.Text = resources.GetString("Btn_SaveGCode", culture);
        sendToolStripButton.Text = resources.GetString("Btn_Send", culture);
        checkBox1.Text = resources.GetString("Im_1BitPalette", culture);
        label1.Text = resources.GetString("Im_Dithering", culture);
        label2.Text = resources.GetString("Im_BlackThreshold", culture);
        checkBox2.Text = resources.GetString("Im_InvertColors", culture);
        label4.Text = resources.GetString("Im_Brightness", culture);
        label6.Text = resources.GetString("Im_Contrast", culture);
        label8.Text = resources.GetString("Im_Gamma", culture);
        label14.Text = resources.GetString("Im_SharpenForce", culture);
        label13.Text = resources.GetString("Im_RotateFlip", culture);
        label10.Text = resources.GetString("Im_Resolution", culture);
        label11.Text = resources.GetString("Im_XDpi", culture);
        label12.Text = resources.GetString("Im_YDpi", culture);
        checkBox4.Text = resources.GetString("Im_SameAsX", culture);
        label46.Text = resources.GetString("Im_ResultPreview", culture);
        label47.Text = resources.GetString("Im_Background", culture);
        label50.Text = resources.GetString("Im_TextureWrapMode", culture);
        label51.Text = resources.GetString("Im_TextureSize", culture);
        label48.Text = resources.GetString("Im_PreviewColor", culture);
        label15.Text = resources.GetString("Im_Left", culture);
        label16.Text = resources.GetString("Im_Top", culture);
        label17.Text = resources.GetString("Im_Width", culture);
        label18.Text = resources.GetString("Im_Height", culture);
        label19.Text = resources.GetString("Im_Interpolation", culture);
        label20.Text = resources.GetString("Im_AspectRatio", culture);
        checkBox5.Text = resources.GetString("Im_KeepAspectRatio", culture);
        label21.Text = resources.GetString("Gc_Speed", culture);
        label22.Text = resources.GetString("Gc_Power", culture);
        label23.Text = resources.GetString("Gc_Accel", culture);
        label24.Text = resources.GetString("Gc_Shift", culture);
        label25.Text = resources.GetString("Gc_HeatDelay", culture);
        checkBox12.Text = resources.GetString("Gc_AirAssist", culture);
        checkBox6.Text = resources.GetString("Gc_ChangeSpeedOnWhite", culture);
        label26.Text = resources.GetString("Gc_WhiteSpeed", culture);
        label44.Text = resources.GetString("Gc_WhiteDistance", culture);
        label27.Text = resources.GetString("Gc_MaxMinSpeed", culture);
        button29.Text = resources.GetString("Gc_EditGraph", culture);
        label29.Text = resources.GetString("Gc_MinMaxPower", culture);
        button30.Text = resources.GetString("Gc_EditGraph", culture);
        checkBox7.Text = resources.GetString("Gc_Bidirectional", culture);
        checkBox8.Text = resources.GetString("Gc_BurnToTheCleaningStrip", culture);
        label31.Text = resources.GetString("Gc_CleaningStrategy", culture);
        label32.Text = resources.GetString("Gc_CleaningRowsCount", culture);
        label33.Text = resources.GetString("Gc_CleaningDistance", culture);
        label34.Text = resources.GetString("Gc_StripSide", culture);
        label35.Text = resources.GetString("Gc_StripWidth", culture);
        label36.Text = resources.GetString("Gc_StripSpeed", culture);
        label37.Text = resources.GetString("Gc_CleaningField", culture);
        label38.Text = resources.GetString("Gc_CleaningSpd", culture);
        label39.Text = resources.GetString("Gc_CleaningCycles", culture);
        label40.Text = resources.GetString("Gc_NumberOfPasses", culture);
        checkBox10.Text = resources.GetString("Gc_PrependImageFrame", culture);
        label42.Text = resources.GetString("Gc_FrameSpeed", culture);
        label43.Text = resources.GetString("Gc_FramePower", culture);
        checkBox11.Text = resources.GetString("Gc_FrameWorkArea", culture);
        this.ResumeLayout2();
        
        progressForm1.SuspendLayout2();
        progressForm1.label2.Text = resources.GetString("PF_FeedOverride", culture);
        progressForm1.label4.Text = resources.GetString("PF_PowerOverride", culture);
        progressForm1.button1.Text = resources.GetString("PF_Run", culture);
        progressForm1.button2.Text = resources.GetString("PF_Pause", culture);
        progressForm1.button3.Text = resources.GetString("PF_Abort", culture);
        progressForm1.ResumeLayout2();
        
        wrappedOutputDialog1.SuspendLayout2();
        wrappedOutputDialog1.Text = resources.GetString("WO_Title", culture);
        wrappedOutputDialog1.label1.Text = resources.GetString("WO_AxisBeingWrapped", culture);
        wrappedOutputDialog1.label2.Text = resources.GetString("WO_MmPerRevolutionX", culture);
        wrappedOutputDialog1.label3.Text = resources.GetString("WO_MmPerRevolutionY", culture);
        wrappedOutputDialog1.label4.Text = resources.GetString("WO_CylinderDiameter", culture);
        wrappedOutputDialog1.button1.Text = resources.GetString("WO_Ok", culture);
        wrappedOutputDialog1.button2.Text = resources.GetString("WO_Cancel", culture);
        wrappedOutputDialog1.ResumeLayout2();
        
        grblSettings1.SuspendLayout2();
        grblSettings1.Text = resources.GetString("GrblSet_Title", culture);
        grblSettings1.label1.Text = resources.GetString("GrblSet_XAxis", culture);
        grblSettings1.label2.Text = resources.GetString("GrblSet_YAxis", culture);
        grblSettings1.label3.Text = resources.GetString("GrblSet_ZAxis", culture);
        grblSettings1.label4.Text = resources.GetString("GrblSet_StepsPerMm", culture);
        grblSettings1.label5.Text = resources.GetString("GrblSet_MaxVelocity", culture);
        grblSettings1.label6.Text = resources.GetString("GrblSet_MaxAccel", culture);
        grblSettings1.label7.Text = resources.GetString("GrblSet_MaxTravel", culture);
        grblSettings1.label8.Text = resources.GetString("GrblSet_FastJog", culture);
        grblSettings1.label9.Text = resources.GetString("GrblSet_SlowJog", culture);
        grblSettings1.checkBox1.Text = resources.GetString("GrblSet_InvertSTEP", culture);
        grblSettings1.checkBox2.Text = resources.GetString("GrblSet_InvertSTEP", culture);
        grblSettings1.checkBox3.Text = resources.GetString("GrblSet_InvertSTEP", culture);
        grblSettings1.checkBox4.Text = resources.GetString("GrblSet_InvertDIR", culture);
        grblSettings1.checkBox5.Text = resources.GetString("GrblSet_InvertDIR", culture);
        grblSettings1.checkBox6.Text = resources.GetString("GrblSet_InvertDIR", culture);
        grblSettings1.checkBox7.Text = resources.GetString("GrblSet_HomingDirInv", culture);
        grblSettings1.checkBox8.Text = resources.GetString("GrblSet_HomingDirInv", culture);
        grblSettings1.checkBox9.Text = resources.GetString("GrblSet_HomingDirInv", culture);
        grblSettings1.label10.Text = resources.GetString("GrblSet_StepPulseLen", culture);
        grblSettings1.label11.Text = resources.GetString("GrblSet_StepIdleDelay", culture);
        grblSettings1.label12.Text = resources.GetString("GrblSet_JunctionDeviation", culture);
        grblSettings1.label13.Text = resources.GetString("GrblSet_ArcTolerance", culture);
        grblSettings1.label14.Text = resources.GetString("GrblSet_StatusReport", culture);
        grblSettings1.checkBox10.Text = resources.GetString("GrblSet_PositionType", culture);
        grblSettings1.checkBox11.Text = resources.GetString("GrblSet_BufferData", culture);
        grblSettings1.checkBox12.Text = resources.GetString("GrblSet_InvertStEnablePin", culture);
        grblSettings1.checkBox13.Text = resources.GetString("GrblSet_InvertLimitPins", culture);
        grblSettings1.checkBox14.Text = resources.GetString("GrblSet_InvertProbePin", culture);
        grblSettings1.checkBox15.Text = resources.GetString("GrblSet_ReportInches", culture);
        grblSettings1.checkBox16.Text = resources.GetString("GrblSet_CoreXY", culture);
        grblSettings1.checkBox17.Text = resources.GetString("GrblSet_EnableHoming", culture);
        grblSettings1.checkBox18.Text = resources.GetString("GrblSet_SoftLimits", culture);
        grblSettings1.checkBox19.Text = resources.GetString("GrblSet_HardLimits", culture);
        grblSettings1.label15.Text = resources.GetString("GrblSet_HomingFeedRate", culture);
        grblSettings1.label16.Text = resources.GetString("GrblSet_HomingSeekRate", culture);
        grblSettings1.label17.Text = resources.GetString("GrblSet_HomingDebounceDelay", culture);
        grblSettings1.label18.Text = resources.GetString("GrblSet_HomingPulloff", culture);
        grblSettings1.label19.Text = resources.GetString("GrblSet_FastGrid", culture);
        grblSettings1.label20.Text = resources.GetString("GrblSet_SlowGrid", culture);
        grblSettings1.label21.Text = resources.GetString("GrblSet_MaxRPM", culture);
        grblSettings1.label22.Text = resources.GetString("GrblSet_MinRPM", culture);
        grblSettings1.checkBox20.Text = resources.GetString("GrblSet_LaserMode", culture);
        grblSettings1.button1.Text = resources.GetString("GrblSet_OK", culture);
        grblSettings1.button2.Text = resources.GetString("GrblSet_Cancel", culture);
        grblSettings1.button3.Text = resources.GetString("GrblSet_Apply", culture);
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
                    case 32: case 39: case 40:
                        if (int_value > 255) {
                            throw new FormatException();
                        }
                        break;
                    case 18: case 24: case 36: case 38: case 25: case 26: case 44:
                        if (value > MaxGcSpeed) {
                            throw new FormatException();
                        }
                        break;
                    case 19: case 27: case 28: case 45:
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
            
            case 52:
            if (((ComboBox)sender).SelectedIndex == 0) {
                previewBackground[activePreview] = null;
                tableLayoutPanel38.Enabled = false;
                tableLayoutPanel39.Enabled = false;
                button31.Enabled = true;
            } else {
                previewBackground[activePreview] = (string)selectedItem;
                tableLayoutPanel38.Enabled = true;
                tableLayoutPanel39.Enabled = true;
                button31.Enabled = false;
            }
            tableLayoutPanel40.Controls[activePreview+1].Invalidate(false);
            break;
            
            case 53: previewWrapMode[activePreview] = (WrapMode)selectedItem; break;
            case 54: previewSize[activePreview] = int_value; break;
            
            case 18: gcSpeed = value; break;
            case 19: gcPower = value; break;
            case 20: gcAccel = value; break;
            case 21: gcShift = value; break;
            case 22: gcHeatDelay = value; break;
            case 51: gcAirAssist = bool_value; break;
            case 23: gcSkipWhite = bool_value; break;
            case 24: gcWhiteSpeed = value; break;
            case 49: gcWhiteDistance = value; break;
            case 29: gcBidirectional = bool_value; break;
            case 30: gcBurnToTheStrip = bool_value; break;
            case 31: gcCleaningStrategy = (CleaningStrategy)selectedItem; break;
            case 32: gcCleaningRowsCount = int_value; break;
            case 33: gcCleaningDistance = value; break;
            case 34: gcStripPosition = (StripPositionType)selectedItem; break;
            case 35: gcStripWidth = value; break;
            case 36: gcStripSpeed = value; break;
            case 37: gcCleaningFieldWidth = value; break;
            case 38: gcCleaningFieldSpeed = value; break;
            case 39: gcNumberOfCleaningCycles = int_value; break;
            case 40: gcNumberOfPasses = int_value; break;
            
            case 25: case 26:
            gcSpeedGraph[26-idx] = value;
            panel1.Invalidate(false);
            break;
            
            case 27: case 28:
            gcPowerGraph[28-idx] = value;
            panel2.Invalidate(false);
            break;
            
            case 43: gcPrependFrame = bool_value; break;
            case 44: gcFrameSpeed = value; break;
            case 45: gcFramePower = value; break;
            case 46: gcFrameWorkArea = bool_value; break;
            
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
            
            case 41:
            new_f_override = int_value;
            progressForm1.label3.Text = int_value.ToString("0", culture);
            break;
            
            case 42:
            new_s_override = int_value;
            progressForm1.label5.Text = int_value.ToString("0", culture);
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
            
            case 56: mmPerRevolutionX = value; break;
            case 57: mmPerRevolutionY = value; break;
            
            case 55:
            wrappingAxis = (GcAxis)selectedItem;
            if ((GcAxis)selectedItem == GcAxis.X) {
                wrappedOutputDialog1.label2.Enabled = true;
                wrappedOutputDialog1.textBox1.Enabled = true;
                wrappedOutputDialog1.label3.Enabled = false;
                wrappedOutputDialog1.textBox2.Enabled = false;
                wrappedOutputDialog1.label5.Text = String.Format(culture, resources.GetString("WO_label5", culture), (imWidth/imDpiX*MmPerInch * 360F / (cylinderDiameter*PI)));
            } else {
                wrappedOutputDialog1.label2.Enabled = false;
                wrappedOutputDialog1.textBox1.Enabled = false;
                wrappedOutputDialog1.label3.Enabled = true;
                wrappedOutputDialog1.textBox2.Enabled = true;
                wrappedOutputDialog1.label5.Text = String.Format(culture, resources.GetString("WO_label5", culture), (imHeight/imDpiY*MmPerInch * 360F / (cylinderDiameter*PI)));
            }
            break;
            
            case 58:
            cylinderDiameter = value;
            if (wrappingAxis == GcAxis.X) {
                wrappedOutputDialog1.label5.Text = String.Format(culture, resources.GetString("WO_label5", culture), (imWidth/imDpiX*MmPerInch * 360F / (value*PI)));
            } else {
                wrappedOutputDialog1.label5.Text = String.Format(culture, resources.GetString("WO_label5", culture), (imHeight/imDpiY*MmPerInch * 360F / (value*PI)));
            }
            break;
        }
        
        if (valueAffectUI) {
            tableLayoutPanel16.Enabled = im1bitPalette;
            tableLayoutPanel28.Enabled = (imDithering == ImageDithering.Threshold);
            comboBox3.Enabled = !imLockDpiY;
            
            bool isNichromeBurner = (machineType == MachineType.NichromeBurner);
            bool isImpactGraver = (machineType == MachineType.ImpactGraver);
            
            label21.Enabled = (im1bitPalette || isImpactGraver);
            textBox5.Enabled = (im1bitPalette || isImpactGraver);
            label22.Enabled = (im1bitPalette || isNichromeBurner);
            textBox6.Enabled = (im1bitPalette || isNichromeBurner);
            label24.Enabled = gcBidirectional;
            textBox8.Enabled = gcBidirectional;
            tableLayoutPanel19.Enabled = (gcSkipWhite || isNichromeBurner);
            tableLayoutPanel32.Visible = (!(im1bitPalette || gcSpeedGraph[1] == gcSpeedGraph[0]));
            tableLayoutPanel20.Enabled = !im1bitPalette;
            tableLayoutPanel33.Visible = (!(im1bitPalette || gcPowerGraph[1] == gcPowerGraph[0]));
            tableLayoutPanel21.Enabled = !im1bitPalette;
            checkBox8.Enabled = !gcBidirectional;
            tableLayoutPanel25.Enabled = (gcCleaningStrategy != CleaningStrategy.None);
            label32.Enabled = (gcCleaningStrategy == CleaningStrategy.AfterNRows);
            comboBox6.Enabled = (gcCleaningStrategy == CleaningStrategy.AfterNRows);
            label33.Enabled = (gcCleaningStrategy == CleaningStrategy.Distance);
            textBox16.Enabled = (gcCleaningStrategy == CleaningStrategy.Distance);
            tableLayoutPanel29.Enabled = gcPrependFrame;
        }
        
        bWorkerFlags = (tag & 0x706000);
    }
    
    private void PreviewButtonClick(object sender, EventArgs e) {
        activePreview = (int)((Control)sender).Tag;
        if (activePreview == prevPreview) {
            return;
        }
        
        if (prevPreview != -2) {
            tableLayoutPanel40.Controls[prevPreview+1].Invalidate(false);
        }
        
        prevPreview = activePreview;
        
        ((Control)sender).Invalidate(false);
        
        if (activePreview == -1) {
            tableLayoutPanel34.Enabled = false;
        } else {
            tableLayoutPanel34.Enabled = true;
            if (previewBackground[activePreview] == null) {
                comboBox12.SelectedIndex = 0;
            } else {
                comboBox12.SelectedItem = previewBackground[activePreview];
            }
            comboBox13.SelectedItem = previewWrapMode[activePreview];
            trackBar5.Value = previewSize[activePreview];
            button31.BackColor = previewBgColor[activePreview];
            button32.BackColor = previewDotColor[activePreview];
        }
        
        bWorkerFlags = (BWorkerFlagBackgroundChanged|BWorkerFlagPreviewChanged);
    }
    
    private void PreviewButtonPaint(object sender, PaintEventArgs e) {
        int tag = (int)((Control)sender).Tag;
        
        Size clientSize = ((Control)sender).ClientSize;
        int width = clientSize.Width;
        int height = clientSize.Height;
        
        if (tag != -1) {
            if (previewBackground[tag] == null) {
                using (Brush brush = new SolidBrush(previewBgColor[tag])) {
                    e.Graphics.FillRectangle(brush, 0, 0, width, height);
                }
            } else {
                e.Graphics.DrawImage(bitmapThumbnails[previewBackground[tag]], 0, 0, width, height);
            }
            using (Brush brush = new SolidBrush(previewDotColor[tag])) {
                e.Graphics.FillRectangle(brush, width/2-3, height/2-3, 6, 6);
            }
            if (tag == activePreview) {
                using (Pen pen = new Pen(previewDotColor[tag], 1F)) {
                    e.Graphics.DrawRectangle(pen, 0, 0, width-1, height-1);
                }
            }
        } else {
            e.Graphics.FillRectangle(Brushes.White, 0, 0, width, height);
            e.Graphics.DrawLine(Pens.Red, width-1, 0, 0, height-1);
            e.Graphics.DrawLine(Pens.Red, width-1, height-1, 0, 0);
            if (tag == activePreview) {
                e.Graphics.DrawRectangle(Pens.Black, 0, 0, width-1, height-1);
            }
        }
    }
    
    private void PreviewColorButtonBackColorChanged(object sender, EventArgs e) {
        int color = ((Control)sender).BackColor.ToArgb();
        byte r = (byte)(color >> 16);
        byte g = (byte)(color >> 8);
        byte b = (byte)(color >> 0);
        
        byte gray = (byte)(0.299F*r + 0.587F*g + 0.114F*b);
        if (gray > 127) {
            ((Control)sender).ForeColor = Color.Black;
        } else {
            ((Control)sender).ForeColor = Color.White;
        }
    }
    
    private void PreviewColorButton1Click(object sender, EventArgs e) {
        colorDialog1.Color = previewBgColor[activePreview];
        if (colorDialog1.ShowDialog(this) != DialogResult.OK) {
            return;
        }
        
        ((Control)sender).BackColor = previewBgColor[activePreview] = colorDialog1.Color;
        tableLayoutPanel40.Controls[activePreview+1].Invalidate(false);
        
        bWorkerFlags = (BWorkerFlagBackgroundChanged|BWorkerFlagPreviewChanged);
    }
    
    private void PreviewColorButton2Click(object sender, EventArgs e) {
        colorDialog1.Color = previewDotColor[activePreview];
        if (colorDialog1.ShowDialog(this) != DialogResult.OK) {
            return;
        }
        
        ((Control)sender).BackColor = previewDotColor[activePreview] = colorDialog1.Color;
        tableLayoutPanel40.Controls[activePreview+1].Invalidate(false);
        
        bWorkerFlags = BWorkerFlagPreviewChanged;
    }
    
    private void ImOriginButtonClick(object sender, EventArgs e) {
        int tag = (int)((Control)sender).Tag;
        
        float left = 0F;
        float top = 0F;
        float width = (-imWidth/imDpiX * MmPerInch);
        float height = (-imHeight/imDpiY * MmPerInch);
        switch (tag) {
            case 1:
                left = (width/2F);
                break;
            case 2:
                left = width;
                break;
            case 3:
                top = (height/2F);
                break;
            case 4:
                left = (width/2F);
                top = (height/2F);
                break;
            case 5:
                left = width;
                top = (height/2F);
                break;
            case 6:
                top = height;
                break;
            case 7:
                left = (width/2F);
                top = height;
                break;
            case 8:
                left = width;
                top = height;
                break;
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
        bWorkerFlags = BWorkerFlagDoWork;
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
            ((Button)tableLayoutPanel31.Controls[prevPreset]).Font = new Font(Control.DefaultFont, FontStyle.Regular);
            
            SavePreset(prevPreset);
        }
        
        prevPreset = activePreset;
        
        ((Button)tableLayoutPanel31.Controls[activePreset]).FlatAppearance.BorderSize = 1;
        ((Button)tableLayoutPanel31.Controls[activePreset]).Font = new Font(Control.DefaultFont, FontStyle.Bold);
        
        LoadPreset(activePreset);
    }
}
