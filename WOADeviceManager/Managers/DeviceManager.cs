using AndroidDebugBridge;
using FastBoot;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnifiedFlashingPlatform;
using Windows.Devices.Enumeration;
using WOADeviceManager.Managers.Connectivity;

namespace WOADeviceManager.Managers
{
    public class DeviceManager
    {
        private const string VAYU_MassStorage_LinuxGadget_USBID = "USBSTOR#Disk&Ven_Linux&Prod_File-Stor_Gadget&Rev_0414#";
        private const string WINDOWS_USBID = "VID_045E&PID_0C2A&MI_00";
        private const string UFP_USBID = "USB#VID_045E&PID_066B#";
        private const string FASTBOOT_USBID = "USB#VID_18D1&PID_D00D#";
        private const string ANDROID_USBID = "USB#VID_2717&PID_FF40";

        private const string VAYU_TWRP_USBID1 = "USB#VID_2717&PID_D001";
        private const string VAYU_TWRP_USBID2 = "USB#VID_18D1&PID_D001";

        private const string VAYU_PLATFORMID = "Xiaomi.SDM855.POCO X3 Pro.vayu";

        private const string ADB_USB_INTERFACEGUID = "{dee824ef-729b-4a0e-9c14-b7117d33a817}";

        private const string VAYU_FRIENDLY_NAME = "POCO X3 Pro";

        private readonly DeviceWatcher watcher;

        private static DeviceManager _instance;
        public static DeviceManager Instance
        {
            get
            {
                _instance ??= new DeviceManager();
                return _instance;
            }
        }

        private static Device device;
        public static Device Device
        {
            get
            {
                device ??= new Device();
                return device;
            }
            private set => device = value;
        }

        public delegate void DeviceFoundEventHandler(object sender, Device device);
        public delegate void DeviceConnectedEventHandler(object sender, Device device);
        public delegate void DeviceDisconnectedEventHandler(object sender, Device device);
        public static event DeviceFoundEventHandler DeviceFoundEvent;
        public static event DeviceConnectedEventHandler DeviceConnectedEvent;
        public static event DeviceDisconnectedEventHandler DeviceDisconnectedEvent;

        private DeviceManager()
        {
            device ??= new Device();

            watcher = DeviceInformation.CreateWatcher();
            watcher.Added += DeviceAdded;
            watcher.Removed += DeviceRemoved;
            watcher.Updated += Watcher_Updated;
            watcher.Start();
        }

        private async void Watcher_Updated(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            _ = args.Properties.TryGetValue("System.Devices.InterfaceEnabled", out object? IsInterfaceEnabledObjectValue);
            bool IsInterfaceEnabled = (bool?)IsInterfaceEnabledObjectValue ?? false;

            // Disconnection
            if (!IsInterfaceEnabled)
            {
                if (args.Id == Device.ID)
                {
                    NotifyDeviceDeparture();

                    if (Device.AndroidDebugBridgeTransport != null)
                    {
                        Device.AndroidDebugBridgeTransport.OnConnectionEstablished -= AndroidDebugBridgeTransport_OnConnectionEstablished;
                        Device.AndroidDebugBridgeTransport.Dispose();
                        Device.AndroidDebugBridgeTransport = null;
                    }

                    if (Device.FastBootTransport != null)
                    {
                        Device.FastBootTransport.Dispose();
                        Device.FastBootTransport = null;
                    }

                    if (Device.UnifiedFlashingPlatformTransport != null)
                    {
                        Device.UnifiedFlashingPlatformTransport.Dispose();
                        Device.UnifiedFlashingPlatformTransport = null;
                    }

                    Device.State = DeviceState.DISCONNECTED;
                    Device.ID = null;
                    Device.Name = null;
                    Device.Variant = null;
                    // TODO: Device.Product = Device.Product;
                }
                else if (args.Id == Device.MassStorageID)
                {
                    NotifyDeviceDeparture();

                    switch (Device.State)
                    {
                        case DeviceState.TWRP_MASS_STORAGE_ADB_ENABLED:
                            {
                                Device.State = DeviceState.TWRP_ADB_ENABLED;
                                break;
                            }
                    }

                    Device.MassStorageID = null;
                    Device.MassStorage.Dispose();
                    Device.MassStorage = null;

                    NotifyDeviceArrival();
                }

                return;
            }

            DeviceInformation deviceInformation = null;
            DeviceInformationCollection allDevices = await DeviceInformation.FindAllAsync();
            foreach (DeviceInformation _deviceInformation in allDevices)
            {
                if (_deviceInformation.Id == args.Id)
                {
                    deviceInformation = _deviceInformation;
                }
            }

            string ID = args.Id;
            string Name = deviceInformation != null ? deviceInformation.Name : "N/A";

            HandleDevice(ID, Name);
        }

