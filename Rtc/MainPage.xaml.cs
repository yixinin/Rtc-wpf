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

        public List<SendCadidate> Candidates { get; set; }

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
            Candidates = new List<SendCadidate>();
            //var test = Http.GetAsync("Test", "").Result;
            //Debug.WriteLine(test);
        }



        public async Task CreateReceiver(MediaStream mediaStream, long fromUid)
        {
            List<RTCIceServer> iceservers = new List<RTCIceServer>()
              {
                    new RTCIceServer {Url=stun},
               }; //不一定是这么多个

            RTCConfiguration configuration = new RTCConfiguration() { BundlePolicy = RTCBundlePolicy.Balanced, IceServers = iceservers, IceTransportPolicy = RTCIceTransportPolicy.All };

            var conn = new RTCPeerConnection(configuration);
            CurrentRoom.Recvs.Add(fromUid, conn);
            CurrentRoom.Recvs[fromUid].AddStream(mediaStream);
            CurrentRoom.Recvs[fromUid].OnIceCandidate += async (p) =>
            {
                var Candidate = p.Candidate;
                var candidate = JsonConvert.SerializeObject(Candidate);
                var m = new SendCadidate();
                m.candidate = candidate;
                m.uid = Uid;
                m.fromUid = fromUid;
                Candidates.Add(m);

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

            await CreatOffer(Uid, fromUid);

        }

        async private Task CreatePublisher(MediaStream mediaStream)
        {
            List<RTCIceServer> iceservers = new List<RTCIceServer>()
              {
                //"stun:stun.ideasip.com"
                    new RTCIceServer {Url=stun },
               };

            RTCConfiguration configuration = new RTCConfiguration() { BundlePolicy = RTCBundlePolicy.Balanced, IceServers = iceservers, IceTransportPolicy = RTCIceTransportPolicy.All };
            CurrentRoom.Pub = new RTCPeerConnection(configuration);
            CurrentRoom.Pub.AddStream(mediaStream);
            CurrentRoom.Pub.OnIceCandidate += Conn_OnIceCandidateAsync;
            CurrentRoom.Pub.OnAddStream += Conn_OnAddStream;
            await CreatOffer(Uid, 0);
        }


        public async Task CaptureMedia(long fromUid)
        {
            LocalMedia = Media.CreateMedia();//创建一个Media对象

            RTCMediaStreamConstraints mediaStreamConstraints = new RTCMediaStreamConstraints() //设置要获取的流 
            {
                audioEnabled = false,
                videoEnabled = false
            };

            var apd = LocalMedia.GetAudioPlayoutDevices();
            if (apd.Count > 0)
            {
                LocalMedia.SelectAudioPlayoutDevice(apd[0]);
            }

            if (fromUid == 0)
            {
                var acd = LocalMedia.GetAudioCaptureDevices();
                var vcd = LocalMedia.GetVideoCaptureDevices();
                if (acd.Count > 0)
                {
                    mediaStreamConstraints.audioEnabled = true;
                    LocalMedia.SelectAudioCaptureDevice(acd[0]);
                }

                if (vcd.Count > 0)
                {
                    mediaStreamConstraints.videoEnabled = true;
                    LocalMedia.SelectVideoDevice(vcd.First(p => p.Location.Panel == Windows.Devices.Enumeration.Panel.Front));//设置视频捕获设备
                    
                }
            }



            var mediaStream = await LocalMedia.GetUserMedia(mediaStreamConstraints);//获取视频流 这里视频和音频是一起传输的
            var videotracs = mediaStream.GetVideoTracks();
            //var audiotracs = mediaStream.GetAudioTracks();
            if (videotracs.Count > 0)
            {
                var source = LocalMedia.CreateMediaSource(videotracs.FirstOrDefault(), mediaStream.Id);//创建播放源
                LocalMediaPlayer.SetMediaStreamSource(source); //设置MediaElement的播放源
                LocalMediaPlayer.Play();
            }
            if (fromUid == 0)
            {
                await CreatePublisher(mediaStream);
            }
            else
            {
                await CreateReceiver(mediaStream, fromUid);
            }

        }

       
        public async Task CreatOffer(long uid, long fromUid) //此时是发起方的操作
        {
            RTCSessionDescription offer;
            if (fromUid == 0)
            {
                offer = await CurrentRoom.Pub.CreateOffer();
                await CurrentRoom.Pub.SetLocalDescription(offer);
            }
            else
            {
                offer = await CurrentRoom.Recvs[fromUid].CreateOffer();
                await CurrentRoom.Recvs[fromUid].SetLocalDescription(offer);
            }


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
                if (fromUid == 0)
                {
                    await CurrentRoom.Pub.SetRemoteDescription(answer);
                }
                else
                {
                    await CurrentRoom.Recvs[fromUid].SetRemoteDescription(answer);
                }

            }

        }

        public async Task<string> SendOffer(GetAnswerModel m)
        {

            var answer = await Http.PostAsnyc(m, "getAnswer");
            foreach (var c in Candidates)
            {
                await Send_Candidate(c);
            }
            return answer;
        }

        private void Conn_OnAddStream(MediaStreamEvent __param0)
        {
            var stream = __param0.Stream;

            var videotracks = stream.GetVideoTracks();
            var media = Media.CreateMedia();

            var apd = media.GetAudioPlayoutDevices();
            if (apd.Count > 0)
            {
                media.SelectAudioPlayoutDevice(apd[0]);
            }
           
            var source = media.CreateMediaSource(videotracks.FirstOrDefault(), stream.Id);
       
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
            Candidates.Add(m);
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
                var candidates = JsonConvert.DeserializeObject<List<string>>(candiate);
                if (fromUidTb.Text == "")
                {
                    foreach (var c in candidates)
                    {
                        await CurrentRoom.Pub.AddIceCandidate(JsonConvert.DeserializeObject<RTCIceCandidate>(c));
                    }

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
                    foreach (var c in candidates)
                    {
                        await CurrentRoom.Recvs[fromUid].AddIceCandidate(JsonConvert.DeserializeObject<RTCIceCandidate>(c));
                    }

                }
            }

        }

        public async void JoinBtn_Click(object sender, RoutedEventArgs e)
        {
            long.TryParse(fromUidTb.Text, out var fromUid);
            await CaptureMedia(fromUid);
        }

        public async void ConnectBtn_Click(object sender, RoutedEventArgs e)
        {
            await CaptureMedia(0);
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
