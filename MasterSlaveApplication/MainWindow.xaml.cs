using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MasterSlaveApplication
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(String property)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(property));
            }
        }
        public class SlaveInfo : INotifyPropertyChanged
        {
            IPAddress ip;
            bool choosen;
            public SlaveInfo(IPAddress ip)
            {
                choosen = true;
                this.ip = ip;
            }
            public IPAddress getIP()
            {
                return ip;
            }
            public event PropertyChangedEventHandler PropertyChanged;
            private void OnPropertyChanged(String property)
            {
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(property));
                }
            }
            public string IP
            {
                get { return ip.ToString(); }
                set
                {
                    ip = IPAddress.Parse(value);
                    OnPropertyChanged("IP");
                }
            }
            public bool isChoosen
            {
                get { return choosen; }
                set
                {
                    choosen = value;
                    OnPropertyChanged("isChoosen");
                }
            }
        }
        public string TaskFileName
        {
            get{return taskFileName;}
            set
            {
                taskFileName = value;
                OnPropertyChanged("TaskFileName");
            }
        }
        public string InputFileName
        {
            get{return inputFileName;}
            set
            {
                inputFileName = value;
                OnPropertyChanged("InputFileName");
            }
        }
        private Master master;
        private ObservableCollection<SlaveInfo> slaves;
        public ObservableCollection<SlaveInfo> Slaves
        {
            get { return slaves; }
            set { slaves = value; }
        }
        private string taskFileName;
        private string inputFileName;
        private ObservableCollection<String> logMessages;
        public ObservableCollection<String> LogMessages
        {
            get{return logMessages;}
        }
        public MainWindow()
        {
            slaves = new ObservableCollection<SlaveInfo>() { new SlaveInfo(new IPAddress(0x0100007F)), new SlaveInfo(new IPAddress(0x0100007F)) };
            master = new Master();
            logMessages = new ObservableCollection<String>() { "Программа запущена" };
            inputFileName = "";
            taskFileName = "";
            master.Log += LogHandler;
            InitializeComponent();
            DataContext = this;
        }
        private void stopDiscover(object sender, RoutedEventArgs e)
        {
            master.stopDiscover();
        }
        private async void DiscoverWorkersButton_Click(object sender, RoutedEventArgs e)
        {
            DiscoverWorkersButton.Click -= DiscoverWorkersButton_Click;
            DiscoverWorkersButton.Click += stopDiscover;
            DiscoverWorkersButton.Content="Стоп";
            slaves.Clear();
            var discoverTask = master.discoverSlaves();
            List<IPAddress> addresses = await discoverTask;
            foreach (IPAddress address in addresses)
            {
                slaves.Add(new SlaveInfo(address));
            }
            DiscoverWorkersButton.Content = "Поиск работников";
            DiscoverWorkersButton.Click -= stopDiscover;
            DiscoverWorkersButton.Click += DiscoverWorkersButton_Click;
        }
        private void LogHandler(string message)
        {
            Dispatcher.BeginInvoke((Action)(delegate {Log(message);}));
        }
        private void Log(string message)
        {
            logMessages.Add(message);
            for(int i=logMessages.Count-1;i>11;i--) 
                logMessages.RemoveAt(i);
        }

        private void ExecuteLocalButton_Click(object sender, RoutedEventArgs e)
        {
            master.executeLocally(TaskFileName, InputFileName);
        }

        private void ExecuteOnWorkersButton_Click(object sender, RoutedEventArgs e)
        {
            List<IPAddress> workers=new List<IPAddress>();
            foreach(var slave in slaves)
            {
                if(slave.isChoosen)
                    workers.Add(slave.getIP());
            }
            master.sendTasks(workers,TaskFileName, InputFileName);
        }

        private void ChooseTaskButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Multiselect = false;
            openFileDialog.Filter = "DLL files (*.dll)|*.dll";
            openFileDialog.InitialDirectory = Assembly.GetEntryAssembly().Location;
            if (openFileDialog.ShowDialog() == true && openFileDialog.FileName != "")
            {
                TaskFileName = openFileDialog.FileName;
            }
        }
        private void ChooseInputButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Multiselect = false;
            openFileDialog.Filter = "Text files (*.txt)|*.txt";
            openFileDialog.InitialDirectory = Assembly.GetEntryAssembly().Location;
            if (openFileDialog.ShowDialog() == true && openFileDialog.FileName!="")
            {
                InputFileName = openFileDialog.FileName;
            }
        }
    }
}
