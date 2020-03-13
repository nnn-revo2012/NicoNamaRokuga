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
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            _props = new Props();
            var result = _props.LoadData();
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

                textBox3.Text = _props.SaveDir;
                textBox4.Text = _props.SaveFile;
                textBox5.Text = _props.SaveFolder;

                comboBox2.Items.Clear();
                foreach (var qu in Props.Quality.ToArray())
                    comboBox2.Items.Add(qu);
                comboBox2.SelectedIndex = (int)_props.QuarityType;

                checkBox2.Checked = _props.IsLogging;
                checkBox3.Checked = _props.IsComment;
                checkBox6.Checked = _props.IsSeetNo;
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
                _props.SaveDir = textBox3.Text;
                _props.SaveFile = textBox4.Text;
                _props.SaveFolder = textBox5.Text;

                _props.QuarityType =
                    (QTypes)Enum.ToObject(typeof(QTypes), comboBox2.SelectedIndex);

                _props.IsLogging = checkBox2.Checked;
                _props.IsComment = checkBox3.Checked;
                _props.IsSeetNo = checkBox6.Checked;

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
            var result = _props.SaveData(); //設定ファイルに保存
            result = Form1.props.LoadData(); //親フォームの設定データを更新
        }

        private void button3_Click(object sender, EventArgs e)
        {
            //設定値を初期値に戻す
            var _props_save = new Props();
            var result = _props_save.LoadData(); //現在の設定ファイル内容を読み込み
            result = _props.ResetData(); //設定ファイルを初期化
            SetForm();
            result = _props_save.SaveData(); //キャンセルした時用に元の設定をファイルに書き込み
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

        private void button7_Click(object sender, EventArgs e)
        {

        }

        private void button6_Click(object sender, EventArgs e)
        {

        }
    }
}
