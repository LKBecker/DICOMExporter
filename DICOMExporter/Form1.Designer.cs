namespace DICOMExporter
{
    partial class Form1
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
			this.buttonBrowseDICOMDirectory = new System.Windows.Forms.Button();
			this.buttonBrowseOutputDirectory = new System.Windows.Forms.Button();
			this.folderBrowserDialog1 = new System.Windows.Forms.FolderBrowserDialog();
			this.textBoxOutputDirectory = new System.Windows.Forms.TextBox();
			this.textBoxDICOMDirectory = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.buttonConvert = new System.Windows.Forms.Button();
			this.checkBoxExtractMetadata = new System.Windows.Forms.CheckBox();
			this.checkBoxCreateMetadataTable = new System.Windows.Forms.CheckBox();
			this.checkBoxCopyAndRename = new System.Windows.Forms.CheckBox();
			this.checkBoxCheckAndDeleteAfterCopy = new System.Windows.Forms.CheckBox();
			this.panelProgressBar = new System.Windows.Forms.Panel();
			this.labelProgressBar = new System.Windows.Forms.Label();
			this.progressBarDICOMFiles = new System.Windows.Forms.ProgressBar();
			this.panelProgressBar.SuspendLayout();
			this.SuspendLayout();
			// 
			// buttonBrowseDICOMDirectory
			// 
			this.buttonBrowseDICOMDirectory.Location = new System.Drawing.Point(505, 10);
			this.buttonBrowseDICOMDirectory.Name = "buttonBrowseDICOMDirectory";
			this.buttonBrowseDICOMDirectory.Size = new System.Drawing.Size(75, 23);
			this.buttonBrowseDICOMDirectory.TabIndex = 0;
			this.buttonBrowseDICOMDirectory.Text = "Browse...";
			this.buttonBrowseDICOMDirectory.UseVisualStyleBackColor = true;
			this.buttonBrowseDICOMDirectory.Click += new System.EventHandler(this.buttonBrowseDirectory_Click);
			// 
			// buttonBrowseOutputDirectory
			// 
			this.buttonBrowseOutputDirectory.Location = new System.Drawing.Point(505, 41);
			this.buttonBrowseOutputDirectory.Name = "buttonBrowseOutputDirectory";
			this.buttonBrowseOutputDirectory.Size = new System.Drawing.Size(75, 23);
			this.buttonBrowseOutputDirectory.TabIndex = 1;
			this.buttonBrowseOutputDirectory.Text = "Browse...";
			this.buttonBrowseOutputDirectory.UseVisualStyleBackColor = true;
			this.buttonBrowseOutputDirectory.Click += new System.EventHandler(this.buttonBrowseDirectory_Click);
			// 
			// textBoxOutputDirectory
			// 
			this.textBoxOutputDirectory.Location = new System.Drawing.Point(99, 38);
			this.textBoxOutputDirectory.Name = "textBoxOutputDirectory";
			this.textBoxOutputDirectory.Size = new System.Drawing.Size(400, 20);
			this.textBoxOutputDirectory.TabIndex = 2;
			// 
			// textBoxDICOMDirectory
			// 
			this.textBoxDICOMDirectory.Location = new System.Drawing.Point(99, 12);
			this.textBoxDICOMDirectory.Name = "textBoxDICOMDirectory";
			this.textBoxDICOMDirectory.Size = new System.Drawing.Size(400, 20);
			this.textBoxDICOMDirectory.TabIndex = 3;
			// 
			// label1
			// 
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(3, 15);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(90, 13);
			this.label1.TabIndex = 4;
			this.label1.Text = "DICOM Directory:";
			// 
			// label2
			// 
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(6, 41);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(87, 13);
			this.label2.TabIndex = 5;
			this.label2.Text = "Output Directory:";
			// 
			// buttonConvert
			// 
			this.buttonConvert.Location = new System.Drawing.Point(505, 73);
			this.buttonConvert.Name = "buttonConvert";
			this.buttonConvert.Size = new System.Drawing.Size(75, 23);
			this.buttonConvert.TabIndex = 6;
			this.buttonConvert.Text = "Convert";
			this.buttonConvert.UseVisualStyleBackColor = true;
			this.buttonConvert.Click += new System.EventHandler(this.buttonConvert_Click);
			// 
			// checkBoxExtractMetadata
			// 
			this.checkBoxExtractMetadata.AutoSize = true;
			this.checkBoxExtractMetadata.Location = new System.Drawing.Point(296, 87);
			this.checkBoxExtractMetadata.Name = "checkBoxExtractMetadata";
			this.checkBoxExtractMetadata.Size = new System.Drawing.Size(201, 17);
			this.checkBoxExtractMetadata.TabIndex = 7;
			this.checkBoxExtractMetadata.Text = "Export per-folder metadata files (slow)";
			this.checkBoxExtractMetadata.UseVisualStyleBackColor = true;
			// 
			// checkBoxCreateMetadataTable
			// 
			this.checkBoxCreateMetadataTable.AutoSize = true;
			this.checkBoxCreateMetadataTable.Location = new System.Drawing.Point(296, 64);
			this.checkBoxCreateMetadataTable.Name = "checkBoxCreateMetadataTable";
			this.checkBoxCreateMetadataTable.Size = new System.Drawing.Size(159, 17);
			this.checkBoxCreateMetadataTable.TabIndex = 8;
			this.checkBoxCreateMetadataTable.Text = "Export metadata table (slow)";
			this.checkBoxCreateMetadataTable.UseVisualStyleBackColor = true;
			// 
			// checkBoxCopyAndRename
			// 
			this.checkBoxCopyAndRename.AutoSize = true;
			this.checkBoxCopyAndRename.Location = new System.Drawing.Point(99, 64);
			this.checkBoxCopyAndRename.Name = "checkBoxCopyAndRename";
			this.checkBoxCopyAndRename.Size = new System.Drawing.Size(130, 17);
			this.checkBoxCopyAndRename.TabIndex = 10;
			this.checkBoxCopyAndRename.Text = "Copy and rename files";
			this.checkBoxCopyAndRename.UseVisualStyleBackColor = true;
			this.checkBoxCopyAndRename.CheckedChanged += new System.EventHandler(this.checkBoxCopyAndRename_CheckedChanged);
			// 
			// checkBoxCheckAndDeleteAfterCopy
			// 
			this.checkBoxCheckAndDeleteAfterCopy.AutoSize = true;
			this.checkBoxCheckAndDeleteAfterCopy.Enabled = false;
			this.checkBoxCheckAndDeleteAfterCopy.Location = new System.Drawing.Point(99, 87);
			this.checkBoxCheckAndDeleteAfterCopy.Name = "checkBoxCheckAndDeleteAfterCopy";
			this.checkBoxCheckAndDeleteAfterCopy.Size = new System.Drawing.Size(174, 17);
			this.checkBoxCheckAndDeleteAfterCopy.TabIndex = 9;
			this.checkBoxCheckAndDeleteAfterCopy.Text = "Compare then delete after copy";
			this.checkBoxCheckAndDeleteAfterCopy.UseVisualStyleBackColor = true;
			// 
			// panelProgressBar
			// 
			this.panelProgressBar.Controls.Add(this.labelProgressBar);
			this.panelProgressBar.Controls.Add(this.progressBarDICOMFiles);
			this.panelProgressBar.Dock = System.Windows.Forms.DockStyle.Fill;
			this.panelProgressBar.Location = new System.Drawing.Point(0, 0);
			this.panelProgressBar.Name = "panelProgressBar";
			this.panelProgressBar.Size = new System.Drawing.Size(587, 105);
			this.panelProgressBar.TabIndex = 11;
			// 
			// labelProgressBar
			// 
			this.labelProgressBar.AutoSize = true;
			this.labelProgressBar.Location = new System.Drawing.Point(193, 71);
			this.labelProgressBar.Name = "labelProgressBar";
			this.labelProgressBar.Size = new System.Drawing.Size(137, 13);
			this.labelProgressBar.TabIndex = 1;
			this.labelProgressBar.Text = "Processing file 0 of 0 (0%)...";
			this.labelProgressBar.Visible = false;
			// 
			// progressBarDICOMFiles
			// 
			this.progressBarDICOMFiles.Location = new System.Drawing.Point(140, 41);
			this.progressBarDICOMFiles.Name = "progressBarDICOMFiles";
			this.progressBarDICOMFiles.Size = new System.Drawing.Size(307, 23);
			this.progressBarDICOMFiles.TabIndex = 0;
			this.progressBarDICOMFiles.Visible = false;
			// 
			// Form1
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(587, 105);
			this.Controls.Add(this.checkBoxCopyAndRename);
			this.Controls.Add(this.checkBoxCheckAndDeleteAfterCopy);
			this.Controls.Add(this.checkBoxCreateMetadataTable);
			this.Controls.Add(this.checkBoxExtractMetadata);
			this.Controls.Add(this.buttonConvert);
			this.Controls.Add(this.label2);
			this.Controls.Add(this.label1);
			this.Controls.Add(this.textBoxDICOMDirectory);
			this.Controls.Add(this.textBoxOutputDirectory);
			this.Controls.Add(this.buttonBrowseOutputDirectory);
			this.Controls.Add(this.buttonBrowseDICOMDirectory);
			this.Controls.Add(this.panelProgressBar);
			this.HelpButton = true;
			this.Name = "Form1";
			this.Text = "DICOM Processor";
			this.HelpButtonClicked += new System.ComponentModel.CancelEventHandler(this.Form1_HelpButtonClicked);
			this.Load += new System.EventHandler(this.Form1_Load);
			this.panelProgressBar.ResumeLayout(false);
			this.panelProgressBar.PerformLayout();
			this.ResumeLayout(false);
			this.PerformLayout();

        }

        #endregion

		private System.Windows.Forms.Button buttonBrowseDICOMDirectory;
		private System.Windows.Forms.Button buttonBrowseOutputDirectory;
		private System.Windows.Forms.FolderBrowserDialog folderBrowserDialog1;
		private System.Windows.Forms.TextBox textBoxOutputDirectory;
		private System.Windows.Forms.TextBox textBoxDICOMDirectory;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.Button buttonConvert;
		private System.Windows.Forms.CheckBox checkBoxExtractMetadata;
		private System.Windows.Forms.CheckBox checkBoxCreateMetadataTable;
		private System.Windows.Forms.CheckBox checkBoxCopyAndRename;
		private System.Windows.Forms.CheckBox checkBoxCheckAndDeleteAfterCopy;
		private System.Windows.Forms.Panel panelProgressBar;
		private System.Windows.Forms.ProgressBar progressBarDICOMFiles;
		private System.Windows.Forms.Label labelProgressBar;
    }
}

