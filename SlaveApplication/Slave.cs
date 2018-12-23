using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace SlaveApplication
{
    enum Header {INCORRECT=-1,ERROR=0,DATASEND=1,SLAVEDISCOVER=2,SLAVEINFO=3 };
    public class Slave : INotifyPropertyChanged
    {
        public delegate void LogHandler(string message);
        public event LogHandler Log;
        public delegate void ExceptionHandler(string message);
        public event ExceptionHandler ExceptionRestart;

        private String state = "Свободен";
        private IPAddress slaveIP;
        public String State
        {
            get { return state; }
            set
            {
                state = value;
                OnPropertyChanged("State");
            }
        }
        public string IP
        {
            get { return slaveIP.ToString(); }
            set {
                slaveIP = IPAddress.Parse(value);
                OnPropertyChanged("MasterIP");
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(String prop)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
        }
        private TcpListener tcpListener;
        private int port;
        public Slave()
        {
            Init();
        }
        private static Header getMessageType(byte[] message)
        {
            int header=BitConverter.ToInt32(message,0);
            if (Enum.IsDefined(typeof(Header), header))
                return (Header)header;
            return Header.INCORRECT;
        }
        public static IPAddress getIPAddress()
        {
            IPHostEntry host;
            host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (IPAddress ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    return ip;
            }
            return IPAddress.Any;
        }
        public void Init(int port=11256)
        {
            tcpListener = new TcpListener(IPAddress.Any, port);
            slaveIP = getIPAddress();
            this.port=port;
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
                return;
            }
            while (true)
            {
                try{
                    TcpClient tcpClient = tcpListener.AcceptTcpClient();
                    this.State = "Занят";
                    processTcpClient(tcpClient);
                }catch(Exception exc)
                {
                    Log(exc.Message);
                }finally
                {
                    this.State="Свободен";
                }
            }
            tcpListener.Stop();
        }
        public void processTcpClient(TcpClient tcpClient)
        {
            byte[] taskData=null;
            string inputData="";
            try
            {
                int workerNumber=0;
                int workerCount = 0;
                string assemblyName = "";
                Stopwatch sw = new Stopwatch();
                sw.Start();
                loadData(tcpClient.GetStream(), out taskData, out inputData, out workerNumber, out workerCount,out assemblyName);
                executeTask(tcpClient, taskData,assemblyName, inputData, workerNumber, workerCount);
                sw.Stop();
                Log("Время выполнения: "+Convert.ToString(sw.ElapsedMilliseconds*0.001)+"сек.");
            }
            catch (Exception exc)
            {
                sendError(tcpClient.GetStream(), exc.Message);
                Log(exc.Message);
            }
            tcpClient.Close();
        }
        private static void appdomainCallback()
        {
            string inputString=(string)AppDomain.CurrentDomain.GetData("inputString");
            int workerNumber=(int)AppDomain.CurrentDomain.GetData("workerNumber");
            int workersCount=(int)AppDomain.CurrentDomain.GetData("workersCount");
            string assemblyName = (string)AppDomain.CurrentDomain.GetData("assemblyName");
            Assembly assm = AppDomain.CurrentDomain.Load(assemblyName);
            Type t = assm.GetExportedTypes()[0];
            dynamic task = Activator.CreateInstance(t); 
            task.validate(inputString);
            byte[] output = task.execute(task.parseData(inputString), workerNumber, workersCount);
            AppDomain.CurrentDomain.SetData("output",output);
        
        }
        void executeTask(TcpClient tcpClient,byte[] taskData,string assemblyName, string inputString,int workerNumber, int workersCount)
        {
            AppDomain appDomain = null;
            File.WriteAllBytes(AppDomain.CurrentDomain.BaseDirectory+assemblyName+".dll",taskData);
            try
            {
                AppDomainSetup setup = new AppDomainSetup();
                setup.ApplicationBase = AppDomain.CurrentDomain.BaseDirectory;
                setup.LoaderOptimization = LoaderOptimization.MultiDomainHost;
                appDomain = AppDomain.CreateDomain("TaskDomain",null,setup);
                appDomain.SetData("inputString",inputString);
                appDomain.SetData("workerNumber",workerNumber);
                appDomain.SetData("workersCount",workersCount);
                appDomain.SetData("assemblyName", assemblyName);
                appDomain.DoCallBack(new CrossAppDomainDelegate(appdomainCallback));
                //appDomain.DoCallBack(()=> assm = AppDomain.CurrentDomain.Load(assemblyName));
                /*Type t = assm.GetExportedTypes()[1];
                MethodInfo validate = t.GetMethod("validate");
                MethodInfo execute = t.GetMethod("execute");
                MethodInfo parseData = t.GetMethod("parseData");
                MethodInfo showResults = t.GetMethod("showResults");
                validate.Invoke(null, new object[] { inputString });
                object data = parseData.Invoke(null, new object[] { inputString });
                byte[] output = (byte[])execute.Invoke(null, new object[] { data, workerNumber, workersCount });*/
                //task.validate(inputString);
                //byte[] output = task.execute(task.parseData(inputString), workerNumber, workersCount);
                byte[] output = (byte[])appDomain.GetData("output");
                Log("Задание выполнено.");
                sendOutput(tcpClient.GetStream(),Header.DATASEND, output);
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
            sendOutput(netStream, Header.ERROR, Encoding.UTF8.GetBytes(message));
        }
        void sendChunk(NetworkStream netStream, byte[] data)
        {
            netStream.Write(BitConverter.GetBytes(data.Length), 0, sizeof(int));
            netStream.Write(data, 0, data.Length);
        }
        void sendOutput(NetworkStream netStream,Header header,byte[] data)
        {
            byte[]message=new byte[data.Length+sizeof(int)];
            int offset = 0;
            System.Buffer.BlockCopy(BitConverter.GetBytes((int)header), 0, message, offset, sizeof(int));
            offset += sizeof(int);
            System.Buffer.BlockCopy(data, 0, message, offset, data.Length);
            sendChunk(netStream,message);
        }
        public void loadData(NetworkStream netStream, out byte[] taskData, out string inputData, out int workerNumber, out int workerCount,out string assemblyName)
        {
            byte[] data = readChunk(netStream);
            int header=BitConverter.ToInt32(data,0);
            if (header != (int)(Header.DATASEND))
                throw new Exception("Некорректный заголовок пакета");
            int taskSize=BitConverter.ToInt32(data,sizeof(int));
            int inputSize = BitConverter.ToInt32(data, 2*sizeof(int));
            int metaSize=sizeof(int)*2;
            int offset = sizeof(int) * 3;
            taskData=new ArraySegment<byte>(data,offset,taskSize).ToArray();
            offset+=taskSize;
            inputData=Encoding.UTF8.GetString(data,offset,inputSize);
            offset+=inputSize;
            workerNumber = BitConverter.ToInt32(data, offset);
            offset+=sizeof(int);
            workerCount = BitConverter.ToInt32(data, offset);
            offset+=sizeof(int);
            assemblyName= Encoding.UTF8.GetString(data,5*sizeof(int)+taskSize+inputSize,data.Length-offset);
        }
        byte[] readChunk(NetworkStream netStream)
        {
            byte[] blength = new byte[4];
            netStream.Read(blength, 0, 4);
            int length=BitConverter.ToInt32(blength, 0);
            byte[] buffer = new byte[length];
            int offset=0;
            int chunkSize=1024;
            while (length > 0)
            {
                int size=Math.Min(length, chunkSize);
                int read=netStream.Read(buffer, offset, size);
                offset += read;
                length -= read;
            }
            return buffer;
        }
    }
}
