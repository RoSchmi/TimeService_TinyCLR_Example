// Copyright RoSchmi 2020 License Apache 2.0
// This is an adaption to TinyCLR of the "FixedTimeService" for NETMF posted by @eolson in GHI Codeshare in 2013
// The example runs on a GHI SC20260D Devboard and uses an ENC28 Ethernet Module on the micro Bus 1 socket
// For other boards the GPIO Pins for the ENC28 Module have to be adapted


using System;
using System.Threading;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using GHIElectronics.TinyCLR.Devices.Gpio;
using GHIElectronics.TinyCLR.Devices.Rtc;
using GHIElectronics.TinyCLR.Pins;
using GHIElectronics.TinyCLR.Devices.Network;
using GHIElectronics.TinyCLR.Native;
using RoSchmi.TinyCLR.Time;


namespace TimeService_TinyCLR_Example
{
    class Program
    {
        private static bool timeServiceIsRunning = false;
        private static bool timeIsSet = false;

        private const string TimeServer_1 = "time1.google.com";
        private const string TimeServer_2 = "1.pool.ntp.org";

        private static int timeZoneOffset = 60;      // for Berlin: offest in minutes of your timezone to Greenwich Mean Time (GMT)

        private static bool linkReady = false;

        private static NetworkController networkController;
        
        static void Main()
        {
            // If wanted blink a LED to signal start of the program
            /*
            var LED = GpioController.GetDefault().OpenPin(SC20260.GpioPin.PH6);
            LED.SetDriveMode(GpioPinDriveMode.Output);
            for (int i = 0; i < 10; i++)
            {
                LED.Write(GpioPinValue.High);
                Thread.Sleep(600);
                LED.Write(GpioPinValue.Low);
                Thread.Sleep(600);
            }
            */

            DoTestEnc28();

            TimeService.SystemTimeChanged += TimeService_SystemTimeChanged;
            TimeService.SystemTimeChecked += TimeService_SystemTimeChecked;

            SetAppTime(timeZoneOffset, TimeServer_1, TimeServer_2);

            Thread.Sleep(-1);
        }

        public static void SetAppTime(int pTimeZoneOffset, string pTimeServer_1, string pTimeServer_2)
        {
            // Set parameters of the TimeService
            TimeServiceSettings timeSettings = new TimeServiceSettings()
            {
                RefreshTime = 60,                          // every 60 sec (for tests)      
                //RefreshTime = 21600,                         // every 6 hours (60 x 60 x 6) default: 300000 sec               
                AutoDayLightSavings = false,                 // We use our own timeshift calculation
                ForceSyncAtWakeUp = true,
                Tolerance = 10000                            // deviation may be up to 10 sec
            };


            int loopCounter = 1;
            while (loopCounter < 3)
            {

                IPAddress[] address = null;
                IPAddress[] address_2 = null;

                try
                {
                    address = System.Net.Dns.GetHostEntry(pTimeServer_1).AddressList;
                }
                catch { };

                try
                {
                    address_2 = System.Net.Dns.GetHostEntry(pTimeServer_2).AddressList;
                }
                catch { };


                try
                {
                    timeSettings.PrimaryServer = address[0].GetAddressBytes();
                }
                catch { };

                try
                {
                    timeSettings.AlternateServer = address_2[0].GetAddressBytes();
                }
                catch { };

                TimeService.Settings = timeSettings;

                TimeService.SetTimeZoneOffset(pTimeZoneOffset);

                Debug.WriteLine("Starting Timeservice");
                TimeService.Start();
                Debug.WriteLine("Returned from Starting Timeservice");
                Thread.Sleep(100);
                if (DateTime.Now > new DateTime(2016, 7, 1))
                {
                    timeServiceIsRunning = true;
                    Debug.WriteLine("Timeserver intialized on try: " + loopCounter);
                    Debug.WriteLine("Synchronization Interval = " + timeSettings.RefreshTime);
                    break;
                }
                else
                {
                    timeServiceIsRunning = false;
                    Debug.WriteLine("Timeserver could not be intialized on try: " + loopCounter);
                }
                loopCounter++;
            }

            var rtc = RtcController.GetDefault();
            if (timeServiceIsRunning)
            {
                rtc.Now = DateTime.Now;
            }
            else
            {
                Debug.WriteLine("No success to get time over internet");
                // Get time from Rtc
                if (rtc.IsValid)
                {
                    SystemTime.SetTime(rtc.Now);
                }
            }

            // SystemTime.SetTime(new DateTime(2000, 1, 1, 1, 1, 1));  //For tests, to see what happens when wrong date

            if (DateTime.Now < new DateTime(2016, 7, 1))
            {
                timeIsSet = false;

                Debug.WriteLine("Restarting Program");
                
                Power.Reset(true);               
            }
            else
            {
                Debug.WriteLine("Could get Time from Internet or RealTime Clock");
                timeIsSet = true;
            }
        }

