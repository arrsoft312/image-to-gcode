using System;
using System.Drawing;
using System.Windows.Forms;

class ProgressForm:Form {
    public Button button3;
    public Button button2;
    public Button button1;
    private TableLayoutPanel tableLayoutPanel5;
    public ProgressBar progressBar1;
    public Label label1;
    private TableLayoutPanel tableLayoutPanel1;
    private Panel panel1;
    
    public ProgressForm() {
        panel1 = new Panel();
        tableLayoutPanel1 = new TableLayoutPanel();
        label1 = new Label();
        progressBar1 = new ProgressBar();
        tableLayoutPanel5 = new TableLayoutPanel();
        button1 = new Button();
        button2 = new Button();
        button3 = new Button();
        
        panel1.SuspendLayout();
        tableLayoutPanel1.SuspendLayout();
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
        tableLayoutPanel1.Controls.Add(label1, 0, 1);
        tableLayoutPanel1.Controls.Add(progressBar1, 0, 2);
        tableLayoutPanel1.Controls.Add(tableLayoutPanel5, 0, 3);
        tableLayoutPanel1.Dock = DockStyle.Fill;
        tableLayoutPanel1.RowCount = 5;
        tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        tableLayoutPanel1.RowStyles.Add(new RowStyle());
        tableLayoutPanel1.RowStyles.Add(new RowStyle());
        tableLayoutPanel1.RowStyles.Add(new RowStyle());
        tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
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
        tableLayoutPanel5.TabIndex = 2;
        
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
        tableLayoutPanel5.ResumeLayout(false);
        tableLayoutPanel5.PerformLayout();
        this.ResumeLayout(false);
        this.PerformLayout();
    }
}
