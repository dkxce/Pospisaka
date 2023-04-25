//
// C#
// DigitalCertAndSignMaker
// v 0.28, 12.04.2023
// https://github.com/dkxce/Pospisaka
// en,ru,1251,utf-8
//


using dkxce;
using Org.BouncyCastle.Cms;
using podpisaka;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml;
using System.Runtime.ConstrainedExecution;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Tls;
using Org.BouncyCastle.Ocsp;

namespace DigitalCertAndSignMaker
{
    public partial class MainForm : Form
    {
        internal object pb_over = null;
        internal IniFile iniFile = new IniFile();
        PrivateFontCollection pfc = new PrivateFontCollection();
        FontFamily fontFamily = new FontFamily("Arial");
        public MainForm()
        {
            InitializeComponent();

            this.statusStrip1.Renderer = new CustomRenderer();
            this.statusStrip2.Renderer = new CustomRenderer();
            this.statusStrip5.Renderer = new CustomRenderer();

            statusStrip1.CanOverflow = true;
            statusStrip2.CanOverflow = true;
            ssn.Spring = true;
            ssc.Spring = true;

            this.AllowDrop = true;
            this.DragEnter += new DragEventHandler(MFDragEnter);
            this.DragDrop += new DragEventHandler(MFDragDrop);
        }

        private void MFDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
        }

        private void MFDragDrop(object sender, DragEventArgs e)
        {
            ProcessDroppedFiles((string[])e.Data.GetData(DataFormats.FileDrop));            
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            if (File.Exists(iniPath)) iniFile = IniFile.Load(iniPath);
            if (iniFile.Maximized) this.WindowState = FormWindowState.Maximized;
            for (int i = iniFile.History.Count - 1; i >= 100; i--) iniFile.History.RemoveAt(i);
            for (int i = iniFile.Docs.Count - 1; i >= 0; i--) if (!File.Exists(iniFile.Docs[i])) iniFile.Docs.RemoveAt(i);
            LoadDocsFromHistory();
            try { txtLogOut.SelectedIndex = iniFile.lastPageSelected; } catch { };
            storageSelector.SelectedIndex = iniFile.lastStorageSelected;
            SetCurrentCert(iniFile.currentCertificate);
            ClickSM(iniFile.signCreateMethod);
            dsval.Checked = iniFile.checkCertValid;
            saepBox.SelectedIndex = iniFile.AddStampEachPage;
            saipBox.SelectedIndex = iniFile.AddStampOnPage;
            saedBox.SelectedIndex = iniFile.AddSignToDoc;
            sanf.SelectedIndex = iniFile.AddSignToNewDoc;
            tsAuthor.Text = iniFile.Author;
            tsReason.Text = iniFile.Reason;
            tsContact.Text = iniFile.Contact;
            tsLocation.Text = iniFile.Location;
            adanaBox.SelectedIndex = iniFile.AddAnnot ? 1 : 0;
            inccb.Checked = iniFile.inccb;
            textBox2.Text = iniFile.TextToEncrypt;            
            textBox3.Text = iniFile.TextToDecrypt;            

            if (iniFile.CSLVHL != null)
                for (int i = 0; i < iniFile.CSLVHL.Length; i++)
                    selSert.Columns[i].Width = iniFile.CSLVHL[i];

            if (iniFile.SSLVHL != null)
                for (int i = 0; i < iniFile.SSLVHL.Length; i++)
                    certList.Columns[i].Width = iniFile.SSLVHL[i];

            if (iniFile.FFLVHL != null)
                for (int i = 0; i < iniFile.FFLVHL.Length; i++)
                    filesListView.Columns[i].Width = iniFile.FFLVHL[i];

            LoadFonts();
            ResizeSplitter();
        }       

        private void LoadFonts()
        {
            string[] fonts = Directory.GetFiles(System.Xml.XMLSaved<int>.CurrentDirectory(), "*.ttf", SearchOption.AllDirectories);
            foreach (string f in fonts)
                pfc.AddFontFile(f);
            foreach (FontFamily ff in pfc.Families)
            {
                fBox.Items.Add(ff.Name);
                if (ff.Name == iniFile.AddStampFont)
                {
                    fontFamily = ff;
                    fBox.SelectedIndex = fBox.Items.Count - 1;
                };
            };
        }

