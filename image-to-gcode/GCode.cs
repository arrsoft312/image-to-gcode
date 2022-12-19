using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows.Forms;

partial class image2gcode {
    private const float MaxGcSpeed = 30000F;
    private const float MaxGcPower = 1000F;
    private const float MaxGcDwell = 60F;
    
    private const float MaxStripWidth = 30F;
    
    private const float AccelDistMultiplier = 2F;
    private const float DecelDistMultiplier = 1F;
    
    private ProgressForm progressForm1;
    
    private enum MotionMode {
        Invalid = -1,
        Seek,
        Linear,
        None = 80,
    }
    
    private enum DistanceMode {
        Invalid = -1,
        Absolute = 90,
        Incremental,
    }
    
    private enum FeedRateMode {
        Invalid = -1,
        InverseTime = 93,
        UnitsPerMin,
    }
    
    private enum NichromeControl {
        Default_M3_M5 = 0,
        OUT2_M8_M9 = 8,
    }
    
    private enum CleaningStrategy {
        None,
        Always,
        AfterNRows,
    }
    
    private float gcSpeed = 1200F;
    private float gcPower = 255F;
    private float gcAccel = 3000F;
    
    private int gcNumberOfPasses = 1;
    private float gcHeatDelay = 7F;
    private bool gcAirAssist = false;
    
    private bool gcSkipWhite = true;
    private float gcWhiteSpeed = 4000F;
    private float gcWhiteDistance = 5F;
    
    private float[] gcSpeedGraph = new float[] { 350F, 2820F, 0.35F, 0.35F, 0.65F, 0.65F, };
    private float[] gcPowerGraph = new float[] { 255F, 255F, 0.35F, 0.35F, 0.65F, 0.65F, };
    
    private bool gcBurnFromBottomToTop = false;
    private bool gcBidirectional = false;
    
    private CleaningStrategy gcCleaningStrategy = CleaningStrategy.None;
    private int gcCleaningRowsCount = 2;
    private float gcStripWidth = 20F;
    private float gcStripSpeed = 1000F;
    private float gcCleaningFieldWidth = 5F;
    private float gcCleaningFieldSpeed = 5000F;
    private int gcNumberOfCleaningCycles = 2;
    
    private NichromeControl gcNichromeOnOffCommand = NichromeControl.Default_M3_M5;
    
    private float gcRotarySpeed = 1500F;
    private bool gcDontReturnY = true;
    
    private bool gcWrappedOutput = false;
    private float mmPerRevolution = 360F;
    private float cylinderDiameter = 50F;
    
    private void BackgroundWorker2ProgressChanged(object sender, ProgressChangedEventArgs e) {
        if (e.UserState != null) {
            progressForm1.label1.Text = (string)e.UserState;
        }
        progressForm1.progressBar1.Value = e.ProgressPercentage;
    }
    
