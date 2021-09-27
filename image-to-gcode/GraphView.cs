using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

partial class image2gcode {
    private GraphView graphView1;
    
    private Color[] gvColors = new Color[ImColorCount];
    private float[] gvGhostValues = new float[4];
    private int gvSelectedColor;
    private int gvSelectedPoint;
    
    private void GraphViewLoad(object sender, EventArgs e) {
        float[] gvValues = (float[])((Control)sender).Tag;
        
        gvGhostValues[0] = gvValues[2];
        gvGhostValues[2] = gvValues[4];
        if (gvValues[0] > gvValues[1]) {
            gvGhostValues[1] = (1F-gvValues[3]);
            gvGhostValues[3] = (1F-gvValues[5]);
        } else {
            gvGhostValues[1] = gvValues[3];
            gvGhostValues[3] = gvValues[5];
        }
    }
    
    private void GraphViewDeactivate(object sender, EventArgs e) {
        gvSelectedPoint = 0;
    }
    
    private void GraphViewMouseMove(object sender, MouseEventArgs e) {
        float[] gvValues = (float[])((Control)sender).Tag;
        
        Size clientSize = ((Control)sender).ClientSize;
        int width = (clientSize.Width-20);
        int height = (clientSize.Height-25-21);
        
        int x = (e.X - 10);
        int y = (e.Y - 10);
        
        if (x >= 0 && x < width && y >= 0 && y < height) {
            for (int i = 0; i < ImColorCount; i++) {
                int p = (i*width / (ImColorCount-1) - width/ImColorCount/2);
                int p2 = ((1+i)*width / (ImColorCount-1) - width/ImColorCount/2);
                if (x >= p && x < p2) {
                    gvSelectedColor = (ImColorCount-1-i);
                    break;
                }
            }
        }
        
        if (e.Button == MouseButtons.Left) {
            if (gvSelectedPoint != 0) {
                float X = ((float)x / (width-1));
                if (X < 0F) {
                    X = 0F;
                } else if (X > 1F) {
                    X = 1F;
                }
                
                float Y;
                if (gvValues[0] > gvValues[1]) {
                    Y = (1F - (float)y / (int)((height-1) * (1F-gvValues[1]/gvValues[0])));
                } else {
                    Y = ((float)y / (int)((height-1) * (1F-gvValues[0]/gvValues[1])));
                }
                if (Y < 0F) {
                    Y = 0F;
                } else if (Y > 1F) {
                    Y = 1F;
                }
                
                if (gvSelectedPoint == 1) {
                    gvValues[2] = X;
                    gvValues[3] = Y;
                } else {
                    gvValues[4] = X;
                    gvValues[5] = Y;
                }
            }
        } else {
            float p2 = ((width-1)*gvValues[2]);
            float p3;
            float p4 = ((width-1)*gvValues[4]);
            float p5;
            if (gvValues[0] > gvValues[1]) {
                int j = (int)((height-1) * (1F-gvValues[1]/gvValues[0]));
                p3 = (j*(1F-gvValues[3]));
                p5 = (j*(1F-gvValues[5]));
            } else {
                int j = (int)((height-1) * (1F-gvValues[0]/gvValues[1]));
                p3 = (j*gvValues[3]);
                p5 = (j*gvValues[5]);
            }
            if (x >= p2-6 && x < p2+6 && y >= p3-6 && y < p3+6) {
                gvSelectedPoint = 1;
            } else if (x >= p4-6 && x < p4+6 && y >= p5-6 && y < p5+6) {
                gvSelectedPoint = 2;
            } else {
                gvSelectedPoint = 0;
            }
        }
        
        ((Control)sender).Invalidate(false);
    }
    
    private void GraphViewPaint2(object sender, PaintEventArgs e) {
        float[] gvValues = (float[])((Control)sender).Tag;
        
        Size clientSize = ((Control)sender).ClientSize;
        int width = clientSize.Width;
        int height = clientSize.Height;
        
        for (int i = 0; i < ImColorCount; i++) {
            int x = (i*width / (ImColorCount-1));
            int w = ((1+i)*width / (ImColorCount-1) - x);
            using (Brush brush = new SolidBrush(gvColors[ImColorCount-1-i])) {
                e.Graphics.FillRectangle(brush, x-width/ImColorCount/2, 0, w, height);
            }
        }
        
        if (gvValues[0] == gvValues[1]) {
            e.Graphics.DrawLine(Pens.Red, 0, height/2, width-1, height/2);
        } else {
            if (gvValues[0] > gvValues[1]) {
                int j = (int)((height-1) * (1F-gvValues[1]/gvValues[0]));
                e.Graphics.DrawBezier(Pens.Red, 0, j, (width-1)*gvValues[2], j*(1F-gvValues[3]), (width-1)*gvValues[4], j*(1F-gvValues[5]), width-1, 0);
            } else {
                int j = (int)((height-1) * (1F-gvValues[0]/gvValues[1]));
                e.Graphics.DrawBezier(Pens.Red, 0, 0, (width-1)*gvValues[2], j*gvValues[3], (width-1)*gvValues[4], j*gvValues[5], width-1, j);
            }
        }
    }
    
