using System;
using System.Windows.Forms;
using System.IO;

using NicoNamaRokuga.Prop;
using NicoNamaRokuga.Net;
using NicoNamaRokuga.Proc;


namespace NicoNamaRokuga
{
    public partial class Form1 : Form
    {
        //ログウインドウ初期化
        private void ClearLog()
        {
            this.Invoke(new Action(() =>
            {
                lock (lockObject)
                {
                    listBox1.Items.Clear();
                    listBox1.TopIndex = 0;
                }
            }));
        }

        //ログウインドウ書き込み
        public void AddLog(string s, int num)

        {
            this.Invoke(new Action(() =>
            {
                lock (lockObject)
                {
                    if (num == 1)
                    {
                        if (listBox1.Items.Count > 50)
                        {
                            listBox1.Items.RemoveAt(0);
                            listBox1.TopIndex = listBox1.Items.Count - 1;
                        }
                        listBox1.Items.Add(s);
                        listBox1.TopIndex = listBox1.Items.Count - 1;
                    }
                    else if (num == 2) //エラー
                    {
                        MessageBox.Show(s + "\r\n",
                            "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else if (num == 3) //注意
                    {
                        MessageBox.Show(s + "\r\n",
                            "", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    if (props.IsLogging)
                        System.IO.File.AppendAllText(LogFile, System.DateTime.Now.ToString("HH:mm:ss ") + s + "\r\n");
                }
            }));
        }

        //実行プロセスのログ書き込み
        public void AddExecLog(string s)
        {
            this.Invoke(new Action(() =>
            {
                lock (lockObject2)
                {
                    textBox7.Text = s;
                    if (props.IsLogging)
                        System.IO.File.AppendAllText(LogFile2, System.DateTime.Now.ToString("HH:mm:ss ") + s);
                }
            }));
        }

        public void ClearHosoData()
        {
            this.Invoke(new Action(() =>
            {
                label2.Text = "";
                label3.Text = "";
                label4.Text = "";
                label5.Text = "";
                label6.Text = "";
                label7.Text = "";
                label8.Text = "";
                label9.Text = "";
            }));
        }

        //放送情報を表示
        public void DispHosoData(BroadCastInfo bci)
        {
            this.Invoke(new Action(() =>
            {
                label2.Text = bci.Title;
                label3.Text = Props.GetProviderType(bci.Provider_Type);
                label4.Text = bci.Community_Title + "(" + bci.Community_Id + ")";
                label5.Text = bci.Provider_Name + "(" + bci.Provider_Id + ")";
                label6.Text = Props.GetUnixToDateTime(bci.Begin_Time).ToString() + " 開始";
                label8.Text = "生放送";
                if (bci.IsTimeShift())
                {
                    label7.Text = Props.GetUnixToDateTime(bci.End_Time).ToString() + " 終了";
                    label8.Text = "タイムシフト";
                }

            }));
        }

        //画質情報を表示
        public void DispQuality(string s)
        {
            this.Invoke(new Action(() =>
            {
                label9.Text = Props.Quality[Props.ParseQTypes(s)];
            }));
        }

        public void EnableButton(bool flag)
        {
            //true 中断→録画開始
            this.Invoke(new Action(() =>
            {
                if (flag)
                {
                    this.textBox1.Enabled = true;
                    //this.button2.Enabled = true;
                    this.button1.Text = "録画開始";
                    this.button1.Focus();
                }
                else
                {
                    this.textBox1.Enabled = false;
                    //this.button2.Enabled = false;
                    this.button1.Text = "中断";
                    this.button1.Focus();
                }
            }));
        }

        //実行ファイルと同じフォルダにある指定ファイルのフルパスをGet
        private string GetExecFile(string file)
        {
            var fullAssemblyName = this.GetType().Assembly.Location;
            if (Path.GetFileName(file) == file)
                return Path.Combine(Path.GetDirectoryName(fullAssemblyName), file);
            return file;
        }

    }
}
