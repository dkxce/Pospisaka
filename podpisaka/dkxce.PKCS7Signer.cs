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
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using Pkcs = System.Security.Cryptography.Pkcs;
using X509 = System.Security.Cryptography.X509Certificates;
using System.Xml.Serialization;
using System.Windows.Forms;
using System.Security.Cryptography;

// https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.pkcs.signedcms.computesignature?view=windowsdesktop-7.0#system-security-cryptography-pkcs-signedcms-computesignature
// https://www.cryptopro.ru/forum2/default.aspx?g=posts&t=893

namespace dkxce
{
    public class CertificateHash
    {
        public static Regex r1 = new Regex(@"((?<key>\w+)=(?<value>[^,=]+))", RegexOptions.IgnoreCase);

        public string Subject;
        public string Issuer;
        public DateTime From;
        public DateTime Till;
        public string Serial;
        public string Thumbprint;
        public string File;
        public string MD5;
        public string Source;

        [XmlIgnore]
        public string PriorityText { get {
                string[][] keys = new string[][] { new string[] { "ИНН", "ИННЮЛ" }, new string[] { "ОГРНИП", "ОГРН" }, new string[] { "E", "O", "OU" } };
                string txt = "";
                List<KeyValuePair<string, string>> list = SubjectDetailed;
                foreach (string[] kk in keys)
                {
                    bool next = true;
                    foreach (string k in kk)
                        if (next)
                            foreach (KeyValuePair<string, string> kvp in list)
                                if (kvp.Key == k)
                                {
                                    txt += (txt.Length > 0 ? ", " : "") + $"{kvp.Key}: {kvp.Value}";
                                    next = false;
                                };
                };
                return txt;
            } }

        private string GetNameFromList(List<KeyValuePair<string, string>> list)
        {
            string[] keys = new string[] { "CN", "OU", "O", "E", "SERIALNUMBER", "G", "T", "DESCRIPTION" };
            foreach (string k in keys)
                foreach (KeyValuePair<string, string> kvp in list)
                    if (kvp.Key == k)
                        return kvp.Value;
            if (list.Count > 0) foreach (KeyValuePair<string, string> kvp in list) return $"{kvp.Key}={kvp.Value}";
            return null;
        }

        private List<KeyValuePair<string, string>> GetDetailedList(string text)
        {
            List<KeyValuePair<string, string>> res = new List<KeyValuePair<string, string>>();
            MatchCollection mc = r1.Matches(text);
            if (mc.Count == 0)
                res.Add(new KeyValuePair<string, string>("@", Subject));
            else
                foreach (Match mx in mc)
                    res.Add(new KeyValuePair<string, string>(mx.Groups["key"].Value.Trim().ToUpper(), mx.Groups["value"].Value.Trim()));
            return res;
        }

        [XmlIgnore]
        public string Owner { get { return GetNameFromList(SubjectDetailed); } }

        [XmlIgnore]
        public string Publisher { get { return GetNameFromList(IssuerDetailed); } }

        [XmlIgnore]
        public List<KeyValuePair<string, string>> SubjectDetailed { get { return GetDetailedList(Subject); } }

        [XmlIgnore]
        public List<KeyValuePair<string, string>> IssuerDetailed { get { return GetDetailedList(Issuer); } }

        [XmlIgnore]
        private X509Certificate2 cert;
        [XmlIgnore]
        public X509Certificate2 Certificate
        {
            get { return cert; }
            set
            {
                cert = value;
                Subject = Issuer = Serial = Thumbprint = null;
                Till = DateTime.MinValue;
                From = DateTime.MinValue;
                if(cert != null)
                {
                    Subject = cert.Subject;
                    Issuer = cert.Issuer;
                    From = cert.NotBefore;
                    Till = cert.NotAfter;
                    Serial = cert.GetSerialNumberString();
                    Thumbprint = cert.Thumbprint;
                };
            }
        }
    }

