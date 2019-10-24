using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using OscCore;

namespace Imp.OscPinger
{
	public class OscService
	{
		readonly UdpClient _client = new UdpClient();
		readonly int _port;
		readonly IReadOnlyList<IPAddress> _targets;
		

		public OscService(int port, IEnumerable<IPAddress> targets)
		{
			_port = port;
			_targets = targets.ToList();

			if (_targets.Any(t => t.GetAddressBytes().Any(b => b == 255)))
				_client.EnableBroadcast = true;
		}

		public void SendStatus(int ping, string oscAddress)
		{
			if (ping < 0)
			{
				var m = new OscMessage(Path.Combine(oscAddress, "available"), 0);
				var data = m.ToByteArray();

				foreach (var t in _targets)
					_client.Send(data, data.Length, new IPEndPoint(t, _port));
			}
			else
			{
				var m1 = new OscMessage(Path.Combine(oscAddress, "available"), 1);
				var data1 = m1.ToByteArray();

				var m2 = new OscMessage(Path.Combine(oscAddress, "pingtime"), ping);
				var data2 = m2.ToByteArray();

				foreach (var t in _targets)
				{
					var ep = new IPEndPoint(t, _port);
					_client.Send(data1, data1.Length, ep);
					_client.Send(data2, data2.Length, ep);
				}
			}
		}
	}
}
