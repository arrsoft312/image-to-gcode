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
    
    private const float AccelDistMultiplier = 1.5F;
    private const float DecelDistMultiplier = 1.5F;
    
    private WrappedOutputDialog wrappedOutputDialog1;
    private ProgressForm progressForm1;
    
    public enum GcAxis {
        X,
        Y,
    }
    
    private enum MotionMode {
        Invalid = -1,
        Seek,
        Linear,
        CwArc,
        CcwArc,
        None = 80,
    }
    
    private enum DistanceMode {
        Invalid = -1,
        Absolute = 90,
        Incremental,
    }
    
    private enum CleaningStrategy {
        None,
        Always,
        AfterNRows,
        Distance,
    }
    
    private enum StripPositionType {
        Right,
        Left,
    }
    
    private enum GoToNextLineType {
        XYatTheSameTime,
        FirstXThenY,
        FirstYThenX,
    }
    
    private enum ReturnToOriginType {
        None,
        XYatTheSameTime,
        FirstXThenY,
        FirstYThenX,
        XAxis,
        YAxis,
    }
    
    private bool gcInverseTime;
    
    private float gcMultiplierX;
    private float gcMultiplierY;
    
    private GcAxis wrappingAxis = GcAxis.Y;
    private float mmPerRevolutionX = 360F;
    private float mmPerRevolutionY = 360F;
    private float cylinderDiameter = 80F;
    
    private float gcSpeed = 1200F;
    private float gcPower = 100F;
    private float gcAccel = 3000F;
    private float gcShift = 0F;
    
    private float gcHeatDelay = 7F;
    
    private bool gcAirAssist = false;
    private bool gcSkipWhite = true;
    private float gcWhiteSpeed = 4000F;
    private float gcWhiteDistance = 5F;
    
    private float[] gcSpeedGraph = new float[] { 350F, 2820F, 0.35F, 0.35F, 0.65F, 0.65F, };
    private float[] gcPowerGraph = new float[] { 100F, 100F, 0.35F, 0.35F, 0.65F, 0.65F, };
    
    private bool gcBidirectional = false;
    private bool gcBurnToTheStrip = true;
    
    private CleaningStrategy gcCleaningStrategy = CleaningStrategy.None;
    private int gcCleaningRowsCount = 2;
    private float gcCleaningDistance = 500F;
    private StripPositionType gcStripPosition = StripPositionType.Right;
    private float gcStripWidth = 20F;
    private float gcStripSpeed = 1000F;
    private float gcCleaningFieldWidth = 5F;
    private float gcCleaningFieldSpeed = 5000F;
    private int gcNumberOfCleaningCycles = 2;
    
    private int gcNumberOfPasses = 1;
    
    private GoToNextLineType gcGoToNextLine = GoToNextLineType.XYatTheSameTime;
    private ReturnToOriginType gcReturnToOrigin = ReturnToOriginType.XYatTheSameTime;
    private bool gcDontReturnY = false;
    
    private bool gcPrependFrame = true;
    private float gcFrameSpeed = 1000F;
    private float gcFramePower = 0F;
    private bool gcFrameWorkArea = false;
    
    private void BackgroundWorker2ProgressChanged(object sender, ProgressChangedEventArgs e) {
        if (e.UserState != null) {
            progressForm1.label1.Text = (string)e.UserState;
        }
        progressForm1.progressBar1.Value = e.ProgressPercentage;
    }
    
    private unsafe void BackgroundWorker2DoWork(object sender, DoWorkEventArgs e) {
        bool sendToDevice = bWorker2SendToDevice;
        bool readFromFile = bWorker2ReadFromFile;
        
        StreamWriter outFile;
        if (sendToDevice) {
            outFile = StreamWriter.Null;
        } else {
            outFile = new StreamWriter(saveFileDialog1.FileName, false, Encoding.ASCII);
        }
        
        try {
            string fwVersion = null;
            GrblOptions fwFlags = GrblOptions.None;
            int rxBufferSize;
            
            int sum_c_line = 0;
            List<int> c_line = new List<int>();
            
            int comment_counter = 0;
            int line_counter = 0;
            
            string[] s_list = new string[MaxImageSize*2+4];
            int i = 0;
            
            Func<bool> SendToGrblController;
            
            if (sendToDevice) {
                serialPort1.BaudRate = baudRate;
                serialPort1.PortName = comPort;
                
                serialPort1.ReadTimeout = 30;
                serialPort1.Open();
                
                if (!Grbl_GetSync(() => ((BackgroundWorker)sender).CancellationPending)) {
                    return;
                }
                
                rxBufferSize = Grbl_GetBuildInfo(ref fwVersion, ref fwFlags);
                
                SendToGrblController = () => {
                    for (int j = 0;; j++) {
                        if (i != -1) {
                            if (j == i) {
                                return true;
                            }
                            
                            int len = (s_list[j].Length + 1);
                            
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
                                    int error_code = Byte.Parse(resp[1], invariantCulture);
                                    if (resp[0] == "error") {
                                        if (line_counter == 0) {
                                            if (error_code == 1) {
                                                goto RESP_OK;
                                            }
                                            //throw new Exception(
                                        }
                                        
                                        if (readFromFile) {
                                            throw new Exception(String.Format(culture, resources.GetString("Grbl_GCodeErrorFile", culture), Path.GetFileName(openFileDialog2.FileName), (comment_counter + line_counter), resp[1]));
                                        }
                                        throw new Exception(String.Format(culture, resources.GetString("Grbl_GCodeError", culture), resp[1]));
                                    }
                                    
                                    return false;
                                }
                                
                                RESP_OK:
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
                        
                        if (i == -1) {
                            return true;
                        }
                        
                        serialPort1.Write(s_list[j] + "\n");
                    }
                };
                
                if (readFromFile) {
                    StreamReader inFile = new StreamReader(openFileDialog2.FileName, Encoding.ASCII, false);
                    Stream baseStream = inFile.BaseStream;
                    
                    try {
                        i = 1;
                        
                        for (;; ++comment_counter) {
                            string line = inFile.ReadLine();
                            if (line == null) {
                                return;
                            }
                            
                            string s = line.Replace(" ", "");
                            if (s.Length == 0 || s[0] != ';') {
                                break;
                            }
                            if (s.Length == 1 || s[1] != '%') {
                                continue;
                            }
                            
                            if (s_list[0] != null) {
                                throw new Exception(resources.GetString("Error_FileComment", culture));
                            }
                            
                            int num = line.IndexOf('%', 1, (line.Length-1));
                            s_list[0] = line.Remove(0, num);
                        }
                        
                        if (s_list[0] == null) {
                            s_list[0] = "%";
                        }
                        
                        baseStream.Seek(0, SeekOrigin.Begin);
                        inFile.DiscardBufferedData();
                        
                        for (int j = 0; j < comment_counter; j++) {
                            inFile.ReadLine();
                        }
                        
                        ((BackgroundWorker)sender).ReportProgress(0, resources.GetString("PF_SendingFile", culture));
                        
                        progressForm1.button1.Enabled = true;
                        progressForm1.button2.Enabled = true;
                        
                        long fileSize = baseStream.Length;
                        for (;;) {
                            if (!SendToGrblController()) {
                                return;
                            }
                            
                            ((BackgroundWorker)sender).ReportProgress((int)(1000 * baseStream.Position / fileSize), null);
                            
                            s_list[0] = inFile.ReadLine();
                            if (s_list[0] == null) {
                                break;
                            }
                        }
                    } finally {
                        inFile.Close();
                    }
                    
                    i = -1;
                    SendToGrblController();
                    
                    return;
                }
            } else {
                SendToGrblController = () => {
                    for (int j = 0; j < i; j++) {
                        outFile.Write(s_list[j] + "\n");
                    }
                    
                    if (((BackgroundWorker)sender).CancellationPending) {
                        return false;
                    }
                    return true;
                };
            }
            
            bool _1bitPalette = im1bitPalette;
            
            float dpiX = (imDpiX / MmPerInch);
            float dpiY = (imDpiY / MmPerInch);
            
            int width = imWidth;
            int height = imHeight;
            
            int scanWidth = ((width+3) / 4 * 4);
            
            bool inverseTime = gcInverseTime;
            
            float multiplierX;
            float multiplierY;
            
            Origin origin = machineOrigin;
            switch (origin) {
                case Origin.TopLeft:
                    multiplierX = -gcMultiplierX;
                    multiplierY = gcMultiplierY;
                    break;
                case Origin.BottomRight:
                    multiplierX = gcMultiplierX;
                    multiplierY = -gcMultiplierY;
                    break;
                case Origin.BottomLeft:
                    multiplierX = -gcMultiplierX;
                    multiplierY = -gcMultiplierY;
                    break;
                default:
                    multiplierX = gcMultiplierX;
                    multiplierY = gcMultiplierY;
                    break;
            }
            
            float left = (imLeft * multiplierX);
            float top = (imTop * multiplierY);
            
            DistanceMode prevDistanceMode = DistanceMode.Invalid;
            MotionMode prevMotionMode = MotionMode.Invalid;
            
            float prevX = (multiplierX / 0F);
            float prevY = (multiplierY / 0F);
            
            float prevF = -1F;
            float prevS = -1F;
            
            Action InitializeParser = () => {
                if (inverseTime) {
                    s_list[i++] = "G17G93G21G40G49";
                } else {
                    s_list[i++] = "G17G94G21G40G49";
                }
            };
            
            Action ProgramPause = () => s_list[i++] = "M0";
            Action ProgramEnd = () => s_list[i++] = "M2";
            
            Action SpindleOff = () => s_list[i++] = "M5";
            
            Action<float> SpindleOn = (s) => {
                string Sx = "";
                if (s != prevS) {
                    Sx = ("S" + s.ToString(invariantCulture));
                    prevS = s;
                }
                
                s_list[i++] = (Sx + "M3");
            };
            
            Action CoolantOn = () => s_list[i++] = "M8";
            Action CoolantOff = () => s_list[i++] = "M9";
            
            Action<float> Dwell = (p) => {
                if (p == 0F) {
                    return;
                }
                
                s_list[i++] = ("G4P" + p.ToString(invariantCulture));
            };
            
            Action<float, float, float, float, MotionMode, DistanceMode> GrblMove = (X, Y, f, s, motionMode, distanceMode) => {
                if (*(int*)&X != NaN) {
                    X = (float)Math.Round((left + X*multiplierX), 3);
                } else {
                    X = prevX;
                }
                if (*(int*)&Y != NaN) {
                    Y = (float)Math.Round((top + Y*multiplierY), 3);
                } else {
                    Y = prevY;
                }
                if (X == prevX && Y == prevY) {
                    return;
                }
                
                string line = "";
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
                if (inverseTime) {
                    if (motionMode != MotionMode.Seek) {
                        line += ("F" + (f / Math.Sqrt((X-prevX)*(X-prevX)/(multiplierX*multiplierX) + (Y-prevY)*(Y-prevY)/(multiplierY*multiplierY))).ToString("#.#", invariantCulture));
                        prevF = f;
                    }
                } else {
                    if (f != prevF) {
                        line += ("F" + f.ToString(invariantCulture));
                        prevF = f;
                    }
                }
                if (X != prevX) {
                    line += "X";
                    if (distanceMode == DistanceMode.Incremental) {
                        line += (X-prevX).ToString("#.####", invariantCulture);
                    } else {
                        line += X.ToString(invariantCulture);
                    }
                    prevX = X;
                }
                if (Y != prevY) {
                    line += "Y";
                    if (distanceMode == DistanceMode.Incremental) {
                        line += (Y-prevY).ToString("#.####", invariantCulture);
                    } else {
                        line += Y.ToString(invariantCulture);
                    }
                    prevY = Y;
                }
                
                s_list[i++] = line;
            };
            
            Action<float, float> MoveAbs = (X, Y) => GrblMove(X, Y, prevF, prevS, MotionMode.Seek, DistanceMode.Absolute);
            Action<float> MoveAbsX = (X) => GrblMove(X, Single.NaN, prevF, prevS, MotionMode.Seek, DistanceMode.Absolute);
            Action<float> MoveAbsY = (Y) => GrblMove(Single.NaN, Y, prevF, prevS, MotionMode.Seek, DistanceMode.Absolute);
            Action<float, float, float> MoveRelX = (X, f, s) => GrblMove(X, Single.NaN, f, s, MotionMode.Linear, DistanceMode.Incremental);
            Action<float, float, float> MoveRelY = (Y, f, s) => GrblMove(Single.NaN, Y, f, s, MotionMode.Linear, DistanceMode.Incremental);
            
            MachineType machine = machineType;
            
            bool isNichromeBurner = (machine == MachineType.NichromeBurner);
            bool isNotNichromeBurner = (machine != MachineType.NichromeBurner);
            bool isImpactGraver = (machine == MachineType.ImpactGraver);
            //bool isNotImpactGraver = (machine != MachineType.ImpactGraver);
            bool isLaserEngraver = (!(isNichromeBurner || isImpactGraver));
            //bool isNotLaserEngraver = (isNichromeBurner || isImpactGraver);
            
            bool prependFrame = gcPrependFrame;
            bool frameWorkArea = gcFrameWorkArea;
            float frameSpeed = (float)Math.Round(gcFrameSpeed, 0);
            
            float framePower = 0F;
            bool airAssist = false;
            
            if (isLaserEngraver) {
                framePower = (float)Math.Round(gcFramePower, 1);
                airAssist = gcAirAssist;
            }
            
            float speed = (float)Math.Round(gcSpeed, 0);
            float power = (float)Math.Round(gcPower, 1);
            
            float[] F = new float[256];
            float[] S = new float[256];
            
            if (_1bitPalette) {
                F[0] = speed;
                S[0] = power;
            } else {
                float[] speedGraph = gcSpeedGraph;
                float[] powerGraph = gcPowerGraph;
                if (isNichromeBurner) {
                    powerGraph = new float[] { gcPower, gcPower, 0.35F, 0.35F, 0.65F, 0.65F, };
                } else if (isImpactGraver) {
                    speedGraph = new float[] { gcSpeed, gcSpeed, 0.35F, 0.35F, 0.65F, 0.65F, };
                }
                
                float p1 = speedGraph[1];
                float p7 = speedGraph[0];
                
                float p2 = (3*ImColorWhite*(1F-speedGraph[2]));
                float p3 = (3F*(p1 - (p1-p7)*speedGraph[3]));
                float p4 = (3*ImColorWhite*(1F-speedGraph[4]));
                float p5 = (3F*(p1 - (p1-p7)*speedGraph[5]));
                
                float q1 = powerGraph[1];
                float q7 = powerGraph[0];
                
                float q2 = (3*ImColorWhite*(1F-powerGraph[2]));
                float q3 = (3F*(q1 - (q1-q7)*powerGraph[3]));
                float q4 = (3*ImColorWhite*(1F-powerGraph[4]));
                float q5 = (3F*(q1 - (q1-q7)*powerGraph[5]));
                
                int color1 = (ImColorWhite-ImColorStep);
                int color2 = (ImColorWhite-ImColorStep);
                
                float x0 = ImColorWhite;
                float y0 = p1;
                
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
                    
                    float u1 = (inv_t*inv_t*inv_t*ImColorWhite + inv_t*inv_t*t*q2 + t*t*inv_t*q4);
                    float v1 = (inv_t*inv_t*inv_t*q1 + inv_t*inv_t*t*q3 + t*t*inv_t*q5 + t*t*t*q7);
                    if (u1 < color2) {
                        S[color2] = (float)Math.Round((v0 + (color2-u0) * (v1-v0) / (u1-u0)), 1);
                        
                        color2 -= ImColorStep;
                        if (color2 == 0 && color1 == 0) {
                            break;
                        }
                    }
                    
                    x0 = x1;
                    y0 = y1;
                    
                    u0 = u1;
                    v0 = v1;
                }
                F[0] = (float)Math.Round(p7, 0);
                S[0] = (float)Math.Round(q7, 1);
            }
            
            F[255] = (float)Math.Round(gcWhiteSpeed, 0);
            if (isNichromeBurner) {
                S[255] = power;
            }
            
            float accel = (gcAccel * 3600F);
            
            float[] acceldist = new float[256];
            float[] deceldist = new float[256];
            
            if (isNotNichromeBurner) {
                for (int j = 0; j <= ImColorWhite; j += ImColorStep) {
                    float acctime = (F[j] / accel);
                    float accdist = (acctime * F[j] / 2F);
                    
                    acceldist[j] = (accdist*AccelDistMultiplier);
                    deceldist[j] = (accdist*DecelDistMultiplier);
                }
            }
            
            bool bidirectional = gcBidirectional;
            
            bool burnToTheStrip = false;
            CleaningStrategy cleaningStrategy = CleaningStrategy.None;
            
            if (isNichromeBurner) {
                burnToTheStrip = gcBurnToTheStrip;
                cleaningStrategy = gcCleaningStrategy;
            }
            
            bool skipWhite = true;
            float whiteDistance = 0F;
            
            float shift = 0F;
            
            if (isNotNichromeBurner) {
                skipWhite = gcSkipWhite;
                whiteDistance = gcWhiteDistance;
                
                if (bidirectional) {
                    shift = gcShift;
                }
            }
            
            float heatDelay = (float)Math.Round(gcHeatDelay, 1);
            bool stripOnTheRightSide = true;
            float stripWidth = gcStripWidth;
            float stripSpeed = (float)Math.Round(gcStripSpeed, 0);
            float cleaningFieldWidth = gcCleaningFieldWidth;
            float cleaningFieldSpeed = (float)Math.Round(gcCleaningFieldSpeed, 0);
            int numberOfCleaningCycles = gcNumberOfCleaningCycles;
            
            if (cleaningStrategy != CleaningStrategy.None) {
                stripOnTheRightSide = (gcStripPosition == StripPositionType.Right);
            }
            
            if (cleaningFieldWidth > stripWidth) {
                cleaningFieldWidth = stripWidth;
            }
            
            GoToNextLineType goToNextLine = gcGoToNextLine;
            ReturnToOriginType returnToOrigin = gcReturnToOrigin;
            bool dontReturnY = gcDontReturnY;
            
            int numberOfPasses = gcNumberOfPasses;
            
            //
            
            i = 0;
            
            if (sendToDevice) {
                
                s_list[i++] = "%";
                
                ((BackgroundWorker)sender).ReportProgress(0, resources.GetString("PF_SendingData", culture));
                
                progressForm1.button1.Enabled = true;
                progressForm1.button2.Enabled = true;
                
            } else {
                
                s_list[i++] = (";Generated with " + AppTitle + " v" + AppVersion + " - https://www.image2gcode.ru");
                s_list[i++] = ";%";
                
            }
            
            InitializeParser();
            if (isNichromeBurner) {
                
                if (stripOnTheRightSide) {
                    MoveAbsX(0F);
                    MoveAbsY(0F);
                    
                    if (prependFrame) {
                        MoveRelX(width/dpiX, frameSpeed, -1F);
                        MoveRelY(height/dpiY, frameSpeed, -1F);
                        MoveRelX(0F, frameSpeed, -1F);
                        MoveRelY(0F, frameSpeed, -1F);
                        
                        ProgramPause();
                    }
                    
                    if (cleaningStrategy != CleaningStrategy.None) {
                        MoveRelX(-stripWidth/2F, stripSpeed, -1F);
                    }
                    
                    SpindleOn(power);
                    Dwell(heatDelay);
                    
                    MoveRelX(0F, stripSpeed, power);
                } else {
                    MoveAbsX(width/dpiX);
                    MoveAbsY(0F);
                    
                    if (prependFrame) {
                        MoveRelX(0F, frameSpeed, -1F);
                        MoveRelY(height/dpiY, frameSpeed, -1F);
                        MoveRelX(width/dpiX, frameSpeed, -1F);
                        MoveRelY(0F, frameSpeed, -1F);
                        
                        ProgramPause();
                    }
                    
                    if (cleaningStrategy != CleaningStrategy.None) {
                        MoveRelX((width/dpiX + stripWidth/2F), stripSpeed, -1F);
                    }
                    
                    SpindleOn(power);
                    Dwell(heatDelay);
                    
                    MoveRelX(width/dpiX, stripSpeed, power);
                }
                
            } else {
                
                if (prependFrame) {
                    int left2 = 0;
                    int top2 = 0;
                    int width2 = width;
                    int height2 = height;
                    
                    if (frameWorkArea) {
                        left2 = width;
                        top2 = -1;
                        width2 = 0;
                        
                        for (int y = 0; y < height; y++) {
                            byte* dest = (byte*)(imDest + y*scanWidth + width);
                            for (int x = 0; x < width; x++) {
                                if (dest[-x-1] == ImColorUltraWhite) {
                                    continue;
                                }
                                
                                if (top2 == -1) {
                                    top2 = y;
                                }
                                if (left2 > x) {
                                    left2 = x;
                                }
                                
                                for (x = width; x > 0; x--) {
                                    if (dest[-x] == ImColorUltraWhite) {
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
                        
                        width2 -= left2;
                        height2 -= (top2-1);
                    }
                    
                    if (top2 != -1) {
                        switch (goToNextLine) {
                            case GoToNextLineType.FirstXThenY:
                                MoveAbsX(left2/dpiX);
                                MoveAbsY(top2/dpiY);
                                break;
                            case GoToNextLineType.FirstYThenX:
                                MoveAbsY(top2/dpiY);
                                MoveAbsX(left2/dpiX);
                                break;
                            default:
                                MoveAbs(left2/dpiX, top2/dpiY);
                                break;
                        }
                        
                        SpindleOn(framePower);
                        MoveRelX((left2+width2)/dpiX, frameSpeed, framePower);
                        MoveRelY((top2+height2)/dpiY, frameSpeed, framePower);
                        MoveRelX(left2/dpiX, frameSpeed, framePower);
                        MoveRelY(top2/dpiY, frameSpeed, framePower);
                        SpindleOff();
                        
                        ProgramPause();
                    }
                }
                
                SpindleOn(0F);
                if (airAssist) {
                    CoolantOn();
                }
                
            }
            
            if (!SendToGrblController()) {
                return;
            }
            
            int cleaningRowsCount;
            switch (cleaningStrategy) {
                case CleaningStrategy.Always:
                    if (bidirectional) {
                        cleaningRowsCount = 2;
                    } else {
                        cleaningRowsCount = 1;
                    }
                    break;
                case CleaningStrategy.AfterNRows:
                    cleaningRowsCount = (gcCleaningRowsCount*numberOfPasses);
                    break;
                case CleaningStrategy.Distance:
                    cleaningRowsCount = (int)(gcCleaningDistance / (width/dpiX) + 0.5F);
                    if (cleaningRowsCount == 0) {
                        cleaningRowsCount = 1;
                    }
                    break;
                default:
                    cleaningRowsCount = (height*numberOfPasses);
                    break;
            }
            
            int cleaningCounter = (1+cleaningRowsCount);
            
            bool forward = stripOnTheRightSide;
            if (!bidirectional) {
                if (burnToTheStrip) {
                    forward = !stripOnTheRightSide;
                }
            }
            
            for (int y = 0; y < height; y++) {
                ((BackgroundWorker)sender).ReportProgress((1000 * y / height), null);
                
                byte* dest = (byte*)(imDest + y*scanWidth);
                
                for (int n = 0; n < numberOfPasses; n++) {
                    int width2 = width;
                    int x = 0;
                    
                    if (isNichromeBurner) {
                        
                        if (--cleaningCounter == 0) {
                            i = 0;
                            
                            if (stripOnTheRightSide) {
                                MoveAbsX(0F);
                                MoveRelX(-(stripWidth-cleaningFieldWidth)/2F, stripSpeed, power);
                                for (int j = 0; j < numberOfCleaningCycles; j++) {
                                    MoveRelX((-(stripWidth-cleaningFieldWidth)/2F - cleaningFieldWidth), cleaningFieldSpeed, power);
                                    MoveRelX(-(stripWidth-cleaningFieldWidth)/2F, cleaningFieldSpeed, power);
                                }
                                MoveRelX(0F, stripSpeed, power);
                            } else {
                                MoveAbsX(width/dpiX);
                                MoveRelX((width/dpiX + (stripWidth-cleaningFieldWidth)/2F), stripSpeed, power);
                                for (int j = 0; j < numberOfCleaningCycles; j++) {
                                    MoveRelX((width/dpiX + (stripWidth-cleaningFieldWidth)/2F + cleaningFieldWidth), cleaningFieldSpeed, power);
                                    MoveRelX((width/dpiX + (stripWidth-cleaningFieldWidth)/2F), cleaningFieldSpeed, power);
                                }
                                MoveRelX(width/dpiX, stripSpeed, power);
                            }
                            
                            if (!SendToGrblController()) {
                                return;
                            }
                            
                            if (bidirectional) {
                                forward = stripOnTheRightSide;
                            }
                            
                            cleaningCounter = cleaningRowsCount;
                        }
                        
                    } else {
                        
                        if (forward) {
                            for (; x < width; x++) {
                                if (dest[width-x-1] != ImColorUltraWhite) {
                                    break;
                                }
                            }
                            if (x >= width) {
                                break;
                            }
                            for (; width2 > 0; width2--) {
                                if (dest[width-width2] != ImColorUltraWhite) {
                                    break;
                                }
                            }
                        } else {
                            for (; x < width; x++) {
                                if (dest[x] != ImColorUltraWhite) {
                                    break;
                                }
                            }
                            if (x >= width) {
                                break;
                            }
                            for (; width2 > 0; width2--) {
                                if (dest[width2-1] != ImColorUltraWhite) {
                                    break;
                                }
                            }
                        }
                        
                    }
                    
                    i = 0;
                    
                    if (forward) {
                        
                        byte prevPixel = dest[width-x-1];
                        int x2 = -1;
                        
                        switch (goToNextLine) {
                            case GoToNextLineType.FirstXThenY:
                                MoveAbsX(x/dpiX - acceldist[prevPixel] - shift);
                                MoveAbsY((y + 0.5F) / dpiY);
                                break;
                            case GoToNextLineType.FirstYThenX:
                                MoveAbsY((y + 0.5F) / dpiY);
                                MoveAbsX(x/dpiX - acceldist[prevPixel] - shift);
                                break;
                            default:
                                MoveAbs((x/dpiX - acceldist[prevPixel] - shift), ((y + 0.5F) / dpiY));
                                break;
                        }
                        
                        MoveRelX((x/dpiX - shift), F[prevPixel], S[255]);
                        for (; x < width2; x++) {
                            byte pixel = dest[width-x-1];
                            if (pixel != ImColorUltraWhite) {
                                if (prevPixel == ImColorUltraWhite) {
                                    if (skipWhite) {
                                        if ((x-x2)/dpiX >= whiteDistance) {
                                            MoveRelX((x/dpiX - shift), F[255], S[255]);
                                        }
                                    }
                                    if (F[pixel] > prevF) {
                                        MoveRelX((x/dpiX - shift), F[pixel], S[255]);
                                    } else {
                                        MoveRelX((x/dpiX - shift), prevF, S[255]);
                                    }
                                }
                                if (isImpactGraver) {
                                    MoveRelX(((x + 0.5F)/dpiX - shift), speed, S[pixel]);
                                    MoveRelX(((x + 1.0F)/dpiX - shift), speed, 0F);
                                } else if (pixel != prevPixel) {
                                    MoveRelX((x/dpiX - shift), F[prevPixel], S[prevPixel]);
                                }
                            } else {
                                if (prevPixel != ImColorUltraWhite) {
                                    MoveRelX((x/dpiX - shift), F[prevPixel], S[prevPixel]);
                                    x2 = x;
                                }
                            }
                            if (x == (width2-1)) {
                                MoveRelX((width2/dpiX - shift), F[pixel], S[pixel]);
                            }
                            prevPixel = pixel;
                        }
                        MoveRelX((x/dpiX + deceldist[prevPixel] - shift), F[prevPixel], S[255]);
                        
                    } else {
                        
                        byte prevPixel = dest[x];
                        int x2 = -1;
                        
                        switch (goToNextLine) {
                            case GoToNextLineType.FirstXThenY:
                                MoveAbsX((width-x)/dpiX + acceldist[prevPixel] + shift);
                                MoveAbsY((y + 0.5F) / dpiY);
                                break;
                            case GoToNextLineType.FirstYThenX:
                                MoveAbsY((y + 0.5F) / dpiY);
                                MoveAbsX((width-x)/dpiX + acceldist[prevPixel] + shift);
                                break;
                            default:
                                MoveAbs(((width-x)/dpiX + acceldist[prevPixel] + shift), ((y + 0.5F) / dpiY));
                                break;
                        }
                        
                        MoveRelX(((width-x)/dpiX + shift), F[prevPixel], S[255]);
                        for (; x < width2; x++) {
                            byte pixel = dest[x];
                            if (pixel != ImColorUltraWhite) {
                                if (prevPixel == ImColorUltraWhite) {
                                    if (skipWhite) {
                                        if ((x-x2)/dpiX >= whiteDistance) {
                                            MoveRelX(((width-x)/dpiX + shift), F[255], S[255]);
                                        }
                                    }
                                    if (F[pixel] > prevF) {
                                        MoveRelX(((width-x)/dpiX + shift), F[pixel], S[255]);
                                    } else {
                                        MoveRelX(((width-x)/dpiX + shift), prevF, S[255]);
                                    }
                                }
                                if (isImpactGraver) {
                                    MoveRelX(((width-x - 0.5F)/dpiX + shift), speed, S[pixel]);
                                    MoveRelX(((width-x - 1.0F)/dpiX + shift), speed, 0F);
                                } else if (pixel != prevPixel) {
                                    MoveRelX(((width-x)/dpiX + shift), F[prevPixel], S[prevPixel]);
                                }
                            } else {
                                if (prevPixel != ImColorUltraWhite) {
                                    MoveRelX(((width-x)/dpiX + shift), F[prevPixel], S[prevPixel]);
                                    x2 = x;
                                }
                            }
                            if (x == (width2-1)) {
                                MoveRelX(((width-width2)/dpiX + shift), F[pixel], S[pixel]);
                            }
                            prevPixel = pixel;
                        }
                        MoveRelX(((width-x)/dpiX - deceldist[prevPixel] + shift), F[prevPixel], S[255]);
                        
                    }
                    
                    if (!SendToGrblController()) {
                        return;
                    }
                    
                    if (bidirectional) {
                        forward = !forward;
                    }
                }
            }
            
            i = 0;
            
            if (isNichromeBurner) {
                
                if (stripOnTheRightSide) {
                    MoveAbsX(0F);
                    if (!dontReturnY) {
                        MoveAbsY(0F);
                    }
                    
                    if (cleaningStrategy != CleaningStrategy.None) {
                        MoveRelX(-stripWidth/2F, stripSpeed, power);
                    }
                    
                    SpindleOff();
                } else {
                    MoveAbsX(width/dpiX);
                    if (!dontReturnY) {
                        MoveAbsY(0F);
                    }
                    
                    if (cleaningStrategy != CleaningStrategy.None) {
                        MoveRelX((width/dpiX + stripWidth/2F), stripSpeed, power);
                    }
                    
                    SpindleOff();
                }
                
            } else {
                
                switch (returnToOrigin) {
                    case ReturnToOriginType.XAxis:
                        MoveAbsX(0F);
                        break;
                    case ReturnToOriginType.YAxis:
                        MoveAbsY(0F);
                        break;
                    case ReturnToOriginType.FirstXThenY:
                        MoveAbsX(0F);
                        MoveAbsY(0F);
                        break;
                    case ReturnToOriginType.FirstYThenX:
                        MoveAbsY(0F);
                        MoveAbsX(0F);
                        break;
                    case ReturnToOriginType.XYatTheSameTime:
                        MoveAbs(0F, 0F);
                        break;
                }
                
                SpindleOff();
                if (airAssist) {
                    CoolantOff();
                }
                
            }
            ProgramEnd();
            
            if (!SendToGrblController()) {
                return;
            }
            
            ((BackgroundWorker)sender).ReportProgress(1000, null);
            
            i = -1;
            SendToGrblController();
            
            return;
        } finally {
            outFile.Close();
            serialPort1.Close();
        }
    }
    
    private void BackgroundWorker2RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
        progressForm1.Close();
        if (e.Error != null) {
            MessageBox.Show(this, e.Error.Message, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
