using System;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Drawing2D;
using Valve.VR;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Configuration;
using System.IO;

namespace AndroidNotificationVRPusher {
    public partial class Form1 : Form {
        int m_nNotifyTime;
        HmdMatrix34_t m_PosMatrix;
        SolidBrush m_TextBrush;
        SolidBrush m_BackgroundBrush;
        int m_nFontSize;

        CVRSystem m_HMD;
        ulong m_ulOverlayHandle;

        bool m_bRunning;
        TcpClient server;
        StreamReader serverStream;

        public Form1() {
            InitializeComponent();
            LoadConfig();
        }

        private void Form1_Shown(object sender, EventArgs e) {
            if( !Init() ) {
                btnStartVR.Enabled = true;
            }
            if (ConfigurationManager.AppSettings["AutoConnect"] == "1") {
                SetTimeout(() => {
                    ConnectServer();
                }, 500);                
            }
        }

        public void LoadConfig() {
            m_nNotifyTime = Convert.ToInt32(ConfigurationManager.AppSettings["NotifyTime"]);
            string[] matrix = ConfigurationManager.AppSettings["NotificationPositionMatrix"].Split(',');
            m_PosMatrix.m0 = (float)Convert.ToDouble(matrix[0]);
            m_PosMatrix.m1 = (float)Convert.ToDouble(matrix[1]);
            m_PosMatrix.m2 = (float)Convert.ToDouble(matrix[2]);
            m_PosMatrix.m3 = (float)Convert.ToDouble(matrix[3]);
            m_PosMatrix.m4 = (float)Convert.ToDouble(matrix[4]);
            m_PosMatrix.m5 = (float)Convert.ToDouble(matrix[5]);
            m_PosMatrix.m6 = (float)Convert.ToDouble(matrix[6]);
            m_PosMatrix.m7 = (float)Convert.ToDouble(matrix[7]);
            m_PosMatrix.m8 = (float)Convert.ToDouble(matrix[8]);
            m_PosMatrix.m9 = (float)Convert.ToDouble(matrix[9]);
            m_PosMatrix.m10 = (float)Convert.ToDouble(matrix[10]);
            m_PosMatrix.m11 = (float)Convert.ToDouble(matrix[11]);

            txtServerIP.Text = ConfigurationManager.AppSettings["DefaultIP"];

            m_TextBrush = new SolidBrush(Color.FromArgb(Convert.ToInt32(ConfigurationManager.AppSettings["TextColor"].ToString(), 16)));            
            m_BackgroundBrush = new SolidBrush(Color.FromArgb(Convert.ToInt32(ConfigurationManager.AppSettings["BackgroundColor"].ToString(), 16)));            
            m_nFontSize = Convert.ToInt32(ConfigurationManager.AppSettings["FontSize"]);
        }

        public bool Init() {
            EVRInitError peError = EVRInitError.None;
            m_HMD = OpenVR.Init(ref peError, EVRApplicationType.VRApplication_Overlay);
            if(peError != EVRInitError.None) {
                txtLog.AppendText("OpenVR啟動失敗: " + peError + '\n');
                return false;
            }
            if (OpenVR.Overlay != null) {
                EVROverlayError overlayError = OpenVR.Overlay.CreateOverlay("AndroidNotificationVRPusher", "AndroidNotifier", ref m_ulOverlayHandle);
                if (overlayError == EVROverlayError.None) {
                    OpenVR.Overlay.SetOverlayWidthInMeters(m_ulOverlayHandle, 1f);
                    OpenVR.Overlay.SetOverlayTransformTrackedDeviceRelative(m_ulOverlayHandle, OpenVR.k_unTrackedDeviceIndex_Hmd, ref m_PosMatrix);
                    OpenVR.Overlay.SetOverlayAlpha(m_ulOverlayHandle, 0.7f);
                    txtLog.AppendText("OpenVR啟動成功\n");
                    return true;
                }
            }
            return false;
        }

        public void ShutDown() {
            m_bRunning = false;            
            if (server != null && server.Connected) {
                serverStream.Close();
                server.Close();
            }
            OpenVR.Shutdown();
        }

