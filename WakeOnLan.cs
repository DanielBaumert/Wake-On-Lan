using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Se7en.MagicWakeOnLan
{
    internal class WakeOnLan : IDisposable
    {
        private delegate TOut Func<T1, T2, TOut>(T1 t1, out T2 t2);
        private string _dirPath;
        private string _filePath;

        private List<WakeOnLanDevice> _devices;

        public WakeOnLan()
        {
            _dirPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Se7en", "WakeOnLan");
            _filePath = Path.Combine(_dirPath, "config");
        }
         
        public void Run() { 
            Console.Clear();
            if (!Directory.Exists(_dirPath))
            {
                Directory.CreateDirectory(_dirPath);
            }

            if (!File.Exists(_filePath))
            {
                File.Create(_filePath).Close();
                _devices = new List<WakeOnLanDevice>();
            }
            else
            {
                _devices = File.ReadAllLines(_filePath)
                               .Select(line =>
                               {
                                   string[] lineParts = line.Split("\t");
                                   return new WakeOnLanDevice
                                   {
                                       Name = lineParts[0],
                                       Mac = PhysicalAddress.Parse(lineParts[1]),
                                       Port = int.Parse(lineParts[2])
                                   };
                               }).ToList();
            }

            do
            {
                Console.WriteLine("[1] Add new device");
                Console.WriteLine("[2] Edit device");
                Console.WriteLine("[3] Remove device");
                Console.WriteLine("[4] Wake up a device");
                Console.WriteLine("[0] Exit");
                Console.Write("Input: ");


                string eingabe = Console.ReadLine();
                switch (eingabe)
                {
                    case "1":
                        Add();
                        break;
                    case "2":
                        Edit();
                        break;
                    case "3":
                        Remove();
                        break;
                    case "4":
                        WakeUp();
                        break;
                    case "0":
                        return;
                    default:
                        Console.Clear();
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Invalite value!");
                        Console.ResetColor();
                        break;
                }
            } while (true);
        }


        private void Add()
        {
            Console.Clear();
            WriteError("Type -1 to exit!");
            ReadInputT("Please type in a device nickname: ", out string name, ReadInputName);
            if (name == "-1")
            {
                return;
            }
            ReadInputT("Please type in the mac-address: ", out PhysicalAddress mac, ReadInputMac);
            if (mac == default)
            {
                return;
            }
            ReadInputT("Please type in the port(default: 9): ", out int port, ReadInputPort);
            if (port == -1)
            {
                return;
            }
            Console.WriteLine();

            if (QuestionForYes($"Would you like to add the Device(Mac: {mac} Port:{port})?"))
            {
                // Get all lines from config file
                // check if any line contains the mac
                if (DeviceExists(mac, out WakeOnLanDevice device))
                {   // mac exists

                    // rename the device nickname?
                    Console.ForegroundColor = ConsoleColor.Red;
                    if (!QuestionForNo("Adding failed, woud you like to rename?"))
                    {
                        // change device name
                        device.Name = name;
                        // write back to file
                        File.WriteAllLines(_filePath, _devices.Select(device => device.ToString()));
                        Console.Clear();
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("Device successfully renamed");
                    }
                }
                else
                {   // mac doesn't exists

                    // add device to programm storage
                    _devices.Add(new WakeOnLanDevice
                    {
                        Name = name,
                        Mac = mac,
                        Port = port
                    });
                    // write new device to file
                    File.WriteAllLines(_filePath, _devices.Select(device => device.ToString()));
                    Console.Clear();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Device successfully added");
                }
            }
            Console.ResetColor();

        }
        private void Edit()
        {
            Console.Clear();
            while (true)
            {
                PrintDevices();
                Console.WriteLine($"[-1]: All");
                Console.WriteLine($"[0]: Exit");
                Console.WriteLine();
                Console.WriteLine("Single selection: [number]");
                Console.WriteLine("Multi selection: [number], [number], ... [no comma on the end] ");
                Console.Write("Please choose the number from the device would you like to edit:");
                string input = Console.ReadLine();


                if (input.Contains(',')) // is muti selection
                {
                    if (!CheckMultiSelection(input, out int[] numbers, out string errorMsg))
                    {
                        WriteError(errorMsg);
                        continue;
                    }

                    foreach (int number in numbers)
                    {
                        _devices[number - 1] = EditMenu(_devices[number - 1]);
                    }
                }
                else
                {
                    if (int.TryParse(input, out int value))
                    {
                        switch (value)
                        {

                            case 0:
                                Console.Clear();
                                return;
                            case -1:
                                {
                                    if (!QuestionForNo("Would you really want to delete all devices?"))
                                    {
                                        _devices.Clear();
                                        File.WriteAllText(_filePath, "");
                                    }
                                }
                                break;
                            default:
                                {
                                    if (value > 0 && value <= _devices.Count)
                                    {
                                        _devices[value - 1] = EditMenu(_devices[value - 1]);
                                    }
                                    else
                                    {
                                        Console.Clear();
                                        WriteError("Value is out of range and an invalid input!");
                                    }
                                }
                                break;
                        }
                    }
                }
            }
        }


        private WakeOnLanDevice EditMenu(WakeOnLanDevice device)
        {
            while (true)
            {
                Console.WriteLine($"Selected Device: {device.Name}(Mac: {device.MacAddress} Port:{device.Port})");
                Console.WriteLine("[1] Change nickname");
                Console.WriteLine("[2] Change Mac");
                Console.WriteLine("[3] Change port");
                Console.WriteLine("[0] Exit");
                Console.Write("Input: ");

                string input = Console.ReadLine();
                switch (input)
                {
                    case "1":
                        {
                            WriteError("Type -1 as nickname to exit!");
                            ReadInputT("Please type in a device nickname: ", out string name, ReadInputName);
                            if (name == "-1")
                            {
                                break;
                            }
                            device.Name = name;
                        }
                        break;
                    case "2":
                        {
                            WriteError("Type -1 as mac-address to exit!");
                            ReadInputT("Please type in a device mac-address: ", out PhysicalAddress mac, ReadInputMac);
                            if (mac != default)
                            {
                                device.Mac = mac;
                            }
                        }
                        break;
                    case "3":
                        {
                            WriteError("Type -1 as port to exit!");
                            ReadInputT("Please type in a device port: ", out int port, ReadInputPort);
                            if (port != -1)
                            {
                                device.Port = port;
                            }
                        }
                        break;
                    default:
                        WriteError("Invalite Input");
                        break;
                }
            }
        }

        private void Remove()
        {
            while (true)
            {
                Console.Clear();
                PrintDevices();
                Console.WriteLine($"[-1]: All");
                Console.WriteLine($"[0]: Exit");
                Console.WriteLine();
                Console.WriteLine("Single selection: [number]");
                Console.WriteLine("Multi selection: [number], [number], ... [no comma on the end] ");
                Console.Write("Please choose the number from the device would you like to remove:");
                string input = Console.ReadLine();


                if (input.Contains(',')) // is muti selection
                {
                    if (!CheckMultiSelection(input, out int[] numbers, out string errorMsg))
                    {
                        WriteError(errorMsg);
                        continue;
                    }

                    Console.Clear();
                    foreach (int number in numbers)
                    {
                        Console.WriteLine(_devices[number - 1].ToString());
                    }

                    if (QuestionForNo("Would you really want to delete all does devices?"))
                    {
                        foreach (int number in numbers)
                        {
                            _devices.RemoveAt(number - 1);
                        }

                        File.WriteAllLines(_filePath, _devices.Select(device => device.ToString()));
                        Console.Clear();
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("All device successfully removed");
                    }
                }
                else //single selection
                {
                    if (int.TryParse(input, out int value))
                    {
                        switch (value)
                        {

                            case 0:
                                Console.Clear();
                                return;
                            case -1:
                                {
                                    if (!QuestionForNo("Would you really want to delete all devices?"))
                                    {
                                        _devices.Clear();
                                        File.WriteAllText(_filePath, "");
                                    }
                                }
                                break;
                            default:
                                {
                                    if (value > 0 && value <= _devices.Count)
                                    {
                                        _devices.RemoveAt(value - 1);

                                        File.WriteAllLines(_filePath, _devices.Select(device => device.ToString()));
                                        Console.Clear();
                                        Console.ForegroundColor = ConsoleColor.Green;
                                        Console.WriteLine("Device successfully removed!");
                                    }
                                    else
                                    {
                                        Console.Clear();
                                        WriteError("Value is out of range and an invalid input!");
                                    }
                                }
                                break;
                        }
                    }
                }
            }
        }

        private void WakeUp()
        {
            Console.Clear();
            while (true)
            {
                PrintDevices();
                Console.WriteLine($"[-1]: All");
                Console.WriteLine($"[0]: Exit");
                Console.WriteLine();
                Console.WriteLine("Single selection: [number]");
                Console.WriteLine("Multi selection: [number], [number], ... [no comma on the end] ");
                Console.Write("Please choose the number from the device would you like to wake up:");
                string input = Console.ReadLine();

                switch (input)
                {
                    case "0":
                        return;
                    case "-1":
                        {
                            foreach (WakeOnLanDevice device in _devices)
                            {
                                device.WakeUp();
                            }
                        }
                        break;
                    default:

                        if (input.Contains(',')) // is muti selection
                        {
                            if (!CheckMultiSelection(input, out int[] numbers, out string errorMsg))
                            {
                                WriteError(errorMsg);
                                break;
                            }

                            foreach (int iDevice in numbers)
                            {
                                _devices[iDevice - 1].WakeUp();
                            }
                        }
                        else
                        {
                            if (int.TryParse(input, out int value))
                            {
                                switch (value)
                                {
                                    case 0:
                                        Console.Clear();
                                        return;
                                    case -1:
                                        {
                                            if (!QuestionForNo("Would you really want to wake up all devices?"))
                                            {
                                                foreach (WakeOnLanDevice device in _devices)
                                                {
                                                    device.WakeUp();
                                                }
                                            }
                                        }
                                        break;
                                    default:
                                        {
                                            if (value > 0 && value <= _devices.Count)
                                            {
                                                _devices[value - 1].WakeUp();
                                            }
                                            else
                                            {
                                                Console.Clear();
                                                WriteError("Value is out of range and an invalid input!");
                                            }
                                        }
                                        break;
                                }
                            }
                        }
                        break;
                }
            }
        }

        private void PrintDevices()
        {
            for (int iDevice = 0, nDevice = _devices.Count; iDevice < nDevice; iDevice++)
            {
                WakeOnLanDevice device = _devices[iDevice];
                Console.WriteLine($"[{iDevice + 1}]: {device.Name}({device.MacAddress}):{device.Port}");
            }
        }


        private void ReadInputT<T>(string message, out T result, Func<string, T, bool> func)
        {
            while (true)
            {
                Console.Write(message);
                string input = Console.ReadLine().Trim(' ');
                if (input == "-1")
                {
                    result = default;
                    return;
                }
                if (func(input, out result))
                {
                    return;
                }
            }
        }
        private bool ReadInputName(string input, out string name)
        {
            if (!input.Contains("\t"))
            {
                name = input;
                return true;
            }
            WriteError("Please choose a name without tab");
            name = null;
            return false;
        }
        private bool ReadInputMac(string input, out PhysicalAddress mac)
        {
            try
            {
                mac = PhysicalAddress.Parse(input);
                return true;
            }
            catch
            {
                mac = PhysicalAddress.None;
                WriteError("Invalite mac address!");
            }
            return false;
        }
        private bool ReadInputPort(string input, out int port)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                port = 9;
                return true;
            }
            if (int.TryParse(input, out port) && ((port > 0 && port <= 65535) || port == -1))
            {
                return true;
            }
            WriteError("Invalite port number, please choose a number between 0 and 65535");
            return false;
        }

        private bool CheckMultiSelection(string input, out int[] numbers, out string errorMessage)
        {

            if (!GetMultiSelectNumbers(input, out numbers))
            {
                errorMessage = "One or more values are an invalid input";
                return false;
            }

            // check for exit code
            if (numbers.Contains(0))
            {
                errorMessage = "0 is not allowed in multi selection";
                return false;
            }

            int devices = _devices.Count;
            // check range 
            if (!numbers.All(number => number > 0 && number <= devices))
            {
                errorMessage = "One or more values are out of range and an invalid input";
                return false;
            }

            errorMessage = null;
            return true;
        }
        private bool GetMultiSelectNumbers(string input, out int[] numbers)
        {
            while (input.Contains(" "))
            {
                input = input.Replace(" ", "");
            }

            // split input off into single elements
            string[] elements = input.Split(',');
            int nElements = elements.Length;

            numbers = new int[nElements];

            for (int iElement = 0; iElement < nElements; iElement++)
            {
                // convert all string numbers to int numbers
                if (!int.TryParse(elements[iElement], out int number))
                {
                    return false;
                }
                numbers[iElement] = number;
            }
            return true;
        }
        private void WriteError(string errorMsg)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(errorMsg);
            Console.ResetColor();
        }
        private bool QuestionForNo(string question)
        {
            while (true)
            {
                string input = Question(question, false);
                if (CheckForDefaultNo(input))
                {
                    return true;
                }
                if (CheckForYes(input))
                {
                    return false;
                }
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Invalite input!");
                Console.ResetColor();
            }
        }
        private bool QuestionForYes(string question)
        {
            while (true)
            {
                string input = Question(question, true);
                if (CheckForDefaultYes(input))
                {
                    return true;
                }
                if (CheckForNo(input))
                {
                    return false;
                }
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Invalite input!");
                Console.ResetColor();
            }
        }
        private string Question(string question, bool match)
        {
            Console.Write($"{question}{(match ? "[Y, n]" : "[y, N]")}:");
            return Console.ReadLine();
        }
        private bool CheckForDefaultNo(string input)
        {
            return string.IsNullOrWhiteSpace(input) || CheckForNo(input);
        }
        private bool CheckForNo(string input)
        {
            return input == "N" || input == "n";
        }
        private bool CheckForDefaultYes(string input)
        {
            return string.IsNullOrWhiteSpace(input) || CheckForYes(input);
        }
        private bool CheckForYes(string input)
        {
            return input == "Y" || input == "y";
        }
        private bool DeviceExists(PhysicalAddress mac, out WakeOnLanDevice device)
        {
            for (int iDevice = 0, nDevice = _devices.Count; iDevice < nDevice; iDevice++)
            {
                device = _devices[iDevice];
                if (device.Mac.Equals(mac))
                {
                    return true;
                }
            }

            device = WakeOnLanDevice.Default;
            return false;
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        private struct WakeOnLanDevice
        {
            private static readonly byte[] MAGIC_HEADER = { 0xff, 0xff, 0xff, 0xff, 0xff, 0xff };
            public static WakeOnLanDevice Default = new WakeOnLanDevice { };

            public string Name;
            public PhysicalAddress Mac;
            public string MacAddress => string.Join("-", Mac.GetAddressBytes().Select(@byte => @byte.ToString("X2")));
            public int Port;

            public WakeOnLanDevice(string name, PhysicalAddress mac, int port = 9)
            {
                Name = name;
                Mac = mac;
                Port = port;
            }

            public void WakeUp()
            {
                try
                {
                    Socket socket = new Socket(AddressFamily.Unknown, SocketType.Raw, ProtocolType.IP) { EnableBroadcast = true };
                    socket.Connect(IPAddress.Broadcast, 9);

                    socket.Send(MAGIC_HEADER);
                    for (int i = 0; i < 16; i++)
                    {
                        socket.Send(Mac.GetAddressBytes());
                    }
                }
                catch
                {

                }
            }
            public override string ToString()
            {
                return $"{Name}\t{MacAddress}\t{Port}";
            }
        }
    }
}
