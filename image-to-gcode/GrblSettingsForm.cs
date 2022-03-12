using System;
using System.Drawing;
using System.Windows.Forms;

partial class GrblSettingsForm:Form {
    private readonly CheckBox NullCheckBox = new CheckBox();
    
    public readonly object[][] settingsTable;
    
    private void GrblSettingsLoad(object sender, EventArgs e) {
        foreach (object[] setting in settingsTable) {
            if (setting[2] != null) {
                continue;
            }
            if (setting[5] != null) {
                ((Control)setting[5]).Enabled = false;
            }
            if ((int)setting[3] != 0) {
                for (int i = 0; i < (int)setting[4]; i++) {
                    ((Control)setting[6+i]).Enabled = false;
                    ((CheckBox)setting[6+i]).Checked = false;
                }
            } else {
                ((Control)setting[6]).BackColor = Color.Empty;
                ((Control)setting[6]).Enabled = false;
                ((TextBox)setting[6]).Text = "0";
            }
        }
        foreach (object[] setting in settingsTable) {
            if (setting[2] == null) {
                continue;
            }
            if (setting[5] != null) {
                ((Control)setting[5]).Enabled = true;
            }
            if ((int)setting[3] != 0) {
                for (int i = 0; i < (int)setting[4]; i++) {
                    ((Control)setting[6+i]).Enabled = true;
                    ((CheckBox)setting[6+i]).Checked = (((byte)setting[2] & (1<<i)) != 0);
                }
            } else {
                ((Control)setting[6]).BackColor = Color.Empty;
                ((Control)setting[6]).Enabled = true;
                if ((int)setting[4] != 0) {
                    ((TextBox)setting[6]).Text = ((byte)setting[2]).ToString();
                } else {
                    ((TextBox)setting[6]).Text = ((float)setting[2]).ToString("0.0##");
                }
            }
        }
        button3.Enabled = false;
        
        foreach (object[] setting in settingsTable) {
            if ((int)setting[3] != 0) {
                for (int i = 0; i < (int)setting[4]; i++) {
                    ((CheckBox)setting[6+i]).CheckedChanged += Control_ValueChanged;
                }
            } else {
                ((TextBox)setting[6]).TextChanged += Control_ValueChanged;
            }
        }
    }
    
    private void GrblSettingsClosing(object sender, FormClosingEventArgs e) {
        foreach (object[] setting in settingsTable) {
            if ((int)setting[3] != 0) {
                for (int i = 0; i < (int)setting[4]; i++) {
                    ((CheckBox)setting[6+i]).CheckedChanged -= Control_ValueChanged;
                }
            } else {
                ((TextBox)setting[6]).TextChanged -= Control_ValueChanged;
            }
        }
    }
    
    private void Control_ValueChanged(object sender, EventArgs e) {
        int idx = (int)((Control)sender).Tag;
        foreach (object[] setting in settingsTable) {
            if ((int)setting[0] != idx) {
                continue;
            }
            
            if ((int)setting[3] != 0) {
                for (int i = 0; i < (int)setting[4]; i++) {
                    if (setting[6+i] == sender) {
                        if (((CheckBox)sender).Checked) {
                            setting[1] = (byte)((byte)setting[1] | (1<<i));
                        } else {
                            setting[1] = (byte)((byte)setting[1] & ~(1<<i));
                        }
                        break;
                    }
                }
            } else {
                try {
                    if ((int)setting[4] != 0) {
                        setting[1] = Byte.Parse(((Control)sender).Text);
                    } else {
                        setting[1] = Single.Parse(((Control)sender).Text);
                    }
                    ((Control)sender).BackColor = Color.Empty;
                } catch {
                    ((Control)sender).BackColor = Color.DarkSalmon;
                    return;
                }
            }
            
            button3.Enabled = true;
            break;
        }
    }
    
    public GrblSettingsForm() {
        InitializeComponent();
        
        settingsTable = new object[][] {
            new object[] { 100, null, null, 0, 0, label4,  textBox1, },
            new object[] { 101, null, null, 0, 0, label4,  textBox2, },
            new object[] { 110, null, null, 0, 0, label5,  textBox4, },
            new object[] { 111, null, null, 0, 0, label5,  textBox5, },
            new object[] { 120, null, null, 0, 0, label6,  textBox7, },
            new object[] { 121, null, null, 0, 0, label6,  textBox8, },
            new object[] { 130, null, null, 0, 0, label7,  textBox10, },
            new object[] { 131, null, null, 0, 0, label7,  textBox11, },
            new object[] { 140, null, null, 0, 0, label8,  textBox13, },
            new object[] { 141, null, null, 0, 0, label8,  textBox14, },
            new object[] { 150, null, null, 0, 0, label9,  textBox16, },
            new object[] { 151, null, null, 0, 0, label9,  textBox17, },
            new object[] { 2,   null, null, 1, 2, null,    checkBox1, checkBox2, },
            new object[] { 3,   null, null, 1, 2, null,    checkBox4, checkBox5, },
            new object[] { 5,   null, null, 1, 2, null,    checkBox3, checkBox6, },
            new object[] { 23,  null, null, 1, 2, null,    checkBox7, checkBox8, },
            new object[] { 18,  null, null, 1, 2, null,    checkBox14, checkBox15, },
            new object[] { 22,  null, null, 1, 8, tableLayoutPanel11, checkBox9, checkBox10, NullCheckBox, NullCheckBox, checkBox11, checkBox13, NullCheckBox, NullCheckBox, },
            new object[] { 24,  null, null, 0, 0, label15, textBox23, },
            new object[] { 25,  null, null, 0, 0, label16, textBox24, },
            new object[] { 28,  null, null, 0, 0, label19, textBox27, },
            new object[] { 29,  null, null, 0, 0, label20, textBox28, },
            new object[] { 11,  null, null, 0, 0, label12, textBox21, },
            new object[] { 12,  null, null, 0, 0, label13, textBox22, },
            new object[] { 30,  null, null, 0, 0, label11, textBox3, },
            new object[] { 31,  null, null, 0, 0, label22, textBox30, },
            new object[] { 32,  null, null, 1, 1, null,    checkBox20, },
            new object[] { 14,  null, null, 1, 1, null,    checkBox16, },
            new object[] { 4,   null, null, 1, 1, null,    checkBox12, },
            new object[] { 7,   null, null, 1, 1, null,    checkBox17, },
            new object[] { 8,   null, null, 1, 1, null,    checkBox18, },
            new object[] { 33,  null, null, 1, 1, null,    checkBox19, },
            new object[] { 10,  null, null, 1, 1, null,    checkBox21, },
        };
    }
}
