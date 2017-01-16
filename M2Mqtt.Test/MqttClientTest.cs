using NUnit.Framework;
using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
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

		[Test()]
		public void StopsItsActivityAfterFailedAttemptToConnect_Issue6()
		{
			var channel = Substitute.For<IMqttNetworkChannel>();
			channel.When((obj) => { obj.Receive(Arg.Any<byte[]>()); }).Do((obj) => { throw new IOException("channel error"); });

			var client = new MqttClient(channel);

			Assert.That(() => { client.Connect("client1"); }, Throws.Exception);

			channel.Received(1).Connect(); // make sure the client used mocked channel
			channel.ClearReceivedCalls();

			Thread.Sleep(TimeSpan.FromSeconds(1)); // give it some time to accumulate Receive calls when defective

			channel.DidNotReceive().Receive(Arg.Any<byte[]>());
		}

        class MessageStream
        {
            private byte[] message;
			private int provided;

			public MessageStream(MqttProtocolVersion v, MqttMsgBase m)
			{
				message = m.GetBytes((byte)v);
				provided = 0;
			}

			public int Read(byte[] buffer)
			{
				var filled = 0;
				var left = message.Length - provided;
				if (left > 0)
				{
					filled = Math.Min(buffer.Length, left);
					Array.Copy(message, provided, buffer, 0, filled);
					provided += filled;
				}
				return filled;
			}
		}

		[Test()]
		public void StopsItsActivityAfterConnectAttemptRefused_Issue22()
		{
			var channel = Substitute.For<IMqttNetworkChannel>();
			var client = new MqttClient(channel);

			var connectionRefusedMessage = new MqttMsgConnack() { ReturnCode = MqttMsgConnack.CONN_REFUSED_NOT_AUTHORIZED };
			var messageStream = new MessageStream(client.ProtocolVersion, connectionRefusedMessage);

			int calls = 0;
			channel.Receive(Arg.Any<byte[]>()).Returns((obj) =>
			{
				++calls;
				return messageStream.Read(obj.Arg<byte[]>()); // returns 0 when EOS reached which will also signal connection dropped
			});

			client.Connect("client1").ShouldEqual(connectionRefusedMessage.ReturnCode);

			channel.Received(1).Connect(); // make sure the client used mocked channel
			calls = 0; // clear number of Receive calls 

			Thread.Sleep(TimeSpan.FromSeconds(1)); // give it some time to accumulate Receive calls when defective

			calls.ShouldEqual(0);
		}

        [Test()]
		public void StopsItsActivityImmediatelyWhenConnectionDropped_Issue13()
		{
			var channel = Substitute.For<IMqttNetworkChannel>();
			var client = new MqttClient(channel);

			var connectionAcceptedMessage = new MqttMsgConnack() { ReturnCode = MqttMsgConnack.CONN_ACCEPTED };
			var messageStream = new MessageStream(client.ProtocolVersion, connectionAcceptedMessage);

			int calls = 0;
			channel.Receive(Arg.Any<byte[]>()).Returns((obj) =>
			{
				++calls;
				int readBytes = 0;
				if (messageStream != null)
				{
					readBytes = messageStream.Read(obj.Arg<byte[]>());
					if (readBytes == 0) // EOS reached
					{
						Thread.Sleep(TimeSpan.FromSeconds(0.5)); // gives the client some time to setup internals when connection accepted
						messageStream = null; // remove the stream so next calls will not use it
					}
				}
				return readBytes; // returning 0 will signal connection dropped
			});

			client.Connect("client1").ShouldEqual(connectionAcceptedMessage.ReturnCode);

			channel.Received(1).Connect(); // make sure the client used mocked channel
			calls = 0; // clear number of Receive calls 

			Thread.Sleep(TimeSpan.FromSeconds(1)); // give it some time to accumulate Receive calls when defective

			calls.ShouldEqual(0);
		}
	}
}
