using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace NativeWifi
{
    public sealed class NativeWifiClient : IDisposable
    {
        private readonly WlanClientHandle clientHandle;

        public NativeWifiClient()
        {
            uint negotiatedVersion;
            var result = NativeMethods.WlanOpenHandle(
                2,
                IntPtr.Zero,
                out negotiatedVersion,
                out clientHandle
            );
            if (result != 0)
            {
                throw new Win32Exception((int)result);
            }
        }

        public void Dispose()
        {
            if (clientHandle != null)
            {
                clientHandle.Dispose();
            }
            GC.SuppressFinalize(this);
        }

        public void Connect(
            Guid interfaceGuid,
            uint connectionMode,
            string profile,
            string ssid,
            string[] desiredBssidList,
            uint bssType,
            uint flags
        )
        {
            var ssidPtr = SsidConverter.ToPtr(ssid);
            var bssidListPtr = BssidListConverter.ToPtr(desiredBssidList);
            try
            {
                var connectionParameters = new NativeMethods.WLAN_CONNECTION_PARAMETERS
                {
                    wlanConnectionMode = connectionMode,
                    strProfile = profile,
                    pDot11Ssid = ssidPtr,
                    pDesiredBssidList = bssidListPtr,
                    dot11BssType = bssType,
                    dwFlags = flags
                };
                var result = NativeMethods.WlanConnect(
                    clientHandle,
                    ref interfaceGuid,
                    ref connectionParameters,
                    IntPtr.Zero
                );
                if (result != 0)
                {
                    throw new Win32Exception((int)result);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(bssidListPtr);
                Marshal.FreeHGlobal(ssidPtr);
            }
        }

        public void Disconnect(Guid interfaceGuid)
        {
            var result = NativeMethods.WlanDisconnect(
                clientHandle,
                ref interfaceGuid,
                IntPtr.Zero
            );
            if (result != 0)
            {
                throw new Win32Exception((int)result);
            }
        }

        public InterfaceInfo[] EnumInterfaces()
        {
            var interfaceListPtr = IntPtr.Zero;
            try
            {
                var result = NativeMethods.WlanEnumInterfaces(
                    clientHandle,
                    IntPtr.Zero,
                    out interfaceListPtr
                );
                if (result != 0)
                {
                    throw new Win32Exception((int)result);
                }
                return InterfaceInfoListConverter.FromPtr(interfaceListPtr);
            }
            finally
            {
                NativeMethods.WlanFreeMemory(interfaceListPtr);
            }
        }

        public AvailableNetwork[] GetAvailableNetworkList(
            Guid interfaceGuid,
            uint flags
        )
        {
            var networkListPtr = IntPtr.Zero;
            try
            {
                var result = NativeMethods.WlanGetAvailableNetworkList(
                    clientHandle,
                    ref interfaceGuid,
                    flags,
                    IntPtr.Zero,
                    out networkListPtr
                );
                if (result != 0)
                {
                    throw new Win32Exception((int)result);
                }
                return AvailableNetworkListConverter.FromPtr(networkListPtr);
            }
            finally
            {
                NativeMethods.WlanFreeMemory(networkListPtr);
            }
        }

        public InterfaceCapability GetInterfaceCapability(Guid interfaceGuid)
        {
            var capabilityPtr = IntPtr.Zero;
            try
            {
                var result = NativeMethods.WlanGetInterfaceCapability(
                    clientHandle,
                    ref interfaceGuid,
                    IntPtr.Zero,
                    out capabilityPtr
                );
                if (result != 0)
                {
                    throw new Win32Exception((int)result);
                }
                return InterfaceCapability.FromPtr(capabilityPtr);
            }
            finally
            {
                NativeMethods.WlanFreeMemory(capabilityPtr);
            }
        }

        public BssEntry[] GetNetworkBssList(
            Guid interfaceGuid,
            string ssid,
            uint bssType,
            bool securityEnabled
        )
        {
            var ssidPtr = SsidConverter.ToPtr(ssid);
            var bssListPtr = IntPtr.Zero;
            try
            {
                var result = NativeMethods.WlanGetNetworkBssList(
                    clientHandle,
                    ref interfaceGuid,
                    ssidPtr,
                    bssType,
                    securityEnabled,
                    IntPtr.Zero,
                    out bssListPtr
                );
                if (result != 0)
                {
                    throw new Win32Exception((int)result);
                }
                return BssListConverter.FromPtr(bssListPtr);
            }
            finally
            {
                NativeMethods.WlanFreeMemory(bssListPtr);
                Marshal.FreeHGlobal(ssidPtr);
            }
        }

        public ConnectionAttributes QueryCurrentConnection(Guid interfaceGuid)
        {
            IntPtr dataPtr = IntPtr.Zero;
            try
            {
                int dataSize;
                uint opcodeValueType;
                var result = NativeMethods.WlanQueryInterface(
                    clientHandle,
                    ref interfaceGuid,
                    7, // wlan_intf_opcode_current_connection
                    IntPtr.Zero,
                    out dataSize,
                    out dataPtr,
                    out opcodeValueType
                );
                if (result != 0)
                {
                    throw new Win32Exception((int)result);
                }
                return ConnectionAttributes.FromPtr(dataPtr);
            }
            finally
            {
                NativeMethods.WlanFreeMemory(dataPtr);
            }
        }

        public void Scan(Guid interfaceGuid)
        {
            // This function returns immediately.
            // The scan will then be completed within 4 seconds.
            var result = NativeMethods.WlanScan(
                clientHandle,
                ref interfaceGuid,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero
            );
            if (result != 0)
            {
                throw new Win32Exception((int)result);
            }
        }
    }

    internal sealed class WlanClientHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public WlanClientHandle() : base(true) { }

        protected override bool ReleaseHandle()
        {
            return NativeMethods.WlanCloseHandle(handle, IntPtr.Zero) == 0;
        }
    }

    public class AssociationAttributes
    {
        public string dot11Ssid;
        public uint dot11BssType;
        public string dot11Bssid;
        public uint dot11PhyType;
        public uint dot11PhyIndex;
        public uint wlanSignalQuality;
        public uint rxRate;
        public uint txRate;

        internal static AssociationAttributes FromStruct(NativeMethods.WLAN_ASSOCIATION_ATTRIBUTES st)
        {
            return new AssociationAttributes()
            {
                dot11Ssid = SsidConverter.ToString(st.dot11Ssid),
                dot11BssType = st.dot11BssType,
                dot11Bssid = BssidConverter.ToString(st.dot11Bssid),
                dot11PhyType = st.dot11PhyType,
                dot11PhyIndex = st.uDot11PhyIndex,
                wlanSignalQuality = st.wlanSignalQuality,
                rxRate = st.ulRxRate,
                txRate = st.ulTxRate,
            };
        }
    }

    public class AvailableNetwork
    {
        public string profileName;
        public string dot11Ssid;
        public uint dot11BssType;
        public uint numberOfBssids;
        public bool networkConnectable;
        public uint wlanNotConnectableReason;
        public uint numberOfPhyTypes;
        public uint[] dot11PhyTypes;
        public bool morePhyTypes;
        public uint wlanSignalQuality;
        public bool securityEnabled;
        public uint dot11DefaultAuthAlgorithm;
        public uint dot11DefaultCipherAlgorithm;
        public uint flags;
        public uint reserved;

        internal static AvailableNetwork FromPtr(IntPtr ptr)
        {
            var st = Marshal.PtrToStructure<NativeMethods.WLAN_AVAILABLE_NETWORK>(ptr);
            var dot11PhyTypes = new uint[st.uNumberOfPhyTypes];
            Array.Copy(st.dot11PhyTypes, dot11PhyTypes, dot11PhyTypes.Length);
            return new AvailableNetwork()
            {
                profileName = st.strProfileName,
                dot11Ssid = SsidConverter.ToString(st.dot11Ssid),
                dot11BssType = st.dot11BssType,
                numberOfBssids = st.uNumberOfBssids,
                networkConnectable = st.bNetworkConnectable,
                wlanNotConnectableReason = st.wlanNotConnectableReason,
                numberOfPhyTypes = st.uNumberOfPhyTypes,
                dot11PhyTypes = dot11PhyTypes,
                morePhyTypes = st.bMorePhyTypes,
                wlanSignalQuality = st.wlanSignalQuality,
                securityEnabled = st.bSecurityEnabled,
                dot11DefaultAuthAlgorithm = st.dot11DefaultAuthAlgorithm,
                dot11DefaultCipherAlgorithm = st.dot11DefaultCipherAlgorithm,
                flags = st.dwFlags,
                reserved = st.dwReserved,
            };
        }
    }

    public class BssEntry
    {
        public string dot11Ssid;
        public uint phyId;
        public string dot11Bssid;
        public uint dot11BssType;
        public uint dot11BssPhyType;
        public int rssi;
        public uint linkQuality;
        public bool inRegDomain;
        public ushort beaconPeriod;
        public ulong timestamp;
        public ulong hostTimestamp;
        public ushort capabilityInformation;
        public uint chCenterFrequency;
        public ushort[] wlanRateSet;
        public byte[] informationElements;

        internal static BssEntry FromPtr(IntPtr ptr)
        {
            var st = Marshal.PtrToStructure<NativeMethods.WLAN_BSS_ENTRY>(ptr);
            var wlanRateSet = new ushort[st.wlanRateSet.uRateSetLength];
            Array.Copy(st.wlanRateSet.usRateSet, wlanRateSet, wlanRateSet.Length);
            var informationElements = Array.Empty<byte>();
            if (st.ulIeSize > 0)
            {
                informationElements = new byte[(int)st.ulIeSize];
                Marshal.Copy(
                    IntPtr.Add(ptr, (int)st.ulIeOffset),
                    informationElements,
                    0,
                    informationElements.Length
                );
            }
            return new BssEntry()
            {
                dot11Ssid = SsidConverter.ToString(st.dot11Ssid),
                phyId = st.uPhyId,
                dot11Bssid = BssidConverter.ToString(st.dot11Bssid),
                dot11BssType = st.dot11BssType,
                dot11BssPhyType = st.dot11BssPhyType,
                rssi = st.lRssi,
                linkQuality = st.uLinkQuality,
                inRegDomain = st.bInRegDomain,
                beaconPeriod = st.usBeaconPeriod,
                timestamp = st.ullTimestamp,
                hostTimestamp = st.ullHostTimestamp,
                capabilityInformation = st.usCapabilityInformation,
                chCenterFrequency = st.ulChCenterFrequency,
                wlanRateSet = wlanRateSet,
                informationElements = informationElements,
            };
        }
    }

    public class ConnectionAttributes
    {
        public uint state;
        public uint wlanConnectionMode;
        public string profileName;
        public AssociationAttributes wlanAssociationAttributes;
        public SecurityAttributes wlanSecurityAttributes;

        internal static ConnectionAttributes FromPtr(IntPtr ptr)
        {
            var st = Marshal.PtrToStructure<NativeMethods.WLAN_CONNECTION_ATTRIBUTES>(ptr);
            return new ConnectionAttributes
            {
                state = st.isState,
                wlanConnectionMode = st.wlanConnectionMode,
                profileName = st.strProfileName,
                wlanAssociationAttributes = AssociationAttributes.FromStruct(st.wlanAssociationAttributes),
                wlanSecurityAttributes = SecurityAttributes.FromStruct(st.wlanSecurityAttributes),
            };
        }
    }

    public class InterfaceCapability
    {
        public uint interfaceType;
        public bool dot11DSupported;
        public uint maxDesiredSsidListSize;
        public uint maxDesiredBssidListSize;
        public uint numberOfSupportedPhys;
        public uint[] dot11PhyTypes;

        internal static InterfaceCapability FromPtr(IntPtr ptr)
        {
            var st = Marshal.PtrToStructure<NativeMethods.WLAN_INTERFACE_CAPABILITY>(ptr);
            var dot11PhyTypes = new uint[st.dwNumberOfSupportedPhys];
            Array.Copy(st.dot11PhyTypes, dot11PhyTypes, dot11PhyTypes.Length);
            return new InterfaceCapability
            {
                interfaceType = st.interfaceType,
                dot11DSupported = st.bDot11DSupported,
                maxDesiredSsidListSize = st.dwMaxDesiredSsidListSize,
                maxDesiredBssidListSize = st.dwMaxDesiredBssidListSize,
                numberOfSupportedPhys = st.dwNumberOfSupportedPhys,
                dot11PhyTypes = dot11PhyTypes,
            };
        }
    }

    public class InterfaceInfo
    {
        public Guid interfaceGuid;
        public string interfaceDescription;
        public uint state;

        internal static InterfaceInfo FromPtr(IntPtr ptr)
        {
            var st = Marshal.PtrToStructure<NativeMethods.WLAN_INTERFACE_INFO>(ptr);
            return new InterfaceInfo
            {
                interfaceGuid = st.InterfaceGuid,
                interfaceDescription = st.strInterfaceDescription,
                state = st.isState,
            };
        }
    }

    public class SecurityAttributes
    {
        public bool securityEnabled;
        public bool oneXEnabled;
        public uint dot11AuthAlgorithm;
        public uint dot11CipherAlgorithm;

        internal static SecurityAttributes FromStruct(NativeMethods.WLAN_SECURITY_ATTRIBUTES st)
        {
            return new SecurityAttributes
            {
                securityEnabled = st.bSecurityEnabled,
                oneXEnabled = st.bOneXEnabled,
                dot11AuthAlgorithm = st.dot11AuthAlgorithm,
                dot11CipherAlgorithm = st.dot11CipherAlgorithm,
            };
        }
    }

    internal static class AvailableNetworkListConverter
    {
        public static AvailableNetwork[] FromPtr(IntPtr ptr)
        {
            var header = Marshal.PtrToStructure<NativeMethods.WLAN_AVAILABLE_NETWORK_LIST>(ptr);
            if (header.dwNumberOfItems > 0)
            {
                var items = new AvailableNetwork[header.dwNumberOfItems];
                var itemPtr = IntPtr.Add(ptr, Marshal.SizeOf<NativeMethods.WLAN_AVAILABLE_NETWORK_LIST>());
                var itemSize = Marshal.SizeOf<NativeMethods.WLAN_AVAILABLE_NETWORK>();
                for (int i = 0; i < header.dwNumberOfItems; i++)
                {
                    items[i] = AvailableNetwork.FromPtr(itemPtr);
                    itemPtr = IntPtr.Add(itemPtr, itemSize);
                }
                return items;
            }
            return Array.Empty<AvailableNetwork>();
        }
    }

    internal static class BssidConverter
    {
        public static NativeMethods.DOT11_MAC_ADDRESS ToStruct(string bssid)
        {
            if (bssid.Length != 12)
            {
                throw new ArgumentException("BSSID must be 12 chars");
            }
            var st = new NativeMethods.DOT11_MAC_ADDRESS
            {
                ucDot11MacAddress = new byte[bssid.Length / 2]
            };
            for (int i = 0; i < st.ucDot11MacAddress.Length; i++)
            {
                st.ucDot11MacAddress[i] = Convert.ToByte(bssid.Substring(i * 2, 2), 16);
            }
            return st;
        }

        public static string ToString(NativeMethods.DOT11_MAC_ADDRESS st)
        {
            return BitConverter.ToString(st.ucDot11MacAddress).Replace("-", "").ToLowerInvariant();
        }
    }

    internal static class BssidListConverter
    {
        public static IntPtr ToPtr(string[] bssids)
        {
            if (bssids.Length == 0)
            {
                return IntPtr.Zero;
            }
            var headerSize = Marshal.SizeOf<NativeMethods.DOT11_BSSID_LIST>();
            var itemSize = Marshal.SizeOf<NativeMethods.DOT11_MAC_ADDRESS>();
            var st = new NativeMethods.DOT11_BSSID_LIST()
            {
                Header = new NativeMethods.NDIS_OBJECT_HEADER()
                {
                    Type = 0x80,
                    Revision = 1,
                    Size = (ushort)Marshal.SizeOf<NativeMethods.NDIS_OBJECT_HEADER>()
                },
                uNumOfEntries = (uint)bssids.Length,
                uTotalNumOfEntries = (uint)bssids.Length,
            };
            var ptr = Marshal.AllocHGlobal(headerSize + itemSize * bssids.Length);
            Marshal.StructureToPtr(st, ptr, false);
            var itemPtr = IntPtr.Add(ptr, headerSize);
            for (int i = 0; i < bssids.Length; i++)
            {
                var item = BssidConverter.ToStruct(bssids[i]);
                Marshal.Copy(item.ucDot11MacAddress, 0, itemPtr, itemSize);
                itemPtr = IntPtr.Add(itemPtr, itemSize);
            }
            return ptr;
        }
    }

    internal static class BssListConverter
    {
        public static BssEntry[] FromPtr(IntPtr ptr)
        {
            var header = Marshal.PtrToStructure<NativeMethods.WLAN_BSS_LIST>(ptr);
            if (header.dwNumberOfItems > 0)
            {
                var items = new BssEntry[header.dwNumberOfItems];
                var itemPtr = IntPtr.Add(ptr, Marshal.SizeOf<NativeMethods.WLAN_BSS_LIST>());
                var itemSize = Marshal.SizeOf<NativeMethods.WLAN_BSS_ENTRY>();
                for (int i = 0; i < header.dwNumberOfItems; i++)
                {
                    items[i] = BssEntry.FromPtr(itemPtr);
                    itemPtr = IntPtr.Add(itemPtr, itemSize);
                }
                return items;
            }
            return Array.Empty<BssEntry>();
        }
    }

    internal static class InterfaceInfoListConverter
    {
        public static InterfaceInfo[] FromPtr(IntPtr ptr)
        {
            var header = Marshal.PtrToStructure<NativeMethods.WLAN_INTERFACE_INFO_LIST>(ptr);
            if (header.dwNumberOfItems > 0)
            {
                var items = new InterfaceInfo[header.dwNumberOfItems];
                var itemPtr = IntPtr.Add(ptr, Marshal.SizeOf<NativeMethods.WLAN_INTERFACE_INFO_LIST>());
                var itemSize = Marshal.SizeOf<NativeMethods.WLAN_INTERFACE_INFO>();
                for (int i = 0; i < header.dwNumberOfItems; i++)
                {
                    items[i] = InterfaceInfo.FromPtr(itemPtr);
                    itemPtr = IntPtr.Add(itemPtr, itemSize);
                }
                return items;
            }
            return Array.Empty<InterfaceInfo>();
        }
    }

    internal static class SsidConverter
    {
        public static IntPtr ToPtr(string ssid)
        {
            if (ssid.Length == 0)
            {
                return IntPtr.Zero;
            }
            var ssidBytes = Encoding.UTF8.GetBytes(ssid);
            if (ssidBytes.Length > 32)
            {
                throw new ArgumentException("SSID must be 32 chars or less");
            }
            var st = new NativeMethods.DOT11_SSID
            {
                uSSIDLength = (uint)ssidBytes.Length,
                ucSSID = new byte[32],
            };
            Array.Copy(ssidBytes, st.ucSSID, ssidBytes.Length);
            IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf<NativeMethods.DOT11_SSID>());
            Marshal.StructureToPtr(st, ptr, false);
            return ptr;
        }

        public static string ToString(NativeMethods.DOT11_SSID st)
        {
            return Encoding.UTF8.GetString(st.ucSSID, 0, (int)st.uSSIDLength);
        }
    }

    internal static class NativeMethods
    {
        [DllImport("wlanapi.dll", SetLastError = true)]
        public static extern uint WlanCloseHandle(
            IntPtr hClientHandle,
            IntPtr pReserved
        );

        [DllImport("wlanapi.dll", SetLastError = true)]
        public static extern uint WlanConnect(
            WlanClientHandle hClientHandle,
            ref Guid pInterfaceGuid,
            ref WLAN_CONNECTION_PARAMETERS pConnectionParameters,
            IntPtr pReserved
        );

        [DllImport("wlanapi.dll", SetLastError = true)]
        public static extern uint WlanDisconnect(
            WlanClientHandle hClientHandle,
            ref Guid pInterfaceGuid,
            IntPtr pReserved
        );

        [DllImport("wlanapi.dll", SetLastError = true)]
        public static extern uint WlanEnumInterfaces(
            WlanClientHandle hClientHandle,
            IntPtr pReserved,
            out IntPtr ppInterfaceList
        );

        [DllImport("wlanapi.dll", SetLastError = true)]
        public static extern void WlanFreeMemory(
            IntPtr pMemory
        );

        [DllImport("wlanapi.dll", SetLastError = true)]
        public static extern uint WlanGetAvailableNetworkList(
            WlanClientHandle hClientHandle,
            ref Guid pInterfaceGuid,
            uint dwFlags,
            IntPtr pReserved,
            out IntPtr ppAvailableNetworkList
        );

        [DllImport("wlanapi.dll", SetLastError = true)]
        public static extern uint WlanGetInterfaceCapability(
            WlanClientHandle hClientHandle,
            ref Guid pInterfaceGuid,
            IntPtr pReserved,
            out IntPtr ppCapability
        );

        [DllImport("wlanapi.dll", SetLastError = true)]
        public static extern uint WlanGetNetworkBssList(
            WlanClientHandle hClientHandle,
            ref Guid pInterfaceGuid,
            IntPtr pDot11Ssid,
            uint dot11BssType,
            bool bSecurityEnabled,
            IntPtr pReserved,
            out IntPtr ppWlanBssList
        );

        [DllImport("wlanapi.dll", SetLastError = true)]
        public static extern uint WlanOpenHandle(
            uint dwClientVersion,
            IntPtr pReserved,
            out uint pdwNegotiatedVersion,
            out WlanClientHandle phClientHandle
        );

        [DllImport("wlanapi.dll", SetLastError = true)]
        public static extern uint WlanQueryInterface(
            WlanClientHandle hClientHandle,
            ref Guid pInterfaceGuid,
            uint OpCode,
            IntPtr pReserved,
            out int pdwDataSize,
            out IntPtr ppData,
            out uint pWlanOpcodeValueType
        );

        [DllImport("wlanapi.dll", SetLastError = true)]
        public static extern uint WlanScan(
            WlanClientHandle hClientHandle,
            ref Guid pInterfaceGuid,
            IntPtr pDot11Ssid,
            IntPtr pIeData,
            IntPtr pReserved
        );

        [StructLayout(LayoutKind.Sequential)]
        public struct DOT11_BSSID_LIST
        {
            public NDIS_OBJECT_HEADER Header;
            public uint uNumOfEntries;
            public uint uTotalNumOfEntries;
            //
            // Note: DOT11_MAC_ADDRESS[] (variable length) follows.
            //
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DOT11_MAC_ADDRESS
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
            public byte[] ucDot11MacAddress;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DOT11_SSID
        {
            public uint uSSIDLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] ucSSID;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct NDIS_OBJECT_HEADER
        {
            public byte Type;
            public byte Revision;
            public ushort Size;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WLAN_ASSOCIATION_ATTRIBUTES
        {
            public DOT11_SSID dot11Ssid;
            public uint dot11BssType;
            public DOT11_MAC_ADDRESS dot11Bssid;
            public uint dot11PhyType;
            public uint uDot11PhyIndex;
            public uint wlanSignalQuality;
            public uint ulRxRate;
            public uint ulTxRate;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WLAN_AVAILABLE_NETWORK
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string strProfileName;
            public DOT11_SSID dot11Ssid;
            public uint dot11BssType;
            public uint uNumberOfBssids;
            [MarshalAs(UnmanagedType.Bool)]
            public bool bNetworkConnectable;
            public uint wlanNotConnectableReason;
            public uint uNumberOfPhyTypes;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public uint[] dot11PhyTypes;
            [MarshalAs(UnmanagedType.Bool)]
            public bool bMorePhyTypes;
            public uint wlanSignalQuality;
            [MarshalAs(UnmanagedType.Bool)]
            public bool bSecurityEnabled;
            public uint dot11DefaultAuthAlgorithm;
            public uint dot11DefaultCipherAlgorithm;
            public uint dwFlags;
            public uint dwReserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WLAN_AVAILABLE_NETWORK_LIST
        {
            public uint dwNumberOfItems;
            public uint dwIndex;
            //
            // Note: WLAN_AVAILABLE_NETWORK[] (variable length) follows.
            //
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WLAN_BSS_ENTRY
        {
            public DOT11_SSID dot11Ssid;
            public uint uPhyId;
            public DOT11_MAC_ADDRESS dot11Bssid;
            public uint dot11BssType;
            public uint dot11BssPhyType;
            public int lRssi;
            public uint uLinkQuality;
            [MarshalAs(UnmanagedType.Bool)]
            public bool bInRegDomain;
            public ushort usBeaconPeriod;
            public ulong ullTimestamp;
            public ulong ullHostTimestamp;
            public ushort usCapabilityInformation;
            public uint ulChCenterFrequency;
            public WLAN_RATE_SET wlanRateSet;
            public uint ulIeOffset;
            public uint ulIeSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WLAN_BSS_LIST
        {
            public uint dwTotalSize;
            public uint dwNumberOfItems;
            //
            // Note: WLAN_BSS_ENTRY[] (variable length) follows.
            //
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WLAN_CONNECTION_ATTRIBUTES
        {
            public uint isState;
            public uint wlanConnectionMode;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string strProfileName;
            public WLAN_ASSOCIATION_ATTRIBUTES wlanAssociationAttributes;
            public WLAN_SECURITY_ATTRIBUTES wlanSecurityAttributes;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WLAN_CONNECTION_PARAMETERS
        {
            public uint wlanConnectionMode;
            [MarshalAs(UnmanagedType.LPWStr, SizeConst = 256)]
            public string strProfile;
            public IntPtr pDot11Ssid;
            public IntPtr pDesiredBssidList;
            public uint dot11BssType;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WLAN_INTERFACE_CAPABILITY
        {
            public uint interfaceType;
            [MarshalAs(UnmanagedType.Bool)]
            public bool bDot11DSupported;
            public uint dwMaxDesiredSsidListSize;
            public uint dwMaxDesiredBssidListSize;
            public uint dwNumberOfSupportedPhys;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
            public uint[] dot11PhyTypes;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WLAN_INTERFACE_INFO
        {
            public Guid InterfaceGuid;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string strInterfaceDescription;
            public uint isState;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WLAN_INTERFACE_INFO_LIST
        {
            public uint dwNumberOfItems;
            public uint dwIndex;
            //
            // Note: WLAN_INTERFACE_INFO[] (variable length) follows.
            //
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WLAN_RATE_SET
        {
            public uint uRateSetLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 126)]
            public ushort[] usRateSet;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WLAN_SECURITY_ATTRIBUTES
        {
            [MarshalAs(UnmanagedType.Bool)]
            public bool bSecurityEnabled;
            [MarshalAs(UnmanagedType.Bool)]
            public bool bOneXEnabled;
            public uint dot11AuthAlgorithm;
            public uint dot11CipherAlgorithm;
        }
    }
}
