using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TcpClient
{
    class Program
    {
        static void Main(string[] args)
        {
            // 접속할 socket을 만든다.
            System.Net.Sockets.TcpClient client = new System.Net.Sockets.TcpClient();
            // 접속할 서버의 adress와 port를 설정하고 연결시도.
            // 127.0.0.1 은 localhost의 주소
            client.Connect("127.0.0.1", 1001);
            
            // 버퍼 크기 설정
            client.ReceiveBufferSize = 1024;
            client.SendBufferSize = 1024;
            byte[] buffer = new byte[1024];

            // 데이터를 UTF8로 인코딩하여 접속 된 서버에 보내본다.
            client.Client.Send(System.Text.Encoding.UTF8.GetBytes("Abc<EOF>"));
            // 서버로부터 데이터를 받는다.
            client.Client.Receive(buffer);
            // 받은 데이터 UTF8로 인코딩 string으로 출력
            Console.WriteLine(System.Text.Encoding.UTF8.GetString(buffer));

            client.Client.Send(System.Text.Encoding.UTF8.GetBytes("test<EOF>"));
            client.Client.Send(System.Text.Encoding.UTF8.GetBytes("test<EOF>"));
            client.Client.Send(System.Text.Encoding.UTF8.GetBytes("test<EOF>"));
            client.Client.Send(System.Text.Encoding.UTF8.GetBytes("test<EOF>"));

            while (Console.ReadKey().Key != ConsoleKey.Escape) ;
        }
    }
}
