// Copyright RoSchmi 2020 License Apache 2.0 Version 20. July 2020
// This is an adaption to TinyCLR of the "FixedTimeService" for NETMF posted by @eolson in GHI Codeshare in 2013
// The example runs on a GHI SC20260D Devboard and uses an ENC28 Ethernet Module on the micro Bus 1 socket
// For other boards the GPIO Pins for the ENC28 Module have to be adapted
//
// There is an option to automatically include DayLightSavingTime compensation, so that DateTime.Now gives a DayLightSavingTime corrected result
// This option should better not be used as DateTime.UtcNow gets also changed according to the DayLightSavingTimeOffset (which is wrong)
// It is better to retrieve the DST Offset through the now implemented method 'TimeService.GetDstOffset(DateTime.Now)' and add this offset to the DateTime.Now value


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
        private const string TimeServer_1 = "time1.google.com";
        private const string TimeServer_2 = "1.pool.ntp.org";

        private static int timeZoneOffset = 60;      // for Berlin: offest in minutes of your timezone to Greenwich Mean Time (GMT)

        // Europe DayLightSavingTimeSettings
        private static int dstOffset = 60; // 1 hour (Europe 2016)
        private static string dstStart = "Mar lastSun @2";
        private static string dstEnd = "Oct lastSun @3";

        //  USA DayLightSavingTimeSettings
        /*
        private static int dstOffset = 60; // 1 hour (US 2013)
        private static string dstStart = "Mar Sun>=8"; // 2nd Sunday March (US 2013)
        private static string dstEnd = "Nov Sun>=1"; // 1st Sunday Nov (US 2013)
        */

          enum AutoDayLightSavingSelection
        {
            UseAutoDst,
            DontUseAutoDst
        }

        private static bool timeServiceIsRunning = false;
        private static bool timeIsSet = false;

        private static bool linkReady = false;

        private static NetworkController networkController;

        private static GpioPin LED;
        
        static void Main()
        {
            // If wanted blink a LED to signal start of the program
            /*
            LED = GpioController.GetDefault().OpenPin(SC20260.GpioPin.PH6);
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

            SetAppTime(timeZoneOffset, AutoDayLightSavingSelection.DontUseAutoDst, TimeServer_1, TimeServer_2, dstStart, dstEnd, dstOffset);
            // Note: Better don't use AutoDstSelection.UseAutoDst  (sets DateTime.UtcNow to wrong values)

            Thread.Sleep(-1);
        }

        // Better don't use AutoDstSelection.UseAutoDst  (sets DateTime.UtcNow to wrong values)
        private static void SetAppTime(int pTimeZoneOffset, AutoDayLightSavingSelection pAutoDstSelection, string pTimeServer_1, string pTimeServer_2, string pDstStart, string pDstEnd, int pDstOffset)
        {
            // Set parameters of the TimeService
            TimeServiceSettings timeSettings = new TimeServiceSettings()
            {
                RefreshTime = 30,                                                                    // every 60 sec (for tests)      
                //RefreshTime = 2 * 60 * 60,                                                         // every 2 hours (2 x 60 x 60) default: 300000 sec               
                AutoDayLightSavings = pAutoDstSelection == AutoDayLightSavingSelection.UseAutoDst,   // Use automatic DayLightSavings timeshift or not
                                                                                                     // Better don't use AutoDstSelection.UseAutoDst  (sets DateTime.UtcNow to wrong values)
                ForceSyncAtWakeUp = true,
                Tolerance = 10000                                                                    // deviation may be up to 10 sec
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
             
                TimeService.SetDst(pDstStart, pDstEnd, pDstOffset);
                
                Debug.WriteLine("Starting Timeservice");
                TimeService.Start();
                Debug.WriteLine("Returned from Starting Timeservice");
                Thread.Sleep(100);
                if (DateTime.Now > new DateTime(2018, 7, 1))
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
                rtc.Now = DateTime.UtcNow;
            }
            else
            {
                Debug.WriteLine("No success to get time over internet");
                // Get time from Rtc
                if (rtc.IsValid)
                {
                    SystemTime.SetTime(rtc.Now, timeZoneOffset);
                }
            }

            
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
            if (e.AutoDayLightSavings)
            {
                Debug.WriteLine("SystemTime was checked! Dst corrected time is " + DateTime.Now);
            }
            else
            {
                Debug.WriteLine("SystemTime was checked! Not Dst corrected time is " + DateTime.Now);
            }
        }

        private static void TimeService_SystemTimeChanged(object sender, SystemTimeChangedEventArgs e)
        {
           

            if (e.AutoDayLightSavings)
            {
                Debug.WriteLine("Actual utc Time is " + DateTime.UtcNow + " Note: This time is not correct as AutoDayLightSaving was selected");
                Debug.WriteLine("SystemTime has changed. Actual local (Dst corrected) Time is " + DateTime.Now);
                Debug.WriteLine("TimeOffset from Dst corrected local Time to Utc is " + (DateTime.Now - DateTime.UtcNow.AddMinutes(-TimeService.GetDstOffset(DateTime.Now))).TotalMinutes.ToString("F0") + " minutes");
                Debug.WriteLine("DstOffset is " + TimeService.GetDstOffset(DateTime.Now).ToString("F0") + " minutes TimeZoneOffset is " + timeZoneOffset + " minutes");
            }
            else
            {
                Debug.WriteLine("Actual utc Time is " + DateTime.UtcNow);
                Debug.WriteLine("SystemTime has changed. Actual local (not Dst corrected) Time is " + DateTime.Now);
                Debug.WriteLine("TimeOffset from (not Dst corrected) local Time to Utc is " + (DateTime.Now - DateTime.UtcNow).TotalMinutes.ToString("F0") + " minutes");
                Debug.WriteLine("DstOffset is " + TimeService.GetDstOffset(DateTime.Now).ToString("F0") + " minutes TimeZoneOffset is " + timeZoneOffset + " minutes");
            }
        }

        static void DoTestEnc28()
        {
            networkController = NetworkController.FromName
                ("GHIElectronics.TinyCLR.NativeApis.ENC28J60.NetworkController");

            var networkInterfaceSetting = new EthernetNetworkInterfaceSettings();

            var networkCommunicationInterfaceSettings = new
                SpiNetworkCommunicationInterfaceSettings();

            var cs = GHIElectronics.TinyCLR.Devices.Gpio.GpioController.GetDefault().
           OpenPin(GHIElectronics.TinyCLR.Pins.SC20260.GpioPin.PG12);

            var settings = new GHIElectronics.TinyCLR.Devices.Spi.SpiConnectionSettings()
            {
                ChipSelectLine = cs,
                ClockFrequency = 4000000,
                Mode = GHIElectronics.TinyCLR.Devices.Spi.SpiMode.Mode0,           
                ChipSelectType = GHIElectronics.TinyCLR.Devices.Spi.SpiChipSelectType.Gpio,
                ChipSelectHoldTime = TimeSpan.FromTicks(10),
                ChipSelectSetupTime = TimeSpan.FromTicks(10)
            };

            networkCommunicationInterfaceSettings.SpiApiName =
                GHIElectronics.TinyCLR.Pins.SC20260.SpiBus.Spi3;

            networkCommunicationInterfaceSettings.GpioApiName =
                GHIElectronics.TinyCLR.Pins.SC20260.GpioPin.Id;

            networkCommunicationInterfaceSettings.SpiSettings = settings;           
            networkCommunicationInterfaceSettings.InterruptPin = GHIElectronics.TinyCLR.Devices.Gpio.GpioController.GetDefault().
               OpenPin(GHIElectronics.TinyCLR.Pins.SC20260.GpioPin.PG6);
            networkCommunicationInterfaceSettings.InterruptEdge = GpioPinEdge.FallingEdge;
            networkCommunicationInterfaceSettings.InterruptDriveMode = GpioPinDriveMode.InputPullUp;          
            networkCommunicationInterfaceSettings.ResetPin = GHIElectronics.TinyCLR.Devices.Gpio.GpioController.GetDefault().
                OpenPin(GHIElectronics.TinyCLR.Pins.SC20260.GpioPin.PI8);
            networkCommunicationInterfaceSettings.ResetActiveState = GpioPinValue.Low;

            networkInterfaceSetting.Address = new IPAddress(new byte[] { 192, 168, 1, 122 });
            networkInterfaceSetting.SubnetMask = new IPAddress(new byte[] { 255, 255, 255, 0 });
            networkInterfaceSetting.GatewayAddress = new IPAddress(new byte[] { 192, 168, 1, 1 });           
            networkInterfaceSetting.DnsAddresses = new IPAddress[] { new IPAddress(new byte[]
        { 75, 75, 75, 75 }), new IPAddress(new byte[] { 75, 75, 75, 76 }) };

       //     networkInterfaceSetting.DnsAddresses = new IPAddress[] { new IPAddress(new byte[]
       // { 192, 168, 1, 1 }), new IPAddress(new byte[] { 75, 75, 75, 76 }) };


            networkInterfaceSetting.MacAddress = new byte[] { 0x00, 0x4, 0x00, 0x00, 0x00, 0x00 };
            networkInterfaceSetting.DhcpEnable = true;
            networkInterfaceSetting.DhcpEnable = true;
            

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

