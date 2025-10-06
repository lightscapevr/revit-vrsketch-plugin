using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;


namespace VRSketch
{
    using RetryWebSocket;

    
    static class VRSketchConnect
    {
//#if !DEBUG     <- use this to make debug versions to use with VirtualBox
#if 1
        static readonly byte[] IPADDRESS = { 127, 0, 0, 1 };
        const string METASERVER = "wss://vrsketch.eu/cloudsession/c1";
        //const bool METASERVER_BLIND_TRUST = false;
#else
        // use this with VirtualBox in NAT mode:
        static readonly byte[] IPADDRESS = { 10, 0, 2, 2 };
        // I didn't manage to get other modes working.  A workaround, depending on what
        // your problem is, is to set the Windows DNS in the VM to a non-existing IP.
        // That prevents many unwanted connections from succeeding, but you can connect
        // to a VR Sketch executable running on the host.  With this workaround, though,
        // we need to resolve the METASERVER url manually---the following line assume
        // you run on the host machine "ssh -N -v -g -L9021:127.0.0.1:9021 baroquesoftware.com"
        const string METASERVER = "ws://10.0.2.2:9021/c1";
        //const bool METASERVER_BLIND_TRUST = true;
#endif

        const int PORT = 17352;



        static bool TryConnectLocalhost(out Socket sock)
        {
            return TryConnect(new IPEndPoint(new IPAddress(IPADDRESS), PORT), out sock);
        }

        static bool TryConnect(EndPoint endpoint, out Socket sock)
        {
            sock = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                sock.Connect(endpoint);
                return true;
            }
            catch (SocketException)
            {
                sock.Close();
                sock = null;
                return false;
            }
        }

        public static void OpenConnection(Connection con, string quest_id /*possibly null*/, Action after_opened)
        {
            if (quest_id == null)
                OpenLocalHostConnection(con, after_opened);
            else
                OpenQuestConnection(con, quest_id, after_opened);
        }

