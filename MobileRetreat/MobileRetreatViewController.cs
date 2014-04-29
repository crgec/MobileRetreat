using System;
using System.Drawing;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using MonoTouch.CoreBluetooth;
using MonoTouch.CoreLocation;
using MonoTouch.CoreFoundation;
using System.IO;
using MonoTouch.AVFoundation;

namespace MobileRetreat
{
	public partial class MobileRetreatViewController : UIViewController
	{
		private readonly NSUuid beaconId = new NSUuid("E2C56DB5-DFFB-48D2-B060-D0F5A71096E0");

		private const string beaconRegionName = "retreat";

		private AVAudioPlayer[] players = new AVAudioPlayer[4];

		CBPeripheralManager peripheralMgr;
		BTPeripheralDelegate peripheralDelegate;
		CLLocationManager locationMgr;

		static bool UserInterfaceIdiomIsPhone {
			get { return UIDevice.CurrentDevice.UserInterfaceIdiom == UIUserInterfaceIdiom.Phone; }
		}

		public MobileRetreatViewController ()
			: base (UserInterfaceIdiomIsPhone ? "MobileRetreatViewController_iPhone" : "MobileRetreatViewController_iPad", null)
		{
		}

		public override void DidReceiveMemoryWarning ()
		{
			// Releases the view if it doesn't have a superview.
			base.DidReceiveMemoryWarning ();
			
			// Release any cached data, images, etc that aren't in use.
		}

		public override void ViewDidLoad ()
		{
			base.ViewDidLoad ();
			
			// Perform any additional setup after loading the view, typically from a nib.
			var beaconRegion = new CLBeaconRegion (beaconId, beaconRegionName) {
				NotifyOnEntry = true,
				NotifyEntryStateOnDisplay = true,
				NotifyOnExit = true
			};

			if (!UserInterfaceIdiomIsPhone) {
				//power - the received signal strength indicator (RSSI) value (measured in decibels) of the beacon from one meter away
				var power = new NSNumber (-59);
				NSMutableDictionary peripheralData = beaconRegion.GetPeripheralData (power);
				peripheralDelegate = new BTPeripheralDelegate (peripheralData);
				peripheralMgr = new CBPeripheralManager (peripheralDelegate, DispatchQueue.DefaultGlobalQueue);
			} else {
				locationMgr = new CLLocationManager ();

				locationMgr.RegionEntered += (object sender, CLRegionEventArgs e) => {
					if (e.Region.Identifier == beaconRegionName) {
						UILocalNotification notification = new UILocalNotification () { AlertBody = "Shark Warning! No Swimming!" };
						UIApplication.SharedApplication.PresentLocationNotificationNow (notification);
					}
				};
				locationMgr.RegionLeft += (object sender, CLRegionEventArgs e) => {
					if (e.Region.Identifier == beaconRegionName) {
						UILocalNotification notification = new UILocalNotification () { AlertBody = "Looks like it's safe to swim." };
						UIApplication.SharedApplication.PresentLocationNotificationNow (notification);
					}
				};

				CLProximity previousProximity = CLProximity.Unknown;
				locationMgr.DidRangeBeacons += (object sender, CLRegionBeaconsRangedEventArgs e) => {
					if (e.Beacons.Length > 0){
						var beacon = e.Beacons[0];
						if (beacon.Proximity == previousProximity) return;
						PlaySound(beacon.Proximity);
						beacon.
						switch(beacon.Proximity){
						case CLProximity.Unknown:
						case CLProximity.Far:
							this.statusMessage.Text = "Is it true that most people get attacked by sharks in three feet of water about ten feet from the beach?";
							this.View.BackgroundColor = UIColor.FromRGB(238, 214, 175);
							break;
						case CLProximity.Near:
							this.statusMessage.Text = "You're gonna need a bigger boat.";
							this.View.BackgroundColor = UIColor.FromRGB(206, 223, 239);
							break;
						case CLProximity.Immediate:
							this.statusMessage.Text = "It was nice to know ya.";
							this.View.BackgroundColor = UIColor.FromRGB(138, 7, 7);
							break;
						}
						previousProximity = beacon.Proximity;
					}
				};

				locationMgr.StartMonitoring (beaconRegion);
				locationMgr.StartRangingBeacons (beaconRegion);
			}
		}

		void PlaySound (CLProximity proximity)
		{
			string loopFilename = null;
			int playerIndex =  -1,
				numberOfLoops = -1;
			float volume = 1f;

			switch(proximity){
			case CLProximity.Far:
				loopFilename = "Jaws2.mp3";
				playerIndex = 0;
				volume = 5f;
				break;
			case CLProximity.Near:
				loopFilename = "Jaws6-loopable.mp3";
				playerIndex = 1;
				break;
			case CLProximity.Immediate:
				loopFilename = "Jaws7.mp3";
				numberOfLoops = 0;
				playerIndex = 2;
				break;
			}

			if (!string.IsNullOrEmpty (loopFilename)) {
				if (players[playerIndex] == null) {
					var file = Path.Combine ("sounds", loopFilename);
					var soundUrl = NSUrl.FromFilename (file);
					players[playerIndex] = AVAudioPlayer.FromUrl (soundUrl);
				}
				var player = players [playerIndex];
				if (player != null) {
					player.NumberOfLoops = numberOfLoops;
					player.Volume = volume;
					player.PrepareToPlay ();
					player.Play ();
				}
			}
			for (int i = 0; i < players.Length; i++) {
				if (i != playerIndex) {
					if(players[i] != null)
						players [i].Stop ();
				}
			}
		}

		class BTPeripheralDelegate : CBPeripheralManagerDelegate
		{
			private NSMutableDictionary beaconPeripheralRegion;

			internal BTPeripheralDelegate(NSMutableDictionary beaconPeripheralRegion)
			{
				this.beaconPeripheralRegion = beaconPeripheralRegion;
			}

			public override void StateUpdated (CBPeripheralManager peripheral)
			{
				if (peripheral.State == CBPeripheralManagerState.PoweredOn) {
					Console.WriteLine ("powered on");
					peripheral.StartAdvertising (beaconPeripheralRegion);
				} else if (peripheral.State == CBPeripheralManagerState.PoweredOff) {
					Console.WriteLine ("powered off");
					peripheral.StopAdvertising ();
				} else if (peripheral.State == CBPeripheralManagerState.Unsupported) {
					Console.WriteLine ("unsupported");
				}
			}

		}
	}
}

