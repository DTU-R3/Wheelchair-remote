using ANYCHATAPI;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using SharpDX.XInput;
using System.Windows.Threading;
using System.IO.Ports;

namespace User
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
        public string userName = "user";
        public string userPassword = "user";
        private int myUserID = -1;

        // Serial port configuration, port name could be different on different device
        static public SerialPort HWPort = new SerialPort();
        public string portName = "COM1";
        public int baudRate = 9600;
        public int dataBits = 8;

        static public PresentationSource source;

        // Local camera configuration
        public int localCamIndex = 0;
        static public double localCamLeftP = 0;
        static public double localCamRightP = 0;
        static public double localCamTopP = 0;
        static public double localCamBottomP = 0;

        public int localCamLeft = 0;
        public int localCamRight = 0;
        public int localCamTop = 0;
        public int localCamBottom = 0;

        // Remote camera configuration
        public int remoteCamIndex = 0;
        static public double remoteCamLeftP = 0.007;
        static public double remoteCamRightP = 0.328;
        static public double remoteCamTopP = 0.654;
        static public double remoteCamBottomP = 0.959;

        public int remoteCamLeft = 0;
        public int remoteCamRight = 0;
        public int remoteCamTop = 0;
        public int remoteCamBottom = 0;

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

        #region Window events

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Open serial port
            try
            {
                Serial.Init(HWPort, portName, baudRate, dataBits);
                ShowInfo("Success open serial port. ");
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
                ShowInfo("Error: " + ex.Message.ToString());
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
                ShowInfo("Error: " + ex.Message.ToString());
            }

            // Init Joystick
            try
            {
                stick = new Controller(UserIndex.One);
                if (stick.IsConnected)
                {
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
            Activate();         
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            try
            {
                stick = null;
                WebVideo.Close(roomNum);
            }
            catch (Exception)
            {
            }
        }

        #endregion

        #region WndProc

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
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
                    ShowInfo("Lose video connection. ");
                    break;
            }
            return IntPtr.Zero;
        }

        private void SetRemoteCamPos(int id, bool en)
        {
            IntPtr windowHdl = new WindowInteropHelper(Application.Current.MainWindow).Handle;

            byte[] userNameByte = new byte[255];
            int ret = AnyChatCoreSDK.GetUserName(id, ref userNameByte[0], 30);
            string userName = ByteToString(userNameByte);

            if (string.Equals(userName, "robot"))
            {
                WebVideo.ControlRemoteVideo(id, en, windowHdl, remoteCamLeft, remoteCamTop, remoteCamRight, remoteCamBottom, remoteCamIndex);
            }
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

        static public void SendCmd(string cmd)
        {
            bool secret = false;
            int userID = -1; // -1 means to all users
            int ret = -1;
            ret = AnyChatCoreSDK.SendTextMessage(userID, secret, cmd, cmd.Length);
        }

        private void ReceivedCmd(int fromUID, int toUID, string Text, bool isserect)
        {
            // ...
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
            sysInfoTxt.AppendText(DateTime.Now.ToString("HH:mm:ss") + ": " + text + "\r\n");
            sysInfoTxt.ScrollToEnd();
        }

        #endregion
        
        #region Mouse over

        static public BitmapImage setSource(string url)
        {
            return new BitmapImage(new Uri(url, UriKind.Relative));
        }

        private void lfImg_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            lfImg.Source = setSource("images/abtnbg.png");
        }
        private void lfImg_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            lfImg.Source = setSource("images/btnbg.png");
        }

        private void lficon_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            lfImg.Source = setSource("images/abtnbg.png");
        }

        private void lficon_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            lfImg.Source = setSource("images/btnbg.png");
        }

        private void fImg_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            fImg.Source = setSource("images/abtnbg.png");
        }

        private void fImg_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            fImg.Source = setSource("images/btnbg.png");
        }

        private void ficon_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            fImg.Source = setSource("images/abtnbg.png");
        }

        private void ficon_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            fImg.Source = setSource("images/btnbg.png");
        }

        private void rfImg_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            rfImg.Source = setSource("images/abtnbg.png");
        }

        private void rfImg_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            rfImg.Source = setSource("images/btnbg.png");
        }

        private void rficon_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            rfImg.Source = setSource("images/abtnbg.png");
        }

        private void rficon_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            rfImg.Source = setSource("images/btnbg.png");
        }

        private void lImg_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            lImg.Source = setSource("images/abtnbg.png");
        }

        private void lImg_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            lImg.Source = setSource("images/btnbg.png");
        }

        private void licon_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            lImg.Source = setSource("images/abtnbg.png");
        }

        private void licon_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            lImg.Source = setSource("images/btnbg.png");
        }

        private void stopImg_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            stopImg.Source = setSource("images/abtnbg.png");
        }

        private void stopImg_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            stopImg.Source = setSource("images/btnbg.png");
        }

        private void stopicon_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            stopImg.Source = setSource("images/abtnbg.png");
        }

        private void stopicon_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            stopImg.Source = setSource("images/btnbg.png");
        }

        private void rImg_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            rImg.Source = setSource("images/abtnbg.png");
        }

        private void rImg_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            rImg.Source = setSource("images/btnbg.png");
        }

        private void ricon_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            rImg.Source = setSource("images/abtnbg.png");
        }

        private void ricon_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            rImg.Source = setSource("images/btnbg.png");
        }

        private void bImg_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            bImg.Source = setSource("images/abtnbg.png");
        }

        private void bImg_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            bImg.Source = setSource("images/btnbg.png");
        }

        private void bicon_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            bImg.Source = setSource("images/abtnbg.png");
        }

        private void bicon_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            bImg.Source = setSource("images/btnbg.png");
        }

        #endregion

        #region Mouse click

        private void lfImg_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            FrontLeft();          
        }

        private void lficon_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            FrontLeft();
        }

        private void fImg_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Forward();
        }

        private void ficon_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Forward();
        }

        private void rfImg_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            FrontRight();
        }

        private void rficon_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            FrontRight();
        }

        private void lImg_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            LeftTurn();
        }

        private void licon_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            LeftTurn();
        }

        private void stopImg_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Stop();
        }

        private void stopicon_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Stop();
        }

        private void rImg_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            RightTurn();
        }

        private void ricon_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            RightTurn();
        }

        private void bImg_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Backward();
        }

        private void bicon_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Backward();
        }

        #endregion

        #region Movement commands

        private void FrontLeft()
        {
            SendCmd("DK1020402050");
            ShowInfo("Send front left command");
        }

        private void Forward()
        {
            SendCmd("DK1020402040");
            ShowInfo("Send forward command");
        }

        private void FrontRight()
        {
            SendCmd("DK1020502040");
            ShowInfo("Send front right command");
        }

        private void LeftTurn()
        {
            SendCmd("DK1000152015");
            ShowInfo("Send left turn command");
        }

        private void Stop()
        {
            SendCmd("DK0010001000");
            ShowInfo("Send stop command");
        }

        private void RightTurn()
        {
            SendCmd("DK1020150015");
            ShowInfo("Send right turn command");
        }

        private void Backward()
        {
            SendCmd("DK1000300030");
            ShowInfo("Send backward command");
        }

        #endregion

        #region Joystick control

        private void StickTick(object sender, EventArgs e)
        {
            if (stick.IsConnected)
            {
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

                // Change the speed limit
                if (state.Gamepad.Buttons == GamepadButtonFlags.LeftShoulder)
                {
                    JoyStick.speedLimit = Math.Min(JoyStick.speedLimit + 10, JoyStick.maxSpeed);
                    ShowInfo("SpeedLimit is set to be "+ JoyStick.speedLimit.ToString() );
                }
                if (state.Gamepad.Buttons == GamepadButtonFlags.RightShoulder)
                {
                    JoyStick.speedLimit = Math.Max(JoyStick.speedLimit - 10, JoyStick.minSpeed);
                    ShowInfo("SpeedLimit is set to be " + JoyStick.speedLimit.ToString());
                }

                // Stop if Button B is pressed
                if (state.Gamepad.Buttons == GamepadButtonFlags.B)
                {
                    JoyStickCmd("DK0110001000");
                }
            }
        }

        private void JoyStickCmd(string v)
        {
            // Send the command direct to the hardware
            if (HWPort.IsOpen)
            {
                try
                {
                    HWDataSend(v);
                }
                catch (Exception)
                {
                    ShowInfo("Serial port sent command failed. ");
                }
            }
            else
            {
                SendCmd(v);
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
            JoyStickCmd(cmd);
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
