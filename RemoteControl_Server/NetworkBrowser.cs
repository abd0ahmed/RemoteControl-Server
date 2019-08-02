using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows.Forms;

namespace RemoteControl_Server
{
	public sealed class NetworkBrowser
	{
		public struct _SERVER_INFO_100
		{
			internal int sv100_platform_id;

			[MarshalAs(UnmanagedType.LPWStr)]
			internal string sv100_name;
		}

		[DllImport("Netapi32", CharSet = CharSet.Auto, SetLastError = true)]
		[SuppressUnmanagedCodeSecurity]
		public static extern int NetServerEnum(string ServerNane, int dwLevel, ref IntPtr pBuf, int dwPrefMaxLen, out int dwEntriesRead, out int dwTotalEntries, int dwServerType, string domain, out int dwResumeHandle);

		[DllImport("Netapi32", SetLastError = true)]
		[SuppressUnmanagedCodeSecurity]
		public static extern int NetApiBufferFree(IntPtr pBuf);

		public ArrayList getNetworkComputers()
		{
			ArrayList arrayList = new ArrayList();
			int num = 1;
			int num2 = 2;
			IntPtr pBuf = IntPtr.Zero;
			IntPtr zero = IntPtr.Zero;
			int dwEntriesRead = 0;
			int dwTotalEntries = 0;
			int dwResumeHandle = 0;
			int num3 = Marshal.SizeOf(typeof(_SERVER_INFO_100));
			try
			{
				if (NetServerEnum(null, 100, ref pBuf, -1, out dwEntriesRead, out dwTotalEntries, num | num2, null, out dwResumeHandle) == 0)
				{
					for (int i = 0; i < dwTotalEntries; i++)
					{
						IntPtr ptr = new IntPtr((int)pBuf + i * num3);
						_SERVER_INFO_100 sERVER_INFO_ = (_SERVER_INFO_100)Marshal.PtrToStructure(ptr, typeof(_SERVER_INFO_100));
						arrayList.Add(sERVER_INFO_.sv100_name);
					}
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show("Problem with acessing network computers in NetworkBrowser \r\n\r\n\r\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
				return null;
			}
			finally
			{
				NetApiBufferFree(pBuf);
			}
			return arrayList;
		}
	}
}
