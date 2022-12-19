using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

partial class image2gcode {
    private int ibImageWidth = -1;
    private int ibImageHeight = -1;
    
    private IntPtr ibScan0;
    private Image ibImage;
    
    private unsafe void ImageBoxPaint(object sender, PaintEventArgs e) {
        lock (imResizeLock) {
            int imIdx = ((ImageBox)sender).ImIdx;
            if (imIdx == -1) {
                return;
            }
            
            Size ibSize = ((Control)sender).ClientSize;
            int ibWidth = ibSize.Width;
            int ibHeight = ibSize.Height;
            
            Point scrollPosition = ((ScrollableControl)sender).AutoScrollPosition;
            
            bool burnFromBottomToTop = gcBurnFromBottomToTop;
            
            float startPtX = ((ImageBox)sender).StartPointX;
            float startPtY = ((ImageBox)sender).StartPointY;
            int imWidth = ((ImageBox)sender).ImWidth;
            int imHeight = ((ImageBox)sender).ImHeight;
            int zoom = ((ImageBox)sender).Zoom;
            
            int scaledImHeight, scaledImWidth;
            if (zoom > 0) {
                scaledImWidth = (imWidth * zoom);
                scaledImHeight = (imHeight * zoom);
            } else {
                scaledImWidth = (imWidth / -zoom);
                scaledImHeight = (imHeight / -zoom);
            }
            
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
            
            int destScanWidth = ((destWidth*3+3) / 4 * 4);
            
            if (destWidth != ibImageWidth || destHeight != ibImageHeight) {
                Marshal.FreeHGlobal(ibScan0);
                ibScan0 = Marshal.AllocHGlobal((IntPtr)(destHeight*destScanWidth));
                ibImage = new Bitmap(destWidth, destHeight, destScanWidth, PixelFormat.Format24bppRgb, ibScan0);
                
                ibImageWidth = destWidth;
                ibImageHeight = destHeight;
            }
            
            int scanWidth = ((imWidth+3) / 4 * 4);
            
            if (zoom > 0) {
                int srcLeft = (-scrollPosition.X / zoom);
                int srcTop = (-scrollPosition.Y / zoom);
                
                int j = (-scrollPosition.X - srcLeft*zoom);
                int i = (-scrollPosition.Y - srcTop*zoom);
                destWidth += j;
                destHeight += i;
                
                IntPtr imScan0;
                if (imIdx == 2) {
                    imScan0 = (imResized + srcTop*scanWidth + srcLeft);
                } else {
                    imScan0 = (imDest + srcTop*scanWidth + srcLeft);
                }
                
                Parallel.For(i, destHeight, (y) => {
                    byte* src = (byte*)(imScan0 + y/zoom * scanWidth);
                    byte* dest = (byte*)(ibScan0 + y*destScanWidth - i*destScanWidth - j*3);
                    
                    for (int x = j; x < destWidth; x++) {
                        dest[x*3+2] = src[x/zoom];
                        dest[x*3+1] = src[x/zoom];
                        dest[x*3+0] = src[x/zoom];
                    }
                });
            } else {
                int srcLeft = (scrollPosition.X * zoom);
                int srcTop = (scrollPosition.Y * zoom);
                
                zoom = -zoom;
                
                IntPtr imScan0;
                if (imIdx == 2) {
                    imScan0 = (imResized + srcTop*scanWidth + srcLeft);
                } else {
                    imScan0 = (imDest + srcTop*scanWidth + srcLeft);
                }
                
                Parallel.For(0, destHeight, (y) => {
                    byte* src = (byte*)(imScan0 + y*zoom * scanWidth);
                    byte* dest = (byte*)(ibScan0 + y*destScanWidth);
                    
                    for (int x = 0; x < destWidth; x++) {
                        int num = 0;
                        for (int i = 0; i < zoom; i++) {
                            for (int j = 0; j < zoom; j++) {
                                num += src[i*scanWidth + x*zoom + j];
                            }
                        }
                        
                        byte gray = (byte)(num/zoom/zoom);
                        dest[x*3+2] = gray;
                        dest[x*3+1] = gray;
                        dest[x*3+0] = gray;
                    }
                });
            }
            
            e.Graphics.DrawImage(ibImage, hSpace/2, vSpace/2);
            
            int xx = (hSpace/2 + (int)((scaledImWidth-1)*startPtX) + scrollPosition.X);
            
            int yy;
            if (burnFromBottomToTop) {
                yy = (vSpace/2 + (int)((scaledImHeight-1)*(1F-startPtY)) + scrollPosition.Y);
            } else {
                yy = (vSpace/2 + (int)((scaledImHeight-1)*startPtY) + scrollPosition.Y);
            }
            
            e.Graphics.DrawLine(Pens.Red, ibWidth-1, yy, 0, yy);
            e.Graphics.DrawLine(Pens.Red, xx, 0, xx, ibHeight-1);
        }
    }
}

