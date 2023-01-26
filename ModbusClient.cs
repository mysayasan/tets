using System;
using System.Linq;
using System.Xml.Serialization;
using System.ComponentModel;
using System.Net.Sockets;
using System.Net;
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
    
    [Serializable()]
    public class ProtocolDataUnit
    {
        [System.Xml.Serialization.XmlElement("FunctionCode")]
        public ModbusFunctionCode FunctionCode { get; set; }

        [System.Xml.Serialization.XmlElement("StartAddress")]
        public ushort StartAddress { get; set; }

        [System.Xml.Serialization.XmlElement("DataLen")]
        public ushort DataLen { get; set; }
    }

    public enum ModbusFunctionCode
    {
        [XmlEnum("1")]
        READ_OUT_COILS = 0x01,

        [XmlEnum("2")]
        READ_IN_CONTACTS = 0x02,

        [XmlEnum("3")]
        READ_OUT_REGS = 0x03,

        [XmlEnum("4")]
        READ_IN_REGS = 0x04,

        [XmlEnum("5")]
        WRITE_SINGLE_OUT_COIL = 0x05,

        [XmlEnum("6")]
        WRITE_SINGLE_OUT_REG = 0x06,

        [XmlEnum("15")]
        WRITE_MULTI_OUT_COILS = 0x0f,

        [XmlEnum("16")]
        WRITE_MULTI_OUT_REGS = 0x10,
    }
    public enum ConnStatus
    {
        [XmlEnum("0")]
        DISCONNECTED = 0,

        [XmlEnum("1")]
        CONNECTED = 1,

        [XmlEnum("2")]
        AWAITING = 2,

        [XmlEnum("3")]
        SLEEPING = 3
    }

    public enum CommProtocol
    {
        [XmlEnum("0")]
        MODBUSTCP = 0,

        [XmlEnum("1")]
        XGTDP = 1,
    }

    public enum SendActionMethod
    {
        [XmlEnum("0")]
        FIX = 0,

        [XmlEnum("1")]
        DATABASE = 1,

        [XmlEnum("2")]
        TEXTFILE = 2,
    }

    public enum SendFailedActionMethod
    {
        [XmlEnum("0")]
        NONE = 0,

        [XmlEnum("1")]
        DATABASE = 1,

        [XmlEnum("2")]
        TEXTFILE = 2,
    }

    public enum RecvActionMethod
    {
        [XmlEnum("0")]
        NONE = 0,

        [XmlEnum("1")]
        DATABASE = 1,

        [XmlEnum("2")]
        TEXTFILE = 2,
    }

    public enum SendValueType
    {
        [XmlEnum("0")]
        UNSIGNED_SHORT = 0,

        [XmlEnum("1")]
        SIGNED_SHORT = 1,

        [XmlEnum("2")]
        HEXA = 2,
    }

    public enum RecvValueType
    {
        [XmlEnum("0")]
        UNSIGNED_SHORT = 0,

        [XmlEnum("1")]
        SIGNED_SHORT = 1,

        [XmlEnum("2")]
        HEXA = 2,
    }

    public class ModbusClient : INotifyPropertyChanged
    {
        // ------------------------------------------------------------------------
        /// <summary> Private declarations </summary>
        // ------------------------------------------------------------------------
        private ushort id;
        private string ip = "192.168.0.99";
        private ushort port = 2004;
        private ushort timeout = 100;
        private ushort refresh = 1;
        //private bool connected = false;
        private static String response = String.Empty;
        private readonly object padlock = new object();
        private Socket client;
        private byte[] buffer = new byte[1024];
        private volatile bool keeprun = true;
        private int retryTimes = 3;
        private int retryCount = 0;
        private ConnStatus connstat = ConnStatus.DISCONNECTED;
        private string localPort;
        private bool persistantConn = true;
        private int reconnectDelay = 5;
        private ProtocolDataUnit pdu = new ProtocolDataUnit() { FunctionCode = ModbusFunctionCode.READ_OUT_REGS, StartAddress = 30001, DataLen = 12 };


        // ------------------------------------------------------------------------
        /// <summary> Property Changed Notification entity </summary>
        // ------------------------------------------------------------------------
        public ushort ID
        {
            get { return id; }
            set { id = value; }
        }
        public string IP
        {
            get { return ip; }
            set
            {
                ip = value;
                OnPropertyChanged("IP");
            }
        }
        public ushort Port
        {
            get { return port; }
            set
            {
                port = value;
                OnPropertyChanged("Port");
            }
        }
        public ushort Timeout
        {
            get { return timeout; }
            set
            {
                OnPropertyChanged("Timeout");
                timeout = value;
            }
        }
        public ushort Refresh
        {
            get { return refresh; }
            set
            {
                OnPropertyChanged("Refresh");
                refresh = value;
            }
        }
        public int RetryTimes
        {
            get { return retryTimes; }
            set
            {
                OnPropertyChanged("RetryTimes");
                retryTimes = value;
            }
        }

        public bool PersistantConnection
        {
            get { return persistantConn; }
            set
            {
                OnPropertyChanged("PersistantConnection");
                persistantConn = value;
            }
        }

        public int ReconnectDelay
        {
            get { return reconnectDelay; }
            set
            {
                OnPropertyChanged("ReconnectDelay");
                reconnectDelay = value;
            }
        }

        [XmlIgnore()]
        public ConnStatus ConnStat
        {
            get { return connstat; }
            set
            {
                if (value == connstat) return;
                connstat = value;
                OnPropertyChanged("ConnStat");
            }
        }

        [XmlIgnore()]
        public string LocalPort
        {
            get { return localPort; }
            set
            {
                if (value == localPort) return;
                localPort = value;
                OnPropertyChanged("LocalPort");
            }
        }

        public ProtocolDataUnit PDU
        {
            get { return pdu; }
            set
            {
                if (value == pdu) return;
                pdu = value;
                OnPropertyChanged("PDU");
            }
        }

        // ------------------------------------------------------------------------
        /// <summary> Property Changed Event Handler </summary>
        // ------------------------------------------------------------------------
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        // ------------------------------------------------------------------------
        /// <summary> Create MBAP Header : More information on how to create, silalah melawat https://www.simplymodbus.ca/TCP.htm </summary>
        // ------------------------------------------------------------------------
        private void CreateMBAPHeader(ushort transId, ushort protocolId, ushort msgLen, ushort unitId, out byte[] data)
        {
            //Create Identity
            data = new byte[6 + msgLen];


            if (!BitConverter.IsLittleEndian)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(transId), 0, data, 0, 2);
                Buffer.BlockCopy(BitConverter.GetBytes(protocolId), 0, data, 2, 2);
                Buffer.BlockCopy(BitConverter.GetBytes(msgLen), 0, data, 4, 2);
            }
            else
            {
                Buffer.BlockCopy(BitConverter.GetBytes(transId << 8 | transId >> 8), 0, data, 0, 2);
                Buffer.BlockCopy(BitConverter.GetBytes(protocolId << 8 | protocolId >> 8), 0, data, 2, 2);
                Buffer.BlockCopy(BitConverter.GetBytes(msgLen << 8 | msgLen >> 8), 0, data, 4, 2);
            }

            data[6] = (byte)unitId;
            //data[0] = (byte)transId;
            //data[1] = (byte)(transId >> 8);            
        }

        // ------------------------------------------------------------------------
        /// <summary> Create Modbus Command : More information on how to create, silalah melawat https://www.simplymodbus.ca/TCP.htm </summary>
        // ------------------------------------------------------------------------
        public byte[] CreateModbusCmd()
        {
            byte[] data;


            CreateMBAPHeader(1, 0, 6, 1, out data);
            data[7] = (byte)pdu.FunctionCode;

            if (!BitConverter.IsLittleEndian)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(pdu.StartAddress), 0, data, 8, 2);
                Buffer.BlockCopy(BitConverter.GetBytes(pdu.DataLen), 0, data, 10, 2);
            }
            else
            {
                Buffer.BlockCopy(BitConverter.GetBytes(pdu.StartAddress << 8 | pdu.StartAddress >> 8), 0, data, 8, 2);
                Buffer.BlockCopy(BitConverter.GetBytes(pdu.DataLen << 8 | pdu.DataLen >> 8), 0, data, 10, 2);
            }
            return data;
        }

        // ------------------------------------------------------------------------
        /// <summary> Modbus Event Handler </summary>
        // ------------------------------------------------------------------------
        public event EventHandler<ModbusResponseEventArgs> OnResponseEvent;
        public event EventHandler<ModbusNotificationEventArgs> OnNotificationEvent;

        public class ModbusResponseEventArgs : EventArgs
        {
            public ushort id { get; set; }
            public string ip { get; set; }
            public string data { get; set; }
        }

        public class ModbusNotificationEventArgs : EventArgs
        {
            public ushort id { get; set; }
            public string ip { get; set; }
            public int notice { get; set; }
            public int code { get; set; }
            public string data { get; set; }
        }

        private void CallNotification(ModbusNotification notice, int code, string message)
        {
            if ((int)notice == 2) Disconnect();

            var handler = OnNotificationEvent;

            if (handler != null)
                handler(this, new ModbusNotificationEventArgs() { id = id, ip = ip, notice = (int)notice, code = code, data = string.Format("{0}!, {1}", notice, message) });
        }

        private enum ModbusNotification
        {
            INFO = 0,
            ERROR = 1,
            ERRORDC = 2
        }

        private void CallResponse(string response)
        {
            //if (client == null) return;            
            var handler = OnResponseEvent;
            if (handler != null)
                handler(this, new ModbusResponseEventArgs() { id = id, ip = ip, data = response });
        }

        // ------------------------------------------------------------------------
        /// <summary> Manual reset event for Asyn Connection </summary>
        // ------------------------------------------------------------------------
        private static ManualResetEvent connectDone = new ManualResetEvent(false);
        private static ManualResetEvent sendDone = new ManualResetEvent(false);
        private static ManualResetEvent receiveDone = new ManualResetEvent(false);

        //Constant
        private const int COMMON_NOTICE = 0;
        private const int COMMON_ERROR = 1;
        private const int SOCKET_ERROR_FORCE_CLOSED = 10054;
        private const int SOCKET_ERROR_RESPONSE_TIMEOUT = 10060;
        private const int SOCKET_ERROR_CONN_REFUSED = 10061;
        private const int SOCKET_UNREACH_NETWORK = 10051;
        private const int SOCKET_UNREACH_HOST = 10065;
        private const int MODBUS_HEADER_SIZE = 8;

        // ------------------------------------------------------------------------
        /// <summary> Modbus Main Events </summary>
        // ------------------------------------------------------------------------
        public void Connect()
        {
            IPAddress remoteIP = IPAddress.Parse(ip);
            IPEndPoint remoteEP = new IPEndPoint(remoteIP, port);
            client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            client.ReceiveTimeout = timeout * 1000;
            client.SendTimeout = timeout * 1000;
            client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

            try
            {
                ConnStat = ConnStatus.AWAITING;
                CallNotification(ModbusNotification.INFO, COMMON_NOTICE, "Connecting...");

                //Code to prevent stuck on awaiting
                IAsyncResult result = client.BeginConnect(remoteEP,
                    new AsyncCallback(ConnectCallback), client);
                bool success = result.AsyncWaitHandle.WaitOne(timeout * 1000, true);

                if (!success)
                {
                    client.Close();
                    retryCount += 1;
                    ConnStat = ConnStatus.SLEEPING;
                    CallNotification(ModbusNotification.ERROR, 999, string.Format("Failed to connect: Reconnecting in {0:0} secs... attempt {1}/{2}", refresh, retryCount, retryTimes));
                    lock (padlock)
                    {
                        Monitor.Wait(padlock, TimeSpan.FromSeconds(refresh));
                    }
                    Connect();
                }
                else
                {
                    LocalPort = ((IPEndPoint)client.LocalEndPoint).Port.ToString();
                }

            }
            catch (Exception e)
            {
                ConnStat = ConnStatus.DISCONNECTED;
                //Connected = false;
                CallNotification(ModbusNotification.ERROR, COMMON_ERROR, e.Message);
            }
        }

        private void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                client = (Socket)ar.AsyncState;

                //if (client.Connected)
                //{
                client.EndConnect(ar);
                //Connected = true;
                ConnStat = ConnStatus.CONNECTED;
                retryCount = 0;
                CallNotification(ModbusNotification.INFO, COMMON_NOTICE, "Connected!");
                keeprun = true;
                ReadSync();
                //}
            }
            catch (ObjectDisposedException objex)
            {
                ConnStat = ConnStatus.DISCONNECTED;
                CallNotification(ModbusNotification.ERRORDC, 404, objex.Message);
            }
            catch (SocketException e)
            {
                if (retryCount < retryTimes)
                {
                    if (e.NativeErrorCode == SOCKET_ERROR_FORCE_CLOSED
                        || e.NativeErrorCode == SOCKET_ERROR_RESPONSE_TIMEOUT
                        || e.NativeErrorCode == SOCKET_UNREACH_NETWORK
                        || e.NativeErrorCode == SOCKET_UNREACH_HOST
                        || e.NativeErrorCode == SOCKET_ERROR_CONN_REFUSED)
                    {
                        retryCount += 1;
                        ConnStat = ConnStatus.SLEEPING;
                        CallNotification(ModbusNotification.ERROR, e.NativeErrorCode, string.Format("Failed to connect: Reconnecting in {0:0} secs... attempt {1}/{2}", refresh, retryCount, retryTimes));
                        lock (padlock)
                        {
                            Monitor.Wait(padlock, TimeSpan.FromSeconds(refresh));
                        }
                        Connect();
                    }
                    else
                    {
                        ConnStat = ConnStatus.DISCONNECTED;
                        CallNotification(ModbusNotification.ERRORDC, e.NativeErrorCode, e.Message);
                    }
                }
                else
                {
                    retryCount = 0;
                    if (persistantConn)
                    {
                        ConnStat = ConnStatus.SLEEPING;
                        CallNotification(ModbusNotification.ERROR, e.NativeErrorCode, string.Format("Failed to connect: Persistant connection mode on, attempt to reconnect in {0:0} mins...", reconnectDelay));
                        lock (padlock)
                        {
                            Monitor.Wait(padlock, TimeSpan.FromMinutes(reconnectDelay));
                        }
                        Connect();
                    }
                    else
                    {
                        ConnStat = ConnStatus.DISCONNECTED;
                        CallNotification(ModbusNotification.ERRORDC, e.NativeErrorCode, e.Message);
                    }
                }
            }
            finally
            {
                //connectDone.Set();
            }
        }

        public void ReadSync()
        {
            try
            {
            loop:
                if (keeprun)
                {
                    //Start read table from database
                    client.Send(CreateModbusCmd());
                    int bytesRec = client.Receive(buffer);

                    byte errorStat = buffer[MODBUS_HEADER_SIZE + 1];
                    //ushort seq = (ushort)(buffer[14] | buffer[14 + 1] << 8);

                    if ((errorStat == 0x81) || (errorStat == 0x82) || (errorStat == 0x83) || (errorStat == 0x84) || (errorStat == 0x85) || (errorStat == 0x86) || (errorStat == 0x8f) || (errorStat == 0x90))
                    {
                        byte errorCode = buffer[MODBUS_HEADER_SIZE + 2];

                        switch (errorCode)
                        {
                            case 0x01: CallNotification(ModbusNotification.ERROR, errorStat, "Modbus Error : Illegal Function"); break;
                            case 0x02: CallNotification(ModbusNotification.ERROR, errorStat, "Modbus Error : Illegal Data Address"); break;
                            case 0x03: CallNotification(ModbusNotification.ERROR, errorStat, "Modbus Error : Illegal Data Value"); break;
                            case 0x04: CallNotification(ModbusNotification.ERROR, errorStat, "Modbus Error : Illegal Slave Device Failure"); break;
                            case 0x05: CallNotification(ModbusNotification.ERROR, errorStat, "Modbus Error : Illegal Ack"); break;
                            case 0x06: CallNotification(ModbusNotification.ERROR, errorStat, "Modbus Error : Illegal Slave Device Busy"); break;
                            case 0x07: CallNotification(ModbusNotification.ERROR, errorStat, "Modbus Error : Illegal Negative Ack"); break;
                            case 0x08: CallNotification(ModbusNotification.ERROR, errorStat, "Modbus Error : Illegal Memory Parity Error"); break;
                            case 0x0a: CallNotification(ModbusNotification.ERROR, errorStat, "Modbus Error : Illegal Gateway Path Unavailable"); break;
                            case 0x0b: CallNotification(ModbusNotification.ERROR, errorStat, "Modbus Error : Illegal Gateway Target Device Failed to Respond"); break;
                        }
                    }
                    else
                    {
                        //ushort dataLen = (ushort)(buffer[MODBUS_HEADER_SIZE] | buffer[MODBUS_HEADER_SIZE] << 8);
                        ushort dataLen = (ushort)(buffer[MODBUS_HEADER_SIZE]);
                        byte[] data = new byte[dataLen];
                        Buffer.BlockCopy(buffer, MODBUS_HEADER_SIZE + 1, data, 0, dataLen);
                        string sdata = String.Join(",", (!BitConverter.IsLittleEndian ? UShortConverter(data) : UShortConverterReverse(data)).Select(p => p.ToString()).ToArray());                        
                        CallResponse(sdata);
                    }

                    lock (padlock)
                    {
                        Monitor.Wait(padlock, TimeSpan.FromSeconds(refresh));
                    }

                    goto loop;
                }
            }
            catch (ArgumentNullException ane)
            {
                ConnStat = ConnStatus.DISCONNECTED;
                CallNotification(ModbusNotification.ERRORDC, COMMON_ERROR, ane.ToString());
            }
            catch (SocketException se)
            {
                if (se.NativeErrorCode == SOCKET_ERROR_FORCE_CLOSED
                    || se.NativeErrorCode == SOCKET_ERROR_RESPONSE_TIMEOUT
                    || se.NativeErrorCode == SOCKET_ERROR_CONN_REFUSED)
                {
                    retryCount += 1;
                    ConnStat = ConnStatus.SLEEPING;
                    CallNotification(ModbusNotification.ERROR, se.NativeErrorCode, string.Format("Failed to connect: Reconnecting in {0:0} secs... attempt {1}/{2}", refresh, retryCount, retryTimes));
                    lock (padlock)
                    {
                        Monitor.Wait(padlock, TimeSpan.FromSeconds(refresh));
                    }
                    Connect();
                }
                else
                {
                    ConnStat = ConnStatus.DISCONNECTED;
                    CallNotification(ModbusNotification.ERRORDC, se.NativeErrorCode, se.Message);
                }
            }
            catch (Exception e)
            {
                ConnStat = ConnStatus.DISCONNECTED;
                CallNotification(ModbusNotification.ERRORDC, COMMON_ERROR, e.ToString());
            }
        }

        public void Disconnect()
        {
            if (client != null)
            {
                if (client.Connected)
                {
                    try
                    {
                        keeprun = false;
                        client.Shutdown(SocketShutdown.Both);
                    }
                    catch { }
                }
                //client.Dispose();
                client.Close();
                client = null;
            }
            //Connected = false;
            CallNotification(ModbusNotification.INFO, COMMON_NOTICE, "Client disconnected!");
            ConnStat = ConnStatus.DISCONNECTED;
        }

        public void Stop()
        {
            keeprun = false;
            lock (padlock)
            {
                Monitor.Pulse(padlock);
            }
        }

        private ushort[] UShortConverter(byte[] buffer)
        {
            ushort[] result = new ushort[buffer.Length / 2];
            Buffer.BlockCopy(buffer, 0, result, 0, buffer.Length);
            return result;
        }

        private ushort[] UShortConverterReverse(byte[] buffer)
        {
            ushort[] result = new ushort[buffer.Length / 2];
            Array.Reverse(buffer, 0, buffer.Length);
            Buffer.BlockCopy(buffer, 0, result, 0, buffer.Length);
            Array.Reverse(result, 0, result.Length);
            return result;
        }
    }


    //Only for Modbus RTU, not used for this program
    public class CRCHelper
    {
        public static void Crc16Modbus(byte[] modbusParam, out byte[] modbusRequest)
        {
            byte[] result = new byte[modbusParam.Length + 2];
            ushort crc16 = 0xffFF;
            ushort temp;
            ushort flag;

            for (int i = 0; i < modbusParam.Length + 2; i++)
            {
                if (i < modbusParam.Length)
                {
                    temp = (ushort)modbusParam[i]; // temp has the first byte 
                    temp &= 0x00ff; // mask the MSB 
                    crc16 = (ushort)(crc16 ^ temp); //crc16 XOR with temp 
                    for (uint c = 0; c < 8; c++)
                    {
                        flag = (ushort)(crc16 & 0x01); // LSBit di crc16 is mantained
                        crc16 = (ushort)(crc16 >> 1); // Lsbit di crc16 is lost 
                        if (flag != 0)
                            crc16 = (ushort)(crc16 ^ 0x0a001); // crc16 XOR with 0x0a001 
                    }
                    result[i] = modbusParam[i];
                }
                else if (i == modbusParam.Length)
                    result[i] = (byte)crc16;
                else if (i == modbusParam.Length + 1)
                    result[i] = (byte)(crc16 >> 8);
            }

            modbusRequest = result;
        }
    }
}
