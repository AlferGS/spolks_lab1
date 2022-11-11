//using System;
//using System.IO;
//using System.Text;
//using System.Net;
//using System.Net.Sockets;
//using System.Threading.Tasks;
//using System.Threading;

////for (int i = 0; i < 100; i++)
////{
////    //Console.Clear();
////    Console.SetCursorPosition(0, 0);
////    Console.WriteLine(i);
////    Thread.Sleep(100);
////}

//namespace SocketTcpServer
//{
//    class Program
//    {
//        static void Main(string[] args)
//        {
//            int port = 8888;
//            //IPAddress serverIP = IPAddress.Parse("127.0.0.1");
//            IPAddress serverIP = IPAddress.Parse("192.168.0.109");
//            string filename = "text2.TXT";
//            int sizOfBlock = 512;

//            Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
//            try
//            {
//                serverSocket.Bind(new IPEndPoint(serverIP, port));
//                serverSocket.Listen(10);
//                Console.WriteLine($"Start Server. Wait new connections...");

//                while (true)
//                {

//                    Socket handler = serverSocket.Accept(); // getting connections to serverSocket
//                    StringBuilder builder = new StringBuilder();
//                    int length = 0;
//                    byte[] data = new byte[sizOfBlock]; //buffer for message 
//                    //receive message from client
//                    do
//                    {
//                        length = handler.Receive(data);
//                        builder.Append(Encoding.Unicode.GetString(data, 0, length));
//                    } while (handler.Available > 0);

//                    Console.WriteLine($"{DateTime.Now.ToString()}: {builder.ToString()}");

//                    //send length of file to client
//                    var file = File.OpenRead(filename);
//                    var fileLength = file.Length;
//                    data = Encoding.Unicode.GetBytes(file.Length.ToString());
//                    int i = handler.Send(data);
//                    file.Close();
//                    Console.WriteLine($"{DateTime.Now.ToString()}: file length = {fileLength}");
//                    Console.WriteLine($"{DateTime.Now.ToString()}: i = {i}");

//                    //receive confirm from client
//                    do
//                    {
//                        length = handler.Receive(data);
//                        builder.Append(Encoding.Unicode.GetString(data, 0, length));
//                    } while (handler.Available > 0);
//                    Console.WriteLine($"{DateTime.Now.ToString()}: {builder.ToString()}");

//                    //send file
//                    using (FileStream stream = new FileStream(filename, FileMode.Open, FileAccess.ReadWrite))
//                    {
//                        byte[] fileData = new byte[fileLength];
//                        stream.Read(fileData, 0, Convert.ToInt32(fileLength));
//                        int offset = 0;
//                        int remnant = Convert.ToInt32(fileLength) % sizOfBlock;

//                        do
//                        {
//                            byte[] buff = new byte[sizOfBlock];
//                            buff = BufToBuf(fileData, buff, offset, sizOfBlock);
//                            handler.Send(buff);
//                            offset += sizOfBlock;
//                            if (offset == Convert.ToInt32(fileLength) - remnant)
//                            {
//                                sizOfBlock = remnant;
//                            }
//                        } while (offset < fileLength);
//                    }
//                    handler.Shutdown(SocketShutdown.Both);
//                    handler.Close();
//                }
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine(ex.Message);
//            }
//            Console.ReadKey();
//        }

//        static byte[] BufToBuf(byte[] sourceArray, byte[] resultArray, int offset, int count)
//        {
//            for(int i = 0; i < count; i++)
//            {
//                resultArray[i] = sourceArray[i + offset];
//            }
//            return resultArray;
//        }
//    }
//}


