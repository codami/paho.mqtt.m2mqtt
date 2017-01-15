using NUnit.Framework;
using System;
using System.IO;
using System.Diagnostics;
using uPLibrary.Networking.M2Mqtt;
using NSubstitute;

namespace M2Mqtt.Test
{
	[TestFixture()]
	public class MqttClientTest
	{
		[Test()]
		public void ClientFailsImmediatelyWhenErrorsDetectedWhileReceivingOnChannel()
		{
			var channel = Substitute.For<IMqttNetworkChannel>();
			channel.When((obj) => { obj.Receive(Arg.Any<byte[]>()); }).Do((obj) => { throw new IOException("channel error"); });

			var client = new MqttClient(channel);

			var stopwatch = new Stopwatch();
			stopwatch.Start();
			Assert.That(() => { client.Connect("client1"); }, Throws.Exception);
			stopwatch.Stop();

			stopwatch.Elapsed.Should(Be.AtMost(TimeSpan.FromSeconds(1))); // immediately = 1s ;)
		}
	}
}