    private void GraphViewPaint(object sender, PaintEventArgs e) {
        float[] gvValues = (float[])((Control)sender).Tag;
        
        Size clientSize = ((Control)sender).ClientSize;
        
        const int left = 10;
        const int top = 10;
        int width = (clientSize.Width-20);
        int height = (clientSize.Height-25-21);
        
        e.Graphics.DrawRectangle(SystemPens.WindowFrame, left-1, top-1, width+1, height+1);
        using (Brush brush = new SolidBrush(gvColors[ImColorCount-1])) {
            e.Graphics.FillRectangle(brush, left, top, width/(ImColorCount-1)-width/ImColorCount/2, height);
        }
        for (int i = 1; i < (ImColorCount-1); i++) {
            int x = (i*width / (ImColorCount-1));
            int w = ((1+i)*width / (ImColorCount-1) - x);
            using (Brush brush = new SolidBrush(gvColors[ImColorCount-1-i])) {
                e.Graphics.FillRectangle(brush, left+x-width/ImColorCount/2, top, w, height);
            }
        }
        using (Brush brush = new SolidBrush(gvColors[0])) {
            e.Graphics.FillRectangle(brush, left+width-width/ImColorCount/2, top, width/ImColorCount/2, height);
        }
        
        float p0 = left;
        float p1;
        float p2 = (left + (width-1)*gvValues[2]);
        float p3;
        float p4 = (left + (width-1)*gvValues[4]);
        float p5;
        float p6 = (left + (width-1));
        float p7;
        
        int j;
        if (gvValues[0] > gvValues[1]) {
            j = (int)((height-1) * (1F-gvValues[1]/gvValues[0]));
            p1 = (top + j);
            p3 = (top + j*(1F-gvValues[3]));
            p5 = (top + j*(1F-gvValues[5]));
            p7 = top;
        } else {
            j = (int)((height-1) * (1F-gvValues[0]/gvValues[1]));
            p1 = top;
            p3 = (top + j*gvValues[3]);
            p5 = (top + j*gvValues[5]);
            p7 = (top + j);
        }
        
        using (Pen pen = new Pen(Color.FromArgb(127*16777216 + 0*65536 + 0*256 + 255), 1F)) {
            e.Graphics.DrawBezier(pen, p0, p1, left+(width-1)*gvGhostValues[0], top+j*gvGhostValues[1], left+(width-1)*gvGhostValues[2], top+j*gvGhostValues[3], p6, p7);
        }
        e.Graphics.DrawLine(Pens.DarkOrange, p0, p1, p2, p3);
        e.Graphics.DrawLine(Pens.DarkOrange, p6, p7, p4, p5);
        e.Graphics.DrawBezier(Pens.Crimson, p0, p1, p2, p3, p4, p5, p6, p7);
        e.Graphics.FillEllipse(Brushes.DarkOrange, p2-8, p3-8, 15, 15);
        e.Graphics.FillEllipse(Brushes.DarkOrange, p4-8, p5-8, 15, 15);
        if (gvSelectedPoint == 1) {
            e.Graphics.FillEllipse(Brushes.Crimson, p2-6, p3-6, 11, 11);
        } else if (gvSelectedPoint == 2) {
            e.Graphics.FillEllipse(Brushes.Crimson, p4-6, p5-6, 11, 11);
        }
        
        using (Font font = new Font(FontFamily.GenericSansSerif, 11F, FontStyle.Regular, GraphicsUnit.Pixel)) {
            int color = (gvSelectedColor*ImColorStep);
            
            float value = Single.NaN;
            if (color > ImColorBlack && color < ImColorWhite) {
                p1 = gvValues[1];
                p7 = gvValues[0];
                
                p2 = (3*ImColorWhite*(1F-gvValues[2]));
                p3 = (3F*(p1 - (p1-p7)*gvValues[3]));
                p4 = (3*ImColorWhite*(1F-gvValues[4]));
                p5 = (3F*(p1 - (p1-p7)*gvValues[5]));
                
                float x0 = ImColorWhite;
                float y0 = p1;
                for (int i = 1; i < BezierSegmentsCount; i++) {
                    float t = ((float)i/BezierSegmentsCount);
                    float inv_t = (1F - t);
                    
                    float x1 = (inv_t*inv_t*inv_t*ImColorWhite + inv_t*inv_t*t*p2 + t*t*inv_t*p4);
                    float y1 = (inv_t*inv_t*inv_t*p1 + inv_t*inv_t*t*p3 + t*t*inv_t*p5 + t*t*t*p7);
                    if (x1 < color) {
                        value = (y0 + (color-x0) * (y1-y0) / (x1-x0));
                        break;
                    }
                    
                    x0 = x1;
                    y0 = y1;
                }
            } else {
                if (color == ImColorWhite) {
                    value = gvValues[1];
                } else {
                    value = gvValues[0];;
                }
            }
            
            string text = (color.ToString(culture) + " | " + value.ToString("0.0", culture));
            SizeF textSize = e.Graphics.MeasureString(text, font, new SizeF(), null);
            int w = (int)textSize.Width;
            int h = (int)textSize.Height;
            
            e.Graphics.DrawRectangle(SystemPens.WindowFrame, left-1, top+height+5-1, w+7, 22);
            using (Brush brush = new SolidBrush(gvColors[gvSelectedColor])) {
                e.Graphics.FillRectangle(brush, left, top+height+5, w+6, 21);
            }
            
            Brush brush2;
            if (color > 127) {
                brush2 = Brushes.Black;
            } else {
                brush2 = Brushes.GhostWhite;
            }
            e.Graphics.DrawString(text, font, brush2, new RectangleF(left+3, top+height+5+(21-h)/2, 0F, 0F), null);
        }
    }
}

class GraphView:Form {
    public GraphView() {
        this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        this.SetStyle(ControlStyles.UserPaint, true);
        this.SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        this.SetStyle(ControlStyles.ResizeRedraw, true);
        this.SetStyle(ControlStyles.StandardDoubleClick, false);
        
        this.AutoScaleDimensions = new SizeF(6F, 13F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.AutoSize = true;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.MinimumSize = new Size(550, 250);
        this.ShowInTaskbar = false;
        this.StartPosition = FormStartPosition.Manual;
        this.Text = "GraphView";
    }
}
