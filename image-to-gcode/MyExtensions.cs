using System;
using System.Windows.Forms;
using Microsoft.Win32;

static class MyExtensions {
    public static void SuspendLayout2(this Control ctrl) {
        foreach (Control ctrl2 in ctrl.Controls) {
            if (ctrl2 is TableLayoutPanel || ctrl2 is Panel) {
                ctrl2.SuspendLayout2();
            } else {
                if (ctrl2 is MenuStrip || ctrl2 is ToolStrip || ctrl2 is StatusStrip) {
                    ctrl2.SuspendLayout();
                }
            }
        }
        ctrl.SuspendLayout();
    }
    
    public static void ResumeLayout2(this Control ctrl) {
        foreach (Control ctrl2 in ctrl.Controls) {
            if (ctrl2 is TableLayoutPanel || ctrl2 is Panel) {
                ctrl2.ResumeLayout2();
            } else {
                if (ctrl2 is MenuStrip || ctrl2 is ToolStrip || ctrl2 is StatusStrip) {
                    ctrl2.ResumeLayout(false);
                    ctrl2.PerformLayout();
                }
            }
        }
        ctrl.ResumeLayout(false);
        ctrl.PerformLayout();
    }
    
    public static string GetString(this RegistryKey key, string name, string defaultValue) {
        return (string)key.GetValue(name, defaultValue);
    }
    
    public static void SetString(this RegistryKey key, string name, string value) {
        key.SetValue(name, value, RegistryValueKind.String);
    }
    
    public static int GetInt32(this RegistryKey key, string name, int defaultValue) {
        return (int)key.GetValue(name, defaultValue);
    }
    
    public static void SetInt32(this RegistryKey key, string name, object value) {
        key.SetValue(name, value, RegistryValueKind.DWord);
    }
    
    public unsafe static float GetSingle(this RegistryKey key, string name, float defaultValue) {
        int int_value = (int)key.GetValue(name, *(int*)&defaultValue);
        return *(float*)&int_value;
    }
    
    public unsafe static void SetSingle(this RegistryKey key, string name, float value) {
        key.SetValue(name, *(int*)&value, RegistryValueKind.DWord);
    }
}
