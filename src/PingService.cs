using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;

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

		public PingService(IEnumerable<PingTask> pingTasks)
		{
			_pingTasks = pingTasks.ToList();
		}

		public void SendPings()
		{
			foreach(var t in _pingTasks)
			{
				Ping pingSender = new Ping();
				PingOptions options = new PingOptions();

				// Use the default Ttl value which is 128,
				// but change the fragmentation behavior.
				options.DontFragment = true;

				// Create a buffer of 32 bytes of data to be transmitted.
				string data = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
				byte[] buffer = Encoding.ASCII.GetBytes(data);
				int timeout = 120;
				PingReply reply = pingSender.Send(t.Address, timeout, buffer, options);
				if (reply.Status == IPStatus.Success)
					OnPingResult?.Invoke(this, new PingResultArgs(t, (int)reply.RoundtripTime));
				else
					OnPingResult?.Invoke(this, new PingResultArgs(t, -1));
			}
		}

		public event EventHandler<PingResultArgs> OnPingResult;
	}
}
