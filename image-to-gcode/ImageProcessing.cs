using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

partial class image2gcode {
    private const int MaxImageSize = 16383;
    private const int MinImageSize = 4;
    
    private const float MaxImageDpi = 600F;
    private const float MinImageDpi = 72F;
    
    private enum ImageDithering {
        Threshold,
        FloydSteinberg,
        Jarvis,
        Stucki,
        Atkinson,
        Burkes,
        Sierra,
    }
    
    private unsafe byte* imColorTable;
    
    private unsafe byte* ditherEmptyTable;
    
    private unsafe byte* ditherTable_7_16;
    private unsafe byte* ditherTable_3_16;
    private unsafe byte* ditherTable_5_16;
    private unsafe byte* ditherTable_1_16;
    
    private unsafe byte* ditherTable_7_48;
    private unsafe byte* ditherTable_5_48;
    private unsafe byte* ditherTable_3_48;
    private unsafe byte* ditherTable_1_48;
    
    private unsafe byte* ditherTable_8_42;
    private unsafe byte* ditherTable_4_42;
    private unsafe byte* ditherTable_2_42;
    private unsafe byte* ditherTable_1_42;
    
    private unsafe byte* ditherTable_1_8;
    
    private unsafe byte* ditherTable_8_32;
    private unsafe byte* ditherTable_4_32;
    private unsafe byte* ditherTable_2_32;
    
    private unsafe byte* ditherTable_5_32;
    private unsafe byte* ditherTable_3_32;
    
    private bool im1bitPalette = true;
    private ImageDithering imDithering = ImageDithering.Threshold;
    private int imBlackThreshold = 127;
    private bool imInvertColors = false;
    private int imBrightness = 0;
    private float imContrast = 1F;
    private float imGamma = 1F;
    private int imSharpenForce = 0;
    private InterpolationMode imInterpolation = InterpolationMode.Default;
    
    private float gcG0Speed = 12000F;
    
    private Bitmap imSrc;
    
    private IntPtr imPreview;
    private IntPtr imResized;
    private IntPtr imDest;
    
    private Rectangle imCanvas;
    
    private Rectangle prevCanvas;
    private InterpolationMode prevInterpolation;
    
    private int gcPixelsCount;
    private int gcJobTime;
    
    private string[] previewBackground = new string[PreviewCount];
    private WrapMode[] previewWrapMode = new WrapMode[PreviewCount];
    private int[] previewSize = new int[PreviewCount];
    private Color[] previewBgColor = new Color[PreviewCount];
    private Color[] previewDotColor = new Color[PreviewCount];
    
