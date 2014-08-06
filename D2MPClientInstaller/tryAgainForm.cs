using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace D2MPClientInstaller
{
    public partial class tryAgainForm : Form
    {
        public event EventHandler DownloadManuallyClick;

        public tryAgainForm(string pText)
        {
            InitializeComponent();
            
            lblText.Text = pText;
        }

        public void DisableDownload()
        {
            btnDownloadManually.Enabled = false;
        }

        private void btnTryAgain_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Retry;
        }

        private void btnDownloadManually_Click(object sender, EventArgs e)
        {
            if (DownloadManuallyClick != null) DownloadManuallyClick(sender, e);
            this.DialogResult = DialogResult.Ignore;
        }
    }
}
