using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Net;
using System.Threading;
using System.Windows.Forms;

namespace Gw2DpsUploader {
    public partial class Form1 : Form {

        private Dictionary<string, ListViewItem> mListViewItems = new Dictionary<string, ListViewItem>();
        private Dictionary<ListViewItem, ArcItem> mItemData = new Dictionary<ListViewItem, ArcItem>();
        private ListViewItemComparer mListViewComprarer = new ListViewItemComparer();
        private string mKeyName = @"HKEY_CURRENT_USER\SOFTWARE\Gw2ArcUploader";
        private string mRegPath = @"SOFTWARE\Gw2ArcUploader";
        private string mArcLogFolder = @"Guild Wars 2\addons\arcdps\arcdps.cbtlogs";
        private int mSortColumn = 1;
        private string filter;
        private BackgroundWorker mBgWorker;
        private bool mFilterUpdated;

        public Form1 () {
            InitializeComponent();
            listView.Columns.Add("Boss", 135);
            listView.Columns.Add("Date", 100);
            listView.Columns.Add("Filesize", 70);
            listView.Columns.Add("Status", 300);
            mListViewComprarer.Init(mItemData);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            mListViewComprarer.ApplySort(1, SortOrder.Descending);
            listView.ListViewItemSorter = mListViewComprarer;
        }

        bool firstTime = false;

        private void Form1_Activated(object sender, EventArgs e) {
           // if(!firstTime)
           // {
           //     firstTime = true;
           //     return;
           // }

            RefreshList();
        }

        private void RefreshList() { 
            string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), mArcLogFolder);
            if (!Directory.Exists(logPath))
                return;

            string [] files = Directory.GetFiles(logPath, "*.*", SearchOption.AllDirectories);

            foreach (var filePath in files) {
                if (mListViewItems.ContainsKey(filePath))
                    continue;

                var fileInfo = new FileInfo(filePath);
                string bossName = GetBossName(fileInfo.Directory);
                string bossNameLwr = bossName.ToLower();
                bool included = string.IsNullOrEmpty(this.filter) || bossNameLwr.Contains(this.filter);

                if (!included)
                    continue;
                
                var data = new ArcItem();
                data.mTimeStamp = fileInfo.LastWriteTime;
                data.mFileSize = (int)fileInfo.Length / 1024;
                data.mFilePath = filePath;
                data.mBossName = bossNameLwr;

                string url = (string)Registry.GetValue(mKeyName, filePath, "");

                var item = new ListViewItem(bossName);
                item.SubItems.Add(data.mTimeStamp.ToString("M/d/yy h:mm tt"));
                item.SubItems.Add(data.mFileSize.ToString() + " kb");
                item.SubItems.Add(string.IsNullOrEmpty(url) ? "Not Uploaded" : url);

                mListViewItems[filePath] = item;
                mItemData[item] = data;
                listView.Items.Add(item);
            }

            foreach(ListViewItem item in listView.Items) {
                if (!File.Exists(mItemData[item].mFilePath)) {
                    RemoveItem(item);
                }
                else
                {
                    bool included = string.IsNullOrEmpty(this.filter) || mItemData[item].mBossName.Contains(this.filter);
                    if (!included)
                    {
                        RemoveItem(item);
                    }
                }
            }

            RegistryKey regKey = Registry.CurrentUser.OpenSubKey(mRegPath, true);
			if(regKey != null) {
				foreach(string regItem in regKey.GetValueNames()) {
					if(!mListViewItems.ContainsKey(regItem)) {
						regKey.DeleteValue(regItem);
					}
				}
			}

