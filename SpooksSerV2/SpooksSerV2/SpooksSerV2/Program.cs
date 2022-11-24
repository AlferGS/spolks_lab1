using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.IO;
using System.Threading.Tasks;

namespace SpooksServerV2
{
    class Program
    {
        const int PORT_NO = 8001;
        const string SERVER_IP = "127.0.0.1";
        static string filename = "text2.TXT";
        static Socket serverSocket;
        static bool QUIT = false;

        static byte[] BufToBuf(byte[] sourceArray, byte[] resultArray, int offset, int count)
        {
            for (int i = 0; i < count; i++)
            {
                resultArray[i] = sourceArray[i + offset];
            }
            return resultArray;
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Listening...");
            serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            serverSocket.Bind(new IPEndPoint(IPAddress.Any, PORT_NO));
            serverSocket.Listen(4);
            serverSocket.BeginAccept(new AsyncCallback(acceptCallback), null);
            string result = "";
            while (result != "QUIT")
            {
                result = Console.ReadLine();
            }
        }

        private const int BUFFER_SIZE = 8192;
        private static byte[] buffer = new byte[BUFFER_SIZE];
        static bool ECHO = false;
        static bool FILESEND = false;
        private static void acceptCallback(IAsyncResult result)
        {
            Socket socket = null;
            try
            {
                socket = serverSocket.EndAccept(result);
                socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(receiveCallback), socket);
                QUIT = false;
                Console.WriteLine("Connection opened");
                serverSocket.BeginAccept(new AsyncCallback(acceptCallback), null);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        const int MAX_RECEIVE_ATTEMPT = 10;
        static int receiveAttempt = 0;
        private static void receiveCallback(IAsyncResult result)
        {
            Socket socket = null;
            try
            {
                socket = (Socket)result.AsyncState;
                if (socket.Connected)
                {
                    int length = 0;
                    long fileLength = 0;
                    byte[] data;
                    {
                        int received = socket.EndReceive(result);
                        if (received > 0)
                        {
                            data = new byte[received];
                            Buffer.BlockCopy(buffer, 0, data, 0, data.Length);
                            string receivedMsg = Encoding.UTF8.GetString(data);

                            if (ECHO)
                            {
                                socket.Send(Encoding.ASCII.GetBytes(receivedMsg));
                                ECHO = false;
                            }
                            else
                            {
                                switch (receivedMsg)
                                {
                                    case "ECHO":
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
                                    case "FILESEND":
                                        {
                                            Console.WriteLine("FILESENDMODE");
                                            string lenghtStr = "";
                                            do
                                            {
                                                length = socket.Receive(data);
                                                lenghtStr += Encoding.Unicode.GetString(data, 0, length);
                                            } while (socket.Available > 0);

                                            fileLength = Convert.ToInt32(lenghtStr);

                                            Console.WriteLine("LENGTH IS " + fileLength);
                                            socket.Send(Encoding.ASCII.GetBytes("Server is ready to receive file " + DateTime.Now));
                                            using (FileStream stream = new FileStream(filename, FileMode.Create, FileAccess.ReadWrite))
                                            {
                                                int offset = 0;
                                                do
                                                {
                                                    data = new byte[BUFFER_SIZE];
                                                    socket.Receive(data, BUFFER_SIZE, 0);
                                                    stream.Write(data, 0, BUFFER_SIZE);
                                                    offset += BUFFER_SIZE;
                                                } while (offset < fileLength);
                                            }
                                            string msg = "finished " + DateTime.Now;
                                            socket.Send(Encoding.ASCII.GetBytes(msg));
                                            Console.WriteLine(msg);
                                        }
                                        break;
                                    case "FILERECEIVE":
                                        {
                                            Console.WriteLine("FILERECEIVEMODE " + DateTime.Now);
                                            socket.Send(Encoding.ASCII.GetBytes("Sending " + DateTime.Now));
                                            var file = File.OpenRead(filename);
                                            fileLength = file.Length;
                                            data = Encoding.Unicode.GetBytes(file.Length.ToString());
                                            socket.Send(data);
                                            file.Close();
                                            Console.WriteLine("LENGTH IS " + fileLength);

                                            int sizOfBlock = 8192;

                                            using (FileStream stream = new FileStream(filename, FileMode.Open, FileAccess.ReadWrite))
                                            {
                                                byte[] fileData = new byte[fileLength];
                                                stream.Read(fileData, 0, Convert.ToInt32(fileLength));
                                                int offset = 0;
                                                int remnant = Convert.ToInt32(fileLength) % sizOfBlock;
                                                do
                                                {
                                                    byte[] buff = new byte[sizOfBlock];
                                                    buff = BufToBuf(fileData, buff, offset, sizOfBlock);
                                                    socket.Send(buff);
                                                    offset += sizOfBlock;
                                                    if (offset == Convert.ToInt32(fileLength) - remnant)
                                                    {
                                                        sizOfBlock = remnant;
                                                    }
                                                } while (offset < fileLength);
                                                sizOfBlock = 8192;
                                            }
                                        }
                                        break;

                                    default:
                                        {
                                            Console.WriteLine(receivedMsg);
                                            string msg = "received at " + DateTime.Now;
                                            socket.Send(Encoding.ASCII.GetBytes(msg));
                                            Console.WriteLine(msg);
                                        }
                                        break;
                                }

                            }

                            receiveAttempt = 0;
                            if (!QUIT)
                                socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(receiveCallback), socket);
                            else Console.WriteLine("Connection closed");
                        }
                        else if (receiveAttempt < MAX_RECEIVE_ATTEMPT)
                        {
                            ++receiveAttempt;
                            if (!QUIT)
                                socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(receiveCallback), socket);
                            else Console.WriteLine("Connection closed");
                        }
                        else
                        {
                            Console.WriteLine("receiveCallback fails!");
                            receiveAttempt = 0;
                        }
                    }


                }
                else
                    Console.WriteLine("Connection closed");
            }
            catch (Exception e)
            {
                Console.WriteLine("receiveCallback fails with exception! ");
            }
        }

    }
}