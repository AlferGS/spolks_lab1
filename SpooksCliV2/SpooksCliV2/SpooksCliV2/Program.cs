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
        static int sizOfBlock = 8192;
        static bool FILESEND = false;
        static bool FILERECEIVE = false;
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
            {
                if (!FILESEND)
                {
                    if (!FILERECEIVE)
                        result = Console.ReadLine();
                }
                byte[] bytes = Encoding.ASCII.GetBytes(result);
                if (FILESEND)
                {
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
                            byte[] fileData = new byte[fileLength];
                            stream.Read(fileData, 0, Convert.ToInt32(fileLength));
                            int offset = 0;
                            int remnant = Convert.ToInt32(fileLength) % sizOfBlock;
                            do
                            {
                                byte[] buff = new byte[sizOfBlock];
                                buff = BufToBuf(fileData, buff, offset, sizOfBlock);
                                clientSocket.Send(buff);
                                offset += sizOfBlock;
                                if (offset == Convert.ToInt32(fileLength) - remnant)
                                {
                                    sizOfBlock = remnant;
                                }
                            } while (offset < fileLength);
                            sizOfBlock = 8192;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Server has disconnected");
                        break;
                    }
                    FILESEND = false;
                }
                else
                    try
                    {
                        if (result == "FILESEND")
                            FILESEND = true;
                        if (result == "FILERECEIVE")
                        {
                            FILERECEIVE = true;
                            clientSocket.Send(bytes);
                            {
                                byte[] data = new byte[100];
                                int length;
                                Console.WriteLine("Ready to receive file " + DateTime.Now);
                                string lenghtStr = "";
                                do
                                {
                                    length = clientSocket.Receive(data);
                                    lenghtStr += Encoding.Unicode.GetString(data, 0, length);
                                } while (clientSocket.Available > 0);
                                Console.WriteLine(lenghtStr);
                                var fileLength = Convert.ToInt32(lenghtStr);

                                Console.WriteLine($"file length = {fileLength} ");
                                using (FileStream stream = new FileStream(filename, FileMode.Create, FileAccess.ReadWrite))
                                {
                                    int offset = 0;
                                    do
                                    {
                                        data = new byte[sizOfBlock];
                                        clientSocket.Receive(data, sizOfBlock, 0);
                                        stream.Write(data, 0, sizOfBlock);
                                        offset += sizOfBlock;
                                    } while (offset < fileLength);
                                }
                                string msg = "finished " + DateTime.Now;
                                clientSocket.Send(Encoding.ASCII.GetBytes(msg));
                                Console.WriteLine(msg);
                                FILERECEIVE = false;
                            }
                            clientSocket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(receiveCallback), clientSocket);
                        }
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
                if (socket.Connected && !FILERECEIVE)
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