        private void ProcessCmdArgFiles()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            if (args.Length < 2) return;
            List<string> files = new List<string>();
            for (int i = 1; i < args.Length; i++)
            {
                try { if (!File.Exists(args[i])) continue; } catch { continue; };
                files.Add(args[i]);
            };
            ProcessDroppedFiles(files.ToArray(), "Передано");
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            IniFile.Save(iniPath, iniFile);
        }        

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            iniFile.lastPageSelected = txtLogOut.SelectedIndex;
            if(txtLogOut.SelectedTab.Name == "tabCERT") ReloadCertificates(storageSelector.SelectedIndex);
            if (txtLogOut.SelectedTab.Name == "logPage") ReloadTextLog();
        }
        
        private void Test()
        {
            string signerCertificate = Path.Combine(dkxce.PKCS7Signer.CurrentDirectory(), "CERT.PKCS#12.pfx");
            string signerCrtPassword = "******";

            string clearText = "XXX-1111-YYY-2222-ZZZ";

            dkxce.PKCS7Signer signer = new dkxce.PKCS7Signer(signerCertificate, signerCrtPassword);
            string result = signer.SignText(clearText, out _);

            FileStream fs = new FileStream("C:\\Downloads\\sql-UA.txt", FileMode.Open, FileAccess.Read);
            byte[] bb = new byte[fs.Length];
            fs.Read(bb, 0, bb.Length);
            fs.Close();

            byte[] res = signer.SignBytes(bb, out _);

            fs = new FileStream("C:\\Downloads\\sql-UA.txt.p7s", FileMode.Create, FileAccess.Write);
            fs.Write(res, 0, res.Length);
            fs.Close();

            bool ok = PKCS7Signer.CheckSignBytes(res, null, true, out _);

            signer.SignFile("C:\\Downloads\\sql-UA22.txt", out _);
            bool ok2 = PKCS7Signer.CheckSignFile("C:\\Downloads\\sql-UA22.txt.p7s", true, out _);
        }

        private void filesListView_ColumnClick(object sender, ColumnClickEventArgs e)
        {            
            ListViewSorter.ListViewColumnSorter sorter = new ListViewSorter.ListViewColumnSorter();
            sorter.SortColumn = e.Column;
            sorter.Order = SortOrder.Ascending;
            if (_fileListViewSortBy.Item1 == e.Column) sorter.Order = _fileListViewSortBy.Item2 ? SortOrder.Descending : SortOrder.Ascending;
            _fileListViewSortBy = (e.Column, sorter.Order == SortOrder.Ascending);
            filesListView.ListViewItemSorter = sorter;
            filesListView.Sorting = sorter.Order;            
            filesListView.Sort();
            ColumnAddSortSymbol(filesListView, e.Column, sorter.Order == SortOrder.Ascending);
        }

        private void ColumnAddSortSymbol(ListView lv, int index, bool asc)
        {
            for (int i = 0; i < lv.Columns.Count; i++)
                lv.Columns[i].Text = Regex.Replace(lv.Columns[i].Text, "[↑↓]", "").Trim();
            if(index >= 0)
                lv.Columns[index].Text += asc? " ↑" : " ↓";
        }

        private void filesMenuStrip_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            rsltd.Enabled = restat.Enabled = sicur.Enabled = delCur.Enabled = tdCS.Enabled = tdCD.Enabled = tdClearAll.Enabled = tdCheckAll.Enabled = tdSignAll.Enabled = tdSCAll.Enabled = false;            
            if (filesListView.Items.Count > 0)
            {
                restat.Enabled = true;
                tdClearAll.Enabled = true;
                int toSign = 0; int toCheck = 0;
                for(int i = 0; i < filesListView.Items.Count; i++)
                {
                    if (filesListView.Items[i].SubItems[2].Text == "документ") toSign++;
                    if (filesListView.Items[i].SubItems[2].Text == "подпись") toCheck++;
                };
                tdSCAll.Enabled = toSign > 0 && toCheck > 0;
                tdCD.Enabled = tdSignAll.Enabled = toSign > 0;
                tdCS.Enabled = tdCheckAll.Enabled = toCheck > 0;                
            };
            rsltd.Enabled = filesListView.SelectedItems.Count > 0;
            delCur.Enabled = filesListView.SelectedItems.Count > 0;
            sicur.Enabled = filesListView.SelectedItems.Count > 0;
            if(filesListView.SelectedItems.Count > 0) sicur.Text = String.Format("{0} текущий (Enter)", filesListView.SelectedItems[0].SubItems[2].Text == "документ" ? "Подписать" : "Проверить");
            stnew.Enabled = btnStamp.Enabled = btnView.Enabled = filesListView.SelectedItems.Count > 0 && (!signExt.Contains(Path.GetExtension(filesListView.SelectedItems[0].SubItems[1].Text).ToLower()));
        }

        private void tdClearAll_Click(object sender, EventArgs e)
        {
            filesListView.Items.Clear();
            fileStatus.Text = $"Список файлов очищен";
            ResaveHistory();
        }

        private void tdSignAll_Click(object sender, EventArgs e)
        {
            ProcessFiles(true, false);
        }

        private void tdCheckAll_Click(object sender, EventArgs e)
        {
            ProcessFiles(false, true);
        }

        private void tdSCAll_Click(object sender, EventArgs e)
        {
            ProcessFiles(true, true);
        }

        private void tdCD_Click(object sender, EventArgs e)
        {
            int del = 0;
            for (int i = filesListView.Items.Count - 1; i >= 0; i--)
                if (filesListView.Items[i].SubItems[2].Text == "документ")
                {
                    filesListView.Items[i].Remove();
                    del++;
                };
            fileStatus.Text = $"Удалено {del} документов";
            ResaveHistory();
        }

        private void tdCS_Click(object sender, EventArgs e)
        {
            int del = 0;
            for (int i = filesListView.Items.Count - 1; i >= 0; i--)
                if (filesListView.Items[i].SubItems[2].Text == "подпись")
                {
                    del++;
                    filesListView.Items[i].Remove();
                };
            fileStatus.Text = $"Удалено {del} подписей";
            ResaveHistory();
        }

        private void storageSelector_SelectedIndexChanged(object sender, EventArgs e)
        {
            iniFile.lastStorageSelected = storageSelector.SelectedIndex;
            ReloadCertificates(storageSelector.SelectedIndex);
        }

        private void selSert_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            ListViewSorter.ListViewColumnSorter sorter = new ListViewSorter.ListViewColumnSorter();
            sorter.SortColumn = e.Column;
            sorter.Order = SortOrder.Ascending;
            if (_selSertSortBy.Item1 == e.Column) sorter.Order = _selSertSortBy.Item2 ? SortOrder.Descending : SortOrder.Ascending;
            _selSertSortBy = (e.Column, sorter.Order == SortOrder.Ascending);
            selSert.ListViewItemSorter = sorter;
            selSert.Sorting = sorter.Order;
            selSert.Sort();
            ColumnAddSortSymbol(selSert, e.Column, sorter.Order == SortOrder.Ascending);
        }


        private void certList_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            ListViewSorter.ListViewColumnSorter sorter = new ListViewSorter.ListViewColumnSorter();
            sorter.SortColumn = e.Column;
            sorter.Order = SortOrder.Ascending;
            if (_certsListSortBy.Item1 == e.Column) sorter.Order = _certsListSortBy.Item2 ? SortOrder.Descending : SortOrder.Ascending;
            _certsListSortBy = (e.Column, sorter.Order == SortOrder.Ascending);
            certList.ListViewItemSorter = sorter;
            certList.Sorting = sorter.Order;
            certList.Sort();
            ColumnAddSortSymbol(certList, e.Column, sorter.Order == SortOrder.Ascending);
        }

        private void certList_SelectedIndexChanged(object sender, EventArgs e)
        {
            cInfo.Items.Clear();
            if (certList.SelectedIndices.Count == 0) return;
            ListViewDetailedItem pvi = (ListViewDetailedItem)certList.SelectedItems[0];
            if (pvi.CertificateHash == null) return;
            SetCandidate(pvi.CertificateHash);
            DetailizeCert(pvi.CertificateHash);
        }

        private void selSert_SelectedIndexChanged(object sender, EventArgs e)
        {
            cInfo.Items.Clear();
            if (selSert.Items.Count == 0) return;
            ListViewDetailedItem pvi = (ListViewDetailedItem)selSert.Items[0];
            if (pvi.CertificateHash == null) return;
            DetailizeCert(pvi.CertificateHash);
        }

        private void sertMenuStrip_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            clrsrtl.Enabled = usBtn.Enabled = sstdRL.Enabled = sstdAF.Enabled = sstdRF.Enabled = false;
            if (certList.SelectedIndices.Count > 0)
            {
                usBtn.Enabled = true;
                sstdRL.Enabled = storageSelector.SelectedIndex == 2 || storageSelector.SelectedIndex == 5;
                ListViewDetailedItem pvi = (ListViewDetailedItem)certList.SelectedItems[0];
                if (pvi.Favorite) sstdRF.Enabled = true; else sstdAF.Enabled = true;
            };
            if (certList.Items.Count > 0)
            {
                clrsrtl.Enabled = storageSelector.SelectedIndex == 2 || storageSelector.SelectedIndex == 4 || storageSelector.SelectedIndex == 5;
            };
        }

        private void sstdAF_Click(object sender, EventArgs e)
        {
            if (certList.SelectedIndices.Count == 0) return;
            AddCertToFav((ListViewDetailedItem)certList.SelectedItems[0]);
        }

        private void sstdRF_Click(object sender, EventArgs e)
        {
            if (certList.SelectedIndices.Count == 0) return;
            ListViewDetailedItem lvi = (ListViewDetailedItem)certList.SelectedItems[0];
            RemoveCertFromFav(lvi.CertificateHash);
            lvi.Favorite = false;
            if(storageSelector.SelectedIndex == 4) ReloadCertificates(storageSelector.SelectedIndex);
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            firstLoad = false;
            RegisterFileAssociations();
            ProcessCmdArgFiles();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            ReloadCertificates(storageSelector.SelectedIndex);
        }

        private void cInfo_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            ListViewSorter.ListViewColumnSorter sorter = new ListViewSorter.ListViewColumnSorter();
            sorter.SortColumn = e.Column;
            sorter.Order = SortOrder.Ascending;

            if (_cInfoSortBy.Item1 == e.Column) sorter.Order = _cInfoSortBy.Item2 ? SortOrder.Descending : SortOrder.Ascending;
            _cInfoSortBy = (e.Column, sorter.Order == SortOrder.Ascending);
            cInfo.ListViewItemSorter = sorter;
            cInfo.Sorting = sorter.Order;
            cInfo.Sort();
            ColumnAddSortSymbol(cInfo, e.Column, sorter.Order == SortOrder.Ascending);
        }

        private void sstdRL_Click(object sender, EventArgs e)
        {
            if (certList.SelectedIndices.Count == 0) return;
            ListViewDetailedItem lvi = (ListViewDetailedItem)certList.SelectedItems[0];
            if (storageSelector.SelectedIndex == 2) RemoveCertFromDropped(lvi.CertificateHash);                
            if (storageSelector.SelectedIndex == 5) RemoveCertFromHistory(lvi.CertificateHash);                
            ReloadCertificates(storageSelector.SelectedIndex);
            int si = certList.SelectedItems[0].Index;
            while (si >= 0)
            {
                if (certList.Items.Count > si)
                {
                    certList.Items[si].Selected = true;
                    certList.EnsureVisible(si);
                    break;
                };
                si--;
            };
        }

        private void certList_DoubleClick(object sender, EventArgs e)
        {
            if (certList.SelectedIndices.Count == 0) return;
            ListViewDetailedItem lvi = (ListViewDetailedItem)certList.SelectedItems[0];
            lvi.Current = true;
            SelectCurrentCert(lvi.CertificateHash);            
        }

        private void usBtn_Click(object sender, EventArgs e)
        {
            if (certList.SelectedIndices.Count == 0) return;
            ListViewDetailedItem lvi = (ListViewDetailedItem)certList.SelectedItems[0];
            lvi.Current = true;
            SelectCurrentCert(lvi.CertificateHash);
        }

        private void clrsrtl_Click(object sender, EventArgs e)
        {
            if (certList.Items.Count == 0) return;
            if (storageSelector.SelectedIndex == 2) iniFile.Dropped.Clear();
            if (storageSelector.SelectedIndex == 4)
            {
                for (int i = certList.Items.Count - 1; i >= 0; i--)
                {
                    ListViewDetailedItem lvi = (ListViewDetailedItem)certList.Items[i];
                    RemoveCertFromFav(lvi.CertificateHash);
                    iniFile.Favorites.Clear();
                };
            };
            if (storageSelector.SelectedIndex == 5) iniFile.History.Clear();
            ReloadCertificates(storageSelector.SelectedIndex);
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            string tts = textBox1.Text;
            if(certList.Items.Count == 0) return;
            for (int i = 0; i < certList.Items.Count; i++)
                certList.Items[i].ForeColor = SystemColors.WindowText;
            if (string.IsNullOrEmpty(tts)) return;
            int loc = -1;
            for (int i = 0; i < certList.Items.Count; i++)
                for (int x = 0; x < certList.Items[i].SubItems.Count; x++)
                    if (certList.Items[i].SubItems[x].Text.Contains(tts))
                    {
                        certList.Items[i].ForeColor = Color.Red;
                        if (loc < 0) loc = i;
                    };
            if (loc >= 0 && tts.Length > 1)
            {
                certList.EnsureVisible(loc);
                certList.Items[loc].Focused = true;
            };
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            iniFile.Maximized = this.WindowState == FormWindowState.Maximized;
        }

        private void ResizeSplitter()
        {
            int cwidth = 0;
            for(int i = 0; i < certList.Columns.Count; i++) cwidth += certList.Columns[i].Width;
            if ((this.Width - cwidth) < 300) cwidth = this.Width - 300;
            splitContainer1.SplitterDistance = cwidth;
        }

        private void dsval_Click(object sender, EventArgs e)
        {
            iniFile.checkCertValid = dsval.Checked = !dsval.Checked;
        }

        private void filesListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (filesListView.SelectedItems.Count == 0)
            {
                fii.Text = "";
                return;
            };
            
            if (!string.IsNullOrEmpty(filesListView.SelectedItems[0].SubItems[5].Text)) fileStatus.Text = filesListView.SelectedItems[0].SubItems[5].Text;
            else if (!string.IsNullOrEmpty(filesListView.SelectedItems[0].SubItems[4].Text)) fileStatus.Text = filesListView.SelectedItems[0].SubItems[4].Text;
            else fileStatus.Text = filesListView.SelectedItems[0].SubItems[3].Text;

            try
            {
                FileInfo fi = new FileInfo(filesListView.SelectedItems[0].SubItems[1].Text);
                fii.Text = $"Размер: {GetFileSize(fi.FullName, out _)}, создан: {fi.CreationTime}, " +
                    $"изменен: {fi.LastWriteTime}, тип: {filesListView.SelectedItems[0].SubItems[2].Text}, " +
                    $"папка: {Path.GetFileName(fi.DirectoryName)}";
            }
            catch { };
        }

        private void delCur_Click(object sender, EventArgs e)
        {
            if (filesListView.SelectedItems.Count == 0) return;
            int si = filesListView.SelectedItems[0].Index;
            filesListView.Items.RemoveAt(si);
            fileStatus.Text = $"Удален 1 документ";
            ResaveHistory();
            while (si >= 0)
            {
                if (filesListView.Items.Count > si)
                {
                    filesListView.Items[si].Selected = true;
                    filesListView.EnsureVisible(si);
                    break;
                };
                si--;
            };
        }

        private void sicur_Click(object sender, EventArgs e)
        {
            if (filesListView.SelectedItems.Count == 0) return;
            bool sign = filesListView.SelectedItems[0].SubItems[2].Text == "документ";
            ProcessFiles(sign, !sign, new List<string>(new string[] { filesListView.SelectedItems[0].SubItems[1].Text } ));
            filesListView_SelectedIndexChanged(sender, e);
        }

        private void restat_Click(object sender, EventArgs e)
        {
            if (filesListView.Items.Count == 0) return;
            for (int i = 0; i < filesListView.Items.Count; i++)
            {
                filesListView.Items[i].BackColor = SystemColors.Window;
                filesListView.Items[i].SubItems[3].Text = "";
                filesListView.Items[i].SubItems[4].Text = "";
                filesListView.Items[i].SubItems[5].Text = "";
            };
        }

        private void refbtn_Click(object sender, EventArgs e)
        {
            ReloadCertificates(storageSelector.SelectedIndex);
        }

        private void addfs_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Добавление файлов";
            ofd.Filter = "Все типы (*.*)|*.*";
            ofd.Multiselect = true;
            if(ofd.ShowDialog() == DialogResult.OK) 
                ProcessDroppedFiles(ofd.FileNames, "Импортировано");
            ofd.Dispose();
        }

        private void certList_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 'd') sstdRL_Click(sender, e);
            if (e.KeyChar == '\r') usBtn_Click(sender, e);
        }

        private void filesListView_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 'a') addfs_Click(sender, e);
            if (e.KeyChar == 'd') delCur_Click(sender, e);
            if (e.KeyChar == 'i') ScanFolder();
            if (e.KeyChar == '\r') sicur_Click(sender, e);
            if (e.KeyChar == ' ') ClearCurrentFileInfo();            
        }

        private void rsltd_Click(object sender, EventArgs e)
        {
            ClearCurrentFileInfo();
        }

        private void adScanned_Click(object sender, EventArgs e)
        {
            ScanFolder(true);
        }

        private void ScanFolder(bool showdialog = true)
        {
            List<string> ftypes = new List<string>(new string[] 
            {
                "Документы PDF (*.pdf)",
                "Документы Word (*.doc,*.docx)",
                "Документы Excel (*.xls,*.xlsx)",
                "Документы PDF + Word + Excel (*.doc,*.docx,*.xls,*.xlsx,*.pdf)",
                "Файлы подписей (*.p7b)",
                "Счета и акты (*счет*.*,*акт*.*)", 
                "Счета (*счет*.*)", 
                "Акты (*акт*.*)", 
                "Все типы файлов (*.*)" 
            });

            string str = iniFile.ImportScanType;
            if (string.IsNullOrEmpty(str)) str = ftypes[3];
            if (!ftypes.Contains(str)) ftypes.Add(str);
            string path = iniFile.ImportScanPath;
            if (string.IsNullOrEmpty(path)) path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            if (showdialog)
            {
                InputBox.defWidth = 500;
                if (InputBox.QueryListBox("Сканирование папки", "Выберите тип файлов (или введите свой фильтр через запятую):", ftypes.ToArray(), ref str, true) != DialogResult.OK) return;
            };

            List<string> fexts = new List<string>();
            Regex rx = new Regex(@"[^;.,\(\)]+\.[^;.,\(\)]+", RegexOptions.None);
            MatchCollection mc = rx.Matches(str);
            if (mc.Count == 0)
            {
                MessageBox.Show($"Введено недопустимое значение фильтра\r\nЗначение: {str}", "Сканирование папки", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            };
            foreach (Match mx in mc) fexts.Add(mx.Groups[0].Value.Trim());

            if (showdialog)
            {
                if (InputBox.QueryDirectoryBox("Сканирование папки", "Выберите папку:", ref path) != DialogResult.OK) return;
            };
            if (!Directory.Exists(path))
            {
                MessageBox.Show($"Указаная папка не найдена\r\nПуть: {path}", "Сканирование папки", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            };

            iniFile.ImportScanType = str;
            iniFile.ImportScanPath = path;

            List<string> files = new List<string>();
            foreach(string f in fexts) 
            { try { files.AddRange(Directory.GetFiles(path, f, SearchOption.AllDirectories)); } catch { }; }
                            
            if(files.Count > 0) ProcessDroppedFiles(files.ToArray(), "Найдено", false);
        }

        private void addScanned2_Click(object sender, EventArgs e)
        {
            ScanFolder(false);
        }

        private void selSert_ColumnWidthChanged(object sender, ColumnWidthChangedEventArgs e)
        {
            if (firstLoad) return;
            iniFile.CSLVHL = new int[selSert.Columns.Count];
            for (int i = 0; i < selSert.Columns.Count; i++)
                if(selSert.Columns[i].Width > 10)
                    iniFile.CSLVHL[i] = selSert.Columns[i].Width;
        }

        private void certList_ColumnWidthChanged(object sender, ColumnWidthChangedEventArgs e)
        {
            if (firstLoad) return;
            iniFile.SSLVHL = new int[certList.Columns.Count];
            for (int i = 0; i < certList.Columns.Count; i++)
                if (certList.Columns[i].Width > 10)
                    iniFile.SSLVHL[i] = certList.Columns[i].Width;
            // ResizeSplitter();
        }

        private void filesListView_ColumnWidthChanged(object sender, ColumnWidthChangedEventArgs e)
        {
            if (firstLoad) return;
            iniFile.FFLVHL = new int[filesListView.Columns.Count];
            for (int i = 0; i < filesListView.Columns.Count; i++)
                if (filesListView.Columns[i].Width > 10)
                    iniFile.FFLVHL[i] = filesListView.Columns[i].Width;
        }

        private void filesListView_DoubleClick(object sender, EventArgs e)
        {
            sicur_Click(sender, e);
        }

        private void RegisterFileAssociations()
        {
            string[] sext = new string[] { "txt", "rtf", "pwi", "ttf", "doc", "docx", "docm", "dot", "dotx", "epub", "ods", "odt", "pdf", "pot", "potm", "potx", "pps", "ppsm", "ppsx", "ppt", "pptm", "pptx", "sldm", "wps", "xar", "xls", "xlsb", "xlsm", "xlsx", "xlt", "xltm", "xltx", "xml", "xps" };           
            foreach(string s in sext)
                FileAss.SetFileAssociation(s, null, "sign", System.Reflection.Assembly.GetExecutingAssembly().Location, "Подписать подписакой");

            string[] cext = new string[] { "p7s", "sig", "sgn" };
            foreach (string s in cext)
                FileAss.SetFileAssociation(s, null, "check", System.Reflection.Assembly.GetExecutingAssembly().Location, "Проверить подписакой");
            
            FileAss.UpdateExplorer();
        }

        private void sm1_Click(object sender, EventArgs e)
        {
            ClickSM(1);
        }

        private void sm2_Click(object sender, EventArgs e)
        {
            ClickSM(2);
        }

        private void sm3_Click(object sender, EventArgs e)
        {
            ClickSM(3);
        }

        private void sm4_Click(object sender, EventArgs e)
        {
            ClickSM(4);
        }

        private void ClickSM(byte index)
        {
            sm1.Checked = index == 1;
            sm2.Checked = index == 2;
            sm3.Checked = index == 3;
            sm4.Checked = index == 4;
            iniFile.signCreateMethod = (byte)index;
        }

        private void btnView_Click(object sender, EventArgs e)
        {
            try { System.Diagnostics.Process.Start(filesListView.SelectedItems[0].SubItems[1].Text); }
            catch { };
        }

        private void addSiSaBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            iniFile.AddStampMode = (byte)addSiSaBox.SelectedIndex;
            saepBox.Enabled = addSiSaBox.SelectedIndex > 0 && saedBox.SelectedIndex == 0;
            if (saedBox.SelectedIndex > 0) saepBox.SelectedIndex = 0;
            saipBox.Enabled = addSiSaBox.SelectedIndex > 0;
            sanf.Enabled = addSiSaBox.SelectedIndex > 0 || saedBox.SelectedIndex > 0;            
            fBox.Enabled = addSiSaBox.SelectedIndex > 0;
            addSiFiBox.Enabled = addSiSaBox.SelectedIndex > 0;
        }

        private void addSiFiBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            string f = addSiFiBox.Text;
            if(f == "Нет") iniFile.AddStampFile = ""; else iniFile.AddStampFile = f;
            LoadImagePreview(iniFile.AddStampFile);

            CertificateHash ch = null;
            if (selSert.Items.Count > 0) ch = ((ListViewDetailedItem)selSert.Items[0]).CertificateHash;
            LoadStampPreview(iniFile.AddStampFile, ch, Color.Maroon);
        }

        private void addSiFiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (addSiFiles.SelectedItems.Count == 0) return;
            addSiFiBox.SelectedIndex = addSiFiles.SelectedIndex + 1;
        }

        private void btnStamp_Click(object sender, EventArgs e)
        {
            if (filesListView.SelectedItems.Count == 0) return;
            string fn = filesListView.SelectedItems[0].SubItems[1].Text;
            if (Path.GetExtension(fn).ToLower() != ".pdf")
                MessageBox.Show($"Файл {Path.GetFileName(fn)} не является PDF-документом", "Добавление оттиска подписи в документ", MessageBoxButtons.OK, MessageBoxIcon.Error);
            else
                AddStampToPDF(fn);
        }

        private void AddStampToPDF(string fn, string newFileName = null)
        {
            CertificateHash ch = null;
            if (string.IsNullOrEmpty(iniFile.currentCertificate))
            {
                fileStatus.Text = "Не выбран сертификат для подписи";
                return;
            }
            else
            {
                ch = GetCurrentCert(iniFile.currentCertificate);
                if (ch == null || (!string.IsNullOrEmpty(ch.File) && (!File.Exists(ch.File))))
                {
                    fileStatus.Text = "Не найден сертификат для подписи";
                    return;
                };
            };

            try
            {
                string stampFile = Path.Combine(XMLSaved<int>.CurrentDirectory(), iniFile.AddStampFile);
                if (string.IsNullOrEmpty(stampFile) || (!File.Exists(stampFile)))
                    MessageBox.Show($"Файл штампа не найден: {iniFile.AddStampFile}", "Добавление штампа", MessageBoxButtons.OK, MessageBoxIcon.Error);
                else
                {
                    if (!string.IsNullOrEmpty(newFileName))
                    {
                        File.Copy(fn, newFileName, true);
                        fn = newFileName;
                    };
                    PDFSignData sd = new PDFSignData() { Mode = PDFSignData.StampMode.OnlyStamp, Offset = (PDFSignData.StampOffset)iniFile.AddStampOnPage, EachPage = iniFile.AddStampEachPage > 0, AddAnnot = iniFile.AddAnnot };
                    bool added = PDFStamper.AddStamp(stampFile, fontFamily, ch, fn, sd);
                    if (added)
                        MessageBox.Show($"Штамп успешно добавлен в файл `{Path.GetFileName(fn)}`", "Добавление штампа", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    else
                    {
                        try { if ((!string.IsNullOrEmpty(newFileName)) && File.Exists(fn)) File.Delete(newFileName); } catch { };
                        MessageBox.Show($"Не удалось добавить штамп в файл `{Path.GetFileName(fn)}`\r\n\r\nВозможно штам уже был добавлен ранее!", "Добавление штампа", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    };
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{ex.Message}", "Добавление штампа", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
        }

        private void stnew_Click(object sender, EventArgs e)
        {
            if (filesListView.SelectedItems.Count == 0) return;
            string fn = filesListView.SelectedItems[0].SubItems[1].Text;
            if (Path.GetExtension(fn).ToLower() != ".pdf")
                MessageBox.Show($"Файл {Path.GetFileName(fn)} не является PDF-документом", "Добавление оттиска подписи в документ", MessageBoxButtons.OK, MessageBoxIcon.Error);
            else
            {
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.InitialDirectory = Path.GetDirectoryName(fn);
                sfd.Filter = "PDF Files (*.pdf)|*.pdf";
                sfd.FileName = $"{Path.Combine(Path.GetDirectoryName(fn), Path.GetFileNameWithoutExtension(fn) + "_stamped" + Path.GetExtension(fn))}";
                sfd.DefaultExt = ".pdf";
                if (sfd.ShowDialog() == DialogResult.OK)
                    AddStampToPDF(fn, sfd.FileName);
                sfd.Dispose();
            };
        }

        private void fBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            foreach (FontFamily ff in pfc.Families)
                if (ff.Name == fBox.Text)
                {
                    fontFamily = ff;
                    iniFile.AddStampFont = ff.Name;
                };
        }

        private void saepBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            iniFile.AddStampEachPage = (byte)saepBox.SelectedIndex;
        }

        private void saipBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            iniFile.AddStampOnPage = (byte)saipBox.SelectedIndex;
        }

        private void saedBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            iniFile.AddSignToDoc = (byte)saedBox.SelectedIndex;
            saepBox.Enabled = addSiSaBox.SelectedIndex > 0 && saedBox.SelectedIndex == 0;
            if (saedBox.SelectedIndex > 0) saepBox.SelectedIndex = 0;
            sanf.Enabled = addSiSaBox.SelectedIndex > 0 || saedBox.SelectedIndex > 0;
            panel2.Enabled = saedBox.SelectedIndex > 0;
        }

        private void sanf_SelectedIndexChanged(object sender, EventArgs e)
        {
            iniFile.AddSignToNewDoc = (byte)sanf.SelectedIndex;
        }

        private void tsAuthor_TextChanged(object sender, EventArgs e)
        {
            iniFile.Author = tsAuthor.Text;
        }

        private void tsReason_TextChanged(object sender, EventArgs e)
        {
            iniFile.Reason = tsReason.Text;
        }

        private void tsContact_TextChanged(object sender, EventArgs e)
        {
            iniFile.Contact = tsContact.Text;
        }

        private void tsLocation_TextChanged(object sender, EventArgs e)
        {
            iniFile.Location = tsLocation.Text;
        }

        private void adanaBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            iniFile.AddAnnot = adanaBox.SelectedIndex > 0;
        }

        private Bitmap LoadImagePreview(string file)
        {
            pb1.Image = null;
            if (string.IsNullOrEmpty(file)) return null;
            if (file.Contains(":")) file = Path.Combine(XMLSaved<int>.CurrentDirectory(), file);
            if (!File.Exists(file)) return null;

            Bitmap b = null;
            try
            {
                b = (Bitmap)Bitmap.FromFile(file);
                if (b.Width > pb1.Width || b.Height > pb1.Height)
                    pb1.Image = ResizeImage(b, pb1.Width, pb1.Height);
                else
                    pb1.Image = b;
            }
            catch { };
            return b;
        }

        private Bitmap LoadStampPreview(string file, CertificateHash ch, Color color, bool askPass = false)
        {
            pb2.Image = null;
            if (string.IsNullOrEmpty(file)) return null;
            if (ch == null) return null;
            if (ch.Certificate == null) try { ch = GetCurrentCert(ch.Thumbprint); } catch { };
            if (ch.Certificate == null) try {
                    string pass = null;
                    if(ch.Source == "File" && askPass)
                    {
                        DialogResult dr = InputBox.QueryPass("Подписание документов", $"Введите пароль для {Path.GetFileName(ch.File)}:", ref pass);
                        if (dr != DialogResult.OK) return null;
                    };
                    ch.Certificate = (new PKCS7Signer(ch.File, pass)).Certificate; 
                } catch { };
            if (ch.Certificate == null) return null;
            if (file.Contains(":")) file = Path.Combine(XMLSaved<int>.CurrentDirectory(), file);
            if (!File.Exists(file)) return null;

            Bitmap b = null;
            try
            {
                b = (Bitmap)Bitmap.FromFile(file);
                PDFStamper.PrepareStampBitmap(b, ch, fontFamily, color);
                if (b.Width > pb2.Width || b.Height > pb2.Height)
                    pb2.Image = ResizeImage(b, pb2.Width, pb2.Height);
                else
                    pb2.Image = b;
            }
            catch { };
            return b;
        }

        public static Bitmap ResizeImage(Image image, int width, int height, bool keepAspectRation = true)
        {
            int sourceWidth = image.Width;
            int sourceHeight = image.Height;
            int sourceX = 0;
            int sourceY = 0;
            int destX = 0;
            int destY = 0;

            int destWidth = width;
            int destHeight = height;

            if (keepAspectRation)
            {
                float nPercent = 0;
                float nPercentW = ((float)width / (float)sourceWidth);
                float nPercentH = ((float)height / (float)sourceHeight);

                if (nPercentH < nPercentW)
                {
                    nPercent = nPercentH;
                    destX = System.Convert.ToInt16((width - (sourceWidth * nPercent)) / 2);
                }
                else
                {
                    nPercent = nPercentW;
                    destY = System.Convert.ToInt16((height - (sourceHeight * nPercent)) / 2);
                };

                destWidth = (int)(sourceWidth * nPercent);
                destHeight = (int)(sourceHeight * nPercent);
            };

            Bitmap bmPhoto = new Bitmap(width, height);//, PixelFormat.Format24bppRgb);
            //bmPhoto.SetResolution(image.HorizontalResolution, image.VerticalResolution);
            Graphics g = Graphics.FromImage(bmPhoto);
            //g.Clear(Color.White);
            //g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(image, new Rectangle(destX, destY, destWidth, destHeight), new Rectangle(sourceX, sourceY, sourceWidth, sourceHeight), GraphicsUnit.Pixel);
            g.Dispose();
            return bmPhoto;
        }

        private void pb2_DoubleClick(object sender, EventArgs e)
        {
            if (pb2.Image == null) return;
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Title = "Сохранить PNG";
            sfd.DefaultExt = ".png";
            sfd.Filter = "Portable Network Graphics File (*.png)|*.png";
            if(sfd.ShowDialog() == DialogResult.OK)
            {
                CertificateHash ch = null;
                if (selSert.Items.Count > 0) ch = ((ListViewDetailedItem)selSert.Items[0]).CertificateHash;
                Bitmap bmp = LoadStampPreview(iniFile.AddStampFile, ch, Color.Black, true);
                if(bmp != null) SavePNG(sfd.FileName, bmp);
            };
            sfd.Dispose();
        }

        private void pb1_DoubleClick(object sender, EventArgs e)
        {
            if (pb1.Image == null) return;
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Title = "Сохранить PNG";
            sfd.DefaultExt = ".png";
            sfd.Filter = "Portable Network Graphics File (*.png)|*.png";
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                Bitmap bmp = LoadImagePreview(iniFile.AddStampFile);
                if (bmp != null) SavePNG(sfd.FileName, bmp);
            };
            sfd.Dispose();
        }

        private void SavePNG(string file, Image bmp)
        {
            bmp.Save(file);
        }

        private void pb2_Click(object sender, EventArgs e)
        {
            string f = addSiFiBox.Text;
            if (f == "Нет") iniFile.AddStampFile = ""; else iniFile.AddStampFile = f;
            
            CertificateHash ch = null;
            if (selSert.Items.Count > 0) ch = ((ListViewDetailedItem)selSert.Items[0]).CertificateHash;
            LoadStampPreview(iniFile.AddStampFile, ch, Color.Maroon, true);
        }

        private void cpyBtn_Click(object sender, EventArgs e)
        {
            if (cInfo.Items.Count == 0) return;
            string txt = "Тип;Имя;Значение;\r\n";
            for(int i=0;i<cInfo.Items.Count;i++) 
            {
                for (int j = 0; j < cInfo.Items[i].SubItems.Count; j++)
                    txt += $"{cInfo.Items[i].SubItems[j].Text};";
                txt += $"\r\n";
            };
            Clipboard.SetText(txt);
            MessageBox.Show("Скопировано в буфер обмена","Информация о сертификате", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void svImg_Click(object sender, EventArgs e)
        {
            if (pb_over == null) return;
            if (!(pb_over is PictureBox)) return;
            string pb = ((PictureBox)pb_over).Name;
            if (pb == "pb1") pb1_DoubleClick(sender, e);
            if (pb == "pb2") pb2_DoubleClick(sender, e);
        }

        private void pb2_MouseHover(object sender, EventArgs e)
        {
            pb_over = sender;
            if (pb_over == null) return;
            if (!(pb_over is PictureBox)) return;
            string pb = ((PictureBox)pb_over).Name;
            renewim.Enabled = pb == "pb2";
        }

        private void renewim_Click(object sender, EventArgs e)
        {
            pb2_Click(sender, e);
        }

        private void inccb_CheckedChanged(object sender, EventArgs e)
        {
            iniFile.inccb = inccb.Checked;
        }

        private void textBox2_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && e.Alt)
                button2_Click(sender, e);
        }

        private void textBox3_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && e.Alt)
                button3_Click(sender, e);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            textBox3.Text = "В процессе...";
            Application.DoEvents();
            System.Threading.Thread.Sleep(125);
            textBox3.Text = EncryptText(textBox2.Text);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            textBox2.Text = "В процессе...";
            Application.DoEvents();
            System.Threading.Thread.Sleep(125);
            textBox2.Text = DecryptText(textBox3.Text);
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try { System.Diagnostics.Process.Start(dixuUrl); } catch { };
        }

        private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try { System.Diagnostics.Process.Start(dixuUrl); } catch { };
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {
            iniFile.TextToEncrypt = textBox2.Text;
        }

        private void textBox3_TextChanged(object sender, EventArgs e)
        {
            iniFile.TextToDecrypt = textBox3.Text;
        }

        private void button4_Click(object sender, EventArgs e)
        {
            textBox2.Clear();
        }

        private void button5_Click(object sender, EventArgs e)
        {
            textBox3.Clear();
        }
    }
}