        private void DeviceAdded(DeviceWatcher sender, DeviceInformation args)
        {
            bool IsInterfaceEnabled = args.IsEnabled;
            if (!IsInterfaceEnabled)
            {
                return;
            }

            string ID = args.Id;
            string Name = args.Name;

            HandleDevice(ID, Name);
        }

        private void NotifyDeviceArrival()
        {
            Device.JustDisconnected = false;

            Debug.WriteLine("New Device Found!");
            Debug.WriteLine($"Device path: {Device.ID}");
            Debug.WriteLine($"Name: {Device.Name}");
            Debug.WriteLine($"Variant: {Device.Variant}");
            Debug.WriteLine($"Product: {Device.Product}");
            Debug.WriteLine($"State: {Device.DeviceStateLocalized}");

            DeviceConnectedEvent?.Invoke(this, device);
        }

        private void NotifyDeviceDeparture()
        {
            Device.JustDisconnected = true;

            Debug.WriteLine("Device Disconnected!");
            Debug.WriteLine($"Device path: {Device.ID}");
            Debug.WriteLine($"Name: {Device.Name}");
            Debug.WriteLine($"Variant: {Device.Variant}");
            Debug.WriteLine($"Product: {Device.Product}");
            Debug.WriteLine($"State: {Device.DeviceStateLocalized}");

            DeviceDisconnectedEvent?.Invoke(this, device);
        }

