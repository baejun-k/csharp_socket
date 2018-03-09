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

            // 접속 버튼의 이벤트
            btnconnect.Click += (s, e) =>
            {
                socket = new SocketClient(tbid.Text);
            };
            // 채팅 보내기의 이벤트
            btnsend.Click += (s, e) =>
            {
                // 공백을 제외한 string의 길이가 0보다 클 때
                if (tbinput.Text.Trim().Length > 0)
                {
                    socket.send(tbinput.Text + "<EOF>");
                }
            };
            // socket이 접속됐을 때의 이벤트
            SocketClient.Connected += (s, e) =>
            {
                // 비동기 작업은 thread로 돌아가기 때문에 
                // ui스레드에 그냥은 접근이 안됨 dispatcher.invoke 하자.
                this.Dispatcher.Invoke(() =>
                {
                    // 접속 버튼과 id칸을 비활성화
                    tbid.IsEnabled = false;
                    btnconnect.IsEnabled = false;
                });
            };
            SocketClient.DisConnected += (s, e) =>
            {
                // 비동기 작업은 thread로 돌아가기 때문에 
                // ui스레드에 그냥은 접근이 안됨 dispatcher.invoke 하자.
                this.Dispatcher.Invoke(() =>
                {
                    // 접속 버튼과 id칸 활성화
                    tbid.IsEnabled = true;
                    btnconnect.IsEnabled = true;
                });
            };
            SocketClient.RecvMsg += (s, str) =>
            {
                // 비동기 작업은 thread로 돌아가기 때문에 
                // ui스레드에 그냥은 접근이 안됨 dispatcher.invoke 하자.
                this.Dispatcher.Invoke(() =>
                {
                    // 채팅데이터를 받았을 때 추가
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
        // 통신할 socket
        System.Net.Sockets.Socket socket;
        // 나의 id
        string m_ID;
        // 데이터 버퍼
        public byte[] RecvBuf;
        public int RecvBufSize {
            get { return RecvBuf.Length; }
            set { RecvBuf = new byte[value]; }
        }
        public StringBuilder m_Sb;

        // 연결됐을 때의 이벤트
        public static event EventHandler Connected;
        private void connected()
        {
            if (Connected != null)
                Connected(this, EventArgs.Empty);
        }
        // 연결이 끊겼을 때의 이벤트
        public static event EventHandler DisConnected;
        private void disConnected()
        {
            if (DisConnected != null)
                DisConnected(this, EventArgs.Empty);
        }
        // 메시지를 받았을 때의 이벤트
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
            // localhost(127.0.0.1)의 주소를 만든다.
            System.Net.IPAddress ipAddress
                = System.Net.IPAddress.Parse("127.0.0.1");
            // address와 port(1001)로 endpoint 설정
            System.Net.IPEndPoint remoteEP
                = new System.Net.IPEndPoint(ipAddress, 1001);
            // 통신에 사용할 소켓 생성
            socket = new System.Net.Sockets.Socket(ipAddress.AddressFamily,
                System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
            // 설정한 정보로 연결 시도
            socket.BeginConnect(remoteEP, new AsyncCallback(ConnectCallBack), socket);
        }
        // 비동기 연결 콜백 함수
        void ConnectCallBack(IAsyncResult ar)
        {
            var client = ar.AsyncState as System.Net.Sockets.Socket;
            try
            {
                client.EndConnect(ar);
                // 연결되면 맨처음 ID를 보낸다.(약속)
                send(m_ID + "<EOF>");
                // 아이디 중복검사 후 최종 접속 확인을 받는다
                client.Receive(RecvBuf, RecvBufSize, 
                    System.Net.Sockets.SocketFlags.None);
            }
            catch (System.Net.Sockets.SocketException e)
            {
                disConnected();
                return;
            }
            // 최종 접속 확인 후 받은 데이터
            string msg = System.Text.Encoding.UTF8.GetString(RecvBuf, 0, RecvBufSize);
            int endpacket = msg.IndexOf("<EOF>");
            // 데이터를 정상적으로 못받았거나 "아이디중복"메시지를 받았다면
            if (endpacket == -1 || msg.Substring(0, endpacket).Equals("아이디중복"))
            {
                disConnected();
                if (endpacket != -1)
                    // 어떤 메시지를 받았는지 알리는 이벤트
                    recvMsg("아이디중복");
                return;
            }
            else
            {
                // 연결되었음을 알리는 이벤트
                connected();
            }

            // 연결됐다면 비동기 데이터 수신 설정
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
                }
                client.BeginReceive(RecvBuf, 0, RecvBufSize,
                        System.Net.Sockets.SocketFlags.None,
                        new AsyncCallback(ReadCallback), client);
            }
            catch (System.Net.Sockets.SocketException e)
            {
                // 에러가 나면 연결 종료
                disConnected();
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
