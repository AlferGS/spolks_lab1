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

        static byte[] BufToBuf(byte[] sourceArray, byte[] resultArray, long offset, long count)
        {
            for (int i = 0; i < count; i++)
            {
                resultArray[i] = sourceArray[i + offset];
            }
            return resultArray;
        }
        static bool CheckFileExists(string filename)
        {
            string path = @"D:\Projects C#\Spolks lab 1\shit\SpooksSerV2\SpooksSerV2\SpooksSerV2\bin\Debug\net5.0\";
            if (!File.Exists(path + filename))
            {
                Console.WriteLine("file doesn't exist");
                return false;
            }
            return true;
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
        static bool UPLOAD = false;
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

        static bool UploadFile(byte[] data, Socket socket, int length, long fileLength)
        {
            UPLOAD = true;
            Console.WriteLine("--UPLOADMODE " + DateTime.Now);
            string lenghtStr = "";
            do
            {
                length = socket.Receive(data);
                lenghtStr += Encoding.Unicode.GetString(data, 0, length);
            } while (socket.Available > 0);

            fileLength = Convert.ToInt32(lenghtStr);

            Console.WriteLine("LENGTH IS " + fileLength);
            DateTime start = DateTime.Now;
            socket.Send(Encoding.ASCII.GetBytes("Server is ready to receive file " + DateTime.Now));
            using (FileStream stream = new FileStream(filename, FileMode.Create, FileAccess.ReadWrite))
            {
                long offset = 0; 
                long block;
                if (fileLength > BUFFER_SIZE)
                    block = BUFFER_SIZE;
                else
                    block = fileLength;
                int remnant = Convert.ToInt32(fileLength) % BUFFER_SIZE;
                do
                {
                    data = new byte[block];
                    socket.Receive(data, (int)block, 0);
                    stream.Write(data, 0, (int)block);
                    offset += block;
                    if (offset == Convert.ToInt32(fileLength) - remnant)
                    {
                        block = remnant;
                    }
                } while (offset < fileLength);
            }
            DateTime end = DateTime.Now;
            string msg = "finished " + end + "\nbitrate: " + fileLength / (end - start).TotalSeconds / 1000 + " kb/s";

            socket.Send(Encoding.ASCII.GetBytes(msg));
            Console.WriteLine(msg);
            UPLOAD = false;
            Console.WriteLine("------------------------------------------------");
            return true;
        }

        static bool DownloadFile(byte[] data, Socket socket, int length, long fileLength)
        {
            Console.WriteLine("--DOWNLOADMODE " + DateTime.Now);
            socket.Send(Encoding.ASCII.GetBytes("Sending " + DateTime.Now));

            string lenghtStr = "";                                          //.
            do                                                              //.
            {                                                               //.
                length = socket.Receive(data);                              //.
                lenghtStr += Encoding.Unicode.GetString(data, 0, length);   //.
            } while (socket.Available > 0);                                 //.
            filename = lenghtStr;                                           //.
            if(!CheckFileExists(filename))                                  //.
            {                                                               //.
                socket.Send(Encoding.Unicode.GetBytes("!EXIST"));           //.
                return true;                                               //.
            }                                                               //.
            var file = File.OpenRead(filename);                             //.
            fileLength = file.Length;
            // send file size
            data = Encoding.Unicode.GetBytes(file.Length.ToString());   
            socket.Send(data);
            file.Close();
            Console.WriteLine("LENGTH IS " + fileLength);

            using (FileStream stream = new FileStream(filename, FileMode.Open, FileAccess.ReadWrite))
            {
                long block;
                if (fileLength > BUFFER_SIZE)
                    block = BUFFER_SIZE;
                else
                    block = fileLength;
                // read file data in fileData buffer
                byte[] fileData = new byte[fileLength];
                stream.Read(fileData, 0, Convert.ToInt32(fileLength));
                long offset = 0;
                int remnant = Convert.ToInt32(fileLength) % BUFFER_SIZE;
                do
                {
                    byte[] buff = new byte[block];
                    buff = BufToBuf(fileData, buff, offset, block);
                    socket.Send(buff);
                    offset += block;
                    if (offset == Convert.ToInt32(fileLength) - remnant)
                    {
                        block = remnant;
                    }
                } while (offset < fileLength);
            }
            //
            return true;
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
                        int received = socket.EndReceive(result);
                    if (received > 0)
                    {
                        data = new byte[received];
                        Buffer.BlockCopy(buffer, 0, data, 0, data.Length);
                        string receivedMsg = Encoding.UTF8.GetString(data);

                        if (ECHO)
                        {
                            socket.Send(Encoding.ASCII.GetBytes(receivedMsg));
                        }
                        switch (receivedMsg)
                        {
                            case "ECHO":
                                if (ECHO)
                                {
                                    Console.WriteLine("ECHO disabled");
                                    ECHO = false;
                                }
                                else
                                {
                                    Console.WriteLine("ECHO activated");
                                    ECHO = true;
                                }
                                break;
                            case "TIME":
                                socket.Send(Encoding.ASCII.GetBytes("" + DateTime.Now));
                                break;
                            case "QUIT":
                                QUIT = true;
                                break;
                            case "UPLOAD":
                                if (!UploadFile(data, socket, length, fileLength))
                                    Console.WriteLine($"Error. Can't upload file to client");
                                break;
                            case "DOWNLOAD":
                                if (!DownloadFile(data, socket, length, fileLength))
                                    Console.WriteLine($"Error. Can't download file from server");
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
                else
                    Console.WriteLine("Connection closed");
            }
            catch (Exception e)
            {
                Console.WriteLine($"receiveCallback fails with exception! {e.Message}");
            }
        }
    }
}