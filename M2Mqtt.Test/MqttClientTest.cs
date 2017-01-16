﻿using NUnit.Framework;
using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using uPLibrary.Networking.M2Mqtt;
using NSubstitute;

namespace M2Mqtt.Test
{
	[TestFixture()]
	public class MqttClientTest
	{
		[Test()]
		public void ConnectFailsImmediatelyWhenErrorsDetectedWhileReceivingOnChannel()
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

        [Test()]
		public void ReceivingErrorOnChannelForwardedWhenConnectFails_Issue27()
		{
			var channelErrorMessage = "channel error";

			var channel = Substitute.For<IMqttNetworkChannel>();
			channel.When((obj) => { obj.Receive(Arg.Any<byte[]>()); }).Do((obj) => { throw new IOException(channelErrorMessage); });

			var client = new MqttClient(channel);

			Exception exception = null;
			try
			{
				client.Connect("client1");
			}
			catch (Exception ex)
			{
				exception = ex;
			}

			exception.ShouldNot(Be.Null);

			var messages = new List<string>() { exception.Message };
			if (exception.InnerException != null)
			{
				messages.Add(exception.InnerException.Message);
			}

			messages.ShouldContain(channelErrorMessage);
		}
	}
}
