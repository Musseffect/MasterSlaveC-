﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SlaveApplication
{
    enum Header {INCORRECT=-1,ERROR=0,DATASEND,SLAVEDISCOVER,SLAVEINFO };
    class Slave : INotifyPropertyChanged
    {
        public delegate void LogHandler(string message);
        public event LogHandler Log;
        public delegate void ExceptionHandler(string message);
        public event ExceptionHandler ExceptionRestart;

        private IPAddress masterIP=new IPAddress(0x0100007F);
        public string MasterIP
        {
            get { return masterIP.ToString(); }
            set { 
                masterIP = IPAddress.Parse(value);
                OnPropertyChanged("MasterIP");
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(String prop)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
        }
        private UdpClient udp;
        private TcpListener tcpListener;
        private int port;
        public Slave()
        {
            Init();
        }
        public static Header getMessageType(byte[] message)
        {
            return Header.INCORRECT;
        }
        public void Init(int port=11256)
        { 
            try{
            udp = new UdpClient(port);
            }catch (ArgumentOutOfRangeException exc)
            {
                //Incorrect port number
                Log("Incorrect port number");
            }
            catch(SocketException exc)
            {
                //Choosen port is already in use
                Log("Choosen port is already in use");
            }
            tcpListener = new TcpListener(IPAddress.Any,port);
            this.port=port;
        }
        public void listenUDP()
        {
            try
            {
                while (true)
                {
                    IPEndPoint endPoint = new IPEndPoint(masterIP,port);
                    byte[] message = udp.Receive(ref endPoint);
                    if (endPoint.Address == this.masterIP && getMessageType(message)==Header.SLAVEDISCOVER)
                    {
                        IPEndPoint ip = endPoint;
                        udp.Send(BitConverter.GetBytes((int)Header.SLAVEINFO),sizeof(int));
                    }
                }
            }
            catch (Exception exc)
            {
                Log(exc.Message);
            }
        }
        public void listenTCP()
        {
            try
            {
                tcpListener.Start();
            }
            catch (Exception exc)
            {
                Log(exc.Message);
            }
            while (true)
            {
                try{
                    TcpClient tcpClient = tcpListener.AcceptTcpClient();
                    processTcpClient(tcpClient);
                }catch(Exception exc)
                {
                    Log(exc.Message);
                }
            }
        }
        public void processTcpClient(TcpClient tcpClient)
        {
            byte[] metaInfo=null;
            byte[] taskData=null;
            byte[] inputData=null;
            loadData(tcpClient.GetStream(), out metaInfo, out taskData, out inputData);
            int header = BitConverter.ToInt32(metaInfo, 0);
            int workerNumber = BitConverter.ToInt32(metaInfo, 4);
            int workerCount = BitConverter.ToInt32(metaInfo, 8);
            executeTask(tcpClient,taskData, BitConverter.ToString(inputData),workerNumber,workerCount);
            tcpClient.Close();
        }
        void executeTask(TcpClient tcpClient,byte[] taskData, string data,int workerNumber, int workersCount)
        {
            AppDomain appDomain = null;
            try
            {
                appDomain = AppDomain.CreateDomain("TaskDomain");
                Assembly assm = appDomain.Load(taskData);
                Type t = assm.GetExportedTypes()[1];
                dynamic taskObject = Activator.CreateInstance(t);
                byte[] output=taskObject.execute(taskObject.parseData(data),workerNumber,workersCount);
                Log("Задание выполнено.");
                sendOutput(tcpClient.GetStream(), output);
                Log("Результаты отправлены назад.");
            }
            catch(Exception exc)
            {
                sendError(tcpClient.GetStream(), exc.Message);
                Log(exc.Message);
                //show error in log
            }finally
            {
                if (appDomain != null)
                    AppDomain.Unload(appDomain);
            }
        }
        void sendError(NetworkStream netStream,string message)
        {
            sendData(netStream, Header.ERROR, Encoding.ASCII.GetBytes(message));
        }
        void sendData(NetworkStream netStream,Header header,byte[]message)
        {
            netStream.Write(BitConverter.GetBytes((int)header), 0, sizeof(int));
            netStream.Write(message, 0, message.Length);
        }
        void sendOutput(NetworkStream netStream,byte[] data)
        {
            sendData(netStream,Header.DATASEND,data);
        }
        public void loadData(NetworkStream netStream, out byte[] metaInfo, out byte[] taskData, out byte[] inputData)
        {
            int header=0;
            metaInfo = readChunk(netStream, ref header);
            if (header != (int)(Header.DATASEND))
                throw new Exception("Некорректный заголовок пакета");
            taskData = readChunk(netStream, ref header);
            if (header != (int)(Header.DATASEND))
                throw new Exception("Некорректный заголовок пакета");
            inputData = readChunk(netStream, ref header);
            if (header != (int)(Header.DATASEND))
                throw new Exception("Некорректный заголовок пакета");
        }
        byte[] readChunk(NetworkStream netStream,ref int header)
        {
            byte[] blength = new byte[4];
            netStream.Read(blength, 0, 4);
            int length = BitConverter.ToInt32(blength, 0);
            length -= 4;
            if (length > 0)
            {
                byte[] headerBytes = new byte[4];
                netStream.Read(headerBytes, 0, 4);
                header = BitConverter.ToInt32(headerBytes,0);
                byte[] buffer = new byte[length];
                netStream.Read(buffer, 0, length);
                return buffer;
            }
            throw new Exception("Некорректный пакет");
        }
    }
}