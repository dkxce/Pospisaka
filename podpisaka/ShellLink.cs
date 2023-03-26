﻿//
// C# (WinAPI)
// MSol.MachineManager.ShellLink
// v 0.1, 16.09.2022
// artem.karimov@weadmire.io
// en,ru,1251,utf-8
//

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace DigitalCertAndSignMaker
{
    /// <remarks>
    ///     http://smdn.jp/programming/tips/createlnk/
    ///     http://www.vbaccelerator.com/home/NET/Code/Libraries/Shell_Projects/Creating_and_Modifying_Shortcuts/article.asp
    /// </remarks>
    public class ShellLink : IDisposable
    {
        #region WinAPI

        #region DLL Calls

        [DllImport("ole32.dll", PreserveSig = false)]
        private static extern void PropVariantClear([In][Out] PropVariant pvar); // Or ref

        #endregion DLL Calls

        #region Com Import

        /// <summary>
        ///     ShellLink CoClass (Shell link object)
        /// </summary>
        [ComImport]
        [Guid("00021401-0000-0000-C000-000000000046")]
        [ClassInterface(ClassInterfaceType.None)]
        private class CShellLink { }

        /// <summary>
        ///     IPropertyStore Interface
        /// </summary>
        [ComImport]
        [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPropertyStore
        {
            uint GetCount([Out] out uint cProps);
            uint GetAt([In] uint iProp, out PropertyKey pkey);
            uint GetValue([In] ref PropertyKey key, [Out] PropVariant pv);
            uint SetValue([In] ref PropertyKey key, [In] PropVariant pv);
            uint Commit();
        }

        /// <summary>
        ///     IShellLink Interface
        /// </summary>
        [ComImport]
        [Guid("000214F9-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellLinkW
        {
            uint GetPath([Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, ref WIN32_FIND_DATAW pfd, SLGP fFlags);
            uint GetIDList(out IntPtr ppidl);
            uint SetIDList(IntPtr pidl);
            uint GetDescription([Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
            uint SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            uint GetWorkingDirectory([Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
            uint SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            uint GetArguments([Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
            uint SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            uint GetHotKey(out ushort pwHotkey);
            uint SetHotKey(ushort wHotKey);
            uint GetShowCmd(out ShowWindow piShowCmd);
            uint SetShowCmd(ShowWindow iShowCmd);
            uint GetIconLocation([Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
            uint SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            uint SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
            uint Resolve(IntPtr hwnd, uint fFlags);
            uint SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        #endregion Com Import

        #region Structs and Classes

        /// <summary>
        ///     PropertyKey Structure
        /// </summary>
        /// <remarks>
        ///     Narrowed down from PropertyKey.cs of Windows API Code Pack 1.1
        /// </remarks>
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct PropertyKey
        {
            public Guid FormatId { get; }
            public int PropertyId { get; }
            public PropertyKey(string formatId, int propertyId) { FormatId = new Guid(formatId); PropertyId = propertyId; }
        }

        /// <summary>
        ///     WIN32_FIND_DATAW Structure
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)]
        [Serializable]
        private struct WIN32_FIND_DATAW
        {
            public uint dwFileAttributes;
            public System.Runtime.InteropServices.FILETIME ftCreationTime;
            public System.Runtime.InteropServices.FILETIME ftLastAccessTime;
            public System.Runtime.InteropServices.FILETIME ftLastWriteTime;
            public uint nFileSizeHigh;
            public uint nFileSizeLow;
            public uint dwReserved0;
            public uint dwReserved1;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)]
            public string cFileName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            public string cAlternateFileName;
        }

        /// <summary>
        ///     PropVariant Class (only for limited types)
        /// </summary>
        /// <remarks>
        ///     Narrowed down from PropVariant.cs of Windows API Code Pack 1.1
        ///     Originally from https://blogs.msdn.microsoft.com/adamroot/2008/04/11/interop-with-propvariants-in-net/
        /// </remarks>
        [StructLayout(LayoutKind.Explicit)]
        private sealed class PropVariant : IDisposable
        {
            // [FieldOffset(2)]
            // private ushort wReserved1;
            // [FieldOffset(4)]
            // private ushort wReserved2;
            // [FieldOffset(6)]
            // private ushort wReserved3;

            [FieldOffset(8)] private IntPtr value;
            [FieldOffset(0)] private ushort valueType;
            public PropVariant() { }

            /// <summary>
            ///     Constructor with string value
            /// </summary>
            /// <param name="value">String value</param>
            public PropVariant(string value)
            {
                if (value == null) throw new ArgumentNullException(nameof(value));
                valueType = (ushort)VarEnum.VT_LPWSTR;
                this.value = Marshal.StringToCoTaskMemUni(value);
            }

            /// <summary>
            ///     Constructor with CLSID value
            /// </summary>
            /// <param name="value">CLSID value</param>
            public PropVariant(Guid value)
            {
                if (value == Guid.Empty) throw new ArgumentNullException(nameof(value));
                valueType = (ushort)VarEnum.VT_CLSID;
                this.value = Marshal.AllocCoTaskMem(Marshal.SizeOf(value));
                Marshal.StructureToPtr(value, this.value, false);
            }

            /// <summary>
            ///     Value (only for limited types)
            /// </summary>
            public object Value
            {
                get
                {
                    switch ((VarEnum)valueType)
                    {
                        case VarEnum.VT_LPWSTR: return Marshal.PtrToStringUni(value);
                        case VarEnum.VT_CLSID: return Marshal.PtrToStructure<Guid>(value);
                        default: return null; // VT_EMPTY and so on                            
                    };
                }
            }

            public void Dispose()
            {
                PropVariantClear(this);
                GC.SuppressFinalize(this);
            }

            ~PropVariant()
            {
                Dispose();
            }
        }

        #endregion Structs and Classes

        #region Enums

        /// <summary>
        ///     SLGP Flags
        /// </summary>
        private enum SLGP : uint
        {
            SLGP_SHORTPATH = 0x1,
            SLGP_UNCPRIORITY = 0x2,
            SLGP_RAWPATH = 0x4,
            SLGP_RELATIVEPRIORITY = 0x8
        }

        /// <summary>
        ///     STGM Constants
        /// </summary>
        private enum STGM
        {
            STGM_READ = 0x00000000,
            STGM_WRITE = 0x00000001,
            STGM_READWRITE = 0x00000002,
            STGM_SHARE_DENY_NONE = 0x00000040,
            STGM_SHARE_DENY_READ = 0x00000030,
            STGM_SHARE_DENY_WRITE = 0x00000020,
            STGM_SHARE_EXCLUSIVE = 0x00000010,
            STGM_PRIORITY = 0x00040000,
            STGM_CREATE = 0x00001000,
            STGM_CONVERT = 0x00020000,
            STGM_FAILIFTHERE = 0x00000000,
            STGM_DIRECT = 0x00000000,
            STGM_TRANSACTED = 0x00010000,
            STGM_NOSCRATCH = 0x00100000,
            STGM_NOSNAPSHOT = 0x00200000,
            STGM_SIMPLE = 0x08000000,
            STGM_DIRECT_SWMR = 0x00400000,
            STGM_DELETEONRELEASE = 0x04000000
        }

        [Flags]
        private enum SLDF
        {
            SLDF_DEFAULT = 0x00000000,
            SLDF_HAS_ID_LIST = 0x00000001,
            SLDF_HAS_LINK_INFO = 0x00000002,
            SLDF_HAS_NAME = 0x00000004,
            SLDF_HAS_RELPATH = 0x00000008,
            SLDF_HAS_WORKINGDIR = 0x00000010,
            SLDF_HAS_ARGS = 0x00000020,
            SLDF_HAS_ICONLOCATION = 0x00000040,
            SLDF_UNICODE = 0x00000080,
            SLDF_FORCE_NO_LINKINFO = 0x00000100,
            SLDF_HAS_EXP_SZ = 0x00000200,
            SLDF_RUN_IN_SEPARATE = 0x00000400,
            SLDF_HAS_LOGO3ID = 0x00000800,
            SLDF_HAS_DARWINID = 0x00001000,
            SLDF_RUNAS_USER = 0x00002000,
            SLDF_HAS_EXP_ICON_SZ = 0x00004000,
            SLDF_NO_PIDL_ALIAS = 0x00008000,
            SLDF_FORCE_UNCNAME = 0x00010000,
            SLDF_RUN_WITH_SHIMLAYER = 0x00020000,
            SLDF_FORCE_NO_LINKTRACK = 0x00040000,
            SLDF_ENABLE_TARGET_METADATA = 0x00080000,
            SLDF_DISABLE_LINK_PATH_TRACKING = 0x00100000,
            SLDF_DISABLE_KNOWNFOLDER_RELATIVE_TRACKING = 0x00200000,
            SLDF_NO_KF_ALIAS = 0x00400000,
            SLDF_ALLOW_LINK_TO_LINK = 0x00800000,
            SLDF_UNALIAS_ON_SAVE = 0x01000000,
            SLDF_PREFER_ENVIRONMENT_PATH = 0x02000000,
            SLDF_KEEP_LOCAL_IDLIST_FOR_UNC_TARGET = 0x04000000,
            SLDF_PERSIST_VOLUME_ID_RELATIVE = 0x08000000,
            SLDF_VALID = 0x003FF7FF,
            SLDF_RESERVED
        }

        #endregion Enums        

        #endregion WinAPI

        private const int MAX_ARGUMENTS_LENGTH = 512;
        private const int MAX_DESCRIPTION_LENGTH = 512;
        private const int MAX_PATH = 512;

        public enum ShowWindow
        {
            SW_HIDE = 0,
            SW_SHOWNORMAL = 1,
            SW_SHOWMINIMIZED = 2,
            SW_SHOWMAXIMIZED = 3,
            SW_SHOWNOACTIVATE = 4,
            SW_SHOW = 5,
            SW_MINIMIZE = 6,
            SW_SHOWMINNOACTIVE = 7,
            SW_SHOWNA = 8,
            SW_RESTORE = 9,
            SW_SHOWDEFAULT = 10
        }

        /// <summary>
        ///     Property key of Arguments
        /// </summary>
        /// <remarks>
        ///     Name = System.Link.Arguments
        ///     ShellPKey = PKEY_Link_Arguments
        ///     FormatID = 436F2667-14E2-4FEB-B30A-146C53B5B674
        ///     PropID = 100
        ///     Type = String (VT_LPWSTR)
        /// </remarks>
        private static readonly PropertyKey ArgumentsKey = new PropertyKey("{436F2667-14E2-4FEB-B30A-146C53B5B674}", 100);

        /// <summary>
        ///     Property key of AppUserModelID
        /// </summary>
        /// <remarks>
        ///     Name = System.AppUserModel.ID
        ///     ShellPKey = PKEY_AppUserModel_ID
        ///     FormatID = 9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3
        ///     PropID = 5
        ///     Type = String (VT_LPWSTR)
        /// </remarks>
        private static readonly PropertyKey AppUserModelIDKey = new PropertyKey("{9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3}", 5);

        /// <summary>
        ///     Property key of AppUserModelToastActivatorCLSID
        /// </summary>
        /// <remarks>
        ///     Name = System.AppUserModel.ToastActivatorCLSID
        ///     ShellPKey = PKEY_AppUserModel_ToastActivatorCLSID
        ///     FormatID = 9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3
        ///     PropID = 26
        ///     Type = Guid (VT_CLSID)
        ///     Taken from propkey.h of Windows SDK
        /// </remarks>
        private static readonly PropertyKey AppUserModelToastActivatorClsidKey = new PropertyKey("{9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3}", 26);

        private IShellLinkW _ShellLink;
        private string shortcutPath;

        public ShellLink() : this(null) { }

        public ShellLink(string name, Environment.SpecialFolder path) : this(Path.Combine(Environment.GetFolderPath(path), Path.GetFileNameWithoutExtension(name) + ".lnk")) { }

        public ShellLink(string fileName)
        {
            try { _ShellLink = (IShellLinkW)new CShellLink(); }
            catch (Exception ex) { throw new COMException("Failed to create Shell link object.", ex); };
            if (string.IsNullOrEmpty(fileName)) return;
            if (File.Exists(shortcutPath = fileName)) Load(shortcutPath);
        }

        public static ShellLink LoadFromFile(string fileName)
        {
            ShellLink res = new ShellLink();
            try { res._ShellLink = (IShellLinkW)new CShellLink(); }
            catch (Exception ex) { throw new COMException("Failed to create Shell link object.", ex); };
            res.Load(fileName);
            return res;
        }

        private IPersistFile PersistFile
        {
            get
            {
                if (!(_ShellLink is IPersistFile pf)) throw new COMException("Failed to create IPersistFile.");
                return pf;
            }
        }

        private IPropertyStore PropertyStore
        {
            get
            {
                if (!(_ShellLink is IPropertyStore ps)) throw new COMException("Failed to create IPropertyStore.");
                return ps;
            }
        }

        /// <summary>
        ///     Shortcut file path
        /// </summary>
        public string ShortcutPath
        {
            get
            {
                PersistFile.GetCurFile(out string buff);
                if (string.IsNullOrEmpty(buff)) return shortcutPath;
                return buff;
            }
        }

        /// <summary>
        ///     Target file path
        /// </summary>
        /// <remarks>This length is limited to maximum path length limitation (260) - last null (1).</remarks>
        public string TargetPath
        {
            get
            {
                StringBuilder sb = new StringBuilder(MAX_PATH - 1);
                WIN32_FIND_DATAW data = new WIN32_FIND_DATAW();
                VerifySucceeded(_ShellLink.GetPath(sb, sb.Capacity, ref data, SLGP.SLGP_UNCPRIORITY));
                return sb.ToString();
            }
            set
            {
                if (value != null && MAX_PATH - 1 < value.Length) throw new ArgumentException("Target file path is too long.", nameof(TargetPath));
                VerifySucceeded(_ShellLink.SetPath(value));
                WorkingDirectory = Path.GetDirectoryName(value);
            }
        }

        /// <summary>
        ///     Arguments
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         According to MSDN, this length should not have a limitation as long as it in Unicode.
        ///         In addition, it is recommended to retrieve argument strings though IPropertyStore rather than
        ///         GetArguments method.
        ///     </para>
        ///     <para>
        ///         The setter accepts Null while the getter never returns Null. This behavior is the same
        ///         as other properties by IShellLink.
        ///     </para>
        /// </remarks>
        public string Arguments
        {
            get
            {
                //StringBuilder sb = new StringBuilder(1024);
                //VerifySucceeded(_ShellLink.GetArguments(sb, sb.Capacity));
                //return sb.ToString();

                using (PropVariant pv = new PropVariant())
                {
                    VerifySucceeded(PropertyStore.GetValue(ArgumentsKey, pv));
                    return pv.Value as string ?? string.Empty;
                };
            }
            set
            {
                if (value != null && MAX_ARGUMENTS_LENGTH - 1 < value.Length) throw new ArgumentException("Arguments length is too big.", nameof(Arguments));
                VerifySucceeded(_ShellLink.SetArguments(value));
            }
        }

        /// <summary>
        ///     Description
        /// </summary>
        /// <remarks>
        ///     According to MSDN, this length is limited to INFOTIPSIZE. However, in practice,
        ///     there seems to be the same limitation as the maximum path length limitation. Moreover,
        ///     Description longer than the limitation will screw up arguments.
        /// </remarks>
        public string Description
        {
            get
            {
                StringBuilder sb = new StringBuilder(MAX_DESCRIPTION_LENGTH);
                VerifySucceeded(_ShellLink.GetDescription(sb, sb.Capacity));
                return sb.ToString();
            }
            set
            {
                if (value != null && MAX_DESCRIPTION_LENGTH < value.Length) throw new ArgumentException("Description is too long.", nameof(Description));
                VerifySucceeded(_ShellLink.SetDescription(value));
            }
        }

        /// <summary>
        ///     Working directory
        /// </summary>
        /// <remarks>This length is limited to maximum path length limitation (260) - last null (1).</remarks>
        public string WorkingDirectory
        {
            get
            {
                StringBuilder sb = new StringBuilder(MAX_PATH - 1);
                VerifySucceeded(_ShellLink.GetWorkingDirectory(sb, sb.Capacity));
                return sb.ToString();
            }
            set
            {
                if (value != null && MAX_PATH - 1 < value.Length) throw new ArgumentException("Working directory is too long.", nameof(WorkingDirectory));
                VerifySucceeded(_ShellLink.SetWorkingDirectory(value));
            }
        }

        public ushort HotKey
        {
            get
            {
                _ShellLink.GetHotKey(out ushort res);
                return res;
            }
            set
            {
                _ShellLink.SetHotKey(value);
            }
        }

        public System.Windows.Forms.Keys HotKeys
        {
            get
            {
                ushort hk = HotKey;
                System.Windows.Forms.Keys res = (System.Windows.Forms.Keys)(hk & 0xFF);
                if ((hk & 0x0100) == 0x0100) res |= System.Windows.Forms.Keys.Shift;
                if ((hk & 0x0200) == 0x0200) res |= System.Windows.Forms.Keys.Control;
                if ((hk & 0x0400) == 0x0400) res |= System.Windows.Forms.Keys.Alt;
                return res;
            }
            set
            {
                uint val = (uint)value;
                ushort hk = (ushort)(val & 0xFF);
                if ((val & ((uint)System.Windows.Forms.Keys.Shift)) == ((uint)System.Windows.Forms.Keys.Shift)) hk += 0x0100;
                if ((val & ((uint)System.Windows.Forms.Keys.Control)) == ((uint)System.Windows.Forms.Keys.Control)) hk += 0x0200;
                if ((val & ((uint)System.Windows.Forms.Keys.Alt)) == ((uint)System.Windows.Forms.Keys.Alt)) hk += 0x0400;
                HotKey = hk;
            }
        }

        public void SetHotKey(char symbol, bool shift, bool control, bool alt)
        {
            ushort hk = (ushort)((byte)symbol & 0xFF);
            if (shift) hk += 0x0100;
            if (control) hk += 0x0200;
            if (alt) hk += 0x0400;
            HotKey = hk;
        }

        public (char symbol, bool shift, bool control, bool alt) GetHotKey()
        {
            ushort hk = HotKey;
            return ((char)(hk & 0xFF), (hk & 0x0100) == 0x0100, (hk & 0x0200) == 0x0200, (hk & 0x0400) == 0x0400);
        }

        public void GetHotKey(out char symbol, out bool shift, out bool control, out bool alt)
        {
            ushort hk = HotKey;
            symbol = (char)(hk & 0xFF);
            shift = (hk & 0x0100) == 0x0100;
            control = (hk & 0x0200) == 0x0200;
            alt = (hk & 0x0400) == 0x0400;
        }

        /// <summary>
        ///     Window style
        /// </summary>
        public ShowWindow WindowStyle
        {
            get
            {
                VerifySucceeded(_ShellLink.GetShowCmd(out var showCmd));
                return showCmd;
            }
            set
            {
                VerifySucceeded(_ShellLink.SetShowCmd(value));
            }
        }

        /// <summary>
        ///     Shortcut icon file path (Path element of icon location)
        /// </summary>
        /// <remarks>This length is limited to the maximum path length limitation (260) - last null (1).</remarks>
        public string IconPath
        {
            get
            {
                StringBuilder sb = new StringBuilder(MAX_PATH - 1);
                VerifySucceeded(_ShellLink.GetIconLocation(sb, sb.Capacity, out _));
                return sb.ToString();
            }
            set
            {
                if (value != null && MAX_PATH - 1 < value.Length) throw new ArgumentException("Shortcut icon file path is too long.", nameof(IconPath));
                VerifySucceeded(_ShellLink.SetIconLocation(value, IconIndex));
            }
        }

        /// <summary>
        ///     Shortcut icon index (Index element of icon location)
        /// </summary>
        public int IconIndex
        {
            get
            {
                StringBuilder sb = new StringBuilder(MAX_PATH);
                VerifySucceeded(_ShellLink.GetIconLocation(sb, sb.Capacity, out var index));
                return index;
            }
            set
            {
                int index = 0 <= value ? value : 0;
                VerifySucceeded(_ShellLink.SetIconLocation(IconPath, index));
            }
        }

        /// <summary>
        ///     AppUserModelID (to be used for Windows 7 or newer)
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         According to MSDN, an AppUserModelID must be in the following form:
        ///         CompanyName.ProductName.SubProduct.VersionInformation
        ///         It can have no more than 128 characters and cannot contain spaces. Each section should be
        ///         camel-cased. CompanyName and ProductName should always be used, while SubProduct and
        ///         VersionInformation are optional.
        ///     </para>
        ///     <para>
        ///         The setter accepts Null while the getter never returns Null. This behavior is the same
        ///         as other properties by IShellLink.
        ///     </para>
        /// </remarks>
        public string AppUserModelID
        {
            get
            {
                using (PropVariant pv = new PropVariant())
                {
                    VerifySucceeded(PropertyStore.GetValue(AppUserModelIDKey, pv));
                    return pv.Value as string ?? string.Empty;
                };
            }
            set
            {
                string buff = value ?? string.Empty;
                if (128 < buff.Length) throw new ArgumentException("AppUserModelID is too long.", nameof(AppUserModelID));
                using (PropVariant pv = new PropVariant(buff))
                {
                    VerifySucceeded(PropertyStore.SetValue(AppUserModelIDKey, pv));
                    VerifySucceeded(PropertyStore.Commit());
                };
            }
        }

        /// <summary>
        ///     AppUserModelToastActivatorCLSID (to be used for Windows 10 or newer)
        /// </summary>
        public Guid AppUserModelToastActivatorCLSID
        {
            get
            {
                using (PropVariant pv = new PropVariant())
                {
                    VerifySucceeded(PropertyStore.GetValue(AppUserModelToastActivatorClsidKey, pv));
                    return pv.Value is Guid guid ? guid : Guid.Empty;
                };
            }
            set
            {
                using (PropVariant pv = new PropVariant(value))
                {
                    VerifySucceeded(PropertyStore.SetValue(AppUserModelToastActivatorClsidKey, pv));
                    VerifySucceeded(PropertyStore.Commit());
                };
            }
        }

        public void Dispose()
        {
            if (_ShellLink != null)
            {
                Marshal.FinalReleaseComObject(_ShellLink);
                _ShellLink = null;
            };
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Load shortcut file.
        /// </summary>
        /// <param name="shortcutPath">Shortcut file path</param>
        public void Load(string shortcutPath)
        {
            if (string.IsNullOrWhiteSpace(shortcutPath)) throw new ArgumentNullException(nameof(shortcutPath));
            if (!File.Exists(shortcutPath)) throw new FileNotFoundException("Shortcut file is not found.", shortcutPath);
            PersistFile.Load(shortcutPath, (int)STGM.STGM_READ);
        }

        /// <summary>
        ///     Save shortcut file.
        /// </summary>
        public void Save()
        {
            Save(ShortcutPath);
        }

        /// <summary>
        ///     Save shortcut file.
        /// </summary>
        /// <param name="shortcutPath">Shortcut file path</param>
        public void Save(string shortcutPath)
        {
            if (string.IsNullOrWhiteSpace(shortcutPath)) throw new ArgumentNullException(nameof(shortcutPath));
            if (Path.GetDirectoryName(shortcutPath) is string directory) Directory.CreateDirectory(directory);
            PersistFile.Save(shortcutPath, true);
        }

        public void Save(string name, Environment.SpecialFolder path)
        {
            string shortcutPath = Path.Combine(Environment.GetFolderPath(path), Path.GetFileNameWithoutExtension(name) + ".lnk");
            Save(shortcutPath);
        }

        /// <summary>
        ///     Verify if operation succeeded.
        /// </summary>
        /// <param name="hresult">HRESULT</param>
        /// <remarks>This method is from Sending toast notifications from desktop apps sample.</remarks>
        private void VerifySucceeded(uint hresult)
        {
            if (hresult > 1) throw new Exception("Failed with HRESULT: " + hresult.ToString("X"));
        }

        ~ShellLink() { Dispose(); }
    }
}