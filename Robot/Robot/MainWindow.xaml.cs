using System;
using System.Windows;
using ANYCHATAPI;
using System.IO.Ports;
using System.Windows.Interop;
using SharpDX.XInput;
using System.Windows.Threading;

namespace Robot
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Variables
        
        // Server information
        public string address = "demo.anychat.cn";
        public int port = 8906;
        public int roomNum = 151001;

        // User information
        public string userName = "robot";
        public string userPassword = "robot";
        private int myUserID = -1;

        static public PresentationSource source;

        // Local camera configuration
        public int localCamIndex = 0;
        static public double localCamLeftP = 0.006;
        static public double localCamRightP = 0.3285;
        static public double localCamTopP = 0.732;
        static public double localCamBottomP = 0.955;

        public int localCamLeft = 0;
        public int localCamRight = 0;
        public int localCamTop = 0;
        public int localCamBottom = 0;

        // Remote camera configuration
        public int remoteCamIndex = 0;
        static public double remoteCamLeftP = 0.006;
        static public double remoteCamRightP = 0.992;
        static public double remoteCamTopP = 0.01;
        static public double remoteCamBottomP = 0.72;

        public int remoteCamLeft = 0;
        public int remoteCamRight = 0;
        public int remoteCamTop = 0;
        public int remoteCamBottom = 0;

        // Serial port configuration, port name could be different on different device
        static public SerialPort HWPort = new SerialPort();
        public string portName = "COM5";
        public int baudRate = 9600;
        public int dataBits = 8;

        public Controller stick;

        // Timer
        DispatcherTimer stickTimer = new DispatcherTimer();

        #endregion

        #region Constructor

        public MainWindow()
        {
            InitializeComponent();
        }

        #endregion

        #region Window Events

        // Load window
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Open serial port
            try
            {
                Serial.Init(HWPort, portName, baudRate, dataBits);
                ShowInfo("Success open serial port. ");
                serialTxt.Text = "Open";
            }
            catch (Exception ex)
            {
                ShowInfo("Serial port error: " + ex.Message.ToString());
            }

            // Init web video
            try
            {
                IntPtr windowHdl = new WindowInteropHelper(Application.Current.MainWindow).Handle;

                SystemSetting.Text_OnReceive = new TextReceivedHandler(ReceivedCmd); // Text callback
                WebVideo.Init(windowHdl);
                ShowInfo("Video server has been initialised. ");

                int ret = AnyChatCoreSDK.Connect(address, port);
                ret = AnyChatCoreSDK.Login(userName, userPassword, 0);
                HwndSource hwndSource = HwndSource.FromHwnd(windowHdl);
                hwndSource.AddHook(new HwndSourceHook(WndProc));
                ShowInfo("Connecting to video server... ");
            }
            catch (Exception ex)
            {
                ShowInfo("Web video error: " + ex.Message.ToString());
            }

            // Fit camera stream to window size
            try
            {
                source = PresentationSource.FromVisual(this);
                if (source != null)
                {
                    localCamLeft = Convert.ToInt32(localCamLeftP * SystemParameters.WorkArea.Width * source.CompositionTarget.TransformToDevice.M11);
                    localCamRight = Convert.ToInt32(localCamRightP * SystemParameters.WorkArea.Width * source.CompositionTarget.TransformToDevice.M11);
                    localCamTop = Convert.ToInt32(localCamTopP * SystemParameters.WorkArea.Height * source.CompositionTarget.TransformToDevice.M22);
                    localCamBottom = Convert.ToInt32(localCamBottomP * SystemParameters.WorkArea.Height * source.CompositionTarget.TransformToDevice.M22);

                    remoteCamLeft = Convert.ToInt32(remoteCamLeftP * SystemParameters.WorkArea.Width * source.CompositionTarget.TransformToDevice.M11);
                    remoteCamRight = Convert.ToInt32(remoteCamRightP * SystemParameters.WorkArea.Width * source.CompositionTarget.TransformToDevice.M11);
                    remoteCamTop = Convert.ToInt32(remoteCamTopP * SystemParameters.WorkArea.Height * source.CompositionTarget.TransformToDevice.M22);
                    remoteCamBottom = Convert.ToInt32(remoteCamBottomP * SystemParameters.WorkArea.Height * source.CompositionTarget.TransformToDevice.M22);
                }
                else
                {
                    localCamLeft = Convert.ToInt32(localCamLeftP * SystemParameters.WorkArea.Width);
                    localCamRight = Convert.ToInt32(localCamRightP * SystemParameters.WorkArea.Width);
                    localCamTop = Convert.ToInt32(localCamTopP * SystemParameters.WorkArea.Height);
                    localCamBottom = Convert.ToInt32(localCamBottomP * SystemParameters.WorkArea.Height);

                    remoteCamLeft = Convert.ToInt32(remoteCamLeftP * SystemParameters.WorkArea.Width);
                    remoteCamRight = Convert.ToInt32(remoteCamRightP * SystemParameters.WorkArea.Width);
                    remoteCamTop = Convert.ToInt32(remoteCamTopP * SystemParameters.WorkArea.Height);
                    remoteCamBottom = Convert.ToInt32(remoteCamBottomP * SystemParameters.WorkArea.Height);
                }
            }
            catch (Exception ex)
            {
                ShowInfo("Camera error: " + ex.Message.ToString());
            }         

            // Init wheelchair, configuration need to be changed
            try
            {
                Wheelchair.Init();
                ShowInfo("Wheelchair has been initialised. ");
            }
            catch (Exception ex)
            {
                ShowInfo("Wheelchair error: " + ex.Message.ToString());
            }

            // Init Joystick
            try
            {
                stick = new Controller(UserIndex.One);
                if (stick.IsConnected)
                {
                    StickStateTxt.Text = "Connected";
                    stickTimer.Tick += new EventHandler(StickTick);
                    stickTimer.Interval = new TimeSpan(0, 0, 0, 0, 100);
                    stickTimer.Start();
                    ShowInfo("Joystick timer starts");
                }
            }
            catch (Exception ex)
            {
                ShowInfo("Joystick error: " + ex.Message.ToString());
            }

            // Maximize the main window and bring to front
            WindowState = WindowState.Maximized;
            this.Activate();          
        }

        // Close window
        private void Window_Closed(object sender, EventArgs e)
        {
            try
            {
                Wheelchair.Stop();
                stick = null;
                WebVideo.Close(roomNum);                
                Serial.Close(HWPort);
            }
            catch (Exception )
            {
            
            }
        }               

        #endregion

        #region WndProc

        protected virtual IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case AnyChatCoreSDK.WM_GV_CONNECT:
                    /// Connect
                    int succeed = wParam.ToInt32();
                    if (succeed == 1)
                    {
                        ShowInfo("Connected to video server. ");
                    }
                    else
                    {
                        ShowInfo("Error: Connect to video server failed. ");
                    }
                    break;
                case AnyChatCoreSDK.WM_GV_LOGINSYSTEM:
                    /// Login system
                    if (lParam.ToInt32() == 0)
                    {
                        ShowInfo("Success login video server. ");
                        myUserID = wParam.ToInt32();
                        AnyChatCoreSDK.EnterRoom(roomNum, "", 0);
                    }
                    else
                    {
                        ShowInfo("Login video server failed. ");
                    }
                    break;
                case AnyChatCoreSDK.WM_GV_ENTERROOM:
                    // Enter room
                    int lparam = lParam.ToInt32();
                    if (lparam == 0)
                    {
                        int roomid = wParam.ToInt32();
                        ShowInfo("Success entered video server room. ");
                        roomNum = roomid;
                        // Open local video camera on the interface
                        WebVideo.OpenLocalVideo(WebVideo.GetLocalVideoDeivceName(), hwnd, localCamLeft, localCamTop, localCamRight, localCamBottom, localCamIndex);
                    }
                    else
                    {
                        ShowInfo("Enter video server room failed. ");
                    }
                    break;
                case AnyChatCoreSDK.WM_GV_ONLINEUSER:
                    /// Get users list
                    int cnt = 0;    // Number of online users
                    AnyChatCoreSDK.GetOnlineUser(null, ref cnt);    // Get the number of online users
                    int[] usersID = new int[cnt];   // Online users ID list
                    AnyChatCoreSDK.GetOnlineUser(usersID, ref cnt); // Get the ID list of online users
                    for (int idx = 0; idx < cnt; idx++)
                    {
                        if (usersID[idx] != myUserID)
                        {
                            SetRemoteCamPos(usersID[idx], true);
                        }
                    }
                    break;
                case AnyChatCoreSDK.WM_GV_USERATROOM:
                    /// New user enter room
                    int userID = wParam.ToInt32();
                    int boEntered = lParam.ToInt32();
                    if (boEntered == 0)
                    {
                        if (userID != myUserID)
                        {
                            SetRemoteCamPos(userID, false);
                        }
                    }
                    else
                    {
                        if (userID != myUserID)
                        {
                            SetRemoteCamPos(userID, true);
                        }
                    }
                    break;
                case AnyChatCoreSDK.WM_GV_CAMERASTATE:
                    // State of the camera
                    break;
                case AnyChatCoreSDK.WM_GV_LINKCLOSE:
                    // Lose connection
                    AnyChatCoreSDK.LeaveRoom(-1);
                    int wpara = wParam.ToInt32();
                    int lpara = lParam.ToInt32();
                    ShowInfo("Lose  video connection. ");
                    break;
            }
            return IntPtr.Zero;
        }
        private void SetRemoteCamPos(int id, bool en)
        {
            IntPtr windowHdl = new WindowInteropHelper(Application.Current.MainWindow).Handle;

            WebVideo.ControlRemoteVideo(id, en, windowHdl, remoteCamLeft, remoteCamTop, remoteCamRight, remoteCamBottom, remoteCamIndex);
            
        }

        private string ByteToString(byte[] byteStr)
        {
            string retVal = "";
            try
            {
                retVal = System.Text.Encoding.GetEncoding("GB18030").GetString(byteStr, 0, byteStr.Length);
            }
            catch (Exception exp)
            {
                Console.Write(exp.Message);
            }
            return retVal.TrimEnd('\0');
        }

        #endregion

        #region Command handle

        // Send command to PC
        static public void SendCmd(string cmd)
        {
            bool secret = false;
            int userID = -1; // -1 means to all users
            int ret = -1;
            ret = AnyChatCoreSDK.SendTextMessage(userID, secret, cmd, cmd.Length);
        }

        // Receive cmd from PC
        private void ReceivedCmd(int fromUID, int toUID, string Text, bool isserect)
        {
            // Show received command
            ShowInfo("Received command: " + Text);

            // Check the identification characters
            if (Text.Substring(0,2) == "DK")
            {
                // Send the command direct to the hardware
                if (HWPort.IsOpen)
                {
                    try
                    {
                        HWDataSend(Text);
                    }
                    catch (Exception)
                    {
                        ShowInfo("Serial port sent command failed. ");
                    }
                }
                else
                {
                    ShowInfo("Error: Serial port is not open!");
                }
            }
        }

        // Serial port send
        internal static void HWDataSend(string c)
        {
            HWPort.Write(c);
        }

        internal static void HWDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            string serialFB = HWPort.ReadLine();
            // Process feedback from hardware ...
        }

        #endregion

        #region Show system information

        public void ShowInfo(string text)
        {
            sysInfoTxt.AppendText(DateTime.Now.ToString() + ": " + text + "\r\n");
            sysInfoTxt.ScrollToEnd();
        }

        #endregion

        #region Joystick event

        private void StickTick(object sender, EventArgs e)
        {
            if (stick.IsConnected)
            {
                StickStateTxt.Text = "Connected";
                var state = stick.GetState();

                // Control the robot movements
                JoyStick.rightX = state.Gamepad.RightThumbX;
                JoyStick.rightY = state.Gamepad.RightThumbY;
                if (JoyStick.rightY >= 0)
                {
                    JoyStick.leftSpeed = Convert.ToInt32((JoyStick.rightY + JoyStick.rightX) / 2 / 32768 * JoyStick.speedLimit);
                    JoyStick.rightSpeed = Convert.ToInt32((JoyStick.rightY - JoyStick.rightX) / 2 / 32768 * JoyStick.speedLimit);
                }
                else
                {
                    JoyStick.leftSpeed = Convert.ToInt32((JoyStick.rightY - JoyStick.rightX) / 2 / 32768 * JoyStick.speedLimit);
                    JoyStick.rightSpeed = Convert.ToInt32((JoyStick.rightY + JoyStick.rightX) / 2 / 32768 * JoyStick.speedLimit);
                }
                StickControl(JoyStick.leftSpeed, JoyStick.rightSpeed);

                // Set the speed limit of the robot
                if (state.Gamepad.Buttons == GamepadButtonFlags.LeftShoulder)
                {
                    JoyStick.speedLimit = JoyStick.speedLimit - 10;
                    if (JoyStick.speedLimit <= JoyStick.minSpeed)
                    {
                        JoyStick.speedLimit = JoyStick.minSpeed;
                    }
                }
                if (state.Gamepad.Buttons == GamepadButtonFlags.RightShoulder)
                {
                    JoyStick.speedLimit = JoyStick.speedLimit + 10;
                    if (JoyStick.speedLimit >= JoyStick.maxSpeed)
                    {
                        JoyStick.speedLimit = JoyStick.maxSpeed;
                    }
                }
                MaxSpeedTxt.Text = JoyStick.speedLimit.ToString();

                // Stop if Button B is pressed
                if (state.Gamepad.Buttons == GamepadButtonFlags.B)
                {
                    // Send the command direct to the hardware
                    if (HWPort.IsOpen)
                    {
                        try
                        {
                            HWDataSend("DK0110001000");
                            ShowInfo("Sent stop command");
                        }
                        catch (Exception)
                        {
                            ShowInfo("Serial port sent command failed. ");
                        }
                    }
                    else
                    {
                        ShowInfo("Error: Serial port is not open!");
                    }

                }
            }
            else
            {
                StickStateTxt.Text = "Not connected";
            }
        }

        private void StickControl(int lSpd, int rSpd)
        {
            string l_cmd = "1000";
            string r_cmd = "1000";
            string cmd = "DK0110001000";
            l_cmd = SpeedToCmd(lSpd);
            r_cmd = SpeedToCmd(rSpd);
            cmd = "DK00" + l_cmd + r_cmd;
            // Send the command direct to the hardware
            if (HWPort.IsOpen)
            {
                try
                {
                    HWDataSend(cmd);
                }
                catch (Exception)
                {
                    ShowInfo("Serial port sent command failed. ");
                }
            }
            else
            {
                ShowInfo("Error: Serial port is not open!");
            }
        }

        private string SpeedToCmd(int spd)
        {
            string s = "1";
            string v = "000";
            if (Math.Abs(spd) < 15)
            {
                spd = 0;
            }

            if (spd > 0)
            {
                s = "2";
            }
            else if (spd < 0)
            {
                s = "0";
            }
            else
            {
                s = "1";
            }
            
            v = Math.Abs(spd).ToString("000");
            return s + v;
        }

        #endregion
    }
}