class ImageBox:ScrollableControl {
    private bool isPanning;
    private Point startMousePosition;
    private Point startScrollPosition;
    
    public event EventHandler ZoomChanged;
    public int Zoom;
    
    public float StartPointX;
    public float StartPointY;
    public int ImWidth;
    public int ImHeight;
    
    public int ImIdx = -1;
    
    protected override void OnMouseEnter(EventArgs e) {
        this.Focus();
    }
    
    protected override void OnMouseLeave(EventArgs e) {
        
    }
    
    protected override void OnMouseDown(MouseEventArgs e) {
        if (this.ImIdx == -1) {
            return;
        }
        
        if (e.Button != MouseButtons.Left) {
            return;
        }
        
        if (!isPanning) {
            isPanning = true;
            startMousePosition = e.Location;
            startScrollPosition = this.AutoScrollPosition;
            this.Cursor = Cursors.SizeAll;
        }
    }
    
    protected override void OnMouseMove(MouseEventArgs e) {
        if (!isPanning) {
            return;
        }
        
        int x = (-startScrollPosition.X + (startMousePosition.X - e.X));
        int y = (-startScrollPosition.Y + (startMousePosition.Y - e.Y));
        this.AutoScrollPosition = new Point(x, y);
        
        this.Invalidate(false);
    }
    
    protected override void OnMouseUp(MouseEventArgs e) {
        if (isPanning) {
            isPanning = false;
            this.Cursor = Cursors.Default;
        }
    }
    
    protected override void OnMouseWheel(MouseEventArgs e) {
        if (this.ImIdx == -1) {
            return;
        }
        
        if (isPanning) {
            return;
        }
        
        Size ibSize = this.ClientSize;
        int ibWidth = ibSize.Width;
        int ibHeight = ibSize.Height;
        
        Point scrollPosition = this.AutoScrollPosition;
        int zoom = this.Zoom;
        
        int eX = e.X;
        int eY = e.Y;
        if (eX < 0 || eX >= ibWidth || eY < 0 || eY >= ibHeight) {
            eX = (ibWidth / 2);
            eY = (ibHeight / 2);
        }
        
        int vSpace, hSpace;
        if (zoom > 0) {
            hSpace = (ibWidth - this.ImWidth*zoom);
            vSpace = (ibHeight - this.ImHeight*zoom);
        } else {
            hSpace = (ibWidth - this.ImWidth/-zoom);
            vSpace = (ibHeight - this.ImHeight/-zoom);
        }
        if (hSpace < 0) {
            hSpace = 0;
        }
        if (vSpace < 0) {
            vSpace = 0;
        }
        
        int y, x;
        if (zoom > 0) {
            x = ((hSpace/2 + scrollPosition.X-eX) / zoom);
            y = ((vSpace/2 + scrollPosition.Y-eY) / zoom);
        } else {
            x = ((hSpace/2 + scrollPosition.X-eX) * -zoom);
            y = ((vSpace/2 + scrollPosition.Y-eY) * -zoom);
        }
        
        if (e.Delta < 0) {
            if (zoom > 0) {
                if (--zoom == 0) {
                    zoom = -2;
                }
            } else {
                zoom *= 2;
                if (zoom < -8) {
                    return;
                }
            }
        } else {
            if (zoom > 0) {
                if (++zoom > 8) {
                    return;
                }
            } else {
                zoom /= 2;
                if (zoom == -1) {
                    zoom = 1;
                }
            }
        }
        
        if (zoom > 0) {
            this.AutoScrollMinSize = new Size((this.ImWidth * zoom), (this.ImHeight * zoom));
            this.AutoScrollPosition = new Point(x*-zoom-eX, y*-zoom-eY);
        } else {
            this.AutoScrollMinSize = new Size((this.ImWidth / -zoom), (this.ImHeight / -zoom));
            this.AutoScrollPosition = new Point(x/zoom-eX, y/zoom-eY);
        }
        
        this.Zoom = zoom;
        if (this.ZoomChanged != null) {
            this.ZoomChanged(this, EventArgs.Empty);
        }
        
        this.Invalidate(false);
    }
    
    protected override void OnScroll(ScrollEventArgs se) {
        this.Invalidate(false);
    }
    
    public ImageBox() {
        this.SetStyle((ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint|ControlStyles.ResizeRedraw), true);
        this.SetStyle(ControlStyles.StandardDoubleClick, false);
    }
    
    public void AdjustLayout() {
        int zoom = this.Zoom;
        if (zoom > 0) {
            this.AutoScrollMinSize = new Size((this.ImWidth * zoom), (this.ImHeight * zoom));
        } else {
            this.AutoScrollMinSize = new Size((this.ImWidth / -zoom), (this.ImHeight / -zoom));
        }
    }
}
