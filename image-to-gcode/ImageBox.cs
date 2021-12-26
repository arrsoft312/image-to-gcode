using System;
using System.Drawing;
using System.Windows.Forms;

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
        
        int zoom = this.Zoom;
        if (zoom > 0) {
            x = (x/zoom*zoom);
            y = (y/zoom*zoom);
        }
        
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
