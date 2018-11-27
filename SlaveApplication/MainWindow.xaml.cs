using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
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

namespace SlaveApplication
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Slave slave;
        private String state = "Свободен";
        private ObservableCollection<String> logMessages;
        public ObservableCollection<String> LogItems
        {
            get { return logMessages; }
        }
        public String State
        {
            get { return state; }
            set { state = value; }
        }
        Thread udpThread;
        Thread tcpThread;
        public MainWindow()
        {
            logMessages = new ObservableCollection<String>() {"Программа запущена"};
            slave=new Slave();
            InitializeComponent();
            DataContext=this;
            slave.Log+=Log;
            slave.ExceptionRestart+=ExceptionHandler;
            RunSlave();
        }
        private void RunSlave()
        {
            udpThread = new Thread(slave.listenUDP);
            tcpThread = new Thread(slave.listenTCP);
            udpThread.Start();
            tcpThread.Start();
        }
        private void RestartSlave()
        {
            udpThread.Abort();
            tcpThread.Abort();
            udpThread = new Thread(slave.listenUDP);
            tcpThread = new Thread(slave.listenTCP);
            udpThread.Start();
            tcpThread.Start();
        }
        private void UpdateIP_Click(object sender, RoutedEventArgs e)
        {
            slave.MasterIP=this.ipTextBox.Text;
        }
        private void Log(string message)
        {
            logMessages.Add(message);
            for (int i = logMessages.Count - 1; i > 11; i--)
                logMessages.RemoveAt(i);
        }
        private void ExceptionHandler(string message)
        {
            Log(message);
            RestartSlave();
        }
        ~MainWindow()
        {
            udpThread.Abort();
            udpThread.Abort();
        }
    }
}
