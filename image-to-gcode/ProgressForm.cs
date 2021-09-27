using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

class ProgressForm:Form {
    public Button button3;
    public Button button2;
    public Button button1;
    private TableLayoutPanel tableLayoutPanel5;
    public TrackBar trackBar2;
    public Label label5;
    public Label label4;
    private TableLayoutPanel tableLayoutPanel4;
    public TrackBar trackBar1;
    public Label label3;
    public Label label2;
    private TableLayoutPanel tableLayoutPanel3;
    private TableLayoutPanel tableLayoutPanel2;
    public ProgressBar progressBar1;
    public Label label1;
    private TableLayoutPanel tableLayoutPanel1;
    private Panel panel1;
    
    public ProgressForm() {
        panel1 = new Panel();
        tableLayoutPanel1 = new TableLayoutPanel();
        label1 = new Label();
        progressBar1 = new ProgressBar();
        tableLayoutPanel2 = new TableLayoutPanel();
        tableLayoutPanel3 = new TableLayoutPanel();
        label2 = new Label();
        label3 = new Label();
        trackBar1 = new TrackBar();
        tableLayoutPanel4 = new TableLayoutPanel();
        label4 = new Label();
        label5 = new Label();
        trackBar2 = new TrackBar();
        tableLayoutPanel5 = new TableLayoutPanel();
        button1 = new Button();
        button2 = new Button();
        button3 = new Button();
        
        panel1.SuspendLayout();
        tableLayoutPanel1.SuspendLayout();
        tableLayoutPanel2.SuspendLayout();
        tableLayoutPanel3.SuspendLayout();
        ((ISupportInitialize)trackBar1).BeginInit();
        tableLayoutPanel4.SuspendLayout();
        ((ISupportInitialize)trackBar2).BeginInit();
        tableLayoutPanel5.SuspendLayout();
        this.SuspendLayout();
        
        panel1.AutoSize = true;
        panel1.BorderStyle = BorderStyle.FixedSingle;
        panel1.Controls.Add(tableLayoutPanel1);
        panel1.Dock = DockStyle.Fill;
        panel1.TabIndex = 0;
        
        tableLayoutPanel1.AutoSize = true;
        tableLayoutPanel1.ColumnCount = 1;
        tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle());
        tableLayoutPanel1.Controls.Add(label1, 0, 0);
        tableLayoutPanel1.Controls.Add(progressBar1, 0, 1);
        tableLayoutPanel1.Controls.Add(tableLayoutPanel2, 0, 2);
        tableLayoutPanel1.Controls.Add(tableLayoutPanel5, 0, 3);
        tableLayoutPanel1.Dock = DockStyle.Fill;
        tableLayoutPanel1.RowCount = 4;
        tableLayoutPanel1.RowStyles.Add(new RowStyle());
        tableLayoutPanel1.RowStyles.Add(new RowStyle());
        tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tableLayoutPanel1.RowStyles.Add(new RowStyle());
        tableLayoutPanel1.TabIndex = 0;
        
        label1.AutoSize = true;
        label1.Dock = DockStyle.Fill;
        label1.Margin = new Padding(6, 6, 6, 0);
        label1.TabIndex = 0;
        label1.TextAlign = ContentAlignment.MiddleLeft;
        
        progressBar1.Dock = DockStyle.Fill;
        progressBar1.Margin = new Padding(6, 3, 6, 3);
        progressBar1.Maximum = 1000;
        progressBar1.Style = ProgressBarStyle.Continuous;
        progressBar1.TabIndex = 1;
        
