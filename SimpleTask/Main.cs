using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace SimpleTask
{
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
        public int numbersCount;
    }
    public static class Task
    {
        public static byte[] execute(TaskData taskData, int workerNumber, int workersCount)
        {
            long sum = 0;
            for (int i = workerNumber; i < taskData.numbersCount; i += workersCount)
            {
                sum += i;
            }
            return BitConverter.GetBytes(sum);
        }
        public static bool validate(string data)
        {
            int result;
            return Int32.TryParse(data, out result);
        }
        public static TaskData parseData(string serializedData)
        {
            TaskData taskData = new TaskData();
            try
            {
                taskData.numbersCount = Int32.Parse(serializedData);
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
        public static void showResults(List<byte[]> dataArrays)//ascending order of workerNumbers
        {
            long sum = 0;
            foreach (byte[] data in dataArrays)
            {
                long number = BitConverter.ToInt64(data, 0);
                sum += number;
            }
            MessageBox.Show("Результат: " + sum.ToString(), "Задание выполнено");
        }
    }
}
