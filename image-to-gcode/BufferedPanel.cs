using System;
using System.Windows.Forms;

class BufferedPanel:Panel {
    public BufferedPanel() {
        this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        this.SetStyle(ControlStyles.UserPaint, true);
        this.SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        this.SetStyle(ControlStyles.ResizeRedraw, true);
        this.SetStyle(ControlStyles.StandardDoubleClick, false);
    }
}
