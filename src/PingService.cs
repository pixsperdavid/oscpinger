using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace Imp.OscPinger
{
	public struct PingTask
	{
		public PingTask(string label, IPAddress address, string oscAddress)
		{
			Label = label;
			Address = address;
			OscAddress = oscAddress;
		}

		public string Label { get; }
		public IPAddress Address { get; }
		public string OscAddress { get; }
	}

	public class PingResultArgs : EventArgs
	{
		public PingResultArgs(PingTask task, int ping)
		{
			Task = task;
			Ping = ping;
		}

		public PingTask Task { get; }
		public int Ping { get; }
	}

	public class PingService
	{
		IReadOnlyList<PingTask> _pingTasks;
		int _timeout;

		public PingService(IEnumerable<PingTask> pingTasks, int timeout)
		{
			_pingTasks = pingTasks.ToList();
			_timeout = timeout;
		}

		public void SendPings()
		{
			var pingReplies = new List<Tuple<Task<PingReply>, PingTask>>();

			foreach(var t in _pingTasks)
			{
				Ping pingSender = new Ping();
				PingOptions options = new PingOptions
				{
					DontFragment = true
				};

				// Create a buffer of 32 bytes of data to be transmitted.
				byte[] buffer = Encoding.ASCII.GetBytes("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

				PingReply reply = pingSender.Send(t.Address, _timeout, buffer, options);
				

				pingReplies.Add(Tuple.Create(pingSender.SendPingAsync(t.Address, _timeout, buffer, options), t));
			}

			Task.WhenAll(pingReplies.Select(t => t.Item1)).Wait();

			foreach (var t in pingReplies)
			{
				if (t.Item1.IsCompletedSuccessfully && t.Item1.Result.Status == IPStatus.Success)
					OnPingResult?.Invoke(this, new PingResultArgs(t.Item2, (int)t.Item1.Result.RoundtripTime));
				else
					OnPingResult?.Invoke(this, new PingResultArgs(t.Item2, -1));
			}
		}

		public event EventHandler<PingResultArgs> OnPingResult;
	}
}
