using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;




namespace MasterSlaveApplication
{
    enum Header {INCORRECT=-1,ERROR=0,DATASEND=1,SLAVEDISCOVER=2,SLAVEINFO=3 };
    class Master
    {
        public delegate void LogHandler(string message);
        public event LogHandler Log;

        int port;
        bool discoverIsRunning;
        public Master()
        {
            port = 11256;
        }
        async public Task<List<IPAddress>> discoverSlaves()
        {
            List<IPAddress> workers=new List<IPAddress>();
            discoverIsRunning = true;
            try
            {
                UdpClient udp;
                try
                {
                    udp = new UdpClient(port);
                    udp.EnableBroadcast = true;
                }
                catch (ArgumentOutOfRangeException exc)
                {
                    Log("Incorrect port number");
                    return workers;
                }
                catch (SocketException exc)
                {
                    Log("Choosen port is already in use");
                    return workers;
                }
                IPEndPoint ip = new IPEndPoint(IPAddress.Broadcast, 11256);
                byte[] message = BitConverter.GetBytes((Int32)Header.SLAVEDISCOVER);
                udp.Send(message, message.Length, ip);
                while(discoverIsRunning)
                {
                    var timeoutTask = Task.Delay(10000);
                    var receiveTask = udp.ReceiveAsync();
                    if(timeoutTask == await Task.WhenAny(timeoutTask,receiveTask))
                    {
                        discoverIsRunning=false;
                        break;
                    }
                    UdpReceiveResult udpReceiveResult = await receiveTask;
                    byte []recv = udpReceiveResult.Buffer;

                    IPEndPoint remoteIPEndPoint = udpReceiveResult.RemoteEndPoint;
                    if (recv == null || recv.Length == 0)
                        continue;
                    if (BitConverter.ToInt32(recv, 0) == (Int32)Header.SLAVEINFO)
                    {
                        workers.Add(remoteIPEndPoint.Address);
                    }
                }
                udp.Close();
            }catch(SocketException exc)
            {
                Log(exc.Message);
            }
            return workers;
        }
        public dynamic loadTask(string filePath)
        {
            var a = Assembly.Load(filePath);
            Type t = a.GetExportedTypes()[0];
            dynamic taskObject = Activator.CreateInstance(t);
            return taskObject;
        }
        public void setPort(int port = 11256)
        {
            this.port = port;
        }
        public void executeLocally(string taskPath, string inputPath)
        {
            AppDomain appDomain = null;
            byte[] taskData;
            string inputString;
            try
            {
                taskData = File.ReadAllBytes(taskPath);
                inputString = File.ReadAllText(inputPath);
            }
            catch (Exception exc)
            {
                Log(exc.Message);
                return;
            }
            try
            {
                appDomain = AppDomain.CreateDomain("TaskDomain");
                Assembly assm = appDomain.Load(taskData);
                Type t = assm.GetExportedTypes()[1];
                MethodInfo validate = t.GetMethod("validate");
                MethodInfo execute = t.GetMethod("execute");
                MethodInfo parseData = t.GetMethod("parseData");
                MethodInfo showResults = t.GetMethod("showResults");
                if ((bool)validate.Invoke(null, new object[] { inputString }) != true)
                {
                    Log("Входные данные имеют неправильный формат");
                    return;
                }
                object data = parseData.Invoke(null, new object[] { inputString });
                byte[] output = (byte[])execute.Invoke(null,new object[]{data, 0, 1});
                Log("Задание выполнено.");
                showResults.Invoke(null, new object[] { new List<byte[]> { output } });
                /*dynamic taskObject = Activator.CreateInstance(t);
                taskObject.validate(BitConverter.ToString(inputData));
                byte[] output = taskObject.execute(taskObject.parseData(BitConverter.ToString(inputData)), 0, 1);
                Log("Задание выполнено.");
                taskObject.showResults(new List<byte[]> { output});*/
            }
            catch (Exception exc)
            {
                Log(exc.Message);
                return;
            }
            finally
            {
                if (appDomain != null)
                    AppDomain.Unload(appDomain);
            }
        }
        public void sendTasks(List<IPAddress> workers,string taskPath,string inputPath)
        {
            AppDomain appDomain = null;
            byte[] taskData;
            string inputString;
            byte[] inputData;
            try
            {
                taskData = File.ReadAllBytes(taskPath);
                inputString = File.ReadAllText(inputPath);
            }
            catch (Exception exc)
            {
                Log(exc.Message);
                return;
            }
            try
            {
                appDomain = AppDomain.CreateDomain("TaskDomain");
                Assembly assm = appDomain.Load(taskData);
                Type t = assm.GetExportedTypes()[1];
                MethodInfo validate = t.GetMethod("validate");
                MethodInfo showResults = t.GetMethod("showResults");
                if ((bool)validate.Invoke(null, new object[] { inputString }) != true)
                {
                    Log("Входные данные имеют неправильный формат");
                    return;
                }

                inputData = Encoding.UTF8.GetBytes(inputString);
                inputString = null;
                List<TcpClient> clients = new List<TcpClient>();
                int i = 0;
                foreach (IPAddress worker in workers)
                {
                    TcpClient tcp = new TcpClient();
                    IPEndPoint endPoint = new IPEndPoint(worker, port);
                    tcp.Connect(endPoint);
                    NetworkStream netstream = tcp.GetStream();
                    byte[] metainfo = new byte[8];
                    using (MemoryStream stream = new MemoryStream())
                    {
                        var writer = new BinaryWriter(stream);

                        writer.Write(i);
                        writer.Write(workers.Count);

                        writer.Close();
                        metainfo = stream.ToArray();
                    }
                    sendTaskAndInput(netstream, taskData, inputData,metainfo);
                    clients.Add(tcp);
                    i++;
                }
                List<byte[]> outputs = new List<byte[]>();
                foreach (TcpClient tcp in clients)
                {
                        NetworkStream netstream = tcp.GetStream();
                        byte[] output = recvTaskOutput(netstream);
                        outputs.Add(output);
                        tcp.Close();
                        //call merge on task
                }
                showResults.Invoke(null, new object[] { outputs });
            }
            catch (SocketException exc)
            {
                Log(exc.Message);
            }
            catch (ObjectDisposedException exc)
            {
                Log(exc.Message);
                //Socket was closed
            }
            catch (Exception exc)
            {
                Log(exc.Message);
                return;
            }
            finally
            {
                if (appDomain != null)
                    AppDomain.Unload(appDomain);
            }
        }
        byte[] recvTaskOutput(NetworkStream netStream)
        {
            byte[] blength=new byte[4];
            netStream.Read(blength,0,4);
            int length=BitConverter.ToInt32(blength,0);
            byte[] headerBytes = new byte[4];
            netStream.Read(headerBytes, 0, 4);
            int header = BitConverter.ToInt32(headerBytes,0);
            length-=4;
            byte[]buffer=null;
            if(length>0)
            {
                buffer=new byte[length]; 
                int bufferCapacity=length;
                netStream.Read(buffer,0,bufferCapacity);
            }
            if (header == (int)Header.ERROR)
            {
                string message = Encoding.UTF8.GetString(buffer);
                throw new Exception(message);
            }
            return buffer;
        }
        void sendTaskAndInput(NetworkStream netStream,byte[] taskData,byte[]inputData,byte[]metainfo)
        {
            sendChunk(netStream, Header.DATASEND, metainfo);
            sendChunk(netStream,Header.DATASEND, taskData);
            sendChunk(netStream, Header.DATASEND, inputData);





        }
        void sendChunk(NetworkStream netStream,Header header,byte[]data)
        {
            netStream.Write(BitConverter.GetBytes(sizeof(int)+data.Length),0,sizeof(int));
            netStream.Write(BitConverter.GetBytes((int)header), 0, sizeof(int));
            netStream.Write(data, 0, data.Length);
        }
        public void stopDiscover()
        {
            discoverIsRunning=false;
        }
    }
}
