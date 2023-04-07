using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace DigitalCertAndSignMaker
{
    public class FileAss
    {        
        public static void SetFileAssociation(string Extension, string Class, string Command, string ExePath)
        {
            string cls = null;
            SetFileAssociation(Extension, ref cls, Command, ExePath, Command);
        }

        public static void SetFileAssociation(string Extension, string Class, string Command, string ExePath, string commandName)
        {
            string cls = null;
            SetFileAssociation(Extension, ref cls, Command, ExePath, commandName);
        }

        public static void SetFileAssociation(string Extension, ref string Class, string Command, string ExePath)
        {
            SetFileAssociation(Extension, ref Class, Command, ExePath, Command);
        }

        public static void SetFileAssociation(string Extension, ref string Class, string Command, string ExePath, string commandName)
        {
            if (string.IsNullOrEmpty(commandName)) commandName = Command;
            Extension = Extension.Trim('.').ToLower();

            try
            {
                Registry.CurrentUser.OpenSubKey("SOFTWARE\\Classes\\", true)
                    .CreateSubKey("." + Extension)
                    .CreateSubKey("OpenWithList")
                    .CreateSubKey(Path.GetFileName(ExePath))
                    .SetValue("", "\"" + ExePath + "\"" + " \"%1\"");

                if (string.IsNullOrEmpty(Class))
                    using (RegistryKey User_Ext = Registry.ClassesRoot.CreateSubKey("." + Extension))
                        Class = User_Ext.GetValue("", "").ToString();
                if (string.IsNullOrEmpty(Class))
                    using (RegistryKey User_Ext = Registry.CurrentUser.CreateSubKey("SOFTWARE\\Classes\\." + Extension))
                        Class = User_Ext.GetValue("", "").ToString();

                if (string.IsNullOrEmpty(Class)) Class = Extension.ToUpper() + "File";

                using (RegistryKey User_Ext = Registry.ClassesRoot.CreateSubKey("." + Extension))
                    User_Ext.SetValue("", Class);
                using (RegistryKey User_Ext = Registry.CurrentUser.CreateSubKey("SOFTWARE\\Classes\\." + Extension))
                    User_Ext.SetValue("", Class);

                using (RegistryKey User_Classes = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Classes\\", true))
                using (RegistryKey User_FileClass = User_Classes.CreateSubKey(Class))
                using (RegistryKey User_Command = User_FileClass.CreateSubKey("shell").CreateSubKey(Command))
                using (RegistryKey User_Execute = User_Command.CreateSubKey("command"))
                {
                    User_Execute.SetValue("", "\"" + ExePath + "\"" + " \"%1\"");
                    User_Command.SetValue("", commandName);
                    User_Command.SetValue("Icon", ExePath);
                };

                using (RegistryKey User_FileClass = Registry.ClassesRoot.CreateSubKey(Class, true))
                using (RegistryKey User_Command = User_FileClass.CreateSubKey("shell").CreateSubKey(Command))
                using (RegistryKey User_Execute = User_Command.CreateSubKey("command"))
                {
                    User_Execute.SetValue("", "\"" + ExePath + "\"" + " \"%1\"");
                    User_Command.SetValue("", commandName);
                    User_Command.SetValue("Icon", ExePath);
                };
            }
            catch (Exception excpt)
            {
                //Your code here
            }
        }

        public static void SetFileOpenWith(string Extension, string ExePath)
        {
            try
            {
                Registry.CurrentUser.OpenSubKey("SOFTWARE\\Classes\\", true)
                    .CreateSubKey("." + Extension)
                    .CreateSubKey("OpenWithList")
                    .CreateSubKey(Path.GetFileName(ExePath))
                    .SetValue("", "\"" + ExePath + "\"" + " \"%1\"");

            }
            catch (Exception ex)
            {
            };
        }

        public static void UpdateExplorer()
        {
            try { SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero); } catch { };
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
    }
}