        static void OpenLocalHostConnection(Connection con, Action after_opened)
        {
            if (!TryConnectLocalhost(out var sock))
            {
                var directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var exe = Path.Combine(directory, "VRSketch", $"VRSketch{VRSketchApp.VRSKETCH_EXE_VERSION}.exe");

                System.Diagnostics.Process process;
                try
                {
                    process = System.Diagnostics.Process.Start(exe);
                }
                catch
                {
                    con.Close();
                    MessageBox.Show($"Failed to start the Unity subprocess: {exe}",
                        "VR Sketch", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                while (true)
                {
                    Thread.Sleep(300);
                    if (process.HasExited)
                    {
                        con.Close();
                        MessageBox.Show("Failed to connect to the Unity subprocess",
                            "VR Sketch", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    if (TryConnectLocalhost(out sock))
                        break;
                }
                process.Dispose();
            }
            con.SetDirectSocket(sock);
            after_opened();
        }


        static class Interop
        {
            /* the content of this class is copied from
             * https://www.pinvoke.net/default.aspx/iphlpapi/GetAdaptersAddresses.html
             */
            public enum FAMILY : uint
            {
                /// <summary>IPv4</summary>
                AF_INET = 2,
                /// <summary>IPv6</summary>
                AF_INET6 = 23,
                /// <summary>Unpecified. Includes both IPv4 and IPv4</summary>
                AF_UNSPEC = 0
            }
            public enum FLAGS : uint
            {
                GAA_FLAG_DEFAULT = 0x0000,
                GAA_FLAG_SKIP_UNICAST = 0x0001,
                GAA_FLAG_SKIP_ANYCAST = 0x0002,
                GAA_FLAG_SKIP_MULTICAST = 0x0004,
                GAA_FLAG_SKIP_DNS_SERVER = 0x0008,
                GAA_FLAG_INCLUDE_PREFIX = 0x0010,
                GAA_FLAG_SKIP_FRIENDLY_NAME = 0x0020,
                GAA_FLAG_INCLUDE_WINS_INFO = 0x0040,
                GAA_FLAG_INCLUDE_GATEWAYS = 0x0080,
                GAA_FLAG_INCLUDE_ALL_INTERFACES = 0x0100,
                GAA_FLAG_INCLUDE_ALL_COMPARTMENTS = 0x0200,
                GAA_FLAG_INCLUDE_TUNNEL_BINDINGORDER = 0x0400,
                GAA_FLAG_SKIP_DNS_INFO = 0x0800
            }
            public enum ERROR : uint
            {
                ERROR_SUCCESS = 0,
                ERROR_NO_DATA = 232,
                ERROR_BUFFER_OVERFLOW = 111,
                ERROR_INVALID_PARAMETER = 87
            }
            public enum IF_OPER_STATUS : uint
            {
                IfOperStatusUp = 1,
                IfOperStatusDown,
                IfOperStatusTesting,
                IfOperStatusUnknown,
                IfOperStatusDormant,
                IfOperStatusNotPresent,
                IfOperStatusLowerLayerDown,
            }
            public enum IF_TYPE : uint
            {
                IF_TYPE_OTHER = 1,   // None of the below
                IF_TYPE_REGULAR_1822 = 2,
                IF_TYPE_HDH_1822 = 3,
                IF_TYPE_DDN_X25 = 4,
                IF_TYPE_RFC877_X25 = 5,
                IF_TYPE_ETHERNET_CSMACD = 6,
                IF_TYPE_IS088023_CSMACD = 7,
                IF_TYPE_ISO88024_TOKENBUS = 8,
                IF_TYPE_ISO88025_TOKENRING = 9,
                IF_TYPE_ISO88026_MAN = 10,
                IF_TYPE_STARLAN = 11,
                IF_TYPE_PROTEON_10MBIT = 12,
                IF_TYPE_PROTEON_80MBIT = 13,
                IF_TYPE_HYPERCHANNEL = 14,
                IF_TYPE_FDDI = 15,
                IF_TYPE_LAP_B = 16,
                IF_TYPE_SDLC = 17,
                IF_TYPE_DS1 = 18,  // DS1-MIB
                IF_TYPE_E1 = 19,  // Obsolete; see DS1-MIB
                IF_TYPE_BASIC_ISDN = 20,
                IF_TYPE_PRIMARY_ISDN = 21,
                IF_TYPE_PROP_POINT2POINT_SERIAL = 22,  // proprietary serial
                IF_TYPE_PPP = 23,
                IF_TYPE_SOFTWARE_LOOPBACK = 24,
                IF_TYPE_EON = 25,  // CLNP over IP
                IF_TYPE_ETHERNET_3MBIT = 26,
                IF_TYPE_NSIP = 27,  // XNS over IP
                IF_TYPE_SLIP = 28,  // Generic Slip
                IF_TYPE_ULTRA = 29,  // ULTRA Technologies
                IF_TYPE_DS3 = 30,  // DS3-MIB
                IF_TYPE_SIP = 31,  // SMDS, coffee
                IF_TYPE_FRAMERELAY = 32,  // DTE only
                IF_TYPE_RS232 = 33,
                IF_TYPE_PARA = 34,  // Parallel port
                IF_TYPE_ARCNET = 35,
                IF_TYPE_ARCNET_PLUS = 36,
                IF_TYPE_ATM = 37,  // ATM cells
                IF_TYPE_MIO_X25 = 38,
                IF_TYPE_SONET = 39,  // SONET or SDH
                IF_TYPE_X25_PLE = 40,
                IF_TYPE_ISO88022_LLC = 41,
                IF_TYPE_LOCALTALK = 42,
                IF_TYPE_SMDS_DXI = 43,
                IF_TYPE_FRAMERELAY_SERVICE = 44,  // FRNETSERV-MIB
                IF_TYPE_V35 = 45,
                IF_TYPE_HSSI = 46,
                IF_TYPE_HIPPI = 47,
                IF_TYPE_MODEM = 48,  // Generic Modem
                IF_TYPE_AAL5 = 49,  // AAL5 over ATM
                IF_TYPE_SONET_PATH = 50,
                IF_TYPE_SONET_VT = 51,
                IF_TYPE_SMDS_ICIP = 52,  // SMDS InterCarrier Interface
                IF_TYPE_PROP_VIRTUAL = 53,  // Proprietary virtual/internal
                IF_TYPE_PROP_MULTIPLEXOR = 54,  // Proprietary multiplexing
                IF_TYPE_IEEE80212 = 55,  // 100BaseVG
                IF_TYPE_FIBRECHANNEL = 56,
                IF_TYPE_HIPPIINTERFACE = 57,
                IF_TYPE_FRAMERELAY_INTERCONNECT = 58,  // Obsolete, use 32 or 44
                IF_TYPE_AFLANE_8023 = 59,  // ATM Emulated LAN for 802.3
                IF_TYPE_AFLANE_8025 = 60,  // ATM Emulated LAN for 802.5
                IF_TYPE_CCTEMUL = 61,  // ATM Emulated circuit
                IF_TYPE_FASTETHER = 62,  // Fast Ethernet (100BaseT)
                IF_TYPE_ISDN = 63,  // ISDN and X.25
                IF_TYPE_V11 = 64,  // CCITT V.11/X.21
                IF_TYPE_V36 = 65,  // CCITT V.36
                IF_TYPE_G703_64K = 66,  // CCITT G703 at 64Kbps
                IF_TYPE_G703_2MB = 67,  // Obsolete; see DS1-MIB
                IF_TYPE_QLLC = 68,  // SNA QLLC
                IF_TYPE_FASTETHER_FX = 69,  // Fast Ethernet (100BaseFX)
                IF_TYPE_CHANNEL = 70,
                IF_TYPE_IEEE80211 = 71,  // Radio spread spectrum
                IF_TYPE_IBM370PARCHAN = 72,  // IBM System 360/370 OEMI Channel
                IF_TYPE_ESCON = 73,  // IBM Enterprise Systems Connection
                IF_TYPE_DLSW = 74,  // Data Link Switching
                IF_TYPE_ISDN_S = 75,  // ISDN S/T interface
                IF_TYPE_ISDN_U = 76,  // ISDN U interface
                IF_TYPE_LAP_D = 77,  // Link Access Protocol D
                IF_TYPE_IPSWITCH = 78,  // IP Switching Objects
                IF_TYPE_RSRB = 79,  // Remote Source Route Bridging
                IF_TYPE_ATM_LOGICAL = 80,  // ATM Logical Port
                IF_TYPE_DS0 = 81,  // Digital Signal Level 0
                IF_TYPE_DS0_BUNDLE = 82,  // Group of ds0s on the same ds1
                IF_TYPE_BSC = 83,  // Bisynchronous Protocol
                IF_TYPE_ASYNC = 84,  // Asynchronous Protocol
                IF_TYPE_CNR = 85,  // Combat Net Radio
                IF_TYPE_ISO88025R_DTR = 86,  // ISO 802.5r DTR
                IF_TYPE_EPLRS = 87,  // Ext Pos Loc Report Sys
                IF_TYPE_ARAP = 88,  // Appletalk Remote Access Protocol
                IF_TYPE_PROP_CNLS = 89,  // Proprietary Connectionless Proto
                IF_TYPE_HOSTPAD = 90,  // CCITT-ITU X.29 PAD Protocol
                IF_TYPE_TERMPAD = 91,  // CCITT-ITU X.3 PAD Facility
                IF_TYPE_FRAMERELAY_MPI = 92,  // Multiproto Interconnect over FR
                IF_TYPE_X213 = 93,  // CCITT-ITU X213
                IF_TYPE_ADSL = 94,  // Asymmetric Digital Subscrbr Loop
                IF_TYPE_RADSL = 95,  // Rate-Adapt Digital Subscrbr Loop
                IF_TYPE_SDSL = 96,  // Symmetric Digital Subscriber Loop
                IF_TYPE_VDSL = 97,  // Very H-Speed Digital Subscrb Loop
                IF_TYPE_ISO88025_CRFPRINT = 98,  // ISO 802.5 CRFP
                IF_TYPE_MYRINET = 99,  // Myricom Myrinet
                IF_TYPE_VOICE_EM = 100,  // Voice recEive and transMit
                IF_TYPE_VOICE_FXO = 101,  // Voice Foreign Exchange Office
                IF_TYPE_VOICE_FXS = 102,  // Voice Foreign Exchange Station
                IF_TYPE_VOICE_ENCAP = 103,  // Voice encapsulation
                IF_TYPE_VOICE_OVERIP = 104,  // Voice over IP encapsulation
                IF_TYPE_ATM_DXI = 105,  // ATM DXI
                IF_TYPE_ATM_FUNI = 106,  // ATM FUNI
                IF_TYPE_ATM_IMA = 107,  // ATM IMA
                IF_TYPE_PPPMULTILINKBUNDLE = 108,  // PPP Multilink Bundle
                IF_TYPE_IPOVER_CDLC = 109,  // IBM ipOverCdlc
                IF_TYPE_IPOVER_CLAW = 110,  // IBM Common Link Access to Workstn
                IF_TYPE_STACKTOSTACK = 111,  // IBM stackToStack
                IF_TYPE_VIRTUALIPADDRESS = 112,  // IBM VIPA
                IF_TYPE_MPC = 113,  // IBM multi-proto channel support
                IF_TYPE_IPOVER_ATM = 114,  // IBM ipOverAtm
                IF_TYPE_ISO88025_FIBER = 115,  // ISO 802.5j Fiber Token Ring
                IF_TYPE_TDLC = 116,  // IBM twinaxial data link control
                IF_TYPE_GIGABITETHERNET = 117,
                IF_TYPE_HDLC = 118,
                IF_TYPE_LAP_F = 119,
                IF_TYPE_V37 = 120,
                IF_TYPE_X25_MLP = 121,  // Multi-Link Protocol
                IF_TYPE_X25_HUNTGROUP = 122,  // X.25 Hunt Group
                IF_TYPE_TRANSPHDLC = 123,
                IF_TYPE_INTERLEAVE = 124,  // Interleave channel
                IF_TYPE_FAST = 125,  // Fast channel
                IF_TYPE_IP = 126,  // IP (for APPN HPR in IP networks)
                IF_TYPE_DOCSCABLE_MACLAYER = 127,  // CATV Mac Layer
                IF_TYPE_DOCSCABLE_DOWNSTREAM = 128,  // CATV Downstream interface
                IF_TYPE_DOCSCABLE_UPSTREAM = 129,  // CATV Upstream interface
                IF_TYPE_A12MPPSWITCH = 130,  // Avalon Parallel Processor
                IF_TYPE_TUNNEL = 131,  // Encapsulation interface
                IF_TYPE_COFFEE = 132,  // Coffee pot
                IF_TYPE_CES = 133,  // Circuit Emulation Service
                IF_TYPE_ATM_SUBINTERFACE = 134,  // ATM Sub Interface
                IF_TYPE_L2_VLAN = 135,  // Layer 2 Virtual LAN using 802.1Q
                IF_TYPE_L3_IPVLAN = 136,  // Layer 3 Virtual LAN using IP
                IF_TYPE_L3_IPXVLAN = 137,  // Layer 3 Virtual LAN using IPX
                IF_TYPE_DIGITALPOWERLINE = 138,  // IP over Power Lines
                IF_TYPE_MEDIAMAILOVERIP = 139,  // Multimedia Mail over IP
                IF_TYPE_DTM = 140,  // Dynamic syncronous Transfer Mode
                IF_TYPE_DCN = 141,  // Data Communications Network
                IF_TYPE_IPFORWARD = 142,  // IP Forwarding Interface
                IF_TYPE_MSDSL = 143,  // Multi-rate Symmetric DSL
                IF_TYPE_IEEE1394 = 144,  // IEEE1394 High Perf Serial Bus
                IF_TYPE_RECEIVE_ONLY = 145 // TV adapter type
            }
            public enum IP_SUFFIX_ORIGIN : uint
            {
                /// IpSuffixOriginOther -> 0
                IpSuffixOriginOther = 0,
                IpSuffixOriginManual,
                IpSuffixOriginWellKnown,
                IpSuffixOriginDhcp,
                IpSuffixOriginLinkLayerAddress,
                IpSuffixOriginRandom,
            }
            public enum IP_PREFIX_ORIGIN : uint
            {
                /// IpPrefixOriginOther -> 0
                IpPrefixOriginOther = 0,
                IpPrefixOriginManual,
                IpPrefixOriginWellKnown,
                IpPrefixOriginDhcp,
                IpPrefixOriginRouterAdvertisement,
            }
            public enum IP_DAD_STATE : uint
            {
                /// IpDadStateInvalid -> 0
                IpDadStateInvalid = 0,
                IpDadStateTentative,
                IpDadStateDuplicate,
                IpDadStateDeprecated,
                IpDadStatePreferred,
            }

#if false
            public enum NET_IF_CONNECTION_TYPE : uint
            {
                NET_IF_CONNECTION_DEDICATED = 1,
                NET_IF_CONNECTION_PASSIVE = 2,
                NET_IF_CONNECTION_DEMAND = 3,
                NET_IF_CONNECTION_MAXIMUM = 4
            }

            public enum TUNNEL_TYPE : uint
            {
                TUNNEL_TYPE_NONE = 0,
                TUNNEL_TYPE_OTHER = 1,
                TUNNEL_TYPE_DIRECT = 2,
                TUNNEL_TYPE_6TO4 = 11,
                TUNNEL_TYPE_ISATAP = 13,
                TUNNEL_TYPE_TEREDO = 14,
                TUNNEL_TYPE_IPHTTPS = 15
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct GUID
            {
                uint Data1;
                ushort Data2;
                ushort Data3;
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
                byte[] Data4;
            }
#endif

            private const int MAX_ADAPTER_ADDRESS_LENGTH = 8;
            private const int MAX_ADAPTER_NAME_LENGTH = 256;
            private const int MAX_DHCPV6_DUID_LENGTH = 130;

#if false
            [StructLayout(LayoutKind.Sequential)]
            public struct SOCKADDR
            {
                /// u_short->unsigned short
                public ushort sa_family;

                /// char[14]
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 14)]
                public byte[] sa_data;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct SOCKET_ADDRESS
            {
                public IntPtr lpSockAddr;
                public int iSockaddrLength;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct IP_ADAPTER_UNICAST_ADDRESS
            {
                public UInt64 Alignment;
                public IntPtr Next;
                public SOCKET_ADDRESS Address;
                public IP_PREFIX_ORIGIN PrefixOrigin;
                public IP_SUFFIX_ORIGIN SuffixOrigin;
                public IP_DAD_STATE DadState;
                public uint ValidLifetime;
                public uint PreferredLifetime;
                public uint LeaseLifetime;
            }
#endif

            [StructLayout(LayoutKind.Sequential)]
            public struct IP_ADAPTER_ADDRESSES
            {
                public uint Length;
                public uint IfIndex;
                public IntPtr Next;
                /*[MarshalAs(UnmanagedType.LPStr)] public string*/ public IntPtr AdapterName;
                public IntPtr FirstUnicastAddress;
                public IntPtr FirstAnycastAddress;
                public IntPtr FirstMulticastAddress;
                public IntPtr FirstDnsServerAddress;
                /*[MarshalAs(UnmanagedType.LPWStr)] public string*/ public IntPtr DnsSuffix;
                /*[MarshalAs(UnmanagedType.LPWStr)] public string*/ public IntPtr Description;
                /*[MarshalAs(UnmanagedType.LPWStr)] public string*/ public IntPtr FriendlyName;
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = MAX_ADAPTER_ADDRESS_LENGTH)]
                public byte[] PhysicalAddress;
                public uint PhysicalAddressLength;
                public uint Flags;
                public uint Mtu;
                public IF_TYPE IfType;
                public IF_OPER_STATUS OperStatus;
                uint Ipv6IfIndex;
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
                public uint[] ZoneIndices;
                public IntPtr FirstPrefix;
#if false
                // Items added for Vista
                // May need to be removed on Windows versions below Vista to work properly (?)
                public UInt64 TrasmitLinkSpeed;
                public UInt64 ReceiveLinkSpeed;
                public IntPtr FirstWinsServerAddress;
                public IntPtr FirstGatewayAddress;
                public uint Ipv4Metric;
                public uint Ipv6Metric;
                public UInt64 Luid;
                public SOCKET_ADDRESS Dhcpv4Server;
                public uint CompartmentId;
                public GUID NetworkGuid;
                public NET_IF_CONNECTION_TYPE ConnectionType;
                public TUNNEL_TYPE TunnelType;
                public SOCKET_ADDRESS Dhcpv6Server;
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = MAX_DHCPV6_DUID_LENGTH)]
                public byte[] Dhcpv6ClientDuid;
                public uint Dhcpv6ClientDuidLength;
                public uint Dhcpv6Iaid;
                public uint FirstDnsSuffix;
#endif
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct IP_ADAPTER_PREFIX_XP       /* added by arigo */
            {
                public uint Length;
                public uint flags;
                public IntPtr Next;
                public IntPtr Address_Ptr;
                public uint Address_Length;
                public uint Padding;
                public uint PrefixLength;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct SOCKADDR_IN                /* added by arigo */
            {
                public ushort sin_family;
                public ushort sin_port;
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
                public byte[] sin_addr;
            }

            [DllImport("iphlpapi.dll")]
            public static extern ERROR GetAdaptersAddresses(uint Family, uint Flags, IntPtr Reserved, IntPtr pAdapterAddresses, ref uint pOutBufLen);
        }


        static void OpenQuestConnection(Connection con, string quest_id, Action after_opened)
        {
            List<IPAddress> EnumerateBroadcastIPs()
            {
                uint psize = 0;
                var err = Interop.GetAdaptersAddresses((uint)Interop.FAMILY.AF_INET,
                    (uint)Interop.FLAGS.GAA_FLAG_INCLUDE_PREFIX, IntPtr.Zero,
                    IntPtr.Zero, ref psize);

                if (err != Interop.ERROR.ERROR_BUFFER_OVERFLOW)
                    return null;
                if (psize <= 0 || psize > 99000000)
                    return null;

                IntPtr root = Marshal.AllocHGlobal((int)psize);
                try
                {
                    err = Interop.GetAdaptersAddresses((uint)Interop.FAMILY.AF_INET,
                        (uint)Interop.FLAGS.GAA_FLAG_INCLUDE_PREFIX, IntPtr.Zero,
                        root, ref psize);
                    
                    IntPtr ipaddr_intptr = err == 0 ? root : IntPtr.Zero;

                    var result = new List<IPAddress>();
                    while (ipaddr_intptr != IntPtr.Zero)
                    {
                        var ipaddr = Marshal.PtrToStructure<Interop.IP_ADAPTER_ADDRESSES>(ipaddr_intptr);

                        if (ipaddr.IfType != Interop.IF_TYPE.IF_TYPE_SOFTWARE_LOOPBACK &&
                            ipaddr.OperStatus == Interop.IF_OPER_STATUS.IfOperStatusUp)
                        {
                            IntPtr prefix_intptr = ipaddr.FirstPrefix;
                            int number = 0;
                            while (prefix_intptr != IntPtr.Zero)
                            {
                                var prefix = Marshal.PtrToStructure<Interop.IP_ADAPTER_PREFIX_XP>(prefix_intptr);

                                if (number == 2)   // because the 3rd item is the broadcast address
                                {
                                    IntPtr prefixipaddr_intptr = prefix.Address_Ptr;
                                    if (prefixipaddr_intptr != IntPtr.Zero)
                                    {
                                        var prefixipaddr = Marshal.PtrToStructure<Interop.SOCKADDR_IN>(prefixipaddr_intptr);
                                        result.Add(new IPAddress(prefixipaddr.sin_addr));
                                        /* this is the broadcast address */
                                    }
                                    break;
                                }
                                prefix_intptr = prefix.Next;
                                number += 1;
                            }
                        }
                        ipaddr_intptr = ipaddr.Next;
                    }
                    return result.Count > 0 ? result : null;
                }
                finally
                {
                    Marshal.FreeHGlobal(root);
                }
            }

            var broadcast_ips = EnumerateBroadcastIPs();
            if (broadcast_ips == null)
                broadcast_ips = new List<IPAddress> { IPAddress.Broadcast };

            EndPoint LookForQuestOnLocalNetwork()
            {
                const int RETRIES = 3;

                var udpsock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                udpsock.EnableBroadcast = true;
                udpsock.Bind(new IPEndPoint(IPAddress.Any, 0));
                
                var request = new byte[] { 0x7b, 0xfc, 0xb2, 0xeb, 0x1e, 0x6e, 0xbb, 0x53 };
                var answer = new byte[] { 0x6a, 0x68, 0x6c, 0xcc, 0x5e, 0xbd, 0xaa, 0xd6 };
                var actual_answer = new byte[512];
                var lst = new List<Socket>();
                for (int i = 0; i < RETRIES * 3; i++)
                {
                    if ((i % 3) == 0)
                    {
                        foreach (var ip in broadcast_ips)
                            udpsock.SendTo(request, SocketFlags.None, new IPEndPoint(ip, PORT));
                    }

                    Thread.Sleep(100 + 10 * i);

                    while (true)
                    {
                        lst.Clear();
                        lst.Add(udpsock);
                        Socket.Select(lst, null, null, 0);
                        if (lst.Count == 0)
                            break;   /* no more pending message */

                        EndPoint remote_endpoint = new IPEndPoint(IPAddress.Any, 0);
                        int count = udpsock.ReceiveFrom(actual_answer, 0, actual_answer.Length, SocketFlags.None, ref remote_endpoint);
                        if (count == 0)
                            break;
                        if (count < 14 || !answer.SequenceEqual(actual_answer.Take(8)))
                            continue;
                        var remote_id = new string(actual_answer.Skip(8).Take(6).Select(n => (char)n).ToArray());
                        if (remote_id == quest_id)
                            return remote_endpoint;
                    }
                }
                return null;
            }

            var endpoint = LookForQuestOnLocalNetwork();
            if (endpoint != null && TryConnect(endpoint, out var sock))
            {
                con.SetDirectSocket(sock);
                return;
            }

            /* not found on the local network, try via baroquesoftware */
            //if (METASERVER_BLIND_TRUST)
            //    ServicePointManager.ServerCertificateValidationCallback += (o, c, ch, er) => true;
            var ws = new RetryWebSocket();

            string error = null;
            int error_count = 0;
            var signal_ready = VRSketchCommand.MakeSignalFromAnyThread(() =>
            {
                if (error == null)
                {
                    after_opened?.Invoke();
                    after_opened = null;
                }
                else
                {
                    error_count += 1;
                    VRSketchCommand._WriteLog($"signal_ready with error #{error_count}: {error}\n");
                    ws.Close();
                    con.Close();
                    if (error_count == 1)   /* only show the first error to the user */
                        MessageBox.Show(error, "VR Sketch", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            });

            ws.OnOpen += (object sender, EventArgs e) =>
            {
                VRSketchCommand._WriteLog($"WebSocket connected\n");
            };

            ws.OnClose += (object sender, CloseEventArgs e) =>
            {
                VRSketchCommand._WriteLog($"WebSocket closed\n");
            };

            ws.OnError += (object sender, ErrorEventArgs e) =>
            {
                VRSketchCommand._WriteLog($"WebSocket error: {e.Message}\n");
                //VRSketchCommand._WriteLog("now sleeping 7000ms...\n");
                //Thread.Sleep(7000);
                //VRSketchCommand._WriteLog("done sleeping 7000ms\n");
                error = e.Message;
                signal_ready.Raise();
            };

            ws.OnMessage += (object sender, MessageEventArgs e) =>
            {
                switch (e.Type)
                {
                    case Opcode.Text:
                        VRSketchCommand._WriteLog($"incoming websocket string message: {e.Data}\n");
                        var msg = JsonConvert.DeserializeObject<TextMessage>(e.Data);
                        switch (msg.cmd)
                        {
                            case "ready":
                                signal_ready.Raise();
                                break;

                            case "error":
                                error = msg.msg;
                                signal_ready.Raise();
                                break;
                        }
                        break;

                    case Opcode.Binary:
                        con.PushIncomingBinaryData(e.RawData);
                        break;
                }
            };

            string process_id = VRSketchApp.GetRandomProcessUid();
            ws.Connect($"{METASERVER}?pid={process_id}&usid={quest_id}");
            con.SetWebSocket(ws);
        }

        [Serializable]
        public class TextMessage
        {
            public string cmd = "?";
            public string msg = "?";
            public int v;
        }
    }
}
