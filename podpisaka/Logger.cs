using System;
using System.IO;
using System.Text;
using System.Xml;

namespace podpisaka
{
    public class Logger
    {
        private const int MaxLineLength = 2 * 1024 * 1024 /* MB */;
        private static string fileName = Path.Combine(XMLSaved<int>.CurrentDirectory(), "log.txt");

        public static void AddLine(string text, bool addDT = true)
        {
            try { Logger.Write(text, addDT); } catch { };
        }

        private static void Write(string text, bool addDT = true)
        {            
            text = text?.Replace("\r", "").Replace("\n", " ") + "\r\n";
            if (addDT) text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss (dddd)") + ": " + text;

            Shrink();
            FileStream fs = null;                       
            try // Write
            {
                fs = new FileStream(fileName, FileMode.Append, FileAccess.Write);                
                byte[] data = Encoding.GetEncoding(1251).GetBytes(text);
                fs.Write(data, 0, data.Length);
                fs.Flush();
            }
            catch { }
            finally { if (fs != null) fs.Close(); };
        }

        private static void Shrink()
        {
            FileStream fs = null;

            try // Shrink if oversize
            {
                if ((new FileInfo(fileName)).Length > MaxLineLength)
                {
                    int index = 0;
                    byte[] buffer = new byte[MaxLineLength / 2];
                    using (fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
                    {
                        fs.Position = fs.Length - buffer.Length;
                        fs.Read(buffer, 0, buffer.Length);
                    };
                    while (index < buffer.Length && Encoding.ASCII.GetString(buffer, index, 4) != "\r\n\r\n") index++;
                    if (index > 0) index += 4;
                    using (fs = new FileStream(fileName, FileMode.Create, FileAccess.Write))
                    {
                        fs.Write(buffer, index, buffer.Length - index);
                    };
                };
            }
            catch { };
        }

        public static string GetText()
        {
            FileStream fs = null;
            try
            {
                fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);
                byte[] data = new byte[fs.Length];
                fs.Read(data, 0, data.Length);
                fs.Flush();
                return Encoding.GetEncoding(1251).GetString(data);
            }
            catch { }
            finally { if (fs != null) fs.Close(); };
            return "";
        }
    }
}
