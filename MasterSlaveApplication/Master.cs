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
    enum Header {INCORRECT=-1,ERROR=0,DATASEND=1 };
    class Master
    {
        public delegate void LogHandler(string message);
        public event LogHandler Log;

        int port;
        public Master()
        {
            port = 11256;
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
            if (workers.Count == 0)
                return;
            AppDomain appDomain = null;
            byte[] taskData;
            string inputString;
            byte[] inputData;
            List<TcpClient> clients = new List<TcpClient>();
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
                string assemblyName=assm.GetName().Name;
                Type t = assm.GetExportedTypes()[1];
                dynamic task = Activator.CreateInstance(t);
                /*MethodInfo validate = t.GetMethod("validate");
                MethodInfo showResults = t.GetMethod("showResults");*/
                if ((bool)task.validate(inputString) != true)
                {
                    Log("Входные данные имеют неправильный формат");
                    return;
                }
                /*if ((bool)validate.Invoke(null, new object[] { inputString }) != true)
                {
                    Log("Входные данные имеют неправильный формат");
                    return;
                }*/

                inputData = Encoding.UTF8.GetBytes(inputString);
                inputString = null;
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
                    sendTaskAndInput(netstream, taskData, inputData, metainfo, assemblyName);
                    clients.Add(tcp);
                    i++;
                }
                List<byte[]> outputs = new List<byte[]>();
                for (i = 0; i < clients.Count; i++)
                {
                    TcpClient tcp = clients[i];
                    NetworkStream netstream = tcp.GetStream();
                    byte[] output = recvTaskOutput(netstream);
                    int header = BitConverter.ToInt32(output, 0);
                    if (header == (int)Header.ERROR)
                    {
                        string message = Encoding.UTF8.GetString(output.Skip(sizeof(int)).ToArray());
                        throw new Exception(message);
                    }
                    outputs.Add(output.Skip(sizeof(int)).ToArray());
                    tcp.Close();
                    clients[i] = null;
                }
                //showResults.Invoke(null, new object[] { outputs });
                task.showResults(outputs);
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
                foreach (TcpClient tcp in clients)
                {
                    if (tcp != null)
                    {
                        tcp.Close();
                    }
                }
                if (appDomain != null)
                    AppDomain.Unload(appDomain);
            }
        }
        void sendOutput(NetworkStream netStream, Header header, byte[] data)
        {
            byte[] message = new byte[data.Length + sizeof(int)];
            int offset = 0;
            System.Buffer.BlockCopy(BitConverter.GetBytes((int)header), 0, message, offset, sizeof(int));
            offset += sizeof(int);
            System.Buffer.BlockCopy(BitConverter.GetBytes(data.Length), 0, message, offset, sizeof(int));
            sendChunk(netStream, message);
        }
        byte[] recvTaskOutput(NetworkStream netStream)
        {
            byte[] messageLength = new byte[4];
            netStream.Read(messageLength, 0, 4);
            int length = BitConverter.ToInt32(messageLength, 0);
            int chunkSize = 1024;
            byte[]data=new byte[length];
            int offset = 0;
            while (length > 0)
            {
                int size=Math.Min(1024,length);
                int read = netStream.Read(data,offset,size);
                offset += read;
                length -= read;
            }
            return data;
        }
        void sendTaskAndInput(NetworkStream netStream,byte[] taskData,byte[]inputData,byte[]metainfo,string assemblyname)
        {
            byte[] rv = new byte[sizeof(int) + sizeof(int) + sizeof(int) + taskData.Length + inputData.Length + metainfo.Length+assemblyname.Length];
            int offset = 0;
            System.Buffer.BlockCopy(BitConverter.GetBytes((int)Header.DATASEND), 0, rv, offset, sizeof(int));
            offset += sizeof(int);
            System.Buffer.BlockCopy(BitConverter.GetBytes(taskData.Length), 0, rv, offset, sizeof(int));
            offset += sizeof(int);
            System.Buffer.BlockCopy(BitConverter.GetBytes(inputData.Length), 0, rv, offset, sizeof(int));
            offset += sizeof(int);
            System.Buffer.BlockCopy(taskData, 0, rv, offset, taskData.Length);
            offset += taskData.Length;
            System.Buffer.BlockCopy(inputData, 0, rv, offset, inputData.Length);
            offset += inputData.Length;
            System.Buffer.BlockCopy(metainfo, 0, rv, offset, metainfo.Length);
            offset += metainfo.Length;
            System.Buffer.BlockCopy(Encoding.UTF8.GetBytes(assemblyname), 0, rv, offset, assemblyname.Length);
            sendChunk(netStream,rv);
        }
        void sendChunk(NetworkStream netStream,byte[]data)
        {
            netStream.Write(BitConverter.GetBytes(data.Length),0,sizeof(int));
            netStream.Write(data, 0, data.Length);
        }
    }
}