    public class PKCS7Signer
    {
        public enum Source
        {
            File = 1,
            CurrentUser = 2,
            LocalMachine = 4,
            UserOrMachine = 6,
            Directory = 8
        }

        private Encoding _encoding = System.Text.Encoding.UTF8;
        private X509Certificate2 _PrivateCert;

        public PKCS7Signer() {  }
        public PKCS7Signer(X509Certificate2 cert) { _PrivateCert = cert; }

        /// <summary>
        ///      Loads the PKCS12 file which contains the certificate and private key of the signer
        /// </summary>
        /// <param name="pkcs12path">File path to the signer's certificate plus private key in PKCS#12 format</param>
        /// <param name="privateKeyPass">Password for signer's private key</param>
        public PKCS7Signer(string pkcs12path, string privateKeyPass = null) { SetPrivateCredentials(pkcs12path, privateKeyPass); }        

        public string Charset { get { return _encoding.WebName; } set { if (!string.IsNullOrEmpty(value)) _encoding = Encoding.GetEncoding(value); } }

        public X509.X509Certificate2 Certificate { set { _PrivateCert = value; } get { return _PrivateCert; } }

        public static X509Certificate2 GetCertificateFrom(Source source, X509FindType find, string match)
        {
            if(source == Source.File)
            {

            }
            else
            {
                List<StoreLocation> stores = new List<StoreLocation>();
                if (source == Source.CurrentUser) stores.Add(StoreLocation.CurrentUser);
                if (source == Source.LocalMachine) stores.Add(StoreLocation.LocalMachine);
                if (source == Source.UserOrMachine) stores.AddRange(new StoreLocation[] { StoreLocation.CurrentUser, StoreLocation.LocalMachine });
                foreach(StoreLocation st in stores)
                {
                    X509Store store = null;
                    try
                    {
                        store = new X509Store(st);
                        store.Open(OpenFlags.ReadOnly);
                        // Place all certificates in an X509Certificate2Collection object.
                        X509Certificate2Collection certCollection = store.Certificates;
                        // If using a certificate with a trusted root you do not need to FindByTimeValid, instead: currentCerts.Find(X509FindType.FindBySubjectDistinguishedName, certName, true);
                        X509Certificate2Collection currentCerts = certCollection.Find(X509FindType.FindByTimeValid, DateTime.Now, false);
                        X509Certificate2Collection signingCert = currentCerts.Find(find, match, false);
                        if (signingCert.Count > 0) return signingCert[0];
                    }
                    finally { if(store != null) store.Close(); };
                };
            };
            return null;
        }

        public void SetCertificateFrom(Source source, X509FindType find, string match)
        {
            X509Certificate2 res = GetCertificateFrom(source, find, match);
            if (res == null)
                throw new Exception("Сертификат не найден");
            _PrivateCert = res;
        }        

        private static string GetFileMD5(string file)
        {
            byte[] inputBytes = null;
            try
            {
                using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    inputBytes = new byte[fs.Length];
                    fs.Read(inputBytes, 0, inputBytes.Length);
                };
                if (inputBytes.Length == 0) return null;
                using (MD5 md5 = MD5.Create())
                {
                    byte[] hashBytes = md5.ComputeHash(inputBytes);
                    return BitConverter.ToString(hashBytes).Replace("-", string.Empty).ToUpper();
                };
            }
            catch { };
            return null;
        }

