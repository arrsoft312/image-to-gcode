using System;
using System.Drawing;
using System.Windows.Forms;

class ProgressForm2:Form {
    public ProgressForm2(string s) {
        Panel panel1 = new Panel();
        TableLayoutPanel tableLayoutPanel1 = new TableLayoutPanel();
        Label label1 = new Label();
        
        panel1.SuspendLayout();
        tableLayoutPanel1.SuspendLayout();
        this.SuspendLayout();
        
        panel1.AutoSize = true;
        panel1.BorderStyle = BorderStyle.FixedSingle;
        panel1.Controls.Add(tableLayoutPanel1);
        panel1.Dock = DockStyle.Fill;
        panel1.TabIndex = 0;
        
        tableLayoutPanel1.AutoSize = true;
        tableLayoutPanel1.ColumnCount = 3;
        tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 15F));
        tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 15F));
        tableLayoutPanel1.Controls.Add(label1, 1, 1);
        tableLayoutPanel1.Dock = DockStyle.Fill;
        tableLayoutPanel1.RowCount = 3;
        tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        tableLayoutPanel1.RowStyles.Add(new RowStyle());
        tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        tableLayoutPanel1.TabIndex = 0;
        
        label1.AutoSize = true;
        label1.Dock = DockStyle.Fill;
        label1.TabIndex = 0;
        label1.Text = s;
        label1.TextAlign = ContentAlignment.MiddleCenter;
        
        this.AutoScaleDimensions = new SizeF(6F, 13F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.AutoSize = true;
        this.ClientSize = new Size();
        this.Controls.Add(panel1);
        this.FormBorderStyle = FormBorderStyle.None;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.ShowInTaskbar = false;
        this.StartPosition = FormStartPosition.CenterParent;
        this.Text = "ProgressForm2";
        
        panel1.ResumeLayout(false);
        panel1.PerformLayout();
        tableLayoutPanel1.ResumeLayout(false);
        tableLayoutPanel1.PerformLayout();
        this.ResumeLayout(false);
        this.PerformLayout();
    }
}