        tableLayoutPanel2.AutoSize = true;
        tableLayoutPanel2.ColumnCount = 1;
        tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle());
        tableLayoutPanel2.Controls.Add(tableLayoutPanel3, 0, 0);
        tableLayoutPanel2.Controls.Add(trackBar1, 0, 1);
        tableLayoutPanel2.Controls.Add(tableLayoutPanel4, 0, 2);
        tableLayoutPanel2.Controls.Add(trackBar2, 0, 3);
        tableLayoutPanel2.Dock = DockStyle.Fill;
        tableLayoutPanel2.RowCount = 4;
        tableLayoutPanel2.RowStyles.Add(new RowStyle());
        tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
        tableLayoutPanel2.RowStyles.Add(new RowStyle());
        tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
        tableLayoutPanel2.TabIndex = 2;
        
        tableLayoutPanel3.AutoSize = true;
        tableLayoutPanel3.ColumnCount = 2;
        tableLayoutPanel3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tableLayoutPanel3.ColumnStyles.Add(new ColumnStyle());
        tableLayoutPanel3.Controls.Add(label2, 0, 0);
        tableLayoutPanel3.Controls.Add(label3, 1, 0);
        tableLayoutPanel3.Dock = DockStyle.Fill;
        tableLayoutPanel3.Margin = new Padding(0, 3, 0, 0);
        tableLayoutPanel3.RowCount = 1;
        tableLayoutPanel3.RowStyles.Add(new RowStyle());
        tableLayoutPanel3.TabIndex = 0;
        
        label2.AutoSize = true;
        label2.Dock = DockStyle.Fill;
        label2.TabIndex = 0;
        label2.Text = "Feed Override:";
        label2.TextAlign = ContentAlignment.MiddleLeft;
        
        label3.AutoSize = true;
        label3.Dock = DockStyle.Fill;
        label3.TabIndex = 1;
        label3.Text = "100";
        label3.TextAlign = ContentAlignment.MiddleRight;
        
        trackBar1.Dock = DockStyle.Fill;
        trackBar1.Margin = new Padding(0, 3, 0, 3);
        trackBar1.Maximum = 200;
        trackBar1.Minimum = 10;
        trackBar1.TabIndex = 1;
        trackBar1.Tag = "zz";
        trackBar1.TickStyle = TickStyle.None;
        trackBar1.Value = 100;
        
        tableLayoutPanel4.AutoSize = true;
        tableLayoutPanel4.ColumnCount = 2;
        tableLayoutPanel4.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tableLayoutPanel4.ColumnStyles.Add(new ColumnStyle());
        tableLayoutPanel4.Controls.Add(label4, 0, 0);
        tableLayoutPanel4.Controls.Add(label5, 1, 0);
        tableLayoutPanel4.Dock = DockStyle.Fill;
        tableLayoutPanel4.Margin = new Padding(0, 3, 0, 0);
        tableLayoutPanel4.RowCount = 1;
        tableLayoutPanel4.RowStyles.Add(new RowStyle());
        tableLayoutPanel4.TabIndex = 2;
        
        label4.AutoSize = true;
        label4.Dock = DockStyle.Fill;
        label4.TabIndex = 0;
        label4.Text = "Power Override:";
        label4.TextAlign = ContentAlignment.MiddleLeft;
        
        label5.AutoSize = true;
        label5.Dock = DockStyle.Fill;
        label5.TabIndex = 1;
        label5.Text = "100";
        label5.TextAlign = ContentAlignment.MiddleRight;
        
        trackBar2.Dock = DockStyle.Fill;
        trackBar2.Margin = new Padding(0, 3, 0, 3);
        trackBar2.Maximum = 200;
        trackBar2.Minimum = 10;
        trackBar2.TabIndex = 3;
        trackBar2.Tag = "zz";
        trackBar2.TickStyle = TickStyle.None;
        trackBar2.Value = 100;
        
        tableLayoutPanel5.AutoSize = true;
        tableLayoutPanel5.ColumnCount = 4;
        tableLayoutPanel5.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tableLayoutPanel5.ColumnStyles.Add(new ColumnStyle());
        tableLayoutPanel5.ColumnStyles.Add(new ColumnStyle());
        tableLayoutPanel5.ColumnStyles.Add(new ColumnStyle());
        tableLayoutPanel5.Controls.Add(button1, 1, 0);
        tableLayoutPanel5.Controls.Add(button2, 2, 0);
        tableLayoutPanel5.Controls.Add(button3, 3, 0);
        tableLayoutPanel5.Dock = DockStyle.Fill;
        tableLayoutPanel5.RowCount = 1;
        tableLayoutPanel5.RowStyles.Add(new RowStyle());
        tableLayoutPanel5.TabIndex = 3;
        
        button1.AutoSize = true;
        button1.Dock = DockStyle.Fill;
        button1.FlatStyle = FlatStyle.Popup;
        button1.Size = new Size();
        button1.TabIndex = 0;
        button1.Text = "&RUN";
        button1.UseVisualStyleBackColor = true;
        
        button2.AutoSize = true;
        button2.Dock = DockStyle.Fill;
        button2.FlatStyle = FlatStyle.Popup;
        button2.Size = new Size();
        button2.TabIndex = 1;
        button2.Text = "&PAUSE";
        button2.UseVisualStyleBackColor = true;
        
        button3.AutoSize = true;
        button3.DialogResult = DialogResult.Abort;
        button3.Dock = DockStyle.Fill;
        button3.FlatStyle = FlatStyle.Popup;
        button3.Size = new Size();
        button3.TabIndex = 2;
        button3.Text = "&ABORT";
        button3.UseVisualStyleBackColor = true;
        
        this.AutoScaleDimensions = new SizeF(6F, 13F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.AutoSize = true;
        this.CancelButton = button3;
        this.ClientSize = new Size(399, -1);
        this.Controls.Add(panel1);
        this.FormBorderStyle = FormBorderStyle.None;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.ShowInTaskbar = false;
        this.StartPosition = FormStartPosition.CenterParent;
        this.Text = "ProgressForm";
        
        panel1.ResumeLayout(false);
        panel1.PerformLayout();
        tableLayoutPanel1.ResumeLayout(false);
        tableLayoutPanel1.PerformLayout();
        tableLayoutPanel2.ResumeLayout(false);
        tableLayoutPanel2.PerformLayout();
        tableLayoutPanel3.ResumeLayout(false);
        tableLayoutPanel3.PerformLayout();
        ((ISupportInitialize)trackBar1).EndInit();
        tableLayoutPanel4.ResumeLayout(false);
        tableLayoutPanel4.PerformLayout();
        ((ISupportInitialize)trackBar2).EndInit();
        tableLayoutPanel5.ResumeLayout(false);
        tableLayoutPanel5.PerformLayout();
        this.ResumeLayout(false);
        this.PerformLayout();
    }
}
