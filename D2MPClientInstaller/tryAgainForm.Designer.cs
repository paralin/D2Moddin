namespace D2MPClientInstaller
{
    partial class tryAgainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(tryAgainForm));
            this.lblText = new System.Windows.Forms.Label();
            this.btnTryAgain = new System.Windows.Forms.Button();
            this.btnDownloadManually = new System.Windows.Forms.Button();
            this.btnExit = new System.Windows.Forms.Button();
            this.pbIcon = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.pbIcon)).BeginInit();
            this.SuspendLayout();
            // 
            // lblText
            // 
            this.lblText.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblText.Location = new System.Drawing.Point(82, 13);
            this.lblText.Name = "lblText";
            this.lblText.Size = new System.Drawing.Size(289, 80);
            this.lblText.TabIndex = 0;
            this.lblText.Text = "lblText";
            this.lblText.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // btnTryAgain
            // 
            this.btnTryAgain.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnTryAgain.Location = new System.Drawing.Point(83, 106);
            this.btnTryAgain.Name = "btnTryAgain";
            this.btnTryAgain.Size = new System.Drawing.Size(75, 23);
            this.btnTryAgain.TabIndex = 1;
            this.btnTryAgain.Text = "Try Again";
            this.btnTryAgain.UseVisualStyleBackColor = true;
            this.btnTryAgain.Click += new System.EventHandler(this.btnTryAgain_Click);
            // 
            // btnDownloadManually
            // 
            this.btnDownloadManually.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnDownloadManually.Location = new System.Drawing.Point(164, 106);
            this.btnDownloadManually.Name = "btnDownloadManually";
            this.btnDownloadManually.Size = new System.Drawing.Size(126, 23);
            this.btnDownloadManually.TabIndex = 2;
            this.btnDownloadManually.Text = "Download Manually";
            this.btnDownloadManually.UseVisualStyleBackColor = true;
            this.btnDownloadManually.Click += new System.EventHandler(this.btnDownloadManually_Click);
            // 
            // btnExit
            // 
            this.btnExit.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnExit.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnExit.Location = new System.Drawing.Point(296, 106);
            this.btnExit.Name = "btnExit";
            this.btnExit.Size = new System.Drawing.Size(75, 23);
            this.btnExit.TabIndex = 3;
            this.btnExit.Text = "Exit";
            this.btnExit.UseVisualStyleBackColor = true;
            // 
            // pbIcon
            // 
            this.pbIcon.Image = ((System.Drawing.Image)(resources.GetObject("pbIcon.Image")));
            this.pbIcon.Location = new System.Drawing.Point(12, 21);
            this.pbIcon.Name = "pbIcon";
            this.pbIcon.Size = new System.Drawing.Size(64, 64);
            this.pbIcon.TabIndex = 4;
            this.pbIcon.TabStop = false;
            // 
            // tryAgainForm
            // 
            this.AcceptButton = this.btnTryAgain;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnExit;
            this.ClientSize = new System.Drawing.Size(383, 141);
            this.Controls.Add(this.pbIcon);
            this.Controls.Add(this.btnExit);
            this.Controls.Add(this.btnDownloadManually);
            this.Controls.Add(this.btnTryAgain);
            this.Controls.Add(this.lblText);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(327, 157);
            this.Name = "tryAgainForm";
            this.Text = "D2Moddin Installer";
            ((System.ComponentModel.ISupportInitialize)(this.pbIcon)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label lblText;
        private System.Windows.Forms.Button btnTryAgain;
        private System.Windows.Forms.Button btnDownloadManually;
        private System.Windows.Forms.Button btnExit;
        private System.Windows.Forms.PictureBox pbIcon;
    }
}