            RefreshSelectionCount();
        }

		private string GetBossName (DirectoryInfo dir)
		{
			while(dir != null)
			{
				if(dir.Parent != null && dir.Parent.Name == "arcdps.cbtlogs")
					return dir.Name;

				dir = dir.Parent;
			}

			return "Unsure...";
		}

        private void btnDelete_Click (object sender, EventArgs e) {
            if (listView.SelectedItems.Count == 0)
                return;

            var result = MessageBox.Show("Are you sure you want to delete the selected log files?", "Delete Files?", MessageBoxButtons.OKCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1);
            if (result != DialogResult.OK)
                return;

            foreach (ListViewItem item in listView.SelectedItems) {
                string filePath = mItemData[item].mFilePath;
                if (File.Exists(filePath)) {
                    File.Delete(filePath);
                }

                RemoveItem(item);
            }
        }

        private void RemoveItem (ListViewItem item) {
            string filePath = mItemData[item].mFilePath;
            listView.Items.Remove(item);
            mListViewItems.Remove(filePath);
            mItemData.Remove(item);
        }

        private void btnUpload_Click (object sender, EventArgs e) {
            var list = new List<ListViewItem>();

            foreach (ListViewItem item in listView.SelectedItems) {
                var url = item.SubItems[3];

                if (url.Text == "Not Uploaded") {
                    url.Text = "Uploading...";
                    list.Add(item);
                }
            }

            if(list.Count > 0) {
                var worker = new BackgroundWorker();
                worker.DoWork += backgroundWorker_DoWork;
                worker.RunWorkerAsync(list);
            }
        }

        private void backgroundWorker_DoWork (object sender, DoWorkEventArgs e) {
            var list = (List<ListViewItem>)e.Argument;

            foreach (var item in list) {
                string filePath = mItemData[item].mFilePath;
                if (!File.Exists(filePath))
                    continue;

                using (WebClient client = new WebClient()) {
                    var bytes = client.UploadFile("https://dps.report/uploadContent?json=1", filePath);
                    var result = System.Text.Encoding.Default.GetString(bytes);

                    Invoke((MethodInvoker)(() => UpdateDpsUrl(item, result)));
                }
            }

            Invoke((MethodInvoker)(() => btnCopyURL_Click(sender, e)));
            SystemSounds.Beep.Play();
        }

        private void UpdateDpsUrl(ListViewItem item, string json) {
            var jsonObj = JsonConvert.DeserializeObject<DpsReportResult>(json);

            var url = item.SubItems[3];
            url.Text = jsonObj.permalink;
            
            Registry.SetValue(mKeyName, mItemData[item].mFilePath, jsonObj.permalink);
        }

        private void btnView_Click (object sender, EventArgs e) {
			var items = listView.SelectedItems;
			for(int ii = items.Count - 1; ii >= 0; --ii) {
				var item = items[ii];
                var url = item.SubItems[3];

                if (url.Text == "Not Uploaded" || url.Text == "Uploading...") {
                    continue;
                }

                Process.Start(url.Text);
            }
        }

        private void btnCopyURL_Click (object sender, EventArgs e) {
            string text = string.Empty;
			var items = listView.SelectedItems;
			for(int ii = items.Count - 1; ii >= 0; --ii)
			{
				var item = items[ii];
				if(text != string.Empty)
                    text += "\n";

                var url = item.SubItems[3];
                text += url.Text;
            }

            if (!string.IsNullOrEmpty(text))
                Clipboard.SetText(text);
        }

        private void listView_ColumnClick (object sender, ColumnClickEventArgs e) {

            if (e.Column != mSortColumn) {
                mSortColumn = e.Column;
                listView.Sorting = (e.Column == 1 || e.Column == 2 ) ? SortOrder.Descending : SortOrder.Ascending;
            }
            else {
                listView.Sorting = listView.Sorting == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
            }

            mListViewComprarer.ApplySort(e.Column, listView.Sorting);
            listView.Sort();
        }
        
        private void listView_KeyDown (object sender, KeyEventArgs e) {
            if (e.KeyCode == Keys.C && e.Modifiers.HasFlag(Keys.Control)) {
                btnCopyURL_Click(sender, new EventArgs());
            }
            if (e.KeyCode == Keys.A && e.Modifiers.HasFlag(Keys.Control)) {
                foreach (ListViewItem item in this.listView.Items) {
                    item.Selected = true;
                }
            }
            else if(e.KeyCode == Keys.Delete) {
                btnDelete_Click(sender, new EventArgs());
            }
            else if (e.KeyCode == Keys.Enter) {
                btnUpload_Click(sender, new EventArgs());
            }
        }

        private void btnStats_Click (object sender, EventArgs e) {
			Cursor.Current = Cursors.WaitCursor;

			string err = "";

            var raidStart = DateTime.MaxValue;
            var raidEnd = DateTime.MinValue;
            var fightDuration = TimeSpan.Zero;
            int numRaids = 0;
			int numWins = 0;

            foreach (ListViewItem item in listView.SelectedItems) {
                string filePath = mItemData[item].mFilePath;

                var parser = new EVTC_Log_Parser.Model.Parser();
                if(!parser.Parse(filePath)) {
                    err += "Failed to parse: " + filePath + "\r\n";
                    continue;
                }

                TimeSpan duration = parser.Metadata.LogEnd - parser.Metadata.LogStart;
                if (parser.Metadata.LogStart < raidStart)
                    raidStart = parser.Metadata.LogStart;
                if (parser.Metadata.LogEnd > raidEnd)
                    raidEnd = parser.Metadata.LogEnd;
                numRaids++;
				
				numWins += parser.NPCs[0].Died ? 1 : 0;
				fightDuration += duration;
            }

            var totalDuration = raidEnd - raidStart;
            var idleDuration = totalDuration - fightDuration;

			var strTotalDuration = FormatTimeSpan(totalDuration);
			var strFightDuration = FormatTimeSpan(fightDuration);
			var strIdleDuration = FormatTimeSpan(idleDuration);
			int startMinute = raidStart.Minute;
			int numFails = numRaids - numWins;

			if(!string.IsNullOrEmpty(err))
				err += "\r\n";

			string msg = err;
			msg += string.Format("Raids started {0} minutes after the hour and lasted for {1}.\r\n", startMinute, strTotalDuration);
			msg += string.Format("During the {0} fights, {1} were in combat and {2} idle.\r\n", numRaids, strFightDuration, strIdleDuration);
			msg += string.Format("{0} bosses were killed with {1} wipe{2}.", numWins, numFails, numFails == 1 ? "" : "s");
			Clipboard.SetText(msg);
			Cursor.Current = Cursors.Default;
			MessageBox.Show(msg, "Raid Summary", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

		private string FormatTimeSpan(TimeSpan t)
		{
			string str = string.Empty;
			int h = (int)t.TotalHours;
			if (h > 0)
				str += h + " hour";
			if (h > 1)
				str += "s";

			if(t.Minutes > 0) {
				if(str != string.Empty)
					str += " ";

				str += t.Minutes + " minute";
			}
			if(t.Minutes > 1)
				str += "s";

			return str;
		}

        private void txtSearch_TextChanged(object sender, EventArgs e)
        {
            if(this.mBgWorker != null)
            {
                this.mFilterUpdated = true;
            }
            else
            {
                this.mBgWorker = new BackgroundWorker();
                this.mBgWorker.DoWork += this.TextChanged_DoWork;
                this.mBgWorker.RunWorkerAsync();
            }
        }

        private void TextChanged_DoWork(object sender, DoWorkEventArgs e)
        {
            while(true)
            {
                Thread.Sleep(500);

                if(!this.mFilterUpdated)
                    break;

                this.mFilterUpdated = false;
            }

            this.mBgWorker = null;
            this.mFilterUpdated = false;
            this.filter = this.txtSearch.Text.ToLower();

            this.Invoke((MethodInvoker)delegate {
                this.RefreshList();
            });
        }

        private void listView_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            this.RefreshSelectionCount();
        }
        
        private void RefreshSelectionCount()
        {
            this.lblCount.Text = string.Format("{0}/{1}", this.listView.SelectedItems.Count, this.listView.Items.Count);
        }
    }
}
