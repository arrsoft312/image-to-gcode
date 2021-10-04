using System;
using System.Drawing;
using System.Windows.Forms;

partial class image2gcode {
    private delegate bool GrblAbort();
    
    private GrblSettingsForm grblSettings1;
    private ProgressForm2 progressForm2;
    private ProgressForm2 progressForm3;
    
    private int new_f_override = 100;
    private int new_s_override = 100;
    
    private void SettingsToolStripMenuItemClick(object sender, EventArgs e) {
        try {
            progressForm3.Left = (this.Left + (this.Width - progressForm3.Width)/2);
            progressForm3.Top = (this.Top + (this.Height - progressForm3.Height)/2);
            
            progressForm3.Show(this);
            progressForm3.Update();
            
            try {
                serialPort1.BaudRate = baudRate;
                serialPort1.PortName = comPort;
                
                serialPort1.ReadTimeout = 500;
                serialPort1.Open();
                
                Grbl_GetSync();
                
                serialPort1.Write("$$\n");
                
                string[] resp = serialPort1.ReadTo("\r\nok\r\n").Split(new string[] { "\r\n", }, 257, StringSplitOptions.None);
                if (resp.Length > 256) {
                    throw new Exception(resources.GetString("Grbl_InvalidResponse", culture));
                }
                
                foreach (object[] setting in grblSettings1.settingsTable) {
                    setting[1] = setting[2] = null;
                }
                
                foreach (string line in resp) {
                    string[] s = line.Split(new string[] { "$", "=", }, 4, StringSplitOptions.None);
                    if (s.Length != 3) {
                        throw new Exception(resources.GetString("Grbl_InvalidResponse", culture));
                    }
                    
                    int parameter = Byte.Parse(s[1], invariantCulture);
                    foreach (object[] setting in grblSettings1.settingsTable) {
                        if ((int)setting[0] == parameter) {
                            if ((int)setting[3] != 0) {
                                setting[2] = Byte.Parse(s[2], invariantCulture);
                            } else {
                                if ((int)setting[4] != 0) {
                                    setting[2] = Byte.Parse(s[2], invariantCulture);
                                } else {
                                    setting[2] = Single.Parse(s[2], invariantCulture);
                                }
                            }
                            
                            setting[1] = setting[2];
                            break;
                        }
                    }
                }
            } catch (TimeoutException) {
                MessageBox.Show(this, resources.GetString("Grbl_NotResponding", culture), AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            } catch (Exception ex) {
                MessageBox.Show(this, ex.Message, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            } finally {
                progressForm3.Hide();
            }
            
            grblSettings1.ShowDialog(this);
        } finally {
            serialPort1.Close();
        }
    }
    
    private void GrblSettingsWrite(object sender, EventArgs e) {
        try {
            foreach (object[] setting in grblSettings1.settingsTable) {
                if (setting[1] == setting[2]) {
                    continue;
                }
                
                string s = ("$" + ((int)setting[0]).ToString(invariantCulture) + "=");
                if ((int)setting[3] != 0) {
                    s += ((byte)setting[1]).ToString(invariantCulture);
                } else {
                    if ((int)setting[4] != 0) {
                        s += ((byte)setting[1]).ToString(invariantCulture);
                    } else {
                        s += ((float)setting[1]).ToString("0.###", invariantCulture);
                    }
                }
                
                serialPort1.Write(s + "\n");
                
                string[] resp = serialPort1.ReadTo("\r\n").Split(new string[] { ":", }, 2, StringSplitOptions.None);
                if (resp[0] != "ok") {
                    if ((int)setting[3] == 0) {
                        ((Control)setting[6]).BackColor = Color.DarkSalmon;
                    }
                    
                    throw new Exception(s);
                }
                
                setting[2] = setting[1];
            }
            grblSettings1.button3.Enabled = false;
        } catch (TimeoutException) {
            MessageBox.Show(this, resources.GetString("Grbl_NotResponding", culture), AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        } catch (Exception ex) {
            MessageBox.Show(this, ex.Message, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        
        if ((bool)((Control)sender).Tag) {
            grblSettings1.Close();
        }
    }
    
    private void Grbl_GetSync(GrblAbort callback = null) {
        int prevReadTimeout = serialPort1.ReadTimeout;
        try {
            serialPort1.ReadTimeout = 250;
            for (int i = 0; i < 15; i++) {
                serialPort1.Write("\n\n");
                
                string resp = null;
                try {
                    for (;;) {
                        resp = serialPort1.ReadTo("\r\n");
                    }
                } catch (TimeoutException) {
                }
                
                if (callback != null) {
                    if (callback()) {
                        return;
                    }
                }
                
                if (resp == null) {
                    continue;
                }
                if (resp != "ok") {
                    throw new Exception(resources.GetString("Grbl_InvalidResponse", culture));
                }
                
                return;
            }
            
            throw new Exception(resources.GetString("Grbl_NotResponding", culture));
        } finally {
            serialPort1.ReadTimeout = prevReadTimeout;
        }
    }
    
    private unsafe void Grbl_GetBuildInfo(bool* isKaskade, int* rxBufferSize = null) {
        int prevReadTimeout = serialPort1.ReadTimeout;
        try {
            serialPort1.ReadTimeout = 100;
            serialPort1.Write("$I\n");
            
            string[] resp = serialPort1.ReadTo("ok\r\n").Split(new string[] { "]\r\n", }, 4, StringSplitOptions.None);
            if (resp.Length != 3) {
                throw new Exception(resources.GetString("Grbl_InvalidResponse", culture));
            }
            
            string[] ver = resp[0].Split(new string[] { ":", ".", }, 5, StringSplitOptions.None);
            if (ver.Length != 5) {
                throw new Exception(resources.GetString("Grbl_InvalidResponse", culture));
            }
            if (ver[0] != "[VER") {
                throw new Exception(resources.GetString("Grbl_InvalidResponse", culture));
            }
            
            string[] opt = resp[1].Split(new string[] { ":", ",", }, 5, StringSplitOptions.None);
            if (opt.Length != 4) {
                throw new Exception(resources.GetString("Grbl_InvalidResponse", culture));
            }
            if (opt[0] != "[OPT") {
                throw new Exception(resources.GetString("Grbl_InvalidResponse", culture));
            }
            
            *isKaskade = (ver[4] == "KASKADE");
            if (rxBufferSize != null) {
                *rxBufferSize = Int32.Parse(opt[3], invariantCulture);
            }
        } catch (TimeoutException) {
            throw new Exception(resources.GetString("Grbl_NotResponding", culture));
        } finally {
            serialPort1.ReadTimeout = prevReadTimeout;
        }
    }
}