    private unsafe void BackgroundWorker2DoWork(object sender, DoWorkEventArgs e) {
        bool sendToDevice = bWorker2SendToDevice;
        bool readFromFile = bWorker2ReadFromFile;
        
        int width = imWidth;
        int height = imHeight;
        
        int scanWidth = ((width+3) / 4 * 4);
        
        int left2 = width;
        int top2 = -1;
        int width2 = 0;
        int height2 = 0;
        
        if (!sendToDevice || !readFromFile) {
            for (int y = 0; y < height; y++) {
                byte* dest = (byte*)(imDest + y*scanWidth);
                for (int x = 0; x < width; x++) {
                    if (dest[x] == ImColorUltraWhite) {
                        continue;
                    }
                    
                    if (top2 == -1) {
                        top2 = y;
                    }
                    if (left2 > x) {
                        left2 = x;
                    }
                    
                    for (x = width; x > 0; x--) {
                        if (dest[x-1] == ImColorUltraWhite) {
                            continue;
                        }
                        if (width2 < x) {
                            width2 = x;
                        }
                        break;
                    }
                    height2 = y;
                    
                    break;
                }
            }
            
            if (top2 == -1) {
                throw new WarningException(resources.GetString("Error_BlankImage", culture));
            }
            
            width2 -= left2;
            height2 -= (top2-1);
        }
        
        StreamWriter outFile;
        if (sendToDevice) {
            outFile = StreamWriter.Null;
        } else {
            outFile = new StreamWriter(saveFileDialog1.FileName, false, Encoding.ASCII);
        }
        
        try {
            int rxBufferSize;
            
            int sum_c_line = 0;
            List<int> c_line = new List<int>();
            
            int line_counter = 1;
            
            string[] s_list = new string[MaxImageSize*2+6];
            int i = 1;
            
            Func<bool> SendToGrblController;
            
            if (sendToDevice) {
                serialPort1.BaudRate = baudRate;
                serialPort1.PortName = comPort;
                
                serialPort1.ReadTimeout = 150;
                serialPort1.Open();
                
                if (!Grbl_GetSync(() => ((BackgroundWorker)sender).CancellationPending)) {
                    return;
                }
                
                serialPort1.Write("$I\n");
                try {
                    string[] resp = serialPort1.ReadTo("ok\r\n").Split(new string[] { "]\r\n", }, 4, StringSplitOptions.None);
                    if (resp.Length != 3 || resp[2] != "") {
                        throw new Exception(resources.GetString("Grbl_InvalidResponse", culture));
                    }
                    
                    string[] ver = resp[0].Split(new string[] { ":", ".", }, 5, StringSplitOptions.None);
                    if (ver.Length != 5 || ver[0] != "[VER") {
                        throw new Exception(resources.GetString("Grbl_InvalidResponse", culture));
                    }
                    
                    string[] opt = resp[1].Split(new string[] { ":", ",", }, 5, StringSplitOptions.None);
                    if (opt.Length != 4 || opt[0] != "[OPT") {
                        throw new Exception(resources.GetString("Grbl_InvalidResponse", culture));
                    }
                    
                    rxBufferSize = Int32.Parse(opt[3], invariantCulture);
                } catch (TimeoutException) {
                    throw new Exception(resources.GetString("Grbl_NotResponding", culture));
                }
                if (((BackgroundWorker)sender).CancellationPending) {
                    return;
                }
                
                serialPort1.Write("%\n");
                try {
                    string[] resp = serialPort1.ReadTo("\r\n").Split(new string[] { ":", }, 2, StringSplitOptions.None);
                    if (resp[0] != "ok") {
                        if (resp[0] == "error") {
                            if (resp[1] == "13") {
                                throw new Exception(resources.GetString("Grbl_GCodeLock", culture));
                            }
                            throw new WarningException("Sending a G-code to GRBL firmware is not supported at this time. Maybe someday I'll add this feature.");
                        }
                        throw new Exception(resources.GetString("Grbl_InvalidResponse", culture));
                    }
                } catch (TimeoutException) {
                    throw new Exception(resources.GetString("Grbl_NotResponding", culture));
                }
                if (((BackgroundWorker)sender).CancellationPending) {
                    return;
                }
                
                serialPort1.ReadTimeout = 30;
                
                SendToGrblController = () => {
                    for (int j = i;; i--) {
                        if (j != 0) {
                            if (i == 0) {
                                return true;
                            }
                            
                            int len = (s_list[j-i].Length + 1);
                            
                            sum_c_line += len;
                            c_line.Add(len);
                        } else {
                            if (sum_c_line == 0) {
                                return true;
                            }
                            
                            sum_c_line += rxBufferSize;
                            c_line[c_line.Count-1] += rxBufferSize;
                        }
                        
                        while (sum_c_line > rxBufferSize || serialPort1.BytesToRead != 0) {
                            try {
                                string[] resp = serialPort1.ReadTo("\r\n").Split(new string[] { ":", }, 2, StringSplitOptions.None);
                                if (resp[0] != "ok") {
                                    if (resp[0] == "error") {
                                        if (readFromFile) {
                                            throw new Exception(String.Format(culture, resources.GetString("Grbl_GCodeErrorFile", culture), Path.GetFileName(openFileDialog2.FileName), line_counter, resp[1]));
                                        }
                                        throw new Exception(String.Format(culture, resources.GetString("Grbl_GCodeError", culture), resp[1]));
                                    }
                                    if (resp[0] == "ALARM") {
                                        if (resp[1] == "3") {
                                            return false;
                                        }
                                    }
                                    
                                    throw new Exception(resources.GetString("Grbl_GCodeUnknownResponse", culture));
                                }
                                
                                ++line_counter;
                                
                                sum_c_line -= c_line[0];
                                c_line.RemoveAt(0);
                            } catch (TimeoutException) {
                            }
                            
                            if (((BackgroundWorker)sender).CancellationPending) {
                                if (rxBufferSize != -1) {
                                    serialPort1.Write(new byte[] { 0x18, }, 0, 1);
                                    
                                    rxBufferSize = -1;
                                }
                            }
                        }
                        
                        if (j == 0) {
                            return true;
                        }
                        serialPort1.Write(s_list[j-i] + "\n");
                    }
                };
                
                if (readFromFile) {
                    StreamReader inFile = new StreamReader(openFileDialog2.FileName, Encoding.ASCII, false);
                    Stream baseStream = inFile.BaseStream;
                    
                    ((BackgroundWorker)sender).ReportProgress(0, resources.GetString("PF_SendingFile", culture));
                    try {
                        long streamLength = baseStream.Length;
                        if (streamLength != 0) {
                            for (;; i = 1) {
                                ((BackgroundWorker)sender).ReportProgress((int)(1000 * baseStream.Position / streamLength), null);
                                
                                s_list[0] = inFile.ReadLine();
                                if (s_list[0] == null) {
                                    break;
                                }
                                
                                if (!SendToGrblController()) {
                                    return;
                                }
                            }
                        }
                    } finally {
                        inFile.Close();
                    }
                    
                    s_list[0] = "M2";
                    if (!SendToGrblController()) {
                        return;
                    }
                    
                    SendToGrblController();
                    return;
                }
                
                ((BackgroundWorker)sender).ReportProgress(0, resources.GetString("PF_SendingData", culture));
            } else {
                SendToGrblController = () => {
                    for (int j = i; i > 0; i--) {
                        outFile.Write(s_list[j-i] + "\n");
                    }
                    
                    if (((BackgroundWorker)sender).CancellationPending) {
                        return false;
                    }
                    return true;
                };
            }
            
            float dpiX = (imDpiX / MmPerInch);
            float dpiY = (imDpiY / MmPerInch);
            
            bool _1bitPalette = im1bitPalette;
            
            bool isNichromeBurner = (machineType == MachineType.NichromeBurner);
            bool isImpactGraver = (machineType == MachineType.ImpactGraver);
            bool isLaserEngraver = !(isNichromeBurner || isImpactGraver);
            
            bool wrappedOutput = false;
            if (isLaserEngraver) {
                wrappedOutput = gcWrappedOutput;
            }
            
            float multiplierY = 1F;
            if (wrappedOutput) {
                multiplierY = (mmPerRevolution / (cylinderDiameter*PI));
            }
            
            bool burnFromBottomToTop = gcBurnFromBottomToTop;
            if (burnFromBottomToTop) {
                multiplierY = -multiplierY;
            }
            
            FeedRateMode prevFeedRateMode = FeedRateMode.UnitsPerMin;
            DistanceMode prevDistanceMode = DistanceMode.Invalid;
            MotionMode prevMotionMode = MotionMode.Invalid;
            
            float prevX = Single.NegativeInfinity;
            float prevY = Single.NegativeInfinity;
            
            float prevF = -1F;
            float prevS = -1F;
            
            float rotarySpeed = gcRotarySpeed;
            
            float left = imLeft;
            float top = (imTop*multiplierY);
            
            Action InitializeParser = () => s_list[i++] = "G17G94G21G40G49";
            Action MagicComment = () => s_list[i++] = ";M0";
            
            Action<float> SpindleOn = (s) => {
                string Sx = "";
                if (s != prevS) {
                    Sx = ("S" + s.ToString(invariantCulture));
                    prevS = s;
                }
                
                s_list[i++] = (Sx + "M3");
            };
            Action SpindleOff = () => {
                s_list[i++] = "M5";
            };
            
            Action CoolantOn = () => s_list[i++] = "M8";
            Action CoolantOff = () => s_list[i++] = "M9";
            
            Action ProgramEnd = () => s_list[i++] = "M2";
            
            Action<float> Dwell = (p) => {
                if (p == 0F) {
                    return;
                }
                s_list[i++] = ("G4P" + p.ToString(invariantCulture));
            };
            
            Action<float, float, float, float, MotionMode, DistanceMode, FeedRateMode> GrblMove = (X, Y, f, s, motionMode, distanceMode, feedRateMode) => {
                if (X == X) {
                    X = (float)Math.Round((left + X), 3);
                } else {
                    X = prevX;
                }
                if (Y == Y) {
                    Y = -(float)Math.Round((top + Y*multiplierY), 3);
                } else {
                    Y = prevY;
                }
                if (X == prevX && Y == prevY) {
                    return;
                }
                
                string line = "";
                if (feedRateMode != prevFeedRateMode) {
                    line += ("G" + ((int)feedRateMode).ToString(invariantCulture));
                    prevFeedRateMode = feedRateMode;
                }
                if (distanceMode != prevDistanceMode) {
                    line += ("G" + ((int)distanceMode).ToString(invariantCulture));
                    prevDistanceMode = distanceMode;
                }
                if (motionMode != prevMotionMode) {
                    line += ("G" + ((int)motionMode).ToString(invariantCulture));
                    prevMotionMode = motionMode;
                }
                if (s != prevS) {
                    line += ("S" + s.ToString(invariantCulture));
                    prevS = s;
                }
                if (feedRateMode == FeedRateMode.InverseTime) {
                    if (motionMode != MotionMode.Seek) {
                        line += ("F" + (f / Math.Sqrt((X-prevX)*(X-prevX) + (Y-prevY)*(Y-prevY)/(multiplierY*multiplierY))).ToString("#.#", invariantCulture));
                        prevF = -1F;
                    }
                } else {
                    if (f != prevF) {
                        line += ("F" + f.ToString(invariantCulture));
                        prevF = f;
                    }
                }
                if (X != prevX) {
                    if (distanceMode == DistanceMode.Incremental) {
                        line += ("X" + (X-prevX).ToString("#.####", invariantCulture));
                    } else {
                        line += ("X" + X.ToString(invariantCulture));
                    }
                    prevX = X;
                }
                if (Y != prevY) {
                    if (distanceMode == DistanceMode.Incremental) {
                        line += ("Y" + (Y-prevY).ToString("#.####", invariantCulture));
                    } else {
                        line += ("Y" + Y.ToString(invariantCulture));
                    }
                    prevY = Y;
                }
                
                s_list[i++] = line;
            };
            
            Action<float, float> MoveAbs = (X, Y) => GrblMove(X, Y, prevF, prevS, MotionMode.Seek, DistanceMode.Absolute, FeedRateMode.UnitsPerMin);
            Action<float> MoveAbsX = (X) => GrblMove(X, Single.NaN, prevF, prevS, MotionMode.Seek, DistanceMode.Absolute, FeedRateMode.UnitsPerMin);
            Action<float> MoveAbsY = (Y) => GrblMove(Single.NaN, Y, prevF, prevS, MotionMode.Seek, DistanceMode.Absolute, FeedRateMode.UnitsPerMin);
            Action<float> MoveAbsRotary = (Y) => GrblMove(Single.NaN, Y, rotarySpeed, 0F, MotionMode.Linear, DistanceMode.Absolute, FeedRateMode.InverseTime);
            Action<float, float, float> MoveRelX = (X, f, s) => GrblMove(X, Single.NaN, f, s, MotionMode.Linear, DistanceMode.Incremental, FeedRateMode.UnitsPerMin);
            //Action<float, float, float> MoveRelY = (Y, f, s) => GrblMove(Single.NaN, Y, f, s, MotionMode.Linear, DistanceMode.Incremental, FeedRateMode.UnitsPerMin);
            
            const float scanOffset = 0F;
            
            NichromeControl nichromeOnOffCommand = gcNichromeOnOffCommand;
            
            float power = (float)Math.Round(gcPower, 1);
            float heatDelay = (float)Math.Round(gcHeatDelay, 1);
            
            float[] F = new float[256];
            float[] S = new float[256];
            
            if (_1bitPalette) {
                F[0] = (float)Math.Round(gcSpeed, 0);
                S[0] = power;
            } else {
                float[] speedGraph = gcSpeedGraph;
                if (isImpactGraver) {
                    speedGraph = new float[] { gcSpeed, gcSpeed, 0.35F, 0.35F, 0.65F, 0.65F, };
                }
                
                float p1 = speedGraph[1];
                float p7 = speedGraph[0];
                
                float p2 = (3*ImColorWhite*(1F-speedGraph[2]));
                float p3 = (3F*(p1 - (p1-p7)*speedGraph[3]));
                float p4 = (3*ImColorWhite*(1F-speedGraph[4]));
                float p5 = (3F*(p1 - (p1-p7)*speedGraph[5]));
                
                int color1 = (ImColorWhite-ImColorStep);
                
                float x0 = ImColorWhite;
                float y0 = p1;
                
                float q1 = gcPowerGraph[1];
                float q7 = gcPowerGraph[0];
                
                float q2 = (3*ImColorWhite*(1F-gcPowerGraph[2]));
                float q3 = (3F*(q1 - (q1-q7)*gcPowerGraph[3]));
                float q4 = (3*ImColorWhite*(1F-gcPowerGraph[4]));
                float q5 = (3F*(q1 - (q1-q7)*gcPowerGraph[5]));
                
                int color2 = (ImColorWhite-ImColorStep);
                
                float u0 = ImColorWhite;
                float v0 = q1;
                
                F[ImColorWhite] = (float)Math.Round(p1, 0);
                S[ImColorWhite] = (float)Math.Round(q1, 1);
                for (int j = 1; j < BezierSegmentsCount; j++) {
                    float t = ((float)j/BezierSegmentsCount);
                    float inv_t = (1F - t);
                    
                    float x1 = (inv_t*inv_t*inv_t*ImColorWhite + inv_t*inv_t*t*p2 + t*t*inv_t*p4);
                    float y1 = (inv_t*inv_t*inv_t*p1 + inv_t*inv_t*t*p3 + t*t*inv_t*p5 + t*t*t*p7);
                    if (x1 < color1) {
                        F[color1] = (float)Math.Round((y0 + (color1-x0) * (y1-y0) / (x1-x0)), 0);
                        
                        color1 -= ImColorStep;
                        if (color1 == 0 && color2 == 0) {
                            break;
                        }
                    }
                    
                    x0 = x1;
                    y0 = y1;
                    
                    float u1 = (inv_t*inv_t*inv_t*ImColorWhite + inv_t*inv_t*t*q2 + t*t*inv_t*q4);
                    float v1 = (inv_t*inv_t*inv_t*q1 + inv_t*inv_t*t*q3 + t*t*inv_t*q5 + t*t*t*q7);
                    if (u1 < color2) {
                        S[color2] = (float)Math.Round((v0 + (color2-u0) * (v1-v0) / (u1-u0)), 1);
                        
                        color2 -= ImColorStep;
                        if (color2 == 0 && color1 == 0) {
                            break;
                        }
                    }
                    
                    u0 = u1;
                    v0 = v1;
                }
                F[0] = (float)Math.Round(p7, 0);
                S[0] = (float)Math.Round(q7, 1);
            }
            
            F[255] = (float)Math.Round(gcWhiteSpeed, 0);
            //S[255] = 0F;
            
            bool bidirectional = gcBidirectional;
            bool dontReturnY = gcDontReturnY;
            
            bool airAssist = false;
            if (isLaserEngraver) {
                airAssist = gcAirAssist;
            }
            
            int numberOfPasses = 1;
            if (isImpactGraver) {
                numberOfPasses = gcNumberOfPasses;
            }
            
            bool skipWhite = gcSkipWhite;
            float whiteDistance = gcWhiteDistance;
            
            float[] acceldist = new float[256];
            float[] deceldist = new float[256];
            
            float accel = (gcAccel*3600F);
            for (int j = 0; j <= ImColorWhite; j += ImColorStep) {
                float acctime = (F[j] / accel);
                float accdist = (acctime * F[j] / 2F);
                
                acceldist[j] = (accdist*AccelDistMultiplier);
                deceldist[j] = (accdist*DecelDistMultiplier);
            }
            
            if (numberOfPasses > 1) {
                if (bidirectional) {
                    if (AccelDistMultiplier > DecelDistMultiplier) {
                        for (int j = 0; j <= ImColorWhite; j += ImColorStep) {
                            deceldist[j] = acceldist[j];
                        }
                    } else if (DecelDistMultiplier > AccelDistMultiplier) {
                        for (int j = 0; j <= ImColorWhite; j += ImColorStep) {
                            acceldist[j] = deceldist[j];
                        }
                    }
                }
            }
            
            CleaningStrategy cleaningStrategy = gcCleaningStrategy;
            bool stripOnTheLeftSide = ((width + (int)(left*dpiX - 0.5F)) > 0);
            
            float stripWidth = gcStripWidth;
            float stripSpeed = (float)Math.Round(gcStripSpeed, 0);
            float cleaningFieldWidth = gcCleaningFieldWidth;
            float cleaningFieldSpeed = (float)Math.Round(gcCleaningFieldSpeed, 0);
            int numberOfCleaningCycles = gcNumberOfCleaningCycles;
            
            if (cleaningFieldWidth > stripWidth) {
                cleaningFieldWidth = stripWidth;
            }
            
            int cleaningRowsCount;
            switch (cleaningStrategy) {
                case CleaningStrategy.None:
                    cleaningRowsCount = height;
                    break;
                case CleaningStrategy.AfterNRows:
                    cleaningRowsCount = gcCleaningRowsCount;
                    break;
                default:
                    if (bidirectional) {
                        cleaningRowsCount = 2;
                    } else {
                        cleaningRowsCount = 1;
                    }
                    break;
            }
            
            float stripPosition = -left;
            float cleaningPosition;
            
            if (stripOnTheLeftSide) {
                if (left < 0F) {
                    stripPosition = 0F;
                }
                cleaningPosition = (stripPosition - (stripWidth-cleaningFieldWidth)/2F);
            } else {
                cleaningPosition = (stripPosition + (stripWidth-cleaningFieldWidth)/2F);
                cleaningFieldWidth = -cleaningFieldWidth;
            }
            
            int cleaningCounter = (1+cleaningRowsCount);
            bool forward = true;
            
            if (isNichromeBurner) {
                if (bidirectional) {
                    forward = stripOnTheLeftSide;
                } else {
                    forward = !stripOnTheLeftSide;
                }
                
                if (stripOnTheLeftSide) {
                    s_list[0] = (
                        ";"+left.ToString("0.###", invariantCulture)+
                        ","+(-top).ToString("0.###", invariantCulture)+
                        ","+(width/dpiX).ToString("0.###", invariantCulture)+
                        ","+(-(height/dpiY*multiplierY)).ToString("0.###", invariantCulture)
                    );
                } else {
                    s_list[0] = (
                        ";"+(left + width/dpiX).ToString("0.###", invariantCulture)+
                        ","+(-top).ToString("0.###", invariantCulture)+
                        ","+(-(width/dpiX)).ToString("0.###", invariantCulture)+
                        ","+(-(height/dpiY*multiplierY)).ToString("0.###", invariantCulture)
                    );
                }
                if (cleaningStrategy != CleaningStrategy.None) {
                    s_list[0] += (
                        ",12"+
                        ","+(heatDelay*1000F).ToString(invariantCulture)+
                        ","+(cleaningPosition-stripPosition).ToString("#.###", invariantCulture)+
                        ","+stripSpeed.ToString(invariantCulture)
                    );
                } else {
                    s_list[0] += (
                        ",4"
                    );
                }
                
                InitializeParser();
                
                MoveAbsX(stripPosition);
                MoveAbsY(0F);
                
                if (cleaningStrategy != CleaningStrategy.None) {
                    MoveRelX(cleaningPosition, stripSpeed, -1F);
                    MagicComment();
                }
                
                if (nichromeOnOffCommand < NichromeControl.OUT2_M8_M9) {
                    switch (nichromeOnOffCommand) {
                        default: s_list[i++] = ("S"+power.ToString(invariantCulture)+"M3"); break;
                    }
                } else {
                    switch (nichromeOnOffCommand) {
                        default: s_list[i++] = "M8"; break;
                    }
                }
                Dwell(heatDelay);
                
                MoveRelX(stripPosition, stripSpeed, -1F);
                
                if (!SendToGrblController()) {
                    return;
                }
                
                for (int y = 0; y < height; y++) {
                    ((BackgroundWorker)sender).ReportProgress((1000 * y / height), null);
                    
                    MoveAbsY((0.5F+y)/dpiY);
                    
                    if (--cleaningCounter == 0) {
                        MoveAbsX(stripPosition);
                        MoveRelX(cleaningPosition, stripSpeed, -1F);
                        MagicComment();
                        
                        for (int j = 0; j < numberOfCleaningCycles; j++) {
                            MoveRelX(cleaningPosition-cleaningFieldWidth, cleaningFieldSpeed, -1F);
                            MoveRelX(cleaningPosition, cleaningFieldSpeed, -1F);
                        }
                        MagicComment();
                        
                        MoveRelX(stripPosition, stripSpeed, -1F);
                        
                        if (!SendToGrblController()) {
                            return;
                        }
                        
                        if (bidirectional) {
                            forward = stripOnTheLeftSide;
                        }
                        
                        cleaningCounter = cleaningRowsCount;
                    }
                    
                    byte* dest;
                    if (burnFromBottomToTop) {
                        dest = (byte*)(imDest + (height-y-1)*scanWidth);
                    } else {
                        dest = (byte*)(imDest + y*scanWidth);
                    }
                    
                    if (forward) {
                        MoveAbsX(0F);
                        
                        byte prevPixel = dest[0];
                        for (int x = 0; x < width; x++) {
                            byte pixel = dest[x];
                            if (pixel != prevPixel) {
                                MoveRelX(x/dpiX, F[prevPixel], -1F);
                            }
                            prevPixel = pixel;
                        }
                        MoveRelX(width/dpiX, F[prevPixel], -1F);
                    } else {
                        MoveAbsX(width/dpiX);
                        
                        byte prevPixel = dest[width-1];
                        for (int x = 0; x < width; x++) {
                            byte pixel = dest[width-x-1];
                            if (pixel != prevPixel) {
                                MoveRelX((width-x)/dpiX, F[prevPixel], -1F);
                            }
                            prevPixel = pixel;
                        }
                        MoveRelX(0F, F[prevPixel], -1F);
                    }
                    if (!SendToGrblController()) {
                        return;
                    }
                    
                    if (bidirectional) {
                        forward = !forward;
                    }
                }
                
                ((BackgroundWorker)sender).ReportProgress(1000, null);
                
                MoveAbsX(stripPosition);
                if (!dontReturnY) {
                    MoveAbsY(0F);
                }
                
                if (cleaningStrategy != CleaningStrategy.None) {
                    MoveRelX(cleaningPosition, stripSpeed, -1F);
                }
                
                if (nichromeOnOffCommand < NichromeControl.OUT2_M8_M9) {
                    switch (nichromeOnOffCommand) {
                        //default: s_list[i++] = "M5"; break;
                    }
                } else {
                    switch (nichromeOnOffCommand) {
                        //default: s_list[i++] = "M9"; break;
                    }
                }
                ProgramEnd();
                
                if (!SendToGrblController()) {
                    return;
                }
            } else {
                s_list[0] = (
                    ";"+(left + left2/dpiX).ToString("0.###", invariantCulture)+
                    ","+(-(top + top2/dpiY*multiplierY)).ToString("0.###", invariantCulture)+
                    ","+(width2/dpiX).ToString("0.###", invariantCulture)+
                    ","+(-(height2/dpiY*multiplierY)).ToString("0.###", invariantCulture)+
                    ",7"
                );
                
                InitializeParser();
                if (wrappedOutput) {
                    MoveAbs(-left, -top/multiplierY);
                    MagicComment();
                }
                
                SpindleOn(0F);
                if (airAssist) {
                    CoolantOn();
                }
                
                if (!SendToGrblController()) {
                    return;
                }
                
                for (int y = 0; y < height; y++) {
                    ((BackgroundWorker)sender).ReportProgress((1000 * y / height), null);
                    
                    byte* dest;
                    if (burnFromBottomToTop) {
                        dest = (byte*)(imDest + (height-y-1)*scanWidth);
                    } else {
                        dest = (byte*)(imDest + y*scanWidth);
                    }
                    
                    for (int j = 0; j < numberOfPasses; j++) {
                        int jj = width;
                        int x = 0;
                        
                        if (forward) {
                            for (; x < width; x++) {
                                if (dest[x] != ImColorUltraWhite) {
                                    break;
                                }
                            }
                            if (x >= width) {
                                break;
                            }
                            for (; jj > 0; jj--) {
                                if (dest[jj-1] != ImColorUltraWhite) {
                                    break;
                                }
                            }
                            
                            byte prevPixel = dest[x];
                            
                            if (wrappedOutput) {
                                MoveAbsRotary((0.5F+y)/dpiY);
                                MoveAbsX(x/dpiX - acceldist[prevPixel] - scanOffset);
                            } else {
                                MoveAbs((x/dpiX - acceldist[prevPixel] - scanOffset), (0.5F+y)/dpiY);
                            }
                            if (i != 0) {
                                MagicComment();
                            }
                            
                            MoveRelX((x/dpiX - scanOffset), F[prevPixel], 0F);
                            for (int x2 = -1; x < jj; x++) {
                                byte pixel = dest[x];
                                if (pixel != ImColorUltraWhite) {
                                    if (prevPixel == ImColorUltraWhite) {
                                        if (skipWhite && (x-x2)/dpiX >= whiteDistance) {
                                            MoveRelX((x/dpiX - scanOffset), F[255], 0F);
                                        } else {
                                            if (F[pixel] > prevF) {
                                                MoveRelX((x/dpiX - scanOffset), F[pixel], 0F);
                                            } else {
                                                MoveRelX((x/dpiX - scanOffset), prevF, 0F);
                                            }
                                        }
                                    }
                                    if (isImpactGraver) {
                                        MoveRelX(((x+0.5F)/dpiX - scanOffset), F[pixel], S[pixel]);
                                        MoveRelX(((x+1)/dpiX - scanOffset), F[pixel], 0F);
                                    } else if (pixel != prevPixel) {
                                        MoveRelX((x/dpiX - scanOffset), F[prevPixel], S[prevPixel]);
                                    }
                                } else {
                                    if (prevPixel != ImColorUltraWhite) {
                                        MoveRelX((x/dpiX - scanOffset), F[prevPixel], S[prevPixel]);
                                        x2 = x;
                                    }
                                }
                                prevPixel = pixel;
                            }
                            MoveRelX((x/dpiX - scanOffset), F[prevPixel], S[prevPixel]);
                            MoveRelX((x/dpiX + deceldist[prevPixel] - scanOffset), F[prevPixel], 0F);
                            MagicComment();
                        } else {
                            for (; x < width; x++) {
                                if (dest[width-x-1] != ImColorUltraWhite) {
                                    break;
                                }
                            }
                            if (x >= width) {
                                break;
                            }
                            for (; jj > 0; jj--) {
                                if (dest[width-jj] != ImColorUltraWhite) {
                                    break;
                                }
                            }
                            
                            byte prevPixel = dest[width-x-1];
                            
                            if (wrappedOutput) {
                                MoveAbsRotary((0.5F+y)/dpiY);
                                MoveAbsX((width-x)/dpiX + acceldist[prevPixel] + scanOffset);
                            } else {
                                MoveAbs(((width-x)/dpiX + acceldist[prevPixel] + scanOffset), (0.5F+y)/dpiY);
                            }
                            if (i != 0) {
                                MagicComment();
                            }
                            
                            MoveRelX(((width-x)/dpiX + scanOffset), F[prevPixel], 0F);
                            for (int x2 = -1; x < jj; x++) {
                                byte pixel = dest[width-x-1];
                                if (pixel != ImColorUltraWhite) {
                                    if (prevPixel == ImColorUltraWhite) {
                                        if (skipWhite && (x-x2)/dpiX >= whiteDistance) {
                                            MoveRelX(((width-x)/dpiX + scanOffset), F[255], 0F);
                                        } else {
                                            if (F[pixel] > prevF) {
                                                MoveRelX(((width-x)/dpiX + scanOffset), F[pixel], 0F);
                                            } else {
                                                MoveRelX(((width-x)/dpiX + scanOffset), prevF, 0F);
                                            }
                                        }
                                    }
                                    if (isImpactGraver) {
                                        MoveRelX(((width-x-0.5F)/dpiX + scanOffset), F[pixel], S[pixel]);
                                        MoveRelX(((width-x-1)/dpiX + scanOffset), F[pixel], 0F);
                                    } else if (pixel != prevPixel) {
                                        MoveRelX(((width-x)/dpiX + scanOffset), F[prevPixel], S[prevPixel]);
                                    }
                                } else {
                                    if (prevPixel != ImColorUltraWhite) {
                                        MoveRelX(((width-x)/dpiX + scanOffset), F[prevPixel], S[prevPixel]);
                                        x2 = x;
                                    }
                                }
                                prevPixel = pixel;
                            }
                            MoveRelX(((width-x)/dpiX + scanOffset), F[prevPixel], S[prevPixel]);
                            MoveRelX(((width-x)/dpiX - deceldist[prevPixel] + scanOffset), F[prevPixel], 0F);
                            MagicComment();
                        }
                        if (!SendToGrblController()) {
                            return;
                        }
                        
                        if (bidirectional) {
                            forward = !forward;
                        }
                    }
                }
                
                ((BackgroundWorker)sender).ReportProgress(1000, null);
                
                if (wrappedOutput) {
                    MoveAbsX(-left);
                    MoveAbsRotary(-top/multiplierY);
                } else {
                    if (isImpactGraver) {
                        MoveAbsX(0F);
                    } else {
                        MoveAbs(-left, -top/multiplierY);
                    }
                }
                ProgramEnd();
                
                if (!SendToGrblController()) {
                    return;
                }
            }
            
            SendToGrblController();
            return;
        } finally {
            outFile.Close();
            serialPort1.Close();
        }
    }
    
    private void BackgroundWorker2RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
        progressForm1.Close();
        
        Exception err = e.Error;
        if (err != null) {
            if (err is WarningException) {
                MessageBox.Show(this, err.Message, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            } else {
                MessageBox.Show(this, err.Message, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
