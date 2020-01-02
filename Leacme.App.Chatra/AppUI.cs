// Copyright (c) 2017 Leacme (http://leac.me). View LICENSE.md for more information.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Leacme.Lib.Chatra;

namespace Leacme.App.Chatra {

	public class AppUI {

		private StackPanel rootPan = (StackPanel)Application.Current.MainWindow.Content;
		private Library lib = new Library();

		public AppUI() {

			var ownClb = App.TextBlock;
			ownClb.Text = "Chatra Client Offline";

			var ownClh = App.HorizontalStackPanel;
			ownClh.HorizontalAlignment = HorizontalAlignment.Center;
			ownClh.Children.Add(ownClb);

			var receiverClh = App.HorizontalStackPanel;
			receiverClh.HorizontalAlignment = HorizontalAlignment.Center;

			var receiverBl = App.TextBlock;
			receiverBl.Text = "Enter another IPV6 Chatra address to chat:";

			var receiverAddr = App.TextBox;
			receiverAddr.Width = 670;
			receiverAddr.Watermark = "http://[1234:567:abcd:ef12:1212:3456:cdef:8765]:40180";

			receiverClh.Children.Add(receiverBl);
			receiverClh.Children.Add(receiverAddr);

			var outpBox = App.TextBox;
			outpBox.IsReadOnly = true;
			outpBox.Width = 900;
			outpBox.Height = 350;

			var obsColl = Observable.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
				zzz => ((ObservableCollection<Message>)lib.ReceivedMessages).CollectionChanged += zzz,
				zzz => ((ObservableCollection<Message>)lib.ReceivedMessages).CollectionChanged -= zzz);
			outpBox[!TextBlock.TextProperty] = obsColl.Select((zzz, zzzz) => outpBox.Text + "\n" + string.Join("\n", zzz.EventArgs.NewItems.Cast<Message>().Select(zzzzz => zzzzz.TimeStamp.ToShortTimeString() + " - " + zzzzz.Sender + " says:\n" + "    " + zzzzz.MessageText))).ToBinding();

			var msgPan = App.HorizontalFieldWithButton;
			msgPan.holder.HorizontalAlignment = HorizontalAlignment.Center;
			msgPan.label.Text = "Compose message:";
			msgPan.field.Width = 670;
			msgPan.button.Content = "Send";

			msgPan.button.Click += async (z, zz) => {
				if (!string.IsNullOrWhiteSpace(receiverAddr.Text) && Uri.IsWellFormedUriString(receiverAddr.Text, UriKind.Absolute) && !string.IsNullOrWhiteSpace(msgPan.field.Text)) {
					var resp = await lib.SendMessageAsync(new Uri(receiverAddr.Text), msgPan.field.Text);
					if (resp.StatusCode.Equals(HttpStatusCode.OK)) {
						outpBox.Text = outpBox.Text + "\n" + DateTime.Now.ToShortTimeString() + " - You said:\n" + "    " + msgPan.field.Text;
					} else {
						outpBox.Text = outpBox.Text + "\n" + DateTime.Now.ToShortTimeString() + " - " + "Message not delivered.";
					}
					msgPan.field.Text = "";
				}
			};

			rootPan.Children.AddRange(new List<IControl> { ownClh, receiverClh, outpBox, msgPan.holder });

			Dispatcher.UIThread.InvokeAsync(async () => {

				try {
					await lib.StartChatraClientAsync();

					ownClb.Text = "Chatra Client Online, your IPV6 address is:";
					var ownCla = App.TextBox;
					ownCla.Background = Brushes.Green;
					ownCla.Foreground = Brushes.White;
					ownCla.Width = 670;
					ownCla.IsReadOnly = true;
					ownCla.Text = lib.GetCurrentChatClientAddress().ToString();

					ownClh.Children.Add(ownCla);

				} catch (Exception e) {
					ownClb.Text = ownClb.Text + " - " + e.Message;
					ownClb.Foreground = Brushes.Red;
					receiverAddr.IsEnabled = false;
					receiverAddr.Background = Brushes.LightGray;
					outpBox.IsEnabled = false;
					outpBox.Background = Brushes.LightGray;
					msgPan.field.Background = Brushes.LightGray;
					msgPan.field.IsEnabled = false;
					msgPan.button.IsEnabled = false;
				}
			});

		}
	}
}