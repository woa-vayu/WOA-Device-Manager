﻿using AndroidDebugBridge;
using FastBoot;
using SharpCompress.Common;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using UnifiedFlashingPlatform;
using Windows.Devices.Enumeration;
using Windows.Devices.Usb;
using WOADeviceManager.Managers.Connectivity;

namespace WOADeviceManager.Managers
{
    public class DeviceManager
    {
        private const string VAYU_MassStorage_LinuxGadget_USBID = "USBSTOR#Disk&Ven_Linux&Prod_File-Stor_Gadget&Rev_0414#";
        private const string UNKNOWN_MassStorage_LinuxGadget_USBID = "USBSTOR#Disk&Ven_Linux&Prod_File-Stor_Gadget&Rev_0504#";

        private const string WINDOWS_USBID = "VID_045E&PID_0C2A&MI_00";
        
        private const string FASTBOOT_USBID = "USB#VID_18D1&PID_D00D#";
        private const string ANDROID_USBID = "USB#VID_2717&PID_FF40";

        private const string VAYU_TWRP_USBID1 = "USB#VID_2717&PID_D001";
        private const string VAYU_TWRP_USBID2 = "USB#VID_2717&PID_FF68";

        public const string VAYU_PLATFORMID = "Xiaomi.SDM855.POCO X3 Pro.vayu";

        private const string ADB_USB_INTERFACEGUID = "{dee824ef-729b-4a0e-9c14-b7117d33a817}";

        private const string VAYU_FRIENDLY_NAME = "POCO X3 Pro";
		private const string UNKNOWN_FRIENDLY_NAME = "Unknown Device";

        private readonly Guid ANDROID_DEVICE_INTERFACE_GUID = new("F72FE0D4-CBCB-407d-8814-9ED673D0DD6B");
        private readonly Guid UFP_DEVICE_INTERFACE_GUID = new("9E3BD5F7-9690-4FCC-8810-3E2650CD6ECC");

        private readonly DeviceWatcher androidDevicesWatcher;
        private readonly DeviceWatcher ufpDevicesWatcher;
        private readonly DeviceWatcher otherDevicesWatcher;

        private readonly object watcherLock = new();

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

        public static event DeviceFoundEventHandler? DeviceFoundEvent;
        public static event DeviceConnectedEventHandler? DeviceConnectedEvent;
        public static event DeviceDisconnectedEventHandler? DeviceDisconnectedEvent;

        private DeviceManager()
        {
            device ??= new Device();

            string DeviceSelectorAndroidDevices = UsbDevice.GetDeviceSelector(ANDROID_DEVICE_INTERFACE_GUID);

            androidDevicesWatcher = DeviceInformation.CreateWatcher(DeviceSelectorAndroidDevices);
            androidDevicesWatcher.Added += AndroidDeviceAdded;
            androidDevicesWatcher.Removed += AndroidDeviceRemoved;
            androidDevicesWatcher.Updated += AndroidDeviceUpdated;
            androidDevicesWatcher.Start();

            string DeviceSelectorUFPDevices = UsbDevice.GetDeviceSelector(UFP_DEVICE_INTERFACE_GUID);

            ufpDevicesWatcher = DeviceInformation.CreateWatcher(DeviceSelectorUFPDevices);
            ufpDevicesWatcher.Added += UFPDeviceAdded;
            ufpDevicesWatcher.Removed += UFPDeviceRemoved;
            ufpDevicesWatcher.Updated += UFPDeviceUpdated;
            ufpDevicesWatcher.Start();

            otherDevicesWatcher = DeviceInformation.CreateWatcher();
            otherDevicesWatcher.Added += OtherDeviceAdded;
            otherDevicesWatcher.Removed += OtherDeviceRemoved;
            otherDevicesWatcher.Updated += OtherDeviceUpdated;
            otherDevicesWatcher.Start();
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

        private async void AndroidDeviceUpdated(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            _ = args.Properties.TryGetValue("System.Devices.InterfaceEnabled", out object? IsInterfaceEnabledObjectValue);
            bool IsInterfaceEnabled = (bool?)IsInterfaceEnabledObjectValue ?? false;

            // Disconnection
            if (!IsInterfaceEnabled)
            {
                lock (watcherLock)
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

                        Device.State = DeviceState.DISCONNECTED;
                        Device.ID = null;
                        Device.Name = null;
                        Device.Variant = null;
                        // TODO: Device.Product = Device.Product;
                    }
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

            lock (watcherLock)
            {
                HandleAndroidDevice(ID, Name);
            }
        }

        private void AndroidDeviceAdded(DeviceWatcher sender, DeviceInformation args)
        {
            bool IsInterfaceEnabled = args.IsEnabled;
            if (!IsInterfaceEnabled)
            {
                return;
            }

            string ID = args.Id;
            string Name = args.Name;

            lock (watcherLock)
            {
                HandleAndroidDevice(ID, Name);
            }
        }

        private void AndroidDeviceRemoved(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            lock (watcherLock)
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
            }
        }