    private unsafe bool LoadImage(string fileName, Bitmap im = null) {
        if (fileName != null) {
            try {
                im = (Bitmap)Image.FromFile(fileName, false);
            } catch (OutOfMemoryException) {
                MessageBox.Show(this, String.Format(culture, resources.GetString("Error_InvalidImageFile", culture), Path.GetFileName(fileName)), AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            } catch (FileNotFoundException) {
                MessageBox.Show(this, String.Format(culture, resources.GetString("Error_CouldNotFindFile", culture), fileName), AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            } catch (Exception ex) {
                MessageBox.Show(this, ex.Message, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }
        
        bWorkerFlags = BWorkerFlagExit;
        while (backgroundWorker1.IsBusy) {
            Application.DoEvents();
        }
        
        toolStripStatusLabel1.Text = resources.GetString("Status_LoadingImage", culture);
        
        imWidth = im.Width;
        imHeight = im.Height;
        
        presetToolStripMenuItem.Enabled = true;
        
        reloadToolStripMenuItem.Enabled = (fileName != null);
        exportToolStripMenuItem.Enabled = true;
        export2ToolStripMenuItem.Enabled = true;
        closeToolStripMenuItem.Enabled = true;
        saveToolStripMenuItem.Enabled = true;
        cropToolStripMenuItem.Enabled = true;
        sendToolStripMenuItem.Enabled = true;
        send2ToolStripMenuItem.Enabled = true;
        
        exportToolStripButton.Enabled = true;
        sendToolStripButton.Enabled = true;
        
        tableLayoutPanel2.Enabled = true;
        tableLayoutPanel13.Enabled = true;
        tableLayoutPanel4.Enabled = true;
        
        Size ibSize = imageBox1.ClientSize;
        
        int zoom = (int)((float)ibSize.Width / imWidth * 100F);
        if ((imHeight * zoom / 100) > ibSize.Height) {
            zoom = (int)((float)ibSize.Height / imHeight * 100F);
        }
        
        if (zoom > 100) {
            imageBox1.Zoom = 100;
        } else {
            imageBox1.Zoom = zoom;
        }
        
        imageBox1.ImWidth = imWidth;
        imageBox1.ImHeight = imHeight;
        imageBox1.StartPointX = 0F;
        imageBox1.StartPointY = 0F;
        imageBox1.AdjustLayout();
        
        toolStripStatusLabel2.Visible = true;
        toolStripStatusLabel3.Visible = true;
        toolStripStatusLabel4.Visible = true;
        toolStripStatusLabel5.Visible = true;
        toolStripStatusLabel6.Visible = true;
        
        imCanvas = new Rectangle(0, 0, imWidth, imHeight);
        
        imSrc = new Bitmap(imWidth, imHeight, PixelFormat.Format8bppIndexed);
        
        ColorPalette palette = imSrc.Palette;
        for (int i = 0; i < 256; i++) {
            palette.Entries[i] = Color.FromArgb(-16777216 + i*65536 + i*256 + i);
        }
        imSrc.Palette = palette;
        
        BitmapData bDataSrc = new BitmapData();
        BitmapData bDataDest = new BitmapData();
        
        im.LockBits(imCanvas, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb, bDataSrc);
        imSrc.LockBits(imCanvas, ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed, bDataDest);
        
        IntPtr imScan0 = bDataSrc.Scan0;
        int imScanWidth = bDataSrc.Stride;
        
        IntPtr destScan0 = bDataDest.Scan0;
        int destScanWidth = bDataDest.Stride;
        
        Parallel.For(0, imHeight, (y) => {
            byte* src = (byte*)(imScan0 + y*imScanWidth);
            byte* dest = (byte*)(destScan0 + y*destScanWidth);
            
            for (int x = 0; x < imWidth; x++) {
                byte alpha = src[x*4+3];
                if (alpha == 0) {
                    dest[x] = ImColorUltraWhite;
                } else {
                    byte gray = (byte)(0.299F*src[x*4+2] + 0.587F*src[x*4+1] + 0.114F*src[x*4+0]);
                    if (alpha == 255) {
                        dest[x] = gray;
                    } else {
                        float j = (alpha / 255F);
                        dest[x] = (byte)(j*gray + (1F-j)*ImColorUltraWhite);
                    }
                }
            }
        });
        
        im.UnlockBits(bDataSrc);
        imSrc.UnlockBits(bDataDest);
        
        im.Dispose();
        
        trackBar1.Value = 127;
        checkBox2.Checked = false;
        trackBar2.Value = 0;
        trackBar3.Value = 0;
        trackBar4.Value = 100;
        trackBar6.Value = 0;
        
        Control_ValueChanged(trackBar1, EventArgs.Empty);
        Control_ValueChanged(trackBar6, EventArgs.Empty);
        Control_ValueChanged(trackBar2, EventArgs.Empty);
        Control_ValueChanged(trackBar3, EventArgs.Empty);
        Control_ValueChanged(trackBar4, EventArgs.Empty);
        
        textBox1.Text = 0.ToString("0.###");
        textBox2.Text = 0.ToString("0.###");
        
        textBox3.Text = (imWidth/imDpiX * MmPerInch).ToString("0.0##");
        textBox4.Text = (imHeight/imDpiY * MmPerInch).ToString("0.0##");
        
        checkBox5.Checked = true;
        imAspectRatio = ((imWidth/imDpiX) / (imHeight/imDpiY));
        
        ImageBox1ZoomChanged(imageBox1, EventArgs.Empty);
        
        bWorkerFlags = -1;
        backgroundWorker1.RunWorkerAsync(null);
        
        return true;
    }
    
    private void CloseToolStripMenuItemClick(object sender, EventArgs e) {
        bWorkerFlags = BWorkerFlagExit;
        while (backgroundWorker1.IsBusy) {
            Application.DoEvents();
        }
        
        openFileDialog1.FileName = null;
        
        presetToolStripMenuItem.Enabled = false;
        
        reloadToolStripMenuItem.Enabled = false;
        exportToolStripMenuItem.Enabled = false;
        export2ToolStripMenuItem.Enabled = false;
        closeToolStripMenuItem.Enabled = false;
        saveToolStripMenuItem.Enabled = false;
        cropToolStripMenuItem.Enabled = false;
        sendToolStripMenuItem.Enabled = false;
        send2ToolStripMenuItem.Enabled = false;
        
        exportToolStripButton.Enabled = false;
        sendToolStripButton.Enabled = false;
        
        tableLayoutPanel2.Enabled = false;
        tableLayoutPanel13.Enabled = false;
        tableLayoutPanel4.Enabled = false;
        
        imageBox1.ImWidth = 0;
        imageBox1.ImHeight = 0;
        imageBox1.AdjustLayout();
        
        //imageBox1.ImIdx = -1;
        imageBox1.Invalidate(false);
        
        toolStripStatusLabel2.Visible = false;
        toolStripStatusLabel3.Visible = false;
        toolStripStatusLabel4.Visible = false;
        toolStripStatusLabel5.Visible = false;
        toolStripStatusLabel6.Visible = false;
        
        imSrc = null;
        
        toolStripStatusLabel1.Text = resources.GetString("Status_Ready", culture);
    }
    
    private unsafe void CropToolStripMenuItemClick(object sender, EventArgs e) {
        if (bWorkerIsBusy) {
            return;
        }
        
        int canvasWidth = imCanvas.Width;
        int canvasHeight = imCanvas.Height;
        byte cropColor = ImColorUltraWhite;
        
        int left = canvasWidth;
        int top = -1;
        int width = 0;
        int height = 0;
        
        BitmapData bDataSrc = new BitmapData();
        imSrc.LockBits(imCanvas, ImageLockMode.ReadOnly, PixelFormat.Format8bppIndexed, bDataSrc);
        
        IntPtr imScan0 = bDataSrc.Scan0;
        int imScanWidth = bDataSrc.Stride;
        
        byte* colorTable = imColorTable;
        
        for (int y = 0; y < canvasHeight; y++) {
            byte* src = (byte*)(imScan0 + y*imScanWidth);
            for (int x = 0; x < canvasWidth; x++) {
                if (colorTable[src[x]] == cropColor) {
                    continue;
                }
                
                if (top == -1) {
                    top = y;
                }
                if (left > x) {
                    left = x;
                }
                
                for (x = canvasWidth; x > 0; x--) {
                    if (colorTable[src[x-1]] == cropColor) {
                        continue;
                    }
                    if (width < x) {
                        width = x;
                    }
                    break;
                }
                height = y;
                
                break;
            }
        }
        
        imSrc.UnlockBits(bDataSrc);
        
        if (top == -1) {
            return;
        }
        
        width -= left;
        height -= (top-1);
        
        int newWidth = (imWidth - (int)((canvasWidth-width) * ((float)imWidth/canvasWidth)));
        int newHeight = (imHeight - (int)((canvasHeight-height) * ((float)imHeight/canvasHeight)));
        if (newWidth < MinImageSize || newHeight < MinImageSize) {
            return;
        }
        
        imWidth = newWidth;
        imHeight = newHeight;
        
        imCanvas.X += left;
        imCanvas.Y += top;
        imCanvas.Width = width;
        imCanvas.Height = height;
        
        imAspectRatio = ((imWidth/imDpiX) / (imHeight/imDpiY));
        
        textBox3.Text = (imWidth/imDpiX * MmPerInch).ToString("0.0##");
        textBox4.Text = (imHeight/imDpiY * MmPerInch).ToString("0.0##");
        
        bWorkerFlags = (BWorkerFlagDoWork|BWorkerFlagImageChanged|BWorkerFlagPreviewChanged);
    }
    
    private unsafe void RotateFlipButtonClick(object sender, EventArgs e) {
        if (bWorkerIsBusy) {
            return;
        }
        
        int srcWidth = imSrc.Width;
        int srcHeight = imSrc.Height;
        
        int width = imWidth;
        int height = imHeight;
        
        int scanWidth = ((width+3) / 4 * 4);
        
        RotateFlipType rotateFlipType = (RotateFlipType)((Control)sender).Tag;
        imSrc.RotateFlip(rotateFlipType);
        
        switch (rotateFlipType) {
            case RotateFlipType.RotateNoneFlipX:
                prevCanvas.X = imCanvas.X = (srcWidth - imCanvas.Right);
                Parallel.For(0, height, (i) => {
                    byte* dest = (byte*)(imResized + i*scanWidth);
                    for (int j = 0; j < (width/2); j++) {
                        byte dest_j = dest[j];
                        dest[j] = dest[width-j-1];
                        dest[width-j-1] = dest_j;
                    }
                });
                break;
            case RotateFlipType.RotateNoneFlipY:
                prevCanvas.Y = imCanvas.Y = (srcHeight - imCanvas.Bottom);
                Parallel.For(0, (height/2), (i) => {
                    byte* dest = (byte*)(imResized + i*scanWidth);
                    byte* dest2 = (dest + ((height-i*2-1) * scanWidth));
                    for (int j = 0; j < width; j++) {
                        byte dest_j = dest[j];
                        dest[j] = dest2[j];
                        dest2[j] = dest_j;
                    }
                });
                break;
            case RotateFlipType.RotateNoneFlipXY:
                prevCanvas.X = imCanvas.X = (srcWidth - imCanvas.Right);
                prevCanvas.Y = imCanvas.Y = (srcHeight - imCanvas.Bottom);
                Parallel.For(0, (height/2), (i) => {
                    byte* dest = (byte*)(imResized + i*scanWidth);
                    byte* dest2 = (dest + ((height-i*2-1) * scanWidth));
                    for (int j = 0; j < width; j++) {
                        byte dest_j = dest[j];
                        dest[j] = dest2[width-j-1];
                        dest2[width-j-1] = dest_j;
                    }
                });
                if ((height % 2) == 1) {
                    byte* dest = (byte*)(imResized + height/2*scanWidth);
                    for (int j = 0; j < (width/2); j++) {
                        byte dest_j = dest[j];
                        dest[j] = dest[width-j-1];
                        dest[width-j-1] = dest_j;
                    }
                }
                break;
            default:
                if (rotateFlipType == RotateFlipType.Rotate90FlipNone) {
                    imCanvas.X = (srcHeight - imCanvas.Bottom);
                    imCanvas.Y = prevCanvas.Left;
                } else {
                    imCanvas.X = imCanvas.Top;
                    imCanvas.Y = (srcWidth - prevCanvas.Right);
                }
                imCanvas.Width = imCanvas.Height;
                imCanvas.Height = prevCanvas.Width;
                
                imWidth = height;
                imHeight = width;
                
                imAspectRatio = ((imWidth/imDpiX) / (imHeight/imDpiY));
                
                prevInterpolation = InterpolationMode.Invalid;
                break;
        }
        
        textBox3.Text = (imWidth/imDpiX * MmPerInch).ToString("0.0##");
        textBox4.Text = (imHeight/imDpiY * MmPerInch).ToString("0.0##");
        
        bWorkerFlags = (BWorkerFlagDoWork|BWorkerFlagImageChanged|BWorkerFlagPreviewChanged);
    }
    
    private void ImageBox1ZoomChanged(object sender, EventArgs e) {
        toolStripStatusLabel2.Text = String.Format(culture, resources.GetString("Status_Zoom", culture), ((ImageBox)sender).Zoom);
    }
    
    private void BackgroundWorker1ProgressChanged(object sender, ProgressChangedEventArgs e) {
        toolStripStatusLabel1.Text = (string)e.UserState;
        if (e.ProgressPercentage != 100) {
            return;
        }
        
        toolStripStatusLabel3.Text = String.Format(culture, resources.GetString("Status_ImageSize", culture), imWidth, imHeight);
        toolStripStatusLabel4.Text = String.Format(culture, resources.GetString("Status_BurningArea", culture), ((gcPixelsCount/imDpiX)*(1F/imDpiY) * (MmPerInch*MmPerInch) / 100F));
        toolStripStatusLabel5.Text = String.Format(culture, resources.GetString("Status_YStepover", culture), (1F/imDpiY * MmPerInch));
        
        TimeSpan jobTime = TimeSpan.FromMilliseconds(gcJobTime);
        if (jobTime.TotalMinutes < 1D) {
            toolStripStatusLabel6.Text = String.Format(culture, resources.GetString("Status_ApproxRunTimeSeconds", culture), (int)jobTime.TotalSeconds);
        } else if (jobTime.TotalHours < 1D) {
            toolStripStatusLabel6.Text = String.Format(culture, resources.GetString("Status_ApproxRunTimeMinutes", culture), (int)jobTime.TotalMinutes);
        } else {
            if (jobTime.Minutes == 0) {
                toolStripStatusLabel6.Text = String.Format(culture, resources.GetString("Status_ApproxRunTimeHours", culture), (int)jobTime.TotalHours);
            } else {
                toolStripStatusLabel6.Text = String.Format(culture, resources.GetString("Status_ApproxRunTime", culture), (int)jobTime.TotalHours, jobTime.Minutes);
            }
        }
    }
    
    private unsafe void BackgroundWorker1DoWork(object sender, DoWorkEventArgs e) {
        int width = imWidth;
        int height = imHeight;
        
        int scanWidth = ((width+3) / 4 * 4);
        int scanWidth2 = ((width*3+3) / 4 * 4);
        
        imPreview = Marshal.AllocHGlobal((IntPtr)(height*scanWidth2));
        
        imResized = Marshal.AllocHGlobal((IntPtr)(height*scanWidth));
        imDest = Marshal.AllocHGlobal((IntPtr)(height*scanWidth));
        
        BitmapData bDataSrc = new BitmapData();
        imSrc.LockBits(imCanvas, ImageLockMode.ReadOnly, PixelFormat.Format8bppIndexed, bDataSrc);
        
        IntPtr imScan0 = bDataSrc.Scan0;
        int imScanWidth = bDataSrc.Stride;
        
        Parallel.For(0, height, (y) => {
            byte* src = (byte*)(imScan0 + y*imScanWidth);
            byte* dest = (byte*)(imResized + y*scanWidth);
            for (int x = 0; x < width; x++) {
                dest[x] = src[x];
            }
        });
        
        imSrc.UnlockBits(bDataSrc);
        
        imageBox1.ImIdx = 2;
        imageBox1.Invalidate(false);
        
        byte* colorTable = imColorTable;
        
        byte* ditherTable1 = ditherEmptyTable;
        byte* ditherTable2 = ditherEmptyTable;
        
        byte* ditherTable3 = ditherEmptyTable;
        byte* ditherTable4 = ditherEmptyTable;
        byte* ditherTable5 = ditherEmptyTable;
        byte* ditherTable6 = ditherEmptyTable;
        byte* ditherTable7 = ditherEmptyTable;
        
        byte* ditherTable8 = ditherEmptyTable;
        byte* ditherTable9 = ditherEmptyTable;
        byte* ditherTable10 = ditherEmptyTable;
        byte* ditherTable11 = ditherEmptyTable;
        byte* ditherTable12 = ditherEmptyTable;
        
        byte* sharpenTable = (byte*)Marshal.AllocHGlobal((IntPtr)262144);
        
        byte* lookupTable = (byte*)Marshal.AllocHGlobal((IntPtr)256);
        byte* lookupTable2 = (byte*)Marshal.AllocHGlobal((IntPtr)256);
        
        byte* lookupTable4 = (byte*)Marshal.AllocHGlobal((IntPtr)65536);
        byte* lookupTable5 = (byte*)Marshal.AllocHGlobal((IntPtr)65536);
        byte* lookupTable6 = (byte*)Marshal.AllocHGlobal((IntPtr)65536);
        
        float* F = (float*)Marshal.AllocHGlobal((IntPtr)1024);
        
        int* acctime = (int*)Marshal.AllocHGlobal((IntPtr)1024);
        int* accdist = (int*)Marshal.AllocHGlobal((IntPtr)1024);
        
        int prevWidth = width;
        int prevHeight = height;
        
        prevCanvas = new Rectangle(0, 0, width, height);
        prevInterpolation = imInterpolation;
        
        IntPtr imBackground = IntPtr.Zero;
        int textureWidth = 0;
        int textureHeight = 0;
        
        int prevDotColor = -1;
        
        int flags = 0;
        for (;;) {
            bWorkerWaitHandle.WaitOne(-1, false);
            
            bWorkerIsBusy = true;
            
            flags |= _bWorkerFlags;
            _bWorkerFlags = 0;
            
            if ((flags & BWorkerFlagExit) != 0) {
                break;
            }
            
            width = imWidth;
            height = imHeight;
            
            scanWidth = ((width+3) / 4 * 4);
            scanWidth2 = ((width*3+3) / 4 * 4);
            
            float dpiX = (imDpiX / MmPerInch);
            float dpiY = (imDpiY / MmPerInch);
            
            float left = imLeft;
            float top = imTop;
            
            int previewIdx = activePreview;
            
            MachineType machine = machineType;
            bool isNichromeBurner = (machine == MachineType.NichromeBurner);
            bool isImpactGraver = (machine == MachineType.ImpactGraver);
            
            bool _1bitPalette = im1bitPalette;
            
            if ((flags & BWorkerFlagImageChanged) != 0) {
                ImageDithering dithering = imDithering;
                int blackThreshold = imBlackThreshold;
                bool invertColors = imInvertColors;
                int brightness = imBrightness;
                float contrast = (imContrast*imContrast);
                float gamma = imGamma;
                int sharpenForce = imSharpenForce;
                InterpolationMode interpolation = imInterpolation;
                
                if (width != prevWidth || height != prevHeight || imCanvas != prevCanvas || interpolation != prevInterpolation) {
                    ((BackgroundWorker)sender).ReportProgress(-1, resources.GetString("Status_ResizingImage", culture));
                    
                    prevInterpolation = InterpolationMode.Invalid;
                    
                    prevWidth = -1;
                    prevHeight = -1;
                    
                    lock (imResizeLock) {
                        Marshal.FreeHGlobal(imPreview);
                        imPreview = Marshal.AllocHGlobal((IntPtr)(height*scanWidth2));
                        
                        Marshal.FreeHGlobal(imResized);
                        imResized = Marshal.AllocHGlobal((IntPtr)(height*scanWidth));
                        
                        Marshal.FreeHGlobal(imDest);
                        imDest = Marshal.AllocHGlobal((IntPtr)(height*scanWidth));
                        
                        imageBox1.ImWidth = width;
                        imageBox1.ImHeight = height;
                        imageBox1.AdjustLayout();
                    }
                    if (_bWorkerFlags != 0) {
                        continue;
                    }
                    
                    using (Graphics g = Graphics.FromImage(new Bitmap(width, height, scanWidth2, PixelFormat.Format24bppRgb, imPreview))) {
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.InterpolationMode = interpolation;
                        g.DrawImage(imSrc, new Rectangle(0, 0, width, height), imCanvas.X, imCanvas.Y, imCanvas.Width, imCanvas.Height, GraphicsUnit.Pixel, null, (callbackdata) => (_bWorkerFlags != 0), IntPtr.Zero);
                    }
                    if (_bWorkerFlags != 0) {
                        continue;
                    }
                    
                    Parallel.For(0, height, (y, loop) => {
                        byte* src = (byte*)(imPreview + y*scanWidth2);
                        byte* dest = (byte*)(imResized + y*scanWidth);
                        
                        for (int x = 0; x < width; x++) {
                            dest[x] = (byte)(0.299F*src[x*3+2] + 0.587F*src[x*3+1] + 0.114F*src[x*3+0]);
                        }
                        if (_bWorkerFlags != 0) {
                            loop.Stop();
                        }
                    });
                    if (_bWorkerFlags != 0) {
                        continue;
                    }
                    
                    prevInterpolation = interpolation;
                    
                    prevCanvas.X = imCanvas.X;
                    prevCanvas.Y = imCanvas.Y;
                    prevCanvas.Width = imCanvas.Width;
                    prevCanvas.Height = imCanvas.Height;
                    
                    prevWidth = width;
                    prevHeight = height;
                }
                
                ((BackgroundWorker)sender).ReportProgress(-1, resources.GetString("Status_ApplyingFilters", culture));
                
                Parallel.For(0, 256, (i) => {
                    int nPixel = i;
                    
                    nPixel = (nPixel + brightness);
                    if (nPixel > 255) {
                        nPixel = 255;
                    } else if (nPixel < 0) {
                        nPixel = 0;
                    }
                    
                    nPixel = (int)(((nPixel/255F - 0.5F) * contrast + 0.5F) * 255F);
                    if (nPixel > 255) {
                        nPixel = 255;
                    } else if (nPixel < 0) {
                        nPixel = 0;
                    }
                    
                    nPixel = (int)(255D * Math.Pow(nPixel/255D, 1D/gamma) + 0.5D);
                    if (nPixel > 255) {
                        nPixel = 255;
                    }
                    
                    if (!_1bitPalette) {
                        if (invertColors) {
                            nPixel ^= 255;
                        }
                        nPixel = colorTable[nPixel];
                    } else {
                        if (dithering == ImageDithering.Threshold) {
                            if (invertColors) {
                                nPixel ^= 255;
                            }
                            if (nPixel <= blackThreshold) {
                                nPixel = 0;
                            } else {
                                nPixel = 255;
                            }
                        } else {
                            if (!invertColors) {
                                lookupTable2[i] = (byte)(i/128 * 255);
                            } else {
                                lookupTable2[i] = (byte)(255 + i/128);
                            }
                        }
                    }
                    
                    lookupTable[i] = (byte)nPixel;
                });
                if (_bWorkerFlags != 0) {
                    continue;
                }
                
                if (sharpenForce == 0) {
                    
                    Parallel.For(0, height, (y, loop) => {
                        byte* src = (byte*)(imResized + y*scanWidth);
                        byte* dest = (byte*)(imDest + y*scanWidth);
                        
                        for (int x = 0; x < width; x++) {
                            dest[x] = lookupTable[src[x]];
                        }
                        if (_bWorkerFlags != 0) {
                            loop.Stop();
                        }
                    });
                    if (_bWorkerFlags != 0) {
                        continue;
                    }
                    
                } else {
                    
                    float m = (4F*sharpenForce/10F + 1F);
                    float m2 = (-1F*sharpenForce/10F);
                    
                    Parallel.For(0, 256, (i, loop) => {
                        for (int j = 0; j < 1021; j++) {
                            int b = (int)(i * m + j * m2);
                            if (b > 255) {
                                b = 255;
                            } else if (b < 0) {
                                b = 0;
                            }
                            
                            sharpenTable[i*1024+j] = lookupTable[b];
                        }
                        if (_bWorkerFlags != 0) {
                            loop.Stop();
                        }
                    });
                    if (_bWorkerFlags != 0) {
                        continue;
                    }
                    
                    Parallel.Invoke(new Action[] { () => {
                        byte* src = (byte*)imResized;
                        byte* dest = (byte*)imDest;
                        for (int x = 0; x < width; x++) {
                            dest[x] = lookupTable[src[x]];
                        }
                    }, () => {
                        byte* src = (byte*)(imResized + height*scanWidth - scanWidth);
                        byte* dest = (byte*)(imDest + height*scanWidth - scanWidth);
                        for (int x = 0; x < width; x++) {
                            dest[x] = lookupTable[src[x]];
                        }
                    }, });
                    if (_bWorkerFlags != 0) {
                        continue;
                    }
                    
                    int width2 = (width-1);
                    Parallel.For(1, (height-1), (y, loop) => {
                        byte* src = (byte*)(imResized + y*scanWidth);
                        byte* dest = (byte*)(imDest + y*scanWidth);
                        
                        dest[0] = lookupTable[src[0]];
                        dest[width2] = lookupTable[src[width2]];
                        
                        for (int x = 1; x < width2; x++) {
                            dest[x] = sharpenTable[src[x]*1024 + src[x-scanWidth]+src[x-1]+src[x+1]+src[x+scanWidth]];
                        }
                        if (_bWorkerFlags != 0) {
                            loop.Stop();
                        }
                    });
                    if (_bWorkerFlags != 0) {
                        continue;
                    }
                    
                }
                
                if (_1bitPalette) {
                    if (dithering != ImageDithering.Threshold) {
                        ((BackgroundWorker)sender).ReportProgress(-1, resources.GetString("Status_ApplyingDithering", culture));
                        
                        bool threeRowDithering = true;
                        switch (dithering) {
                            case ImageDithering.FloydSteinberg:
                                ditherTable1 = ditherTable_7_16;
                                ditherTable2 = ditherEmptyTable;
                                ditherTable3 = ditherEmptyTable;
                                ditherTable4 = ditherTable_3_16;
                                ditherTable5 = ditherTable_5_16;
                                ditherTable6 = ditherTable_1_16;
                                ditherTable7 = ditherEmptyTable;
                                ditherTable8 = ditherEmptyTable;
                                ditherTable9 = ditherEmptyTable;
                                ditherTable10 = ditherEmptyTable;
                                ditherTable11 = ditherEmptyTable;
                                ditherTable12 = ditherEmptyTable;
                                threeRowDithering = false;
                                break;
                            case ImageDithering.Jarvis:
                                ditherTable1 = ditherTable_7_48;
                                ditherTable2 = ditherTable_5_48;
                                ditherTable3 = ditherTable_3_48;
                                ditherTable4 = ditherTable_5_48;
                                ditherTable5 = ditherTable_7_48;
                                ditherTable6 = ditherTable_5_48;
                                ditherTable7 = ditherTable_3_48;
                                ditherTable8 = ditherTable_1_48;
                                ditherTable9 = ditherTable_3_48;
                                ditherTable10 = ditherTable_5_48;
                                ditherTable11 = ditherTable_3_48;
                                ditherTable12 = ditherTable_1_48;
                                break;
                            case ImageDithering.Stucki:
                                ditherTable1 = ditherTable_8_42;
                                ditherTable2 = ditherTable_4_42;
                                ditherTable3 = ditherTable_2_42;
                                ditherTable4 = ditherTable_4_42;
                                ditherTable5 = ditherTable_8_42;
                                ditherTable6 = ditherTable_4_42;
                                ditherTable7 = ditherTable_2_42;
                                ditherTable8 = ditherTable_1_42;
                                ditherTable9 = ditherTable_2_42;
                                ditherTable10 = ditherTable_4_42;
                                ditherTable11 = ditherTable_2_42;
                                ditherTable12 = ditherTable_1_42;
                                break;
                            case ImageDithering.Atkinson:
                                ditherTable1 = ditherTable_1_8;
                                ditherTable2 = ditherTable_1_8;
                                ditherTable3 = ditherEmptyTable;
                                ditherTable4 = ditherTable_1_8;
                                ditherTable5 = ditherTable_1_8;
                                ditherTable6 = ditherTable_1_8;
                                ditherTable7 = ditherEmptyTable;
                                ditherTable8 = ditherEmptyTable;
                                ditherTable9 = ditherEmptyTable;
                                ditherTable10 = ditherTable_1_8;
                                ditherTable11 = ditherEmptyTable;
                                ditherTable12 = ditherEmptyTable;
                                break;
                            case ImageDithering.Burkes:
                                ditherTable1 = ditherTable_8_32;
                                ditherTable2 = ditherTable_4_32;
                                ditherTable3 = ditherTable_2_32;
                                ditherTable4 = ditherTable_4_32;
                                ditherTable5 = ditherTable_8_32;
                                ditherTable6 = ditherTable_4_32;
                                ditherTable7 = ditherTable_2_32;
                                ditherTable8 = ditherEmptyTable;
                                ditherTable9 = ditherEmptyTable;
                                ditherTable10 = ditherEmptyTable;
                                ditherTable11 = ditherEmptyTable;
                                ditherTable12 = ditherEmptyTable;
                                threeRowDithering = false;
                                break;
                            case ImageDithering.Sierra:
                                ditherTable1 = ditherTable_5_32;
                                ditherTable2 = ditherTable_3_32;
                                ditherTable3 = ditherTable_2_32;
                                ditherTable4 = ditherTable_4_32;
                                ditherTable5 = ditherTable_5_32;
                                ditherTable6 = ditherTable_4_32;
                                ditherTable7 = ditherTable_2_32;
                                ditherTable8 = ditherEmptyTable;
                                ditherTable9 = ditherTable_2_32;
                                ditherTable10 = ditherTable_3_32;
                                ditherTable11 = ditherTable_2_32;
                                ditherTable12 = ditherEmptyTable;
                                break;
                        }
                        
                        int width2 = (width-2);
                        
                        for (int y = 0; y < height; y++) {
                            byte* dest = (byte*)(imDest + y*scanWidth);
                            byte* dest2 = (dest + scanWidth);
                            byte* dest3 = (dest2 + scanWidth);
                            
                            int j;
                            if (y == (height-1)) {
                                
                                j = dest[0];
                                dest[0] = lookupTable2[dest[0]];
                                dest[1] = ditherTable1[dest[1]*256+j];
                                dest[2] = ditherTable2[dest[2]*256+j];
                                
                                j = dest[1];
                                dest[1] = lookupTable2[dest[1]];
                                dest[2] = ditherTable1[dest[2]*256+j];
                                dest[3] = ditherTable2[dest[3]*256+j];
                                
                                for (int x = 2; x < width2; x++) {
                                    j = dest[x];
                                    dest[x] = lookupTable2[dest[x]];
                                    dest[x+1] = ditherTable1[dest[x+1]*256+j];
                                    dest[x+2] = ditherTable2[dest[x+2]*256+j];
                                }
                                
                                j = dest[width2];
                                dest[width2] = lookupTable2[dest[width2]];
                                dest[width2+1] = ditherTable1[dest[width2+1]*256+j];
                                
                                //j = dest[width2+1];
                                dest[width2+1] = lookupTable2[dest[width2+1]];
                                
                            } else if (y == (height-2) || !threeRowDithering) {
                                
                                j = dest[0];
                                dest[0] = lookupTable2[dest[0]];
                                dest[1] = ditherTable1[dest[1]*256+j];
                                dest[2] = ditherTable2[dest[2]*256+j];
                                dest2[0] = ditherTable5[dest2[0]*256+j];
                                dest2[1] = ditherTable6[dest2[1]*256+j];
                                dest2[2] = ditherTable7[dest2[2]*256+j];
                                
                                j = dest[1];
                                dest[1] = lookupTable2[dest[1]];
                                dest[2] = ditherTable1[dest[2]*256+j];
                                dest[3] = ditherTable2[dest[3]*256+j];
                                dest2[0] = ditherTable4[dest2[0]*256+j];
                                dest2[1] = ditherTable5[dest2[1]*256+j];
                                dest2[2] = ditherTable6[dest2[2]*256+j];
                                dest2[3] = ditherTable7[dest2[3]*256+j];
                                
                                for (int x = 2; x < width2; x++) {
                                    j = dest[x];
                                    dest[x] = lookupTable2[dest[x]];
                                    dest[x+1] = ditherTable1[dest[x+1]*256+j];
                                    dest[x+2] = ditherTable2[dest[x+2]*256+j];
                                    dest2[x-2] = ditherTable3[dest2[x-2]*256+j];
                                    dest2[x-1] = ditherTable4[dest2[x-1]*256+j];
                                    dest2[x] = ditherTable5[dest2[x]*256+j];
                                    dest2[x+1] = ditherTable6[dest2[x+1]*256+j];
                                    dest2[x+2] = ditherTable7[dest2[x+2]*256+j];
                                }
                                
                                j = dest[width2];
                                dest[width2] = lookupTable2[dest[width2]];
                                dest[width2+1] = ditherTable1[dest[width2+1]*256+j];
                                dest2[width2-2] = ditherTable3[dest2[width2-2]*256+j];
                                dest2[width2-1] = ditherTable4[dest2[width2-1]*256+j];
                                dest2[width2] = ditherTable5[dest2[width2]*256+j];
                                dest2[width2+1] = ditherTable6[dest2[width2+1]*256+j];
                                
                                j = dest[width2+1];
                                dest[width2+1] = lookupTable2[dest[width2+1]];
                                dest2[width2-1] = ditherTable3[dest2[width2-1]*256+j];
                                dest2[width2] = ditherTable4[dest2[width2]*256+j];
                                dest2[width2+1] = ditherTable5[dest2[width2+1]*256+j];
                                
                            } else {
                                
                                j = dest[0];
                                dest[0] = lookupTable2[dest[0]];
                                dest[1] = ditherTable1[dest[1]*256+j];
                                dest[2] = ditherTable2[dest[2]*256+j];
                                dest2[0] = ditherTable5[dest2[0]*256+j];
                                dest2[1] = ditherTable6[dest2[1]*256+j];
                                dest2[2] = ditherTable7[dest2[2]*256+j];
                                dest3[0] = ditherTable10[dest3[0]*256+j];
                                dest3[1] = ditherTable11[dest3[1]*256+j];
                                dest3[2] = ditherTable12[dest3[2]*256+j];
                                
                                j = dest[1];
                                dest[1] = lookupTable2[dest[1]];
                                dest[2] = ditherTable1[dest[2]*256+j];
                                dest[3] = ditherTable2[dest[3]*256+j];
                                dest2[0] = ditherTable4[dest2[0]*256+j];
                                dest2[1] = ditherTable5[dest2[1]*256+j];
                                dest2[2] = ditherTable6[dest2[2]*256+j];
                                dest2[3] = ditherTable7[dest2[3]*256+j];
                                dest3[0] = ditherTable9[dest3[0]*256+j];
                                dest3[1] = ditherTable10[dest3[1]*256+j];
                                dest3[2] = ditherTable11[dest3[2]*256+j];
                                dest3[3] = ditherTable12[dest3[3]*256+j];
                                
                                for (int x = 2; x < width2; x++) {
                                    j = dest[x];
                                    dest[x] = lookupTable2[dest[x]];
                                    dest[x+1] = ditherTable1[dest[x+1]*256+j];
                                    dest[x+2] = ditherTable2[dest[x+2]*256+j];
                                    dest2[x-2] = ditherTable3[dest2[x-2]*256+j];
                                    dest2[x-1] = ditherTable4[dest2[x-1]*256+j];
                                    dest2[x] = ditherTable5[dest2[x]*256+j];
                                    dest2[x+1] = ditherTable6[dest2[x+1]*256+j];
                                    dest2[x+2] = ditherTable7[dest2[x+2]*256+j];
                                    dest3[x-2] = ditherTable8[dest3[x-2]*256+j];
                                    dest3[x-1] = ditherTable9[dest3[x-1]*256+j];
                                    dest3[x] = ditherTable10[dest3[x]*256+j];
                                    dest3[x+1] = ditherTable11[dest3[x+1]*256+j];
                                    dest3[x+2] = ditherTable12[dest3[x+2]*256+j];
                                }
                                
                                j = dest[width2];
                                dest[width2] = lookupTable2[dest[width2]];
                                dest[width2+1] = ditherTable1[dest[width2+1]*256+j];
                                dest2[width2-2] = ditherTable3[dest2[width2-2]*256+j];
                                dest2[width2-1] = ditherTable4[dest2[width2-1]*256+j];
                                dest2[width2] = ditherTable5[dest2[width2]*256+j];
                                dest2[width2+1] = ditherTable6[dest2[width2+1]*256+j];
                                dest3[width2-2] = ditherTable8[dest3[width2-2]*256+j];
                                dest3[width2-1] = ditherTable9[dest3[width2-1]*256+j];
                                dest3[width2] = ditherTable10[dest3[width2]*256+j];
                                dest3[width2+1] = ditherTable11[dest3[width2+1]*256+j];
                                
                                j = dest[width2+1];
                                dest[width2+1] = lookupTable2[dest[width2+1]];
                                dest2[width2-1] = ditherTable3[dest2[width2-1]*256+j];
                                dest2[width2] = ditherTable4[dest2[width2]*256+j];
                                dest2[width2+1] = ditherTable5[dest2[width2+1]*256+j];
                                dest3[width2-1] = ditherTable8[dest3[width2-1]*256+j];
                                dest3[width2] = ditherTable9[dest3[width2]*256+j];
                                dest3[width2+1] = ditherTable10[dest3[width2+1]*256+j];
                                
                            }
                            if (_bWorkerFlags != 0) {
                                break;
                            }
                        }
                        if (_bWorkerFlags != 0) {
                            continue;
                        }
                    }
                }
                
                flags &= ~BWorkerFlagImageChanged;
            }
            
            if ((flags & BWorkerFlagBackgroundChanged) != 0) {
                if (previewIdx != -1) {
                    ((BackgroundWorker)sender).ReportProgress(-1, resources.GetString("Status_PreparingTexture", culture));
                    
                    string background = previewBackground[previewIdx];
                    if (background == null) {
                        int bgColor = previewBgColor[previewIdx].ToArgb();
                        
                        Marshal.FreeHGlobal(imBackground);
                        imBackground = Marshal.AllocHGlobal((IntPtr)196608);
                        
                        textureWidth = 256;
                        textureHeight = 256;
                        
                        byte bgR = (byte)(bgColor >> 16);
                        byte bgG = (byte)(bgColor >> 8);
                        byte bgB = (byte)(bgColor >> 0);
                        
                        Parallel.For(0, 256, (y, loop) => {
                            byte* dest = (byte*)(imBackground + y*768);
                            for (int x = 0; x < 256; x++) {
                                dest[x*3+2] = bgR;
                                dest[x*3+1] = bgG;
                                dest[x*3+0] = bgB;
                            }
                            if (_bWorkerFlags != 0) {
                                loop.Stop();
                            }
                        });
                        if (_bWorkerFlags != 0) {
                            continue;
                        }
                    } else {
                        int textureScale = previewSize[previewIdx];
                        using (Bitmap im = (Bitmap)Image.FromStream(bitmapTextures[background], false, true)) {
                            int W = im.Width;
                            int H = im.Height;
                            
                            textureWidth = (W * textureScale / 100);
                            textureHeight = (H * textureScale / 100);
                            
                            Marshal.FreeHGlobal(imBackground);
                            imBackground = Marshal.AllocHGlobal((IntPtr)(textureHeight*textureWidth*3));
                            
                            im.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb, bDataSrc);
                            
                            imScan0 = bDataSrc.Scan0;
                            imScanWidth = bDataSrc.Stride;
                            
                            float resizeFactorX = ((float)W / textureWidth);
                            float resizeFactorY = ((float)H / textureHeight);
                            
                            Parallel.For(0, textureHeight, (y, loop) => {
                                byte* dest = (byte*)(imBackground + y*textureWidth*3);
                                
                                int floorY = (int)(y*resizeFactorY);
                                int ceilY = (floorY+1);
                                if (ceilY >= H) {
                                    ceilY = (H-1);
                                }
                                float fractionY = (y*resizeFactorY - floorY);
                                float invFractionY = (1F - fractionY);
                                
                                byte* srcFloor = (byte*)(imScan0 + floorY*imScanWidth);
                                byte* srcCeil = (byte*)(imScan0 + ceilY*imScanWidth);
                                
                                for (int x = 0; x < textureWidth; x++) {
                                    int floorX = (int)(x*resizeFactorX);
                                    int ceilX = (floorX+1);
                                    if (ceilX >= W) {
                                        ceilX = (W-1);
                                    }
                                    float fractionX = (x*resizeFactorX - floorX);
                                    float invFractionX = (1F - fractionX);
                                    
                                    float b1, b2;
                                    
                                    b1 = (invFractionX*srcFloor[floorX*3+2] + fractionX*srcFloor[ceilX*3+2]);
                                    b2 = (invFractionX*srcCeil[floorX*3+2] + fractionX*srcCeil[ceilX*3+2]);
                                    dest[x*3+2] = (byte)(invFractionY*b1 + fractionY*b2);
                                    
                                    b1 = (invFractionX*srcFloor[floorX*3+1] + fractionX*srcFloor[ceilX*3+1]);
                                    b2 = (invFractionX*srcCeil[floorX*3+1] + fractionX*srcCeil[ceilX*3+1]);
                                    dest[x*3+1] = (byte)(invFractionY*b1 + fractionY*b2);
                                    
                                    b1 = (invFractionX*srcFloor[floorX*3+0] + fractionX*srcFloor[ceilX*3+0]);
                                    b2 = (invFractionX*srcCeil[floorX*3+0] + fractionX*srcCeil[ceilX*3+0]);
                                    dest[x*3+0] = (byte)(invFractionY*b1 + fractionY*b2);
                                }
                                if (_bWorkerFlags != 0) {
                                    loop.Stop();
                                }
                            });
                            
                            im.UnlockBits(bDataSrc);
                            if (_bWorkerFlags != 0) {
                                continue;
                            }
                        }
                    }
                    
                    imageBox1.ImIdx = 1;
                } else {
                    Marshal.FreeHGlobal(imBackground);
                    imBackground = IntPtr.Zero;
                    
                    textureWidth = 0;
                    textureHeight = 0;
                    
                    imageBox1.ImIdx = 0;
                }
                
                flags &= ~BWorkerFlagBackgroundChanged;
            }
            
            if ((flags & BWorkerFlagPreviewChanged) != 0) {
                if (previewIdx != -1) {
                    ((BackgroundWorker)sender).ReportProgress(-1, resources.GetString("Status_GeneratingPreview", culture));
                    
                    int textureWrapMode = (int)previewWrapMode[previewIdx];
                    
                    int dotColor = (previewDotColor[previewIdx].ToArgb() & 0x00FFFFFF);
                    if (dotColor != prevDotColor) {
                        byte dotR = (byte)(dotColor >> 16);
                        byte dotG = (byte)(dotColor >> 8);
                        byte dotB = (byte)(dotColor >> 0);
                        
                        prevDotColor = -1;
                        
                        Parallel.For(0, 256, (i) => {
                            for (int j = 0; j <= ImColorWhite; j += ImColorStep) {
                                float invAlpha = (j / 255F);
                                float alpha = (1F - invAlpha);
                                lookupTable4[i*256+j] = (byte)(alpha*dotR + invAlpha*i);
                                lookupTable5[i*256+j] = (byte)(alpha*dotG + invAlpha*i);
                                lookupTable6[i*256+j] = (byte)(alpha*dotB + invAlpha*i);
                            }
                            lookupTable4[i*256+255] = (byte)i;
                            lookupTable5[i*256+255] = (byte)i;
                            lookupTable6[i*256+255] = (byte)i;
                        });
                        if (_bWorkerFlags != 0) {
                            continue;
                        }
                        
                        prevDotColor = dotColor;
                    }
                    
                    Parallel.For(0, height, (y, loop) => {
                        byte* src = (byte*)(imDest + y*scanWidth);
                        byte* dest = (byte*)(imPreview + y*scanWidth2);
                        
                        byte* src2;
                        if ((textureWrapMode & 2) != 0) {
                            if (((y / textureHeight) % 2) == 1) {
                                src2 = (byte*)(imBackground + (textureHeight - y + y/textureHeight*textureHeight - 1)*textureWidth*3);
                            } else {
                                src2 = (byte*)(imBackground + (y - y/textureHeight*textureHeight)*textureWidth*3);
                            }
                        } else {
                            src2 = (byte*)(imBackground + (y - y/textureHeight*textureHeight)*textureWidth*3);
                        }
                        
                        int j = textureWidth;
                        for (int x = 0; x < width;) {
                            if ((x + j) > width) {
                                j = (width-x);
                            }
                            for (int i = 0; i < j; x++, i++) {
                                dest[x*3+2] = lookupTable4[src2[i*3+2]*256 + src[x]];
                                dest[x*3+1] = lookupTable5[src2[i*3+1]*256 + src[x]];
                                dest[x*3+0] = lookupTable6[src2[i*3+0]*256 + src[x]];
                            }
                            
                            if ((textureWrapMode & 1) == 0) {
                                continue;
                            }
                            
                            if ((x + j) > width) {
                                j = (width-x);
                            }
                            for (int i = (textureWidth-1); i >= (textureWidth-j); x++, i--) {
                                dest[x*3+2] = lookupTable4[src2[i*3+2]*256 + src[x]];
                                dest[x*3+1] = lookupTable5[src2[i*3+1]*256 + src[x]];
                                dest[x*3+0] = lookupTable6[src2[i*3+0]*256 + src[x]];
                            }
                        }
                        if (_bWorkerFlags != 0) {
                            loop.Stop();
                        }
                    });
                    if (_bWorkerFlags != 0) {
                        continue;
                    }
                }
                
                imageBox1.StartPointX = (-left / (width/dpiX));
                imageBox1.StartPointY = (-top / (height/dpiY));
                imageBox1.Invalidate(false);
                
                flags &= ~(BWorkerFlagPreviewChanged|BWorkerFlagRedrawOrigin);
            }
            
            if ((flags & BWorkerFlagDoWork) != 0) {
                ((BackgroundWorker)sender).ReportProgress(-1, resources.GetString("Status_CalculatingWorkTime", culture));
                
                int pixelsCount = 0;
                int jobTime = 0;
                
                float rapidSpeed = (gcG0Speed/60000F);
                
                if (_1bitPalette) {
                    F[0] = (gcSpeed/60000F);
                } else {
                    if (isImpactGraver) {
                        for (int i = 0; i <= ImColorWhite; i += ImColorStep) {
                            F[i] = (gcSpeed/60000F);
                        }
                    } else {
                        float p1 = (gcSpeedGraph[1]/60000F);
                        float p7 = (gcSpeedGraph[0]/60000F);
                        
                        float p2 = (3*ImColorWhite*(1F-gcSpeedGraph[2]));
                        float p3 = (3F*(p1 - (p1-p7)*gcSpeedGraph[3]));
                        float p4 = (3*ImColorWhite*(1F-gcSpeedGraph[4]));
                        float p5 = (3F*(p1 - (p1-p7)*gcSpeedGraph[5]));
                        
                        int color = (ImColorWhite-ImColorStep);
                        
                        float x0 = ImColorWhite;
                        float y0 = p1;
                        
                        F[ImColorWhite] = p1;
                        for (int i = 1; i < BezierSegmentsCount; i++) {
                            float t = ((float)i/BezierSegmentsCount);
                            float inv_t = (1F - t);
                            
                            float x1 = (inv_t*inv_t*inv_t*ImColorWhite + inv_t*inv_t*t*p2 + t*t*inv_t*p4);
                            float y1 = (inv_t*inv_t*inv_t*p1 + inv_t*inv_t*t*p3 + t*t*inv_t*p5 + t*t*t*p7);
                            if (x1 < color) {
                                F[color] = (y0 + (color-x0) * (y1-y0) / (x1-x0));
                                
                                color -= ImColorStep;
                                if (color == 0) {
                                    break;
                                }
                            }
                            
                            x0 = x1;
                            y0 = y1;
                        }
                        F[0] = p7;
                    }
                }
                F[255] = (gcWhiteSpeed/60000F);
                
                bool bidirectional = gcBidirectional;
                int numberOfPasses = gcNumberOfPasses;
                
                if (isNichromeBurner) {
                    CleaningStrategy cleaningStrategy = gcCleaningStrategy;
                    float stripWidth = gcStripWidth;
                    float stripSpeed = (gcStripSpeed/60000F);
                    float cleaningFieldWidth = gcCleaningFieldWidth;
                    float cleaningFieldSpeed = (gcCleaningFieldSpeed/60000F);
                    int numberOfCleaningCycles = gcNumberOfCleaningCycles;
                    
                    if (cleaningFieldWidth > stripWidth) {
                        cleaningFieldWidth = stripWidth;
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
                    
                    int n = ((height*numberOfPasses-1) / cleaningRowsCount);
                    jobTime += (int)(((stripWidth-cleaningFieldWidth)/stripSpeed + cleaningFieldWidth*2*numberOfCleaningCycles/cleaningFieldSpeed) * n);
                    
                    if (!bidirectional) {
                        jobTime += (int)(width*height*numberOfPasses/dpiX / rapidSpeed);
                    } else {
                        if ((cleaningRowsCount % 2) == 1) {
                            jobTime += (int)(width*n/dpiX / rapidSpeed);
                        }
                        if (((height*numberOfPasses - cleaningRowsCount*n) % 2) == 1) {
                            jobTime += (int)(width/dpiX / rapidSpeed);
                        }
                    }
                    
                    Parallel.For<int[]>(0, height, () => new int[256], (y, loop, j) => {
                        byte* dest = (byte*)(imDest + y*scanWidth);
                        
                        for (int x = 0; x < width; x++) {
                            ++j[dest[x]];
                        }
                        if (_bWorkerFlags != 0) {
                            loop.Stop();
                        }
                        
                        return j;
                    }, (j) => {
                        if (_bWorkerFlags != 0) {
                            return;
                        }
                        
                        int t = 0;
                        
                        t += (int)(j[0]/dpiX / F[0]);
                        if (!_1bitPalette) {
                            for (int i = ImColorStep; i <= ImColorWhite; i += ImColorStep) {
                                t += (int)(j[i]/dpiX / F[i]);
                            }
                        }
                        t += (int)(j[255]/dpiX / F[255]);
                        
                        t *= numberOfPasses;
                        Interlocked.Add(ref jobTime, t);
                    });
                    if (_bWorkerFlags != 0) {
                        continue;
                    }
                    
                    pixelsCount = (width*height);
                } else {
                    bool skipWhite = gcSkipWhite;
                    float whiteDistance = gcWhiteDistance;
                    
                    float accel = (gcAccel/1000000F);
                    
                    acctime[0] = (int)(F[0] / accel);
                    accdist[0] = (int)(acctime[0]*F[0]/2F * dpiX);
                    if (!_1bitPalette) {
                        for (int i = ImColorStep; i <= ImColorWhite; i += ImColorStep) {
                            acctime[i] = (int)(F[i] / accel);
                            accdist[i] = (int)(acctime[i]*F[i]/2F * dpiX);
                        }
                    }
                    
                    acctime[255] = (int)(rapidSpeed / accel);
                    accdist[255] = (int)(acctime[255]*rapidSpeed/2F * dpiX);
                    
                    int[] X2 = new int[height];
                    int[] X = new int[height];
                    
                    Parallel.For<int[]>(0, height, () => new int[258], (y, loop, j) => {
                        byte* dest = (byte*)(imDest + y*scanWidth);
                        
                        int width2 = width;
                        int x = 0;
                        
                        for (; x < width; x++) {
                            if (dest[x] != ImColorUltraWhite) {
                                break;
                            }
                        }
                        if (x >= width) {
                            goto SKIP_LINE;
                        }
                        for (; width2 > 0; width2--) {
                            if (dest[width2-1] != ImColorUltraWhite) {
                                break;
                            }
                        }
                        
                        X2[y] = (width2 + accdist[dest[width2-1]]);
                        X[y] = (x - accdist[dest[x]]);
                        
                        byte prevPixel = dest[x];
                        int x2 = -1;
                        
                        j[257] += acctime[prevPixel];
                        for (; x < width2; x++) {
                            byte pixel = dest[x];
                            if (pixel != ImColorUltraWhite) {
                                ++j[256];
                                if (prevPixel == ImColorUltraWhite) {
                                    int n = (x-x2);
                                    if (skipWhite && ((n/dpiX) >= whiteDistance)) {
                                        j[255] += n;
                                    } else {
                                        byte pixel2 = dest[x2-1];
                                        if (F[pixel] > F[pixel2]) {
                                            j[pixel] += n;
                                        } else {
                                            j[pixel2] += n;
                                        }
                                    }
                                }
                                ++j[pixel];
                            } else {
                                if (prevPixel != ImColorUltraWhite) {
                                    x2 = x;
                                }
                            }
                            prevPixel = pixel;
                        }
                        j[257] += acctime[prevPixel];
                        
                        SKIP_LINE:
                        if (_bWorkerFlags != 0) {
                            loop.Stop();
                        }
                        
                        return j;
                    }, (j) => {
                        if (_bWorkerFlags != 0) {
                            return;
                        }
                        
                        j[257] += (int)(j[0]/dpiX / F[0]);
                        if (!_1bitPalette) {
                            for (int i = ImColorStep; i <= ImColorWhite; i += ImColorStep) {
                                j[257] += (int)(j[i]/dpiX / F[i]);
                            }
                        }
                        j[257] += (int)(j[255]/dpiX / F[255]);
                        
                        j[257] *= numberOfPasses;
                        Interlocked.Add(ref pixelsCount, j[256]);
                        Interlocked.Add(ref jobTime, j[257]);
                    });
                    if (_bWorkerFlags != 0) {
                        continue;
                    }
                    
                    bool forward = true;
                    int prevX = 0;
                    
                    for (int y = 0; y < height; y++) {
                        if (X[y] == X2[y]) {
                            continue;
                        }
                        
                        int rapidTravel;
                        if (forward) {
                            rapidTravel = (X2[y] - prevX);
                        } else {
                            rapidTravel = (X[y] - prevX);
                        }
                        
                        if (rapidTravel < 0) {
                            rapidTravel = -rapidTravel;
                        }
                        
                        if (rapidTravel >= (2*accdist[255])) {
                            jobTime += (2*acctime[255] + (int)((rapidTravel-2*accdist[255])/dpiX / rapidSpeed));
                        } else {
                            jobTime += (int)(rapidTravel/dpiX / rapidSpeed);
                        }
                        
                        if (!bidirectional) {
                            int rapidTravel2 = (X2[y] - X[y]);
                            if (rapidTravel2 >= (2*accdist[255])) {
                                jobTime += (int)((2*acctime[255] + (rapidTravel2-2*accdist[255])/dpiX / rapidSpeed) * (numberOfPasses-1));
                            } else {
                                jobTime += (int)(rapidTravel2/dpiX / rapidSpeed * (numberOfPasses-1));
                            }
                            prevX = X[y];
                        } else {
                            if ((numberOfPasses % 2) == 1) {
                                if (forward) {
                                    prevX = X[y];
                                } else {
                                    prevX = X2[y];
                                }
                                forward = !forward;
                            } else {
                                prevX = X2[y];
                            }
                        }
                    }
                    if (_bWorkerFlags != 0) {
                        continue;
                    }
                }
                
                gcPixelsCount = pixelsCount;
                gcJobTime = jobTime;
                
                flags &= ~BWorkerFlagDoWork;
            }
            
            if ((flags & BWorkerFlagRedrawOrigin) != 0) {
                imageBox1.StartPointX = (-left / (width/dpiX));
                imageBox1.StartPointY = (-top / (height/dpiY));
                imageBox1.Invalidate(false);
                
                flags &= ~BWorkerFlagRedrawOrigin;
            }
            
            ((BackgroundWorker)sender).ReportProgress(100, resources.GetString("Status_Ready", culture));
            bWorkerIsBusy = false;
        }
        
        Marshal.FreeHGlobal((IntPtr)sharpenTable);
        
        Marshal.FreeHGlobal((IntPtr)lookupTable);
        Marshal.FreeHGlobal((IntPtr)lookupTable2);
        
        Marshal.FreeHGlobal((IntPtr)lookupTable4);
        Marshal.FreeHGlobal((IntPtr)lookupTable5);
        Marshal.FreeHGlobal((IntPtr)lookupTable6);
        
        Marshal.FreeHGlobal((IntPtr)F);
        
        Marshal.FreeHGlobal((IntPtr)acctime);
        Marshal.FreeHGlobal((IntPtr)accdist);
        
        Marshal.FreeHGlobal(imBackground);
        
        imSrc.Dispose();
        
        imageBox1.ImIdx = -1;
        lock (imResizeLock) {
            Marshal.FreeHGlobal(imPreview);
            Marshal.FreeHGlobal(imResized);
            Marshal.FreeHGlobal(imDest);
        }
    }
    
    private void BackgroundWorker1RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
        
    }
}
