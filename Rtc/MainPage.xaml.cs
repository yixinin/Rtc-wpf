using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using webrtc_winrt_api;
using System.Threading.Tasks;
using Windows.Web.Http;
using System.Diagnostics;
using Newtonsoft.Json;

// https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x804 上介绍了“空白页”项模板

namespace Rtc
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MainPage : Page
    {


        const string stun = "stun:stun.voipgate.com:3478";
        public Media LocalMedia { get; set; }
        public Room CurrentRoom { get; set; }

        public long Uid { get; set; }
        public MainPage()
        {
            this.InitializeComponent();
            WebRTC.Initialize(this.Dispatcher);
            CurrentRoom = new Room
            {
                Id = 10001,
                Uid = Uid,
                Recvs = new Dictionary<long, RTCPeerConnection>()
            };
            Random R = new Random();
            Uid = R.Next(1000, 10000);

            //var test = Http.GetAsync("Test", "").Result;
            //Debug.WriteLine(test);
        }



        public async void CreateReceiver(long fromUid)
        {
            List<RTCIceServer> iceservers = new List<RTCIceServer>()
              {
                    new RTCIceServer {Url= stun},
               }; //不一定是这么多个

            RTCConfiguration configuration = new RTCConfiguration() { BundlePolicy = RTCBundlePolicy.Balanced, IceServers = iceservers, IceTransportPolicy = RTCIceTransportPolicy.All };

            CurrentRoom.Recvs.Add(fromUid, new RTCPeerConnection(configuration));

            await CreatOffer(Uid, fromUid);

            CurrentRoom.Recvs[fromUid].OnIceCandidate += async (p) =>
            {
                var Candidate = p.Candidate;
                var candidate = JsonConvert.SerializeObject(Candidate);
                var m = new SendCadidate();
                m.candidate = candidate;
                m.uid = Uid;
                m.fromUid = fromUid;
                await Send_Candidate(m);
            };
            CurrentRoom.Recvs[fromUid].OnAddStream += (p) =>
            {
                var stream = p.Stream;
                var videotracks = stream.GetVideoTracks();
                var source = LocalMedia.CreateMediaSource(videotracks.FirstOrDefault(), stream.Id);
                RemoteMediaPlayer.SetMediaStreamSource(source);
                RemoteMediaPlayer.Play();
            };


        }


        public async Task CaptureMedia()
        {
            LocalMedia = Media.CreateMedia();//创建一个Media对象

            RTCMediaStreamConstraints mediaStreamConstraints = new RTCMediaStreamConstraints() //设置要获取的流 
            {
                audioEnabled = true,
                videoEnabled = true
            };

            var apd = LocalMedia.GetAudioPlayoutDevices();
            var acd = LocalMedia.GetAudioCaptureDevices();
            var vcd = LocalMedia.GetVideoCaptureDevices();
            if (acd.Count > 0)
            {
                LocalMedia.SelectAudioCaptureDevice(acd[0]);
            }
            if (apd.Count > 0)
            {
                LocalMedia.SelectAudioPlayoutDevice(apd[0]);
            }
            if (vcd.Count > 0)
            {
                LocalMedia.SelectVideoDevice(vcd.First(p => p.Location.Panel == Windows.Devices.Enumeration.Panel.Front));//设置视频捕获设备
            }


            var mediaStream = await LocalMedia.GetUserMedia(mediaStreamConstraints);//获取视频流 这里视频和音频是一起传输的
            var videotracs = mediaStream.GetVideoTracks();
            var audiotracs = mediaStream.GetAudioTracks();
            if (videotracs.Count > 0)
            {
                var source = LocalMedia.CreateMediaSource(videotracs.FirstOrDefault(), mediaStream.Id);//创建播放源
                LocalMediaPlayer.SetMediaStreamSource(source); //设置MediaElement的播放源
            }

            await CreatePublisher(mediaStream);
        }

        async private Task CreatePublisher(MediaStream mediaStream)
        {
            List<RTCIceServer> iceservers = new List<RTCIceServer>()
              {
                    new RTCIceServer {Url="stun:stun.ideasip.com" },
               }; //不一定是这么多个

            RTCConfiguration configuration = new RTCConfiguration() { BundlePolicy = RTCBundlePolicy.Balanced, IceServers = iceservers, IceTransportPolicy = RTCIceTransportPolicy.All };
            CurrentRoom.Pub = new RTCPeerConnection(configuration);
            CurrentRoom.Pub.AddStream(mediaStream);


            await CreatOffer(Uid, 0);

            CurrentRoom.Pub.OnIceCandidate += Conn_OnIceCandidateAsync;
            CurrentRoom.Pub.OnAddStream += Conn_OnAddStream;
        }

        public async Task CreatOffer(long uid, long fromUid) //此时是发起方的操作
        {
            var offer = await CurrentRoom.Pub.CreateOffer();
            await CurrentRoom.Pub.SetLocalDescription(offer);

            var m = new GetAnswerModel();
            m.offer = offer.Sdp;
            m.uid = uid;
            m.fromUid = fromUid;
            var answerSdp = await SendOffer(m);
            if (answerSdp != "")
            {
                var answer = new RTCSessionDescription();
                answer.Type = RTCSdpType.Answer;
                answer.Sdp = answerSdp;
                await CurrentRoom.Pub.SetRemoteDescription(answer);
            }


        }

        public async Task<string> SendOffer(GetAnswerModel m)
        {
            return await Http.PostAsnyc(m, "getAnswer");
        }

        private void Conn_OnAddStream(MediaStreamEvent __param0)
        {
            var stream = __param0.Stream;
            var videotracks = stream.GetVideoTracks();
            var source = LocalMedia.CreateMediaSource(videotracks.FirstOrDefault(), stream.Id);
            RemoteMediaPlayer.SetMediaStreamSource(source);
            RemoteMediaPlayer.Play();
        }

        private async void Conn_OnIceCandidateAsync(RTCPeerConnectionIceEvent __param0)
        {
            var Candidate = __param0.Candidate;
            var candidate = JsonConvert.SerializeObject(Candidate);
            var m = new SendCadidate();
            m.candidate = candidate;
            m.uid = Uid;
            await Send_Candidate(m);
        }

        public async Task<string> Send_Candidate(SendCadidate m)
        {
            return await Http.PostAsnyc(m, "sendCandidate");
        }

        public async Task<string> GetCandiate(GetCandidateModel m)
        {
            return await Http.PostAsnyc(m, "getCandidate");
        }

        public async void CandiateBtn_Click(object sender, RoutedEventArgs e)
        {
            var m = new GetCandidateModel();
            m.uid = Uid;
            long.TryParse(fromUidTb.Text, out var fromUid);
            m.fromUid = fromUid;

            var candiate = await GetCandiate(m);
            if (candiate != "")
            {
                if (fromUidTb.Text == "")
                {
                    if (CurrentRoom.Pub.IceConnectionState == RTCIceConnectionState.Completed)
                    {

                        Debug.WriteLine("重复添加candidate");
                        return;
                    }
                    await CurrentRoom.Pub.AddIceCandidate(JsonConvert.DeserializeObject<RTCIceCandidate>(candiate));
                }
                else
                {
                    if (fromUid == 0)
                    {
                        return;
                    }
                    if (!CurrentRoom.Recvs.ContainsKey(fromUid))
                    {
                        return;
                    }
                    if (CurrentRoom.Recvs[fromUid].IceConnectionState == RTCIceConnectionState.Completed)
                    {
                        Debug.WriteLine("重复添加candidate fromUid=" + fromUid.ToString());
                        return;
                    }
                    await CurrentRoom.Recvs[fromUid].AddIceCandidate(JsonConvert.DeserializeObject<RTCIceCandidate>(candiate));
                }
            }

        }

        public void JoinBtn_Click(object sender, RoutedEventArgs e)
        {
            long.TryParse(fromUidTb.Text, out var fromUid);
            CreateReceiver(fromUid);
        }

        public async void ConnectBtn_Click(object sender, RoutedEventArgs e)
        {
            await CaptureMedia();
        }
    }

    public class GetAnswerModel
    {
        public string offer { get; set; }
        public long uid { get; set; }
        public long fromUid { get; set; }
        public int roomId { get; set; }
    }

    public class SendCadidate
    {
        public int roomId { get; set; }
        public long uid { get; set; }
        public long fromUid { get; set; }
        public string candidate { get; set; }
    }

    public class GetCandidateModel
    {

        public int roomId { get; set; }
        public long uid { get; set; }
        public long fromUid { get; set; }
    }
}
