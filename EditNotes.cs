using System;
using System.Windows.Forms;

namespace Gw2DpsUploader
{
    public partial class EditNotes : Form
    {
        public string mNotes;

        public EditNotes()
        {
            InitializeComponent();
        }

        private void EditNotes_Load(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(this.mNotes))
                this.txtNotes.Text = this.mNotes;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            this.mNotes = this.txtNotes.Text;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void txtNotes_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                this.mNotes = this.txtNotes.Text;
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        }
    }
}