        private static void TimeService_SystemTimeChecked(object sender, SystemTimeChangedEventArgs e)
        {
            Debug.WriteLine("SystemTime was checked! " + DateTime.Now);         
        }

        private static void TimeService_SystemTimeChanged(object sender, SystemTimeChangedEventArgs e)
        {
            Debug.WriteLine("SystemTime has changed. Actual local Time is " + DateTime.Now);
            Debug.WriteLine("Actual utc Time is " + DateTime.UtcNow);
        }

        static void DoTestEnc28()
        {
            networkController = NetworkController.FromName
                ("GHIElectronics.TinyCLR.NativeApis.ENC28J60.NetworkController");

            var networkInterfaceSetting = new EthernetNetworkInterfaceSettings();

            var networkCommunicationInterfaceSettings = new
                SpiNetworkCommunicationInterfaceSettings();

            var settings = new GHIElectronics.TinyCLR.Devices.Spi.SpiConnectionSettings()
            {
                ChipSelectLine = SC20260.GpioPin.PG12,
                ClockFrequency = 4000000,
                Mode = GHIElectronics.TinyCLR.Devices.Spi.SpiMode.Mode0,
                DataBitLength = 8,
                ChipSelectType = GHIElectronics.TinyCLR.Devices.Spi.SpiChipSelectType.Gpio,
                ChipSelectHoldTime = TimeSpan.FromTicks(10),
                ChipSelectSetupTime = TimeSpan.FromTicks(10)
            };

            networkCommunicationInterfaceSettings.SpiApiName =
                GHIElectronics.TinyCLR.Pins.SC20260.SpiBus.Spi3;

            networkCommunicationInterfaceSettings.GpioApiName =
                GHIElectronics.TinyCLR.Pins.SC20260.GpioPin.Id;

            networkCommunicationInterfaceSettings.SpiSettings = settings;
            networkCommunicationInterfaceSettings.InterruptPin = SC20260.GpioPin.PG6;
            networkCommunicationInterfaceSettings.InterruptEdge = GpioPinEdge.FallingEdge;
            networkCommunicationInterfaceSettings.InterruptDriveMode = GpioPinDriveMode.InputPullUp;
            networkCommunicationInterfaceSettings.ResetPin = SC20260.GpioPin.PI8;
            networkCommunicationInterfaceSettings.ResetActiveState = GpioPinValue.Low;

            networkInterfaceSetting.Address = new IPAddress(new byte[] { 192, 168, 1, 122 });
            networkInterfaceSetting.SubnetMask = new IPAddress(new byte[] { 255, 255, 255, 0 });
            networkInterfaceSetting.GatewayAddress = new IPAddress(new byte[] { 192, 168, 1, 1 });
            networkInterfaceSetting.DnsAddresses = new IPAddress[] { new IPAddress(new byte[]
        { 75, 75, 75, 75 }), new IPAddress(new byte[] { 75, 75, 75, 76 }) };

            networkInterfaceSetting.MacAddress = new byte[] { 0x00, 0x4, 0x00, 0x00, 0x00, 0x00 };
            networkInterfaceSetting.IsDhcpEnabled = true;
            networkInterfaceSetting.IsDynamicDnsEnabled = true;

            networkInterfaceSetting.TlsEntropy = new byte[] { 0, 1, 2, 3 };

            networkController.SetInterfaceSettings(networkInterfaceSetting);
            networkController.SetCommunicationInterfaceSettings
                (networkCommunicationInterfaceSettings);

            networkController.SetAsDefaultController();

            networkController.NetworkAddressChanged += NetworkController_NetworkAddressChanged;
            networkController.NetworkLinkConnectedChanged +=
                NetworkController_NetworkLinkConnectedChanged;

            networkController.Enable();

            while (linkReady == false) ;

            System.Diagnostics.Debug.WriteLine("Network is ready to use");
        }

        private static void NetworkController_NetworkLinkConnectedChanged
            (NetworkController sender, NetworkLinkConnectedChangedEventArgs e)
        {
            // Raise event connect/disconnect
        }

        private static void NetworkController_NetworkAddressChanged
            (NetworkController sender, NetworkAddressChangedEventArgs e)
        {
            var ipProperties = sender.GetIPProperties();
            var address = ipProperties.Address.GetAddressBytes();

            linkReady = address[0] != 0;
        }
    }
}

