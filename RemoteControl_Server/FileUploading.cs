using System;
using System.ComponentModel;
using System.Drawing;
using System.Media;
using System.Windows.Forms;

namespace RemoteControl_Server
{
	public class FileUploading : Form
	{
		private IContainer components = null;

		private TableLayoutPanel tableLayoutPanel;

		public ProgressBar progressBar;

		public Label label;

		public Button cancelBtn;

		protected override void Dispose(bool disposing)
		{
			if (disposing && components != null)
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		private void InitializeComponent()
		{
			tableLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
			progressBar = new System.Windows.Forms.ProgressBar();
			label = new System.Windows.Forms.Label();
			cancelBtn = new System.Windows.Forms.Button();
			tableLayoutPanel.SuspendLayout();
			SuspendLayout();
			tableLayoutPanel.CellBorderStyle = System.Windows.Forms.TableLayoutPanelCellBorderStyle.Inset;
			tableLayoutPanel.ColumnCount = 1;
			tableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100f));
			tableLayoutPanel.Controls.Add(label, 0, 0);
			tableLayoutPanel.Controls.Add(progressBar, 0, 1);
			tableLayoutPanel.Controls.Add(cancelBtn, 0, 2);
			tableLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
			tableLayoutPanel.Location = new System.Drawing.Point(0, 0);
			tableLayoutPanel.Name = "tableLayoutPanel";
			tableLayoutPanel.RowCount = 3;
			tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30f));
			tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100f));
			tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30f));
			tableLayoutPanel.Size = new System.Drawing.Size(394, 97);
			tableLayoutPanel.TabIndex = 0;
			progressBar.Dock = System.Windows.Forms.DockStyle.Fill;
			progressBar.Location = new System.Drawing.Point(5, 37);
			progressBar.MarqueeAnimationSpeed = 50;
			progressBar.Name = "progressBar";
			progressBar.Size = new System.Drawing.Size(384, 23);
			progressBar.Style = System.Windows.Forms.ProgressBarStyle.Marquee;
			progressBar.TabIndex = 2;
			label.Dock = System.Windows.Forms.DockStyle.Fill;
			label.Location = new System.Drawing.Point(5, 2);
			label.Name = "label";
			label.Size = new System.Drawing.Size(384, 30);
			label.TabIndex = 3;
			label.Text = "Awaiting connection...";
			label.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			cancelBtn.Dock = System.Windows.Forms.DockStyle.Right;
			cancelBtn.Location = new System.Drawing.Point(264, 68);
			cancelBtn.Name = "cancelBtn";
			cancelBtn.Size = new System.Drawing.Size(125, 24);
			cancelBtn.TabIndex = 4;
			cancelBtn.Text = "Cancel Upload";
			cancelBtn.UseVisualStyleBackColor = true;
			base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			AutoSize = true;
			base.ClientSize = new System.Drawing.Size(394, 97);
			base.Controls.Add(tableLayoutPanel);
			base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
			base.MaximizeBox = false;
			MaximumSize = new System.Drawing.Size(400, 125);
			MinimumSize = new System.Drawing.Size(400, 125);
			base.Name = "FileUploading";
			Text = "Uploading (?) item (?.? MB)";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FileUploading_FormClosing);
			base.Load += new System.EventHandler(FileUploading_Load);
			tableLayoutPanel.ResumeLayout(false);
			ResumeLayout(false);
		}

		public FileUploading()
		{
			InitializeComponent();
		}

		private void FileUploading_Load(object sender, EventArgs e)
		{
			base.DialogResult = DialogResult.Retry;
		}

		private void FileUploading_FormClosing(object sender, FormClosingEventArgs e)
		{
			CloseReason closeReason = e.CloseReason;
			if (closeReason != CloseReason.UserClosing)
			{
				return;
			}
			DialogResult dialogResult = base.DialogResult;
			if (dialogResult != DialogResult.OK && dialogResult == DialogResult.Retry)
			{
				e.Cancel = true;
				cancelBtn.Focus();
				SystemSounds.Beep.Play();
				if (base.Parent != null)
				{
					base.Parent.Focus();
				}
			}
		}
	}
}
