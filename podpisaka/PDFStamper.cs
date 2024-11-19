//
// C#
// DigitalCertAndSignMaker
// v 0.29, 19.11.2024
// https://github.com/dkxce/Pospisaka
// en,ru,1251,utf-8
//


using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Security.Cryptography.Pkcs;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.security;
using iTextSharp.text.xml.xmp;
using Org.BouncyCastle.X509;

namespace podpisaka
{
    public class PDFStamper
    {
        public static float StampDPI = 72.0f;
        public static float StampXOffset = 25.0f;
        public static float StampYOffset = 10.0f;
        private const string StampAnnoName = "podpisaka_stamp_thumbprint";

        #region PUBLIC        

        public static bool AddStamp(string stampFile, FontFamily fontFamily, short fontCorrection, dkxce.CertificateHash certificateHash, string fileName, PDFSignData signdata, byte hashAlgo = 0)
        {            
            string tempFile = null;
            try
            {
                Bitmap bitmap = PrepareStampBitmap(stampFile, certificateHash, fontFamily, fontCorrection, Color.Black);
                iTextSharp.text.Image image = PrepareImage(bitmap);
                tempFile = Path.GetTempFileName();
                bool modified = false;

                if (signdata.Mode == PDFSignData.StampMode.OnlyStamp)
                {
                    using (FileStream inputPdfStream = new FileStream(fileName, FileMode.Open))
                    using (Stream outputPdfStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write))
                    {
                        PdfReader reader = new PdfReader(inputPdfStream);
                        iTextSharp.text.pdf.parser.PdfReaderContentParser parser = new iTextSharp.text.pdf.parser.PdfReaderContentParser(reader);
                        PdfStamper stamper = new PdfStamper(reader, outputPdfStream);

                        int lastAddedPage = GetLastAddedPage(reader, certificateHash.Thumbprint, ref signdata.Offset, out bool annotated);
                        if (signdata.EachPage == PDFSignData.StampWhere.First)
                        {
                            AddStampToPage(reader, stamper, parser, 1, image, signdata.Offset);
                            
                        }
                        else if(signdata.EachPage == PDFSignData.StampWhere.Each)
                        {
                            lastAddedPage = Math.Max(lastAddedPage, 0);
                            for (int i = lastAddedPage + 1; i <= reader.NumberOfPages; i++)
                                AddStampToPage(reader, stamper, parser, i, image, signdata.Offset);
                        }
                        else
                        {
                            AddStampToPage(reader, stamper, parser, reader.NumberOfPages, image, signdata.Offset);
                        };
                        modified = true;


                        if ((!annotated) && modified && signdata.AddAnnot)
                            AddAnnotation(stamper, StampAnnoName, certificateHash.Thumbprint, reader.NumberOfPages, signdata);

                        stamper.Close();
                        reader.Close();
                    };
                }
                else if (signdata.Mode == PDFSignData.StampMode.OnlySign || signdata.Mode == PDFSignData.StampMode.SignAndStamp)
                {                    
                    using (FileStream outputPdfStream = new FileStream(tempFile, FileMode.Create))
                        modified = AddSignature(fileName, outputPdfStream, certificateHash, image, signdata, hashAlgo);
                };

                if (modified)
                {
                    File.Delete(fileName);
                    File.Move(tempFile, fileName);
                    return true;
                }
                else
                {
                    File.Delete(tempFile);
                    return false;
                };
            }
            catch (Exception ex) { throw ex;  }
            finally { try { if(!string.IsNullOrEmpty(tempFile)) File.Delete(tempFile); } catch { }; };
        }

        public static Bitmap PrepareStampBitmap(string imageFile, dkxce.CertificateHash certificateHash, FontFamily fontFamily, short fontCorrection, Color color)
        {
            List<string> sInfo = new List<string>(new string[] {
                certificateHash.Thumbprint,
                certificateHash.Owner,
                $"c {certificateHash.From:dd.MM.yyyy} по {certificateHash.Till:dd.MM.yyyy}" });
            Bitmap bmp = (Bitmap)Bitmap.FromFile(imageFile);
            PrepareStampBitmap(bmp, fontFamily, fontCorrection, color, sInfo);
            return bmp;
        }

        public static void PrepareStampBitmap(Bitmap bmp, dkxce.CertificateHash certificateHash, FontFamily fontFamily, short fontCorrection, Color color)
        {
            List<string> sInfo = new List<string>(new string[] {
                certificateHash.Thumbprint,
                certificateHash.Owner,
                $"c {certificateHash.From:dd.MM.yyyy} по {certificateHash.Till:dd.MM.yyyy}" });
            PrepareStampBitmap(bmp, fontFamily, fontCorrection, color, sInfo);
        }