////////////////////////////////////////////////////////////////////////////////////////////////////////////////


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SpooksServerV2
{
    class Program
    {
        const int PORT_NO = 8001;
        const string SERVER_IP = "127.0.0.1";
        //static string filename = "text2.TXT";
        static Socket serverSocket;
        static bool QUIT = false;
        private const int BUFFER_SIZE = 8192;
        private static byte[] buffer = new byte[BUFFER_SIZE];
        static bool ECHO = false;
        static bool UPLOAD = false;
        static bool DOWNLOAD = false;
        const int MAX_RECEIVE_ATTEMPT = 10;
        static int receiveAttempt = 0;

        static void Main(string[] args)
        {
            Console.WriteLine("Listening...");
            serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            serverSocket.Bind(new IPEndPoint(IPAddress.Any, PORT_NO));
            serverSocket.Listen(4);
            serverSocket.BeginAccept(new AsyncCallback(AcceptCallback), null);
            string result = "";
            while (result != "QUIT")
            {
                result = Console.ReadLine();
            }
        }

        private static void AcceptCallback(IAsyncResult result)
        {
            Socket socket = null;
            try
            {
                socket = serverSocket.EndAccept(result);
                socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), socket);
                QUIT = false;
                Console.WriteLine("Connection opened");
                serverSocket.BeginAccept(new AsyncCallback(AcceptCallback), null);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private static bool UploadFileToServer(Socket socket, byte[] data, string filename)
        {
            Console.WriteLine("UPLOADMODE");
            int length;
            long fileLength;
   
            string lenghtStr = "";
            do
            {
                length = socket.Receive(data);
                lenghtStr += Encoding.Unicode.GetString(data, 0, length);
            } while (socket.Available > 0);

            fileLength = Convert.ToInt32(lenghtStr);

            Console.WriteLine("LENGTH IS " + fileLength);
            using (FileStream stream = new FileStream(filename, FileMode.Create, FileAccess.ReadWrite))
            {
                int offset = 0;
                int block = BUFFER_SIZE;
                int remnant = Convert.ToInt32(fileLength) % BUFFER_SIZE;
                do
                {
                    data = new byte[block];
                    socket.Receive(data, block, 0);
                    stream.Write(data, 0, block);
                    offset += block;
                    if (offset == Convert.ToInt32(fileLength) - remnant)
                    {
                        block = remnant;
                    }
                } while (offset < fileLength);
            }
            string msg = "finished " + DateTime.Now;
            socket.Send(Encoding.ASCII.GetBytes(msg));
            Console.WriteLine(msg);
            UPLOAD = false;
            return true;
        }

        private static bool DownloadFileFromServer(Socket socket, string filename)
        {
            Console.WriteLine("DOWNLOADMODE");
            if (!File.Exists(filename)) { 
                Console.WriteLine("FILE DOESN'T EXIST");
                socket.Send(Encoding.UTF8.GetBytes("FILE DOESN'T EXIST"));
                return false;
            }
            try
            {
                byte[] data;
                var file = File.OpenRead(filename);
                var fileLength = file.Length;
                // string message = "";
                
                // send file length
                Console.WriteLine("LENGTH IS " + fileLength);
                data = Encoding.Unicode.GetBytes(fileLength.ToString());
                socket.Send(data);
                file.Close();  
                
                // send file to client
                //using (FileStream stream = new FileStream(filename, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                //{
                //    // read file data in fileData buffer
                //    int block = BUFFER_SIZE;
                //    byte[] fileData = new byte[fileLength];
                //    stream.Read(fileData, 0, Convert.ToInt32(fileLength));
                //    int offset = 0;
                //    int remnant = Convert.ToInt32(fileLength) % BUFFER_SIZE;
                //    do
                //    {
                //        data = new byte[block];
                //        data = BufToBuf(fileData, data, offset, block);
                //        socket.Send(data);
                //        offset += block;
                //        if (offset == Convert.ToInt32(fileLength) - remnant)    //if EOF
                //        {
                //            block = remnant;
                //        }
                //    } while (offset < fileLength);
                //}
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
            return true;
        }

        static byte[] BufToBuf(byte[] sourceArray, byte[] resultArray, int offset, int count)
        {
            for (int i = 0; i < count; i++)
            {
                resultArray[i] = sourceArray[i + offset];
            }
            return resultArray;
        }

        private static void ReceiveCallback(IAsyncResult result)
        {
            Socket socket = null;
            try
            {
                socket = (Socket)result.AsyncState;
                if (socket.Connected)
                {
                    //int length = 0;
                    //long fileLength = 0;
                    byte[] data;
                    int received = socket.EndReceive(result);
                    if (received > 0)
                    {
                        data = new byte[received];
                        Buffer.BlockCopy(buffer, 0, data, 0, data.Length);
                        string receivedMsg = Encoding.UTF8.GetString(data);
                        string switchMsg;
                        if (receivedMsg.IndexOf(' ') == -1)
                            switchMsg = receivedMsg;
                        else
                            switchMsg = receivedMsg.Substring(0, receivedMsg.IndexOf(' '));

                        if (ECHO)
                        {
                            socket.Send(Encoding.ASCII.GetBytes(receivedMsg));
                        }
                        switch (switchMsg)
                        {
                            case "ECHO":
                                if(ECHO)
                                    ECHO = false;
                                else
                                    ECHO = true;
                                break;
                            case "TIME":
                                {
                                    socket.Send(Encoding.ASCII.GetBytes("" + DateTime.Now));
                                }
                                break;
                            case "QUIT":
                                {
                                    QUIT = true;
                                }
                                break;
                            case "UPLOAD":
                                {
                                    string filename = receivedMsg.Substring(receivedMsg.IndexOf(' ') + 1,
                                                       receivedMsg.Length - receivedMsg.IndexOf(' ') - 1);
                                    Console.WriteLine(filename);
                                    UploadFileToServer(socket, data, filename);
                                }
                                break;
                            case "DOWNLOAD":
                                {
                                    string filename = receivedMsg.Substring(receivedMsg.IndexOf(' ') + 1,
                                                       receivedMsg.Length - receivedMsg.IndexOf(' ') - 1);
                                    Console.WriteLine(filename);
                                    DownloadFileFromServer(socket, filename);
                                }
                                break;
                            default:
                                {
                                    if (UPLOAD || DOWNLOAD)
                                        break;
                                    Console.WriteLine(receivedMsg);
                                    string msg = "received at " + DateTime.Now;
                                    socket.Send(Encoding.ASCII.GetBytes(msg));
                                    Console.WriteLine(msg);
                                }
                                break;
                        }

                        received = 0;
                        receiveAttempt = 0;
                        if (!QUIT)
                            socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), socket);
                        else Console.WriteLine("Connection closed");
                    }

                    else if (receiveAttempt < MAX_RECEIVE_ATTEMPT)
                    {
                        ++receiveAttempt;
                        if (!QUIT)
                            socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), socket);
                        else Console.WriteLine("Connection closed");
                    }

                    else
                    {
                        Console.WriteLine("receiveCallback fails!");
                        receiveAttempt = 0;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("receiveCallback fails with exception! " + e.ToString());
            }
        }
    }
}


