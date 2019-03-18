using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Kbg.NppPluginNET.PluginInfrastructure;
using NipCheck;

namespace Kbg.NppPluginNET
{
    class Main
    {
        internal const string PluginName = "NipCheck";
        static string iniFilePath = null;
        static bool someSetting = false;
        static frmMyDlg frmMyDlg = null;
        static int idMyDlg = -1;
        static Bitmap tbBmp = new Icon(NipCheck.Properties.Resources.D2, new Size(16, 16)).ToBitmap();
        static Bitmap tbBmp_tbTab = NipCheck.Properties.Resources.star_bmp;
        static Icon tbIcon = null;

        public static void OnNotification(ScNotification notification)
        {  
            // This method is invoked whenever something is happening in notepad++
            // use eg. as
            // if (notification.Header.Code == (uint)NppMsg.NPPN_xxx)
            // { ... }
            // or
            //
            // if (notification.Header.Code == (uint)SciMsg.SCNxxx)
            // { ... }
        }

        internal static void CommandMenuInit()
        {
            StringBuilder sbIniFilePath = new StringBuilder(Win32.MAX_PATH);
            Win32.SendMessage(PluginBase.nppData._nppHandle, (uint) NppMsg.NPPM_GETPLUGINSCONFIGDIR, Win32.MAX_PATH, sbIniFilePath);
            iniFilePath = sbIniFilePath.ToString();
            if (!Directory.Exists(iniFilePath)) Directory.CreateDirectory(iniFilePath);
            iniFilePath = Path.Combine(iniFilePath, PluginName + ".ini");
            someSetting = (Win32.GetPrivateProfileInt("SomeSection", "SomeKey", 0, iniFilePath) != 0);

            Helper.init();
            PluginBase.SetCommand(0, "Run", myMenuFunction, new ShortcutKey(false, false, false, Keys.None));
            PluginBase.SetCommand(1, "About", myAboutFunction); idMyDlg = 0;
        }

        internal static void SetToolBarIcon()
        {
            toolbarIcons tbIcons = new toolbarIcons();
            tbIcons.hToolbarBmp = tbBmp.GetHbitmap();
            IntPtr pTbIcons = Marshal.AllocHGlobal(Marshal.SizeOf(tbIcons));
            Marshal.StructureToPtr(tbIcons, pTbIcons, false);
            Win32.SendMessage(PluginBase.nppData._nppHandle, (uint) NppMsg.NPPM_ADDTOOLBARICON, PluginBase._funcItems.Items[idMyDlg]._cmdID, pTbIcons);
            Marshal.FreeHGlobal(pTbIcons);
        }

        internal static void PluginCleanUp()
        {
            Win32.WritePrivateProfileString("SomeSection", "SomeKey", someSetting ? "1" : "0", iniFilePath);
        }

        internal static void myAboutFunction()
        {
            MessageBox.Show("NTIP Syntax and Error Checker\n\nVersion: 1.0.0.0\nAuthor: github.com/noah-\nCopyright © 2013 - 2019\nAll Rights Reserved", "NipCheck");
        }

        internal static void myMenuFunction()
        {
            try
            {
                IntPtr hCurrScintilla = PluginBase.GetCurrentScintilla();
                int textLen = (int)Win32.SendMessage(hCurrScintilla, SciMsg.SCI_GETLENGTH, 0, 0);
                IntPtr ptrText = Marshal.AllocHGlobal(textLen + 5);
                Win32.SendMessage(hCurrScintilla, SciMsg.SCI_GETTEXT, textLen + 1, ptrText);
                string s = Marshal.PtrToStringAnsi(ptrText);
                s = s.Trim();
                List<Error> errors = new List<Error>();
                string[] lines = s.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None);
                Error error = null;
                //Marshal.FreeHGlobal(ptrText);


                for (int i = 0; i < lines.Length; i++)
                {
                    try
                    {
                        error = Helper.CheckLine(lines[i], i);
                    }
                    catch (Exception)
                    {
                    }
                    if (error != null)
                    {
                        errors.Add(error);
                    }
                }

                myDockableDialog(errors);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString());
            }
        }

        internal static void updateUI(List<Error> errors)
        {
            try
            {
                frmMyDlg.Results.Nodes.Clear();
                if (errors.Count == 0)
                {
                    TreeNode tn = new TreeNode("No Errors Found!");
                    tn.ForeColor = Color.Green;
                    frmMyDlg.Results.Nodes.Add(tn);
                }
                foreach (Error e in errors)
                {
                    TreeNode tn = new TreeNode("At Line: " + e.Line + "     " + e.Query);
                    tn.ForeColor = Color.DarkRed;
                    tn.Tag = e;
                    if (e.Syntax != null && e.Syntax.Length > 0)
                    {
                        tn.Nodes.Add(e.Syntax);
                    }

                    if (e.Identifiers != null && e.Identifiers.Length > 0)
                    {
                        tn.Nodes.Add(e.Identifiers);
                    }
                    frmMyDlg.Results.Nodes.Add(tn);
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString());
            }
        }

        internal static void myDockableDialog(List<Error> errors)
        {
            if (frmMyDlg == null || frmMyDlg.IsDisposed)
            {
                frmMyDlg = new frmMyDlg();

                using (Bitmap newBmp = new Bitmap(16, 16))
                {
                    Graphics g = Graphics.FromImage(newBmp);
                    ColorMap[] colorMap = new ColorMap[1];
                    colorMap[0] = new ColorMap();
                    colorMap[0].OldColor = Color.Fuchsia;
                    colorMap[0].NewColor = Color.FromKnownColor(KnownColor.ButtonFace);
                    ImageAttributes attr = new ImageAttributes();
                    attr.SetRemapTable(colorMap);
                    g.DrawImage(tbBmp_tbTab, new Rectangle(0, 0, 16, 16), 0, 0, 16, 16, GraphicsUnit.Pixel, attr);
                    tbIcon = Icon.FromHandle(newBmp.GetHicon());
                }

                updateUI(errors);

                NppTbData _nppTbData = new NppTbData();
                _nppTbData.hClient = frmMyDlg.Handle;
                _nppTbData.pszName = "NipCheck Console";
                _nppTbData.dlgID = idMyDlg;
                _nppTbData.uMask = NppTbMsg.DWS_DF_CONT_RIGHT | NppTbMsg.DWS_ICONTAB | NppTbMsg.DWS_ICONBAR;
                _nppTbData.hIconTab = (uint)tbIcon.Handle;
                _nppTbData.pszModuleName = PluginName;
                IntPtr _ptrNppTbData = Marshal.AllocHGlobal(Marshal.SizeOf(_nppTbData));
                Marshal.StructureToPtr(_nppTbData, _ptrNppTbData, false);

                Win32.SendMessage(PluginBase.nppData._nppHandle, (uint) NppMsg.NPPM_DMMREGASDCKDLG, 0, _ptrNppTbData);
            }
            else
            {
                updateUI(errors);
                Win32.SendMessage(PluginBase.nppData._nppHandle, (uint) NppMsg.NPPM_DMMSHOW, 0, frmMyDlg.Handle);
            }
        }
    }
}