        private void HandleDevice(string ID, string Name)
        {
            // Adb Enabled Android example:
            //
            // Args.ID: \\?\USB#VID_045E&PID_0C26#0F0012E214600A#{dee824ef-729b-4a0e-9c14-b7117d33a817}
            //
            // SurfaceDuoUsb.inf list:
            //
            // %Fastboot%              = Fastboot_Install, USB\VID_045E&PID_0C2F
            // %Adb%                   = Adb_Install, USB\VID_045E&PID_0C26
            // %AdbSideload%           = AdbSideload_Install, USB\VID_045E&PID_0C30
            // %AdbComposite%          = AdbComposite_Install, USB\VID_045E&PID_0C26&MI_01
            // %AdbCompositeTether%    = AdbCompositeTether_Install, USB\VID_045E&PID_0C28&MI_02
            // %AdbCompositeFT%        = AdbCompositeFT_Install, USB\VID_045E&PID_0C2A&MI_01
            // %AdbCompositePTP%       = AdbCompositePTP_Install, USB\VID_045E&PID_0C2C&MI_01
            // %AdbCompositeMIDI%      = AdbCompositeMIDI_Install, USB\VID_045E&PID_0C2E&MI_02
            //
            // Fastboot           = "POCO X3 Pro Fastboot"
            // Adb                = "POCO X3 Pro ADB"
            // AdbSideload        = "POCO X3 Pro ADB Sideload"
            // AdbComposite       = "POCO X3 Pro Composite ADB"
            // AdbCompositeTether = "POCO X3 Pro Composite ADB Tether"
            // AdbCompositeFT     = "POCO X3 Pro Composite ADB File Transfer"
            // AdbCompositePTP    = "POCO X3 Pro Composite ADB PTP"
            // AdbCompositeMIDI   = "POCO X3 Pro Composite ADB MIDI"

            if (ID.Contains(VAYU_MassStorage_LinuxGadget_USBID))
            {
                if (Device.State != DeviceState.DISCONNECTED)
                {
                    NotifyDeviceDeparture();
                }

                if (Device.State == DeviceState.TWRP_ADB_ENABLED)
                {
                    Device.State = DeviceState.TWRP_MASS_STORAGE_ADB_ENABLED;
                }
                else
                {
                    Device.State = DeviceState.OFFLINE_CHARGING;
                }

                // No ID, to be filled later
                Device.MassStorageID = ID;
                Device.Product = DeviceProduct.Vayu;
                Device.Name = VAYU_FRIENDLY_NAME;
                Device.Variant = "N/A";
                Device.MassStorage = new Helpers.MassStorage(ID);

                NotifyDeviceArrival();
                return;
            }
            else if (ID.Contains(WINDOWS_USBID))
            {
                if (Device.State != DeviceState.DISCONNECTED)
                {
                    NotifyDeviceDeparture();
                }

                Device.State = DeviceState.WINDOWS;
                Device.ID = ID;
                Device.Name = Name;
                Device.Variant = "N/A";
                Device.Product = DeviceProduct.Vayu;

                NotifyDeviceArrival();
            }
            else if (ID.Contains(UFP_USBID))
            {
                try
                {
                    UnifiedFlashingPlatformTransport unifiedFlashingPlatformTransport;
                    if (ID == Device.ID && Device.UnifiedFlashingPlatformTransport != null)
                    {
                        unifiedFlashingPlatformTransport = Device.UnifiedFlashingPlatformTransport;
                    }
                    else
                    {
                        unifiedFlashingPlatformTransport = new(ID);
                    }

                    // Xiaomi.Surface.POCO X3 Pro.vayu
                    string PlatformID = unifiedFlashingPlatformTransport.ReadDevicePlatformID();

                    switch (PlatformID)
                    {
                        case VAYU_PLATFORMID:
                            {
                                if (Device.State != DeviceState.DISCONNECTED)
                                {
                                    NotifyDeviceDeparture();
                                }

                                Device.State = DeviceState.UFP;
                                Device.ID = ID;
                                Device.Name = VAYU_FRIENDLY_NAME;
                                Device.Variant = "N/A";
                                Device.Product = DeviceProduct.Vayu;

                                if (Device.UnifiedFlashingPlatformTransport != null && Device.UnifiedFlashingPlatformTransport != unifiedFlashingPlatformTransport)
                                {
                                    Device.UnifiedFlashingPlatformTransport.Dispose();
                                    Device.UnifiedFlashingPlatformTransport = unifiedFlashingPlatformTransport;
                                }
                                else if (Device.UnifiedFlashingPlatformTransport == null)
                                {
                                    Device.UnifiedFlashingPlatformTransport = unifiedFlashingPlatformTransport;
                                }

                                NotifyDeviceArrival();
                                return;
                            }
                    }

                    unifiedFlashingPlatformTransport.Dispose();
                } catch { }
            }
            // Normal:
            // POCO X3 Pro Fastboot
            else if (ID.Contains(FASTBOOT_USBID))
            {
                try
                {
                    FastBootTransport fastBootTransport = new(ID);

                    bool result = fastBootTransport.GetVariable("product", out string productGetVar);
                    string ProductName = !result ? null : productGetVar;
                    result = fastBootTransport.GetVariable("is-userspace", out productGetVar);
                    string IsUserSpace = !result ? null : productGetVar;

                    // Attempt to retrieve the device type in bootloader mode
                    string DeviceVariant = "N/A";
                    if (IsUserSpace != "yes")
                    {
                        (FastBootStatus getBootPropResponseStatus, string getBootPropResponse, byte[] _)[] getBootPropResponses = fastBootTransport.SendCommand("oem get-boot-prop");
                        if (getBootPropResponses.Length > 0 && getBootPropResponses.Last().getBootPropResponseStatus == FastBootStatus.OKAY)
                        {
                            (FastBootStatus _, DeviceVariant, byte[] _) = getBootPropResponses[0];
                            DeviceVariant = DeviceVariant.Split('=').Last();

                            switch (DeviceVariant)
                            {
                                case "gen":
                                    {
                                        DeviceVariant = "GEN";
                                        break;
                                    }
                                case "att":
                                    {
                                        DeviceVariant = "ATT";
                                        break;
                                    }
                                case "eea":
                                    {
                                        DeviceVariant = "EEA";
                                        break;
                                    }
                            }
                        }
                    }

                    switch (ProductName)
                    {
                        case "vayu":
                            {
                                if (Device.State != DeviceState.DISCONNECTED)
                                {
                                    NotifyDeviceDeparture();
                                }

                                if (IsUserSpace == "yes")
                                {
                                    Device.State = DeviceState.FASTBOOTD;
                                }
                                else
                                {
                                    Device.State = DeviceState.BOOTLOADER;
                                }

                                Device.ID = ID;
                                Device.Name = VAYU_FRIENDLY_NAME;
                                Device.Variant = DeviceVariant;
                                Device.Product = DeviceProduct.Vayu;

                                if (Device.FastBootTransport != null && Device.FastBootTransport != fastBootTransport)
                                {
                                    Device.FastBootTransport.Dispose();
                                    Device.FastBootTransport = fastBootTransport;
                                }
                                else if (Device.FastBootTransport == null)
                                {
                                    Device.FastBootTransport = fastBootTransport;
                                }

                                NotifyDeviceArrival();
                                return;
                            }
                    }

                    fastBootTransport.Dispose();
                }
                catch { }
            }
            // Normal:
            // POCO X3 Pro ADB
            // POCO X3 Pro ADB Sideload
            // POCO X3 Pro Composite ADB
            // POCO X3 Pro Composite ADB Tether
            // POCO X3 Pro Composite ADB File Transfer
            // POCO X3 Pro Composite ADB PTP
            // POCO X3 Pro Composite ADB MIDI
            //
            // Custom:
            // POCO X3 Pro TWRP
            else if ((ID.Contains("USB#VID_18D1&PID_4EE7#") ||
             ID.Contains("USB#VID_18D1&PID_4E11#") ||
             ID.Contains("USB#VID_18D1&PID_FF48&MI_01#") ||
             ID.Contains("USB#VID_2717&PID_FF88&MI_02#") ||
             ID.Contains("USB#VID_2717&PID_FF48&MI_01#") ||
             ID.Contains("USB#VID_2717&PID_FF18&MI_01#") ||
             ID.Contains("USB#VID_18D1&PID_0C2E&MI_02#") ||
             ID.Contains(VAYU_TWRP_USBID1) ||
             ID.Contains(VAYU_TWRP_USBID2)) && ID.Contains(ADB_USB_INTERFACEGUID))
            {
                Thread.Sleep(1000);
                try
                {
                    AndroidDebugBridgeTransport androidDebugBridgeTransport;
                    if (ID == Device.ID && Device.AndroidDebugBridgeTransport != null)
                    {
                        androidDebugBridgeTransport = Device.AndroidDebugBridgeTransport;
                    }
                    else
                    {
                        androidDebugBridgeTransport = new(ID);
                    }

                    if (androidDebugBridgeTransport.IsConnected)
                    {
                        HandleADBEnabledDevice(androidDebugBridgeTransport);
                    }
                    else
                    {
                        HandleADBDisabledDevice(androidDebugBridgeTransport);

                        // Request a connection
                        androidDebugBridgeTransport.OnConnectionEstablished += AndroidDebugBridgeTransport_OnConnectionEstablished;
                        androidDebugBridgeTransport.Connect();
                    }
                }
                catch { }
            }
            else if (ID.Contains(ANDROID_USBID))
            {
                if (Device.State != DeviceState.DISCONNECTED)
                {
                    NotifyDeviceDeparture();
                }

                Device.State = DeviceState.ANDROID;
                Device.ID = ID;
                Device.Name = Name;
                Device.Variant = "N/A";
                Device.Product = DeviceProduct.Vayu;

                NotifyDeviceArrival();
            }
        }