        public static List<CertificateHash> GetCertificatesFrom(Source source, string path = null, List<CertificateHash> chash = null, bool allowDialogs = false)
        {
            List<CertificateHash> res = new List<CertificateHash>();
            if (source == Source.File && (!string.IsNullOrEmpty(path)) && File.Exists(path))
            {
                string md5 = GetFileMD5(path);
                bool added = false;
                if ((chash != null) && (chash.Count > 0))
                    for (int i = 0; i < chash.Count; i++)
                    {
                        if (chash[i].MD5 == md5)
                        {
                            chash[i].File = path;
                            res.Add(chash[i]);
                            added = true;
                            break;
                        };
                    };

                if (added) return res;
                
                X509Certificate2 c = null;
                if (allowDialogs)
                {
                    string pass = "";
                    DialogResult dr = InputBox.QueryPass("Импорт сертификата из файла", $"Введите пароль для {Path.GetFileName(path)}:", ref pass);
                    if (dr != DialogResult.OK) return res;
                    try { c = new X509Certificate2(path, pass); }
                    catch (Exception ex)
                    {
                        dr = MessageBox.Show(ex.Message, $"Импорт сертификата из {Path.GetFileName(path)}", MessageBoxButtons.OKCancel, MessageBoxIcon.Error);
                        if (dr == DialogResult.Cancel) return res;
                    };
                };
                if (c != null) res.Add(new CertificateHash() { Certificate = c, File = path, MD5 = md5, Source = $"{source}" });
            }
            if (source == Source.Directory && (!string.IsNullOrEmpty(path)) && Directory.Exists(path))
            {
                string pass = "";
                List<string> certExt = new List<string>(new string[] { "*.p12", "*.pfx", "*.crt", "*.p7b", "*.cer", "*.pem" });
                List<string> files = new List<string>();
                foreach (string e in certExt) files.AddRange(Directory.GetFiles(path, e, SearchOption.AllDirectories));
                foreach (string f in files)
                {
                    string md5 = GetFileMD5(f);
                    bool added = false;
                    if((chash != null) && (chash.Count > 0))
                        for(int i = 0;i<chash.Count;i++)
                        {
                            if (chash[i].MD5 == md5)
                            {
                                chash[i].File = f;
                                res.Add(chash[i]);
                                added = true;
                                break;
                            };
                        };

                    if (added) continue;

                    X509Certificate2 c = null;
                    if (allowDialogs)
                    {
                        DialogResult dr = InputBox.QueryPass("Импорт сертификата из файла", $"Введите пароль для {Path.GetFileName(f)}:", ref pass);
                        if (dr != DialogResult.OK) return res;
                        try { c = new X509Certificate2(f, pass); }
                        catch (Exception ex)
                        {
                            dr = MessageBox.Show(ex.Message, $"Импорт сертификата из {Path.GetFileName(f)}", MessageBoxButtons.OKCancel, MessageBoxIcon.Error);
                            if (dr == DialogResult.Cancel) return res;
                        };
                    };
                    if (c != null) res.Add(new CertificateHash() { Certificate = c, File = f, MD5 = md5, Source = $"{source}" });
                };
            }
            else if ((source != Source.File) && (source != Source.Directory))
            {
                List<StoreLocation> stores = new List<StoreLocation>();
                if (source == Source.CurrentUser) stores.Add(StoreLocation.CurrentUser);
                if (source == Source.LocalMachine) stores.Add(StoreLocation.LocalMachine);
                if (source == Source.UserOrMachine) stores.AddRange(new StoreLocation[] { StoreLocation.CurrentUser, StoreLocation.LocalMachine });
                foreach (StoreLocation st in stores)
                {
                    X509Store store = null;
                    try
                    {
                        store = new X509Store(st);
                        store.Open(OpenFlags.ReadOnly);
                        try { foreach (X509Certificate2 c in store.Certificates) res.Add(new CertificateHash() { Certificate = c, Source = $"{source}" }); } catch { };
                    }
                    finally { if (store != null) store.Close(); };
                };
            };
            return res;
        }       

        /// <summary>
        ///     Loads the PKCS12 file which contains the certificate and private key of the signer
        /// </summary>
        /// <param name="pkcs12path">File path to the signer's certificate plus private key in PKCS#12 format</param>
        /// <param name="privateKeyPass">Password for signer's private key</param>
        public void SetPrivateCredentials(string pkcs12path, string privateKeyPass = null)
        {
            if (!string.IsNullOrEmpty(privateKeyPass))
                _PrivateCert = new X509.X509Certificate2(pkcs12path, privateKeyPass);
            else
                _PrivateCert = new X509.X509Certificate2(pkcs12path);
        }        
        
