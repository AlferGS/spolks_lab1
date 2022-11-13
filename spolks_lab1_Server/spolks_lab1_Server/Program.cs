using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SpooksServerV2
{
    class Program
    {
        const int PORT_NO = 8001;
        const string SERVER_IP = "127.0.0.1";
        static string filename;
        static Socket serverSocket;
        static bool QUIT = false;
        static bool ECHO = false;
        static bool UPLOAD = false;
        static bool DOWNLOAD = false;
        private const int BUFFER_SIZE = 8192;
        private static byte[] buffer = new byte[BUFFER_SIZE];
        const int MAX_RECEIVE_ATTEMPT = 10;
        static int receiveAttempt = 0;

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
            string lenghtStr = "";
            int fileLength;
   
            do
            {
                length = socket.Receive(data);
                lenghtStr += Encoding.Unicode.GetString(data, 0, length);
            } while (socket.Available > 0);

            fileLength = Convert.ToInt32(lenghtStr);

            Console.WriteLine("LENGTH IS " + fileLength);
 /*add*/    socket.Send(Encoding.ASCII.GetBytes("Server is ready to receive file " + DateTime.Now));
            using (FileStream stream = new FileStream(filename, FileMode.Create, FileAccess.ReadWrite))
            {
                int offset = 0;
                int block;
                if (fileLength > BUFFER_SIZE)
                    block = BUFFER_SIZE;
                else
                    block = fileLength;
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
                socket.Send(Encoding.Unicode.GetBytes("FILE DOESN'T EXIST"));
                return false;
            }
            try
            {
                byte[] data;
   // /*add*/     socket.Send(Encoding.UTF8.GetBytes("SENDING " + DateTime.Now));
                var file = File.OpenRead(filename);
                int fileLength = Convert.ToInt32(file.Length);
                
                // send file length
                Console.WriteLine("LENGTH IS " + fileLength);
                data = Encoding.ASCII.GetBytes(fileLength.ToString());
                socket.Send(data);
                file.Close();
                Console.WriteLine("Start sending file to client");
                string lengthStr = "";
                int length = 0;
                do
                {
                    length = socket.Receive(data);
                    lengthStr += Encoding.Unicode.GetString(data, 0, length);
                } while (socket.Available > 0);
                Console.WriteLine(lengthStr);

                // send file to client
                using (FileStream stream = new FileStream(filename, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                {
                    // read file data in fileData buffer
                    int block;
                    if (fileLength > BUFFER_SIZE)
                        block = BUFFER_SIZE;
                    else
                        block = fileLength;
                    byte[] fileData = new byte[fileLength];
                    stream.Read(fileData, 0, Convert.ToInt32(fileLength));
                    int offset = 0;
                    int remnant = Convert.ToInt32(fileLength) % BUFFER_SIZE;
                    do
                    {
                        data = new byte[block];
                        data = BufToBuf(fileData, data, offset, block);
                        socket.Send(data);
                        offset += block;
                        if (offset == Convert.ToInt32(fileLength) - remnant)    //if EOF
                        {
                            block = remnant;
                        }
                    } while (offset < fileLength);
                }

                do
                {
                    length = socket.Receive(data);
                    lengthStr += Encoding.Unicode.GetString(data, 0, length);
                } while (socket.Available > 0);
                Console.WriteLine(lengthStr);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
            return true;
        }

        private static void ReceiveCallback(IAsyncResult result)
        {
            Socket socket = null;
            try
            {
                socket = (Socket)result.AsyncState;
                if (socket.Connected)
                {
                    byte[] data;
                    int received = socket.EndReceive(result);
                    if (received > 0)
                    {
                        data = new byte[received];
                        Buffer.BlockCopy(buffer, 0, data, 0, data.Length);
                        string receivedMsg = Encoding.UTF8.GetString(data);
                        string switchMsg;
                        //  Check that receiveMsg got ' '
                        if (receivedMsg.IndexOf(' ') == -1)
                            switchMsg = receivedMsg;
                        else  // if it have ' ', get substring with first word (command word) 
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
                            case "UPLOAD":  // FILESEND
                                {
                                    filename = receivedMsg.Substring(receivedMsg.IndexOf(' ') + 1,
                                                       receivedMsg.Length - receivedMsg.IndexOf(' ') - 1);
                                    Console.WriteLine(filename);
                                    UploadFileToServer(socket, data, filename);
                                }
                                break;
                            case "DOWNLOAD": // FILERECEIVE
                                {
                                    filename = receivedMsg.Substring(receivedMsg.IndexOf(' ') + 1,
                                                       receivedMsg.Length - receivedMsg.IndexOf(' ') - 1);
                                    Console.WriteLine(filename);
                                    DownloadFileFromServer(socket, filename);
                                }
                                break;
                            default:
                                {
                                    if (UPLOAD || DOWNLOAD) // maybe comment this if
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


