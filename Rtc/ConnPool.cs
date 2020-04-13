using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using webrtc_winrt_api;

namespace Rtc
{
    public class Room
    {

        public int Id { get; set; }
        public long Uid { get; set; }

        public RTCPeerConnection Pub { get; set; }
       
        public Dictionary<long, RTCPeerConnection> Recvs { get; set; }
        
    }
}