        /// <summary>
        ///     Sign a message and encrypt it for the recipient.
        /// </summary>
        /// <param name="innerText">Name value pairs must be separated by \n (vbLf or chr&#40;10)), for example "cmd=_xclick\nbusiness=..."</param>
        /// <returns>base64 string</returns>
        public string SignPairsAndEncrypt(string innerText, out string certInfo)
        {
            // Get innerText as Bytes
            byte[] messageBytes = _encoding.GetBytes(innerText);
            // The dataToSign byte array holds the data to be signed.
            ContentInfo content = new ContentInfo(messageBytes);
            // Create a new, detached SignedCms message.
            SignedCms signed = new SignedCms(content);
            // Set Signer
            CmsSigner signer = new Pkcs.CmsSigner(_PrivateCert);
            // Sign the message.
            signed.ComputeSignature(signer);

            certInfo = "";
            foreach (X509.X509Certificate el in signed.Certificates)
                certInfo += "--- CERTIFICATE BEGIN ---\r\n\r\n" + el.ToString() + "\r\n--- CERTIFICATE END ---\r\n";

            // Encode the message.
            byte[] signedBytes = signed.Encode();
            // Get Base64
            return Base64Encode(signedBytes);
        }

        /// <summary>
        ///     Sign a message and encrypt it for the recipient.
        /// </summary>
        /// <param name="innerText">message</param>
        /// <returns>base64 string</returns>
        public string SignText(string innerText, out string certInfo)
        {
            // Get innerText as Bytes
            byte[] messageBytes = _encoding.GetBytes(innerText);
            // The dataToSign byte array holds the data to be signed.
            ContentInfo content = new ContentInfo(messageBytes);
            // Create a new, detached SignedCms message.
            SignedCms signed = new SignedCms(content);
            // Set Signer
            CmsSigner signer = new Pkcs.CmsSigner(_PrivateCert);
            // Sign the message.
            signed.ComputeSignature(signer);

            certInfo = "";
            foreach (X509.X509Certificate el in signed.Certificates)
                certInfo += "--- CERTIFICATE BEGIN ---\r\n\r\n" + el.ToString() + "\r\n--- CERTIFICATE END ---\r\n";

            // Encode the message.
            byte[] signedBytes = signed.Encode();
            // Get Base64
            return Base64Encode(signedBytes);
        }

        /// <summary>
        ///     Sign a message and encrypt it as signature
        /// </summary>
        /// <param name="messageBytes">message</param>
        /// <returns></returns>
        public byte[] SignBytes(byte[] messageBytes, out X509Certificate2 cert)
        {
            // The dataToSign byte array holds the data to be signed.
            ContentInfo content = new ContentInfo(messageBytes);
            // Create a new, detached SignedCms message.
            SignedCms signed = new SignedCms(content);
            // Set Signer
            CmsSigner signer = new Pkcs.CmsSigner(_PrivateCert);
            // Sign the message.
            signed.ComputeSignature(signer);
            cert = signed.Certificates[0];
            // Encode the message.
            return signed.Encode();
        }

        public byte[] SignDetachedBytes(byte[] messageBytes, out X509Certificate2 cert)
        {
            // The dataToSign byte array holds the data to be signed.
            ContentInfo content = new ContentInfo(messageBytes);
            // Create a new, detached SignedCms message.
            SignedCms signed = new SignedCms(content, true);
            // Set Signer
            CmsSigner signer = new Pkcs.CmsSigner(_PrivateCert);
            // Sign the message.
            signed.ComputeSignature(signer);
            cert = signed.Certificates[0];
            // Encode the message.
            return signed.Encode();
        }

