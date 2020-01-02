// Copyright (c) 2017 Leacme (http://leac.me). View LICENSE.md for more information.
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Leacme.Lib.Chatra {

	public class Library {

		public IWebHost CurrentChatraClient { get; private set; } = null;
		public static TimeSpan Timeout = new TimeSpan(0, 0, 3);
		public ObservableCollection<Message> ReceivedMessages { get; } = new ObservableCollection<Message>();

		public Library() {

		}

		/// <summary>
		/// Queries the online service to retrieve your IPV6 address.
		/// </summary>
		/// <param name="hostWithIPV6Response"></param>
		/// <returns>The IPV6 address or throws NotSupportedException</returns>
		public async Task<IPAddress> GetIPV6(string hostWithIPV6Response = "https://api6.my-ip.io/ip") {
			var rsp = await new HttpClient() { Timeout = Timeout }.GetStringAsync(hostWithIPV6Response);
			if (!string.IsNullOrWhiteSpace(rsp) && IPAddress.TryParse(rsp, out var ipv6Addr)) {
				return ipv6Addr;
			} else {
				throw new NotSupportedException("Unable to get your IPV6 address - check network.");
			}
		}

		/// <summary>
		///	Starts the local Chatra client to send and receive messages from other clients.
		/// /// </summary>
		/// <param name="port">A configurable local port to run the client on.</param>
		/// <returns></returns>
		public async Task StartChatraClientAsync(int port = 40180) {
			if (this.CurrentChatraClient != null) {
				throw new ApplicationException("Chatra client already started.");
			}
			var ipv6 = await GetIPV6();

			CurrentChatraClient = WebHost.CreateDefaultBuilder().
								ConfigureAppConfiguration((z, zz) => { })
									.ConfigureServices((z, zz) => {
										zz.AddMvcCore(options => options.EnableEndpointRouting = false).AddControllersAsServices();
										zz.AddTransient(ctx => new OmniController(this));
									})
									.ConfigureKestrel((z, zz) => {
										zz.Configure().Options.Listen(ipv6, port);
										zz.AllowSynchronousIO = true;
									})
									.ConfigureLogging((z, zz) => {
									})
									.Configure(z => {
										z.UseMvc();
									})
									.Build();
			await CurrentChatraClient.StartAsync();
		}

		/// <summary>
		/// Once the Chatra client has been started, can query current local address to share with others.
		/// /// </summary>
		/// <returns>The local Chatra Client address</returns>
		public Uri GetCurrentChatClientAddress() {
			if (CurrentChatraClient == null) {
				throw new ApplicationException("Chatra client is not running - start it first.");
			} else {
				return new Uri(CurrentChatraClient.ServerFeatures.Get<IServerAddressesFeature>().Addresses.First());
			}
		}

		/// <summary>
		/// Send a message to another running Chatra Client by their IPV6 address.
		/// /// </summary>
		/// <param name="receiverIPV6Address">The IPV6 address of another client.</param>
		/// <param name="message">Message to send.</param>
		/// <returns>HTTP Response containing a code, with code 200 if the other client has received the message.</returns>
		public async Task<HttpResponseMessage> SendMessageAsync(Uri receiverIPV6Address, string message) {
			if (CurrentChatraClient == null) {
				throw new ApplicationException("Chatra client is not running - start it first.");
			} else {
				HttpResponseMessage response = null;
				try {
					var msnger = new HttpClient() { Timeout = Timeout };
					msnger.DefaultRequestHeaders.Host = GetCurrentChatClientAddress().Host + ":" + GetCurrentChatClientAddress().Port;

					response = await msnger.PostAsync(
						receiverIPV6Address, new StringContent(message));
				} catch (OperationCanceledException) {
					return new HttpResponseMessage() { StatusCode = HttpStatusCode.GatewayTimeout };
				} catch (HttpRequestException) {
					return new HttpResponseMessage() { StatusCode = HttpStatusCode.BadGateway };
				}
				return response;
			}
		}
	}

	/// <summary>
	/// The controller to receive the HTTP messages from other Chatra clients.
	/// /// </summary>
	public class OmniController : Controller {
		private Library library;

		public OmniController(Library library) {
			this.library = library;
		}

		[Route("{*.}")]
		public IActionResult Omni() {
			string msg = "";
			try {
				using (StreamReader reader = new StreamReader(Request.Body)) {
					msg = reader.ReadToEnd();
				}
			} catch {
				return BadRequest();
			}

			var hostHeader = Request.Headers["Host"];
			if (hostHeader.Any() && !string.IsNullOrWhiteSpace(msg)) {
				library.ReceivedMessages.Add(new Message(new Uri("http://" + hostHeader.First()), DateTime.Now, msg));
			} else {
				return BadRequest();
			}
			return Ok();
		}
	}

	/// <summary>
	/// The message consisting of the sender, the date sent, and the message text.
	/// /// </summary>
	public class Message {
		public Uri Sender { get; set; }
		public DateTime TimeStamp { get; set; }
		public string MessageText { get; set; }
		public Message(Uri Sender, DateTime TimeStamp, string MessageText) {
			this.Sender = Sender;
			this.TimeStamp = TimeStamp;
			this.MessageText = MessageText;
		}
	}
}