        private void btnStartVR_Click(object sender, EventArgs e) {
            btnStartVR.Enabled = false;
            if( !Init()) {
                btnStartVR.Enabled = true;
            }
        }

        int count = 0;
        private void button1_Click(object sender, EventArgs e) {
            ShowNotification("Test Msg "+count++);
        }

        void ShowNotification(string msg) {
            string strImgPath = Directory.GetCurrentDirectory() + "\\Msg.png";
            Bitmap bitmap = new Bitmap(1024, 512);
            Graphics g = Graphics.FromImage(bitmap);
            Font textFont = new Font("Arial", m_nFontSize);
            SizeF textSize = g.MeasureString(msg, textFont);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.FillRectangle(m_BackgroundBrush, new Rectangle(0, 0, (int)textSize.Width+20, (int)textSize.Height+20));
            RectangleF rectf = new RectangleF(10, 10, textSize.Width, textSize.Height);
            g.DrawString(msg, textFont, m_TextBrush, rectf);
            g.Flush();
            bitmap.Save(strImgPath);
            OpenVR.Overlay.SetOverlayFromFile(m_ulOverlayHandle, strImgPath);
            OpenVR.Overlay.ShowOverlay(m_ulOverlayHandle);

            ClearAllTimeout();
            SetTimeout(() => {
                OpenVR.Overlay.HideOverlay(m_ulOverlayHandle);
            }, m_nNotifyTime);
        }

        private void btnConnect_Click(object sender, EventArgs e) {
            ConnectServer();
        }

        void ConnectServer() {
            try {
                server = new TcpClient(txtServerIP.Text, 17749);
                serverStream = new StreamReader(server.GetStream(), Encoding.UTF8);
                Thread thread = new Thread(ReceiveTask);
                thread.Start();
                m_bRunning = true;
                this.Invoke((MethodInvoker)delegate {
                    txtStatus.Text = "已連線";
                });
            }
            catch (SocketException ex) {
                this.Invoke((MethodInvoker)delegate {
                    txtStatus.Text = "連線失敗";
                });
            }
        }

        void ReceiveTask() {
            while (m_bRunning) {
                if (server.Connected) {
                    try {
                        string s = serverStream.ReadLine();
                        if(s.Length > 2) {
                            s = s.Substring(2);
                            Console.WriteLine(s);
                            this.Invoke((MethodInvoker)delegate {
                                txtLog.AppendText("收到通知: " + s + '\n');
                            });
                            ShowNotification(s.Replace(';','\n'));
                        }
                    }
                    catch(Exception ex) {
                        this.Invoke((MethodInvoker)delegate {
                            txtStatus.Text = "未連線";
                        });
                        ConnectServer();
                    }
                }
                else {
                    this.Invoke((MethodInvoker)delegate {
                        txtStatus.Text = "未連線";
                    });
                    ConnectServer();
                }
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e) {
            ShutDown();
        }


        #region SetTimeout/ClearTimeout Simulation
        static Dictionary<Guid, Thread> _setTimeoutHandles =
            new Dictionary<Guid, Thread>();
        static Guid SetTimeout(Action cb, int delay) {
            return SetTimeout(cb, delay, null);
        }
        static Guid SetTimeout(Action cb, int delay, Form uiForm) {
            Guid g = Guid.NewGuid();
            Thread t = new Thread(() => {
                Thread.Sleep(delay);
                _setTimeoutHandles.Remove(g);
                if (uiForm != null)
                    uiForm.Invoke(cb);
                else
                    cb();
            });
            _setTimeoutHandles.Add(g, t);
            t.Start();
            return g;
        }
        static void ClearTimeout(Guid g) {
            if (!_setTimeoutHandles.ContainsKey(g))
                return;
            _setTimeoutHandles[g].Abort();
            _setTimeoutHandles.Remove(g);
        }
        static void ClearAllTimeout() {
            foreach(var g in _setTimeoutHandles) {
                g.Value.Abort();
            }
            _setTimeoutHandles.Clear();
        }
        #endregion
    }
}
