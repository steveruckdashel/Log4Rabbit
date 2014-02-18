using System;
using System.Diagnostics;
using RabbitMQ.Client;
using log4net.Core;
using log4net.Util;
using log4net.Layout;
using System.Text;
using System.IO;

namespace log4net.Appender
{
	public class RabbitMQAppender : AppenderSkeleton
	{
		private ConnectionFactory _connectionFactory;
		private WorkerThread<LoggingEvent> _worker;

		public RabbitMQAppender()
		{
			HostName = "localhost";
			VirtualHost = "/";
			UserName = "guest";
			Password = "guest";
			RequestedHeartbeat = 0;
			Port = 5672;
			Exchange = "logs";
			RoutingKey = "";
			FlushInterval = 5;
		}

		/// <summary>
		/// Default to "localhost"
		/// </summary>
		public string HostName { get; set; }

		/// <summary>
		/// Default to "/"
		/// </summary>
		public string VirtualHost { get; set; }

		/// <summary>
		/// Default to "guest"
		/// </summary>
		public string UserName { get; set; }

		/// <summary>
		/// Default to "guest"
		/// </summary>
		public string Password { get; set; }

		/// <summary>
		/// Value in seconds, default to 0 that mean no heartbeat
		/// </summary>
		public ushort RequestedHeartbeat { get; set; }

		/// <summary>
		/// Default to 5672
		/// </summary>
		public int Port { get; set; }

		/// <summary>
		/// Default to "logs"
		/// </summary>
		public string Exchange { get; set; }

		/// <summary>
		/// Default to ""
		/// </summary>
		public string RoutingKey { get; set; }

		/// <summary>
		/// Seconds to wait between message send. Default to 5 seconds
		/// </summary>
		public int FlushInterval { get; set; }

		protected override void OnClose()
		{
			_worker.Dispose();
			_worker = null;
		}

		protected override void Append(LoggingEvent loggingEvent)
		{
			loggingEvent.Fix = FixFlags.All;
			_worker.Enqueue(loggingEvent);
		}

		public override void ActivateOptions()
		{
			_connectionFactory = new ConnectionFactory {
				HostName = HostName, 
				VirtualHost = VirtualHost, 
				UserName = UserName, 
				Password = Password, 
				RequestedHeartbeat = RequestedHeartbeat, 
				Port = Port
			};
			_worker = new WorkerThread<LoggingEvent>(string.Concat("Worker for log4net appender '", Name, "'"), TimeSpan.FromSeconds(FlushInterval), Process);
		}

		public void Process(LoggingEvent[] logs)
		{
			Stopwatch sw = Stopwatch.StartNew();
			try
			{
                byte[] body;
                foreach (LoggingEvent loggingEvent in logs)
                {
                    var sb = new StringBuilder();
                    using (var sr = new StringWriter(sb))
                    {
                        if (loggingEvent == null)
                        {
                            continue;
                        }
                        Layout.Format(sr, loggingEvent);

                        if (loggingEvent.ExceptionObject != null)
                            sr.Write(loggingEvent.GetExceptionString());

                        body = Encoding.UTF8.GetBytes(sr.ToString());
                    }
				    using (IConnection connection = _connectionFactory.CreateConnection())
				    {
					    using (IModel model = connection.CreateModel())
					    {
						    IBasicProperties basicProperties = model.CreateBasicProperties();
						    basicProperties.ContentType = Layout.ContentType;
						    basicProperties.DeliveryMode = 2;
						    model.BasicPublish(Exchange, RoutingKey, basicProperties, body);
					    }
				    }                
                }



			}
			catch (Exception e)
			{
				LogLog.Debug(typeof(RabbitMQAppender), "Exception comunicating with rabbitmq", e);
			}
			finally
			{
				LogLog.Debug(typeof(RabbitMQAppender), string.Concat("process completed, took ", sw.Elapsed));
			}
		}
	}
}