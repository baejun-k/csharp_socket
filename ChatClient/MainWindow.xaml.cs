using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ChatClient
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        SocketClient socket;

        public MainWindow()
        {
            InitializeComponent();

            btnconnect.Click += (s, e) =>
            {
                socket = new SocketClient(tbid.Text);
            };
            btnsend.Click += (s, e) =>
            {
                if (tbinput.Text.Trim().Length > 0)
                {
                    socket.send(tbinput.Text + "<EOF>");
                }
            };
            SocketClient.Connected += (s, e) =>
            {
                this.Dispatcher.Invoke(() =>
                {
                    tbid.IsEnabled = false;
                    btnconnect.IsEnabled = false;
                });
            };
            SocketClient.DisConnected += (s, e) =>
            {
                this.Dispatcher.Invoke(() =>
                {
                    tbid.IsEnabled = true;
                    btnconnect.IsEnabled = true;
                });
            };
            SocketClient.RecvMsg += (s, str) =>
            {
                this.Dispatcher.Invoke(() =>
                {
                    var item = new ListViewItem();
                    item.Content = str;
                    chatbox.Items.Add(item);
                    chatbox.ScrollIntoView(chatbox.Items[chatbox.Items.Count - 1]);
                });
            };
        }
    }

    public class SocketClient
    {
        System.Net.Sockets.Socket socket;
        string m_ID;
        public int RecvBufSize {
            get { return RecvBuf.Length; }
            set { RecvBuf = new byte[value]; }
        }
        public byte[] RecvBuf;
        public StringBuilder m_Sb;

        public static event EventHandler Connected;
        private void connected()
        {
            if (Connected != null)
                Connected(this, EventArgs.Empty);
        }
        public static event EventHandler DisConnected;
        private void disConnected()
        {
            if (DisConnected != null)
                DisConnected(this, EventArgs.Empty);
        }
        public static event EventHandler<string> RecvMsg;
        private void recvMsg(string str)
        {
            if (RecvMsg != null)
            {
                RecvMsg(this, str);
            }
        }

        public SocketClient(string id)
        {
            m_ID = id;
            RecvBufSize = 100;
            m_Sb = new StringBuilder();

            System.Net.IPAddress ipAddress
                = System.Net.IPAddress.Parse("127.0.0.1");
            System.Net.IPEndPoint remoteEP
                = new System.Net.IPEndPoint(ipAddress, 1001);

            socket = new System.Net.Sockets.Socket(ipAddress.AddressFamily,
                System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);

            socket.BeginConnect(remoteEP, new AsyncCallback(ConnectCallBack), socket);
        }

        void ConnectCallBack(IAsyncResult ar)
        {
            var client = ar.AsyncState as System.Net.Sockets.Socket;
            try
            {
                client.EndConnect(ar);
                send(m_ID + "<EOF>");
                client.Receive(RecvBuf, RecvBufSize, System.Net.Sockets.SocketFlags.None);
            }
            catch (System.Net.Sockets.SocketException e)
            {
                disConnected();
                return;
            }
            string msg = System.Text.Encoding.UTF8.GetString(RecvBuf, 0, RecvBufSize);
            int endpacket = msg.IndexOf("<EOF>");

            if (endpacket == -1 || !msg.Substring(0, endpacket).Equals("성공"))
            {
                disConnected();
                if (endpacket != -1)
                    recvMsg("아이디중복");
                return;
            }
            else
            {
                connected();
            }

            client.BeginReceive(RecvBuf, 0, RecvBufSize,
                System.Net.Sockets.SocketFlags.None,
                new AsyncCallback(ReadCallback), client);
        }
        private void ReadCallback(IAsyncResult ar)
        {
            var client = ar.AsyncState as System.Net.Sockets.Socket;

            try
            {
                int readLen = client.EndReceive(ar);
                if (readLen > 0)
                {
                    string content = System.Text.Encoding.UTF8.GetString(
                        RecvBuf, 0, readLen);
                    m_Sb.Append(content);
                    content = m_Sb.ToString();
                    if (content.IndexOf("<EOF>") > -1)
                    {
                        m_Sb.Clear();
                        content = content.Substring(0, content.IndexOf("<EOF>"));
                        if (RecvMsg != null)
                            RecvMsg(this, content);
                    }
                    else
                    {
                    }
                    client.BeginReceive(RecvBuf, 0, RecvBufSize,
                        System.Net.Sockets.SocketFlags.None,
                        new AsyncCallback(ReadCallback), client);
                }
            }
            catch (System.Net.Sockets.SocketException e)
            {
            }
        }

        public void send(string data)
        {
            byte[] bytedatas = System.Text.Encoding.UTF8.GetBytes(data);

            socket.BeginSend(bytedatas, 0, bytedatas.Length,
                System.Net.Sockets.SocketFlags.None,
                new AsyncCallback(SendCallback), socket);

        }

        void SendCallback(IAsyncResult ar)
        {
            var client = ar.AsyncState as System.Net.Sockets.Socket;
            client.EndSend(ar);
        }
    }
}
