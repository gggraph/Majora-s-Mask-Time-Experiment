using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Threading;
using System.IO;


namespace Majora_Clock
{
    public partial class Form1 : Form
    {
        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        private CheatEngineLibrary lib;
        public static int          resultLength = 0;
        public static bool         ready = false;

        public static DateTime      BaseDate;

        public Form1()
        {
            InitializeComponent();
            LoadEpochFile();
            button1.Enabled = false;
            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                Run();
            }).Start();

        }
        public void LoadEpochFile() 
        {
            if (File.Exists("epoch"))
            {
                byte[] data = File.ReadAllBytes("epoch");
                int y = BitConverter.ToInt32(data, 0);
                int m = BitConverter.ToInt32(data, 4);
                int d = BitConverter.ToInt32(data, 8);
                BaseDate = new DateTime(y,m,d);
                DateTime time = DateTime.Now;
                TimeSpan s = time.Subtract(BaseDate);
                WriteLog("[INFO] Time before doom : " + (3 - s.Days) + " days " + s.Hours + " hours " + s.Minutes +" minutes");
                return;
            }
            else 
                InitEpochFile();
        }
        public void InitEpochFile() 
        {
            BaseDate = DateTime.Now;
            List<byte> data = new List<byte>();
            data.AddRange(BitConverter.GetBytes(BaseDate.Year));
            data.AddRange(BitConverter.GetBytes(BaseDate.Month));
            data.AddRange(BitConverter.GetBytes(BaseDate.Day));
            File.WriteAllBytes("epoch", data.ToArray());
        }
        public void Run() 
        {
            // Load dll
            WriteLog("Loading .dll ...");
            lib = new CheatEngineLibrary();
            if ( !lib.Load()) 
            {
                WriteLog("[FAIL] ce-lib64.dll is missing...");
                return;
            }

            // Load the save
            WriteLog("Try loading Majora's Mask save....");
            WriteLog("Please open Majora's Mask ROM in Project64 as active window if it is not already...");
            SendKeyToWindow("Legend of Zelda - Majora's Mask", "{F7}");

            // Searching Majora's Mask proccess ...

            bool procFound = false;
            string processes;
            lib.iGetProcessList(out processes);
            foreach (string process in Regex.Split(processes, "\r\n"))
            {
                if (!process.Contains("Project64"))
                    continue;

                string pid = process;
                pid = pid.Substring(0, pid.IndexOf('-', 0));
                if (!pid.Equals(""))
                {
                    lib.iOpenProcess(pid);
                    lib.iInitMemoryScanner(Process.GetCurrentProcess().MainWindowHandle.ToInt32());
                    procFound = true;
                    break;

                }
            }
            if (!procFound) 
            {
                WriteLog("[FAIL] Project64 was not found in regex...");
                return;
            }

            WriteLog("Start searching similar values in memory region... Please wait...");
            Thread.Sleep(1000);
            // Empty scanner
            lib.NewScan();
            Thread.Sleep(1000);
            // Start a first scan. Search for value 16338 from the majora's save memory.
            lib.FirstScan( TScanOption.soExactValue,
                           TVariableType.vtWord,
                           "$0000000000000000",
                           "$7fffffffffffffff",
                           Tscanregionpreference.scanInclude,
                           Tscanregionpreference.scanDontCare,
                           Tscanregionpreference.scanExclude,
                           TFastScanMethod.fsmAligned,
                           false,
                           false,
                           "16338",
                           "",
                           "2");

            // Wait while resultLength is 0
            int sctr = 0;
            while ( resultLength == 0 && sctr < 59) { Thread.Sleep(1000); sctr++; }
            if ( resultLength == 0) 
            {
                WriteLog("[FAIL] No address found.");
                return;
            }
            WriteLog("Found " + resultLength + " addresses...");

            WriteLog("Searching valid clock pointers... Please wait...");
            Thread.Sleep(1000);
            // Read up to 200 values from the scan
            int m = resultLength;
            if (resultLength < m)
                m = resultLength;

            string minuteAddress1 = null;
            string dayAddress1 = null;
            string dayAddress2 = null;

            for (int i = 0; i < m; i++) 
            {
                string address, value;
                lib.iGetAddress(i, out address, out value);
                // check if we got out 
                if (!address.EndsWith("5E"))
                    continue;
                if (CheckMatchingSaveStates(address)) 
                {
                    minuteAddress1 = address;
                    int decimalAddress = int.Parse(address, System.Globalization.NumberStyles.HexNumber) + 0xA;
                    dayAddress1 = decimalAddress.ToString("X");
                    decimalAddress = int.Parse(dayAddress1, System.Globalization.NumberStyles.HexNumber) + 0x4;
                    dayAddress2 = decimalAddress.ToString("X");
                    WriteLog("[SUCCESS] Clock address has been found!");
                    WriteLog("Minute Address found at: " + minuteAddress1);
                    WriteLog("Day Address found at: " + dayAddress1);
                    break;
                }
            }

            if (minuteAddress1 == null || dayAddress1 == null)
            {
                WriteLog("[FAIL] Addresses cannot be found. Please try again or close this software...");
                return;
            }

            WriteLog("[WARNING] This will reboot Majora's Mask rom");
            WriteLog("[WARNING] Launch a save you want to play and click on RUN button!");
            SendKeyToWindow("Legend of Zelda - Majora's Mask", "{F1}");
            lib.iResetTable();

            button1.BeginInvoke((Action)delegate () { button1.Enabled = true; });
            while (!ready) { Thread.Sleep(1000);}

            // @ Load default mode which sync system clock to MM time
            SystemClockTimeMachine(dayAddress1, dayAddress2, minuteAddress1);
            
        }
        private void SystemClockTimeMachine(string dayAddress1, string dayAddress2, string minuteAddress1)
        {
            uint unixTimestamp = (uint)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;

            // @ Clear addresses and add manually clock addresses to work with.
            lib.iAddAddressManually(dayAddress1, TVariableType.vtByte);
            lib.iAddAddressManually(minuteAddress1, TVariableType.vtWord);
            lib.iAddAddressManually(dayAddress2, TVariableType.vtByte);
            
            DateTime time = DateTime.Now;
            int day = 1;
            int minute = 17874;
            int hour = 0;

            // @ Jump to current day at load.
            time = DateTime.Now;
            TimeSpan s = time.Subtract(BaseDate);
            day = s.Days + 1;
            if ( day != 1) 
            {
               
                SetDayWithCinematic(day-1);
                Thread.Sleep(1000);
            }

            while (true)
            {
                // Convert system time to now.
                time = DateTime.Now;
                minute = time.Hour * 2730 + (int)Math.Floor((decimal)(time.Minute * 46.5));
                s = time.Subtract(BaseDate);
                day = s.Days + 1;
                hour = (int)((minute / 1986) / 1.375f);
                lib.iSetValue(1, minute.ToString(), true);
                Thread.Sleep(100);
            }

        }
        public class TimeStamp
        {
            public int Day { get; set; }
            public int Minute { get; set; }
            public TimeStamp(int day, int minute)
            {
                this.Day = day;
                this.Minute = minute;
            }
        }
        private void CPUTimeMachine(string dayAddress1, string dayAddress2, string minuteAddress1, int CPUIMPACT = 20) 
        {
            PerformanceCounter cpuCounter;
            cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");

            lib.iAddAddressManually(dayAddress1, TVariableType.vtByte);
            lib.iAddAddressManually(minuteAddress1, TVariableType.vtWord);
            lib.iAddAddressManually(dayAddress2, TVariableType.vtByte);

            int day = 1;
            int minute = 17874; // 6am

            lib.iSetValue(0, day.ToString(), true);
            lib.iSetValue(1, minute.ToString(), true);
            lib.iSetValue(2, day.ToString(), true);

            int CPUMULTIPLICATOR = (int)CPUIMPACT / 10;
            int cpupower = (int)(cpuCounter.NextValue());
            while (true)
            {
                logbox.BeginInvoke((Action)delegate () { logbox.Text = ""; });
                CPUMULTIPLICATOR = (int)CPUIMPACT;
                cpupower = (int)(cpuCounter.NextValue());
                minute += (int)(cpupower / 4) * CPUMULTIPLICATOR;
                if (minute > 65538) { minute = 0; }
                lib.iSetValue(1, minute.ToString(), true);
                WriteLog("CPU CYCLING : " + cpupower.ToString() + "%");
                int hour = (int)((minute / 1986) / 1.375f);
                WriteLog("hour : " + hour.ToString());
                Thread.Sleep(100);
            }
        }
        private void SetDayWithCinematic(int daynumber) 
        {
            int minute = 50000;
            lib.iSetValue(0, daynumber.ToString(), true);
            lib.iSetValue(2, daynumber.ToString(), true);
            lib.iSetValue(1, minute.ToString(), true);
        }
        private void BatteryTimeMachine(string dayAddress1, string dayAddress2, string minuteAddress1)
        {
            // @ Use battery power setting the game time. When battery is at 1% it loads the doom day.
            lib.iAddAddressManually(dayAddress1, TVariableType.vtByte);
            lib.iAddAddressManually(minuteAddress1, TVariableType.vtWord);
            lib.iAddAddressManually(dayAddress2, TVariableType.vtByte);

            TimeStamp[] Times = new TimeStamp[101];
            int m = 17874;
            int d = 1;
            for (int i = 100; i > -1; i--)
            {
                Times[i] = new TimeStamp(d, m);
                m += 1986;
                if (m == 65538) { m = 0; } // aucun probleme au tour d'horloge...
                if (m == 17874) { d++; }   
            }

            PowerStatus pwr = SystemInformation.PowerStatus;
            int minute;
            int day;
            int hour;
            int lasthour = 0;
            bool setlastMorning = false ;
            int counter = 0;

            while (true)
            {
                int prct = (int)(pwr.BatteryLifePercent * 100);
                minute = Times[prct].Minute;
                day = Times[prct].Day;
                hour = (int)((minute / 1986) / 1.375f);

                if (hour == 17 && lasthour == 18)
                {
                    if (day == 3)
                    {
                        setlastMorning = true;
                    }
                    counter = 5;

                }
                if (hour == 5 && lasthour == 6)
                {
                    counter = 5;

                }
                if (Math.Abs(hour - lasthour) > 1)
                {
                    counter = 5;

                }
                if (counter != 0)
                {
                    counter--;


                    if (setlastMorning)
                    {
                        day = 2;
                        minute = 50000;
                    }

                    lib.iSetValue(0, day.ToString(), true); 
                    lib.iSetValue(2, day.ToString(), true);
                    if (counter == 0)
                    {
                        setlastMorning = false;
                    }
                }
                lasthour = hour;
                lib.iSetValue(1, minute.ToString(), true); 
                Thread.Sleep(1000);
            }

        }


        // Searching good pointer from known values
        private bool CheckMatchingSaveStates(string pointer)
        {
            // Offsets from given pointer
            int[] memsOffset = new int[] { 0xA, 0x15, 0x2A, 0x28, 0x26, 0x2C, 0x9C };
            // Expected values for each offset
            int[] expectedValues = new int[] { 0, 3, 6, 48, 48, 48, 10 };

            for (int i = 0; i < memsOffset.Length; i++)
            {
                int decimalAddress = int.Parse(pointer, System.Globalization.NumberStyles.HexNumber) + memsOffset[i];
                string hexAddress = decimalAddress.ToString("X");
                string result;
                // Read memory pointer using iProcessAddress
                if (i > 1 && i < 6)
                    lib.iProcessAddress(hexAddress, TVariableType.vtWord, true, false, 4, out result);
                else
                    lib.iProcessAddress(hexAddress, TVariableType.vtByte, true, false, 4, out result);

                uint value;
                bool success = uint.TryParse(result, System.Globalization.NumberStyles.AllowHexSpecifier, null, out value);
                // Return false if value do not match or parsing failed...
                if (success)
                {
                    if (value != expectedValues[i])
                        return false;
                }
                else
                    return false;

            }
            return true;
        }


        // Hook dll message which tell app how much items has been found....
        protected override void WndProc(ref Message m)
        {
            int size, i;
            if (m.Msg == 0x8000 + 2)
            {
                size = lib.iGetBinarySize();
                lib.iInitFoundList(TVariableType.vtDword, size, false, false, false, false);
                i = Math.Min((int)lib.iCountAddressesFound(), 10000000);
                resultLength = i;
            }
            else
            {
                base.WndProc(ref m);
            }
        }

        // Utilities
        public void SendKeyToWindow(string windowname, string keycommand) 
        {
            while ( true) 
            {
                if (GetActiveWindowTitle() == null)
                    continue;
                if (GetActiveWindowTitle().Contains(windowname))
                {
                    SendKeys.SendWait(keycommand);
                    break;
                }
                Thread.Sleep(1000);
            }
           
        }
        private string GetActiveWindowTitle()
        {
            const int nChars = 256;
            StringBuilder Buff = new StringBuilder(nChars);
            IntPtr handle = GetForegroundWindow();

            if (GetWindowText(handle, Buff, nChars) > 0)
            {
                return Buff.ToString();
            }
            return null;
        }

        delegate void SetTextCallback(string text);
        private void WriteLog(string s)
        {

            if (this.logbox.InvokeRequired)
            {
                SetTextCallback d = new SetTextCallback(WriteLog);
                this.Invoke(d, new object[] { s });
            }
            else
            {
                this.logbox.Text += s + "\r\n";
                this.logbox.SelectionStart = this.logbox.TextLength;
                this.logbox.ScrollToCaret();
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            ready = true;
        }
    }
 
}
