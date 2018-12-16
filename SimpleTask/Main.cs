using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace SimpleTask
{
    [Serializable]
    public class Task
    {
        public Task()
        { 
        }
        public byte[] execute(TaskData taskData, int workerNumber, int workersCount)
        {
            ulong sum = 0;
            for (ulong i = (ulong)workerNumber; i < taskData.numbersCount; i += (ulong)workersCount)
            {
                sum += i;
            }
            return BitConverter.GetBytes(sum);
        }
        public bool validate(string data)
        {
            ulong result=UInt64.Parse(data);
            return true;
        }
        public TaskData parseData(string serializedData)
        {
            TaskData taskData = new TaskData();
            try
            {
                taskData.numbersCount = UInt64.Parse(serializedData);
            }
            catch (System.FormatException exc)
            {
                throw new Exception("Исходные данные представлены в неверном формате");
            }
            catch (Exception exc)
            {
                throw exc;
            }
            return taskData;
        }
        public void showResults(List<byte[]> dataArrays)//ascending order of workerNumbers
        {
            ulong sum = 0;
            foreach (byte[] data in dataArrays)
            {
                ulong number = BitConverter.ToUInt64(data, 0);
                sum += number;
            }
            MessageBox.Show("Результат: " + sum.ToString(), "Задание выполнено");
        }
    }
    public class TaskData
    {
        //camera position
        //camera direction
        //camera up vector
        //camera dof
        //camera aa
        //camera mod - linear or spherical perspective - ortho
        //camera fov
        //Mandelbox params
        //Scale
        //bla bla bla
        //etc
        public ulong numbersCount;
        public TaskData()
        {
        }
    }
}
