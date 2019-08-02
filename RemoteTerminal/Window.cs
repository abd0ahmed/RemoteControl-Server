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
									fileHistory.Items.Add(text3);
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
				Console.WriteLine(ex.Message);
				handleDisconnect();
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
			catch
			{
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
			components = new System.ComponentModel.Container();
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(RemoteTerminal.Window));
			addressBox = new System.Windows.Forms.GroupBox();
			addressText = new System.Windows.Forms.TextBox();
			connectBtn = new System.Windows.Forms.Button();
			loginBox = new System.Windows.Forms.GroupBox();
			localNetworkGroupBox = new System.Windows.Forms.GroupBox();
			localNetwork = new System.Windows.Forms.ListBox();
			localNetworkStrip = new System.Windows.Forms.ContextMenuStrip(components);
			populateToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			windowMenuStrip = new System.Windows.Forms.ContextMenuStrip(components);
			closeConnectionToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			connectionDetailsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			flashClientsideUpdateToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			fileManagerMenuStrip = new System.Windows.Forms.ContextMenuStrip(components);
			addExistingFileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			removeCachedFileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			toolStripSeparator = new System.Windows.Forms.ToolStripSeparator();
			runOnTargetComputerToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			desktopControlTab = new System.Windows.Forms.TabPage();
			noTimeoutTimer = new System.Windows.Forms.Timer(components);
			openFileDialog = new System.Windows.Forms.OpenFileDialog();
			localNetworkSearch = new System.ComponentModel.BackgroundWorker();
			connectionBox = new System.Windows.Forms.Panel();
			mainLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
			splitContainer = new System.Windows.Forms.SplitContainer();
			tabControl = new System.Windows.Forms.TabControl();
			batchCodeTab = new System.Windows.Forms.TabPage();
			batchCodeBox = new System.Windows.Forms.TextBox();
			batchProcessorMenuStrip = new System.Windows.Forms.MenuStrip();
			saveCodeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			loadCodeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			saveCodeComboBox = new System.Windows.Forms.ToolStripComboBox();
			batchAdministrationTab = new System.Windows.Forms.TabPage();
			notifyLabel = new System.Windows.Forms.Label();
			batchConsoleLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
			batchAdministrativeHistory = new System.Windows.Forms.ListBox();
			batchAdministrativeInput = new System.Windows.Forms.TextBox();
			batchConsoleMenuStrip = new System.Windows.Forms.MenuStrip();
			enableDisableToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			terminateTaskBtn = new System.Windows.Forms.ToolStripMenuItem();
			batchStatusLabel = new System.Windows.Forms.ToolStripMenuItem();
			usageMonitorTab = new System.Windows.Forms.TabPage();
			monitorStatusLabel = new System.Windows.Forms.Label();
			startStopMonitor = new System.Windows.Forms.Button();
			usageLabel = new System.Windows.Forms.Label();
			screenshotTab = new System.Windows.Forms.TabPage();
			captureScreenshotPanel = new System.Windows.Forms.TableLayoutPanel();
			screenshotBtn = new System.Windows.Forms.Button();
			captureScreenshotBtnStrip = new System.Windows.Forms.ContextMenuStrip(components);
			startEndlessCaptureLoopToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			screenshotImage = new System.Windows.Forms.PictureBox();
			screenshotCaptureStrip = new System.Windows.Forms.ContextMenuStrip(components);
			copyScreenshotToMemoryToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			saveScreenshotToFileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			fileHistoryGroup = new System.Windows.Forms.GroupBox();
			fileHistory = new System.Windows.Forms.ListBox();
			splitContainer2 = new System.Windows.Forms.SplitContainer();
			messageTextInput = new System.Windows.Forms.TextBox();
			sendMessageCheck = new System.Windows.Forms.CheckBox();
			sendBatchCheck = new System.Windows.Forms.CheckBox();
			codeGroupBox = new System.Windows.Forms.GroupBox();
			saveScreenshotDialog = new System.Windows.Forms.SaveFileDialog();
			screenshotWorker = new System.ComponentModel.BackgroundWorker();
			loadingAnimation = new System.Windows.Forms.Timer(components);
			uploadingFile = new System.ComponentModel.BackgroundWorker();
			uploadUpdate = new System.ComponentModel.BackgroundWorker();
			listLocalNetwork = new System.Windows.Forms.Timer(components);
			addressBox.SuspendLayout();
			loginBox.SuspendLayout();
			localNetworkGroupBox.SuspendLayout();
			localNetworkStrip.SuspendLayout();
			windowMenuStrip.SuspendLayout();
			fileManagerMenuStrip.SuspendLayout();
			connectionBox.SuspendLayout();
			mainLayoutPanel.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)splitContainer).BeginInit();
			splitContainer.Panel1.SuspendLayout();
			splitContainer.Panel2.SuspendLayout();
			splitContainer.SuspendLayout();
			tabControl.SuspendLayout();
			batchCodeTab.SuspendLayout();
			batchProcessorMenuStrip.SuspendLayout();
			batchAdministrationTab.SuspendLayout();
			batchConsoleLayoutPanel.SuspendLayout();
			batchConsoleMenuStrip.SuspendLayout();
			usageMonitorTab.SuspendLayout();
			screenshotTab.SuspendLayout();
			captureScreenshotPanel.SuspendLayout();
			captureScreenshotBtnStrip.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)screenshotImage).BeginInit();
			screenshotCaptureStrip.SuspendLayout();
			fileHistoryGroup.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)splitContainer2).BeginInit();
			splitContainer2.Panel1.SuspendLayout();
			splitContainer2.Panel2.SuspendLayout();
			splitContainer2.SuspendLayout();
			SuspendLayout();
			addressBox.Controls.Add(addressText);
			addressBox.Controls.Add(connectBtn);
			addressBox.Dock = System.Windows.Forms.DockStyle.Top;
			addressBox.Location = new System.Drawing.Point(3, 16);
			addressBox.Name = "addressBox";
			addressBox.Size = new System.Drawing.Size(243, 73);
			addressBox.TabIndex = 0;
			addressBox.TabStop = false;
			addressBox.Text = "Address";
			addressText.Dock = System.Windows.Forms.DockStyle.Fill;
			addressText.Location = new System.Drawing.Point(3, 16);
			addressText.Name = "addressText";
			addressText.Size = new System.Drawing.Size(237, 20);
			addressText.TabIndex = 0;
			addressText.KeyDown += new System.Windows.Forms.KeyEventHandler(addressText_KeyDown);
			connectBtn.Dock = System.Windows.Forms.DockStyle.Bottom;
			connectBtn.Location = new System.Drawing.Point(3, 41);
			connectBtn.Name = "connectBtn";
			connectBtn.Size = new System.Drawing.Size(237, 29);
			connectBtn.TabIndex = 3;
			connectBtn.Text = "Connect ->";
			connectBtn.UseVisualStyleBackColor = true;
			connectBtn.Click += new System.EventHandler(connectBtn_Click);
			loginBox.BackColor = System.Drawing.SystemColors.Control;
			loginBox.Controls.Add(localNetworkGroupBox);
			loginBox.Controls.Add(addressBox);
			loginBox.Location = new System.Drawing.Point(178, 115);
			loginBox.Name = "loginBox";
			loginBox.Size = new System.Drawing.Size(249, 201);
			loginBox.TabIndex = 0;
			loginBox.TabStop = false;
			localNetworkGroupBox.Controls.Add(localNetwork);
			localNetworkGroupBox.Dock = System.Windows.Forms.DockStyle.Fill;
			localNetworkGroupBox.Location = new System.Drawing.Point(3, 89);
			localNetworkGroupBox.Name = "localNetworkGroupBox";
			localNetworkGroupBox.Size = new System.Drawing.Size(243, 109);
			localNetworkGroupBox.TabIndex = 5;
			localNetworkGroupBox.TabStop = false;
			localNetworkGroupBox.Text = "Devices found on the local network ";
			localNetwork.ContextMenuStrip = localNetworkStrip;
			localNetwork.Dock = System.Windows.Forms.DockStyle.Fill;
			localNetwork.FormattingEnabled = true;
			localNetwork.Location = new System.Drawing.Point(3, 16);
			localNetwork.Name = "localNetwork";
			localNetwork.Size = new System.Drawing.Size(237, 90);
			localNetwork.TabIndex = 4;
			localNetwork.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(localNetwork_MouseDoubleClick);
			localNetworkStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[1]
			{
				populateToolStripMenuItem
			});
			localNetworkStrip.Name = "localNetworkStrip";
			localNetworkStrip.Size = new System.Drawing.Size(134, 26);
			localNetworkStrip.Opening += new System.ComponentModel.CancelEventHandler(localNetworkStrip_Opening);
			populateToolStripMenuItem.Name = "populateToolStripMenuItem";
			populateToolStripMenuItem.Size = new System.Drawing.Size(133, 22);
			populateToolStripMenuItem.Text = "Populate ...";
			populateToolStripMenuItem.Click += new System.EventHandler(populateToolStripMenuItem_Click);
			windowMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[3]
			{
				closeConnectionToolStripMenuItem,
				connectionDetailsToolStripMenuItem,
				flashClientsideUpdateToolStripMenuItem
			});
			windowMenuStrip.Name = "windowMenuStrip";
			windowMenuStrip.Size = new System.Drawing.Size(214, 70);
			closeConnectionToolStripMenuItem.Name = "closeConnectionToolStripMenuItem";
			closeConnectionToolStripMenuItem.Size = new System.Drawing.Size(213, 22);
			closeConnectionToolStripMenuItem.Text = "Close connection ---";
			closeConnectionToolStripMenuItem.Click += new System.EventHandler(closeConnectionToolStripMenuItem_Click);
			connectionDetailsToolStripMenuItem.Name = "connectionDetailsToolStripMenuItem";
			connectionDetailsToolStripMenuItem.Size = new System.Drawing.Size(213, 22);
			connectionDetailsToolStripMenuItem.Text = "Connection details ---";
			connectionDetailsToolStripMenuItem.Click += new System.EventHandler(connectionDetailsToolStripMenuItem_Click);
			flashClientsideUpdateToolStripMenuItem.Name = "flashClientsideUpdateToolStripMenuItem";
			flashClientsideUpdateToolStripMenuItem.Size = new System.Drawing.Size(213, 22);
			flashClientsideUpdateToolStripMenuItem.Text = "Flash client-side update (!)";
			flashClientsideUpdateToolStripMenuItem.Click += new System.EventHandler(flashClientsideUpdateToolStripMenuItem_Click);
			fileManagerMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[4]
			{
				addExistingFileToolStripMenuItem,
				removeCachedFileToolStripMenuItem,
				toolStripSeparator,
				runOnTargetComputerToolStripMenuItem
			});
			fileManagerMenuStrip.Name = "fileManagerMenuStrip";
			fileManagerMenuStrip.Size = new System.Drawing.Size(267, 76);
			fileManagerMenuStrip.Opening += new System.ComponentModel.CancelEventHandler(fileManagerMenuStrip_Opening);
			addExistingFileToolStripMenuItem.Name = "addExistingFileToolStripMenuItem";
			addExistingFileToolStripMenuItem.Size = new System.Drawing.Size(266, 22);
			addExistingFileToolStripMenuItem.Text = "Add an existing file to memory";
			addExistingFileToolStripMenuItem.Click += new System.EventHandler(addExistingFileToolStripMenuItem_Click);
			removeCachedFileToolStripMenuItem.Name = "removeCachedFileToolStripMenuItem";
			removeCachedFileToolStripMenuItem.Size = new System.Drawing.Size(266, 22);
			removeCachedFileToolStripMenuItem.Text = "Remove selected file from memory";
			removeCachedFileToolStripMenuItem.Click += new System.EventHandler(removeCachedFileToolStripMenuItem_Click);
			toolStripSeparator.Name = "toolStripSeparator";
			toolStripSeparator.Size = new System.Drawing.Size(263, 6);
			runOnTargetComputerToolStripMenuItem.Name = "runOnTargetComputerToolStripMenuItem";
			runOnTargetComputerToolStripMenuItem.Size = new System.Drawing.Size(266, 22);
			runOnTargetComputerToolStripMenuItem.Text = "Run selected file on target computer";
			runOnTargetComputerToolStripMenuItem.Click += new System.EventHandler(runOnTargetComputerToolStripMenuItem_Click);
			desktopControlTab.Location = new System.Drawing.Point(4, 22);
			desktopControlTab.Name = "desktopControlTab";
			desktopControlTab.Padding = new System.Windows.Forms.Padding(3);
			desktopControlTab.Size = new System.Drawing.Size(450, 350);
			desktopControlTab.TabIndex = 1;
			desktopControlTab.Text = "Desktop Control Center";
			desktopControlTab.UseVisualStyleBackColor = true;
			noTimeoutTimer.Interval = 500;
			noTimeoutTimer.Tick += new System.EventHandler(noTimeoutTimer_Tick);
			localNetworkSearch.WorkerSupportsCancellation = true;
			localNetworkSearch.DoWork += new System.ComponentModel.DoWorkEventHandler(localNetworkSearch_DoWork);
			connectionBox.ContextMenuStrip = windowMenuStrip;
			connectionBox.Controls.Add(mainLayoutPanel);
			connectionBox.Dock = System.Windows.Forms.DockStyle.Fill;
			connectionBox.Location = new System.Drawing.Point(0, 0);
			connectionBox.Name = "connectionBox";
			connectionBox.Size = new System.Drawing.Size(624, 441);
			connectionBox.TabIndex = 5;
			connectionBox.Visible = false;
			mainLayoutPanel.ColumnCount = 1;
			mainLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100f));
			mainLayoutPanel.Controls.Add(splitContainer, 0, 0);
			mainLayoutPanel.Controls.Add(splitContainer2, 0, 1);
			mainLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
			mainLayoutPanel.Location = new System.Drawing.Point(0, 0);
			mainLayoutPanel.Name = "mainLayoutPanel";
			mainLayoutPanel.RowCount = 2;
			mainLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100f));
			mainLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30f));
			mainLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20f));
			mainLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20f));
			mainLayoutPanel.Size = new System.Drawing.Size(624, 441);
			mainLayoutPanel.TabIndex = 7;
			splitContainer.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			splitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
			splitContainer.Location = new System.Drawing.Point(3, 3);
			splitContainer.Name = "splitContainer";
			splitContainer.Panel1.Controls.Add(tabControl);
			splitContainer.Panel2.Controls.Add(fileHistoryGroup);
			splitContainer.Size = new System.Drawing.Size(618, 405);
			splitContainer.SplitterDistance = 443;
			splitContainer.TabIndex = 0;
			tabControl.Appearance = System.Windows.Forms.TabAppearance.Buttons;
			tabControl.Controls.Add(batchCodeTab);
			tabControl.Controls.Add(batchAdministrationTab);
			tabControl.Controls.Add(usageMonitorTab);
			tabControl.Controls.Add(screenshotTab);
			tabControl.Dock = System.Windows.Forms.DockStyle.Fill;
			tabControl.Location = new System.Drawing.Point(0, 0);
			tabControl.Multiline = true;
			tabControl.Name = "tabControl";
			tabControl.RightToLeft = System.Windows.Forms.RightToLeft.No;
			tabControl.SelectedIndex = 0;
			tabControl.Size = new System.Drawing.Size(441, 403);
			tabControl.TabIndex = 0;
			batchCodeTab.BackColor = System.Drawing.SystemColors.Control;
			batchCodeTab.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			batchCodeTab.Controls.Add(batchCodeBox);
			batchCodeTab.Controls.Add(batchProcessorMenuStrip);
			batchCodeTab.Location = new System.Drawing.Point(4, 25);
			batchCodeTab.Name = "batchCodeTab";
			batchCodeTab.Padding = new System.Windows.Forms.Padding(3);
			batchCodeTab.Size = new System.Drawing.Size(433, 374);
			batchCodeTab.TabIndex = 0;
			batchCodeTab.Text = "Batch Processor";
			batchCodeBox.BackColor = System.Drawing.Color.White;
			batchCodeBox.Dock = System.Windows.Forms.DockStyle.Fill;
			batchCodeBox.Font = new System.Drawing.Font("Lucida Console", 8f);
			batchCodeBox.ForeColor = System.Drawing.SystemColors.WindowText;
			batchCodeBox.Location = new System.Drawing.Point(3, 30);
			batchCodeBox.Multiline = true;
			batchCodeBox.Name = "batchCodeBox";
			batchCodeBox.ScrollBars = System.Windows.Forms.ScrollBars.Both;
			batchCodeBox.Size = new System.Drawing.Size(425, 339);
			batchCodeBox.TabIndex = 0;
			batchCodeBox.WordWrap = false;
			batchProcessorMenuStrip.BackColor = System.Drawing.SystemColors.Control;
			batchProcessorMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[3]
			{
				saveCodeToolStripMenuItem,
				loadCodeToolStripMenuItem,
				saveCodeComboBox
			});
			batchProcessorMenuStrip.Location = new System.Drawing.Point(3, 3);
			batchProcessorMenuStrip.Name = "batchProcessorMenuStrip";
			batchProcessorMenuStrip.Size = new System.Drawing.Size(425, 27);
			batchProcessorMenuStrip.TabIndex = 1;
			saveCodeToolStripMenuItem.Name = "saveCodeToolStripMenuItem";
			saveCodeToolStripMenuItem.Size = new System.Drawing.Size(74, 23);
			saveCodeToolStripMenuItem.Text = "Save Code";
			saveCodeToolStripMenuItem.Click += new System.EventHandler(saveCodeToolStripMenuItem_Click);
			loadCodeToolStripMenuItem.Name = "loadCodeToolStripMenuItem";
			loadCodeToolStripMenuItem.Size = new System.Drawing.Size(76, 23);
			loadCodeToolStripMenuItem.Text = "Load Code";
			loadCodeToolStripMenuItem.Click += new System.EventHandler(loadCodeToolStripMenuItem_Click);
			saveCodeComboBox.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
			saveCodeComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			saveCodeComboBox.FlatStyle = System.Windows.Forms.FlatStyle.Standard;
			saveCodeComboBox.Name = "saveCodeComboBox";
			saveCodeComboBox.Size = new System.Drawing.Size(215, 23);
			saveCodeComboBox.Sorted = true;
			batchAdministrationTab.BackColor = System.Drawing.SystemColors.Control;
			batchAdministrationTab.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			batchAdministrationTab.Controls.Add(notifyLabel);
			batchAdministrationTab.Controls.Add(batchConsoleLayoutPanel);
			batchAdministrationTab.Controls.Add(batchConsoleMenuStrip);
			batchAdministrationTab.Location = new System.Drawing.Point(4, 25);
			batchAdministrationTab.Name = "batchAdministrationTab";
			batchAdministrationTab.Padding = new System.Windows.Forms.Padding(3);
			batchAdministrationTab.Size = new System.Drawing.Size(433, 374);
			batchAdministrationTab.TabIndex = 1;
			batchAdministrationTab.Text = "Batch Console";
			notifyLabel.BackColor = System.Drawing.Color.White;
			notifyLabel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			notifyLabel.Cursor = System.Windows.Forms.Cursors.PanNW;
			notifyLabel.Dock = System.Windows.Forms.DockStyle.Fill;
			notifyLabel.Location = new System.Drawing.Point(3, 27);
			notifyLabel.Name = "notifyLabel";
			notifyLabel.Size = new System.Drawing.Size(425, 342);
			notifyLabel.TabIndex = 4;
			notifyLabel.Text = "Enable console to see the console history.";
			notifyLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			batchConsoleLayoutPanel.BackColor = System.Drawing.Color.White;
			batchConsoleLayoutPanel.CellBorderStyle = System.Windows.Forms.TableLayoutPanelCellBorderStyle.Single;
			batchConsoleLayoutPanel.ColumnCount = 1;
			batchConsoleLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100f));
			batchConsoleLayoutPanel.Controls.Add(batchAdministrativeHistory, 0, 0);
			batchConsoleLayoutPanel.Controls.Add(batchAdministrativeInput, 0, 1);
			batchConsoleLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
			batchConsoleLayoutPanel.Location = new System.Drawing.Point(3, 27);
			batchConsoleLayoutPanel.Name = "batchConsoleLayoutPanel";
			batchConsoleLayoutPanel.RowCount = 2;
			batchConsoleLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100f));
			batchConsoleLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25f));
			batchConsoleLayoutPanel.Size = new System.Drawing.Size(425, 342);
			batchConsoleLayoutPanel.TabIndex = 3;
			batchAdministrativeHistory.BackColor = System.Drawing.Color.White;
			batchAdministrativeHistory.BorderStyle = System.Windows.Forms.BorderStyle.None;
			batchAdministrativeHistory.Dock = System.Windows.Forms.DockStyle.Fill;
			batchAdministrativeHistory.Font = new System.Drawing.Font("Consolas", 8.75f);
			batchAdministrativeHistory.ForeColor = System.Drawing.Color.White;
			batchAdministrativeHistory.HorizontalScrollbar = true;
			batchAdministrativeHistory.ItemHeight = 14;
			batchAdministrativeHistory.Location = new System.Drawing.Point(4, 4);
			batchAdministrativeHistory.Name = "batchAdministrativeHistory";
			batchAdministrativeHistory.Size = new System.Drawing.Size(417, 308);
			batchAdministrativeHistory.TabIndex = 1;
			batchAdministrativeInput.BackColor = System.Drawing.Color.White;
			batchAdministrativeInput.Dock = System.Windows.Forms.DockStyle.Fill;
			batchAdministrativeInput.Enabled = false;
			batchAdministrativeInput.ForeColor = System.Drawing.Color.Black;
			batchAdministrativeInput.Location = new System.Drawing.Point(4, 319);
			batchAdministrativeInput.Name = "batchAdministrativeInput";
			batchAdministrativeInput.Size = new System.Drawing.Size(417, 20);
			batchAdministrativeInput.TabIndex = 2;
			batchAdministrativeInput.KeyDown += new System.Windows.Forms.KeyEventHandler(batchAdministrativeInput_KeyDown);
			batchConsoleMenuStrip.BackColor = System.Drawing.SystemColors.Control;
			batchConsoleMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[3]
			{
				enableDisableToolStripMenuItem,
				terminateTaskBtn,
				batchStatusLabel
			});
			batchConsoleMenuStrip.Location = new System.Drawing.Point(3, 3);
			batchConsoleMenuStrip.Name = "batchConsoleMenuStrip";
			batchConsoleMenuStrip.Size = new System.Drawing.Size(425, 24);
			batchConsoleMenuStrip.TabIndex = 0;
			enableDisableToolStripMenuItem.Name = "enableDisableToolStripMenuItem";
			enableDisableToolStripMenuItem.Size = new System.Drawing.Size(114, 20);
			enableDisableToolStripMenuItem.Text = "[ Enable Console ]";
			enableDisableToolStripMenuItem.Click += new System.EventHandler(enableDisableToolStripMenuItem_Click);
			terminateTaskBtn.Enabled = false;
			terminateTaskBtn.Name = "terminateTaskBtn";
			terminateTaskBtn.Size = new System.Drawing.Size(100, 20);
			terminateTaskBtn.Text = "Terminate Task";
			terminateTaskBtn.Click += new System.EventHandler(terminateTaskBtn_Click);
			batchStatusLabel.Name = "batchStatusLabel";
			batchStatusLabel.Size = new System.Drawing.Size(98, 20);
			batchStatusLabel.Text = "Status: Inactive";
			batchStatusLabel.Click += new System.EventHandler(batchStatusLabel_Click);
			usageMonitorTab.Controls.Add(monitorStatusLabel);
			usageMonitorTab.Controls.Add(startStopMonitor);
			usageMonitorTab.Controls.Add(usageLabel);
			usageMonitorTab.Location = new System.Drawing.Point(4, 25);
			usageMonitorTab.Name = "usageMonitorTab";
			usageMonitorTab.Size = new System.Drawing.Size(433, 374);
			usageMonitorTab.TabIndex = 2;
			usageMonitorTab.Text = "Usage Monitor";
			usageMonitorTab.UseVisualStyleBackColor = true;
			monitorStatusLabel.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
			monitorStatusLabel.Dock = System.Windows.Forms.DockStyle.Top;
			monitorStatusLabel.Location = new System.Drawing.Point(0, 0);
			monitorStatusLabel.Name = "monitorStatusLabel";
			monitorStatusLabel.Size = new System.Drawing.Size(433, 23);
			monitorStatusLabel.TabIndex = 2;
			monitorStatusLabel.Text = "Status: Awaiting Connection";
			monitorStatusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			startStopMonitor.Dock = System.Windows.Forms.DockStyle.Bottom;
			startStopMonitor.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
			startStopMonitor.Location = new System.Drawing.Point(0, 344);
			startStopMonitor.Name = "startStopMonitor";
			startStopMonitor.Size = new System.Drawing.Size(433, 30);
			startStopMonitor.TabIndex = 1;
			startStopMonitor.Text = "Forcibly Start - Usage Monitor";
			startStopMonitor.UseVisualStyleBackColor = true;
			startStopMonitor.Click += new System.EventHandler(startStopMonitor_Click);
			usageLabel.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
			usageLabel.Dock = System.Windows.Forms.DockStyle.Fill;
			usageLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 15.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
			usageLabel.Location = new System.Drawing.Point(0, 0);
			usageLabel.Name = "usageLabel";
			usageLabel.Size = new System.Drawing.Size(433, 374);
			usageLabel.TabIndex = 0;
			usageLabel.Text = "n/a";
			usageLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			screenshotTab.Controls.Add(captureScreenshotPanel);
			screenshotTab.Location = new System.Drawing.Point(4, 25);
			screenshotTab.Name = "screenshotTab";
			screenshotTab.Padding = new System.Windows.Forms.Padding(3);
			screenshotTab.Size = new System.Drawing.Size(433, 374);
			screenshotTab.TabIndex = 3;
			screenshotTab.Text = "Screenshot Capture";
			screenshotTab.UseVisualStyleBackColor = true;
			captureScreenshotPanel.CellBorderStyle = System.Windows.Forms.TableLayoutPanelCellBorderStyle.Single;
			captureScreenshotPanel.ColumnCount = 1;
			captureScreenshotPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100f));
			captureScreenshotPanel.Controls.Add(screenshotBtn, 0, 1);
			captureScreenshotPanel.Controls.Add(screenshotImage, 0, 0);
			captureScreenshotPanel.Dock = System.Windows.Forms.DockStyle.Fill;
			captureScreenshotPanel.Location = new System.Drawing.Point(3, 3);
			captureScreenshotPanel.Name = "captureScreenshotPanel";
			captureScreenshotPanel.RowCount = 2;
			captureScreenshotPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100f));
			captureScreenshotPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 35f));
			captureScreenshotPanel.Size = new System.Drawing.Size(427, 368);
			captureScreenshotPanel.TabIndex = 0;
			screenshotBtn.ContextMenuStrip = captureScreenshotBtnStrip;
			screenshotBtn.Dock = System.Windows.Forms.DockStyle.Fill;
			screenshotBtn.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
			screenshotBtn.Location = new System.Drawing.Point(4, 335);
			screenshotBtn.Name = "screenshotBtn";
			screenshotBtn.Size = new System.Drawing.Size(419, 29);
			screenshotBtn.TabIndex = 0;
			screenshotBtn.Text = "Capture Screenshot";
			screenshotBtn.UseVisualStyleBackColor = true;
			screenshotBtn.Click += new System.EventHandler(screenshotBtn_Click);
			captureScreenshotBtnStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[1]
			{
				startEndlessCaptureLoopToolStripMenuItem
			});
			captureScreenshotBtnStrip.Name = "captureScreenshotBtnStrip";
			captureScreenshotBtnStrip.Size = new System.Drawing.Size(231, 26);
			startEndlessCaptureLoopToolStripMenuItem.Name = "startEndlessCaptureLoopToolStripMenuItem";
			startEndlessCaptureLoopToolStripMenuItem.Size = new System.Drawing.Size(230, 22);
			startEndlessCaptureLoopToolStripMenuItem.Text = "Start the endless capture loop";
			startEndlessCaptureLoopToolStripMenuItem.Click += new System.EventHandler(startEndlessCaptureLoopToolStripMenuItem_Click);
			screenshotImage.BackColor = System.Drawing.SystemColors.AppWorkspace;
			screenshotImage.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
			screenshotImage.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			screenshotImage.ContextMenuStrip = screenshotCaptureStrip;
			screenshotImage.Dock = System.Windows.Forms.DockStyle.Fill;
			screenshotImage.Location = new System.Drawing.Point(4, 4);
			screenshotImage.Name = "screenshotImage";
			screenshotImage.Size = new System.Drawing.Size(419, 324);
			screenshotImage.TabIndex = 1;
			screenshotImage.TabStop = false;
			screenshotCaptureStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[2]
			{
				copyScreenshotToMemoryToolStripMenuItem,
				saveScreenshotToFileToolStripMenuItem
			});
			screenshotCaptureStrip.Name = "screenshotCaptureStrip";
			screenshotCaptureStrip.Size = new System.Drawing.Size(237, 48);
			copyScreenshotToMemoryToolStripMenuItem.Name = "copyScreenshotToMemoryToolStripMenuItem";
			copyScreenshotToMemoryToolStripMenuItem.Size = new System.Drawing.Size(236, 22);
			copyScreenshotToMemoryToolStripMenuItem.Text = "Copy screenshot to memory";
			copyScreenshotToMemoryToolStripMenuItem.Click += new System.EventHandler(copyScreenshotToMemoryToolStripMenuItem_Click);
			saveScreenshotToFileToolStripMenuItem.Name = "saveScreenshotToFileToolStripMenuItem";
			saveScreenshotToFileToolStripMenuItem.Size = new System.Drawing.Size(236, 22);
			saveScreenshotToFileToolStripMenuItem.Text = "Save screenshot to a image file";
			saveScreenshotToFileToolStripMenuItem.Click += new System.EventHandler(saveScreenshotToFileToolStripMenuItem_Click);
			fileHistoryGroup.AutoSize = true;
			fileHistoryGroup.Controls.Add(fileHistory);
			fileHistoryGroup.Dock = System.Windows.Forms.DockStyle.Fill;
			fileHistoryGroup.Location = new System.Drawing.Point(0, 0);
			fileHistoryGroup.Name = "fileHistoryGroup";
			fileHistoryGroup.Size = new System.Drawing.Size(169, 403);
			fileHistoryGroup.TabIndex = 0;
			fileHistoryGroup.TabStop = false;
			fileHistoryGroup.Text = "File History";
			fileHistory.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			fileHistory.ContextMenuStrip = fileManagerMenuStrip;
			fileHistory.Dock = System.Windows.Forms.DockStyle.Fill;
			fileHistory.FormattingEnabled = true;
			fileHistory.HorizontalScrollbar = true;
			fileHistory.Location = new System.Drawing.Point(3, 16);
			fileHistory.Name = "fileHistory";
			fileHistory.Size = new System.Drawing.Size(163, 384);
			fileHistory.Sorted = true;
			fileHistory.TabIndex = 0;
			splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
			splitContainer2.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
			splitContainer2.IsSplitterFixed = true;
			splitContainer2.Location = new System.Drawing.Point(3, 414);
			splitContainer2.Name = "splitContainer2";
			splitContainer2.Panel1.Controls.Add(messageTextInput);
			splitContainer2.Panel2.Controls.Add(sendMessageCheck);
			splitContainer2.Panel2.Controls.Add(sendBatchCheck);
			splitContainer2.Size = new System.Drawing.Size(618, 24);
			splitContainer2.SplitterDistance = 460;
			splitContainer2.TabIndex = 1;
			messageTextInput.Dock = System.Windows.Forms.DockStyle.Fill;
			messageTextInput.Location = new System.Drawing.Point(0, 0);
			messageTextInput.Name = "messageTextInput";
			messageTextInput.Size = new System.Drawing.Size(460, 20);
			messageTextInput.TabIndex = 0;
			messageTextInput.KeyDown += new System.Windows.Forms.KeyEventHandler(messageTextInput_KeyDown);
			sendMessageCheck.AutoSize = true;
			sendMessageCheck.Dock = System.Windows.Forms.DockStyle.Right;
			sendMessageCheck.Location = new System.Drawing.Point(21, 0);
			sendMessageCheck.Name = "sendMessageCheck";
			sendMessageCheck.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
			sendMessageCheck.Size = new System.Drawing.Size(69, 24);
			sendMessageCheck.TabIndex = 1;
			sendMessageCheck.Text = "Message";
			sendMessageCheck.UseVisualStyleBackColor = true;
			sendMessageCheck.Click += new System.EventHandler(sendMessageCheck_Click);
			sendBatchCheck.AutoSize = true;
			sendBatchCheck.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
			sendBatchCheck.Dock = System.Windows.Forms.DockStyle.Right;
			sendBatchCheck.Location = new System.Drawing.Point(90, 0);
			sendBatchCheck.Name = "sendBatchCheck";
			sendBatchCheck.Size = new System.Drawing.Size(64, 24);
			sendBatchCheck.TabIndex = 2;
			sendBatchCheck.Text = "Process";
			sendBatchCheck.UseVisualStyleBackColor = true;
			sendBatchCheck.Click += new System.EventHandler(sendBatchCheck_Click);
			codeGroupBox.AutoSize = true;
			codeGroupBox.Dock = System.Windows.Forms.DockStyle.Fill;
			codeGroupBox.Location = new System.Drawing.Point(0, 0);
			codeGroupBox.Name = "codeGroupBox";
			codeGroupBox.Size = new System.Drawing.Size(477, 385);
			codeGroupBox.TabIndex = 0;
			codeGroupBox.TabStop = false;
			codeGroupBox.Text = "Code Window";
			saveScreenshotDialog.Filter = "Bitmap Files|*.bmp";
			saveScreenshotDialog.Title = "Save Screenshot";
			screenshotWorker.WorkerReportsProgress = true;
			screenshotWorker.WorkerSupportsCancellation = true;
			screenshotWorker.DoWork += new System.ComponentModel.DoWorkEventHandler(screenshotWorker_DoWork);
			loadingAnimation.Enabled = true;
			loadingAnimation.Interval = 400;
			loadingAnimation.Tick += new System.EventHandler(loadingAnimation_Tick);
			uploadingFile.WorkerSupportsCancellation = true;
			uploadingFile.DoWork += new System.ComponentModel.DoWorkEventHandler(uploadingFile_DoWork_1);
			uploadUpdate.WorkerSupportsCancellation = true;
			uploadUpdate.DoWork += new System.ComponentModel.DoWorkEventHandler(uploadUpdate_DoWork);
			listLocalNetwork.Enabled = true;
			listLocalNetwork.Interval = 1000;
			listLocalNetwork.Tick += new System.EventHandler(listLocalNetwork_Tick);
			base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			AutoSize = true;
			BackColor = System.Drawing.SystemColors.Control;
			base.ClientSize = new System.Drawing.Size(624, 441);
			base.Controls.Add(loginBox);
			base.Controls.Add(connectionBox);
			base.Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
			base.MainMenuStrip = batchConsoleMenuStrip;
			base.Name = "Window";
			base.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(Window_FormClosing);
			base.Load += new System.EventHandler(Window_Load);
			addressBox.ResumeLayout(false);
			addressBox.PerformLayout();
			loginBox.ResumeLayout(false);
			localNetworkGroupBox.ResumeLayout(false);
			localNetworkStrip.ResumeLayout(false);
			windowMenuStrip.ResumeLayout(false);
			fileManagerMenuStrip.ResumeLayout(false);
			connectionBox.ResumeLayout(false);
			mainLayoutPanel.ResumeLayout(false);
			splitContainer.Panel1.ResumeLayout(false);
			splitContainer.Panel2.ResumeLayout(false);
			splitContainer.Panel2.PerformLayout();
			((System.ComponentModel.ISupportInitialize)splitContainer).EndInit();
			splitContainer.ResumeLayout(false);
			tabControl.ResumeLayout(false);
			batchCodeTab.ResumeLayout(false);
			batchCodeTab.PerformLayout();
			batchProcessorMenuStrip.ResumeLayout(false);
			batchProcessorMenuStrip.PerformLayout();
			batchAdministrationTab.ResumeLayout(false);
			batchAdministrationTab.PerformLayout();
			batchConsoleLayoutPanel.ResumeLayout(false);
			batchConsoleLayoutPanel.PerformLayout();
			batchConsoleMenuStrip.ResumeLayout(false);
			batchConsoleMenuStrip.PerformLayout();
			usageMonitorTab.ResumeLayout(false);
			screenshotTab.ResumeLayout(false);
			captureScreenshotPanel.ResumeLayout(false);
			captureScreenshotBtnStrip.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)screenshotImage).EndInit();
			screenshotCaptureStrip.ResumeLayout(false);
			fileHistoryGroup.ResumeLayout(false);
			splitContainer2.Panel1.ResumeLayout(false);
			splitContainer2.Panel1.PerformLayout();
			splitContainer2.Panel2.ResumeLayout(false);
			splitContainer2.Panel2.PerformLayout();
			((System.ComponentModel.ISupportInitialize)splitContainer2).EndInit();
			splitContainer2.ResumeLayout(false);
			ResumeLayout(false);
		}
	}
}
