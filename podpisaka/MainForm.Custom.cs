using dkxce;
using podpisaka;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml;

namespace DigitalCertAndSignMaker
{
    public partial class MainForm : Form
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool AppendMenu(IntPtr hMenu, int uFlags, int uIDNewItem, string lpNewItem);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool InsertMenu(IntPtr hMenu, int uPosition, int uFlags, int uIDNewItem, string lpNewItem);

        private string formCaption = "ПОДПИСАКА";
        private string webURLS = "https://github.com/dkxce";
        private string webURL = "https://github.com/dkxce/Pospisaka";

        private bool firstLoad = true;
        private string iniPath = Path.Combine(XMLSaved<int>.CurrentDirectory(), "podpisaka.xml");        
        private (int, bool) _fileListViewSortBy = (-1, true);
        private (int, bool) _selSertSortBy = (-1, true);
        private (int, bool) _certsListSortBy = (-1, true);
        private (int, bool) _cInfoSortBy = (-1, true);

        private List<string> certExt = new List<string>(new string[] { ".p12", ".pfx", ".crt", ".p7b", ".cer", ".pem" });
        private List<string> signExt = new List<string>(new string[] { ".p7s", ".sig", ".sgn" });

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            IntPtr hSysMenu = GetSystemMenu(this.Handle, false);
            AppendMenu(hSysMenu, 0x000, 0x01, "Author: dkxce");            
            AppendMenu(hSysMenu, 0x000, 0x02, webURL);
            AppendMenu(hSysMenu, 0x800, 0x03, string.Empty);            
            AppendMenu(hSysMenu, 0x000, 0x04, "Создать ярлык на Рабочем столе");
            AppendMenu(hSysMenu, 0x000, 0x05, "Создать ярлык в меню Пуск");
            AppendMenu(hSysMenu, 0x800, 0x06, string.Empty);            
            AppendMenu(hSysMenu, 0x000, 0x07, "Открыть менеджер сертификатов Windows ... ");
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if ((m.Msg == 0x112) && ((int)m.WParam == 0x01)) // Author
            {
                try { System.Diagnostics.Process.Start(webURLS); } catch { };
            };
            if ((m.Msg == 0x112) && ((int)m.WParam == 0x02))
            {
                try { System.Diagnostics.Process.Start(webURL); } catch { };
            };
            if ((m.Msg == 0x112) && ((int)m.WParam == 0x04))
            {
                try
                {
                    string path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    string file = Path.Combine(path, $"{formCaption}.lnk");
                    ShellLink sl = new ShellLink(file);
                    sl.TargetPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    sl.Description = "Запустить мастер подписей {formCaption}";
                    sl.Save();
                }
                catch { };
            };
            if ((m.Msg == 0x112) && ((int)m.WParam == 0x05))
            {
                try
                {
                    string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),$"{formCaption}");
                    try { Directory.CreateDirectory(path); } catch { };
                    string file = Path.Combine(path, $"{formCaption}.lnk");
                    ShellLink sl = new ShellLink(file);
                    sl.TargetPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    sl.Description = "Запустить мастер подписей {formCaption}";
                    sl.Save();
                }
                catch { };
            };  
            if ((m.Msg == 0x112) && ((int)m.WParam == 0x07))
            {
                try { System.Diagnostics.Process.Start("certmgr.msc"); } catch { };
            };            
        }

        private void ClearCurrentFileInfo()
        {
            if (filesListView.SelectedItems.Count == 0) return;
            for (int i = 0; i < filesListView.SelectedItems.Count; i++)
            {
                filesListView.SelectedItems[i].BackColor = SystemColors.Window;
                filesListView.SelectedItems[i].SubItems[3].Text = "";
                filesListView.SelectedItems[i].SubItems[4].Text = "";
                filesListView.SelectedItems[i].SubItems[5].Text = "";
            };
        }

        private void ProcessDroppedFiles(string[] files, string suff = "Переброшено", bool overwrite = true)
        {
            if ((!overwrite) && (files != null) && (files.Length > 0))
            {
                List<string> ff = new List<string>(files);
                for (int i = ff.Count - 1; i >= 0; i--)
                {
                    string f1 = ff[i];
                    bool rem = false;
                    for (int x = 0; x < filesListView.Items.Count; x++)
                    {
                        string f2 = filesListView.Items[x].SubItems[1].Text;
                        if (string.Compare(f1, f2, true) == 0)
                            rem = true;
                    };
                    if (rem) ff.RemoveAt(i);
                };
                files = ff.ToArray();
                if(files.Length == 0)
                {
                    MessageBox.Show("Подходящих файлов не найдено", "Импорт файлов", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                };
            };

            Func<string, bool> FileIsDir = (f) => { return File.GetAttributes(f).HasFlag(FileAttributes.Directory); };
            Func<string, string[]> SearchFiles = (d) => { return Directory.GetFiles(d, "*.*", SearchOption.AllDirectories); };
            Func<string, string> GetFileExt = (f) => { return Path.GetExtension(f).ToLower(); };            

            int indirs = 0;
            List<string> list = new List<string>();
            foreach (string f in files)
            {
                try { if (!FileIsDir(f)) { list.Add(f); continue; }; } catch { continue; };
                if (list.Count > 0 && indirs == 0) indirs++;
                indirs++;
                try { list.AddRange(SearchFiles(f)); } catch { };
            };            

            List<string> lDocs = new List<string>();
            List<string> lSigns = new List<string>();
            List<string> lCerts = new List<string>();            

            foreach (string f in list)
            {
                string ext = GetFileExt(f);
                if (certExt.Contains(ext)) lCerts.Add(f);
                else if (signExt.Contains(ext)) lSigns.Add(f);
                else lDocs.Add(f);
            };

            int sum = lDocs.Count + lSigns.Count + lCerts.Count;
            int sum_ds = lDocs.Count + lSigns.Count;
            if (sum == 0) return;

            string text_0 = indirs == 0 ? $"{suff} {sum} файлов." : $"{suff} {sum} файлов из {indirs} папок.";
            string text_1 = text_0 + " Выберите действие:";

            fileStatus.Text = text_0;
            Application.DoEvents();

            List<string> toDo = new List<string>();
            List<string> questions = new List<string>();
            int selIndex = 0;

            if (lCerts.Count > 0 & lDocs.Count > 0 && lSigns.Count > 0) { toDo.Add("cds"); questions.Add($"Добавить {lCerts.Count} сертификатов, {lDocs.Count} документов и {lSigns.Count} подписей"); };
            if (lCerts.Count > 0 & lDocs.Count > 0) { toDo.Add("cd"); questions.Add($"Добавить {lCerts.Count} сертификатов и {lDocs.Count} документов для подписи"); };
            if (lCerts.Count > 0 & lSigns.Count > 0) { toDo.Add("cs"); questions.Add($"Добавить {lCerts.Count} сертификатов и {lSigns.Count} подписей для проверки"); };
            if (lDocs.Count > 0 && lSigns.Count > 0) { toDo.Add("ds"); questions.Add($"Добавить {lDocs.Count} документов для подписания и {lSigns.Count} подписей для проверки"); };
            if (lCerts.Count > 0) { toDo.Add("c"); questions.Add($"Добавить {lCerts.Count} найденных сертификатов"); };
            if (lDocs.Count > 0) { toDo.Add("d"); questions.Add($"Добавить {lDocs.Count} найденных документов для подписи"); };
            if (lSigns.Count > 0) { toDo.Add("s"); questions.Add($"Добавить {lSigns.Count} найденных подписей для проверки"); };

            if (toDo.Count == 1) 
            {
                if (MessageBox.Show($"{text_0}\r\n{questions[0]}?", "Добавление файлов", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No) return; 
            }
            else {
                InputBox.defWidth = 500;
                if (InputBox.QueryListBox("Добавление файлов", $"{text_1}", questions.ToArray(), ref selIndex) != DialogResult.OK) return;
            };

            fileStatus.Text = $"{questions[selIndex]}...";
            Application.DoEvents();

            string wtd = toDo[selIndex];
            int added = 0;
            if (wtd.IndexOf("d") >= 0) foreach (string f in lDocs) if (AddFile(f, false)) added++;
            if (wtd.IndexOf("s") >= 0) foreach (string f in lSigns) if (AddFile(f, true)) added++;            

            fileStatus.Text = $"Добавлено {added} из {sum_ds} файлов";
            if (added > 0 && txtLogOut.SelectedTab.Name != "tabFile")
                for (int i = 0; i < txtLogOut.TabPages.Count; i++)
                    if (txtLogOut.TabPages[i].Name == "tabFile")
                        txtLogOut.SelectedIndex = i;
            Application.DoEvents();

            if ((!string.IsNullOrEmpty(iniFile.currentCertificate) && (lDocs.Count > 0) && (added > 0) && (toDo.Count == 1)))
                if (MessageBox.Show($"{fileStatus.Text}\r\nПодписать {added} документов?", "Добавление файлов", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    ProcessFiles(true, false, lDocs);

            if ((!string.IsNullOrEmpty(iniFile.currentCertificate) && (lSigns.Count > 0) && (added > 0) && (toDo.Count == 1)))
                if (MessageBox.Show($"{fileStatus.Text}\r\nПроверить {added} подписей?", "Добавление файлов", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    ProcessFiles(false, true, lSigns);

            int c_added = 0;
            if (wtd.IndexOf("c") >= 0)
            {
                foreach(string f in lCerts)
                {
                    dkxce.PKCS7Signer.Source source = dkxce.PKCS7Signer.Source.File;
                    List<CertificateHash> cc = dkxce.PKCS7Signer.GetCertificatesFrom(source, f, iniFile.Hash, true);
                    foreach (CertificateHash ch in cc) { AddDropedCert(ch); c_added++; };
                };
                fileStatus.Text = $"Добавлено {added} из {sum_ds} файлов и {c_added} из {lCerts.Count} сертификатов";
                if (added > 0 && txtLogOut.SelectedTab.Name != "tabCERT")
                    for (int i = 0; i < txtLogOut.TabPages.Count; i++)
                        if (txtLogOut.TabPages[i].Name == "tabCERT")
                            txtLogOut.SelectedIndex = i;
                Application.DoEvents();                
                if (c_added > 0 && storageSelector.SelectedIndex == 2)
                    ReloadCertificates(storageSelector.SelectedIndex);
            };
        }

        private void LoadDocsFromHistory()
        {
            Func<string, string> GetFileExt = (f) => { return Path.GetExtension(f).ToLower(); };
            foreach (string f in iniFile.Docs)
            {
                bool sign = false;
                string ext = GetFileExt(f);
                if (signExt.Contains(ext)) sign = true;
                AddFile(f, sign, false);
            };
        }

        public static string GetFileSize(string filename, out long size)
        {
            size = 0;
            try
            {
                string[] sizes = { "B", "KB", "MB", "GB", "TB" };
                double len = size = new FileInfo(filename).Length;
                int order = 0;
                while (len >= 1024 && order < sizes.Length - 1)
                {
                    order++;
                    len = len / 1024;
                };

                return String.Format("{0:0.##} {1}", len, sizes[order]);
            }
            catch { };
            return "";
        }

        private bool AddFile(string f, bool toCheck, bool addToHistory = true)
        {
            RemoveFile(f);
            ListViewItem lvi = new ListViewItem($"{Path.GetFileName(f)}  [{GetFileSize(f, out long fSize)}]");
            if (!addToHistory) lvi.BackColor = Color.Bisque;
            if (fSize < 0) lvi.BackColor = Color.Silver;
            lvi.SubItems.Add(f);
            lvi.SubItems.Add(toCheck ? "подпись" : "документ");
            lvi.SubItems.Add(addToHistory ? $"Добавлен {DateTime.Now}" : $"Из истории");
            lvi.SubItems.Add("");
            lvi.SubItems.Add("");
            filesListView.Items.Add(lvi);

            if (addToHistory)
            {
                for (int i = iniFile.Docs.Count - 1; i >= 0; i--)
                    if (iniFile.Docs[i] == f)
                        iniFile.Docs.RemoveAt(i);
                iniFile.Docs.Add(f);
            };

            return true;
        }

        private void ResaveHistory()
        {
            iniFile.Docs.Clear();
            for (int i = 0; i < filesListView.Items.Count; i++)
                iniFile.Docs.Add(filesListView.Items[i].SubItems[1].Text);
        }

        private void RemoveFile(string f)
        {
            for (int i = filesListView.Items.Count - 1; i >= 0; i--)
                if (filesListView.Items[i].SubItems[1].Text == f)
                    filesListView.Items[i].Remove();
        }

        private void ProcessFiles(bool sign, bool check, List<string> onlyFiles = null)
        {
            if (filesListView.Items.Count == 0)
            {
                fileStatus.Text = "Нет файлов для подписи или проверки";
                return;
            };

            CertificateHash ch = null;
            if (string.IsNullOrEmpty(iniFile.currentCertificate))
            {
                fileStatus.Text = "Не выбран сертификат для подписи";
                return;
            }else
            {
                ch = GetCurrentCert(iniFile.currentCertificate);
                if(ch == null || (!string.IsNullOrEmpty(ch.File) && (!File.Exists(ch.File))))
                {
                    fileStatus.Text = "Не найден сертификат для подписи";
                    return;
                };
            };

            int sCount = 0;
            int cCount = 0;
            fileStatus.Text = "Сброс статусов...";
            for (int i = 0; i < filesListView.Items.Count; i++)
            {
                if (onlyFiles != null && (!onlyFiles.Contains(filesListView.Items[i].SubItems[1].Text))) continue;
                if ((sign && filesListView.Items[i].SubItems[2].Text == "документ")) sCount++;
                if ((check && filesListView.Items[i].SubItems[2].Text == "подпись")) cCount++;
                if ((sign && filesListView.Items[i].SubItems[2].Text == "документ") || (check && filesListView.Items[i].SubItems[2].Text == "подпись"))
                {
                    filesListView.Items[i].BackColor = SystemColors.Window;
                    filesListView.Items[i].SubItems[3].Text = "В обработке";
                    filesListView.Items[i].SubItems[4].Text = "";
                    filesListView.Items[i].SubItems[5].Text = "";
                };
            };

            fileStatus.Text = "Обработка...";
            Application.DoEvents();

            string pass = "";
            if (sign && (sCount > 0) && (!string.IsNullOrEmpty(ch.File)))
            {
                DialogResult dr = InputBox.QueryPass("Подписание документов", $"Введите пароль для {Path.GetFileName(ch.File)}:", ref pass);
                if(dr != DialogResult.OK)
                {
                    fileStatus.Text = "Прервано пользователем";
                    return;
                };
            };

            Application.DoEvents();
                        
            int counter = 0; int success = 0;
            for (int i = 0; i < filesListView.Items.Count; i++)
            {
                if (onlyFiles != null && (!onlyFiles.Contains(filesListView.Items[i].SubItems[1].Text))) continue;
                fileStatus.Text = $"Обработка {i + 1}/{filesListView.Items.Count}...";
                if (sign && filesListView.Items[i].SubItems[2].Text == "документ")
                {
                    counter++;
                    if (ProcessFile(true, false, filesListView.Items[i], ch, pass)) success++;
                };
                if (check && filesListView.Items[i].SubItems[2].Text == "подпись")
                {
                    counter++;
                    if (ProcessFile(false, true, filesListView.Items[i], ch, pass)) success++;
                };
                fileStatus.Text = $"Обработано {i + 1}/{filesListView.Items.Count}...";
            };
            fileStatus.Text = $"Готово, обработано {counter} файлов из {filesListView.Items.Count}, успешно: {success}";
        }

        private bool ProcessFile(bool sign, bool check, ListViewItem lvi, CertificateHash ch, string pass)
        {
            string status = "В процессе...";
            lvi.SubItems[3].Text = status;
            Application.DoEvents();
            
            string fn = lvi.SubItems[1].Text;

            if (sign)
            {
                PKCS7Signer s = null;
                try
                {
                    Logger.AddLine("");
                    Logger.AddLine($"  - Подписание", false);
                    Logger.AddLine($"  - Файл: {fn}", false);
                    FileInfo fi = new FileInfo(fn);
                    Logger.AddLine($"  - Размер: {GetFileSize(fn, out _)}, создан: {fi.CreationTime}, изменен: {fi.LastWriteTime}", false);
                    Logger.AddLine($"  - Сертификат: {GetPriorityText(ch)}", false);
                    if (ch.Certificate != null) s = new PKCS7Signer(ch.Certificate);
                    else s = new PKCS7Signer(ch.File, pass);
                    lvi.SubItems[3].Text = status = $"Подписание...";
                    Application.DoEvents();

                    // Attached
                    if (iniFile.signCreateMethod == 1 || iniFile.signCreateMethod == 3)
                        s.SignFile(fn, out _);
                    // Detached
                    if (iniFile.signCreateMethod == 2 || iniFile.signCreateMethod == 3 || iniFile.signCreateMethod == 4)
                    {
                        s.SignDetachedFile(fn, out _);
                        if (iniFile.signCreateMethod == 4)
                        {
                            File.Delete($"{fn}.p7s");
                            File.Move($"{fn}.detached.p7s", $"{fn}.p7s");
                        };
                    };

                    lvi.BackColor = Color.LightSkyBlue;
                    lvi.SubItems[3].Text = status = $"Подписан: {DateTime.Now}";
                    Logger.AddLine($"  - Подписан: {DateTime.Now}", false);
                    lvi.SubItems[4].Text = ch.Thumbprint;
                    lvi.SubItems[5].Text = GetPriorityText(ch);
                    Application.DoEvents();
                    Logger.AddLine("", false);
                    return true;
                }
                catch (Exception ex)
                {
                    lvi.BackColor = Color.LightPink;
                    lvi.SubItems[3].Text = status = $"Ошибка: {ex.Message.Replace("\r", " ").Replace("\n", " ")}";
                    Logger.AddLine($"  - Ошибка: {ex.Message}", false);
                    Application.DoEvents();
                    Logger.AddLine("", false);
                    return false;
                };                
            };

            if(check)
            {
                X509Certificate2 c = null;
                string submsg = "";
                try
                {
                    Logger.AddLine("");
                    Logger.AddLine($"  - Проверка", false);
                    Logger.AddLine($"  - Файл: {fn}", false);
                    FileInfo fi = new FileInfo(fn);
                    Logger.AddLine($"  - Размер: {GetFileSize(fn, out _)}, создан: {fi.CreationTime}, изменен: {fi.LastWriteTime}", false);

                    bool ok = false;                    
                    // Detached
                    if (!ok) try { submsg = " - подпись без содержимого документа"; ok = PKCS7Signer.CheckSignDetachedFile(fn, !iniFile.checkCertValid, out c); } catch { };
                    // Attached
                    if (!ok) try { submsg = " - подпись с полным содержимым документа"; ok = PKCS7Signer.CheckSignFile(fn, !iniFile.checkCertValid, out c); } catch (Exception ex) { throw ex; };                    

                    lvi.SubItems[3].Text = status = ok ? $"Подпись верна: {DateTime.Now}" : $"Неверная подпись: {DateTime.Now}";
                    Logger.AddLine(ok ? $"  - Подпись верна: {DateTime.Now}" : $"  - Неверная подпись: {DateTime.Now}", false);
                    if (ok) lvi.BackColor = Color.LightGreen; else lvi.BackColor = Color.LightPink;
                    if (c != null)
                    {
                        CertificateHash chc = new CertificateHash { Certificate = c };
                        lvi.SubItems[4].Text = chc.Thumbprint;
                        string cinfo = GetPriorityText(chc) + submsg;
                        lvi.SubItems[5].Text = cinfo;
                        Logger.AddLine($"  - Сертификат: {cinfo}", false);
                    }
                    else
                    {
                        lvi.SubItems[4].Text = "НЕИЗВЕСТЕН";
                        Logger.AddLine($"  - Сертификат неизвестен", false);
                    };
                    Logger.AddLine("", false);
                    Application.DoEvents();
                    return true;
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("Подпись верна"))
                    {
                        lvi.BackColor = Color.Pink;
                        lvi.SubItems[3].Text = status = $"{ex.Message} {DateTime.Now}";
                        Logger.AddLine($"  - Предупреждение: {ex.Message}", false);                        
                        if (c != null)
                        {
                            CertificateHash chc = new CertificateHash { Certificate = c };
                            lvi.SubItems[4].Text = chc.Thumbprint;
                            string cinfo = GetPriorityText(chc) + submsg;
                            lvi.SubItems[5].Text = cinfo;
                            Logger.AddLine($"  - {cinfo}", false);
                        }
                        else
                        {
                            lvi.SubItems[4].Text = "НЕИЗВЕСТЕН";
                            Logger.AddLine($"  - Сертификат: Сертификат неизвестен", false);
                        };
                        Logger.AddLine("", false);
                        Application.DoEvents();
                        return true;
                    }
                    else
                    {

                        lvi.BackColor = Color.OrangeRed;
                        lvi.SubItems[3].Text = status = $"Ошибка: {ex.Message.Replace("\r", " ").Replace("\n", " ")}";
                        Logger.AddLine($"  - Ошибка: {ex.Message}", false);
                        Logger.AddLine("", false);
                        Application.DoEvents();
                        return false;
                    };
                };
            };

            Application.DoEvents();
            return false;
        }

        private void ReloadTextLog()
        {
            logg.Text = Logger.GetText();
            if (logg.Visible)
            {
                logg.SelectionStart = logg.TextLength;
                logg.ScrollToCaret();
            };
        }

        private void ReloadCertificates(int src)
        {
            certList.Items.Clear();
            if ((src == 0) || (src == 1)) // System
            {
                dkxce.PKCS7Signer.Source source = src == 0 ? dkxce.PKCS7Signer.Source.CurrentUser : dkxce.PKCS7Signer.Source.LocalMachine;
                List<CertificateHash> list = dkxce.PKCS7Signer.GetCertificatesFrom(source);
                foreach (CertificateHash ch in list)
                    AddCert(ch);
            };
            if (src == 2) // Dropped
            {
                foreach (CertificateHash ch in iniFile.Dropped)
                    AddCert(ch);
            }; 
            if (src == 3) // Path
            {
                dkxce.PKCS7Signer.Source source = dkxce.PKCS7Signer.Source.Directory;
                List<CertificateHash> list = dkxce.PKCS7Signer.GetCertificatesFrom(source, XMLSaved<int>.CurrentDirectory(), iniFile.Hash, !firstLoad);
                foreach (CertificateHash ch in list)
                    AddCert(ch);
            };
            if(src == 4) // Fav
            {
                foreach (CertificateHash ch in iniFile.Favorites) 
                    AddCert(ch);
            };
            if (src == 5) // Last-Used
            {
                certList.Sorting = SortOrder.None;
                ColumnAddSortSymbol(certList, -1, true);
                foreach (CertificateHash ch in iniFile.History)
                    AddCert(ch);
            };
        }

        private void AddCert(CertificateHash ch)
        {
            ListViewDetailedItem lvi = new ListViewDetailedItem(ch.Owner) { CertificateHash = ch };
            CheckCertFavCurr(ch, lvi);
            lvi.SubItems.Add(ch.Publisher);
            lvi.SubItems.Add(ch.Subject);
            lvi.SubItems.Add(ch.Issuer);
            lvi.SubItems.Add(DateTime.Now < ch.Till ? "да" : "просрочен");
            lvi.SubItems.Add($"{ch.From}");
            lvi.SubItems.Add($"{ch.Till}");
            lvi.SubItems.Add(ch.Serial);
            lvi.SubItems.Add(ch.Thumbprint);
            certList.Items.Add(lvi);

            if((!string.IsNullOrEmpty(ch.File)) && (!string.IsNullOrEmpty(ch.MD5)))
            {
                string MD5 = ch.MD5;
                for (int i = iniFile.Hash.Count - 1; i >= 0; i--)
                    if (iniFile.Hash[i].MD5 == MD5)
                        iniFile.Hash.RemoveAt(i);
                iniFile.Hash.Add(ch);
            };
        }

        private void AddDropedCert(CertificateHash ch)
        {
            if ((!string.IsNullOrEmpty(ch.File)) && (!string.IsNullOrEmpty(ch.MD5)))
            {
                string MD5 = ch.MD5;
                for (int i = iniFile.Dropped.Count - 1; i >= 0; i--)
                    if (iniFile.Dropped[i].MD5 == MD5)
                        iniFile.Dropped.RemoveAt(i);
                iniFile.Dropped.Add(ch);
            };
        }

        private void AddHistoryCert(CertificateHash ch)
        {
            for (int i = iniFile.History.Count - 1; i >= 0; i--)
                if (iniFile.History[i].Thumbprint == ch.Thumbprint)
                    iniFile.History.RemoveAt(i);
            iniFile.History.Insert(0, ch);
        }

        private void CheckCertFavCurr(CertificateHash ch, ListViewDetailedItem lvi)
        {
            if (lvi == null || ch == null) return;
            lvi.Current = false;
            lvi.Favorite = false;
            foreach (CertificateHash f in iniFile.Favorites)
                if (f.Thumbprint == ch.Thumbprint)
                    lvi.Favorite = true;
            if (ch.Thumbprint == iniFile.currentCertificate)
                lvi.Current = true;
        }

        private void DetailizeCert(CertificateHash h)
        {
            List<string> colored1 = new List<string>(new string[] { "E" });
            List<string> colored2 = new List<string>(new string[] { "ЮЛ", "ИНН", "ИННЮЛ", "ОГРН", "ОГРНИП", "СНИЛС" });
            List<string> colored3 = new List<string>(new string[] { "НАЛОГОВАЯ" });

            Func<string, string, string, ListViewItem> AddItm = (t1, t2, t3) =>
            {                
                ListViewItem lvi = new ListViewItem(t1);
                string t3u = t3.ToUpper();
                if (colored1.Contains(t1) || colored1.Contains(t2)) lvi.BackColor = Color.LightSkyBlue;
                if (colored2.Contains(t1) || colored2.Contains(t2)) lvi.BackColor = Color.LightSalmon;                
                foreach (string c3 in colored3) if (t3u.Contains(c3)) lvi.BackColor = Color.RosyBrown;
                lvi.SubItems.Add(t2);
                lvi.SubItems.Add(t3);
                return cInfo.Items.Add(lvi);
            };

            AddItm("Владелец", "", h.Owner);
            AddItm("Издатель", "", h.Publisher);
            AddItm("Subject", "", h.Subject);
            AddItm("Issuer", "", h.Issuer);
            AddItm("Действителен", "", DateTime.Now < h.Till ? "да" : "просрочен");
            AddItm("Действителен от", "", $"{h.From}");
            AddItm("Действителен до", "", $"{h.Till}");
            AddItm("Serial", "", h.Serial);
            AddItm("Thumbprint", "", h.Thumbprint);
            foreach (KeyValuePair<string, string> kvp in h.SubjectDetailed)
                AddItm("Subject", kvp.Key, kvp.Value);
            foreach (KeyValuePair<string, string> kvp in h.IssuerDetailed)
                AddItm("Issuer", kvp.Key, kvp.Value);
            if (!string.IsNullOrEmpty(h.Source)) AddItm("Source", "", h.Source);
            if (!string.IsNullOrEmpty(h.MD5)) AddItm("MD5", "", h.MD5);
            if (!string.IsNullOrEmpty(h.File)) AddItm("File", "", Path.GetFileName(h.File));
        }

        private void AddCertToFav(ListViewDetailedItem lvi)
        {
            if (lvi.CertificateHash == null) return;
            dkxce.CertificateHash h = lvi.CertificateHash;
            RemoveCertFromFav(h);
            iniFile.Favorites.Add(h);
            lvi.Favorite = true;
        }

        private void RemoveCertFromFav(dkxce.CertificateHash h)
        {
            for (int i = iniFile.Favorites.Count - 1; i >= 0; i--)
                if (iniFile.Favorites[i].Thumbprint == h.Thumbprint)
                    iniFile.Favorites.RemoveAt(i);
        }

        private void RemoveCertFromDropped(dkxce.CertificateHash h)
        {
            for (int i = iniFile.Dropped.Count - 1; i >= 0; i--)
                if (iniFile.Dropped[i].Thumbprint == h.Thumbprint)
                    iniFile.Dropped.RemoveAt(i);
        }

        private void RemoveCertFromHistory(dkxce.CertificateHash h)
        {
            for (int i = iniFile.History.Count - 1; i >= 0; i--)
                if (iniFile.History[i].Thumbprint == h.Thumbprint)
                    iniFile.History.RemoveAt(i);
        }

        private void SetCurrentCert(string thumbprint)
        {
            if (string.IsNullOrEmpty(thumbprint))
            {
                SetTitle(null);
                return;
            };
            foreach (dkxce.CertificateHash ch in iniFile.History)
                if (ch.Thumbprint == thumbprint)
                { SelectCurrentCert(ch); return; };
            foreach (dkxce.CertificateHash ch in iniFile.Favorites)
                if (ch.Thumbprint == thumbprint)
                { SelectCurrentCert(ch); return;  };
            foreach (dkxce.CertificateHash ch in iniFile.Dropped)
                if (ch.Thumbprint == thumbprint)
                { SelectCurrentCert(ch); return; };
            foreach (dkxce.CertificateHash ch in iniFile.Hash)
                if (ch.Thumbprint == thumbprint)
                { SelectCurrentCert(ch); return; };
            // SYSTEM
            {
                X509Certificate2 c = dkxce.PKCS7Signer.GetCertificateFrom(PKCS7Signer.Source.UserOrMachine, X509FindType.FindByThumbprint, thumbprint);
                if (c != null) SelectCurrentCert(new CertificateHash { Certificate = c } ); 
            };
        }

        private dkxce.CertificateHash GetCurrentCert(string thumbprint)
        {
            if (string.IsNullOrEmpty(thumbprint)) return null;
            // First if all search in files
            // DROPPED
            foreach (dkxce.CertificateHash ch in iniFile.Dropped)
                if (ch.Thumbprint == thumbprint && ((!string.IsNullOrEmpty(ch.File)) || ch.Certificate != null))
                    return ch;
            // HASH
            foreach (dkxce.CertificateHash ch in iniFile.Hash)
                if (ch.Thumbprint == thumbprint && ((!string.IsNullOrEmpty(ch.File)) || ch.Certificate != null))
                    return ch;
            // Next in System
            // SYSTEM
            {
                X509Certificate2 c = dkxce.PKCS7Signer.GetCertificateFrom(PKCS7Signer.Source.UserOrMachine, X509FindType.FindByThumbprint, thumbprint);
                if (c != null) return new CertificateHash() { Certificate = c };
            };
            // Then History & Favs
            // HISTORY 
            foreach (dkxce.CertificateHash ch in iniFile.History)
                if (ch.Thumbprint == thumbprint && ((!string.IsNullOrEmpty(ch.File)) || ch.Certificate != null))
                    return ch;
            // FAVORITES
            foreach (dkxce.CertificateHash ch in iniFile.Favorites)
                if (ch.Thumbprint == thumbprint && ((!string.IsNullOrEmpty(ch.File)) || ch.Certificate != null))
                    return ch;

            return null;
        }

        private void SelectCurrentCert(dkxce.CertificateHash ch)
        {
            iniFile.currentCertificate = ch.Thumbprint;
            AddHistoryCert(ch);
            SetTitle(ch);
            {
                selSert.Items.Clear();
                ListViewDetailedItem lvi = new ListViewDetailedItem(ch.Owner) { CertificateHash = ch };
                CheckCertFavCurr(ch, lvi);
                lvi.SubItems.Add(ch.Publisher);
                lvi.SubItems.Add(ch.Subject);
                lvi.SubItems.Add(ch.Issuer);
                lvi.SubItems.Add(DateTime.Now < ch.Till ? "да" : "просрочен");
                lvi.SubItems.Add($"{ch.From}");
                lvi.SubItems.Add($"{ch.Till}");
                lvi.SubItems.Add(ch.Serial);
                lvi.SubItems.Add(ch.Thumbprint);
                selSert.Items.Add(lvi);                
            };            
            if (storageSelector.SelectedIndex == 5) ReloadCertificates(storageSelector.SelectedIndex);
        }

        private string GetPriorityText(dkxce.CertificateHash ch)
        {
            return $"{ch.Owner} [{ch.Thumbprint}] ({ch.PriorityText}) / {ch.Publisher}";
        }

        private void SetTitle(dkxce.CertificateHash ch)
        {
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            System.Diagnostics.FileVersionInfo fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
            string v = fvi.FileVersion;
            if (ch == null)
            {
                this.Text = $"{formCaption} - v{v}";
                ssn.Text = "";
            }
            else
            {
                this.Text = $"{formCaption} - {ch.Owner} [{ch.Thumbprint}] - v{v}";
                ssn.Text = GetPriorityText(ch);
            };
        }

        private void SetCandidate(dkxce.CertificateHash ch)
        {            
            ssc.Text = GetPriorityText(ch);
        }
    }

    public class CustomRenderer : ToolStripProfessionalRenderer
    {
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (e.Item is ToolStripStatusLabel)
                TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont,
                    e.TextRectangle, e.TextColor, Color.Transparent,
                    e.TextFormat | TextFormatFlags.EndEllipsis | TextFormatFlags.Left);
            else
                base.OnRenderItemText(e);
        }
    }

    public class ListViewDetailedItem: ListViewItem
    {
        private CertificateHash _certificateHash;
        private bool favorite = false;
        private bool current = false;

        public CertificateHash CertificateHash
        {
            get { return _certificateHash; }
            set
            {
                _certificateHash = value;
                ChangeColor();
            }
        }

        private void ChangeColor()
        {
            if (favorite && current) this.BackColor = Color.GreenYellow;
            else if (current) this.BackColor = Color.Yellow;            
            else if (favorite) this.BackColor = Color.LightGreen;
            else this.BackColor = SystemColors.Window;
            if (this.CertificateHash != null && CertificateHash.Till < DateTime.Now) this.BackColor = Color.Silver;
        }

        public bool Favorite
        {
            get { return favorite; }
            set
            {
                favorite = value;
                ChangeColor();                           
            }
        }

        public bool Current
        {
            get { return current; }
            set
            {
                current = value;
                ChangeColor();                
            }
        }

        public ListViewDetailedItem() : base() { }
        public ListViewDetailedItem(string text) : base(text) { }
    }
}