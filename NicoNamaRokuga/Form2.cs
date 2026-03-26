using System;
using System.Net;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SunokoLibrary.Application;
using SunokoLibrary.Windows.ViewModels;

using NicoNamaRokuga.Prop;
using NicoNamaRokuga.Net;

namespace NicoNamaRokuga
{
    public partial class Form2 : Form
    {

        private static Regex rbRegex = new Regex("^rB_(.+)$", RegexOptions.Compiled);

        private Form1 _form;  //親フォーム
        private Props _props;
        private string _accountdbfile;
        private string _user = null;
        private string _pass = null;

        public Form2(Form1 fo, string accountdbfile)
        {
            InitializeComponent();
            _form = fo;
            _accountdbfile = accountdbfile;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            _props = new Props();
            var result = _props.LoadData(_accountdbfile);
            _user = _props.UserID;
            _pass = _props.Password;
            SetForm();
        }

        //変数→フォーム
        private void SetForm()
        {
            try
            {
                foreach (Control co in groupBox1.Controls)
                {
                    if (co.GetType().Name == "RadioButton")
                    {
                        if (rbRegex.Replace(co.Name.ToString(), "$1") == _props.IsLogin.ToString())
                            ((RadioButton)co).Checked = true;
                    }
                }
                foreach (Control co in groupBox2.Controls)
                {
                    if (co.GetType().Name == "RadioButton")
                    {
                        if (rbRegex.Replace(co.Name.ToString(), "$1") == _props.LoginMethod.ToString())
                            ((RadioButton)co).Checked = true;
                    }
                }
                foreach (Control co in groupBox5.Controls)
                {
                    if (co.GetType().Name == "RadioButton")
                    {
                        if (rbRegex.Replace(co.Name.ToString(), "$1") == _props.Protocol.ToString())
                            ((RadioButton)co).Checked = true;
                    }
                }
                foreach (Control co in groupBox6.Controls)
                {
                    if (co.GetType().Name == "RadioButton")
                    {
                        if (rbRegex.Replace(co.Name.ToString(), "$1") == _props.UseExternal.ToString())
                            ((RadioButton)co).Checked = true;
                    }
                }

                textBox1.Text = _props.UserID;
                textBox2.Text = _props.Password;

                checkBox1.Checked = _props.IsAllCookie;
                nicoSessionComboBox1.Selector.IsAllBrowserMode = checkBox1.Checked;
                var tsk = nicoSessionComboBox1.Selector.SetInfoAsync(_props.SelectedCookie);
                checkBox8.Checked = _props.IsCookieFile;
                textBox6.Text = _props.CookieFile;
                textBox7.Text = _props.UserSession;

                textBox3.Text = _props.SaveDir;
                textBox4.Text = _props.SaveFile;
                textBox5.Text = _props.SaveFolder;

                comboBox2.Items.Clear();
                foreach (var qu in Props.Quality.ToArray())
                    comboBox2.Items.Add(qu);
                comboBox2.SelectedIndex = Props.ParseQTypes(_props.QuarityType, Props.Quality);

                comboBox1.Items.Clear();
                foreach (var qu in Props.Quality2.ToArray())
                    comboBox1.Items.Add(qu);
                comboBox1.SelectedIndex = Props.ParseQTypes(_props.QuarityType2, Props.Quality2);

                checkBox2.Checked = _props.IsLogging;
                checkBox3.Checked = _props.IsComment;
                checkBox6.Checked = _props.IsSeetNo;
                checkBox7.Checked = _props.IsVideo;
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(SetForm), Ex);
                return;
            }

            return;
        }

        //フォーム→変数
        private void GetForm()
        {
            try
            {
                foreach (Control co in groupBox1.Controls)
                {
                    if (co.GetType().Name == "RadioButton")
                    {
                        if ((bool)((RadioButton)co).Checked)
                            _props.IsLogin =
                                (IsLogin)Enum.Parse(typeof(IsLogin), rbRegex.Replace(co.Name.ToString(), "$1"));
                    }
                }
                foreach (Control co in groupBox2.Controls)
                {
                    if (co.GetType().Name == "RadioButton")
                    {
                        if ((bool)((RadioButton)co).Checked)
                            _props.LoginMethod =
                                (LoginMethod)Enum.Parse(typeof(LoginMethod), rbRegex.Replace(co.Name.ToString(), "$1"));
                    }
                }
                foreach (Control co in this.groupBox5.Controls)
                {
                    if (co.GetType().Name == "RadioButton")
                    {
                        if ((bool)((RadioButton)co).Checked)
                            _props.Protocol =
                                (Protocol)Enum.Parse(typeof(Protocol), rbRegex.Replace(co.Name.ToString(), "$1"));
                    }
                }
                foreach (Control co in this.groupBox6.Controls)
                {
                    if (co.GetType().Name == "RadioButton")
                    {
                        if ((bool)((RadioButton)co).Checked)
                            _props.UseExternal =
                                (UseExternal)Enum.Parse(typeof(UseExternal), rbRegex.Replace(co.Name.ToString(), "$1"));
                    }
                }

                _props.UserID = textBox1.Text;
                _props.Password = textBox2.Text;

                _props.IsAllCookie = checkBox1.Checked;
                _props.IsCookieFile = checkBox8.Checked;
                _props.CookieFile = textBox6.Text;
                _props.UserSession = textBox7.Text;

                _props.SaveDir = textBox3.Text;
                _props.SaveFile = textBox4.Text;
                _props.SaveFolder = textBox5.Text;

                _props.QuarityType =
                    Props.EnumQTypes(comboBox2.SelectedIndex, Props.Quality);

                _props.QuarityType2 =
                    Props.EnumQTypes(comboBox1.SelectedIndex, Props.Quality2);

                _props.IsLogging = checkBox2.Checked;
                _props.IsComment = checkBox3.Checked;
                _props.IsSeetNo = checkBox6.Checked;
                _props.IsVideo = checkBox7.Checked;

                _props.SelectedCookie = nicoSessionComboBox1.Selector.SelectedImporter.SourceInfo;

            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(GetForm), Ex);
                return;
            }