        private void HandleADBEnabledDevice(AndroidDebugBridgeTransport androidDebugBridgeTransport)
        {
            if (!androidDebugBridgeTransport.IsConnected)
            {
                return;
            }

            string ID = androidDebugBridgeTransport.DevicePath;

            if (ID.Contains(VAYU_TWRP_USBID1) || ID.Contains(VAYU_TWRP_USBID2))
            {
                if (Device.MassStorageID != null)
                {
                    Device.State = DeviceState.TWRP_MASS_STORAGE_ADB_ENABLED;
                }
                else
                {
                    Device.State = DeviceState.TWRP_ADB_ENABLED;
                }

                Device.ID = ID;
                Device.Name = VAYU_FRIENDLY_NAME;

                string DeviceVariant = Device.AndroidDebugBridgeTransport.GetVariableValue("ro.boot.hwc");
                switch (DeviceVariant)
                {
                    case "INDIA":
                        {
                            DeviceVariant = "bhima";
                            break;
                        }
                    default:
                        {
                            DeviceVariant = "vayu";
                            break;
                        }
                }

                Device.Variant = DeviceVariant;

                Device.Product = DeviceProduct.Vayu;

                if (Device.AndroidDebugBridgeTransport != null && Device.AndroidDebugBridgeTransport != androidDebugBridgeTransport)
                {
                    Device.AndroidDebugBridgeTransport.Dispose();
                    Device.AndroidDebugBridgeTransport = androidDebugBridgeTransport;
                }
                else if (Device.AndroidDebugBridgeTransport == null)
                {
                    Device.AndroidDebugBridgeTransport = androidDebugBridgeTransport;
                }

                NotifyDeviceArrival();
                return;
            }
            else
            {
                string ProductDevice = "vayu";
                if (androidDebugBridgeTransport.GetPhoneConnectionVariables().ContainsKey("ro.product.device"))
                {
                    ProductDevice = androidDebugBridgeTransport.GetPhoneConnectionVariables()["ro.product.device"];
                }

                switch (ProductDevice)
                {
                    case "vayu":
                        {
                            if (androidDebugBridgeTransport.GetPhoneConnectionEnvironment() == "recovery")
                            {
                                Device.State = DeviceState.RECOVERY_ADB_ENABLED;
                            }
                            else if (androidDebugBridgeTransport.GetPhoneConnectionEnvironment() == "sideload")
                            {
                                Device.State = DeviceState.SIDELOAD_ADB_ENABLED;
                            }
                            else if (androidDebugBridgeTransport.GetPhoneConnectionEnvironment() == "device")
                            {
                                Device.State = DeviceState.ANDROID_ADB_ENABLED;
                            }
                            else
                            {
                                Device.State = DeviceState.ANDROID_ADB_ENABLED;
                            }
                            Device.ID = ID;
                            Device.Name = VAYU_FRIENDLY_NAME;

                            string ProductName = "N/A";
                            if (androidDebugBridgeTransport.GetPhoneConnectionVariables().ContainsKey("ro.product.name"))
                            {
                                ProductName = androidDebugBridgeTransport.GetPhoneConnectionVariables()["ro.product.name"];
                            }

                            switch (ProductName)
                            {
                                case "vayu_eea":
                                    {
                                        Device.Variant = "vayu_eea";
                                        break;
                                    }
                                case "vayu_global":
                                    {
                                        Device.Variant = "vayu_global";
                                        break;
                                    }
                                default:
                                    {
                                        Device.Variant = ProductName;
                                        break;
                                    }
                            }

                            Device.Product = DeviceProduct.Vayu;

                            if (Device.AndroidDebugBridgeTransport != null && Device.AndroidDebugBridgeTransport != androidDebugBridgeTransport)
                            {
                                Device.AndroidDebugBridgeTransport.Dispose();
                                Device.AndroidDebugBridgeTransport = androidDebugBridgeTransport;
                            }
                            else if (Device.AndroidDebugBridgeTransport == null)
                            {
                                Device.AndroidDebugBridgeTransport = androidDebugBridgeTransport;
                            }

                            NotifyDeviceArrival();
                            return;
                        }
                }
            }
        }

