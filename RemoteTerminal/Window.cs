using RemoteControl_Server;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Media;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace RemoteTerminal
{
	public class Window : Form
	{
		private delegate IPHostEntry GetHostEntryHandler(string ip);

		public static class StringExtensions
		{
			public static string ToLimitedString(string instance, string validCharacters)
			{
				return new string((from c in instance
					where validCharacters.Contains(c)
					select c).ToArray());
			}
		}

		private TcpClient socket = new TcpClient();

		private string line = Environment.NewLine;

		private string applicationName = "RemoteControl-Server";

		public static string serverData = "";

		private bool connected = false;

		private string saveDirectory = Path.GetTempPath() + "RemoteControl_saves\\";

		private Size defaultBounds = default(Size);

		private FileUploading uploadForm = null;

		private int processHash = 0;

		private int processStartTime = 0;

		private bool batchConsoleEnabled = false;

		private int screenshotAttempts = 0;

		private long lastScreenshotTime = 0L;

		private bool runCaptureLoop = false;

		private string[] loadingSeq = new string[4]
		{
			"/",
			"-",
			"\\",
			"|"
		};

		private int localNetworkSearchLoadingSeq = 0;

		private IContainer components = null;

		private GroupBox addressBox;

		private TextBox addressText;

		private GroupBox loginBox;

		private Button connectBtn;

		private System.Windows.Forms.Timer noTimeoutTimer;

		private ContextMenuStrip fileManagerMenuStrip;

		private ToolStripMenuItem addExistingFileToolStripMenuItem;

		private ToolStripMenuItem removeCachedFileToolStripMenuItem;

		private OpenFileDialog openFileDialog;

		private ToolStripSeparator toolStripSeparator;

		private ToolStripMenuItem runOnTargetComputerToolStripMenuItem;

		private ContextMenuStrip windowMenuStrip;

		private ToolStripMenuItem closeConnectionToolStripMenuItem;

		private TabPage desktopControlTab;

		private ListBox localNetwork;

		private ContextMenuStrip localNetworkStrip;

		private ToolStripMenuItem populateToolStripMenuItem;

		private BackgroundWorker localNetworkSearch;

		private ToolStripMenuItem connectionDetailsToolStripMenuItem;

		private Panel connectionBox;

		private TableLayoutPanel mainLayoutPanel;

		private SplitContainer splitContainer;

		private CheckBox sendMessageCheck;

		private GroupBox fileHistoryGroup;

		private ListBox fileHistory;

		private GroupBox codeGroupBox;

		private TabControl tabControl;

		private TabPage batchCodeTab;

		private TextBox batchCodeBox;

		private TabPage batchAdministrationTab;

		private MenuStrip batchConsoleMenuStrip;

		private ToolStripMenuItem terminateTaskBtn;

		private ToolStripMenuItem batchStatusLabel;

		private ListBox batchAdministrativeHistory;

		private TextBox batchAdministrativeInput;

		private ToolStripMenuItem enableDisableToolStripMenuItem;

		private MenuStrip batchProcessorMenuStrip;

		private ToolStripMenuItem saveCodeToolStripMenuItem;

		private ToolStripMenuItem loadCodeToolStripMenuItem;

		private ToolStripComboBox saveCodeComboBox;

		private TableLayoutPanel batchConsoleLayoutPanel;

		private SplitContainer splitContainer2;

		private TextBox messageTextInput;

		private CheckBox sendBatchCheck;

		private TabPage usageMonitorTab;

		private Label usageLabel;

		private Button startStopMonitor;

		private Label monitorStatusLabel;

		private TabPage screenshotTab;

		private TableLayoutPanel captureScreenshotPanel;

		private Button screenshotBtn;

		private PictureBox screenshotImage;

		private ContextMenuStrip screenshotCaptureStrip;

		private ToolStripMenuItem copyScreenshotToMemoryToolStripMenuItem;

		private ContextMenuStrip captureScreenshotBtnStrip;

		private ToolStripMenuItem startEndlessCaptureLoopToolStripMenuItem;

		private ToolStripMenuItem saveScreenshotToFileToolStripMenuItem;

		private SaveFileDialog saveScreenshotDialog;

		private BackgroundWorker screenshotWorker;

		private GroupBox localNetworkGroupBox;

		private System.Windows.Forms.Timer loadingAnimation;

		private BackgroundWorker uploadingFile;

		private Label notifyLabel;

		private ToolStripMenuItem flashClientsideUpdateToolStripMenuItem;

		private BackgroundWorker uploadUpdate;

		private System.Windows.Forms.Timer listLocalNetwork;

		[DllImport("user32.dll", CharSet = CharSet.Auto)]
		private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, [MarshalAs(UnmanagedType.LPWStr)] string lParam);

		public Window()
		{
			InitializeComponent();
			defaultBounds = base.Size;
			if (!File.Exists(Path.GetTempPath() + "RemoteControl_windowInfo.tmp"))
			{
				base.StartPosition = FormStartPosition.CenterScreen;
			}
		}

		public Dictionary<string, string> VisibleComputers()
		{
			ArrayList networkComputers = new NetworkBrowser().getNetworkComputers();
			Dictionary<string, string> dictionary = new Dictionary<string, string>();
			foreach (string item in networkComputers)
			{
				dictionary.Add(item, Dns.GetHostByName(item).AddressList.FirstOrDefault().ToString());
			}
			return dictionary;
		}

		private void Window_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (localNetworkSearch.IsBusy)
			{
				localNetworkSearch.CancelAsync();
			}
			if (connected)
			{
				DialogResult dialogResult = MessageBox.Show("This will close your ongoing connection, continue?", applicationName, MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);
				if (dialogResult == DialogResult.No)
				{
					e.Cancel = true;
				}
			}
			if (base.WindowState == FormWindowState.Maximized)
			{
				File.WriteAllText(Path.GetTempPath() + "RemoteControl_windowInfo.tmp", "Maximized;" + base.RestoreBounds.Width + ";" + base.RestoreBounds.Height + ";" + base.RestoreBounds.X + ";" + base.RestoreBounds.Y);
			}
			else
			{
				File.WriteAllText(Path.GetTempPath() + "RemoteControl_windowInfo.tmp", base.Width + ";" + base.Height + ";" + base.Location.X + ";" + base.Location.Y);
			}
		}

		public string GetReverseDNS(string ip, int timeout)
		{
			try
			{
				GetHostEntryHandler getHostEntryHandler = Dns.GetHostEntry;
				IAsyncResult asyncResult = getHostEntryHandler.BeginInvoke(ip, null, null);
				if (asyncResult.AsyncWaitHandle.WaitOne(timeout, false))
				{
					return getHostEntryHandler.EndInvoke(asyncResult).HostName;
				}
				return ip;
			}
			catch (Exception)
			{
				return ip;
			}
		}

		private void Window_Load(object sender, EventArgs e)
		{
			Text = applicationName;
			loginBox.Dock = DockStyle.Fill;
			if (!Directory.Exists(saveDirectory))
			{
				Directory.CreateDirectory(saveDirectory);
			}
			if (File.Exists(Path.GetTempPath() + "\\previous_connection.tmp"))
			{
				addressText.Text = File.ReadAllText(Path.GetTempPath() + "\\previous_connection.tmp");
			}
			ToolTip toolTip = new ToolTip();
			toolTip.AutoPopDelay = 5000;
			toolTip.InitialDelay = 1000;
			toolTip.ReshowDelay = 500;
			toolTip.ShowAlways = true;
			toolTip.SetToolTip(sendBatchCheck, "Send the provided batch code over the personal connection");
			toolTip.SetToolTip(sendMessageCheck, "Send the provided message over the personal connection");
			toolTip.SetToolTip(connectBtn, "Attempt to privately connect to the given server address");
			if (File.Exists(Path.GetTempPath() + "RemoteControl_windowInfo.tmp"))
			{
				string text = File.ReadAllText(Path.GetTempPath() + "RemoteControl_windowInfo.tmp");
				if (text.Split(';')[0] == "Maximized")
				{
					base.Size = new Size(int.Parse(text.Split(';')[1]), int.Parse(text.Split(';')[2]));
					base.Location = new Point(int.Parse(text.Split(';')[3]), int.Parse(text.Split(';')[4]));
					base.WindowState = FormWindowState.Maximized;
				}
				else
				{
					base.Size = new Size(int.Parse(text.Split(';')[0]), int.Parse(text.Split(';')[1]));
					base.Location = new Point(int.Parse(text.Split(';')[2]), int.Parse(text.Split(';')[3]));
				}
			}
		}

		private string GetRemoteHostName()
		{
			string text = null;
			try
			{
				text = serverData.Split(new string[1]
				{
					Environment.NewLine
				}, StringSplitOptions.None)[0];
			}
			catch
			{
			}
			if (text != null)
			{
				return text;
			}
			return "?";
		}

		public static string GetLocalIPAddress()
		{
			IPHostEntry hostEntry = Dns.GetHostEntry(Dns.GetHostName());
			IPAddress[] addressList = hostEntry.AddressList;
			foreach (IPAddress iPAddress in addressList)
			{
				if (iPAddress.AddressFamily == AddressFamily.InterNetwork)
				{
					return iPAddress.ToString();
				}
			}
			throw new Exception("Local IP address failed to resolve");
		}

		public double ConvertToUnixTimestamp(DateTime date)
		{
			DateTime d = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
			return Math.Floor((date.ToUniversalTime() - d).TotalMilliseconds);
		}

		private void connectBtn_Click(object sender, EventArgs e)
		{
			try
			{
				serverData = "";
				fileHistory.Items.Clear();
				IAsyncResult asyncResult = socket.BeginConnect(addressText.Text.ToLower(), 28895, null, null);
				if (!asyncResult.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(1.0)))
				{
					socket.Close();
					socket = new TcpClient();
					throw new Exception("Failed to connect to the specified address.");
				}
				socket.EndConnect(asyncResult);
				int num = 0;
				while (num < 20)
				{
					num++;
					if (socket.GetStream().DataAvailable)
					{
						byte[] array = new byte[8192];
						int num2 = socket.GetStream().Read(array, 0, array.Length);
						byte[] array2 = new byte[num2];
						Array.Copy(array, array2, array2.Length);
						string text = Encoding.UTF8.GetString(array2).Trim();
						if (text.Contains('\\'))
						{
							string text2 = text.Substring(text.IndexOf('\\'));
							string[] array3 = text2.Split('\\');
							foreach (string text3 in array3)
							{
								if (text3.Length > 0)
								{
									fileHistory.Items.Add(text3.Trim());
								}
							}
						}
						serverData = text.Split('\\')[0];
						break;
					}
					Thread.Sleep(25);
				}
				if (num == 5)
				{
					MessageBox.Show("Failed to retrieve server information, functionality may be limited." + Environment.NewLine + "You may consider manually restarting the client on the other end.", applicationName, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				}
				base.Enabled = false;
				loginBox.Hide();
				connectionBox.Show();
				connected = true;
				messageTextInput.Text = "";
				batchCodeBox.Text = "";
				Text = applicationName + " - " + addressText.Text.ToLower() + " (" + GetRemoteHostName() + ")";
				noTimeoutTimer.Start();
				batchStatusLabel.Text = "Status: Inactive";
				batchAdministrativeInput.Text = "";
				batchAdministrativeInput.Enabled = false;
				terminateTaskBtn.Enabled = false;
				batchAdministrativeHistory.Items.Clear();
				tabControl.SelectedIndex = 0;
				screenshotAttempts = 0;
				screenshotImage.BackgroundImage = null;
				screenshotBtn.Text = "Capture Screenshot";
				SystemSounds.Asterisk.Play();
				File.WriteAllText(Path.GetTempPath() + "\\previous_connection.tmp", addressText.Text.ToLower());
				saveCodeComboBox.Items.Clear();
				DirectoryInfo directoryInfo = new DirectoryInfo(saveDirectory);
				FileInfo[] files = directoryInfo.GetFiles("*.*");
				if (batchConsoleEnabled)
				{
					enableDisableToolStripMenuItem_Click(sender, new EventArgs());
				}
				FileInfo[] array4 = files;
				foreach (FileInfo fileInfo in array4)
				{
					string ip = fileInfo.Name.Substring(0, fileInfo.Name.IndexOf(".txt"));
					string text = fileInfo.Name + " (" + GetReverseDNS(ip, 100) + ")";
					string text4 = text.Replace(".txt.1", ".txt.2").Replace(".txt.2", ".txt.3").Replace(".txt.3", ".txt.1");
					string text5 = text.Replace(".txt.1", ".txt.2").Replace(".txt.2", ".txt.3").Replace(".txt.3", ".txt.2");
					string text6 = text.Replace(".txt.1", ".txt.2").Replace(".txt.2", ".txt.3").Replace(".txt.3", ".txt.3");
					if (!saveCodeComboBox.Items.Contains(text4))
					{
						saveCodeComboBox.Items.Add(text4);
					}
					if (!saveCodeComboBox.Items.Contains(text5))
					{
						saveCodeComboBox.Items.Add(text5);
					}
					if (!saveCodeComboBox.Items.Contains(text6))
					{
						saveCodeComboBox.Items.Add(text6);
					}
				}
				string remoteHostName = GetRemoteHostName();
				string str = addressText.Text.ToLower() + ".txt";
				if (!saveCodeComboBox.Items.Contains(str + ".1 (" + remoteHostName + ")"))
				{
					saveCodeComboBox.Items.Add(str + ".1 (" + remoteHostName + ")");
				}
				if (!saveCodeComboBox.Items.Contains(str + ".2 (" + remoteHostName + ")"))
				{
					saveCodeComboBox.Items.Add(str + ".2 (" + remoteHostName + ")");
				}
				if (!saveCodeComboBox.Items.Contains(str + ".3 (" + remoteHostName + ")"))
				{
					saveCodeComboBox.Items.Add(str + ".3 (" + remoteHostName + ")");
				}
				saveCodeComboBox.SelectedIndex = saveCodeComboBox.Items.IndexOf(str + ".1 (" + remoteHostName + ")");
				base.Enabled = true;
			}
			catch (Exception ex)
			{
				base.Enabled = true;
				try
				{
					socket.Close();
					socket = new TcpClient();
				}
				catch
				{
				}
				MessageBox.Show("The specified network address could not be resolved" + line + line + ex.Message, applicationName, MessageBoxButtons.OK, MessageBoxIcon.Hand);
			}
		}

		public Image toImage(byte[] arr, int width, int height)
		{
			try
			{
				return (Bitmap)new ImageConverter().ConvertFrom(arr);
			}
			catch
			{
				return new Bitmap(width, height);
			}
		}

		private void noTimeoutTimer_Tick(object sender, EventArgs e)
		{
			try
			{
				if (!uploadingFile.IsBusy && !uploadUpdate.IsBusy)
				{
					byte[] bytes = Encoding.UTF8.GetBytes("cmd /c @update");
					socket.GetStream().Write(bytes, 0, bytes.Length);
					socket.GetStream().Flush();
				}
				SendMessage(messageTextInput.Handle, 5377, 2, "Enter your message here");
				SendMessage(batchAdministrativeInput.Handle, 5377, 1, "Enter your response here");
				if (Control.ModifierKeys == Keys.Shift || (screenshotWorker.IsBusy && runCaptureLoop))
				{
					screenshotBtn.FlatStyle = FlatStyle.Flat;
				}
				else
				{
					screenshotBtn.FlatStyle = FlatStyle.Popup;
				}
				if (screenshotWorker.IsBusy && runCaptureLoop)
				{
					startEndlessCaptureLoopToolStripMenuItem.Text = "Stop the endless capture loop";
				}
				else
				{
					startEndlessCaptureLoopToolStripMenuItem.Text = "Start the endless capture loop";
				}
			}
			catch (Exception ex)
			{
                handleDisconnect();
                MessageBox.Show(ex.Message + Environment.NewLine + ex.StackTrace);
			}
		}

		private void sendMessageCheck_Click(object sender, EventArgs e)
		{
			sendMessageCheck.Checked = false;
			if (messageTextInput.Text.Length > 0)
			{
				bool flag = false;
				if (batchConsoleEnabled)
				{
					flag = true;
					batchConsoleEnabled = false;
				}
				handleCommand("start \"\" /min cmd /c \"echo msgbox \"" + messageTextInput.Text + "\" > %tmp%\\message.vbs && cscript /nologo %tmp%\\message.vbs && del %tmp%\\message.vbs\"");
				if (flag)
				{
					batchConsoleEnabled = true;
				}
				sendMessageCheck.Enabled = false;
				sendMessageCheck.Update();
				Thread.Sleep(100);
				sendMessageCheck.Enabled = true;
				messageTextInput.Text = "";
				Activate();
			}
		}

		private void sendBatchCheck_Click(object sender, EventArgs e)
		{
			sendBatchCheck.Checked = false;
			if (batchCodeBox.Text.Length > 0)
			{
				handleCommand(batchCodeBox.Text);
				sendBatchCheck.Enabled = false;
				sendBatchCheck.Update();
				Thread.Sleep(100);
				sendBatchCheck.Enabled = true;
				Activate();
			}
		}

		private void messageTextInput_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Return)
			{
				e.SuppressKeyPress = true;
				sendMessageCheck_Click(sender, new EventArgs());
			}
		}

		private void addExistingFileToolStripMenuItem_Click(object sender, EventArgs e)
		{
			DialogResult dialogResult = openFileDialog.ShowDialog();
			if (dialogResult == DialogResult.OK)
			{
				if (fileHistory.Items.Contains(Path.GetFileName(openFileDialog.FileName)))
				{
					fileHistory.Items.Remove(Path.GetFileName(openFileDialog.FileName));
				}
				FileInfo fileInfo = new FileInfo(openFileDialog.FileName);
				uploadForm = new FileUploading();
				uploadForm.Show();
				uploadForm.Text = "Uploading (1) item (" + (fileInfo.Length >> 20) + " MB)";
				uploadForm.cancelBtn.Click += cancelBtn_Click;
				base.Enabled = false;
				uploadingFile.RunWorkerAsync();
			}
		}

		private void callUploadFinish()
		{
			base.Enabled = true;
			bool flag = false;
			if (uploadForm.DialogResult == DialogResult.OK)
			{
				flag = true;
			}
			uploadForm.DialogResult = DialogResult.OK;
			uploadForm.Close();
			uploadForm = null;
			if (flag)
			{
				MessageBox.Show("File upload process was cancelled by the user.", applicationName, MessageBoxButtons.OK, MessageBoxIcon.Hand);
			}
			else
			{
				SystemSounds.Asterisk.Play();
			}
			BringToFront();
			Focus();
		}

		private void cancelBtn_Click(object sender, EventArgs e)
		{
			DialogResult dialogResult = MessageBox.Show("Are you sure you want to cancel the upload?", applicationName, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
			if (dialogResult == DialogResult.Yes)
			{
				uploadForm.DialogResult = DialogResult.OK;
				uploadForm.Close();
			}
		}

		private void uploadingFile_DoWork_1(object sender, DoWorkEventArgs e)
		{
			FileInfo info = new FileInfo(openFileDialog.FileName);
			string fileNameString = Path.GetFileName(openFileDialog.FileName);
			byte[] bytes = Encoding.UTF8.GetBytes("cmd /c @file(" + fileNameString + ")");
			socket.GetStream().Write(bytes, 0, bytes.Length);
			socket.GetStream().Flush();
			Thread.Sleep(200);
			BeginInvoke((MethodInvoker)delegate
			{
				uploadForm.label.Text = "Uploading " + fileNameString + " to " + addressText.Text.ToLower() + " (" + GetRemoteHostName() + ")";
			});
			BeginInvoke((MethodInvoker)delegate
			{
				uploadForm.progressBar.Style = ProgressBarStyle.Blocks;
			});
			BeginInvoke((MethodInvoker)delegate
			{
				uploadForm.progressBar.Maximum = (int)(info.Length >> 10);
			});
			int num = 8192;
			FileStream fileStream = new FileStream(openFileDialog.FileName, FileMode.Open, FileAccess.Read);
			int num2 = Convert.ToInt32(Math.Ceiling((double)fileStream.Length / (double)num));
			for (int i = 0; i < num2; i++)
			{
				if (!uploadForm.Visible)
				{
					break;
				}
				byte[] array = new byte[num];
				int size = fileStream.Read(array, 0, array.Length);
				socket.GetStream().Write(array, 0, size);
				socket.GetStream().Flush();
				if (uploadForm.progressBar.Value + size / 1000 < uploadForm.progressBar.Maximum)
				{
					BeginInvoke((MethodInvoker)delegate
					{
						uploadForm.progressBar.Value += size / 1000;
					});
				}
				else
				{
					BeginInvoke((MethodInvoker)delegate
					{
						uploadForm.progressBar.Value = uploadForm.progressBar.Maximum;
					});
				}
				BeginInvoke((MethodInvoker)delegate
				{
					uploadForm.progressBar.Update();
				});
			}
			Thread.Sleep(200);
			socket.GetStream().Write(Encoding.UTF8.GetBytes("</endFile>"), 0, Encoding.UTF8.GetBytes("</endFile>").Length);
			socket.GetStream().Flush();
			fileStream.Close();
			BeginInvoke((MethodInvoker)delegate
			{
				fileHistory.Items.Add(Path.GetFileName(openFileDialog.FileName));
			});
			BeginInvoke((MethodInvoker)delegate
			{
				callUploadFinish();
			});
		}

		private void removeCachedFileToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (fileHistory.SelectedItem != null)
			{
				base.Enabled = false;
				string str = fileHistory.SelectedItem.ToString();
				byte[] bytes = Encoding.UTF8.GetBytes("cmd /c @delfile(" + str + ")");
				socket.GetStream().Write(bytes, 0, bytes.Length);
				socket.GetStream().Flush();
				fileHistory.Items.RemoveAt(fileHistory.SelectedIndex);
				Thread.Sleep(200);
				base.Enabled = true;
			}
		}

		private void runOnTargetComputerToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (fileHistory.SelectedItem == null)
			{
				MessageBox.Show("You must first select the file you would like to run", applicationName, MessageBoxButtons.OK, MessageBoxIcon.Hand);
				return;
			}
			string str = fileHistory.SelectedItem.ToString();
			base.Enabled = false;
			byte[] bytes = Encoding.UTF8.GetBytes("cmd /c @runfile(" + str + ")");
			socket.GetStream().Write(bytes, 0, bytes.Length);
			socket.GetStream().Flush();
			Thread.Sleep(200);
			base.Enabled = true;
		}

		private void fileManagerMenuStrip_Opening(object sender, CancelEventArgs e)
		{
			if (fileHistory.SelectedItem == null)
			{
				toolStripSeparator.Visible = false;
				removeCachedFileToolStripMenuItem.Visible = false;
				runOnTargetComputerToolStripMenuItem.Visible = false;
			}
			else
			{
				toolStripSeparator.Visible = true;
				removeCachedFileToolStripMenuItem.Visible = true;
				runOnTargetComputerToolStripMenuItem.Visible = true;
			}
		}

		private void closeConnectionToolStripMenuItem_Click(object sender, EventArgs e)
		{
			DialogResult dialogResult = MessageBox.Show("This will close your ongoing connection, continue?", applicationName, MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);
			if (dialogResult == DialogResult.Yes)
			{
				handleDisconnect();
			}
		}

		private void addressText_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Return)
			{
				e.SuppressKeyPress = true;
				connectBtn_Click(sender, new EventArgs());
			}
		}

		private void populateToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (!localNetworkSearch.IsBusy)
			{
				localNetwork.Items.Clear();
				localNetworkSearch.RunWorkerAsync();
			}
		}

		private void localNetwork_MouseDoubleClick(object sender, MouseEventArgs e)
		{
			if (localNetwork.SelectedItem != null && localNetwork.SelectedItem.ToString().Length != 0)
			{
				addressText.Text = localNetwork.SelectedItem.ToString().Split(" (".ToCharArray())[0];
				connectBtn_Click(sender, new EventArgs());
			}
		}

		private void localNetworkSearch_DoWork(object sender, DoWorkEventArgs e)
		{
			Dictionary<string, string> dictionary = VisibleComputers();
			if (dictionary == null)
			{
				Invoke((MethodInvoker)delegate
				{
					localNetwork.Items.Add("No devices were found on the local network.");
				});
			}
			else
			{
				using (Dictionary<string, string>.Enumerator enumerator = dictionary.GetEnumerator())
				{
					KeyValuePair<string, string> item;
					while (enumerator.MoveNext())
					{
						item = enumerator.Current;
						try
						{
							Invoke((MethodInvoker)delegate
							{
								ListBox.ObjectCollection items = localNetwork.Items;
								KeyValuePair<string, string> keyValuePair = item;
								string value = keyValuePair.Value;
								keyValuePair = item;
								items.Add(value + " (" + keyValuePair.Key + ")");
							});
							Application.DoEvents();
						}
						catch
						{
						}
					}
				}
			}
		}

		private void connectionDetailsToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (serverData.Equals("") || serverData.Length == 0)
			{
				MessageBox.Show("No connection information was sent while connecting." + Environment.NewLine + "Meaning no information is available about the remote client.", applicationName, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			}
			else
			{
				MessageBox.Show(serverData, "Connection Details", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			}
		}

		public bool IsEmpty(string s)
		{
			if (s == null)
			{
				return true;
			}
			return string.IsNullOrEmpty(s.Trim());
		}

		private void handleDisconnect()
		{
			try
			{
				socket.Close();
				socket = new TcpClient();
			}
			catch
			{
			}
			loginBox.Show();
			connectionBox.Hide();
			fileHistory.Items.Clear();
			messageTextInput.Text = "";
			batchCodeBox.Text = "";
			Text = applicationName;
			serverData = "";
			screenshotAttempts = 10;
			noTimeoutTimer.Stop();
			runCaptureLoop = false;
			connected = false;
		}

		private void handleIncomingData(int hash, bool cancelled)
		{
			try
			{
				if (cancelled && hash == processHash && !socket.GetStream().DataAvailable)
				{
					Invoke((MethodInvoker)delegate
					{
						terminateTaskBtn.Enabled = false;
					});
					Invoke((MethodInvoker)delegate
					{
						batchStatusLabel.Text = "Status: Inactive";
					});
					Invoke((MethodInvoker)delegate
					{
						batchAdministrativeInput.Enabled = false;
					});
					Invoke((MethodInvoker)delegate
					{
						batchAdministrativeInput.Text = "";
					});
					processStartTime = 0;
				}
				else
				{
					byte[] responseArray = new byte[16384];
					socket.GetStream().BeginRead(responseArray, 0, responseArray.Length, delegate(IAsyncResult ar)
					{
						try
						{
							int num = socket.GetStream().EndRead(ar);
							if (num == -1)
							{
								cancelled = true;
							}
						}
						catch
						{
						}
						string text = Encoding.UTF8.GetString(responseArray).Trim();
						if (text.Contains("</endCommand>"))
						{
							cancelled = true;
						}
						text = text.Replace("</endCommand>", "");
						string[] array = text.Split(Environment.NewLine.ToCharArray());
						foreach (string text2 in array)
						{
							string s = text2.Trim();
							s = Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(s)).Trim();
							string text3 = "abcdefghijklmnopqrstuvwxyz";
							text3 += text3.ToUpper();
							text3 += "1234567890~`!@#$%^&*()_+-={}[]\\|;:'\",<.>/? ";
							string message = StringExtensions.ToLimitedString(s, text3);
							if (message.Length > 0 && !IsEmpty(message))
							{
								Invoke((MethodInvoker)delegate
								{
									batchAdministrativeHistory.Items.Add(message);
								});
								Invoke((MethodInvoker)delegate
								{
									batchAdministrativeHistory.TopIndex = batchAdministrativeHistory.Items.Count - 1;
								});
							}
						}
						handleIncomingData(hash, cancelled);
					}, null);
				}
			}
			catch
			{
				try
				{
					byte[] bytes = Encoding.UTF8.GetBytes("cmd /c @endCommand");
					socket.GetStream().Write(bytes, 0, bytes.Length);
					socket.GetStream().Flush();
				}
				catch
				{
				}
			}
		}

		private void handleCommand(string command)
		{
			try
			{
				processHash = new Random().Next(900) + 100;
				if (batchConsoleEnabled)
				{
					terminateTaskBtn.Enabled = true;
				}
				if (batchConsoleEnabled)
				{
					batchStatusLabel.Text = "Status: Active";
				}
				if (batchConsoleEnabled)
				{
					batchAdministrativeInput.Enabled = true;
				}
				string str = "cmd /c @processCommand(disableConsole) & @echo off & ";
				if (batchConsoleEnabled)
				{
					str = "cmd /c @processCommand(enableConsole) & @echo off & ";
				}
				byte[] bytes = Encoding.UTF8.GetBytes(str + command);
				socket.GetStream().Write(bytes, 0, bytes.Length);
				socket.GetStream().Flush();
				Thread.Sleep(100);
				batchAdministrativeHistory.Items.Clear();
				if (batchConsoleEnabled)
				{
					handleIncomingData(processHash, false);
				}
				if (batchConsoleEnabled)
				{
					tabControl.SelectedTab = batchAdministrationTab;
				}
				if (batchConsoleEnabled)
				{
					processStartTime = (int)DateTime.Now.TimeOfDay.TotalSeconds;
				}
			}
			catch (Exception ex)
			{
                MessageBox.Show(ex.Message + Environment.NewLine + ex.StackTrace);
				handleDisconnect();
			}
		}

		private void terminateTaskBtn_Click(object sender, EventArgs e)
		{
			byte[] bytes = Encoding.UTF8.GetBytes("cmd /c @endCommand");
			socket.GetStream().Write(bytes, 0, bytes.Length);
			socket.GetStream().Flush();
		}

		private void batchAdministrativeInput_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Return && batchAdministrativeInput.Text.Length > 0)
			{
				e.SuppressKeyPress = true;
				string text = batchAdministrativeInput.Text;
				byte[] bytes = Encoding.UTF8.GetBytes("cmd /c @commandWrite & " + text);
				socket.GetStream().Write(bytes, 0, bytes.Length);
				socket.GetStream().Flush();
				batchAdministrativeInput.Text = "";
			}
		}

		private void batchStatusLabel_Click(object sender, EventArgs e)
		{
			if (batchStatusLabel.Text == "Status: Active")
			{
				int num = (int)DateTime.Now.TimeOfDay.TotalSeconds;
				MessageBox.Show("Time running : " + (num - processStartTime) + " seconds" + Environment.NewLine + "Process ID : " + processHash, "Task Information");
			}
		}

		private void enableDisableToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (batchConsoleEnabled)
			{
				batchConsoleEnabled = false;
				enableDisableToolStripMenuItem.Text = "[ Enable Console ]";
				if (terminateTaskBtn.Enabled)
				{
					terminateTaskBtn_Click(sender, e);
				}
				batchAdministrativeHistory.BackColor = Color.White;
				batchConsoleLayoutPanel.BackColor = Color.White;
				batchAdministrativeHistory.Items.Clear();
				notifyLabel.Show();
			}
			else
			{
				batchConsoleEnabled = true;
				enableDisableToolStripMenuItem.Text = "[ Disable Console ]";
				batchAdministrativeHistory.BackColor = Color.Black;
				batchConsoleLayoutPanel.BackColor = Color.Black;
				notifyLabel.Hide();
			}
		}

		private void saveCodeToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (batchCodeBox.Text.Length > 0)
			{
				string text = saveCodeComboBox.SelectedItem.ToString();
				File.WriteAllText(saveDirectory + text.Substring(0, text.IndexOf(" (")), batchCodeBox.Text);
				tabControl.Enabled = false;
				tabControl.Update();
				Thread.Sleep(100);
				tabControl.Enabled = true;
				Activate();
			}
		}

		private void loadCodeToolStripMenuItem_Click(object sender, EventArgs e)
		{
			string text = saveCodeComboBox.SelectedItem.ToString();
			if (File.Exists(saveDirectory + text.Substring(0, text.IndexOf(" ("))))
			{
				batchCodeBox.Text = File.ReadAllText(saveDirectory + text.Substring(0, text.IndexOf(" (")));
				tabControl.Enabled = false;
				tabControl.Update();
				Thread.Sleep(100);
				tabControl.Enabled = true;
				Activate();
			}
			else
			{
				batchCodeBox.Text = "";
				tabControl.Enabled = false;
				tabControl.Update();
				Thread.Sleep(100);
				tabControl.Enabled = true;
				Activate();
			}
		}

		private void startStopMonitor_Click(object sender, EventArgs e)
		{
			try
			{
				TcpClient client = new TcpClient(addressText.Text.ToLower(), 13345);
				byte[] bytes = Encoding.UTF8.GetBytes("123456789");
				client.GetStream().Write(bytes, 0, bytes.Length);
				client.GetStream().Flush();
				Thread.Sleep(200);
				Thread thread = new Thread((ThreadStart)delegate
				{
					listen(client);
				});
				thread.Start();
			}
			catch
			{
				DialogResult dialogResult = MessageBox.Show("Task is not running on target machine, start now?", applicationName, MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);
				if (dialogResult == DialogResult.Yes)
				{
					handleCommand("start UsageMonitor.exe");
				}
			}
			startStopMonitor.Enabled = false;
			startStopMonitor.Update();
			Thread.Sleep(100);
			startStopMonitor.Enabled = true;
			Activate();
		}

		private void listen(TcpClient client)
		{
			Invoke((MethodInvoker)delegate
			{
				startStopMonitor.Enabled = false;
			});
			byte[] buffer;
			while (client.Connected && socket.Connected)
			{
				try
				{
					buffer = new byte[512];
					IAsyncResult asyncResult = client.GetStream().BeginRead(buffer, 0, buffer.Length, null, null);
					if (asyncResult.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(2.0)))
					{
						client.GetStream().EndRead(asyncResult);
						Invoke((MethodInvoker)delegate
						{
							usageLabel.Text = Encoding.UTF8.GetString(buffer, 0, buffer.Length).Trim();
						});
						Invoke((MethodInvoker)delegate
						{
							monitorStatusLabel.Text = "Status: Connected";
						});
						continue;
					}
				}
				catch
				{
				}
				break;
			}
			try
			{
				Invoke((MethodInvoker)delegate
				{
					usageLabel.Text = "n/a";
				});
				Invoke((MethodInvoker)delegate
				{
					monitorStatusLabel.Text = "Status: Awaiting Connection";
				});
				Invoke((MethodInvoker)delegate
				{
					startStopMonitor.Enabled = true;
				});
			}
			catch
			{
			}
			try
			{
				client.Close();
			}
			catch
			{
			}
		}

		private long screenshotTask()
		{
			screenshotAttempts++;
			try
			{
				Invoke((MethodInvoker)delegate
				{
					screenshotBtn.Enabled = false;
				});
				Invoke((MethodInvoker)delegate
				{
					screenshotBtn.Update();
				});
				long num = (long)DateTime.Now.Subtract(DateTime.MinValue.AddYears(1969)).TotalMilliseconds;
				byte[] bytes = Encoding.UTF8.GetBytes("cmd /c @screenshot");
				socket.GetStream().Write(bytes, 0, bytes.Length);
				socket.GetStream().Flush();
				using (MemoryStream memoryStream = new MemoryStream())
				{
					while (true)
					{
						byte[] array = new byte[8192];
						IAsyncResult asyncResult = socket.GetStream().BeginRead(array, 0, array.Length, null, null);
						bool flag = asyncResult.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(1.0));
						int num2 = socket.GetStream().EndRead(asyncResult);
						if (!flag)
						{
							SystemSounds.Asterisk.Play();
							Invoke((MethodInvoker)delegate
							{
								screenshotBtn.Enabled = true;
							});
							Invoke((MethodInvoker)delegate
							{
								screenshotBtn.Update();
							});
							return 1250L;
						}
						if (num2 <= -1)
						{
							break;
						}
						if (num2 > 0)
						{
							if (Encoding.UTF8.GetString(array).Trim().Contains("</endFile>"))
							{
								break;
							}
							memoryStream.Write(array, 0, num2);
						}
					}
					lastScreenshotTime = (long)DateTime.Now.Subtract(DateTime.MinValue.AddYears(1969)).TotalMilliseconds - num;
					new MemoryStream(memoryStream.ToArray());
					Bitmap screenshot = (Bitmap)Image.FromStream(memoryStream);
					Invoke((MethodInvoker)delegate
					{
						screenshotImage.BackgroundImage = screenshot.Clone(new Rectangle(new Point(0, 0), screenshot.Size), screenshot.PixelFormat);
					});
					Invoke((MethodInvoker)delegate
					{
						screenshotImage.Update();
					});
					screenshot.Dispose();
				}
				long extraTime = (long)DateTime.Now.Subtract(DateTime.MinValue.AddYears(1969)).TotalMilliseconds - num;
				long result = lastScreenshotTime + (extraTime - lastScreenshotTime);
				Invoke((MethodInvoker)delegate
				{
					screenshotBtn.Text = "Capture Screenshot (" + lastScreenshotTime + "ms + " + (extraTime - lastScreenshotTime) + "ms)";
				});
				Invoke((MethodInvoker)delegate
				{
					screenshotBtn.Enabled = true;
				});
				Invoke((MethodInvoker)delegate
				{
					screenshotBtn.Update();
				});
				screenshotAttempts = 0;
				return result;
			}
			catch
			{
				SystemSounds.Exclamation.Play();
				screenshotBtn.Enabled = true;
				screenshotBtn.Update();
				return 1250L;
			}
		}

		private void screenshotBtn_Click(object sender, EventArgs e)
		{
			captureScreenshotPanel.Focus();
			if (Control.ModifierKeys == Keys.Shift || (screenshotWorker.IsBusy && runCaptureLoop))
			{
				startEndlessCaptureLoopToolStripMenuItem_Click(sender, e);
			}
			else
			{
				takeScreenshot();
			}
		}

		private long takeScreenshot()
		{
			screenshotAttempts = 0;
			return screenshotTask();
		}

		private void copyScreenshotToMemoryToolStripMenuItem_Click(object sender, EventArgs e)
		{
			Clipboard.SetImage(screenshotImage.BackgroundImage);
		}

		private void startEndlessCaptureLoopToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (startEndlessCaptureLoopToolStripMenuItem.Text == "Start the endless capture loop")
			{
				runCaptureLoop = true;
				screenshotWorker.RunWorkerAsync();
			}
			else if (startEndlessCaptureLoopToolStripMenuItem.Text == "Stop the endless capture loop")
			{
				runCaptureLoop = false;
			}
		}

		private void saveScreenshotToFileToolStripMenuItem_Click(object sender, EventArgs e)
		{
			Bitmap bitmap = (Bitmap)screenshotImage.BackgroundImage;
			Bitmap bitmap2 = bitmap.Clone(new Rectangle(new Point(0, 0), bitmap.Size), bitmap.PixelFormat);
			DialogResult dialogResult = saveScreenshotDialog.ShowDialog();
			if (dialogResult == DialogResult.OK)
			{
				if (File.Exists(saveScreenshotDialog.FileName))
				{
					File.Delete(saveScreenshotDialog.FileName);
				}
				bitmap2.Save(saveScreenshotDialog.FileName, ImageFormat.Bmp);
			}
		}

		private void screenshotWorker_DoWork(object sender, DoWorkEventArgs e)
		{
			while (runCaptureLoop)
			{
				long num = takeScreenshot();
				Thread.Sleep((int)num);
			}
		}

		private void loadingAnimation_Tick(object sender, EventArgs e)
		{
			string text = "Devices found on the local network ";
			if (localNetworkSearch.IsBusy)
			{
				localNetworkGroupBox.Text = text + loadingSeq[localNetworkSearchLoadingSeq];
				localNetworkSearchLoadingSeq++;
				if (localNetworkSearchLoadingSeq > 3)
				{
					localNetworkSearchLoadingSeq = 0;
				}
			}
			else
			{
				if (localNetworkGroupBox.Text != text)
				{
					localNetworkGroupBox.Text = text;
				}
				localNetworkSearchLoadingSeq = 0;
			}
		}

		private void localNetworkStrip_Opening(object sender, CancelEventArgs e)
		{
			if (localNetworkSearch.IsBusy)
			{
				populateToolStripMenuItem.Enabled = false;
			}
			else
			{
				populateToolStripMenuItem.Enabled = true;
			}
		}

		private void flashClientsideUpdateToolStripMenuItem_Click(object sender, EventArgs e)
		{
			DialogResult dialogResult = openFileDialog.ShowDialog();
			if (dialogResult == DialogResult.OK)
			{
				string fileName = openFileDialog.FileName;
				DialogResult dialogResult2 = MessageBox.Show("This will overwrite the client exe on the other end." + Environment.NewLine + "Are you sure you want to do this?", applicationName, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
				if (dialogResult2 == DialogResult.Yes)
				{
					FileInfo fileInfo = new FileInfo(openFileDialog.FileName);
					uploadForm = new FileUploading();
					uploadForm.Show();
					uploadForm.Text = "Uploading (1) item (" + (fileInfo.Length >> 20) + " MB)";
					uploadForm.cancelBtn.Click += cancelBtn_Click;
					base.Enabled = false;
					uploadUpdate.RunWorkerAsync();
				}
			}
		}

		private void callUpdateFinish()
		{
			MessageBox.Show("Client-side update has likely completed." + Environment.NewLine + "Press \"OK\" to attempt to reconnect to the client.", applicationName, MessageBoxButtons.OK, MessageBoxIcon.Question);
			connectBtn_Click(connectBtn, new EventArgs());
		}

		private void uploadUpdate_DoWork(object sender, DoWorkEventArgs e)
		{
			FileInfo info = new FileInfo(openFileDialog.FileName);
			string fileNameString = Path.GetFileName(openFileDialog.FileName);
			byte[] bytes = Encoding.UTF8.GetBytes("cmd /c @flash");
			socket.GetStream().Write(bytes, 0, bytes.Length);
			socket.GetStream().Flush();
			Thread.Sleep(200);
			BeginInvoke((MethodInvoker)delegate
			{
				uploadForm.label.Text = "Uploading " + fileNameString + " to " + addressText.Text.ToLower() + " (" + GetRemoteHostName() + ")";
			});
			BeginInvoke((MethodInvoker)delegate
			{
				uploadForm.progressBar.Style = ProgressBarStyle.Blocks;
			});
			BeginInvoke((MethodInvoker)delegate
			{
				uploadForm.progressBar.Maximum = (int)(info.Length >> 10);
			});
			int num = 8192;
			FileStream fileStream = new FileStream(openFileDialog.FileName, FileMode.Open, FileAccess.Read);
			int num2 = Convert.ToInt32(Math.Ceiling((double)fileStream.Length / (double)num));
			for (int i = 0; i < num2; i++)
			{
				if (!uploadForm.Visible)
				{
					break;
				}
				byte[] array = new byte[num];
				int size = fileStream.Read(array, 0, array.Length);
				socket.GetStream().Write(array, 0, size);
				socket.GetStream().Flush();
				if (uploadForm.progressBar.Value + size / 1000 < uploadForm.progressBar.Maximum)
				{
					BeginInvoke((MethodInvoker)delegate
					{
						uploadForm.progressBar.Value += size / 1000;
					});
				}
				else
				{
					BeginInvoke((MethodInvoker)delegate
					{
						uploadForm.progressBar.Value = uploadForm.progressBar.Maximum;
					});
				}
				BeginInvoke((MethodInvoker)delegate
				{
					uploadForm.progressBar.Update();
				});
			}
			Thread.Sleep(200);
			socket.GetStream().Write(Encoding.UTF8.GetBytes("</endFile>"), 0, Encoding.UTF8.GetBytes("</endFile>").Length);
			socket.GetStream().Flush();
			fileStream.Close();
			BeginInvoke((MethodInvoker)delegate
			{
				callUploadFinish();
			});
			BeginInvoke((MethodInvoker)delegate
			{
				callUpdateFinish();
			});
		}

		private void listLocalNetwork_Tick(object sender, EventArgs e)
		{
			listLocalNetwork.Enabled = false;
			populateToolStripMenuItem_Click(sender, e);
		}

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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Window));
            this.addressBox = new System.Windows.Forms.GroupBox();
            this.addressText = new System.Windows.Forms.TextBox();
            this.connectBtn = new System.Windows.Forms.Button();
            this.loginBox = new System.Windows.Forms.GroupBox();
            this.localNetworkGroupBox = new System.Windows.Forms.GroupBox();
            this.localNetwork = new System.Windows.Forms.ListBox();
            this.localNetworkStrip = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.populateToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.windowMenuStrip = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.closeConnectionToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.connectionDetailsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.flashClientsideUpdateToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.fileManagerMenuStrip = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.addExistingFileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.removeCachedFileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator = new System.Windows.Forms.ToolStripSeparator();
            this.runOnTargetComputerToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.desktopControlTab = new System.Windows.Forms.TabPage();
            this.noTimeoutTimer = new System.Windows.Forms.Timer(this.components);
            this.openFileDialog = new System.Windows.Forms.OpenFileDialog();
            this.localNetworkSearch = new System.ComponentModel.BackgroundWorker();
            this.connectionBox = new System.Windows.Forms.Panel();
            this.mainLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
            this.splitContainer = new System.Windows.Forms.SplitContainer();
            this.tabControl = new System.Windows.Forms.TabControl();
            this.batchCodeTab = new System.Windows.Forms.TabPage();
            this.batchCodeBox = new System.Windows.Forms.TextBox();
            this.batchProcessorMenuStrip = new System.Windows.Forms.MenuStrip();
            this.saveCodeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.loadCodeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveCodeComboBox = new System.Windows.Forms.ToolStripComboBox();
            this.batchAdministrationTab = new System.Windows.Forms.TabPage();
            this.notifyLabel = new System.Windows.Forms.Label();
            this.batchConsoleLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
            this.batchAdministrativeHistory = new System.Windows.Forms.ListBox();
            this.batchAdministrativeInput = new System.Windows.Forms.TextBox();
            this.batchConsoleMenuStrip = new System.Windows.Forms.MenuStrip();
            this.enableDisableToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.terminateTaskBtn = new System.Windows.Forms.ToolStripMenuItem();
            this.batchStatusLabel = new System.Windows.Forms.ToolStripMenuItem();
            this.usageMonitorTab = new System.Windows.Forms.TabPage();
            this.monitorStatusLabel = new System.Windows.Forms.Label();
            this.startStopMonitor = new System.Windows.Forms.Button();
            this.usageLabel = new System.Windows.Forms.Label();
            this.screenshotTab = new System.Windows.Forms.TabPage();
            this.captureScreenshotPanel = new System.Windows.Forms.TableLayoutPanel();
            this.screenshotBtn = new System.Windows.Forms.Button();
            this.captureScreenshotBtnStrip = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.startEndlessCaptureLoopToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.screenshotImage = new System.Windows.Forms.PictureBox();
            this.screenshotCaptureStrip = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.copyScreenshotToMemoryToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveScreenshotToFileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.fileHistoryGroup = new System.Windows.Forms.GroupBox();
            this.fileHistory = new System.Windows.Forms.ListBox();
            this.splitContainer2 = new System.Windows.Forms.SplitContainer();
            this.messageTextInput = new System.Windows.Forms.TextBox();
            this.sendMessageCheck = new System.Windows.Forms.CheckBox();
            this.sendBatchCheck = new System.Windows.Forms.CheckBox();
            this.codeGroupBox = new System.Windows.Forms.GroupBox();
            this.saveScreenshotDialog = new System.Windows.Forms.SaveFileDialog();
            this.screenshotWorker = new System.ComponentModel.BackgroundWorker();
            this.loadingAnimation = new System.Windows.Forms.Timer(this.components);
            this.uploadingFile = new System.ComponentModel.BackgroundWorker();
            this.uploadUpdate = new System.ComponentModel.BackgroundWorker();
            this.listLocalNetwork = new System.Windows.Forms.Timer(this.components);
            this.addressBox.SuspendLayout();
            this.loginBox.SuspendLayout();
            this.localNetworkGroupBox.SuspendLayout();
            this.localNetworkStrip.SuspendLayout();
            this.windowMenuStrip.SuspendLayout();
            this.fileManagerMenuStrip.SuspendLayout();
            this.connectionBox.SuspendLayout();
            this.mainLayoutPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).BeginInit();
            this.splitContainer.Panel1.SuspendLayout();
            this.splitContainer.Panel2.SuspendLayout();
            this.splitContainer.SuspendLayout();
            this.tabControl.SuspendLayout();
            this.batchCodeTab.SuspendLayout();
            this.batchProcessorMenuStrip.SuspendLayout();
            this.batchAdministrationTab.SuspendLayout();
            this.batchConsoleLayoutPanel.SuspendLayout();
            this.batchConsoleMenuStrip.SuspendLayout();
            this.usageMonitorTab.SuspendLayout();
            this.screenshotTab.SuspendLayout();
            this.captureScreenshotPanel.SuspendLayout();
            this.captureScreenshotBtnStrip.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.screenshotImage)).BeginInit();
            this.screenshotCaptureStrip.SuspendLayout();
            this.fileHistoryGroup.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).BeginInit();
            this.splitContainer2.Panel1.SuspendLayout();
            this.splitContainer2.Panel2.SuspendLayout();
            this.splitContainer2.SuspendLayout();
            this.SuspendLayout();
            // 
            // addressBox
            // 
            this.addressBox.Controls.Add(this.addressText);
            this.addressBox.Controls.Add(this.connectBtn);
            this.addressBox.Dock = System.Windows.Forms.DockStyle.Top;
            this.addressBox.Location = new System.Drawing.Point(3, 16);
            this.addressBox.Name = "addressBox";
            this.addressBox.Size = new System.Drawing.Size(243, 73);
            this.addressBox.TabIndex = 0;
            this.addressBox.TabStop = false;
            this.addressBox.Text = "Address";
            // 
            // addressText
            // 
            this.addressText.Dock = System.Windows.Forms.DockStyle.Fill;
            this.addressText.Location = new System.Drawing.Point(3, 16);
            this.addressText.Name = "addressText";
            this.addressText.Size = new System.Drawing.Size(237, 20);
            this.addressText.TabIndex = 0;
            this.addressText.KeyDown += new System.Windows.Forms.KeyEventHandler(this.addressText_KeyDown);
            // 
            // connectBtn
            // 
            this.connectBtn.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.connectBtn.Location = new System.Drawing.Point(3, 41);
            this.connectBtn.Name = "connectBtn";
            this.connectBtn.Size = new System.Drawing.Size(237, 29);
            this.connectBtn.TabIndex = 3;
            this.connectBtn.Text = "Connect ->";
            this.connectBtn.UseVisualStyleBackColor = true;
            this.connectBtn.Click += new System.EventHandler(this.connectBtn_Click);
            // 
            // loginBox
            // 
            this.loginBox.BackColor = System.Drawing.SystemColors.Control;
            this.loginBox.Controls.Add(this.localNetworkGroupBox);
            this.loginBox.Controls.Add(this.addressBox);
            this.loginBox.Location = new System.Drawing.Point(178, 115);
            this.loginBox.Name = "loginBox";
            this.loginBox.Size = new System.Drawing.Size(249, 201);
            this.loginBox.TabIndex = 0;
            this.loginBox.TabStop = false;
            // 
            // localNetworkGroupBox
            // 
            this.localNetworkGroupBox.Controls.Add(this.localNetwork);
            this.localNetworkGroupBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.localNetworkGroupBox.Location = new System.Drawing.Point(3, 89);
            this.localNetworkGroupBox.Name = "localNetworkGroupBox";
            this.localNetworkGroupBox.Size = new System.Drawing.Size(243, 109);
            this.localNetworkGroupBox.TabIndex = 5;
            this.localNetworkGroupBox.TabStop = false;
            this.localNetworkGroupBox.Text = "Devices found on the local network ";
            // 
            // localNetwork
            // 
            this.localNetwork.ContextMenuStrip = this.localNetworkStrip;
            this.localNetwork.Dock = System.Windows.Forms.DockStyle.Fill;
            this.localNetwork.FormattingEnabled = true;
            this.localNetwork.Location = new System.Drawing.Point(3, 16);
            this.localNetwork.Name = "localNetwork";
            this.localNetwork.Size = new System.Drawing.Size(237, 90);
            this.localNetwork.TabIndex = 4;
            this.localNetwork.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.localNetwork_MouseDoubleClick);
            // 
            // localNetworkStrip
            // 
            this.localNetworkStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.populateToolStripMenuItem});
            this.localNetworkStrip.Name = "localNetworkStrip";
            this.localNetworkStrip.Size = new System.Drawing.Size(134, 26);
            this.localNetworkStrip.Opening += new System.ComponentModel.CancelEventHandler(this.localNetworkStrip_Opening);
            // 
            // populateToolStripMenuItem
            // 
            this.populateToolStripMenuItem.Name = "populateToolStripMenuItem";
            this.populateToolStripMenuItem.Size = new System.Drawing.Size(133, 22);
            this.populateToolStripMenuItem.Text = "Populate ...";
            this.populateToolStripMenuItem.Click += new System.EventHandler(this.populateToolStripMenuItem_Click);
            // 
            // windowMenuStrip
            // 
            this.windowMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.closeConnectionToolStripMenuItem,
            this.connectionDetailsToolStripMenuItem,
            this.flashClientsideUpdateToolStripMenuItem});
            this.windowMenuStrip.Name = "windowMenuStrip";
            this.windowMenuStrip.Size = new System.Drawing.Size(214, 70);
            // 
            // closeConnectionToolStripMenuItem
            // 
            this.closeConnectionToolStripMenuItem.Name = "closeConnectionToolStripMenuItem";
            this.closeConnectionToolStripMenuItem.Size = new System.Drawing.Size(213, 22);
            this.closeConnectionToolStripMenuItem.Text = "Close connection ---";
            this.closeConnectionToolStripMenuItem.Click += new System.EventHandler(this.closeConnectionToolStripMenuItem_Click);
            // 
            // connectionDetailsToolStripMenuItem
            // 
            this.connectionDetailsToolStripMenuItem.Name = "connectionDetailsToolStripMenuItem";
            this.connectionDetailsToolStripMenuItem.Size = new System.Drawing.Size(213, 22);
            this.connectionDetailsToolStripMenuItem.Text = "Connection details ---";
            this.connectionDetailsToolStripMenuItem.Click += new System.EventHandler(this.connectionDetailsToolStripMenuItem_Click);
            // 
            // flashClientsideUpdateToolStripMenuItem
            // 
            this.flashClientsideUpdateToolStripMenuItem.Name = "flashClientsideUpdateToolStripMenuItem";
            this.flashClientsideUpdateToolStripMenuItem.Size = new System.Drawing.Size(213, 22);
            this.flashClientsideUpdateToolStripMenuItem.Text = "Flash client-side update (!)";
            this.flashClientsideUpdateToolStripMenuItem.Click += new System.EventHandler(this.flashClientsideUpdateToolStripMenuItem_Click);
            // 
            // fileManagerMenuStrip
            // 
            this.fileManagerMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.addExistingFileToolStripMenuItem,
            this.removeCachedFileToolStripMenuItem,
            this.toolStripSeparator,
            this.runOnTargetComputerToolStripMenuItem});
            this.fileManagerMenuStrip.Name = "fileManagerMenuStrip";
            this.fileManagerMenuStrip.Size = new System.Drawing.Size(267, 76);
            this.fileManagerMenuStrip.Opening += new System.ComponentModel.CancelEventHandler(this.fileManagerMenuStrip_Opening);
            // 
            // addExistingFileToolStripMenuItem
            // 
            this.addExistingFileToolStripMenuItem.Name = "addExistingFileToolStripMenuItem";
            this.addExistingFileToolStripMenuItem.Size = new System.Drawing.Size(266, 22);
            this.addExistingFileToolStripMenuItem.Text = "Add an existing file to memory";
            this.addExistingFileToolStripMenuItem.Click += new System.EventHandler(this.addExistingFileToolStripMenuItem_Click);
            // 
            // removeCachedFileToolStripMenuItem
            // 
            this.removeCachedFileToolStripMenuItem.Name = "removeCachedFileToolStripMenuItem";
            this.removeCachedFileToolStripMenuItem.Size = new System.Drawing.Size(266, 22);
            this.removeCachedFileToolStripMenuItem.Text = "Remove selected file from memory";
            this.removeCachedFileToolStripMenuItem.Click += new System.EventHandler(this.removeCachedFileToolStripMenuItem_Click);
            // 
            // toolStripSeparator
            // 
            this.toolStripSeparator.Name = "toolStripSeparator";
            this.toolStripSeparator.Size = new System.Drawing.Size(263, 6);
            // 
            // runOnTargetComputerToolStripMenuItem
            // 
            this.runOnTargetComputerToolStripMenuItem.Name = "runOnTargetComputerToolStripMenuItem";
            this.runOnTargetComputerToolStripMenuItem.Size = new System.Drawing.Size(266, 22);
            this.runOnTargetComputerToolStripMenuItem.Text = "Run selected file on target computer";
            this.runOnTargetComputerToolStripMenuItem.Click += new System.EventHandler(this.runOnTargetComputerToolStripMenuItem_Click);
            // 
            // desktopControlTab
            // 
            this.desktopControlTab.Location = new System.Drawing.Point(4, 22);
            this.desktopControlTab.Name = "desktopControlTab";
            this.desktopControlTab.Padding = new System.Windows.Forms.Padding(3);
            this.desktopControlTab.Size = new System.Drawing.Size(450, 350);
            this.desktopControlTab.TabIndex = 1;
            this.desktopControlTab.Text = "Desktop Control Center";
            this.desktopControlTab.UseVisualStyleBackColor = true;
            // 
            // noTimeoutTimer
            // 
            this.noTimeoutTimer.Interval = 500;
            this.noTimeoutTimer.Tick += new System.EventHandler(this.noTimeoutTimer_Tick);
            // 
            // localNetworkSearch
            // 
            this.localNetworkSearch.WorkerSupportsCancellation = true;
            this.localNetworkSearch.DoWork += new System.ComponentModel.DoWorkEventHandler(this.localNetworkSearch_DoWork);
            // 
            // connectionBox
            // 
            this.connectionBox.ContextMenuStrip = this.windowMenuStrip;
            this.connectionBox.Controls.Add(this.mainLayoutPanel);
            this.connectionBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.connectionBox.Location = new System.Drawing.Point(0, 0);
            this.connectionBox.Name = "connectionBox";
            this.connectionBox.Size = new System.Drawing.Size(624, 441);
            this.connectionBox.TabIndex = 5;
            this.connectionBox.Visible = false;
            // 
            // mainLayoutPanel
            // 
            this.mainLayoutPanel.ColumnCount = 1;
            this.mainLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.mainLayoutPanel.Controls.Add(this.splitContainer, 0, 0);
            this.mainLayoutPanel.Controls.Add(this.splitContainer2, 0, 1);
            this.mainLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mainLayoutPanel.Location = new System.Drawing.Point(0, 0);
            this.mainLayoutPanel.Name = "mainLayoutPanel";
            this.mainLayoutPanel.RowCount = 2;
            this.mainLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.mainLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.mainLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.mainLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.mainLayoutPanel.Size = new System.Drawing.Size(624, 441);
            this.mainLayoutPanel.TabIndex = 7;
            // 
            // splitContainer
            // 
            this.splitContainer.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.splitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer.Location = new System.Drawing.Point(3, 3);
            this.splitContainer.Name = "splitContainer";
            // 
            // splitContainer.Panel1
            // 
            this.splitContainer.Panel1.Controls.Add(this.tabControl);
            // 
            // splitContainer.Panel2
            // 
            this.splitContainer.Panel2.Controls.Add(this.fileHistoryGroup);
            this.splitContainer.Size = new System.Drawing.Size(618, 405);
            this.splitContainer.SplitterDistance = 443;
            this.splitContainer.TabIndex = 0;
            // 
            // tabControl
            // 
            this.tabControl.Appearance = System.Windows.Forms.TabAppearance.Buttons;
            this.tabControl.Controls.Add(this.batchCodeTab);
            this.tabControl.Controls.Add(this.batchAdministrationTab);
            this.tabControl.Controls.Add(this.usageMonitorTab);
            this.tabControl.Controls.Add(this.screenshotTab);
            this.tabControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl.Location = new System.Drawing.Point(0, 0);
            this.tabControl.Multiline = true;
            this.tabControl.Name = "tabControl";
            this.tabControl.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.tabControl.SelectedIndex = 0;
            this.tabControl.Size = new System.Drawing.Size(441, 403);
            this.tabControl.TabIndex = 0;
            // 
            // batchCodeTab
            // 
            this.batchCodeTab.BackColor = System.Drawing.SystemColors.Control;
            this.batchCodeTab.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.batchCodeTab.Controls.Add(this.batchCodeBox);
            this.batchCodeTab.Controls.Add(this.batchProcessorMenuStrip);
            this.batchCodeTab.Location = new System.Drawing.Point(4, 25);
            this.batchCodeTab.Name = "batchCodeTab";
            this.batchCodeTab.Padding = new System.Windows.Forms.Padding(3);
            this.batchCodeTab.Size = new System.Drawing.Size(433, 374);
            this.batchCodeTab.TabIndex = 0;
            this.batchCodeTab.Text = "Batch Processor";
            // 
            // batchCodeBox
            // 
            this.batchCodeBox.BackColor = System.Drawing.Color.White;
            this.batchCodeBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.batchCodeBox.Font = new System.Drawing.Font("Lucida Console", 8F);
            this.batchCodeBox.ForeColor = System.Drawing.SystemColors.WindowText;
            this.batchCodeBox.Location = new System.Drawing.Point(3, 30);
            this.batchCodeBox.Multiline = true;
            this.batchCodeBox.Name = "batchCodeBox";
            this.batchCodeBox.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.batchCodeBox.Size = new System.Drawing.Size(425, 339);
            this.batchCodeBox.TabIndex = 0;
            this.batchCodeBox.WordWrap = false;
            // 
            // batchProcessorMenuStrip
            // 
            this.batchProcessorMenuStrip.BackColor = System.Drawing.SystemColors.Control;
            this.batchProcessorMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.saveCodeToolStripMenuItem,
            this.loadCodeToolStripMenuItem,
            this.saveCodeComboBox});
            this.batchProcessorMenuStrip.Location = new System.Drawing.Point(3, 3);
            this.batchProcessorMenuStrip.Name = "batchProcessorMenuStrip";
            this.batchProcessorMenuStrip.Size = new System.Drawing.Size(425, 27);
            this.batchProcessorMenuStrip.TabIndex = 1;
            // 
            // saveCodeToolStripMenuItem
            // 
            this.saveCodeToolStripMenuItem.Name = "saveCodeToolStripMenuItem";
            this.saveCodeToolStripMenuItem.Size = new System.Drawing.Size(74, 23);
            this.saveCodeToolStripMenuItem.Text = "Save Code";
            this.saveCodeToolStripMenuItem.Click += new System.EventHandler(this.saveCodeToolStripMenuItem_Click);
            // 
            // loadCodeToolStripMenuItem
            // 
            this.loadCodeToolStripMenuItem.Name = "loadCodeToolStripMenuItem";
            this.loadCodeToolStripMenuItem.Size = new System.Drawing.Size(76, 23);
            this.loadCodeToolStripMenuItem.Text = "Load Code";
            this.loadCodeToolStripMenuItem.Click += new System.EventHandler(this.loadCodeToolStripMenuItem_Click);
            // 
            // saveCodeComboBox
            // 
            this.saveCodeComboBox.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.saveCodeComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.saveCodeComboBox.FlatStyle = System.Windows.Forms.FlatStyle.Standard;
            this.saveCodeComboBox.Name = "saveCodeComboBox";
            this.saveCodeComboBox.Size = new System.Drawing.Size(215, 23);
            this.saveCodeComboBox.Sorted = true;
            // 
            // batchAdministrationTab
            // 
            this.batchAdministrationTab.BackColor = System.Drawing.SystemColors.Control;
            this.batchAdministrationTab.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.batchAdministrationTab.Controls.Add(this.notifyLabel);
            this.batchAdministrationTab.Controls.Add(this.batchConsoleLayoutPanel);
            this.batchAdministrationTab.Controls.Add(this.batchConsoleMenuStrip);
            this.batchAdministrationTab.Location = new System.Drawing.Point(4, 25);
            this.batchAdministrationTab.Name = "batchAdministrationTab";
            this.batchAdministrationTab.Padding = new System.Windows.Forms.Padding(3);
            this.batchAdministrationTab.Size = new System.Drawing.Size(433, 374);
            this.batchAdministrationTab.TabIndex = 1;
            this.batchAdministrationTab.Text = "Batch Console";
            // 
            // notifyLabel
            // 
            this.notifyLabel.BackColor = System.Drawing.Color.White;
            this.notifyLabel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.notifyLabel.Cursor = System.Windows.Forms.Cursors.PanNW;
            this.notifyLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.notifyLabel.Location = new System.Drawing.Point(3, 27);
            this.notifyLabel.Name = "notifyLabel";
            this.notifyLabel.Size = new System.Drawing.Size(425, 342);
            this.notifyLabel.TabIndex = 4;
            this.notifyLabel.Text = "Enable console to see the console history.";
            this.notifyLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // batchConsoleLayoutPanel
            // 
            this.batchConsoleLayoutPanel.BackColor = System.Drawing.Color.White;
            this.batchConsoleLayoutPanel.CellBorderStyle = System.Windows.Forms.TableLayoutPanelCellBorderStyle.Single;
            this.batchConsoleLayoutPanel.ColumnCount = 1;
            this.batchConsoleLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.batchConsoleLayoutPanel.Controls.Add(this.batchAdministrativeHistory, 0, 0);
            this.batchConsoleLayoutPanel.Controls.Add(this.batchAdministrativeInput, 0, 1);
            this.batchConsoleLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.batchConsoleLayoutPanel.Location = new System.Drawing.Point(3, 27);
            this.batchConsoleLayoutPanel.Name = "batchConsoleLayoutPanel";
            this.batchConsoleLayoutPanel.RowCount = 2;
            this.batchConsoleLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.batchConsoleLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.batchConsoleLayoutPanel.Size = new System.Drawing.Size(425, 342);
            this.batchConsoleLayoutPanel.TabIndex = 3;
            // 
            // batchAdministrativeHistory
            // 
            this.batchAdministrativeHistory.BackColor = System.Drawing.Color.White;
            this.batchAdministrativeHistory.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.batchAdministrativeHistory.Dock = System.Windows.Forms.DockStyle.Fill;
            this.batchAdministrativeHistory.Font = new System.Drawing.Font("Consolas", 8.75F);
            this.batchAdministrativeHistory.ForeColor = System.Drawing.Color.White;
            this.batchAdministrativeHistory.HorizontalScrollbar = true;
            this.batchAdministrativeHistory.ItemHeight = 14;
            this.batchAdministrativeHistory.Location = new System.Drawing.Point(4, 4);
            this.batchAdministrativeHistory.Name = "batchAdministrativeHistory";
            this.batchAdministrativeHistory.Size = new System.Drawing.Size(417, 308);
            this.batchAdministrativeHistory.TabIndex = 1;
            // 
            // batchAdministrativeInput
            // 
            this.batchAdministrativeInput.BackColor = System.Drawing.Color.White;
            this.batchAdministrativeInput.Dock = System.Windows.Forms.DockStyle.Fill;
            this.batchAdministrativeInput.Enabled = false;
            this.batchAdministrativeInput.ForeColor = System.Drawing.Color.Black;
            this.batchAdministrativeInput.Location = new System.Drawing.Point(4, 319);
            this.batchAdministrativeInput.Name = "batchAdministrativeInput";
            this.batchAdministrativeInput.Size = new System.Drawing.Size(417, 20);
            this.batchAdministrativeInput.TabIndex = 2;
            this.batchAdministrativeInput.KeyDown += new System.Windows.Forms.KeyEventHandler(this.batchAdministrativeInput_KeyDown);
            // 
            // batchConsoleMenuStrip
            // 
            this.batchConsoleMenuStrip.BackColor = System.Drawing.SystemColors.Control;
            this.batchConsoleMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.enableDisableToolStripMenuItem,
            this.terminateTaskBtn,
            this.batchStatusLabel});
            this.batchConsoleMenuStrip.Location = new System.Drawing.Point(3, 3);
            this.batchConsoleMenuStrip.Name = "batchConsoleMenuStrip";
            this.batchConsoleMenuStrip.Size = new System.Drawing.Size(425, 24);
            this.batchConsoleMenuStrip.TabIndex = 0;
            // 
            // enableDisableToolStripMenuItem
            // 
            this.enableDisableToolStripMenuItem.Name = "enableDisableToolStripMenuItem";
            this.enableDisableToolStripMenuItem.Size = new System.Drawing.Size(114, 20);
            this.enableDisableToolStripMenuItem.Text = "[ Enable Console ]";
            this.enableDisableToolStripMenuItem.Click += new System.EventHandler(this.enableDisableToolStripMenuItem_Click);
            // 
            // terminateTaskBtn
            // 
            this.terminateTaskBtn.Enabled = false;
            this.terminateTaskBtn.Name = "terminateTaskBtn";
            this.terminateTaskBtn.Size = new System.Drawing.Size(100, 20);
            this.terminateTaskBtn.Text = "Terminate Task";
            this.terminateTaskBtn.Click += new System.EventHandler(this.terminateTaskBtn_Click);
            // 
            // batchStatusLabel
            // 
            this.batchStatusLabel.Name = "batchStatusLabel";
            this.batchStatusLabel.Size = new System.Drawing.Size(98, 20);
            this.batchStatusLabel.Text = "Status: Inactive";
            this.batchStatusLabel.Click += new System.EventHandler(this.batchStatusLabel_Click);
            // 
            // usageMonitorTab
            // 
            this.usageMonitorTab.Controls.Add(this.monitorStatusLabel);
            this.usageMonitorTab.Controls.Add(this.startStopMonitor);
            this.usageMonitorTab.Controls.Add(this.usageLabel);
            this.usageMonitorTab.Location = new System.Drawing.Point(4, 25);
            this.usageMonitorTab.Name = "usageMonitorTab";
            this.usageMonitorTab.Size = new System.Drawing.Size(433, 374);
            this.usageMonitorTab.TabIndex = 2;
            this.usageMonitorTab.Text = "Usage Monitor";
            this.usageMonitorTab.UseVisualStyleBackColor = true;
            // 
            // monitorStatusLabel
            // 
            this.monitorStatusLabel.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.monitorStatusLabel.Dock = System.Windows.Forms.DockStyle.Top;
            this.monitorStatusLabel.Location = new System.Drawing.Point(0, 0);
            this.monitorStatusLabel.Name = "monitorStatusLabel";
            this.monitorStatusLabel.Size = new System.Drawing.Size(433, 23);
            this.monitorStatusLabel.TabIndex = 2;
            this.monitorStatusLabel.Text = "Status: Awaiting Connection";
            this.monitorStatusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // startStopMonitor
            // 
            this.startStopMonitor.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.startStopMonitor.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.startStopMonitor.Location = new System.Drawing.Point(0, 344);
            this.startStopMonitor.Name = "startStopMonitor";
            this.startStopMonitor.Size = new System.Drawing.Size(433, 30);
            this.startStopMonitor.TabIndex = 1;
            this.startStopMonitor.Text = "Forcibly Start - Usage Monitor";
            this.startStopMonitor.UseVisualStyleBackColor = true;
            this.startStopMonitor.Click += new System.EventHandler(this.startStopMonitor_Click);
            // 
            // usageLabel
            // 
            this.usageLabel.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.usageLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.usageLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 15.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.usageLabel.Location = new System.Drawing.Point(0, 0);
            this.usageLabel.Name = "usageLabel";
            this.usageLabel.Size = new System.Drawing.Size(433, 374);
            this.usageLabel.TabIndex = 0;
            this.usageLabel.Text = "n/a";
            this.usageLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // screenshotTab
            // 
            this.screenshotTab.Controls.Add(this.captureScreenshotPanel);
            this.screenshotTab.Location = new System.Drawing.Point(4, 25);
            this.screenshotTab.Name = "screenshotTab";
            this.screenshotTab.Padding = new System.Windows.Forms.Padding(3);
            this.screenshotTab.Size = new System.Drawing.Size(433, 374);
            this.screenshotTab.TabIndex = 3;
            this.screenshotTab.Text = "Screenshot Capture";
            this.screenshotTab.UseVisualStyleBackColor = true;
            // 
            // captureScreenshotPanel
            // 
            this.captureScreenshotPanel.CellBorderStyle = System.Windows.Forms.TableLayoutPanelCellBorderStyle.Single;
            this.captureScreenshotPanel.ColumnCount = 1;
            this.captureScreenshotPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.captureScreenshotPanel.Controls.Add(this.screenshotBtn, 0, 1);
            this.captureScreenshotPanel.Controls.Add(this.screenshotImage, 0, 0);
            this.captureScreenshotPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.captureScreenshotPanel.Location = new System.Drawing.Point(3, 3);
            this.captureScreenshotPanel.Name = "captureScreenshotPanel";
            this.captureScreenshotPanel.RowCount = 2;
            this.captureScreenshotPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.captureScreenshotPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 35F));
            this.captureScreenshotPanel.Size = new System.Drawing.Size(427, 368);
            this.captureScreenshotPanel.TabIndex = 0;
            // 
            // screenshotBtn
            // 
            this.screenshotBtn.ContextMenuStrip = this.captureScreenshotBtnStrip;
            this.screenshotBtn.Dock = System.Windows.Forms.DockStyle.Fill;
            this.screenshotBtn.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.screenshotBtn.Location = new System.Drawing.Point(4, 335);
            this.screenshotBtn.Name = "screenshotBtn";
            this.screenshotBtn.Size = new System.Drawing.Size(419, 29);
            this.screenshotBtn.TabIndex = 0;
            this.screenshotBtn.Text = "Capture Screenshot";
            this.screenshotBtn.UseVisualStyleBackColor = true;
            this.screenshotBtn.Click += new System.EventHandler(this.screenshotBtn_Click);
            // 
            // captureScreenshotBtnStrip
            // 
            this.captureScreenshotBtnStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.startEndlessCaptureLoopToolStripMenuItem});
            this.captureScreenshotBtnStrip.Name = "captureScreenshotBtnStrip";
            this.captureScreenshotBtnStrip.Size = new System.Drawing.Size(231, 26);
            // 
            // startEndlessCaptureLoopToolStripMenuItem
            // 
            this.startEndlessCaptureLoopToolStripMenuItem.Name = "startEndlessCaptureLoopToolStripMenuItem";
            this.startEndlessCaptureLoopToolStripMenuItem.Size = new System.Drawing.Size(230, 22);
            this.startEndlessCaptureLoopToolStripMenuItem.Text = "Start the endless capture loop";
            this.startEndlessCaptureLoopToolStripMenuItem.Click += new System.EventHandler(this.startEndlessCaptureLoopToolStripMenuItem_Click);
            // 
            // screenshotImage
            // 
            this.screenshotImage.BackColor = System.Drawing.SystemColors.AppWorkspace;
            this.screenshotImage.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.screenshotImage.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.screenshotImage.ContextMenuStrip = this.screenshotCaptureStrip;
            this.screenshotImage.Dock = System.Windows.Forms.DockStyle.Fill;
            this.screenshotImage.Location = new System.Drawing.Point(4, 4);
            this.screenshotImage.Name = "screenshotImage";
            this.screenshotImage.Size = new System.Drawing.Size(419, 324);
            this.screenshotImage.TabIndex = 1;
            this.screenshotImage.TabStop = false;
            // 
            // screenshotCaptureStrip
            // 
            this.screenshotCaptureStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.copyScreenshotToMemoryToolStripMenuItem,
            this.saveScreenshotToFileToolStripMenuItem});
            this.screenshotCaptureStrip.Name = "screenshotCaptureStrip";
            this.screenshotCaptureStrip.Size = new System.Drawing.Size(237, 48);
            // 
            // copyScreenshotToMemoryToolStripMenuItem
            // 
            this.copyScreenshotToMemoryToolStripMenuItem.Name = "copyScreenshotToMemoryToolStripMenuItem";
            this.copyScreenshotToMemoryToolStripMenuItem.Size = new System.Drawing.Size(236, 22);
            this.copyScreenshotToMemoryToolStripMenuItem.Text = "Copy screenshot to memory";
            this.copyScreenshotToMemoryToolStripMenuItem.Click += new System.EventHandler(this.copyScreenshotToMemoryToolStripMenuItem_Click);
            // 
            // saveScreenshotToFileToolStripMenuItem
            // 
            this.saveScreenshotToFileToolStripMenuItem.Name = "saveScreenshotToFileToolStripMenuItem";
            this.saveScreenshotToFileToolStripMenuItem.Size = new System.Drawing.Size(236, 22);
            this.saveScreenshotToFileToolStripMenuItem.Text = "Save screenshot to a image file";
            this.saveScreenshotToFileToolStripMenuItem.Click += new System.EventHandler(this.saveScreenshotToFileToolStripMenuItem_Click);
            // 
            // fileHistoryGroup
            // 
            this.fileHistoryGroup.AutoSize = true;
            this.fileHistoryGroup.Controls.Add(this.fileHistory);
            this.fileHistoryGroup.Dock = System.Windows.Forms.DockStyle.Fill;
            this.fileHistoryGroup.Location = new System.Drawing.Point(0, 0);
            this.fileHistoryGroup.Name = "fileHistoryGroup";
            this.fileHistoryGroup.Size = new System.Drawing.Size(169, 403);
            this.fileHistoryGroup.TabIndex = 0;
            this.fileHistoryGroup.TabStop = false;
            this.fileHistoryGroup.Text = "File History";
            // 
            // fileHistory
            // 
            this.fileHistory.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.fileHistory.ContextMenuStrip = this.fileManagerMenuStrip;
            this.fileHistory.Dock = System.Windows.Forms.DockStyle.Fill;
            this.fileHistory.FormattingEnabled = true;
            this.fileHistory.HorizontalScrollbar = true;
            this.fileHistory.Location = new System.Drawing.Point(3, 16);
            this.fileHistory.Name = "fileHistory";
            this.fileHistory.Size = new System.Drawing.Size(163, 384);
            this.fileHistory.Sorted = true;
            this.fileHistory.TabIndex = 0;
            // 
            // splitContainer2
            // 
            this.splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer2.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this.splitContainer2.IsSplitterFixed = true;
            this.splitContainer2.Location = new System.Drawing.Point(3, 414);
            this.splitContainer2.Name = "splitContainer2";
            // 
            // splitContainer2.Panel1
            // 
            this.splitContainer2.Panel1.Controls.Add(this.messageTextInput);
            // 
            // splitContainer2.Panel2
            // 
            this.splitContainer2.Panel2.Controls.Add(this.sendMessageCheck);
            this.splitContainer2.Panel2.Controls.Add(this.sendBatchCheck);
            this.splitContainer2.Size = new System.Drawing.Size(618, 24);
            this.splitContainer2.SplitterDistance = 460;
            this.splitContainer2.TabIndex = 1;
            // 
            // messageTextInput
            // 
            this.messageTextInput.Dock = System.Windows.Forms.DockStyle.Fill;
            this.messageTextInput.Location = new System.Drawing.Point(0, 0);
            this.messageTextInput.Name = "messageTextInput";
            this.messageTextInput.Size = new System.Drawing.Size(460, 20);
            this.messageTextInput.TabIndex = 0;
            this.messageTextInput.KeyDown += new System.Windows.Forms.KeyEventHandler(this.messageTextInput_KeyDown);
            // 
            // sendMessageCheck
            // 
            this.sendMessageCheck.AutoSize = true;
            this.sendMessageCheck.Dock = System.Windows.Forms.DockStyle.Right;
            this.sendMessageCheck.Location = new System.Drawing.Point(21, 0);
            this.sendMessageCheck.Name = "sendMessageCheck";
            this.sendMessageCheck.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.sendMessageCheck.Size = new System.Drawing.Size(69, 24);
            this.sendMessageCheck.TabIndex = 1;
            this.sendMessageCheck.Text = "Message";
            this.sendMessageCheck.UseVisualStyleBackColor = true;
            this.sendMessageCheck.Click += new System.EventHandler(this.sendMessageCheck_Click);
            // 
            // sendBatchCheck
            // 
            this.sendBatchCheck.AutoSize = true;
            this.sendBatchCheck.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.sendBatchCheck.Dock = System.Windows.Forms.DockStyle.Right;
            this.sendBatchCheck.Location = new System.Drawing.Point(90, 0);
            this.sendBatchCheck.Name = "sendBatchCheck";
            this.sendBatchCheck.Size = new System.Drawing.Size(64, 24);
            this.sendBatchCheck.TabIndex = 2;
            this.sendBatchCheck.Text = "Process";
            this.sendBatchCheck.UseVisualStyleBackColor = true;
            this.sendBatchCheck.Click += new System.EventHandler(this.sendBatchCheck_Click);
            // 
            // codeGroupBox
            // 
            this.codeGroupBox.AutoSize = true;
            this.codeGroupBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.codeGroupBox.Location = new System.Drawing.Point(0, 0);
            this.codeGroupBox.Name = "codeGroupBox";
            this.codeGroupBox.Size = new System.Drawing.Size(477, 385);
            this.codeGroupBox.TabIndex = 0;
            this.codeGroupBox.TabStop = false;
            this.codeGroupBox.Text = "Code Window";
            // 
            // saveScreenshotDialog
            // 
            this.saveScreenshotDialog.Filter = "Bitmap Files|*.bmp";
            this.saveScreenshotDialog.Title = "Save Screenshot";
            // 
            // screenshotWorker
            // 
            this.screenshotWorker.WorkerReportsProgress = true;
            this.screenshotWorker.WorkerSupportsCancellation = true;
            this.screenshotWorker.DoWork += new System.ComponentModel.DoWorkEventHandler(this.screenshotWorker_DoWork);
            // 
            // loadingAnimation
            // 
            this.loadingAnimation.Enabled = true;
            this.loadingAnimation.Interval = 400;
            this.loadingAnimation.Tick += new System.EventHandler(this.loadingAnimation_Tick);
            // 
            // uploadingFile
            // 
            this.uploadingFile.WorkerSupportsCancellation = true;
            this.uploadingFile.DoWork += new System.ComponentModel.DoWorkEventHandler(this.uploadingFile_DoWork_1);
            // 
            // uploadUpdate
            // 
            this.uploadUpdate.WorkerSupportsCancellation = true;
            this.uploadUpdate.DoWork += new System.ComponentModel.DoWorkEventHandler(this.uploadUpdate_DoWork);
            // 
            // listLocalNetwork
            // 
            this.listLocalNetwork.Enabled = true;
            this.listLocalNetwork.Interval = 1000;
            this.listLocalNetwork.Tick += new System.EventHandler(this.listLocalNetwork_Tick);
            // 
            // Window
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.ClientSize = new System.Drawing.Size(624, 441);
            this.Controls.Add(this.loginBox);
            this.Controls.Add(this.connectionBox);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.batchConsoleMenuStrip;
            this.Name = "Window";
            this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Window_FormClosing);
            this.Load += new System.EventHandler(this.Window_Load);
            this.addressBox.ResumeLayout(false);
            this.addressBox.PerformLayout();
            this.loginBox.ResumeLayout(false);
            this.localNetworkGroupBox.ResumeLayout(false);
            this.localNetworkStrip.ResumeLayout(false);
            this.windowMenuStrip.ResumeLayout(false);
            this.fileManagerMenuStrip.ResumeLayout(false);
            this.connectionBox.ResumeLayout(false);
            this.mainLayoutPanel.ResumeLayout(false);
            this.splitContainer.Panel1.ResumeLayout(false);
            this.splitContainer.Panel2.ResumeLayout(false);
            this.splitContainer.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).EndInit();
            this.splitContainer.ResumeLayout(false);
            this.tabControl.ResumeLayout(false);
            this.batchCodeTab.ResumeLayout(false);
            this.batchCodeTab.PerformLayout();
            this.batchProcessorMenuStrip.ResumeLayout(false);
            this.batchProcessorMenuStrip.PerformLayout();
            this.batchAdministrationTab.ResumeLayout(false);
            this.batchAdministrationTab.PerformLayout();
            this.batchConsoleLayoutPanel.ResumeLayout(false);
            this.batchConsoleLayoutPanel.PerformLayout();
            this.batchConsoleMenuStrip.ResumeLayout(false);
            this.batchConsoleMenuStrip.PerformLayout();
            this.usageMonitorTab.ResumeLayout(false);
            this.screenshotTab.ResumeLayout(false);
            this.captureScreenshotPanel.ResumeLayout(false);
            this.captureScreenshotBtnStrip.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.screenshotImage)).EndInit();
            this.screenshotCaptureStrip.ResumeLayout(false);
            this.fileHistoryGroup.ResumeLayout(false);
            this.splitContainer2.Panel1.ResumeLayout(false);
            this.splitContainer2.Panel1.PerformLayout();
            this.splitContainer2.Panel2.ResumeLayout(false);
            this.splitContainer2.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).EndInit();
            this.splitContainer2.ResumeLayout(false);
            this.ResumeLayout(false);

		}
	}
}
