using System;
using System.Drawing;
using System.Windows.Forms;

class WrappedOutputDialog:Form {
    public Button button2;
    public Button button1;
    private TableLayoutPanel tableLayoutPanel3;
    public Label label5;
    public TextBox textBox3;
    public Label label4;
    public TextBox textBox2;
    public Label label3;
    public TextBox textBox1;
    public Label label2;
    public ComboBox comboBox1;
    public Label label1;
    private TableLayoutPanel tableLayoutPanel2;
    private TableLayoutPanel tableLayoutPanel1;
    
    public WrappedOutputDialog() {
        tableLayoutPanel1 = new TableLayoutPanel();
        tableLayoutPanel2 = new TableLayoutPanel();
        label1 = new Label();
        comboBox1 = new ComboBox();
        label2 = new Label();
        textBox1 = new TextBox();
        label3 = new Label();
        textBox2 = new TextBox();
        label4 = new Label();
        textBox3 = new TextBox();
        label5 = new Label();
        tableLayoutPanel3 = new TableLayoutPanel();
        button1 = new Button();
        button2 = new Button();
        
        tableLayoutPanel1.SuspendLayout();
        tableLayoutPanel2.SuspendLayout();
        tableLayoutPanel3.SuspendLayout();
        this.SuspendLayout();
        
        tableLayoutPanel1.AutoSize = true;
        tableLayoutPanel1.ColumnCount = 1;
        tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle());
        tableLayoutPanel1.Controls.Add(tableLayoutPanel2, 0, 0);
        tableLayoutPanel1.Controls.Add(label5, 0, 1);
        tableLayoutPanel1.Controls.Add(tableLayoutPanel3, 0, 2);
        tableLayoutPanel1.Dock = DockStyle.Fill;
        tableLayoutPanel1.RowCount = 3;
        tableLayoutPanel1.RowStyles.Add(new RowStyle());
        tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tableLayoutPanel1.RowStyles.Add(new RowStyle());
        tableLayoutPanel1.TabIndex = 0;
        