        private void HandleADBDisabledDevice(AndroidDebugBridgeTransport androidDebugBridgeTransport)
        {
            if (androidDebugBridgeTransport.IsConnected)
            {
                return;
            }

            string ID = androidDebugBridgeTransport.DevicePath;

            if (ID.Contains(VAYU_TWRP_USBID1) || ID.Contains(VAYU_TWRP_USBID2))
            {
                if (Device.State != DeviceState.DISCONNECTED)
                {
                    NotifyDeviceDeparture();
                }

                if (Device.MassStorageID != null)
                {
                    Device.State = DeviceState.TWRP_MASS_STORAGE_ADB_DISABLED;
                }
                else
                {
                    Device.State = DeviceState.TWRP_ADB_DISABLED;
                }

                Device.ID = ID;
                Device.Name = VAYU_FRIENDLY_NAME;
                Device.Variant = "N/A";
                Device.Product = DeviceProduct.Vayu;

                if (Device.AndroidDebugBridgeTransport != null && Device.AndroidDebugBridgeTransport != androidDebugBridgeTransport)
                {
                    Device.AndroidDebugBridgeTransport.Dispose();
                    Device.AndroidDebugBridgeTransport = androidDebugBridgeTransport;
                }
                else if (Device.AndroidDebugBridgeTransport == null)
                {
                    Device.AndroidDebugBridgeTransport = androidDebugBridgeTransport;
                }

                NotifyDeviceArrival();
                return;
            }
            else
            {
                string ProductDevice = "vayu";

                switch (ProductDevice)
                {
                    case "vayu":
                        {
                            if (Device.State != DeviceState.DISCONNECTED)
                            {
                                NotifyDeviceDeparture();
                            }

                            Device.State = DeviceState.ANDROID_ADB_DISABLED;
                            Device.ID = ID;
                            Device.Name = VAYU_FRIENDLY_NAME;
                            Device.Variant = "N/A";
                            Device.Product = DeviceProduct.Vayu;

                            if (Device.AndroidDebugBridgeTransport != null && Device.AndroidDebugBridgeTransport != androidDebugBridgeTransport)
                            {
                                Device.AndroidDebugBridgeTransport.Dispose();
                                Device.AndroidDebugBridgeTransport = androidDebugBridgeTransport;
                            }
                            else if (Device.AndroidDebugBridgeTransport == null)
                            {
                                Device.AndroidDebugBridgeTransport = androidDebugBridgeTransport;
                            }

                            NotifyDeviceArrival();
                            return;
                        }
                }
            }
        }

