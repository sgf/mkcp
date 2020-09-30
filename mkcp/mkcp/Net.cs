using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace mkcp {
    public class Net {

        //To get local Ip Address:
        public static string GetLocalIPAddress() {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList) {
                if (ip.AddressFamily == AddressFamily.InterNetwork) {
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }

        //        To check if you're connected or not:

        //System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable();


        //        string localIP;
        //using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
        //{
        //    socket.Connect("8.8.8.8", 65530);
        //    IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
        //    localIP = endPoint.Address.ToString();
        //}


        private static string LocalIPAddress() {
            if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable()) {
                return null;
            }

            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList) {
                if (ip.AddressFamily == AddressFamily.InterNetwork) {
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }

        /// <summary>
        /// 现在获取以太网网络接口呼叫的 IPv4 地址： GetLocalIPv4(NetworkInterfaceType.Ethernet);
        /// 或您的无线接口： GetLocalIPv4(NetworkInterfaceType.Wireless80211);
        /// </summary>
        /// <param name="_type"></param>
        /// <returns></returns>
        public string GetLocalIPv4(NetworkInterfaceType _type) {
            string output = "";
            foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces()) {
                if (item.NetworkInterfaceType == _type && item.OperationalStatus == OperationalStatus.Up) {
                    foreach (UnicastIPAddressInformation ip in item.GetIPProperties().UnicastAddresses) {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork) {
                            output = ip.Address.ToString();
                        }
                    }
                }
            }
            return output;
        }

        /// <summary>
        /// 上面的函数改进型（可以返回一个网卡多个地址的情况）
        /// </summary>
        /// <param name="_type"></param>
        /// <returns></returns>
        public static string[] GetAllLocalIPv4(NetworkInterfaceType _type) {
            List<string> ipAddrList = new List<string>();
            foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces()) {
                if (item.NetworkInterfaceType == _type && item.OperationalStatus == OperationalStatus.Up) {
                    foreach (UnicastIPAddressInformation ip in item.GetIPProperties().UnicastAddresses) {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork) {
                            ipAddrList.Add(ip.Address.ToString());
                        }
                    }
                }
            }
            return ipAddrList.ToArray();
        }

        /// <summary>
        /// 另一个回答者说 从上面函数获取的改进型
        /// </summary>
        /// <param name="_type"></param>
        /// <returns></returns>
        internal static string GetLocalIPv4_(NetworkInterfaceType _type) {  // Checks your IP adress from the local network connected to a gateway. This to avoid issues with double network cards
            string output = "";  // default output
            foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces()) // Iterate over each network interface
            {  // Find the network interface which has been provided in the arguments, break the loop if found
                if (item.NetworkInterfaceType == _type && item.OperationalStatus == OperationalStatus.Up) {   // Fetch the properties of this adapter
                    IPInterfaceProperties adapterProperties = item.GetIPProperties();
                    // Check if the gateway adress exist, if not its most likley a virtual network or smth
                    if (adapterProperties.GatewayAddresses.FirstOrDefault() != null) {   // Iterate over each available unicast adresses
                        foreach (UnicastIPAddressInformation ip in adapterProperties.UnicastAddresses) {   // If the IP is a local IPv4 adress
                            if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) {   // we got a match!
                                output = ip.Address.ToString();
                                break;  // break the loop!!
                            }
                        }
                    }
                }
                // Check if we got a result if so break this method
                if (output != "") { break; }
            }
            // Return results
            return output;
        }

        /// <summary>
        /// 这是我找到的最佳代码，以获得当前的 IP，避免获取 VMWare 主机或其他无效的 IP 地址。
        /// 其他方法不使用 IsDnsE 资格和前缀原始验证
        /// 如果地址不符合 DNS 条件，则它是保留的内部 IP。它不是互联网提供商主机。如果前缀Origin由DHCP服务器支持，这可能是最好的地址选择。这是对我有效的独特功能！
        /// </summary>
        /// <returns></returns>
        public string GetLocalIpAddress() {
            UnicastIPAddressInformation mostSuitableIp = null;

            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();

            foreach (var network in networkInterfaces) {
                if (network.OperationalStatus != OperationalStatus.Up)
                    continue;

                var properties = network.GetIPProperties();

                if (properties.GatewayAddresses.Count == 0)
                    continue;

                foreach (var address in properties.UnicastAddresses) {
                    if (address.Address.AddressFamily != AddressFamily.InterNetwork)
                        continue;

                    if (IPAddress.IsLoopback(address.Address))
                        continue;

                    if (!address.IsDnsEligible) {
                        if (mostSuitableIp == null)
                            mostSuitableIp = address;
                        continue;
                    }

                    // The best IP is the IP got from DHCP server
                    if (address.PrefixOrigin != PrefixOrigin.Dhcp) {
                        if (mostSuitableIp == null || !mostSuitableIp.IsDnsEligible)
                            mostSuitableIp = address;
                        continue;
                    }

                    return address.Address.ToString();
                }
            }

            return mostSuitableIp != null
                ? mostSuitableIp.Address.ToString()
                : "";
        }

        /// <summary>
        /// https://stackoverflow.com/questions/7232287/check-if-ip-is-in-lan-behind-firewalls-and-routers#
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>

        private static bool IsLanIP(IPAddress address) {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var iface in interfaces) {
                var properties = iface.GetIPProperties();
                foreach (var ifAddr in properties.UnicastAddresses) {
                    if (ifAddr.IPv4Mask != null &&
                        ifAddr.Address.AddressFamily == AddressFamily.InterNetwork &&
                        CheckMask(ifAddr.Address, ifAddr.IPv4Mask, address))
                        return true;
                }
            }
            return false;
        }

        private static bool CheckMask(IPAddress address, IPAddress mask, IPAddress target) {
            if (mask == null)
                return false;

            var ba = address.GetAddressBytes();
            var bm = mask.GetAddressBytes();
            var bb = target.GetAddressBytes();

            if (ba.Length != bm.Length || bm.Length != bb.Length)
                return false;

            for (var i = 0; i < ba.Length; i++) {
                int m = bm[i];

                int a = ba[i] & m;
                int b = bb[i] & m;

                if (a != b)
                    return false;
            }

            return true;
        }


        //https://stackoverflow.com/questions/9855230/how-do-i-get-the-network-interface-and-its-right-ipv4-address?noredirect=1&lq=1
        //foreach(NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
        //{
        //   if(ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
        //   {
        //       Console.WriteLine(ni.Name);
        //       foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
        //       {
        //           if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        //           {
        //               Console.WriteLine(ip.Address.ToString());
        //           }
        //       }
        //   }
        //}

        //https://stackoverflow.com/questions/8113546/how-to-determine-whether-an-ip-address-in-private
        //private bool _IsPrivate(string ipAddress) {
        //    int[] ipParts = ipAddress.Split(new String[] { "." }, StringSplitOptions.RemoveEmptyEntries)
        //                             .Select(s => int.Parse(s)).ToArray();
        //    // in private ip range
        //    if (ipParts[0] == 10 ||
        //        (ipParts[0] == 192 && ipParts[1] == 168) ||
        //        (ipParts[0] == 172 && (ipParts[1] >= 16 && ipParts[1] <= 31))) {
        //        return true;
        //    }

        //    // IP Address is probably public.
        //    // This doesn't catch some VPN ranges like OpenVPN and Hamachi.
        //    return false;
        //}
    }
}
