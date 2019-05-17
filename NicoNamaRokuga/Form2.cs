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

using NicoNamaRokuga.Prop;
using NicoNamaRokuga.Net;

namespace NicoNamaRokuga
{
    public partial class Form2 : Form
    {

        private static Regex rbRegex = new Regex("^rB_(.+)$", RegexOptions.Compiled);

        private Form1 _form;  //親フォーム
        private Props _props;

        public Form2(Form1 fo)
        {
            InitializeComponent();

            _form = fo;
            _props = new Props();

        }

        //変数→フォーム
        private async Task SetFormAsync()
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

            textBox1.Text = _props.UserID;
            textBox2.Text = _props.Password;

            checkBox1.Checked = _props.IsAllCookie;
            comboBox1.SelectedIndex =
                await ShowCookiesAsync(!_props.IsAllCookie, _props.SelectedCookie);

            textBox3.Text = _props.SaveDir;
            textBox4.Text = _props.SaveFile;

            comboBox2.Items.Clear();
            foreach (var qu in Props.Quality.ToArray())
                comboBox2.Items.Add(qu);
            comboBox2.SelectedIndex = (int)_props.QuarityType;

            checkBox2.Checked = _props.IsLogging;
            checkBox3.Checked = _props.IsComment;

            return;
        }

        //フォーム→変数
        private async Task GetFormAsync()
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

            _props.UserID = textBox1.Text;
            _props.Password = textBox2.Text;

            _props.IsAllCookie = checkBox1.Checked;
            _props.SaveDir = textBox3.Text;
            _props.SaveFile = textBox4.Text;

            _props.QuarityType =
                (QTypes)Enum.ToObject(typeof(QTypes), comboBox2.SelectedIndex);

            _props.IsLogging = checkBox2.Checked;
            _props.IsComment = checkBox3.Checked;

            _props.SelectedCookie =
                await NicoLiveNet.GetCookieSource(!checkBox1.Checked, comboBox1.SelectedIndex);

            return;
        }

        private async Task<int> ShowCookiesAsync(bool flag, CookieSourceInfo csi)
        {
            var result = 0;
            var bn = string.Empty;
            if (csi != null) bn = csi.BrowserName;

            try
            {
                //使えるブラウザー一覧を取得
                comboBox1.Items.Clear();
                var bb = await NicoLiveNet.GetCookieBrowsers(flag);

                //combobox1 にブラウザ名を登録
                for (int i = 0; i < bb.Count(); i++)
                {
                    comboBox1.Items.Add(bb[i]);
                    if (bb[i] == bn) result = i;
                }
                comboBox1.Text = comboBox1.Items[0].ToString();
            }catch (Exception Ex)
            {
                MessageBox.Show("ShowCookiesAsync Error: \r\n"+Ex.Message);
                return result;
            }
            return result;
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            //OKボタンが押されたら設定を保存
            await GetFormAsync();
            var result = _props.SaveData(); //設定ファイルに保存
            result = Form1.props.LoadData(); //親フォームの設定データを更新
        }

        private async void button3_Click(object sender, EventArgs e)
        {
            //設定値を初期値に戻す
            var _props_save = new Props();
            var result = _props_save.LoadData(); //現在の設定ファイル内容を読み込み
            result = _props.ResetData(); //設定ファイルを初期化
            await SetFormAsync();
            result = _props_save.SaveData(); //キャンセルした時用に元の設定をファイルに書き込み
        }

        private async void button5_Click(object sender, EventArgs e)
        {
            //クッキー一覧を更新
            comboBox1.SelectedIndex =
                await ShowCookiesAsync(!checkBox1.Checked, _props.SelectedCookie);

        }

        private async void button4_Click(object sender, EventArgs e)
        {
            //ログインする
        }


        private async void Form2_Shown(object sender, EventArgs e)
        {
            //フォーム表示後データー読み込み＆表示
            var result = _props.LoadData();
            await SetFormAsync();

        }

        private async void checkBox1_Click(object sender, EventArgs e)
        {
            //クッキー一覧を更新
            comboBox1.SelectedIndex =
                await ShowCookiesAsync(!checkBox1.Checked, _props.SelectedCookie);


        }
    }
}
