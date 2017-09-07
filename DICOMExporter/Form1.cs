using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace DICOMExporter {
    public partial class Form1 : Form {
		static int lostTagCount = 0;
		Stack<DicomFile> DICOMFileStack = new Stack<DicomFile>();
		char[] InvalidFileChars = Path.GetInvalidFileNameChars();
		char[] InvalidPathChars = Path.GetInvalidPathChars();
		const int BYTES_TO_READ = sizeof(Int64);

        public Form1(){ 
			InitializeComponent();
		}

        private void Form1_Load(object sender, EventArgs e){ 
			this.Text = "DICOM Processor v" + Application.ProductVersion; 
#if DEBUG
			this.textBoxDICOMDirectory.Text = @"F:\Other People's Data\2017-03_SecondEchoPurge";
			this.textBoxOutputDirectory.Text = @"\\nask.man.ac.uk\home$\Desktop\TestFolder";
#endif
		}

		private void buttonBrowseDirectory_Click(object sender, EventArgs e) {
			DialogResult result = folderBrowserDialog1.ShowDialog();
			TextBox target = ((Button)sender).Name.Contains("DICOM") ? this.textBoxDICOMDirectory : this.textBoxOutputDirectory;
			if (result == DialogResult.OK) { target.Text = folderBrowserDialog1.SelectedPath; }
		}

		private void buttonConvert_Click(object sender, EventArgs e) {
			if (!checkBoxCreateMetadataTable.Checked && !checkBoxCopyAndRename.Checked && !checkBoxExtractMetadata.Checked) { MessageBox.Show("No checkboxes ticked - program has no task to perform."); return; }
			if (textBoxDICOMDirectory.Text.Length == 0 || textBoxOutputDirectory.Text.Length == 0) { 
				MessageBox.Show("Either Source or Destination directories not set. Cannot continue.", "Not all paths specified", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}
			if (!Directory.Exists(textBoxDICOMDirectory.Text)) {
				MessageBox.Show("Source directory not found: " + textBoxDICOMDirectory.Text, "Invalid Source Path", MessageBoxButtons.OK, MessageBoxIcon.Error); 
				return;
			}
			if (!Directory.Exists(textBoxOutputDirectory.Text)) {
				MessageBox.Show("Output directory not found: " + textBoxOutputDirectory.Text, "Invalid Output Path", MessageBoxButtons.OK, MessageBoxIcon.Error); 
				return;
			}

			panelProgressBar.BringToFront();
			panelProgressBar.Visible = true;
			labelProgressBar.BringToFront();
			labelProgressBar.Visible = true;
			progressBarDICOMFiles.BringToFront();
			progressBarDICOMFiles.Visible = true;

			try { TraverseDirectoryTree(textBoxDICOMDirectory.Text); }

			finally {
				panelProgressBar.SendToBack();
				panelProgressBar.Visible = false;
				labelProgressBar.SendToBack();
				labelProgressBar.Visible = false;
				progressBarDICOMFiles.SendToBack();
				progressBarDICOMFiles.Visible = false;
			}
		}

		private void TraverseDirectoryTree(string root) {
			StreamWriter MetadataTable = null;
			int errorCount = 0, copyCount = 0, deleteCount = 0, extractCount = 0, tableCount = 0;
			lostTagCount = 0;
			Stack<string> dirs = new Stack<string>(100);
			List<string> files = new List<string>();
			if (checkBoxCreateMetadataTable.Checked) {
				MetadataTable = new StreamWriter(Path.Combine(textBoxOutputDirectory.Text, DateTime.Now.ToString("yyyy-MM-dd_HH-mm") + "_DICOMFilesMetadata-Table.txt"), false);
				MetadataTable.WriteLine("Patient ID\tPatient Name\tStudy Date\tStudy Time\tData Type\tFrames");
				MetadataTable.Flush();
			}

			//Acquisition
			if (!Directory.Exists(root)) { throw new ArgumentException("Specified root directory doesn't exist: " + root); }
			dirs.Push(root);
			while (dirs.Count > 0) {
				string currentDir = dirs.Pop();
	
				string[] subDirs;
				try { subDirs = Directory.GetDirectories(currentDir); }
				catch (UnauthorizedAccessException) { continue; }
				catch (DirectoryNotFoundException) { continue; }
				foreach (string str in subDirs) { dirs.Push(str); }				// Push subdirectories on stack for traversal.

				string[] DICOMFiles = null;
				try { DICOMFiles = Directory.GetFiles(currentDir, "*.dcm"); }
				catch (UnauthorizedAccessException) { continue; }
				catch (DirectoryNotFoundException) { continue; }
				foreach (string file in DICOMFiles) { files.Add(file); }
			} // while dirs.Count > 0

			//Processing

			bool DoSimpleParse = !(checkBoxCreateMetadataTable.Checked || checkBoxExtractMetadata.Checked);

			int maxFiles = files.Count;
			for (int i = 0; i < maxFiles; i++) {
				float progress = ((float)(i+1) / maxFiles);
				labelProgressBar.Text = string.Format("Processing file {0} of {1} ({2:p0})", i+1, maxFiles, progress);
				progressBarDICOMFiles.Value = (int)Math.Floor(progress * 100);
				Application.DoEvents(); //keeps GUI alive by returning control to OS thread

				string DICOMFile = files[i];
				string NewFolderName, NewFileName, StudyTime, PatientName, StudyDate, PatientID;
				int nFrames;
				DICOM.File dcf = null;
				try { //Take and open file, extract metadata
					dcf = new DICOM.File(DICOMFile, DoSimpleParse);
					PatientName = TagValueOrDefault(0x00100010, dcf.Tags);
					PatientID = TagValueOrDefault(0x00100020, dcf.Tags);
					StudyDate = TagValueOrDefault(0x00080023, dcf.Tags);
					StudyTime = TagValueOrDefault(0x00080033, dcf.Tags);
					if (!DoSimpleParse) { nFrames = int.Parse(TagValueOrDefault(0x00280008, dcf.Tags, "1")); }
					else { nFrames = ((new FileInfo(DICOMFile).Length / 1024) > 811) ? 125 : 1; } //single-image files are always 811KB big, so 811 * 1024 bytes
					NewFolderName = string.Format("{0}_{1}_{2}", StudyDate, PatientID, PatientName);
				}// try
				catch (FileNotFoundException) { errorCount++; continue; }
				
				if (checkBoxCreateMetadataTable.Checked) {
					//"Patient ID\tPatient Name\tStudy Date\tStudy Time\tData Type\tFrames"
					MetadataTable.WriteLine(string.Format("{0}\t{1}\t{2}\t{3}\t{4}\t{5}", PatientID, PatientName, StudyDate, StudyTime, (nFrames > 1 ? "Movie" : "Image"), nFrames));
					tableCount++;
					MetadataTable.Flush();
				}

				foreach (char c in InvalidPathChars) { NewFolderName = NewFolderName.Replace(c.ToString(), ""); } //sanitize path input
				foreach (char c in new char[] { '/', '\\', '?' }) { NewFolderName = NewFolderName.Replace(c.ToString(), "-"); } //these aren't in InvalidPathChars since they're allowed in (full) paths
				DirectoryInfo OutputFolder = new DirectoryInfo(Path.Combine(textBoxOutputDirectory.Text, NewFolderName));
				if (checkBoxCopyAndRename.Checked || checkBoxExtractMetadata.Checked) {
					//prepare receiving folder
					if (!Directory.Exists(OutputFolder.FullName)) {
						try { Directory.CreateDirectory(OutputFolder.FullName); }
						catch (UnauthorizedAccessException) {
							MessageBox.Show("Cannot gain access to " + textBoxOutputDirectory.Text + ". Please select another directory or run as administrator.");
							return;
						}
					}//if FolderName doesn't exist
				}

				if (checkBoxCopyAndRename.Checked) {
					//Ensure numbering is consistent and continuous
					int nPictures = 1, nVideos = 1;
					FileInfo[] ExistingFiles = OutputFolder.GetFiles("*.dcm");
					nPictures += ExistingFiles.Where(n => n.Name.Contains("Image")).Count();
					nVideos += ExistingFiles.Where(n => n.Name.Contains("Video")).Count();
					NewFileName = string.Format("{0}_{1}_{2}.dcm", NewFolderName, StudyTime, (nFrames > 1 ? ("Video" + nVideos) : ("Image" + nPictures)));
					foreach (char c in InvalidFileChars) { NewFileName.Replace(c.ToString(), ""); }

					//Copy File, Test File, if there's a request, Delete File
					string NewFilePath = Path.Combine(OutputFolder.FullName, NewFileName);
					if(File.Exists(NewFilePath)) { //Need to change name not to overwrite; safety first
						int fileCount = OutputFolder.GetFiles(Path.GetFileNameWithoutExtension(NewFileName) + "*").Length + 1;
						NewFileName = string.Format("{0}({1}){2}", Path.GetFileNameWithoutExtension(NewFileName), fileCount, Path.GetExtension(NewFileName));
						NewFilePath = Path.Combine(OutputFolder.FullName, NewFileName);
					} //File.Exists(NewFilePath)

					try {
						File.Copy(DICOMFile, NewFilePath);
						copyCount++;
					}
					catch (UnauthorizedAccessException) { MessageBox.Show("Access denied for export directory. Please select another directory or restart as administrator."); return; }
					catch (IOException ioe) { MessageBox.Show("Unexpected IO Exception: " + ioe.Message); }

					Application.DoEvents();

					if (checkBoxCheckAndDeleteAfterCopy.Checked) {
						if (TestFileEquality(new FileInfo(DICOMFile), new FileInfo(NewFilePath))) {
							File.Delete(DICOMFile);
							deleteCount++;
						}
					} //checkBoxCheckAndDeleteAfterCopy.Checked

				}//if checkboxCopyAndRename.Checked

				if (checkBoxExtractMetadata.Checked) {
					using (StreamWriter metadataFile = new StreamWriter(Path.Combine(OutputFolder.FullName, "metadata.txt"), true)) {
						metadataFile.WriteLine("Metadata extracted from " + DICOMFile +"\nTag\tValue");
						foreach (DICOM.Tag tag in dcf.Tags) { metadataFile.WriteLine(tag.Name + "\t" + tag.Value); } //TODO something fucky here.
						metadataFile.WriteLine("\n");
						extractCount++;
					}
				}//if checkboxExtractMetadata.Checked
			}//foreach DICOMFile

			if (checkBoxCreateMetadataTable.Checked) { MetadataTable.Close(); } //cleanup

			//Generating success message to user
			string Report = "Operation(s) completed successfully\n";
			string LostTagReport = lostTagCount > 0 ? lostTagCount + " values could not found and defaulted to 'undefined'.\n" : "";
			if (checkBoxCopyAndRename.Checked) { Report += copyCount + " files copied and renamed. " + LostTagReport; }
			if (checkBoxCheckAndDeleteAfterCopy.Checked) { Report += deleteCount + " files deleted.\n"; }
			if (checkBoxExtractMetadata.Checked) { Report += "Metadata extracted for " + extractCount + " files. " + LostTagReport;  }
			if (checkBoxCreateMetadataTable.Checked) { Report += tableCount + " files processed for metadata table. " + LostTagReport; }
			progressBarDICOMFiles.Value = 100;
			Application.DoEvents();
			this.BringToFront();
			MessageBox.Show(Report, "DICOM processing complete.");
		} // TraverseDirectoryTree

		private void Form1_HelpButtonClicked(object sender, CancelEventArgs e) {
			MessageBox.Show("DICOM Processor Help", "DICOM Processor is created and optimised for use with the specific model of GE Echocardiograph stationed in the University of Manchester BSF." +
				"There is no warranty, express or implied. Use at your own risk. Back up your data regularly.");
		}

		private void checkBoxCopyAndRename_CheckedChanged(object sender, EventArgs e) {
			bool state = ((CheckBox)sender).Checked;
			if (!state) { checkBoxCheckAndDeleteAfterCopy.Checked = false; }
			checkBoxCheckAndDeleteAfterCopy.Enabled = state;
		}
		private string TagValueOrDefault(int TagKey, List<DICOM.Tag> SearchSpace, string defaultValue = "Undefined") {
			try {
				DICOM.Tag tag = SearchSpace.Find(n => n.GroupElement == TagKey);
				return tag.Value;
			}
			catch {
				if (TagKey != 0x00280008) { lostTagCount++; } //we expect not to see Number of Frames in Image Files (single frame, but not listed)
				return defaultValue; 
			}
		}

		private bool TestFileEquality(FileInfo file1, FileInfo file2) {
			if (file1.Length != file2.Length) { return false; }
			int iterations = (int)Math.Ceiling((double)file1.Length / BYTES_TO_READ);
			using (FileStream fs1 = file1.OpenRead(), fs2 = file2.OpenRead()) {
				byte[] one = new byte[BYTES_TO_READ];
				byte[] two = new byte[BYTES_TO_READ];
				for (int i = 0; i < iterations; i++) {
					fs1.Read(one, 0, BYTES_TO_READ);
					fs2.Read(two, 0, BYTES_TO_READ);
					if (BitConverter.ToInt64(one, 0) != BitConverter.ToInt64(two, 0)) { return false; }//tripped at the first mismatch
				}// for iterations
			}//using FileStream fs1 and FileStream fs2
			return true;
		} //bool TestFileEquality
	} //class Form 1
} // namespace DICOMExporter
