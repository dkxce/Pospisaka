//
// C#
// DigitalCertAndSignMaker
// v 0.28, 12.04.2023
// https://github.com/dkxce/Pospisaka
// en,ru,1251,utf-8
//


using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace DigitalCertAndSignMaker
{
    public class IniFile: XMLSaved<IniFile>
    {        
        [XmlElement("Maximized")]
        public bool Maximized = false;

        [XmlElement("LastPageSelected")]
        public int lastPageSelected = 0;

        [XmlElement("LastStorageSelected")]
        public int lastStorageSelected = 3;

        [XmlElement("CheckCertificateValidity")]
        public bool checkCertValid = false;

        [XmlElement("SignatureCreationMethod")]
        public byte signCreateMethod = 3;

        [XmlElement("CurrentCertificate")]
        public string currentCertificate = null;

        [XmlArray("CertificatesFavorites")]
        public List<dkxce.CertificateHash> Favorites = new List<dkxce.CertificateHash>();

        [XmlArray("CertificatesFilesHash")]
        public List<dkxce.CertificateHash> Hash = new List<dkxce.CertificateHash>();

        [XmlArray("CertificatesDropped")]
        public List<dkxce.CertificateHash> Dropped = new List<dkxce.CertificateHash>();

        [XmlArray("CertificatesHistory")]
        public List<dkxce.CertificateHash> History = new List<dkxce.CertificateHash>();

        [XmlArray("DocumentsList")]
        public List<string> Docs = new List<string>();

        [XmlElement("ImportDocsByScanFiles")]
        public string ImportScanType = null;

        [XmlElement("ImportScanPath")]
        public string ImportScanPath = null;

        [XmlElement("AddStampMode")]
        public byte AddStampMode = 0; // 0 - no add, 1 - add to new file, 2 - add to source file

        [XmlElement("AddStampFile")]
        public string AddStampFile = null;

        [XmlElement("AddStampFont")]
        public string AddStampFont = "PT Sans";

        [XmlElement("StampOnEachPage")]
        public byte AddStampEachPage = 0;

        [XmlElement("StampPagePosition")]
        public byte AddStampOnPage = 0;

        [XmlElement("SignPDFDocument")]
        public byte AddSignToDoc = 0;

        [XmlElement("SignPDFAsNew")]
        public byte AddSignToNewDoc = 0;

        [XmlArray("CSLVHL")]
        public int[] CSLVHL = null;

        [XmlArray("SSLVHL")]
        public int[] SSLVHL = null;

        [XmlArray("FFLVHL")]
        public int[] FFLVHL = null;

        [XmlElement("SignAuthor")]
        public string Author = null;

        [XmlElement("SignReason")]
        public string Reason = "Я подтверждаю подлинность этого документа";

        [XmlElement("SignContact")]
        public string Contact = null;

        [XmlElement("SignLocation")]
        public string Location = null;

        [XmlElement("AddAnnotation")]
        public bool AddAnnot = false;
        
        [XmlElement("EncDecInContainer")]
        public bool inccb = true;

        [XmlElement("TextToEncrypt")]
        public string TextToEncrypt = "\"Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.\"";

        [XmlElement("TextToDecrypt")]
        public string TextToDecrypt = "";
    }

    public class ContainerInfo: XMLSaved<ContainerInfo>
    {
        [XmlAnyElement("CreatorXmlComment")]
        public XmlComment CreatorXmlComment { get { return GetType().GetXmlComment(); } set { } }

        [XmlComment("Minimal Version 1.0.2.19")]
        public string Creator { set; get; } = "Podpisaka by dkxce";

        [XmlAnyElement("UrlXmlComment")]
        public XmlComment UrlXmlComment { get { return GetType().GetXmlComment(); } set { } }

        [XmlComment("Author: https://github.com/dkxce")]
        public string Url { set; get; } = "https://github.com/dkxce/Pospisaka";

        public string OriginalFile;
        public string Thumbprint;

        public int FileAttrs;
        public DateTime FileCreated;
        public long FileLength;
        public DateTime FileModified;        
    }
}