        private void AndroidDebugBridgeTransport_OnConnectionEstablished(object sender, EventArgs e)
        {
            AndroidDebugBridgeTransport androidDebugBridgeTransport = (AndroidDebugBridgeTransport)sender;
            androidDebugBridgeTransport.OnConnectionEstablished -= AndroidDebugBridgeTransport_OnConnectionEstablished;
            HandleADBEnabledDevice(androidDebugBridgeTransport);
        }

        private void DeviceRemoved(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            if (Device.ID != args.Id)
            {
                return;
            }

            NotifyDeviceDeparture();

            Device.State = DeviceState.DISCONNECTED;
            Device.ID = null;
            Device.Name = null;
            Device.Variant = null;
            // TODO: Device.Product = Device.Product;

            if (Device.FastBootTransport != null)
            {
                Device.FastBootTransport.Dispose();
                Device.FastBootTransport = null;
            }

            if (Device.AndroidDebugBridgeTransport != null)
            {
                Device.AndroidDebugBridgeTransport.Dispose();
                Device.AndroidDebugBridgeTransport = null;
            }

            if (Device.UnifiedFlashingPlatformTransport != null)
            {
                Device.UnifiedFlashingPlatformTransport.Dispose();
                Device.UnifiedFlashingPlatformTransport = null;
            }
        }
    }
}
