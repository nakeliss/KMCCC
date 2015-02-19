﻿namespace KMCCC.Launcher
{
	#region

	using System;
	using System.Linq;
	using System.Management;
	using System.Net;
	using System.Reflection;
	using System.Text;
	using System.Threading.Tasks;
	using LitJson;
	using Tools;

	#endregion

	/// <summary>
	///     KMCCC启动报告上传
	/// </summary>
	public static partial class Reporter
	{
		/// <summary>
		///     启动报告的详细程度
		/// </summary>
		public enum ReportLevel
		{
			/// <summary>
			///     完整报告
			/// </summary>
			Full,

			/// <summary>
			///     去除可能的敏感信息
			/// </summary>
			Basic,

			/// <summary>
			///     关闭报告
			/// </summary>
			None
		}

#if DEBUG
		private const string SERVICE_ROOT = @"http://localhost:12580";
#else
		private const string SERVICE_ROOT = @"http://api.kmccc.minestudio.org/v1";
#endif

		public const string Version = "0.9.5.1";

		private const string LAUNCH_REPORT = SERVICE_ROOT + "/launch";

		private static ReportLevel _reportLevel = ReportLevel.Full;

		private static string _clientName;

		public static void SetReportLevel(ReportLevel level)
		{
			_reportLevel = level;
		}

		static Reporter()
		{
			_clientName = string.Format("(Default[{0}]) {1}", KMCCC_TYPE, Assembly.GetEntryAssembly().GetName().Name);
		}

		/// <summary>
		///     设置客户端名称，默认为 "(Default) {KMCCC_TYPE}"
		/// </summary>
		/// <param name="name">客户端名称</param>
		public static void SetClientName(string name)
		{
			_clientName = name;
		}

		#region GetInfo

		private static string GetVideoCardInfo()
		{
			try
			{
				var sb = new StringBuilder();
				var i = 0;
				foreach (var mo in new ManagementClass("Win32_VideoController").GetInstances().Cast<ManagementObject>())
				{
					sb.Append('#').Append(i++).Append(mo["Name"].ToString().Trim()).Append(' ');
				}
				return sb.ToString();
			}
			catch
			{
				return String.Empty;
			}
		}

		private static string GetProcessorInfo()
		{
			try
			{
				var sb = new StringBuilder();
				var i = 0;
				foreach (var mo in new ManagementClass("WIN32_Processor").GetInstances().Cast<ManagementObject>())
				{
					sb.Append('#').Append(i++).Append(mo["Name"].ToString().Trim()).Append(' ');
				}
				return sb.ToString();
			}
			catch
			{
				return String.Empty;
			}
		}

		#endregion

		#region LaunchReport

		/// <summary>
		///     报告一次启动结果
		/// </summary>
		/// <param name="core">启动器核心</param>
		/// <param name="result">启动结果</param>
		/// <param name="options">启动选项</param>
		public static LaunchResult Report(this LauncherCore core, LaunchResult result, LaunchOptions options)
		{
			if (_reportLevel == ReportLevel.None) return result;
			Task.Factory.StartNew(() =>
			{
				try
				{
					var wc = new WebClient();
					wc.Headers.Add("user-agent", _clientName);
					wc.UploadString(LAUNCH_REPORT,
						JsonMapper.ToJson((_reportLevel == ReportLevel.Full)
							? new FullLaunchReport(core, result, options)
							: new BasicLaunchReport(core, result, options))
#if DEBUG
							.Print()
#endif
						);
				}
				catch
				{
					Console.WriteLine("[KMCCC] report failed");
				}
			});
			return result;
		}

		public class BasicLaunchReport
		{
			#region 目录/位置

			public string JavaPath;

			#endregion

			#region 启动信息

			public string AuthenticationType;
			public string LaunchErrorType;

			#endregion

			#region KMCCC信息

			public string LauncherType;
			public string LauncherVersion;

			#endregion

			#region 系统信息

			public string Arch;
			public uint Memory;
			public string ProcessorInfo;
			public string RuntimeVersion;
			public string SystemVersion;
			public string VideoCardInfo;

			#endregion

			public BasicLaunchReport(LauncherCore core, LaunchResult result, LaunchOptions options)
			{
				#region KMCCC信息

				LauncherType = KMCCC_TYPE;
				LauncherVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();

				#endregion

				#region 系统信息

				RuntimeVersion = Environment.Version.ToString();
				SystemVersion = Environment.OSVersion.VersionString;
				Memory = ((uint) SystemTools.GetTotalMemory() << 20);
				Arch = SystemTools.GetArch();
				VideoCardInfo = GetVideoCardInfo();
				ProcessorInfo = GetProcessorInfo();

				#endregion

				#region 启动信息

				LaunchErrorType = result.ErrorType.ToString();
				JavaPath = core.JavaPath;
				AuthenticationType = options.Authenticator.Type;

				#endregion
			}
		}

		public class FullLaunchReport : BasicLaunchReport
		{
			#region 目录/位置

			public string GameDirectory;
			public string LauncherDirectory;

			#endregion

			#region 启动信息

			public string AutoConnectServer;
			public string LaunchedVersionId;
			public string PlayerName;

			#endregion

			public FullLaunchReport(LauncherCore core, LaunchResult result, LaunchOptions options) : base(core, result, options)
			{
				LaunchedVersionId = options.Version.Id;
				AutoConnectServer = (options.Server != null) ? options.Server.ToString() : "";
				LauncherDirectory = Environment.CurrentDirectory;
				GameDirectory = core.GameRootPath;
				if (result.Handle != null)
				{
					PlayerName = result.Handle.Arguments.Authentication.DisplayName;
				}
			}
		}

		#endregion
	}
}