        tableLayoutPanel2.AutoSize = true;
        tableLayoutPanel2.ColumnCount = 2;
        tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle());
        tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tableLayoutPanel2.Controls.Add(label1, 0, 0);
        tableLayoutPanel2.Controls.Add(comboBox1, 1, 0);
        tableLayoutPanel2.Controls.Add(label2, 0, 1);
        tableLayoutPanel2.Controls.Add(textBox1, 1, 1);
        tableLayoutPanel2.Controls.Add(label3, 0, 2);
        tableLayoutPanel2.Controls.Add(textBox2, 1, 2);
        tableLayoutPanel2.Controls.Add(label4, 0, 3);
        tableLayoutPanel2.Controls.Add(textBox3, 1, 3);
        tableLayoutPanel2.Dock = DockStyle.Fill;
        tableLayoutPanel2.RowCount = 4;
        tableLayoutPanel2.RowStyles.Add(new RowStyle());
        tableLayoutPanel2.RowStyles.Add(new RowStyle());
        tableLayoutPanel2.RowStyles.Add(new RowStyle());
        tableLayoutPanel2.RowStyles.Add(new RowStyle());
        tableLayoutPanel2.TabIndex = 0;
        
        label1.AutoSize = true;
        label1.Dock = DockStyle.Fill;
        label1.Margin = new Padding(3, 3, 3, 2);
        label1.TabIndex = 0;
        label1.Text = "&Axis Being Wrapped:";
        label1.TextAlign = ContentAlignment.MiddleLeft;
        
        comboBox1.Dock = DockStyle.Fill;
        comboBox1.DropDownStyle = ComboBoxStyle.DropDownList;
        comboBox1.Items.AddRange(new object[] { image2gcode.GcAxis.X, image2gcode.GcAxis.Y, });
        comboBox1.Margin = new Padding(3, 3, 3, 2);
        comboBox1.Size = new Size();
        comboBox1.TabIndex = 1;
        comboBox1.Tag = 183;
        
        label2.AutoSize = true;
        label2.Dock = DockStyle.Fill;
        label2.Enabled = false;
        label2.Margin = new Padding(3, 1, 3, 2);
        label2.TabIndex = 2;
        label2.Text = "MM per R&evolution X:";
        label2.TextAlign = ContentAlignment.MiddleLeft;
        
        textBox1.Dock = DockStyle.Fill;
        textBox1.Enabled = false;
        textBox1.Margin = new Padding(3, 1, 3, 2);
        textBox1.Size = new Size();
        textBox1.TabIndex = 3;
        textBox1.Tag = 5048;
        
        label3.AutoSize = true;
        label3.Dock = DockStyle.Fill;
        label3.Margin = new Padding(3, 1, 3, 3);
        label3.TabIndex = 4;
        label3.Text = "MM per R&evolution Y:";
        label3.TextAlign = ContentAlignment.MiddleLeft;
        
        textBox2.Dock = DockStyle.Fill;
        textBox2.Margin = new Padding(3, 1, 3, 3);
        textBox2.Size = new Size();
        textBox2.TabIndex = 5;
        textBox2.Tag = 5049;
        
        label4.AutoSize = true;
        label4.Dock = DockStyle.Fill;
        label4.TabIndex = 6;
        label4.Text = "&Diameter of Cylinder:";
        label4.TextAlign = ContentAlignment.MiddleLeft;
        
        textBox3.Dock = DockStyle.Fill;
        textBox3.Size = new Size();
        textBox3.TabIndex = 7;
        textBox3.Tag = 5050;
        
        label5.AutoSize = false;
        label5.Dock = DockStyle.Fill;
        label5.Margin = new Padding(6, 3, 6, 3);
        label5.TabIndex = 1;
        label5.TextAlign = ContentAlignment.TopLeft;
        
        tableLayoutPanel3.AutoSize = true;
        tableLayoutPanel3.ColumnCount = 3;
        tableLayoutPanel3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tableLayoutPanel3.ColumnStyles.Add(new ColumnStyle());
        tableLayoutPanel3.ColumnStyles.Add(new ColumnStyle());
        tableLayoutPanel3.Controls.Add(button1, 1, 0);
        tableLayoutPanel3.Controls.Add(button2, 2, 0);
        tableLayoutPanel3.Dock = DockStyle.Fill;
        tableLayoutPanel3.RowCount = 1;
        tableLayoutPanel3.RowStyles.Add(new RowStyle());
        tableLayoutPanel3.TabIndex = 2;
        
        button1.AutoSize = true;
        button1.DialogResult = DialogResult.OK;
        button1.Dock = DockStyle.Fill;
        button1.TabIndex = 0;
        button1.Text = "OK";
        button1.UseVisualStyleBackColor = true;
        
        button2.AutoSize = true;
        button2.DialogResult = DialogResult.Abort;
        button2.Dock = DockStyle.Fill;
        button2.TabIndex = 1;
        button2.Text = "Cancel";
        button2.UseVisualStyleBackColor = true;
        
        this.AutoScaleDimensions = new SizeF(6F, 13F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.AutoSize = true;
        this.AcceptButton = button1;
        this.CancelButton = button2;
        this.ClientSize = new Size(218, 179);
        this.Controls.Add(tableLayoutPanel1);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.ShowInTaskbar = false;
        this.StartPosition = FormStartPosition.CenterParent;
        this.Text = "Wrapped Output";
        
        tableLayoutPanel1.ResumeLayout(false);
        tableLayoutPanel1.PerformLayout();
        tableLayoutPanel2.ResumeLayout(false);
        tableLayoutPanel2.PerformLayout();
        tableLayoutPanel3.ResumeLayout(false);
        tableLayoutPanel3.PerformLayout();
        this.ResumeLayout(false);
        this.PerformLayout();
    }
}