        /// <summary>
        ///     Sign a file and create it signature
        /// </summary>
        /// <param name="fileName">file path</param>
        /// <returns></returns>
        public byte[] SignFile(string fileName, out X509Certificate2 cert)
        {
            byte[] messageBytes = new byte[0];
            {
                FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);
                messageBytes = new byte[fs.Length];
                fs.Read(messageBytes, 0, messageBytes.Length);
                fs.Close();
            };
            byte[] signature = SignBytes(messageBytes, out cert);
            {
                FileStream fs = new FileStream(fileName+ ".p7s", FileMode.Create, FileAccess.Write);
                fs.Write(signature, 0, signature.Length);
                fs.Close();
            };
            return signature;
        }

        public byte[] SignFile(string fileName, bool detached, out X509Certificate2 cert)
        {
            return detached ? SignDetachedFile(fileName, out cert) : SignFile(fileName, out cert);
        }

        public byte[] SignDetachedFile(string fileName, out X509Certificate2 cert)
        {
            byte[] messageBytes = new byte[0];
            {
                FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);
                messageBytes = new byte[fs.Length];
                fs.Read(messageBytes, 0, messageBytes.Length);
                fs.Close();
            };
            byte[] signature = SignDetachedBytes(messageBytes, out cert);
            {
                FileStream fs = new FileStream(fileName + ".detached.p7s", FileMode.Create, FileAccess.Write);
                fs.Write(signature, 0, signature.Length);
                fs.Close();
            };
            return signature;
        }

        private string Base64Encode(byte[] encoded)
        {
            const string PKCS7_HEADER = "-----BEGIN PKCS7-----";
            const string PKCS7_FOOTER = "-----END PKCS7-----";

            string base64 = Convert.ToBase64String(encoded);
            StringBuilder formatted = new StringBuilder();
            formatted.Append(PKCS7_HEADER);
            formatted.Append(base64);
            formatted.Append(PKCS7_FOOTER);

            return formatted.ToString();
        }

        /// <summary>
        ///     Check Signature for bytes array
        /// </summary>
        /// <param name="encodedMessage">signed bytes array</param>
        /// <param name="originalBytes">not-signed bytes array</param>
        /// <param name="verifySignatureOnly">check certificates or signature only</param>
        /// <returns></returns>
        public static bool CheckSignBytes(byte[] encodedMessage, byte[] originalBytes, bool verifySignatureOnly, out X509Certificate2 certInfo)
        {
            // Create a new, nondetached SignedCms message.
            SignedCms signedCms = new SignedCms();            
            // encodedMessage is the encoded message received from the sender.
            signedCms.Decode(encodedMessage);
            // Verify the signature without validating the certificate.
            signedCms.CheckSignature(verifySignatureOnly);
            certInfo = signedCms.Certificates[0];
            if (originalBytes != null)
            {
                if (signedCms.ContentInfo.Content.Length != originalBytes.Length) throw new Exception("Исходный файл был изменен!");
                for (int i = 0; i < signedCms.ContentInfo.Content.Length; i++)
                    if (signedCms.ContentInfo.Content[i] != originalBytes[i])
                        throw new Exception("Исходный файл был изменен!");
            }
            else
                throw new Exception("Подпись верна, но исходный файл не найден.");
            return true;
        }

        public static bool CheckSignDetachedBytes(byte[] encodedMessage, byte[] originalBytes, bool verifySignatureOnly, out X509Certificate2 certInfo)
        {
            // Create a new, nondetached SignedCms message.
            SignedCms signedCms = new SignedCms(new ContentInfo(originalBytes), true);
            // encodedMessage is the encoded message received from the sender.
            signedCms.Decode(encodedMessage);
            // Verify the signature without validating the certificate.
            signedCms.CheckSignature(verifySignatureOnly);
            certInfo = signedCms.Certificates[0];            
            return true;
        }

        public static bool CheckSignFile(string p7sFileName, string originFileName, bool verifySignatureOnly, out X509Certificate2 cert)
        {
            cert = null;
            byte[] encodedMessage = new byte[0];
            {
                FileStream fs = new FileStream(p7sFileName, FileMode.Open, FileAccess.Read);
                encodedMessage = new byte[fs.Length];
                fs.Read(encodedMessage, 0, encodedMessage.Length);
                fs.Close();
            };
            byte[] originalMessage = null;
            {
                FileStream fs = new FileStream(originFileName, FileMode.Open, FileAccess.Read);
                originalMessage = new byte[fs.Length];
                fs.Read(originalMessage, 0, encodedMessage.Length);
                fs.Close();
            };

            bool res = false;
            if (!res) try { res = CheckSignDetachedBytes(encodedMessage, originalMessage, verifySignatureOnly, out cert); } catch { };
            if (!res) try { res = CheckSignBytes(encodedMessage, originalMessage, verifySignatureOnly, out cert); } catch (Exception ex) { throw ex; };
            return res;
        }

        /// <summary>
        ///     Check Signature for file p7s
        /// </summary>
        /// <param name="p7sFileName">p7s file path</param>
        /// <param name="verifySignatureOnly">check certificates or signature only</param>
        /// <returns></returns>
        public static bool CheckSignFile(string p7sFileName, bool verifySignatureOnly, out X509Certificate2 cert)
        {
            byte[] encodedMessage = new byte[0];
            {
                FileStream fs = new FileStream(p7sFileName, FileMode.Open, FileAccess.Read);
                encodedMessage = new byte[fs.Length];
                fs.Read(encodedMessage, 0, encodedMessage.Length);
                fs.Close();
            };
            byte[] originalMessage = null;
            {
                int extL = Path.GetExtension(p7sFileName).Length;
                string originalFile = p7sFileName.Substring(0, p7sFileName.Length - extL);
                bool fileExists = File.Exists(originalFile);
                if ((!fileExists) && Path.GetExtension(originalFile).ToLower() == ".detached")
                {
                    originalFile = originalFile.Substring(0, originalFile.Length - 9);
                    fileExists = File.Exists(originalFile);
                };
                if (fileExists)
                {
                    FileStream fs = new FileStream(originalFile, FileMode.Open, FileAccess.Read);
                    originalMessage = new byte[fs.Length];
                    fs.Read(originalMessage, 0, originalMessage.Length);
                    fs.Close();
                };
            };
            return CheckSignBytes(encodedMessage, originalMessage, verifySignatureOnly, out cert);
        }

        public static bool CheckSignDetachedFile(string p7sFileName, bool verifySignatureOnly, out X509Certificate2 cert)
        {
            byte[] encodedMessage = new byte[0];
            {
                FileStream fs = new FileStream(p7sFileName, FileMode.Open, FileAccess.Read);
                encodedMessage = new byte[fs.Length];
                fs.Read(encodedMessage, 0, encodedMessage.Length);
                fs.Close();
            };
            byte[] originalMessage = null;
            {
                int extL = Path.GetExtension(p7sFileName).Length;
                string originalFile = p7sFileName.Substring(0, p7sFileName.Length - extL);
                bool fileExists = File.Exists(originalFile);
                if ((!fileExists) && Path.GetExtension(originalFile).ToLower() == ".detached")
                {
                    originalFile = originalFile.Substring(0, originalFile.Length - 9);
                    fileExists = File.Exists(originalFile);
                };
                if(fileExists)
                {
                    FileStream fs = new FileStream(originalFile, FileMode.Open, FileAccess.Read);
                    originalMessage = new byte[fs.Length];
                    fs.Read(originalMessage, 0, originalMessage.Length);
                    fs.Close();
                };
            };
            return CheckSignDetachedBytes(encodedMessage, originalMessage, verifySignatureOnly, out cert);
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
}