        private async void UFPDeviceUpdated(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            _ = args.Properties.TryGetValue("System.Devices.InterfaceEnabled", out object? IsInterfaceEnabledObjectValue);
            bool IsInterfaceEnabled = (bool?)IsInterfaceEnabledObjectValue ?? false;

            // Disconnection
            if (!IsInterfaceEnabled)
            {
                lock (watcherLock)
                {
                    if (args.Id == Device.ID)
                    {
                        NotifyDeviceDeparture();

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

            lock (watcherLock)
            {
                HandleUFPDevice(ID, Name);
            }
        }

        private void UFPDeviceAdded(DeviceWatcher sender, DeviceInformation args)
        {
            bool IsInterfaceEnabled = args.IsEnabled;
            if (!IsInterfaceEnabled)
            {
                return;
            }

            string ID = args.Id;
            string Name = args.Name;

            lock (watcherLock)
            {
                HandleUFPDevice(ID, Name);
            }
        }

        private void UFPDeviceRemoved(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            lock (watcherLock)
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

                if (Device.UnifiedFlashingPlatformTransport != null)
                {
                    Device.UnifiedFlashingPlatformTransport.Dispose();
                    Device.UnifiedFlashingPlatformTransport = null;
                }
            }
        }

        private async void OtherDeviceUpdated(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            _ = args.Properties.TryGetValue("System.Devices.InterfaceEnabled", out object? IsInterfaceEnabledObjectValue);
            bool IsInterfaceEnabled = (bool?)IsInterfaceEnabledObjectValue ?? false;

            // Disconnection
            if (!IsInterfaceEnabled)
            {
                lock (watcherLock)
                {
                    if (args.Id == Device.ID)
                    {
                        NotifyDeviceDeparture();

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

            lock (watcherLock)
            {
                HandleOtherDevice(ID, Name);
            }
        }

        private void OtherDeviceAdded(DeviceWatcher sender, DeviceInformation args)
        {
            bool IsInterfaceEnabled = args.IsEnabled;
            if (!IsInterfaceEnabled)
            {
                return;
            }

            string ID = args.Id;
            string Name = args.Name;

            lock (watcherLock)
            {
                HandleOtherDevice(ID, Name);
            }
        }

        private void OtherDeviceRemoved(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            lock (watcherLock)
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
            }
        }

        private void HandleUFPDevice(string ID, string Name)
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

                // Xiaomi.SDM855.POCO X3 Pro.vayu
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
                    default:
                        {
                            if (Device.State != DeviceState.DISCONNECTED)
                            {
                                NotifyDeviceDeparture();
                            }

                            Device.State = DeviceState.UFP;
                            Device.ID = ID;
                            Device.Name = UNKNOWN_FRIENDLY_NAME;
                            Device.Variant = UNKNOWN_FRIENDLY_NAME;
                            Device.Product = DeviceProduct.Unknown;

                            if (unifiedFlashingPlatformTransport.ReadDeviceTargetInfo() is DeviceTargetingInfo deviceTargetingInfo)
                            {
                                Device.Name = deviceTargetingInfo.ProductName;
                            }

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
            }
            catch { }
        }

        private void HandleOtherDevice(string ID, string Name)
        {
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
            else if (ID.Contains(UNKNOWN_MassStorage_LinuxGadget_USBID))
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
                Device.Product = DeviceProduct.Unknown;
                Device.Name = UNKNOWN_FRIENDLY_NAME;
                Device.Variant = UNKNOWN_FRIENDLY_NAME;
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

                DeviceProduct deviceProduct = Name.Contains("POCO X3 Pro") ? DeviceProduct.Vayu : DeviceProduct.Unknown;

                Device.State = DeviceState.WINDOWS;
                Device.ID = ID;
                Device.Name = Name;
                Device.Variant = deviceProduct != DeviceProduct.Unknown ? "N/A" : UNKNOWN_FRIENDLY_NAME;
                Device.Product = deviceProduct;

                NotifyDeviceArrival();
            }
            else if (ID.Contains(ANDROID_USBID))
            {
                if (Device.State != DeviceState.DISCONNECTED)
                {
                    NotifyDeviceDeparture();
                }

                DeviceProduct deviceProduct = Name.Contains("POCO X3 Pro") ? DeviceProduct.Vayu : DeviceProduct.Unknown;

                Device.State = DeviceState.ANDROID;
                Device.ID = ID;
                Device.Name = Name;
                Device.Variant = deviceProduct != DeviceProduct.Unknown ? "N/A" : UNKNOWN_FRIENDLY_NAME;
                Device.Product = deviceProduct;

                NotifyDeviceArrival();
            }
        }

        private void HandleFastbootDevice(string ID, string Name)
        {
            FastBootTransport fastBootTransport = new(ID);

            bool result = fastBootTransport.GetVariable("product", out string productGetVar);
            string ProductName = !result ? null : productGetVar;
            result = fastBootTransport.GetVariable("version-baseband", out string versionBaseBand);
            bool isUEFI = !result ? false : versionBaseBand == "1.0.0.0";
            result = fastBootTransport.GetVariable("is-userspace", out productGetVar);
            string IsUserSpace = !result ? null : productGetVar;

            // Attempt to retrieve the device type in bootloader mode
            string DeviceVariant = "N/A";

            switch (ProductName)
            {
                case "vayu":
                    {
                        if (Device.State != DeviceState.DISCONNECTED)
                        {
                            NotifyDeviceDeparture();
                        }

                        if (isUEFI)
                        {
                            Device.State = DeviceState.UEFI;
                        }
                        else if (IsUserSpace == "yes")
                        {
                            Device.State = DeviceState.FASTBOOTD;
                        }
                        else
                        {
                            Device.State = DeviceState.BOOTLOADER;
                        }

                        Device.ID = ID;
                        Device.Name = VAYU_FRIENDLY_NAME;
                        Device.Variant = "vayu";
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
                default:
                    {
                        if (Device.State != DeviceState.DISCONNECTED)
                        {
                            NotifyDeviceDeparture();
                        }

                        if (isUEFI)
                        {
                            Device.State = DeviceState.UEFI;
                        }
                        else if (IsUserSpace == "yes")
                        {
                            Device.State = DeviceState.FASTBOOTD;
                        }
                        else
                        {
                            Device.State = DeviceState.BOOTLOADER;
                        }

                        Device.ID = ID;
                        Device.Name = ProductName;
                        Device.Variant = UNKNOWN_FRIENDLY_NAME;
                        Device.Product = DeviceProduct.Unknown;

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
        }

        private void HandleADBDevice(string ID, string Name)
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

                try
                {
                    androidDebugBridgeTransport.Connect();
                }
                catch { }
            }
        }

        private void HandleAndroidDevice(string ID, string Name)
        {
            if (ID.Contains(FASTBOOT_USBID))
            {
                try
                {
                    HandleFastbootDevice(ID, Name);
                }
                catch { }
            }
            else if (ID.Contains(ANDROID_USBID))
            {
                Thread.Sleep(1000);

                try
                {
                    HandleADBDevice(ID, Name);
                }
                catch { }
            }
            else
            {
                    Thread.Sleep(1000);

                    try
                    {
                        HandleADBDevice(ID, Name);
                    }
                    catch { }
            
            }
        }

        private void HandleADBEnabledDevice(AndroidDebugBridgeTransport androidDebugBridgeTransport)
        {
            if (!androidDebugBridgeTransport.IsConnected)
            {
                return;
            }

            string ID = androidDebugBridgeTransport.DevicePath;

            bool isTWRPEnvironment = androidDebugBridgeTransport.GetVariableValue("ro.twrp.boot") == "1";
            string Hardware = androidDebugBridgeTransport.GetVariableValue("ro.boot.product.hardware.sku");
            if (string.IsNullOrEmpty(Hardware))
            {
                Hardware = "vayu";
                if (androidDebugBridgeTransport.GetPhoneConnectionVariables().ContainsKey("ro.product.device"))
                {
                    Hardware = androidDebugBridgeTransport.GetPhoneConnectionVariables()["ro.product.device"];
                }
            }

            switch (Hardware)
            {
                case "vayu":
                    {
                        if (isTWRPEnvironment)
                        {
                            if (Device.MassStorageID != null)
                            {
                                Device.State = DeviceState.TWRP_MASS_STORAGE_ADB_ENABLED;
                            }
                            else
                            {
                                Device.State = DeviceState.TWRP_ADB_ENABLED;
                            }
                        }
                        else if (androidDebugBridgeTransport.GetPhoneConnectionEnvironment() == "recovery")
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

                        Device.Variant = "vayu";
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
                default:
                    {
                        if (isTWRPEnvironment)
                        {
                            if (Device.MassStorageID != null)
                            {
                                Device.State = DeviceState.TWRP_MASS_STORAGE_ADB_ENABLED;
                            }
                            else
                            {
                                Device.State = DeviceState.TWRP_ADB_ENABLED;
                            }
                        }
                        else if (androidDebugBridgeTransport.GetPhoneConnectionEnvironment() == "recovery")
                        {
                            Device.State = DeviceState.RECOVERY_ADB_ENABLED;
                        }
                        else if (androidDebugBridgeTransport.GetPhoneConnectionEnvironment() == "sideload")
                        {
                            Device.State = DeviceState.SIDELOAD_ADB_ENABLED;
                        }
                        else
                        {
                            Device.State = DeviceState.ANDROID_ADB_ENABLED;
                        }

                        Device.ID = ID;
                        Device.Name = Hardware;
                        Device.Variant = UNKNOWN_FRIENDLY_NAME;
                        Device.Product = DeviceProduct.Unknown;

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

        private void HandleADBDisabledDevice(AndroidDebugBridgeTransport androidDebugBridgeTransport)
        {
            if (androidDebugBridgeTransport.IsConnected)
            {
                return;
            }

            string ID = androidDebugBridgeTransport.DevicePath;

            string Hardware = "vayu";
            bool isTWRPEnvironment = false;

            if (ID.Contains(VAYU_TWRP_USBID1) || ID.Contains(VAYU_TWRP_USBID2))
            {
                Hardware = "vayu";
                isTWRPEnvironment = true;
            }

            switch (Hardware)
            {
                case "vayu":
                    {
                        if (isTWRPEnvironment)
                        {
                            if (Device.MassStorageID != null)
                            {
                                Device.State = DeviceState.TWRP_MASS_STORAGE_ADB_DISABLED;
                            }
                            else
                            {
                                Device.State = DeviceState.TWRP_ADB_DISABLED;
                            }
                        }
                        else if (Device.State != DeviceState.DISCONNECTED)
                        {
                            NotifyDeviceDeparture();
                        }

                        Device.State = DeviceState.ANDROID_ADB_DISABLED;
                        Device.ID = ID;
                        Device.Name = VAYU_FRIENDLY_NAME;
                        Device.Variant = "vayu";
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
                default:
                    {
                        if (isTWRPEnvironment)
                        {
                            if (Device.MassStorageID != null)
                            {
                                Device.State = DeviceState.TWRP_MASS_STORAGE_ADB_DISABLED;
                            }
                            else
                            {
                                Device.State = DeviceState.TWRP_ADB_DISABLED;
                            }
                        }
                        else if (Device.State != DeviceState.DISCONNECTED)
                        {
                            NotifyDeviceDeparture();
                        }

                        Device.State = DeviceState.ANDROID_ADB_DISABLED;

                        Device.ID = ID;
                        Device.Name = Hardware;
                        Device.Variant = UNKNOWN_FRIENDLY_NAME;
                        Device.Product = DeviceProduct.Unknown;

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

        private void AndroidDebugBridgeTransport_OnConnectionEstablished(object sender, EventArgs e)
        {
            AndroidDebugBridgeTransport androidDebugBridgeTransport = (AndroidDebugBridgeTransport)sender;
            androidDebugBridgeTransport.OnConnectionEstablished -= AndroidDebugBridgeTransport_OnConnectionEstablished;
            HandleADBEnabledDevice(androidDebugBridgeTransport);
        }
    }
}