            return;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            //OKボタンが押されたら設定を保存
            GetForm();
            var acc_flg = (_user != _props.UserID || _pass != _props.Password) ? true : false;
            var result = _props.SaveData(_accountdbfile, acc_flg); //設定ファイルに保存
            result = Form1.props.LoadData(_accountdbfile); //親フォームの設定データを更新
        }

        private void button3_Click(object sender, EventArgs e)
        {
            //設定値を初期値に戻す
            var _props_save = new Props();
            var result = _props_save.LoadData(_accountdbfile); //現在の設定ファイル内容を読み込み
            result = _props.ResetData(_accountdbfile); //設定ファイルを初期化
            SetForm();
            result = _props_save.SaveData(_accountdbfile, false); //キャンセルした時用に元の設定をファイルに書き込み
        }

        private void button5_Click(object sender, EventArgs e)
        {
            //クッキー一覧を更新
            var tsk = nicoSessionComboBox1.Selector.UpdateAsync();

        }

        private void button4_Click(object sender, EventArgs e)
        {
            //ログインする
        }

        private void checkBox1_Click(object sender, EventArgs e)
        {
            //クッキー一覧を更新
            nicoSessionComboBox1.Selector.IsAllBrowserMode = checkBox1.Checked;
            var tsk = nicoSessionComboBox1.Selector.UpdateAsync();

        }

        private void button6_Click(object sender, EventArgs e)
        {
            //録画ファイル保存先フォルダー
            try
            {
                using (var folderBrowserDialog1 = new System.Windows.Forms.FolderBrowserDialog())
                {
                    folderBrowserDialog1.Description = "フォルダを指定してください。";
                    folderBrowserDialog1.RootFolder = Environment.SpecialFolder.Desktop;
                    if (String.IsNullOrEmpty(textBox3.Text)) //空白の場合はマイドキュメント指定
                    {
                        folderBrowserDialog1.SelectedPath = System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    }
                    else
                    {
                        folderBrowserDialog1.SelectedPath = textBox3.Text;
                    }
                    folderBrowserDialog1.ShowNewFolderButton = true;

                    //ダイアログを表示する
                    if (folderBrowserDialog1.ShowDialog(this) == DialogResult.OK)
                    {
                        //選択されたフォルダを表示する
                        textBox3.Text = folderBrowserDialog1.SelectedPath;
                    }
                }
            }
            catch (Exception Ex)
            {
                MessageBox.Show(Ex.Message, "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button7_Click(object sender, EventArgs e)
        {
            //ファイル名書式

        }

        private void button8_Click(object sender, EventArgs e)
        {
            //振り分けフォルダー
        }

        private void checkBox8_Click(object sender, EventArgs e)
        {
            if (checkBox8.Checked)
            {
                textBox6.Enabled = true;
                //textBox1.Text = null;
                button9.Enabled = true;
            }
            else
            {
                textBox6.Enabled = false;
                textBox1.Text = null;
                button9.Enabled = false;
            }
        }

        private async void button9_Click(object sender, EventArgs e)
        {
            //Cookieファイル直接指定
            await nicoSessionComboBox1.ShowCookieDialogAsync();
            var currentGetter = nicoSessionComboBox1.Selector.SelectedImporter;
            if (currentGetter != null)
            {
                textBox6.Text = currentGetter.SourceInfo.CookiePath;
            }
        }

        //指定されたcookieファイルを取得する
        public async Task GetCookieFileAsync(string cookiefile)
        {
            var currentImporter = nicoSessionComboBox1.Selector.SelectedImporter;
            var currentCookiePath = currentImporter.SourceInfo.CookiePath;
            CookieSourceInfo newInfo = null;
            if (!string.IsNullOrEmpty(cookiefile) &&
                System.IO.File.Exists(cookiefile))
            {
                currentCookiePath = cookiefile;
                newInfo = currentImporter.SourceInfo.GenerateCopy(cookiePath: currentCookiePath);
            }
            await nicoSessionComboBox1.Selector.SetInfoAsync(newInfo);
        }

        //Cookieファイル直接指定(TextBox)
        private async void textBox6_KeyDown(object sender, KeyEventArgs e)
        {
            if (!string.IsNullOrEmpty(textBox6.Text))
                await GetCookieFileAsync(textBox6.Text);
        }

    }
}
