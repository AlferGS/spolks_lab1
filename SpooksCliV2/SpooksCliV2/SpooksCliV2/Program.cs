using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.IO;
using System.Threading.Tasks;

namespace SpooksClientV2
{
    class Program
    {
        const int PORT_NO = 8001;
        const string SERVER_IP = "127.0.0.1";
        static string filename = "text2.TXT";
        static Socket clientSocket;
        //static int sizOfBlock = 8192;
        static bool UPLOAD = false;
        static bool DOWNLOAD = false;
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
            loopConnect(3, 3);
            string result = "";
            do 
            {   //get String from cmd
                if (!UPLOAD && !DOWNLOAD)
                    result = Console.ReadLine();
                byte[] bytes = Encoding.ASCII.GetBytes(result);
                // check command
                if (result == "UPLOAD")
                    UPLOAD = true;
                if (result == "DOWNLOAD")
                    DOWNLOAD = true;
                if (filename == "BREAK")
                {
                    UPLOAD = false;
                    DOWNLOAD = false;
                }
                // get filename
                if (UPLOAD || DOWNLOAD)
                {
                    Console.WriteLine("Enter filename:");
                    filename = Console.ReadLine();
                }
                // (send or receive file) or send message 
                try
                {
                    if (DOWNLOAD && DownloadFile(bytes))
                    {
                        DOWNLOAD = false;
                        continue;
                    }

                    else if (UPLOAD)
                    {
                        if (!CheckFileExists(filename))
                        {
                            UPLOAD = false;
                            continue;
                        }
                        if (!UploadFile(bytes))
                        {
                            UPLOAD = false;
                            continue;
                        }
                    }
                    // send message
                    else
                        clientSocket.Send(bytes);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Server has disconnected" + ex.ToString());
                    break;
                }
            } while (result.ToLower().Trim() != "QUIT");
        }

        static bool UploadFile(byte[] bytes)
        {
            clientSocket.Send(bytes);
            try
            {
                byte[] data;
                var file = File.OpenRead(filename);
                var fileLength = file.Length;
                data = Encoding.Unicode.GetBytes(file.Length.ToString());
                clientSocket.Send(data);
                file.Close();
                Console.WriteLine($"file length = {fileLength} ");

                using (FileStream stream = new FileStream(filename, FileMode.Open, FileAccess.ReadWrite))
                {
                    int block;
                    if (fileLength > BUFFER_SIZE)
                        block = BUFFER_SIZE;
                    else
                        block = (int)fileLength;
                    byte[] fileData = new byte[fileLength];
                    stream.Read(fileData, 0, Convert.ToInt32(fileLength));
                    int offset = 0;
                    int remnant = Convert.ToInt32(fileLength) % block;
                    do
                    {
                        byte[] buff = new byte[block];
                        buff = BufToBuf(fileData, buff, offset, block);
                        clientSocket.Send(buff);
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
                Console.WriteLine($"Server has disconnected: {ex}");
                return false;
            }
            UPLOAD = false;
            return true;
        }

        static bool DownloadFile(byte[] bytes)
        {
            DOWNLOAD = true;
            clientSocket.Send(bytes);
            byte[] data = new byte[100];
            int length;
            long fileLength;
            DateTime start = DateTime.Now;
            DateTime end;
            clientSocket.Send(Encoding.Unicode.GetBytes(filename));           
            Console.WriteLine("Ready to receive file " + DateTime.Now);
            string lenghtStr = "";
            string msg = "";
            do
            {
                length = clientSocket.Receive(data);
                lenghtStr += Encoding.Unicode.GetString(data, 0, length);
            } while (clientSocket.Available > 0);
            if(lenghtStr == "!EXIST")                                       
            {                                                               
                Console.WriteLine("File doesn't exist");
                end = DateTime.Now;
                msg = "finished " + end + "\nbitrate: " + 0 / (end - start).TotalSeconds / 1000 + " kb/s"
                    + "\n------------------------------------------------";
                clientSocket.Send(Encoding.ASCII.GetBytes(msg));
                Console.WriteLine(msg);
                clientSocket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(receiveCallback), clientSocket);
                DOWNLOAD = false;                                         
                return true;                                              
            }                                                             
            else                                                          
                fileLength = Convert.ToInt32(lenghtStr);                  

            Console.WriteLine($"file length = {fileLength} ");
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
                    clientSocket.Receive(data, (int)block, 0);
                    stream.Write(data, 0, (int)block);
                    offset += block;
                    if (offset == Convert.ToInt32(fileLength) - remnant)    //if EOF
                    {
                        block = remnant;
                    }
                } while (offset < fileLength);
            }
            end = DateTime.Now;
            msg = "finished " + end + "\nbitrate: " + fileLength / (end - start).TotalSeconds / 1000 + " kb/s" 
                +"\n------------------------------------------------";
            clientSocket.Send(Encoding.ASCII.GetBytes(msg));
            Console.WriteLine(msg);
            DOWNLOAD = false;
            
            clientSocket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(receiveCallback), clientSocket);
            return true;
        }

        static bool CheckFileExists(string filename)
        {
            if (!File.Exists(filename))
            {
                Console.WriteLine("file doesn't exist");
                return false;
            }
            return true;
        }

        static void loopConnect(int noOfRetry, int attemptPeriodInSeconds)
        {
            int attempts = 0;
            while (!clientSocket.Connected && attempts < noOfRetry)
            {
                try
                {
                    ++attempts;
                    IAsyncResult result = clientSocket.BeginConnect(IPAddress.Parse(SERVER_IP), PORT_NO, endConnectCallback, null);
                    result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(attemptPeriodInSeconds));
                    System.Threading.Thread.Sleep(attemptPeriodInSeconds * 1000);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error: ");
                }
            }
            if (!clientSocket.Connected)
            {
                Console.WriteLine("Connection attempt is unsuccessful!");
                return;
            }
        }

        private const int BUFFER_SIZE = 8192;
        private static byte[] buffer = new byte[BUFFER_SIZE];
        private static void endConnectCallback(IAsyncResult ar)
        {
            try
            {
                clientSocket.EndConnect(ar);
                if (clientSocket.Connected)
                {
                    clientSocket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(receiveCallback), clientSocket);
                }
                else
                {
                    Console.WriteLine("End of connection attempt, fail to connect...");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("End-connection attempt is unsuccessful! ");
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
                if (socket.Connected && !DOWNLOAD)
                {
                    int received = socket.EndReceive(result);
                    byte[] data;


                    if (received > 0)
                    {

                        receiveAttempt = 0;
                        data = new byte[received];
                        Buffer.BlockCopy(buffer, 0, data, 0, data.Length);
                        Console.WriteLine("Server: " + Encoding.UTF8.GetString(data));
                        socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(receiveCallback), socket);
                    }
                    else if (receiveAttempt < MAX_RECEIVE_ATTEMPT)
                    {
                        ++receiveAttempt;
                        socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(receiveCallback), socket);
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
                Console.WriteLine("receiveCallback is failed! ");
            }
        }
    }
}