        public static void PrepareStampBitmap(Bitmap bmp, FontFamily fontFamily, short fontCorrection, Color color, List<string> text = null)
        {
            List<PointF> points = new List<PointF>();
            for (int h = 0; h < bmp.Height; h++)
                for (int w = 0; w < bmp.Width; w++)
                {
                    Color c = bmp.GetPixel(w, h);
                    if (c.R == 0 && c.G == 255 & c.B == 0 && CheckImageAround(bmp, w, h))
                    {
                        points.Add(new PointF(w, h));
                        ClearImageAround(bmp, w, h);
                    };
                };
            if (points.Count > 1 && text != null && text.Count > 0)
            {
                float textSize = 0;
                for (int i = 1; i < points.Count; i++) textSize += points[i].Y - points[i - 1].Y;
                textSize = textSize / (points.Count - 1) / 2;
                textSize = textSize * StampDPI / bmp.VerticalResolution;
                textSize += (float)((float)fontCorrection / 20f);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    for (int i = 0; i < text.Count && i < points.Count; i++)
                    {
                        string txt = text[i];
                        System.Drawing.Font f = new System.Drawing.Font(fontFamily, textSize, i == 1 ? FontStyle.Bold : FontStyle.Regular);
                        while ((g.MeasureString(txt, f).Width + points[i].X + 10) > bmp.Width) txt = txt.Remove(txt.Length - 1);
                        g.DrawString(txt, f, new SolidBrush(color), points[i]);
                    };
                };
            };
        }

        #endregion PUBLIC

        #region PRIVATE

        private static int GetLastAddedPage(PdfReader reader, string thumbprint, ref PDFSignData.StampOffset offset, out bool annotated)
        {
            annotated = false;
            int lastAddedPage = 0;
            PdfDictionary pg = reader.GetPageN(1);
            PdfArray annotArray = pg.GetAsArray(PdfName.ANNOTS);
            if (annotArray != null)
                for (int j = 0; j < annotArray.Size; ++j)
                {
                    PdfDictionary curAnnot = annotArray.GetAsDict(j);
                    PdfString name = curAnnot.GetAsString(PdfName.TITLE);
                    PdfString contents = curAnnot.GetAsString(PdfName.CONTENTS);
                    if (name?.ToString() == StampAnnoName && contents?.ToString() == thumbprint)
                    {
                        annotated = true;
                        PdfString lastpage = curAnnot.GetAsString(PdfName.LASTPAGE);
                        string lp = lastpage?.ToString();
                        if (!string.IsNullOrEmpty(lp)) lastAddedPage = int.Parse(lp);

                        string ko = curAnnot.GetAsString(PdfName.KEYWORDS)?.ToString();
                        if(!string.IsNullOrEmpty(ko))
                        {
                            int p = ko.IndexOf("Offset=");
                            if ((p >= 0) && int.TryParse(ko.Substring(7, 1), out p)) 
                                offset = (PDFSignData.StampOffset)(++p > 5 ? 0 : p);
                        };

                        // NOT WORKING !!!
                        try { curAnnot.Remove(PdfName.LASTPAGE); } catch { };
                        curAnnot.Put(PdfName.LASTPAGE, new PdfString($"{reader.NumberOfPages}"));
                        try { curAnnot.Remove(PdfName.KEYWORDS); } catch { };
                        curAnnot.Put(PdfName.KEYWORDS, new PdfString($"Offset={(byte)offset}"));
                        //////////////////

                        break;                        
                    };
                };
            return lastAddedPage;
        }

        private static PointF AddStampToPage(PdfReader reader, PdfStamper stamper, iTextSharp.text.pdf.parser.PdfReaderContentParser parser, int page, iTextSharp.text.Image image, PDFSignData.StampOffset stampOffset)
        {
            PointF p = GetXYToStampOfPage(reader, stamper, parser, page, image, stampOffset);
            PdfContentByte pdfContentByte = stamper.GetOverContent(page);
            image.SetAbsolutePosition(p.X, p.Y);
            pdfContentByte.AddImage(image);
            return p;
        }

        private static PointF GetXYToStampOfPage(PdfReader reader, PdfStamper stamper, iTextSharp.text.pdf.parser.PdfReaderContentParser parser, int page, iTextSharp.text.Image image, PDFSignData.StampOffset stampOffset)
        {
            iTextSharp.text.pdf.parser.TextMarginFinder finder = parser.ProcessContent(page, new iTextSharp.text.pdf.parser.TextMarginFinder());
            float ph = 0;
            float wi = 0;
            float th = 0;            
            try
            {
                ph = reader.GetPageSize(page).Height;
                wi = reader.GetPageSize(page).Width;       
                th = finder.GetHeight();
            }
            catch { };

            float imtop = Math.Max(StampYOffset, (ph - th - 120) - image.PlainHeight);
            if (((byte)stampOffset) < 3) imtop = StampYOffset;

            if (stampOffset == PDFSignData.StampOffset.BottomLeft || stampOffset == PDFSignData.StampOffset.UnderLeft)
                return new PointF(StampXOffset, imtop);            
            if(stampOffset == PDFSignData.StampOffset.BottomRight || stampOffset == PDFSignData.StampOffset.UnderRight)
                return new PointF(wi - image.PlainWidth - StampXOffset, imtop);
            return new PointF(wi / 2 - image.PlainWidth / 2, imtop);            
        }

        private static iTextSharp.text.Image PrepareImage(Bitmap bmp)
        {
            iTextSharp.text.Image image = iTextSharp.text.Image.GetInstance(bmp, System.Drawing.Imaging.ImageFormat.Png);
            image.ScaleAbsolute((float)image.Width / (float)image.DpiX * StampDPI, (float)image.Height / (float)image.DpiY * StampDPI);
            return image;
        }

        private static bool AddSignature(string src, Stream outStream, dkxce.CertificateHash ch, iTextSharp.text.Image image, PDFSignData signdata, byte hashAlgo = 0)
        {
            PdfReader reader = new PdfReader(src);            

            PdfStamper stamper = PdfStamper.CreateSignature(reader, outStream, '\0', null, true); // PdfStamper.CreateSignature(reader, outStream, '\0'); 
            stamper.MoreInfo = signdata.Meta.getMetaData();
            stamper.XmpMetadata = signdata.Meta.getStreamedMetaData();

            int lastAddedPage = GetLastAddedPage(reader, ch.Thumbprint, ref signdata.Offset, out bool annotated);
            if ((!annotated) && (signdata.AddAnnot))
                AddAnnotation(stamper, StampAnnoName, ch.Thumbprint, reader.NumberOfPages, signdata);            

            PdfSignatureAppearance sap = stamper.SignatureAppearance;
            sap.Reason = signdata.Reason;
            sap.Contact = signdata.Contact;
            sap.Location = signdata.Location;
            sap.SignatureGraphic = image;
            sap.SignatureRenderingMode = PdfSignatureAppearance.RenderingMode.GRAPHIC;
            //sap.CertificationLevel = PdfSignatureAppearance.CERTIFIED_NO_CHANGES_ALLOWED;
            if (signdata.Mode == PDFSignData.StampMode.SignAndStamp)
            {
                iTextSharp.text.pdf.parser.PdfReaderContentParser parser = new iTextSharp.text.pdf.parser.PdfReaderContentParser(reader);
                if (signdata.EachPage == PDFSignData.StampWhere.First)
                {
                    PointF p = GetXYToStampOfPage(reader, stamper, new iTextSharp.text.pdf.parser.PdfReaderContentParser(reader), reader.NumberOfPages, image, signdata.Offset);
                    sap.SetVisibleSignature(new iTextSharp.text.Rectangle(p.X, p.Y, p.X + image.PlainWidth, p.Y + image.PlainHeight), 1, null);
                }
                else 
                {
                    if (signdata.EachPage == PDFSignData.StampWhere.Each)
                    {
                        lastAddedPage = Math.Max(lastAddedPage, 0);
                        for (int i = lastAddedPage + 1; i < reader.NumberOfPages; i++)
                            AddStampToPage(reader, stamper, parser, i, image, signdata.Offset);
                    };
                    PointF p = GetXYToStampOfPage(reader, stamper, new iTextSharp.text.pdf.parser.PdfReaderContentParser(reader), reader.NumberOfPages, image, signdata.Offset);
                    sap.SetVisibleSignature(new iTextSharp.text.Rectangle(p.X, p.Y, p.X + image.PlainWidth, p.Y + image.PlainHeight), reader.NumberOfPages, null);
                };          
            };

            X509Certificate cert = Org.BouncyCastle.Security.DotNetUtilities.FromX509Certificate(ch.Certificate);
            X509Certificate[] chain = new Org.BouncyCastle.X509.X509Certificate[] { new Org.BouncyCastle.X509.X509CertificateParser().ReadCertificate(ch.Certificate.RawData) };
            Exception exc = null;
            IExternalSignature externalSignature = null;

            if (hashAlgo == 0)
            {
                if (externalSignature == null) try { externalSignature = new X509Certificate2Signature(ch.Certificate, "sha256" /* chain[0].SigAlgName */ ); } catch (Exception ex) { exc = ex; };
                if (externalSignature == null) try { externalSignature = new X509Certificate2Signature(ch.Certificate, "sha1" /* chain[0].SigAlgName */ ); } catch (Exception ex) { exc = ex; };
                if (externalSignature == null) try { externalSignature = new X509Certificate2Signature(ch.Certificate, "SHA-1" /* chain[0].SigAlgName */ ); } catch (Exception ex) { exc = ex; };
            };
            if (hashAlgo == 1)
            {
                if (externalSignature == null) try { externalSignature = new X509Certificate2Signature(ch.Certificate, "rsa" /* chain[0].SigAlgName */ ); } catch (Exception ex) { exc = ex; };
                if (externalSignature == null) try { externalSignature = new X509Certificate2Signature(ch.Certificate, "sha256" /* chain[0].SigAlgName */ ); } catch (Exception ex) { exc = ex; };
                if (externalSignature == null) try { externalSignature = new X509Certificate2Signature(ch.Certificate, "sha1" /* chain[0].SigAlgName */ ); } catch (Exception ex) { exc = ex; };
                if (externalSignature == null) try { externalSignature = new X509Certificate2Signature(ch.Certificate, "SHA-1" /* chain[0].SigAlgName */ ); } catch (Exception ex) { exc = ex; };
            };
            if (hashAlgo == 2)
            {
                if (externalSignature == null) try { externalSignature = new X509Certificate2Signature(ch.Certificate, "sha256" /* chain[0].SigAlgName */ ); } catch (Exception ex) { exc = ex; };
                if (externalSignature == null) try { externalSignature = new X509Certificate2Signature(ch.Certificate, "sha1" /* chain[0].SigAlgName */ ); } catch (Exception ex) { exc = ex; };
                if (externalSignature == null) try { externalSignature = new X509Certificate2Signature(ch.Certificate, "SHA-1" /* chain[0].SigAlgName */ ); } catch (Exception ex) { exc = ex; };
            };
            if (hashAlgo == 3)
            {
                if (externalSignature == null) try { externalSignature = new X509Certificate2Signature(ch.Certificate, "sha1" /* chain[0].SigAlgName */ ); } catch (Exception ex) { exc = ex; };
                if (externalSignature == null) try { externalSignature = new X509Certificate2Signature(ch.Certificate, "SHA-1" /* chain[0].SigAlgName */ ); } catch (Exception ex) { exc = ex; };
                if (externalSignature == null) try { externalSignature = new X509Certificate2Signature(ch.Certificate, "sha256" /* chain[0].SigAlgName */ ); } catch (Exception ex) { exc = ex; };                
            };
            if (externalSignature == null) try { externalSignature = new X509Certificate2Signature(ch.Certificate, ch.Certificate?.SignatureAlgorithm?.FriendlyName /*  "SHA-1"  */ ); } catch (Exception ex) { exc = ex; };
            if (externalSignature == null) try { externalSignature = new X509Certificate2Signature(ch.Certificate, ch.Certificate?.SignatureAlgorithm?.Value /*  "SHA-1"  */ ); } catch (Exception ex) { exc = ex; };
            if (externalSignature == null)
            {
                FieldInfo mapField = typeof(Org.BouncyCastle.Cms.CmsSignedData).Assembly.GetType("Org.BouncyCastle.Cms.CmsSignedHelper").GetField("digestAlgs", BindingFlags.Static | BindingFlags.NonPublic);
                System.Collections.IDictionary map = (System.Collections.IDictionary)mapField.GetValue(null);
                foreach (string k in map.Keys)
                {
                    string v = (string)map[k];
                    try { externalSignature = new X509Certificate2Signature(ch.Certificate, k); break; } catch (Exception ex) { exc = ex; };
                    try { externalSignature = new X509Certificate2Signature(ch.Certificate, v); break; } catch (Exception ex) { exc = ex; };
                };
            };
            if (externalSignature == null) try { externalSignature = new X509ExternalSignature(src, ch.Certificate, ch.Certificate.GetKeyAlgorithm()); } catch (Exception ex) { exc = ex; };
            if (externalSignature == null) throw exc;
            MakeSignature.SignDetached(sap, externalSignature, chain, null, null, null, 0, CryptoStandard.CMS);

            stamper.Close();

            return true;
        }

        private static void AddAnnotation(PdfStamper stamper, string annot_name, string annot_text, int pages, PDFSignData signdata)
        {
            PdfAnnotation pa = new PdfAnnotation(stamper.Writer, new iTextSharp.text.Rectangle(0, 0));
            try
            {
                if (!string.IsNullOrEmpty(signdata.Meta?.Author)) pa.Put(PdfName.AUTHOR, new PdfString(signdata.Meta.Author));
                if (!string.IsNullOrEmpty(signdata.Meta?.Creator)) pa.Put(PdfName.CREATOR, new PdfString(signdata.Meta.Creator));
            }
            catch { };
            pa.Put(PdfName.TITLE, new PdfString(annot_name));
            pa.Put(PdfName.CONTENTS, new PdfString(annot_text));
            pa.Put(PdfName.FIRSTPAGE, new PdfString("1"));
            pa.Put(PdfName.LASTPAGE, new PdfString($"{pages}"));
            pa.Put(PdfName.KEYWORDS, new PdfString($"Offset={(byte)signdata.Offset}"));
            pa.Put(PdfName.TYPE, PdfName.ANNOT);
            stamper.AddAnnotation(pa, 1);
        }

        private static bool CheckImageAround(Bitmap bmp, int x, int y)
        {
            if ((x == 0) || (y == 0) || (x == (bmp.Width - 1)) || (y == (bmp.Height - 1))) return false;
            for (int w = x - 1; w <= x + 1; w++)
                for (int h = y - 1; h <= y + 1; h++)
                    if (w == x && h == y)
                        continue;
                    else
                    {
                        Color c = bmp.GetPixel(w, h);
                        if (c.R != 255 || c.G != 255 || c.B != 255) return false;
                    };
            return true;
        }

        private static bool ClearImageAround(Bitmap bmp, int x, int y)
        {
            if ((x == 0) || (y == 0) || (x == (bmp.Width - 1)) || (y == (bmp.Height - 1))) return false;
            for (int w = x - 1; w <= x + 1; w++)
                for (int h = y - 1; h <= y + 1; h++)
                    bmp.SetPixel(w, h, Color.FromArgb(0, 255, 255, 255));
            return true;
        }

        #endregion PRIVATE
    }

    public class PDFMetaData
    {
        private Dictionary<string, string> info = new Dictionary<string, string>();

        public Dictionary<string, string> Info
        {
            get { return info; }
            set { info = value; }
        }

        public string Author
        {
            get { return info.ContainsKey("Author") ? info["Author"] : null; }
            set { if (info.ContainsKey("Author")) info["Author"] = value; else info.Add("Author", value); }
        }
        public string Title
        {
            get { return info.ContainsKey("Author") ? info["Author"] : null; }
            set { if (info.ContainsKey("Title")) info["Title"] = value; info.Add("Title", value); }
        }
        public string Subject
        {
            get { return info.ContainsKey("Subject") ? info["Subject"] : null; }
            set { if (info.ContainsKey("Subject")) info["Subject"] = value; info.Add("Subject", value); }
        }
        public string Keywords
        {
            get { return info.ContainsKey("Keywords") ? info["Keywords"] : null; }
            set { if (info.ContainsKey("Keywords")) info["Keywords"] = value; info.Add("Keywords", value); }
        }
        public string Producer
        {
            get { return info.ContainsKey("Producer") ? info["Producer"] : null; }
            set { if (info.ContainsKey("Producer")) info["Producer"] = value; info.Add("Producer", value); }
        }
        public string Creator
        {
            get { return info.ContainsKey("Creator") ? info["Creator"] : null; }
            set { if (info.ContainsKey("Creator")) info["Creator"] = value; info.Add("Creator", value); }
        }

        public Dictionary<string, string> getMetaData()
        {
            return this.info;
        }

        public byte[] getStreamedMetaData()
        {
            MemoryStream os = new System.IO.MemoryStream();
            XmpWriter xmp = new XmpWriter(os, this.info);
            xmp.Close();
            return os.ToArray();
        }

    }

    public class PDFSignData
    {
        public enum StampOffset
        {
            BottomLeft = 0,
            BottomMiddle = 1,
            BottomRight = 2,
            UnderLeft = 3,
            UnderMiddle = 4,
            UnderRight =5
        }

        public enum StampMode
        {
            OnlyStamp = 0,
            OnlySign = 1,
            SignAndStamp = 2
        }

        public enum StampWhere
        {
            Last = 0,
            Each = 1,
            First = 2
        }

        public StampOffset Offset = StampOffset.BottomLeft;
        public StampMode Mode = StampMode.OnlyStamp;
        public PDFMetaData Meta = new PDFMetaData();
        public StampWhere EachPage = StampWhere.Last;
        public bool AddAnnot = false;

        public string Reason = null;
        public string Contact = null;
        public string Location = null;        
    }

    public class X509ExternalSignature : IExternalSignature
    {
        private string sourceFile;
        /// The certificate with the private key
        /// </summary>
        private System.Security.Cryptography.X509Certificates.X509Certificate2 certificate;
        /** The hash algorithm. */
        private string hashAlgorithm;
        /** The encryption algorithm (obtained from the private key) */
        private string encryptionAlgorithm;

        /// <summary>
        /// Creates a signature using a X509Certificate2. It supports smartcards without 
        /// exportable private keys.
        /// </summary>
        /// <param name="certificate">The certificate with the private key</param>
        /// <param name="hashAlgorithm">The hash algorithm for the signature. As the Windows CAPI is used
        /// to do the signature the only hash guaranteed to exist is SHA-1</param>
        public X509ExternalSignature(string sourceFile, System.Security.Cryptography.X509Certificates.X509Certificate2 certificate, string hashAlgorithm)
        {
            if (string.IsNullOrEmpty(sourceFile)) throw new FileNotFoundException("No file");
            this.sourceFile = sourceFile;
            if (!certificate.HasPrivateKey) throw new ArgumentException("No private key.");
            this.certificate = certificate;
            this.hashAlgorithm = "SHA256"; // hashAlgorithm; // DigestAlgorithms.GetDigest(DigestAlgorithms.GetAllowedDigests(hashAlgorithm));
            this.encryptionAlgorithm = "RSA"; // "ECDSA";
            try
            {
                if (certificate.PrivateKey is System.Security.Cryptography.RSACryptoServiceProvider)
                    encryptionAlgorithm = "RSA";
                else if (certificate.PrivateKey is System.Security.Cryptography.DSACryptoServiceProvider)
                    encryptionAlgorithm = "DSA";
                else
                    throw new ArgumentException("Unknown encryption algorithm " + certificate.PrivateKey);
            }
            catch { };
        }

        public virtual byte[] Sign(byte[] message)
        {
            try
            {
                if (certificate.PrivateKey is System.Security.Cryptography.RSACryptoServiceProvider)
                {
                    System.Security.Cryptography.RSACryptoServiceProvider rsa = (System.Security.Cryptography.RSACryptoServiceProvider)certificate.PrivateKey;
                    return rsa.SignData(message, hashAlgorithm);
                }
                else
                {
                    System.Security.Cryptography.DSACryptoServiceProvider dsa = (System.Security.Cryptography.DSACryptoServiceProvider)certificate.PrivateKey;
                    return dsa.SignData(message);
                };
            }
            catch { };

            //byte[] documentBytes = File.ReadAllBytes(this.sourceFile);

            ContentInfo content = new ContentInfo(message /*documentBytes*/);
            SignedCms signed = new SignedCms(content, true);
            CmsSigner signer = new System.Security.Cryptography.Pkcs.CmsSigner(certificate);
            signer.DigestAlgorithm = new System.Security.Cryptography.Oid("SHA256"); //(new Org.BouncyCastle.Crypto.Digests.Sha256Digest()).
            signer.IncludeOption = System.Security.Cryptography.X509Certificates.X509IncludeOption.EndCertOnly;

            signed.ComputeSignature(signer);
            byte[] result = signed.Encode();
            return result;
        }

        /**
         * Returns the hash algorithm.
         * @return  the hash algorithm (e.g. "SHA-1", "SHA-256,...")
         * @see com.itextpdf.text.pdf.security.ExternalSignature#getHashAlgorithm()
         */
        public virtual string GetHashAlgorithm()
        {
            return hashAlgorithm;
        }

        /**
         * Returns the encryption algorithm used for signing.
         * @return the encryption algorithm ("RSA" or "DSA")
         * @see com.itextpdf.text.pdf.security.ExternalSignature#getEncryptionAlgorithm()
         */
        public virtual string GetEncryptionAlgorithm()
        {
            return encryptionAlgorithm;
        }
    }
}
