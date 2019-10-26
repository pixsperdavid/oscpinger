using System;
using System.IO;
using System.Net;
using CsvHelper;
using CsvHelper.Configuration.Attributes;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using CommandLine;

namespace Imp.OscPinger
{
	public class Options
	{
		[Option('c', "config", Required = true, HelpText = "Path to configuration csv file.")]
		public string ConfigurationFilePath { get; set; }
		[Option('t', "targets", Required = true, HelpText = "Set target IP address(es) to send OSC ping status messages to")]
		public IEnumerable<string> Targets { get; set; }
		[Option('p', "port", Required = true, HelpText = "Set targert UDP port to send OSC ping status messages to.")]
		public int Port { get; set; }
		[Option('i', "interval", Required = false, Default = 1000, HelpText = "Set interval in ms at which pings are sent.")]
		public int Interval { get; set; }
		[Option('o', "timeout", Required = false, Default = 120, HelpText = "Set ping message timeout.")]
		public int Timeout { get; set; }
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
			Parser.Default.ParseArguments<Options>(args)
				.WithParsed(opts => RunOptionsAndReturnExitCode(opts))
				.WithNotParsed((errs) =>
				{
					Environment.Exit(-1);
				});
		}

		static void RunOptionsAndReturnExitCode(Options options)
		{
			string configPathAbs = null;

			try
			{
				configPathAbs = Path.GetFullPath(options.ConfigurationFilePath);
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

					foreach (var e in entries)
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

			int port = options.Port;
			if (port < 1 || port > UInt16.MaxValue)
			{
				Console.WriteLine($"Invalid UDP port number '{port}'");
				Environment.Exit(-1);
			}

			int interval = options.Interval;
			if (interval < 500 || interval > 60000)
			{
				Console.WriteLine($"Invalid interval time '{interval}'. Interval time is specified in ms and must be between 500 and 60000.");
				Environment.Exit(-1);
			}

			int timeout = options.Timeout;
			if (timeout < 10 || timeout > 10000)
			{
				Console.WriteLine($"Invalid interval time '{interval}'. Timeout is specified in ms and must be between 10 and 10000.");
				Environment.Exit(-1);
			}

			var targets = new List<IPAddress>();

			foreach (var t in options.Targets)
			{
				if (!IPAddress.TryParse(t, out IPAddress ip))
				{
					Console.WriteLine($"Invalid target IP address '{t}'");
					Environment.Exit(-1);
				}

				targets.Add(ip);
			}

			Console.WriteLine($"--------------------------------------------------------------------------------");
			Console.WriteLine($"OSC Pinger v0.1.0 by David Butler");
			Console.WriteLine($"--------------------------------------------------------------------------------");
			Console.WriteLine("");
			Console.WriteLine($"Sending pings to targets every {interval} ms with timeout of {timeout} ms");
			Console.WriteLine($"Sending OSC to UDP port {port} at IP(s) {string.Join(", ", targets.Select(t => t.ToString()))}");
			Console.WriteLine("");

			var oscService = new OscService(port, targets);
			var pingService = new PingService(pingTasks, timeout);
			pingService.OnPingResult += (s, e) =>
			{
				if (e.Ping < 0)
					Console.WriteLine($"{DateTime.Now} - No response from {e.Task.Label} ({e.Task.Address})");
				else
					Console.WriteLine($"{DateTime.Now} - Response from {e.Task.Label} ({e.Task.Address}) with ping time of {e.Ping}");

				oscService.SendStatus(e.Ping, e.Task.OscAddress);
			};

			while (true)
			{
				Console.WriteLine($"{DateTime.Now} - Sending pings...");

				pingService.SendPings();

				Console.WriteLine($"{DateTime.Now} - Finished sending pings");
				Console.WriteLine("");

				Thread.Sleep(interval);

			}
		}
	}
}
