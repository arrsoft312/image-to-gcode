using System;
using System.Drawing;
using System.Windows.Forms;

class ImageBox:ScrollableControl {
    private const int ZoomIncrement = 5;
    private const int MinZoom = 1;
    private const int MaxZoom = 800;
    
    private bool isPanning;
    private Point startMousePosition;
    private Point startScrollPosition;
    
    public event EventHandler ZoomChanged;
    public int Zoom = 100;
    
    public int ImIdx = -1;
    
    public float StartPointX;
    public float StartPointY;
    public int ImWidth;
    public int ImHeight;
    
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
        
        int x = (-startScrollPosition.X + (startMousePosition.X - e.Location.X));
        int y = (-startScrollPosition.Y + (startMousePosition.Y - e.Location.Y));
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
        
        int zoom = this.Zoom;
        if (e.Delta < 0) {
            if (zoom > 100 && (zoom-ZoomIncrement) < 100) {
                zoom = 100;
            } else {
                zoom -= ZoomIncrement;
                if (zoom < MinZoom) {
                    zoom = MinZoom;
                }
            }
        } else {
            if (zoom < 100 && (zoom+ZoomIncrement) > 100) {
                zoom = 100;
            } else {
                zoom += ZoomIncrement;
                if (zoom > MaxZoom) {
                    zoom = MaxZoom;
                }
            }
        }
        
        this.AutoScrollMinSize = new Size((this.ImWidth * zoom / 100), (this.ImHeight * zoom / 100));
        
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
        this.AutoScrollMinSize = new Size((this.ImWidth * zoom / 100), (this.ImHeight * zoom / 100));
    }
}
