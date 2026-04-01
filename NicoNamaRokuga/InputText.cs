using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NicoNamaRokuga
{
    public partial class InputText : Form
    {
        private string _input_text = "";
        public InputText()
        {
            InitializeComponent();
        }

        //OK
        private void button1_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
            _input_text = this.textBox1.Text;
            this.Close();
        }

        //CANCEL
        private void button2_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            _input_text = "";
            this.Close();
        }
        public string GetInputText()
        {
            return _input_text;
        }

    }
}
