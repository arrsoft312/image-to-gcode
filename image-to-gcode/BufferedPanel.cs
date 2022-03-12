using System;
using System.ComponentModel;
using System.Windows.Forms;

class BufferedPanel:Panel {
    private readonly object eventMouseWheel = new object();
    
    [Browsable(false), EditorBrowsable(EditorBrowsableState.Advanced)]
    public new event MouseEventHandler MouseWheel {
        add { base.Events.AddHandler(eventMouseWheel, value); }
        remove { base.Events.RemoveHandler(eventMouseWheel, value); }
    }
    
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    protected override void OnMouseWheel(MouseEventArgs e) {
        MouseEventHandler mouseEventHandler = (MouseEventHandler)base.Events[eventMouseWheel];
        if (mouseEventHandler != null) {
            mouseEventHandler(this, e);
        }
    }
    
    public BufferedPanel() {
        this.SetStyle((ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint|ControlStyles.ResizeRedraw), true);
        this.SetStyle(ControlStyles.StandardDoubleClick, false);
    }
}
