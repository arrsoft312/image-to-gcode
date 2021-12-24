using System;
using System.Windows.Forms;

class BufferedPanel:Panel {
    public BufferedPanel() {
        this.SetStyle((ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint|ControlStyles.ResizeRedraw), true);
        this.SetStyle(ControlStyles.StandardDoubleClick, false);
    }
}
