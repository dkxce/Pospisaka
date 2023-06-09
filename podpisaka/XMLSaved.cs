﻿//
// C#
// DigitalCertAndSignMaker
// v 0.28, 12.04.2023
// https://github.com/dkxce/Pospisaka
// en,ru,1251,utf-8
//

using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;

namespace System.Xml
{

    [Serializable]
    public class XMLSaved<T>
    {
        /// <summary>
        ///     Сохранение структуры в файл
        /// </summary>
        /// <param name="file">Полный путь к файлу</param>
        /// <param name="obj">Структура</param>
        public static void Save(string file, T obj)
        {
            XmlSerializerNamespaces ns = new XmlSerializerNamespaces(); ns.Add("", "");
            System.Xml.Serialization.XmlSerializer xs = new System.Xml.Serialization.XmlSerializer(typeof(T));
            System.IO.StreamWriter writer = System.IO.File.CreateText(file);
            xs.Serialize(writer, obj, ns);
            writer.Flush();
            writer.Close();
        }

        public static void SaveHere(string file, T obj)
        {
            Save(System.IO.Path.Combine(CurrentDirectory(), file), obj);
        }

        public static string Save(T obj)
        {
            XmlSerializerNamespaces ns = new XmlSerializerNamespaces(); ns.Add("", "");
            System.Xml.Serialization.XmlSerializer xs = new System.Xml.Serialization.XmlSerializer(typeof(T));
            System.IO.MemoryStream ms = new MemoryStream();
            System.IO.StreamWriter writer = new StreamWriter(ms);
            xs.Serialize(writer, obj, ns);
            writer.Flush();
            ms.Position = 0;
            byte[] bb = new byte[ms.Length];
            ms.Read(bb, 0, bb.Length);
            writer.Close();
            return System.Text.Encoding.UTF8.GetString(bb); ;
        }

        /// <summary>
        ///     Подключение структуры из файла
        /// </summary>
        /// <param name="file">Полный путь к файлу</param>
        /// <returns>Структура</returns>
        public static T Load(string file)
        {
            try
            {
                System.Xml.Serialization.XmlSerializer xs = new System.Xml.Serialization.XmlSerializer(typeof(T));
                System.IO.StreamReader reader = System.IO.File.OpenText(file);
                T c = (T)xs.Deserialize(reader);
                reader.Close();
                return c;
            }
            catch { };
            {
                Type type = typeof(T);
                System.Reflection.ConstructorInfo c = type.GetConstructor(new Type[0]);
                return (T)c.Invoke(null);
            };
        }

        public static T Load(byte[] data)
        {
            try
            {
                System.Xml.Serialization.XmlSerializer xs = new System.Xml.Serialization.XmlSerializer(typeof(T));
                MemoryStream ms = new MemoryStream(data);
                System.IO.StreamReader reader = new StreamReader(ms);
                T c = (T)xs.Deserialize(reader);
                reader.Close();
                ms.Close();
                return c;
            }
            catch { };
            {
                Type type = typeof(T);
                System.Reflection.ConstructorInfo c = type.GetConstructor(new Type[0]);
                return (T)c.Invoke(null);
            };
        }

        public static T Load(byte[] data, int index, int count)
        {
            try
            {
                System.Xml.Serialization.XmlSerializer xs = new System.Xml.Serialization.XmlSerializer(typeof(T));
                MemoryStream ms = new MemoryStream(data, index, count);
                System.IO.StreamReader reader = new StreamReader(ms);
                T c = (T)xs.Deserialize(reader);
                reader.Close();
                ms.Close();
                return c;
            }
            catch { };
            {
                Type type = typeof(T);
                System.Reflection.ConstructorInfo c = type.GetConstructor(new Type[0]);
                return (T)c.Invoke(null);
            };
        }

        public static T LoadHere(string file)
        {
            return Load(System.IO.Path.Combine(CurrentDirectory(), file));
        }

        public static T Load()
        {
            try { return Load(CurrentDirectory() + @"\config.xml"); }
            catch { };
            Type type = typeof(T);
            System.Reflection.ConstructorInfo c = type.GetConstructor(new Type[0]);
            return (T)c.Invoke(null);
        }

        public static string CurrentDirectory()
        {
            return AppDomain.CurrentDomain.BaseDirectory;
            // return Application.StartupPath;
            // return System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            // return System.IO.Directory.GetCurrentDirectory();
            // return Environment.CurrentDirectory;
            // return System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase);
            // return System.IO.Path.GetDirectory(Application.ExecutablePath);
        }
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class XmlCommentAttribute : Attribute
    {
        public XmlCommentAttribute(string value) { this.Value = value; }
        public string Value { get; set; }
    }

    public static class XmlCommentExtensions
    {
        const string XmlCommentPropertyPostfix = "XmlComment";

        static XmlCommentAttribute GetXmlCommentAttribute(this Type type, string memberName)
        {
            PropertyInfo member = type.GetProperty(memberName);
            if (member == null) return null;
            XmlCommentAttribute attr = member.GetCustomAttribute<XmlCommentAttribute>();
            return attr;
        }

        public static XmlComment GetXmlComment(this Type type, [CallerMemberName] string memberName = "")
        {
            XmlCommentAttribute attr = GetXmlCommentAttribute(type, memberName);
            if (attr == null && memberName.EndsWith(XmlCommentPropertyPostfix))
                    attr = GetXmlCommentAttribute(type, memberName.Substring(0, memberName.Length - XmlCommentPropertyPostfix.Length));
            if (attr == null || string.IsNullOrEmpty(attr.Value)) return null;
            return new XmlDocument().CreateComment(attr.Value);
        }
    }
}