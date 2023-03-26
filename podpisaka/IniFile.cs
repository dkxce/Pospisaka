﻿using System;
using System.Collections.Generic;
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

        [XmlArray("CSLVHL")]
        public int[] CSLVHL = null;

        [XmlArray("SSLVHL")]
        public int[] SSLVHL = null;

        [XmlArray("FFLVHL")]
        public int[] FFLVHL = null;
    }
}