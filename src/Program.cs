using System;
using System.IO;
using System.Net;
using Fclp;
using CsvHelper;
using CsvHelper.Configuration.Attributes;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace Imp.OscPinger
{
	public class ApplicationArguments
	{
		public string ConfigurationFilePath { get; set; }
		public List<string> Targets { get; set; }
		public int Port { get; set; }
		public int Interval { get; set; }
	}

	public struct ConfigEntry
	{
		[Index(0)]
		public string Label { get; set; }
		[Index(1)]
		public string Address { get; set; }
		[Index(2)]
		public string OscAddress { get; set; }
	}

	class Program
	{
		static void Main(string[] args)
		{
			var p = new FluentCommandLineParser<ApplicationArguments>();

			p.Setup(arg => arg.ConfigurationFilePath)
				.As('c', "configuration")
				.Required();

			p.Setup(arg => arg.Targets)
				.As('t', "targets")
				.Required();

			p.Setup(arg => arg.Port)
				.As('p', "port")
				.Required();

			p.Setup(arg => arg.Interval)
				.As('i', "interval")
				.SetDefault(1000);

			var result = p.Parse(args);

			if (result.HasErrors)
			{
				Console.WriteLine(result.ErrorText);
				Environment.Exit(-1);
			}

			string configPathAbs = null;

			try
			{
				configPathAbs = Path.GetFullPath(p.Object.ConfigurationFilePath);
			}
			catch (ArgumentException ex)
			{
				Console.WriteLine($"Config file path invalid - {ex.Message}");
				Environment.Exit(-1);
			}

			var pingTasks = new List<PingTask>();

			try
			{
				using (var reader = new StreamReader(configPathAbs))
				using (var csv = new CsvReader(reader))
				{
					csv.Configuration.HasHeaderRecord = false;
					var entries = csv.GetRecords<ConfigEntry>().ToList();

					var oscregex = new Regex(@"^\/[^ #*,?\[\]\{\}]+[^ #*,?\[\]\{\}\/]$");

					foreach(var e in entries)
					{
						if (!IPAddress.TryParse(e.Address, out IPAddress ip))
						{
							Console.WriteLine($"Config file invalid - item '{e.Label}' has invalid IP '{e.Address}'");
							Environment.Exit(-1);
						}

						if (!oscregex.IsMatch(e.OscAddress))
						{
							Console.WriteLine($"Config file invalid - item '{e.Label}' has invalid OSC address '{e.OscAddress}'");
							Environment.Exit(-1);
						}

						pingTasks.Add(new PingTask(e.Label, ip, e.OscAddress));
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Config file invalid - {ex.Message}");
				Environment.Exit(-1);
			}

			int port = p.Object.Port;
			if (port < 1 || port > UInt16.MaxValue)
			{
				Console.WriteLine($"Invalid UDP port number '{port}'");
				Environment.Exit(-1);
			}

			int interval = p.Object.Interval;
			if (interval < 500 || interval > 60000)
			{
				Console.WriteLine($"Invalid interval time '{interval}'. Interval time is specified in ms and must be between 500 and 60000.");
				Environment.Exit(-1);
			}

			var targets = new List<IPAddress>();

			foreach(var t in p.Object.Targets)
			{
				if (!IPAddress.TryParse(t, out IPAddress ip))
				{
					Console.WriteLine($"Invalid target IP address '{t}'");
					Environment.Exit(-1);
				}

				targets.Add(ip);
			}

			var oscService = new OscService(port, targets);
			var pingService = new PingService(pingTasks);
			pingService.OnPingResult += (s, e) => oscService.SendStatus(e.Ping, e.Task.OscAddress);

			while(true)
			{
				pingService.SendPings();
				Thread.Sleep(interval);
			}
		}
	}
}
