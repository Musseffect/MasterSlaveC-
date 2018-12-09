using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
namespace FractalRenderTask
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
        public int width;
        public int height;
        public int numbersCount;
    }
    public class Task
    {
        private Vector3 getColor(int x, int y, int width, int height)
        {
            return new Vector3(0.0f,0.0f,0.0f);
        }
        public byte[] execute(TaskData taskData,int workerNumber,int workersCount)
        {
            int pixels = taskData.width * taskData.height;
            int tileSize = (int)Math.Floor((float)(pixels - workerNumber) / (float)workersCount);
            float[] pixelColors = new float[3 * tileSize];
            for (int i = workerNumber; i < pixels; i += workersCount)
            {
                Vector3 res = getColor(i % taskData.width, i / taskData.width, taskData.width, taskData.height);
                pixelColors[i * 3] = res.X;
                pixelColors[i * 3 + 1] = res.Y;
                pixelColors[i * 3 + 2] = res.Z;
            }
            byte[] result = new byte[sizeof(float) * pixelColors.Length];
            Buffer.BlockCopy(pixelColors, 0, result, 0, result.Length);
            return result;
        }
        public bool validate(string data)
        { 
            int result;
            return Int32.TryParse(data,out result);
        }
        public TaskData parseData(string serializedData)
        {
            TaskData taskData=new TaskData();
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
        public void showResults(List<byte[]> dataArrays)//ascending order of workerNumbers
        {
            long sum = 0;
            foreach (byte[] data in dataArrays)
            {
                long number = BitConverter.ToInt64(data,0);
                sum += number;
            }
            MessageBox.Show("Результат: "+sum.ToString(),"Задание выполнено");
        }
    }
    public class Camera
    {
        Vector3 pos;
        Vector3 dir;
        Vector3 up;
        float aperture;
        float focalDist;
        float fov;
    }
}
