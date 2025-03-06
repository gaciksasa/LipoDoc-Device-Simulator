using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LipoDocDeviceSimulator
{
    class Program
    {
        // Server connection settings (default values, can be changed through UI)
        private static string ServerIP = "192.168.1.124";
        private static int ServerPort = 5000;

        // Constants for the device protocol
        private const char SEPARATOR = '\u00AA'; // Unicode 170 - this is the special separator character
        private const char LINE_FEED = '\u000A'; // Unicode 10 - Line Feed character
        private const char END_MARKER = '\u00FD'; // Unicode 253 - ý - string end marker

        // List of simulated devices
        private static List<SimulatedDevice> _devices = new List<SimulatedDevice>();

        // Flags for main loop
        private static bool _running = true;
        private static bool _refreshUI = true;

        // Main execution thread
        static async Task Main(string[] args)
        {
            Console.Title = "LipoDoc Device Simulator";
            DisplayWelcomeScreen();

            // Start the main application loop
            while (_running)
            {
                if (_refreshUI)
                {
                    DisplayMainMenu();
                    _refreshUI = false;
                }

                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);
                    await HandleMenuInput(key.KeyChar);
                }

                // Allow some CPU rest
                await Task.Delay(100);
            }

            // Cleanup before exit
            await DisconnectAllDevices();

            Console.WriteLine("Thank you for using the LipoDoc Device Simulator!");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        private static void DisplayWelcomeScreen()
        {
            try
            {
                Console.Clear();
            }
            catch (IOException)
            {
                // Ignore clear screen errors
                Console.WriteLine("\n\n");
            }

            try
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("==========================================================");
                Console.WriteLine("          LIPODOC DEVICE SIMULATOR                        ");
                Console.WriteLine("==========================================================");
                Console.ResetColor();
            }
            catch (IOException)
            {
                // Fallback if console colors aren't available
                Console.WriteLine("==========================================================");
                Console.WriteLine("          LIPODOC DEVICE SIMULATOR                        ");
                Console.WriteLine("==========================================================");
            }

            Console.WriteLine();
            Console.WriteLine("This application simulates multiple LipoDoc devices");
            Console.WriteLine("connecting to your TCP/IP server and sending status and");
            Console.WriteLine("data messages in the proper format.");
            Console.WriteLine();
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);
        }

        private static void DisplayMainMenu()
        {
            try
            {
                Console.Clear();
            }
            catch (IOException)
            {
                // Ignore clear screen errors
                Console.WriteLine("\n\n");
            }

            try
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("==========================================================");
                Console.WriteLine("                  MAIN MENU                               ");
                Console.WriteLine("==========================================================");
                Console.ResetColor();
            }
            catch (IOException)
            {
                // Fallback if console colors aren't available
                Console.WriteLine("==========================================================");
                Console.WriteLine("                  MAIN MENU                               ");
                Console.WriteLine("==========================================================");
            }
            Console.WriteLine();

            // Display server settings
            Console.WriteLine($"Server IP: {ServerIP}");
            Console.WriteLine($"Server Port: {ServerPort}");
            Console.WriteLine();

            // Display devices
            Console.WriteLine("Simulated Devices:");
            Console.WriteLine("----------------------------------------------------------");

            if (_devices.Count == 0)
            {
                Console.WriteLine("  No devices configured. Add a device to begin simulation.");
            }
            else
            {
                int index = 1;
                foreach (var device in _devices)
                {
                    Console.Write($"  {index}. SN: {device.SerialNumber} | ");

                    if (device.IsConnected)
                    {
                        try
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.Write("CONNECTED");
                            Console.ResetColor();
                        }
                        catch (IOException)
                        {
                            // Fallback without colors
                            Console.Write("CONNECTED");
                        }
                    }
                    else
                    {
                        try
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.Write("DISCONNECTED");
                            Console.ResetColor();
                        }
                        catch (IOException)
                        {
                            // Fallback without colors
                            Console.Write("DISCONNECTED");
                        }
                    }

                    Console.Write($" | Last msg: {device.LastStatusTime.ToString("HH:mm:ss")}");

                    // Show the number of stored records
                    if (device.StoredRecordsCount > 0)
                    {
                        try
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.Write($" | {device.StoredRecordsCount} stored records");
                            Console.ResetColor();
                        }
                        catch (IOException)
                        {
                            Console.Write($" | {device.StoredRecordsCount} stored records");
                        }
                    }

                    Console.WriteLine();

                    index++;
                }
            }

            Console.WriteLine("----------------------------------------------------------");
            Console.WriteLine();

            // Display menu options
            Console.WriteLine("Menu Options:");
            Console.WriteLine("  1) Add new device");
            Console.WriteLine("  2) Delete device");
            Console.WriteLine("  3) Connect/Disconnect device");
            Console.WriteLine("  4) Configure server settings");
            Console.WriteLine("  5) Create donation data (offline or online)");
            Console.WriteLine("  6) Send specific data format");
            Console.WriteLine("  7) Connect all devices");
            Console.WriteLine("  8) Disconnect all devices");
            Console.WriteLine("  9) Refresh display");
            Console.WriteLine("  0) Exit");
            Console.WriteLine();
            Console.Write("Select an option: ");
        }

        private static async Task HandleMenuInput(char input)
        {
            switch (input)
            {
                case '1':
                    await AddNewDevice();
                    break;
                case '2':
                    await DeleteDevice();
                    break;
                case '3':
                    await ConnectDisconnectDevice();
                    break;
                case '4':
                    ConfigureServerSettings();
                    break;
                case '5':
                    await CreateDonationData();
                    break;
                case '6':
                    await SendSpecificDataFormat();
                    break;
                case '7':
                    await ConnectAllDevices();
                    break;
                case '8':
                    await DisconnectAllDevices();
                    break;
                case '9':
                    _refreshUI = true;
                    break;
                case '0':
                    _running = false;
                    break;
                default:
                    break;
            }
        }

        private static async Task AddNewDevice()
        {
            try
            {
                Console.Clear();
            }
            catch (IOException)
            {
                // Ignore clear screen errors
                Console.WriteLine("\n\n");
            }

            Console.WriteLine("=== Add New Device ===");
            Console.WriteLine();

            Console.Write("Enter device serial number (e.g., LD0000000): ");
            string serialNumber = Console.ReadLine().Trim();

            if (string.IsNullOrWhiteSpace(serialNumber))
            {
                Console.WriteLine("Serial number cannot be empty. Press any key to return.");
                Console.ReadKey(true);
                _refreshUI = true;
                return;
            }

            if (_devices.Any(d => d.SerialNumber == serialNumber))
            {
                Console.WriteLine("A device with this serial number already exists. Press any key to return.");
                Console.ReadKey(true);
                _refreshUI = true;
                return;
            }

            // Create the device
            var device = new SimulatedDevice(serialNumber, ServerIP, ServerPort);
            _devices.Add(device);

            Console.WriteLine($"Device {serialNumber} has been added.");
            Console.WriteLine("Do you want to connect the device now? (Y/N, default Y): ");

            if (Console.ReadLine().Trim().ToUpper() != "N")
            {
                await device.Connect();
                Console.WriteLine($"Device {serialNumber} has been connected.");
            }

            Console.WriteLine("Press any key to return to the main menu.");
            Console.ReadKey(true);
            _refreshUI = true;
        }

        private static async Task DeleteDevice()
        {
            if (_devices.Count == 0)
            {
                Console.WriteLine("No devices to delete. Press any key to return.");
                Console.ReadKey(true);
                _refreshUI = true;
                return;
            }

            try
            {
                Console.Clear();
            }
            catch (IOException)
            {
                // Ignore clear screen errors
                Console.WriteLine("\n\n");
            }

            Console.WriteLine("=== Delete Device ===");
            Console.WriteLine();

            for (int i = 0; i < _devices.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {_devices[i].SerialNumber}");
            }

            Console.WriteLine();
            Console.Write("Enter the number of the device to delete (or 0 to cancel): ");

            if (!int.TryParse(Console.ReadLine().Trim(), out int selection) || selection < 0 || selection > _devices.Count)
            {
                Console.WriteLine("Invalid selection. Press any key to return.");
                Console.ReadKey(true);
                _refreshUI = true;
                return;
            }

            if (selection == 0)
            {
                _refreshUI = true;
                return;
            }

            var deviceToDelete = _devices[selection - 1];

            // Disconnect the device if it's connected
            if (deviceToDelete.IsConnected)
            {
                await deviceToDelete.Disconnect();
            }

            _devices.RemoveAt(selection - 1);

            Console.WriteLine($"Device {deviceToDelete.SerialNumber} has been deleted.");
            Console.WriteLine("Press any key to return to the main menu.");
            Console.ReadKey(true);
            _refreshUI = true;
        }

        private static async Task ConnectDisconnectDevice()
        {
            if (_devices.Count == 0)
            {
                Console.WriteLine("No devices to connect/disconnect. Press any key to return.");
                Console.ReadKey(true);
                _refreshUI = true;
                return;
            }

            try
            {
                Console.Clear();
            }
            catch (IOException)
            {
                // Ignore clear screen errors
                Console.WriteLine("\n\n");
            }

            Console.WriteLine("=== Connect/Disconnect Device ===");
            Console.WriteLine();

            for (int i = 0; i < _devices.Count; i++)
            {
                var device = _devices[i];
                Console.Write($"{i + 1}. {device.SerialNumber} - ");

                if (device.IsConnected)
                {
                    try
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write("CONNECTED");
                        Console.ResetColor();
                    }
                    catch (IOException)
                    {
                        // Fallback without colors
                        Console.Write("CONNECTED");
                    }
                }
                else
                {
                    try
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write("DISCONNECTED");
                        Console.ResetColor();
                    }
                    catch (IOException)
                    {
                        // Fallback without colors
                        Console.Write("DISCONNECTED");
                    }
                }

                // Show the number of stored records
                if (device.StoredRecordsCount > 0)
                {
                    try
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write($" | {device.StoredRecordsCount} stored records");
                        Console.ResetColor();
                    }
                    catch (IOException)
                    {
                        Console.Write($" | {device.StoredRecordsCount} stored records");
                    }
                }

                Console.WriteLine();
            }

            Console.WriteLine();
            Console.Write("Enter the number of the device to connect/disconnect (or 0 to cancel): ");

            if (!int.TryParse(Console.ReadLine().Trim(), out int selection) || selection < 0 || selection > _devices.Count)
            {
                Console.WriteLine("Invalid selection. Press any key to return.");
                Console.ReadKey(true);
                _refreshUI = true;
                return;
            }

            if (selection == 0)
            {
                _refreshUI = true;
                return;
            }

            var selectedDevice = _devices[selection - 1];

            if (selectedDevice.IsConnected)
            {
                await selectedDevice.Disconnect();
                Console.WriteLine($"Device {selectedDevice.SerialNumber} has been disconnected.");
            }
            else
            {
                await selectedDevice.Connect();
                Console.WriteLine($"Device {selectedDevice.SerialNumber} has been connected.");
            }

            Console.WriteLine("Press any key to return to the main menu.");
            Console.ReadKey(true);
            _refreshUI = true;
        }

        private static void ConfigureServerSettings()
        {
            try
            {
                Console.Clear();
            }
            catch (IOException)
            {
                // Ignore clear screen errors
                Console.WriteLine("\n\n");
            }

            Console.WriteLine("=== Configure Server Settings ===");
            Console.WriteLine();

            Console.WriteLine($"Current Server IP: {ServerIP}");
            Console.Write("Enter new Server IP (or leave empty to keep current): ");
            string newIP = Console.ReadLine().Trim();

            if (!string.IsNullOrWhiteSpace(newIP))
            {
                ServerIP = newIP;

                // Update all devices with the new IP
                foreach (var device in _devices)
                {
                    device.ServerIP = ServerIP;
                }
            }

            Console.WriteLine($"Current Server Port: {ServerPort}");
            Console.Write("Enter new Server Port (or leave empty to keep current): ");
            string newPortStr = Console.ReadLine().Trim();

            if (!string.IsNullOrWhiteSpace(newPortStr) && int.TryParse(newPortStr, out int newPort))
            {
                ServerPort = newPort;

                // Update all devices with the new port
                foreach (var device in _devices)
                {
                    device.ServerPort = ServerPort;
                }
            }

            Console.WriteLine();
            Console.WriteLine($"Server settings updated to {ServerIP}:{ServerPort}");
            Console.WriteLine("Press any key to return to the main menu.");
            Console.ReadKey(true);
            _refreshUI = true;
        }

        private static async Task CreateDonationData()
        {
            if (_devices.Count == 0)
            {
                Console.WriteLine("No devices available. Press any key to return.");
                Console.ReadKey(true);
                _refreshUI = true;
                return;
            }

            try
            {
                Console.Clear();
            }
            catch (IOException)
            {
                // Ignore clear screen errors
                Console.WriteLine("\n\n");
            }

            Console.WriteLine("=== Create Donation Data ===");
            Console.WriteLine();

            for (int i = 0; i < _devices.Count; i++)
            {
                var device = _devices[i];
                Console.Write($"{i + 1}. {device.SerialNumber} - ");

                if (device.IsConnected)
                {
                    try
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("CONNECTED");
                        Console.ResetColor();
                    }
                    catch (IOException)
                    {
                        // Fallback without colors
                        Console.WriteLine("CONNECTED");
                    }
                }
                else
                {
                    try
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("DISCONNECTED");
                        Console.ResetColor();
                    }
                    catch (IOException)
                    {
                        // Fallback without colors
                        Console.WriteLine("DISCONNECTED");
                    }
                }
            }

            Console.WriteLine();
            Console.Write("Enter the number of the device to create donation data (or 0 to cancel): ");

            if (!int.TryParse(Console.ReadLine().Trim(), out int selection) || selection < 0 || selection > _devices.Count)
            {
                Console.WriteLine("Invalid selection. Press any key to return.");
                Console.ReadKey(true);
                _refreshUI = true;
                return;
            }

            if (selection == 0)
            {
                _refreshUI = true;
                return;
            }

            var selectedDevice = _devices[selection - 1];

            Console.WriteLine();
            Console.Write("How many donation records to create? (1-50, default 1): ");

            if (!int.TryParse(Console.ReadLine().Trim(), out int count) || count < 1)
            {
                count = 1;
            }

            if (count > 50) count = 50;

            Console.WriteLine();
            Console.WriteLine("Select data format to use:");
            Console.WriteLine("1. All Barcodes (REF, DonationID, OperatorID, LOT)");
            Console.WriteLine("2. No Barcodes");
            Console.WriteLine("3. Only Required Barcodes (REF, DonationID)");
            Console.WriteLine("4. Required + OperatorID Barcodes");
            Console.WriteLine("5. Required + LOT Barcodes");
            Console.WriteLine("6. Random (mix of all formats)");
            Console.WriteLine();
            Console.Write("Select format (1-6, default 6): ");

            if (!int.TryParse(Console.ReadLine().Trim(), out int formatType) || formatType < 1 || formatType > 6)
            {
                formatType = 6; // Default to random
            }

            Console.WriteLine();
            Console.Write("Do you want to send immediately if device is connected? (Y/N, default Y): ");
            bool sendImmediately = Console.ReadLine().Trim().ToUpper() != "N";

            // Create and optionally send the data
            for (int i = 0; i < count; i++)
            {
                // If random format is selected, choose a random format for each record
                int actualFormat = formatType;
                if (formatType == 6)
                {
                    actualFormat = new Random().Next(1, 6);
                }

                bool success = await selectedDevice.CreateDonationData(actualFormat, sendImmediately);

                if (success)
                {
                    try
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        if (selectedDevice.IsConnected && sendImmediately)
                        {
                            Console.WriteLine($"Donation record {i + 1}/{count} (Format {actualFormat}) created and sent.");
                        }
                        else
                        {
                            Console.WriteLine($"Donation record {i + 1}/{count} (Format {actualFormat}) created and stored.");
                        }
                        Console.ResetColor();
                    }
                    catch (IOException)
                    {
                        // Fallback without colors
                        if (selectedDevice.IsConnected && sendImmediately)
                        {
                            Console.WriteLine($"Donation record {i + 1}/{count} (Format {actualFormat}) created and sent.");
                        }
                        else
                        {
                            Console.WriteLine($"Donation record {i + 1}/{count} (Format {actualFormat}) created and stored.");
                        }
                    }
                }
                else
                {
                    try
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Failed to create donation record {i + 1}/{count}.");
                        Console.ResetColor();
                    }
                    catch (IOException)
                    {
                        // Fallback without colors
                        Console.WriteLine($"Failed to create donation record {i + 1}/{count}.");
                    }
                }

                // Brief delay between records
                if (i < count - 1)
                {
                    await Task.Delay(200);
                }
            }

            Console.WriteLine();
            Console.WriteLine("Press any key to return to the main menu.");
            Console.ReadKey(true);
            _refreshUI = true;
        }

        private static async Task SendSpecificDataFormat()
        {
            if (_devices.Count == 0)
            {
                Console.WriteLine("No devices available. Press any key to return.");
                Console.ReadKey(true);
                _refreshUI = true;
                return;
            }

            try
            {
                Console.Clear();
            }
            catch (IOException)
            {
                // Ignore clear screen errors
                Console.WriteLine("\n\n");
            }

            Console.WriteLine("=== Send Specific Data Format ===");
            Console.WriteLine();

            // Display device selection
            for (int i = 0; i < _devices.Count; i++)
            {
                var device = _devices[i];
                Console.Write($"{i + 1}. {device.SerialNumber} - ");

                if (device.IsConnected)
                {
                    try
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("CONNECTED");
                        Console.ResetColor();
                    }
                    catch (IOException)
                    {
                        // Fallback without colors
                        Console.WriteLine("CONNECTED");
                    }
                }
                else
                {
                    try
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("DISCONNECTED");
                        Console.ResetColor();
                    }
                    catch (IOException)
                    {
                        // Fallback without colors
                        Console.WriteLine("DISCONNECTED");
                    }
                }
            }

            Console.WriteLine();
            Console.Write("Enter the number of the device to send data (or 0 to cancel): ");

            if (!int.TryParse(Console.ReadLine().Trim(), out int deviceSelection) || deviceSelection < 0 || deviceSelection > _devices.Count)
            {
                Console.WriteLine("Invalid selection. Press any key to return.");
                Console.ReadKey(true);
                _refreshUI = true;
                return;
            }

            if (deviceSelection == 0)
            {
                _refreshUI = true;
                return;
            }

            var selectedDevice = _devices[deviceSelection - 1];

            // Check if device is connected
            if (!selectedDevice.IsConnected)
            {
                Console.WriteLine("Device is disconnected. Do you want to connect it first? (Y/N, default Y): ");
                if (Console.ReadLine().Trim().ToUpper() != "N")
                {
                    await selectedDevice.Connect();
                    Console.WriteLine($"Device {selectedDevice.SerialNumber} has been connected.");
                }
                else
                {
                    Console.WriteLine("Cannot send data when device is disconnected. Press any key to return.");
                    Console.ReadKey(true);
                    _refreshUI = true;
                    return;
                }
            }

            // Display format selection
            Console.WriteLine();
            Console.WriteLine("Available Data Formats:");
            Console.WriteLine("1. All Barcodes (REF, DonationID, OperatorID, LOT)");
            Console.WriteLine("2. No Barcodes");
            Console.WriteLine("3. Only Required Barcodes (REF, DonationID)");
            Console.WriteLine("4. Required + OperatorID Barcodes");
            Console.WriteLine("5. Required + LOT Barcodes");
            Console.WriteLine();
            Console.Write("Select format (1-5): ");

            if (!int.TryParse(Console.ReadLine().Trim(), out int formatType) || formatType < 1 || formatType > 5)
            {
                Console.WriteLine("Invalid format. Press any key to return.");
                Console.ReadKey(true);
                _refreshUI = true;
                return;
            }

            // Send the data with the selected format
            bool success = await selectedDevice.SendDonationData(formatType);

            if (success)
            {
                try
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"Donation data with format {formatType} sent successfully.");
                    Console.ResetColor();
                }
                catch (IOException)
                {
                    // Fallback without colors
                    Console.WriteLine($"Donation data with format {formatType} sent successfully.");
                }
            }
            else
            {
                try
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Failed to send donation data.");
                    Console.ResetColor();
                }
                catch (IOException)
                {
                    // Fallback without colors
                    Console.WriteLine("Failed to send donation data.");
                }
            }

            Console.WriteLine();
            Console.WriteLine("Press any key to return to the main menu.");
            Console.ReadKey(true);
            _refreshUI = true;
        }

        private static async Task ConnectAllDevices()
        {
            if (_devices.Count == 0)
            {
                Console.WriteLine("No devices to connect. Press any key to return.");
                Console.ReadKey(true);
                _refreshUI = true;
                return;
            }

            Console.WriteLine("Connecting all devices...");

            foreach (var device in _devices)
            {
                if (!device.IsConnected)
                {
                    await device.Connect();
                    Console.WriteLine($"Device {device.SerialNumber} connected.");
                }
            }

            Console.WriteLine("All devices connected. Press any key to continue.");
            Console.ReadKey(true);
            _refreshUI = true;
        }

        private static async Task DisconnectAllDevices()
        {
            if (_devices.Count == 0)
            {
                return;
            }

            Console.WriteLine("Disconnecting all devices...");

            foreach (var device in _devices)
            {
                if (device.IsConnected)
                {
                    await device.Disconnect();
                    Console.WriteLine($"Device {device.SerialNumber} disconnected.");
                }
            }

            if (_running) // Only display the message if we're not exiting
            {
                Console.WriteLine("All devices disconnected. Press any key to continue.");
                Console.ReadKey(true);
                _refreshUI = true;
            }
        }
    }

    /// <summary>
    /// Represents a donation data record stored by a device
    /// </summary>
    class DonationRecord
    {
        public int FormatType { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }

        public DonationRecord(int formatType, string message, DateTime timestamp)
        {
            FormatType = formatType;
            Message = message;
            Timestamp = timestamp;
        }
    }

    /// <summary>
    /// Represents a simulated LipoDoc device that connects to the server
    /// </summary>
    class SimulatedDevice
    {
        private const char SEPARATOR = '\u00AA'; // Unicode 170 - this is the special separator character
        private const char LINE_FEED = '\u000A'; // Unicode 10 - Line Feed character
        private const char END_MARKER = '\u00FD'; // Unicode 253 - ý - string end marker

        // Device properties
        public string SerialNumber { get; }
        public string ServerIP { get; set; }
        public int ServerPort { get; set; }
        public bool IsConnected { get; private set; }
        public DateTime LastStatusTime { get; private set; } = DateTime.MinValue;

        // List of stored donation records when device is offline
        private List<DonationRecord> _storedRecords = new List<DonationRecord>();
        public int StoredRecordsCount => _storedRecords.Count;

        // Private device state
        private CancellationTokenSource _cts;
        private Task _statusTask;
        private int _availableData = 0;
        private int _deviceStatus = 0; // 0=IDLE, 1=Process in progress, 2=Process completed
        private Random _random = new Random();

        // Constructor
        public SimulatedDevice(string serialNumber, string serverIP, int serverPort)
        {
            SerialNumber = serialNumber;
            ServerIP = serverIP;
            ServerPort = serverPort;
        }

        /// <summary>
        /// Connects the device and starts sending status messages and any stored data
        /// </summary>
        public async Task Connect()
        {
            if (IsConnected)
                return;

            _cts = new CancellationTokenSource();
            IsConnected = true;

            // Start the status sending task
            _statusTask = Task.Run(async () =>
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        // Send a status message
                        await SendStatusMessage();

                        // Check if there are stored records to send
                        if (_storedRecords.Count > 0 && IsConnected)
                        {
                            // Send oldest stored record first
                            var record = _storedRecords[0];

                            // Try to send it
                            bool sent = await SendStoredRecord(record);

                            if (sent)
                            {
                                // Remove from stored records
                                _storedRecords.RemoveAt(0);

                                // Log successful send
                                try
                                {
                                    Console.ForegroundColor = ConsoleColor.Green;
                                    Console.WriteLine($"Device {SerialNumber} sent stored record (Format {record.FormatType}) from {record.Timestamp.ToString("HH:mm:ss")}.");
                                    Console.ResetColor();
                                }
                                catch (IOException)
                                {
                                    // Fallback without colors
                                    Console.WriteLine($"Device {SerialNumber} sent stored record (Format {record.FormatType}) from {record.Timestamp.ToString("HH:mm:ss")}.");
                                }

                                // Brief delay to avoid flooding server
                                await Task.Delay(500, _cts.Token);
                            }
                        }

                        // Update available data count for status messages
                        _availableData = _storedRecords.Count;

                        // Wait random time between 3-4 seconds
                        int delay = _random.Next(3000, 4000);
                        await Task.Delay(delay, _cts.Token);
                    }
                    catch (TaskCanceledException)
                    {
                        // Expected when cancelling
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"Error in device {SerialNumber}: {ex.Message}");
                            Console.ResetColor();
                        }
                        catch (IOException)
                        {
                            // Fallback without colors
                            Console.WriteLine($"Error in device {SerialNumber}: {ex.Message}");
                        }

                        // Brief delay before retry
                        try
                        {
                            await Task.Delay(5000, _cts.Token);
                        }
                        catch
                        {
                            // Expected when cancelling
                        }
                    }
                }
            });
        }

        /// <summary>
        /// Disconnects the device and stops communication
        /// </summary>
        public async Task Disconnect()
        {
            if (!IsConnected)
                return;

            // Cancel ongoing operations
            _cts?.Cancel();

            // Wait for status task to complete
            if (_statusTask != null)
            {
                try
                {
                    await _statusTask;
                }
                catch
                {
                    // Ignore task cancellation exceptions
                }
            }

            _cts?.Dispose();
            _cts = null;

            IsConnected = false;
        }

        /// <summary>
        /// Sends a status message to the server
        /// </summary>
        private async Task SendStatusMessage()
        {
            try
            {
                using (var client = new TcpClient())
                {
                    await client.ConnectAsync(ServerIP, ServerPort);

                    using (var stream = client.GetStream())
                    {
                        // Format: #SªSNªStatusªvreme.timevreme.dateªAvailableDataªCSý

                        // Update device state
                        _deviceStatus = _random.Next(0, 3); // 0=IDLE, 1=Process in progress, 2=Process completed

                        // Create timestamp in required format
                        var now = DateTime.Now;
                        string timestamp = $"{now.Hour:D2}:{now.Minute:D2}:{now.Second:D2}{now.Day:D2}:{now.Month:D2}:{now.Year}";

                        // Create checksum (just a random value for simulation)
                        string checksum = $"{_random.Next(1000, 9999)}";

                        // Build the message
                        string message = $"#S{SEPARATOR}{SerialNumber}{SEPARATOR}{_deviceStatus}{SEPARATOR}{timestamp}{SEPARATOR}{_availableData}{SEPARATOR}{checksum}{END_MARKER}{LINE_FEED}";

                        // Send the message
                        byte[] data = Encoding.ASCII.GetBytes(message);
                        await stream.WriteAsync(data, 0, data.Length);

                        // Update last status time
                        LastStatusTime = DateTime.Now;
                    }
                }
            }
            catch (Exception ex)
            {
                try
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Error sending status for device {SerialNumber}: {ex.Message}");
                    Console.ResetColor();
                }
                catch (IOException)
                {
                    // Fallback without colors
                    Console.WriteLine($"Error sending status for device {SerialNumber}: {ex.Message}");
                }
                throw;
            }
        }

        /// <summary>
        /// Creates a donation data record and optionally sends it to the server
        /// </summary>
        /// <param name="formatType">Format type (1-5) corresponding to different message formats</param>
        /// <param name="sendImmediately">Whether to send immediately if device is connected</param>
        public async Task<bool> CreateDonationData(int formatType = 0, bool sendImmediately = true)
        {
            try
            {
                // If formatType wasn't specified, randomly select one
                if (formatType < 1 || formatType > 5)
                {
                    formatType = _random.Next(1, 6);
                }

                // Create timestamp in required format
                var now = DateTime.Now;
                string timestamp = $"{now.Hour:D2}:{now.Minute:D2}:{now.Second:D2}{now.Day:D2}:{now.Month:D2}:{now.Year}";

                // Generate random values for the donation
                string refCode = $"4KA{_random.Next(1000000, 9999999):D7}";
                string donationId = $"0406059980{_random.Next(1000, 9999):D4}";
                string operatorId = $"{_random.Next(1000, 9999):D4}";
                string lotNumber = $"00{_random.Next(100000, 999999):D6}KA";

                // Generate lipemic test results
                int lipemicValue = _random.Next(100, 3500);
                string lipemicGroup;
                string lipemicStatus;

                if (lipemicValue < 250)
                {
                    lipemicGroup = "I";
                    lipemicStatus = "PASSED";
                }
                else if (lipemicValue < 550)
                {
                    lipemicGroup = "II";
                    lipemicStatus = (lipemicValue < 400) ? "PASSED" : "LIPEMIC";
                }
                else if (lipemicValue < 2500)
                {
                    lipemicGroup = "III";
                    lipemicStatus = "LIPEMIC";
                }
                else
                {
                    lipemicGroup = "IV";
                    lipemicStatus = "LIPEMIC";
                }

                // Generate checksum (random for simulation - use hexadecimal format)
                string checksum = _random.Next(1, 255).ToString("X2");

                // Build the message based on the format type
                string message;

                switch (formatType)
                {
                    case 1: // All barcodes
                        // #DªSNªvreme.timevreme.dateªBªREF_codeªDonationID_barcodeªOperatorID_barcodeªLOT_numberªMªiFinallDispªLipGroupªIsLipemicªENDEª"CS"ýLF
                        message = $"#D{SEPARATOR}{SerialNumber}{SEPARATOR}{timestamp}{SEPARATOR}B{SEPARATOR}" +
                                  $"{refCode}{SEPARATOR}{donationId}{SEPARATOR}{operatorId}{SEPARATOR}{lotNumber}{SEPARATOR}" +
                                  $"M{SEPARATOR}{lipemicValue}{SEPARATOR}{lipemicGroup}{SEPARATOR}{lipemicStatus}{SEPARATOR}ENDE{SEPARATOR}{checksum}{END_MARKER}{LINE_FEED}";
                        break;

                    case 2: // No barcodes
                        // #DªSNªvreme.timevreme.dateªBªªMªiFinallDispªLipGroupªIsLipemicªENDEª"CS"ýLF
                        message = $"#D{SEPARATOR}{SerialNumber}{SEPARATOR}{timestamp}{SEPARATOR}B{SEPARATOR}{SEPARATOR}" +
                                  $"M{SEPARATOR}{lipemicValue}{SEPARATOR}{lipemicGroup}{SEPARATOR}{lipemicStatus}{SEPARATOR}ENDE{SEPARATOR}{checksum}{END_MARKER}{LINE_FEED}";
                        break;

                    case 3: // Only required barcodes (REF, DonationID)
                        // #DªSNªvreme.timevreme.dateªBªREF_codeªDonationID_barcodeªªªMªiFinallDispªLipGroupªIsLipemicªENDEª"CS"ýLF
                        message = $"#D{SEPARATOR}{SerialNumber}{SEPARATOR}{timestamp}{SEPARATOR}B{SEPARATOR}" +
                                  $"{refCode}{SEPARATOR}{donationId}{SEPARATOR}{SEPARATOR}{SEPARATOR}" +
                                  $"M{SEPARATOR}{lipemicValue}{SEPARATOR}{lipemicGroup}{SEPARATOR}{lipemicStatus}{SEPARATOR}ENDE{SEPARATOR}{checksum}{END_MARKER}{LINE_FEED}";
                        break;

                    case 4: // Required + OperatorID
                        // #DªSNªvreme.timevreme.dateªBªREF_codeªDonationID_barcodeªOperatorID_barcodeªªMªiFinallDispªLipGroupªIsLipemicªENDEª"CS"ýLF
                        message = $"#D{SEPARATOR}{SerialNumber}{SEPARATOR}{timestamp}{SEPARATOR}B{SEPARATOR}" +
                                  $"{refCode}{SEPARATOR}{donationId}{SEPARATOR}{operatorId}{SEPARATOR}{SEPARATOR}" +
                                  $"M{SEPARATOR}{lipemicValue}{SEPARATOR}{lipemicGroup}{SEPARATOR}{lipemicStatus}{SEPARATOR}ENDE{SEPARATOR}{checksum}{END_MARKER}{LINE_FEED}";
                        break;

                    case 5: // Required + LOT
                        // #DªSNªvreme.timevreme.dateªBªREF_codeªDonationID_barcodeªªLOT_numberªMªiFinallDispªLipGroupªIsLipemicªENDEª"CS"ýLF
                        message = $"#D{SEPARATOR}{SerialNumber}{SEPARATOR}{timestamp}{SEPARATOR}B{SEPARATOR}" +
                                  $"{refCode}{SEPARATOR}{donationId}{SEPARATOR}{SEPARATOR}{lotNumber}{SEPARATOR}" +
                                  $"M{SEPARATOR}{lipemicValue}{SEPARATOR}{lipemicGroup}{SEPARATOR}{lipemicStatus}{SEPARATOR}ENDE{SEPARATOR}{checksum}{END_MARKER}{LINE_FEED}";
                        break;

                    default:
                        message = $"#D{SEPARATOR}{SerialNumber}{SEPARATOR}{timestamp}{SEPARATOR}B{SEPARATOR}" +
                                  $"{refCode}{SEPARATOR}{donationId}{SEPARATOR}{operatorId}{SEPARATOR}{lotNumber}{SEPARATOR}" +
                                  $"M{SEPARATOR}{lipemicValue}{SEPARATOR}{lipemicGroup}{SEPARATOR}{lipemicStatus}{SEPARATOR}ENDE{SEPARATOR}{checksum}{END_MARKER}{LINE_FEED}";
                        break;
                }

                // Create a donation record
                DonationRecord record = new DonationRecord(formatType, message, now);

                // Check if device is connected and should send immediately
                if (IsConnected && sendImmediately)
                {
                    // Send the data immediately
                    bool sent = await SendStoredRecord(record);

                    if (!sent)
                    {
                        // If sending failed, store the record
                        _storedRecords.Add(record);
                        _availableData = _storedRecords.Count;
                    }
                }
                else
                {
                    // Store the record for later transmission
                    _storedRecords.Add(record);
                    _availableData = _storedRecords.Count;
                }

                return true;
            }
            catch (Exception ex)
            {
                try
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Error creating donation data for device {SerialNumber}: {ex.Message}");
                    Console.ResetColor();
                }
                catch (IOException)
                {
                    // Fallback without colors
                    Console.WriteLine($"Error creating donation data for device {SerialNumber}: {ex.Message}");
                }
                return false;
            }
        }

        /// <summary>
        /// Sends a stored donation record to the server
        /// </summary>
        private async Task<bool> SendStoredRecord(DonationRecord record)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    await client.ConnectAsync(ServerIP, ServerPort);

                    using (var stream = client.GetStream())
                    {
                        // Log the message being sent
                        Console.WriteLine($"Sending: {record.Message.Replace(SEPARATOR, 'ª').Replace(END_MARKER, 'ý')}");

                        // Send the message
                        byte[] data = Encoding.ASCII.GetBytes(record.Message);
                        await stream.WriteAsync(data, 0, data.Length);

                        // Wait for acknowledgment
                        byte[] responseBuffer = new byte[1024];
                        stream.ReadTimeout = 5000; // 5 second timeout

                        int bytesRead = await stream.ReadAsync(responseBuffer, 0, responseBuffer.Length);
                        string response = Encoding.ASCII.GetString(responseBuffer, 0, bytesRead);

                        if (!response.StartsWith("#A"))
                        {
                            try
                            {
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine($"Warning: Unexpected response from server: {response}");
                                Console.ResetColor();
                            }
                            catch (IOException)
                            {
                                // Fallback without colors
                                Console.WriteLine($"Warning: Unexpected response from server: {response}");
                            }
                        }

                        // Update last status time since we've communicated
                        LastStatusTime = DateTime.Now;

                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                try
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Error sending record for device {SerialNumber}: {ex.Message}");
                    Console.ResetColor();
                }
                catch (IOException)
                {
                    // Fallback without colors
                    Console.WriteLine($"Error sending record for device {SerialNumber}: {ex.Message}");
                }
                return false;
            }
        }

        /// <summary>
        /// Sends a donation data message to the server
        /// </summary>
        /// <param name="formatType">Format type (1-5) corresponding to different message formats</param>
        public async Task<bool> SendDonationData(int formatType = 0)
        {
            if (!IsConnected)
            {
                Console.WriteLine("Device is not connected. Cannot send data.");
                return false;
            }

            // Create and send a donation data record
            return await CreateDonationData(formatType, true);
        }
    }
}