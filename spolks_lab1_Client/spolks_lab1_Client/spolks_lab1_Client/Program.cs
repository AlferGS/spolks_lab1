using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SpooksClientV2
{
    class Program
    {
        const int PORT_NO = 8001;
        const string SERVER_IP = "127.0.0.1";
        static string filename;
        static Socket clientSocket;
        static int BUFFER_SIZE = 8192;
        static bool UPLOAD = false;
        static bool DOWNLOAD = false;
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
            clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            LoopConnect(3, 3);
            string result = "";
            do
            {
                if (!UPLOAD && !DOWNLOAD)
                    result = Console.ReadLine();
                else
                    result = "";
                byte[] bytes = Encoding.ASCII.GetBytes(result);
                try
                {
                    if (result.IndexOf(' ') != -1)
                    {
                        if (result.Substring(0, result.IndexOf(' ')).Equals("UPLOAD"))
                            UPLOAD = true;

                        if (result.Substring(0, result.IndexOf(' ')).Equals("DOWNLOAD"))
                            DOWNLOAD = true;

                        filename = result.Substring(result.IndexOf(' ') + 1,
                                    result.Length - result.IndexOf(' ') - 1);
                        
                        if (!File.Exists(filename) && UPLOAD)
                        {
                            Console.WriteLine("file doesn't exist");
                            UPLOAD = false;
                            continue;
                        }
                    }
                    clientSocket.Send(bytes);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    break;
                }
                if (UPLOAD)
                {
                    if (UploadFileToServer() == false)
                        Console.WriteLine("Error with upload");
                    UPLOAD = false;
                }
                if(DOWNLOAD)
                {
                    if (DownloadFileFromServer(clientSocket) == false)
                        Console.WriteLine("Error with download");
                    DOWNLOAD = false;
                }
            } while (result.ToLower().Trim() != "QUIT");
        }

        static bool UploadFileToServer()    // FILESEND
        {
            try
            {
                byte[] data = new byte[BUFFER_SIZE];
                var file = File.OpenRead(filename);
                int fileLength = Convert.ToInt32(file.Length);
                data = Encoding.Unicode.GetBytes(file.Length.ToString());
                clientSocket.Send(data);
                file.Close();
                Console.WriteLine($"file length = {fileLength}");
                
                using (FileStream stream = new FileStream(filename, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                {
                    int block;
                    if (fileLength > BUFFER_SIZE)
                        block = BUFFER_SIZE;
                    else
                        block = fileLength;
                    byte[] fileData = new byte[fileLength];
                    stream.Read(fileData, 0, Convert.ToInt32(fileLength));
                    int offset = 0;
                    int remnant = Convert.ToInt32(fileLength) % block;
                    do
                    {
                        data = new byte[block];
                        data = BufToBuf(fileData, data, offset, block);
                        clientSocket.Send(data);
                        offset += block;
                        if (offset == Convert.ToInt32(fileLength) - remnant)
                        {
                            block = remnant;
                        }
                    } while (offset < fileLength);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                UPLOAD = false;
                return false;
            }
            UPLOAD = false;
            return true;
        }

        static bool DownloadFileFromServer(Socket clientSocket) //FILERECEIVE
        {
            try
            {
                int length = 0;
                int fileLength = 0;
                string lengthStr = "";
                byte[] data = new byte[BUFFER_SIZE];
                
                // get confirm that that file exist on server
                do
                {
                    length = clientSocket.Receive(data);
                    lengthStr += Encoding.Unicode.GetString(data, 0, length);
                } while (clientSocket.Available > 0);
                Console.WriteLine(lengthStr);
                if (lengthStr.Equals("FILE DOESN'T EXIST"))
                {
                    DOWNLOAD = false;
                    return false;
                }
                Console.WriteLine("File exist");

                // get fileLength in client
                //do
                //{
                //    length = clientSocket.Receive(data);
                //    lengthStr += Encoding.Unicode.GetString(data, 0, length);
                //} while (clientSocket.Available > 0);
                //Console.WriteLine(lengthStr);
                fileLength = Convert.ToInt32(lengthStr);
                Console.WriteLine("LENGTH IS " + fileLength);
                Console.WriteLine("Start receiving file from server");
                clientSocket.Send(Encoding.ASCII.GetBytes("Client is ready to receive file " + DateTime.Now));

                // receive file in client
                using (FileStream stream = new FileStream(filename, FileMode.OpenOrCreate, FileAccess.ReadWrite))
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
                        clientSocket.Receive(data, block, 0);
                        stream.Write(data, 0, block);
                        offset += block;
                        if (offset == Convert.ToInt32(fileLength) - remnant)    //if EOF
                        {
                            block = remnant;
                        }
                    } while (offset < fileLength);
                }
                string msg = "finished " + DateTime.Now;
                clientSocket.Send(Encoding.Unicode.GetBytes(msg));
                Console.WriteLine(msg);
                DOWNLOAD = false;
            }
            catch (Exception ex) 
            {
                Console.WriteLine(ex.Message);
                return false;
            }
            return true;
        }

        static void LoopConnect(int noOfRetry, int attemptPeriodInSeconds)
        {
            int attempts = 0;
            while (!clientSocket.Connected && attempts < noOfRetry)
            {
                try
                {
                    ++attempts;
                    IAsyncResult result = clientSocket.BeginConnect(IPAddress.Parse(SERVER_IP), PORT_NO, EndConnectCallback, null);
                    result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(attemptPeriodInSeconds));
                    System.Threading.Thread.Sleep(attemptPeriodInSeconds * 1000);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error: " + e.ToString());
                }
            }
            if (!clientSocket.Connected)
            {
                Console.WriteLine("Connection attempt is unsuccessful!");
                return;
            }
        }

        private static void EndConnectCallback(IAsyncResult ar)
        {
            try
            {
                clientSocket.EndConnect(ar);
                if (clientSocket.Connected)
                {
                    clientSocket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), clientSocket);
                }
                else
                {
                    Console.WriteLine("End of connection attempt, fail to connect...");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("End-connection attempt is unsuccessful! " + e.ToString());
            }
        }

        private static void ReceiveCallback(IAsyncResult result)
        {
            Socket socket = null;
            try
            {
                socket = (Socket)result.AsyncState;
                if (socket.Connected && !DOWNLOAD)
                {
                    int received = socket.EndReceive(result);
                    byte[] data;

                    if (received > 0 && !DOWNLOAD)
                    {
                        receiveAttempt = 0;
                        data = new byte[received];
                        Buffer.BlockCopy(buffer, 0, data, 0, data.Length);
                        Console.WriteLine("Server: " + Encoding.UTF8.GetString(data));
                        socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), socket);
                    }
                    else if (receiveAttempt < MAX_RECEIVE_ATTEMPT)
                    {
                        ++receiveAttempt;
                        socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), socket);
                    }
                    else
                    {
                        Console.WriteLine("receiveCallback is failed!");
                        receiveAttempt = 0;
                        clientSocket.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("receiveCallback is failed! " + e.ToString());
            }
        }
    }
}