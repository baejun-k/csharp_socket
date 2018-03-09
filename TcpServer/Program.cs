using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TcpServer
{
    class Program
    {
        class ClientInfo
        {
            private System.Net.Sockets.Socket m_Client;
            // 데이터를 받을 버퍼
            private byte[] m_RecvBuf;
            // 데이터를 하나의 문장으로 만들어 저장할 공간
            public StringBuilder m_Sb;

            public ClientInfo(System.Net.Sockets.Socket client)
            {
                m_Client = client;
                m_Sb = new StringBuilder();
                RecvBufferSize = 100;
            }
            public System.Net.Sockets.Socket Client {
                get { return m_Client; }
            }
            public byte[] RecvBuf {
                get { return m_RecvBuf; }
            }
            // 버퍼 크기조정
            public int RecvBufferSize {
                get { return m_RecvBuf.Length; }
                set { m_RecvBuf = new byte[value]; }
            }
            // 이 소켓에 데이터 전송
            public void send(string data)
            {
                // string을 UTF8로 인코딩된 byte[]타입으로
                byte[] byteData = System.Text.Encoding.UTF8.GetBytes(data);

                // 비동기로 데이터 전송
                // AsyncCallback이 비동기로 진행할 작업.
                // 보낼 데이터는 byteData, 시작위치, 시작으로부터 길이로 설정
                // 마지막 파라미터는 콜백함수의 파라미터로 넘어갈 객체
                Client.BeginSend(byteData, 0, byteData.Length,
                    System.Net.Sockets.SocketFlags.None,
                    new AsyncCallback(SendCallback), Client);
            }

            private void SendCallback(IAsyncResult ar)
            {
                try
                {
                    // 파라미터로 넘겨 받은 객체는 Socket이였다.
                    var socket = ar.AsyncState as System.Net.Sockets.Socket;
                    // 데이터를 보낸다.
                    socket.EndSend(ar);
                }
                catch (System.Net.Sockets.SocketException e)
                {
                    // 소켓접속 끊겼을 때 보내면 에러 남
                    Console.WriteLine(e.ToString());
                }
            }
        }

        class ChatServer
        {
            // 접속자 정보를 저장해 둘 곳
            System.Collections.Hashtable m_ClientList
                = new System.Collections.Hashtable();
            // mutex와 같은 용도
            System.Threading.ManualResetEvent allDone
                = new System.Threading.ManualResetEvent(false);
            // 접속자를 받을 socket
            System.Net.Sockets.Socket server;
            // address와 port 정보
            System.Net.IPEndPoint localEndPoint;
            // 접속한 모든 사람에게 데이터를 보내는 이벤트
            public event BroadCastHndlr BroadCast;
            public delegate void BroadCastHndlr(string data);

            public ChatServer(System.Net.IPAddress address, int port = 1001)
            {
                // 접속 정보를 설정
                localEndPoint = new System.Net.IPEndPoint(address, port);
                // 접속을 받을 socket을 만든다.
                server = new System.Net.Sockets.Socket(address.AddressFamily,
                    System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
            }

            public void StartListening()
            {
                // bind and listen
                server.Bind(localEndPoint);
                server.Listen(100);

                Console.WriteLine("채팅 서버를 시작합니다.");
                while (true)        // 서버가 닫힐 때까지 접속자를 계속 받는다.
                {
                    allDone.Reset();
                    // 콜백의 파라미터로 넘겨줄 객체는 server(마지막 파라미터)
                    server.BeginAccept(new AsyncCallback(AcceptCallback), server);
                    allDone.WaitOne();    // 접속이 있을때까지 대기
                }
            }

            private void AcceptCallback(IAsyncResult ar)
            {
                allDone.Set();  // 접속이 있음을 알림
                try
                {
                    // 넘겨받은 객체는 server의 소켓
                    System.Net.Sockets.Socket li =
                                ar.AsyncState as System.Net.Sockets.Socket;
                    // EndAccept을 통해 통신할 소켓을 받아 ClientInfo에 저장
                    ClientInfo cl = new ClientInfo(li.EndAccept(ar));
                    // 접속한 유저의 버퍼를 설정
                    cl.RecvBufferSize = 256;

                    string id = "";
                    // 유저가 처음 접속하면 id를 먼저 보내기로 약속
                    // 접속한 유저와 통신하는 소켓의 스트림을 받는다.
                    using (var ns = new System.Net.Sockets.NetworkStream(cl.Client))
                    {
                        // 처음보낸 정보(id)를 읽어온다.
                        byte[] buf = new byte[1024];
                        ns.Read(buf, 0, 1024);
                        // 받은 byet[]는 UTF8의 string 값
                        id = System.Text.Encoding.UTF8.GetString(buf);
                        // 데이터의 끝을 <EOF>로 표시했다 (약속)
                        // 받을 데이터의 예 : 강아지<EOF>
                        int idx = id.IndexOf("<EOF>");
                        // 여기서 <EOF>가 없다는 것은 보낼때 <EOF>를 안해줬단 의미
                        // (즉, 잘못된 데이터를 보냄)
                        if (idx < 0 || id.Trim().Length < 1)
                        {
                            // 연결을 끊고 함수 종료
                            cl.Client.Shutdown(System.Net.Sockets.SocketShutdown.Both);
                            return;
                        }
                        // 받은데이터의 id를 저장
                        id = id.Substring(0, idx);
                        // 접속자 중에 id가 있는지 검사
                        if (!m_ClientList.ContainsKey(id))
                        {
                            // 접속에 성공했단 것을 접속을 시도한 사람에게 알림
                            cl.send("성공<EOF>");
                            // BroadCast 에 이 사용자에게 보내는 함수를 추가
                            BroadCast += cl.send;
                            Console.WriteLine("\"{0}\"이 참여하였습니다.", id);
                            if (BroadCast != null)
                            {
                                // 접속한 모든 이에게 접속을 알림.
                                BroadCast(string.Format("\"{0}\"이 참여하였습니다.<EOF>", id));
                            }
                            // 접속한 사용자의 비동기 받기를 설정함
                            // 콜백 함수에 ClientInfo를 넘겨준다.
                            cl.Client.BeginReceive(cl.RecvBuf, 0, cl.RecvBufferSize,
                                System.Net.Sockets.SocketFlags.None,
                                new AsyncCallback(ReadCallback), cl);
                            // 접속자 목록에 추가
                            m_ClientList.Add(id, cl);
                        }
                        else
                        {
                            // 아이디 중복으로 접속이 안됨을 알림
                            cl.send("아이디중복<EOF>");
                            cl.Client.Shutdown(System.Net.Sockets.SocketShutdown.Both);
                        }
                    }
                }
                catch (System.Net.Sockets.SocketException e)
                {
                }
            }

            private void ReadCallback(IAsyncResult ar)
            {
                // 넘겨받은 파라미터는 ClientInfo
                ClientInfo cl = ar.AsyncState as ClientInfo;

                try
                {
                    // 비동기로 읽은 데이터의 길이를 반환
                    int readLen = cl.Client.EndReceive(ar);
                    // 읽은 데이터가 있을 때
                    if (readLen > 0)
                    {
                        // 버퍼에 저장된 데이터를 string으로 반환
                        string content = System.Text.Encoding.UTF8.GetString(
                            cl.RecvBuf, 0, readLen);
                        // 받은 데이터 통합
                        cl.m_Sb.Append(content);
                        content = cl.m_Sb.ToString();
                        // 보낸 데이터를 끝까지 받았으면
                        if (content.IndexOf("<EOF>") > -1)
                        {
                            // 통합하는 곳 초기화
                            cl.m_Sb.Clear();
                            // 데이터에 표시된 끝까지 자른다.
                            content = content.Substring(0, content.IndexOf("<EOF>"));
                            Console.WriteLine(content);
                            // 이 소켓의 id를 받아온다.
                            var key = m_ClientList.Keys.OfType<object>()
                                .FirstOrDefault((s) => m_ClientList[s].Equals(cl));
                            if (BroadCast != null)
                            {
                                // 접속한 모든 사용자에게 받은 데이터를 보낸다.
                                BroadCast(String.Format("{0} : {1}<EOF>",
                                    key, content));
                            }
                        }
                    }
                    // 비동기 읽기작업 반복
                    cl.Client.BeginReceive(cl.RecvBuf, 0, cl.RecvBufferSize,
                        System.Net.Sockets.SocketFlags.None,
                        new AsyncCallback(ReadCallback), cl);
                }
                catch (System.Net.Sockets.SocketException e)
                {
                    // 혹시 끊긴 접속이라면 목록에서 제거
                    removeClient(cl);
                }
            }

            private void removeClient(ClientInfo cl)
            {
                // 접속자 id를 찾는다
                var key = m_ClientList.Keys.OfType<object>().FirstOrDefault((s) => m_ClientList[s].Equals(cl));
                var client = m_ClientList[key] as ClientInfo;
                // BroadCast할 목록에서 제거
                BroadCast -= client.send;
                // 접속자 목록에서 제거
                m_ClientList.Remove(key);
                Console.WriteLine("\"{0}\"이 나갔습니다.", key);
                if (client.Client.Connected)
                {
                    // 혹시라도 접속 돼있는 socket이라면
                    client.send("에러로 접속 끊김<EOF>");
                }
                if (BroadCast != null)
                {
                    // 사용자가 나감을 알림
                    BroadCast(String.Format("\"{0}\"이 나갔습니다.", key));
                }
            }
        }

        static void Main(string[] args)
        {
            // 서버를 만든다.
            ChatServer server = new ChatServer(System.Net.IPAddress.Any, 1001);
            // 실행한다.
            server.StartListening();
        }
    }
}
