using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;

namespace Tets
{
    //Developed By          : Muhammad Adib aka mysayasan.github.io
    //Date Origin           : 2019-04-11
    //Program Function      : To retrieved PLC data by using Modbus/TCP protocol
    //Program Origin        : Originated from windows WPF modbus 1st version, same author
    //Disclaimer            : Free to share but please give credit to author. Thanks
    //Contact me            : mysayasan@gmail.com kalau ada job ke apa jgn segan2 contact
    //Language & Framework  : C#, .NET Core    
    //Why named Tets        : Typo from the word 'test' and let it be.. lantak lah    

    //Collection of observable Modbus client
    public class ModbusClientCollection : ObservableCollection<ModbusClient>
    {
        public ModbusClientCollection() : base() { }
    }   

    class Program
    {

        //Create observable modbus clients
        private static ModbusClientCollection modbusclients = new ModbusClientCollection();
        //Create threads for multiple clients
        private static List<Thread> threads = new List<Thread>();


        static void Main(string[] args)
        {
            //Get arguments for config file
            //Sample config file can be found with bundle
            string filePath = Path.Combine(args);
            XMLUtils.DeserializeXMLToObject<ModbusClientCollection>(out modbusclients, filePath);
            if (modbusclients == null) modbusclients = new ModbusClientCollection();            
            if (modbusclients.Count < 1)
            {
                Console.Write("\nNo modbus client found... ");
                Console.Write("\nPress any key to exit... ");
                Environment.Exit(0);
            }
            else
            {
                Console.Write(string.Format("\nFound {0} modbus client(s)\n", modbusclients.Count()));
                // Thread.Sleep(100);                
                Console.Write("\nProgram will run in loop\nPress any key to continue... \nAnd press again to exit... \n");
                Console.ReadLine();
                Console.Write("\nStarting modbus client...\n");

                //Run Modbus clients 
                RunModbusses();
                Console.ReadLine();
            }
        }

        private static void RunModbusses()
        {
            foreach (ModbusClient work in modbusclients)
            {
                ModbusClient w = work;
                RunModbus(work);
                Thread.Sleep(100);
            }
        }

        private static void RunModbus(ModbusClient client)
        {
            switch (client.ConnStat)
            {
                case ConnStatus.DISCONNECTED:
                    {
                        Thread oldthread = threads.FirstOrDefault(i => i.Name == client.ID.ToString());
                        if (oldthread != null)
                        {
                            threads.Remove(oldthread);
                        }
                        else
                        {
                            client.OnResponseEvent += new EventHandler<ModbusClient.ModbusResponseEventArgs>(work_OnResponseEvent);
                            client.OnNotificationEvent += new EventHandler<ModbusClient.ModbusNotificationEventArgs>(work_OnInfoEvent);
                        }

                        try
                        {
                            Thread thread = new Thread(new ThreadStart(() => client.Connect())) { Name = client.ID.ToString(), IsBackground = true };
                            thread.Start();
                            threads.Add(thread);
                            Thread.Sleep(100);
                        }
                        catch (ThreadStateException ex)
                        {
                            Console.WriteLine(ex.Message);
                        }
                        break;
                    }
                case ConnStatus.CONNECTED:
                    {
                        Console.WriteLine(string.Format("ID:{0}, is currently running!", client.ID));
                        break;
                    }
                case ConnStatus.AWAITING:
                    {
                        Console.WriteLine(string.Format("ID:{0}, is currently awaiting for connection!", client.ID));
                        break;
                    }
            }
        }

        private static void work_OnResponseEvent(object sender, ModbusClient.ModbusResponseEventArgs e)
        {
            Console.WriteLine(string.Format("[{0}] {1} >> {2}", e.id, e.ip, e.data));
        }

        private static void work_OnInfoEvent(object sender, ModbusClient.ModbusNotificationEventArgs e)
        {
            Console.WriteLine(string.Format("[{0}] {1} >> {2}", e.id, e.ip, e.data));
        }
    }
}