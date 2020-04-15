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
using Org.WebRtc;
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

        public RTCConfiguration RtcConfig { get; set; }
        public Media LocalMedia { get; set; }
        public Room CurrentRoom { get; set; }

        public List<SendCadidatModel> Candidates { get; set; }

        public long Uid { get; set; }
        public long FromUid { get; set; }
        public MainPage()
        {
            this.InitializeComponent();
            WebRTC.Initialize(this.Dispatcher);

            Random R = new Random();
            Uid = R.Next(1000, 10000);
            uidTbk.Text = "Uid: " + Uid.ToString();
            CurrentRoom = new Room
            {
                Id = 10001,
                Uid = Uid,
                Recvs = new Dictionary<long, RTCPeerConnection>()
            };

            Candidates = new List<SendCadidatModel>();
            var iceServers = new List<RTCIceServer>()
            {
                new RTCIceServer{Url="stun:stun.ideasip.com"},
                new RTCIceServer{Url="stun:stun.voipgate.com:3478"}

            };
            RtcConfig = new RTCConfiguration()
            {
                BundlePolicy = RTCBundlePolicy.Balanced,
                IceServers = iceServers,
                IceTransportPolicy = RTCIceTransportPolicy.All,
            };

            //var test = Http.GetAsync("Test", "").Result;
            //Debug.WriteLine(test);
        }

        public async Task CaptureMedia(long fromUid)
        {
            LocalMedia = Media.CreateMedia();//创建一个Media对象

            RTCMediaStreamConstraints mediaStreamConstraints = new RTCMediaStreamConstraints() //设置要获取的流 
            {
                audioEnabled = false,
                videoEnabled = false
            };

            //音频播放
            var apd = LocalMedia.GetAudioPlayoutDevices();
            if (apd.Count > 0)
            {
                LocalMedia.SelectAudioPlayoutDevice(apd[0]);
            }

            if (fromUid == 0)
            {
                //音频捕获
                var acd = LocalMedia.GetAudioCaptureDevices();
                if (acd.Count > 0)
                {
                    mediaStreamConstraints.audioEnabled = true;
                    LocalMedia.SelectAudioCaptureDevice(acd[0]);
                }

                //视频捕获
                var vcd = LocalMedia.GetVideoCaptureDevices();
                if (vcd.Count > 0)
                {
                    mediaStreamConstraints.videoEnabled = true;
                    LocalMedia.SelectVideoDevice(vcd.First(p => p.Location.Panel == Windows.Devices.Enumeration.Panel.Front));//设置视频捕获设备

                }
            }



            var mediaStream = await LocalMedia.GetUserMedia(mediaStreamConstraints);//获取视频流 这里视频和音频是一起传输的

            if (fromUid == 0)
            {
                var videotracs = mediaStream.GetVideoTracks();
                //var audiotracs = mediaStream.GetAudioTracks();
                if (videotracs.Count > 0)
                {
                    var source = LocalMedia.CreateMediaSource(videotracs.FirstOrDefault(), mediaStream.Id);//创建播放源
                    LocalMediaPlayer.SetMediaStreamSource(source); //设置MediaElement的播放源
                    LocalMediaPlayer.Play();
                }
                //await CreatePublisher(mediaStream);
               await CreateServer(mediaStream);
            }
            else
            {
                CreateClient(mediaStream);
                //await CreateReceiver(mediaStream, fromUid);
            }

        }



        public async Task CreateReceiver(MediaStream mediaStream, long fromUid)
        {


            var conn = new RTCPeerConnection(RtcConfig);

            CurrentRoom.Recvs.Add(fromUid, conn);
            CurrentRoom.Recvs[fromUid].AddStream(mediaStream);
            CurrentRoom.Recvs[fromUid].OnIceCandidate += async (p) =>
            {
                var Candidate = p.Candidate;
                var m = new SendCadidatModel();
                m.candidate = new CandidateModel
                {
                    candidate = Candidate.Candidate,
                    sdpMlineindex = Candidate.SdpMLineIndex,
                    sdpMid = Candidate.SdpMid,
                };
                m.uid = Uid;
                m.fromUid = fromUid;
                Candidates.Add(m);
                await SendCandidate(m);

            };
            CurrentRoom.Recvs[fromUid].OnAddStream += (p) =>
            {
                var stream = p.Stream;

                var videotracks = stream.GetVideoTracks();
                //var media = Media.CreateMedia();


                //var apd = media.GetAudioPlayoutDevices();
                //if (apd.Count > 0)
                //{
                //    media.SelectAudioPlayoutDevice(apd[0]);
                //}

                var source = LocalMedia.CreateMediaSource(videotracks.FirstOrDefault(), stream.Id);

                RemoteMediaPlayer.SetMediaStreamSource(source);

                RemoteMediaPlayer.Play();
            };

            await CreatOffer(Uid, fromUid);

        }

        async private Task CreatePublisher(MediaStream mediaStream)
        {
            CurrentRoom.Pub = new RTCPeerConnection(RtcConfig);
            CurrentRoom.Pub.AddStream(mediaStream);
            CurrentRoom.Pub.OnIceCandidate += Conn_OnIceCandidateAsync;
            CurrentRoom.Pub.OnAddStream += Conn_OnAddStream;
            await CreatOffer(Uid, 0);
        }

        public async Task CreateServer(MediaStream mediaStream)
        {
            CurrentRoom.Pub = new RTCPeerConnection(RtcConfig);
            CurrentRoom.Pub.AddStream(mediaStream);
            CurrentRoom.Pub.OnIceCandidate += Conn_OnIceCandidateAsync;
            CurrentRoom.Pub.OnAddStream += Conn_OnAddStream;

            var offer = await CurrentRoom.Pub.CreateOffer();
            await CurrentRoom.Pub.SetLocalDescription(offer);
            await SendSdp(offer.Sdp, "offer");
        }

        public void CreateClient(MediaStream mediaStream)
        {
            CurrentRoom.Pub = new RTCPeerConnection(RtcConfig);
            CurrentRoom.Pub.AddStream(mediaStream);
            CurrentRoom.Pub.OnIceCandidate += Conn_OnIceCandidateAsync;
            CurrentRoom.Pub.OnAddStream += Conn_OnAddStream;
            long.TryParse(fromUidTb.Text, out var fromUid);
            if (fromUid != 0)
            {
                PollSdp("offer");
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
                await SendCandidate(c);
            }
            return answer;
        }

        private void Conn_OnAddStream(MediaStreamEvent __param0)
        {
            var stream = __param0.Stream;

            var videotracks = stream.GetVideoTracks();
            //var media = Media.CreateMedia();

            //var apd = media.GetAudioPlayoutDevices();
            //if (apd.Count > 0)
            //{
            //    media.SelectAudioPlayoutDevice(apd[0]);
            //}

            var source = LocalMedia.CreateMediaSource(videotracks.FirstOrDefault(), stream.Id);

            RemoteMediaPlayer.SetMediaStreamSource(source);

            RemoteMediaPlayer.Play();
        }

        private async void Conn_OnIceCandidateAsync(RTCPeerConnectionIceEvent __param0)
        {
            var Candidate = __param0.Candidate;
            var m = new SendCadidatModel();
            m.candidate = new CandidateModel
            {
                candidate = Candidate.Candidate,
                sdpMid = Candidate.SdpMid,
                sdpMlineindex = Candidate.SdpMLineIndex
            };
            m.uid = Uid;
            Candidates.Add(m);
            //await SendCandidate(m);
            await SendCand(Candidate);
        }

        public async Task<string> SendCandidate(SendCadidatModel m)
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
                var candidates = JsonConvert.DeserializeObject<List<CandidateModel>>(candiate);
                if (fromUidTb.Text == "")
                {
                    foreach (var c in candidates)
                    {
                        await CurrentRoom.Pub.AddIceCandidate(new RTCIceCandidate
                        {
                            SdpMid = c.sdpMid,
                            Candidate = c.candidate,
                            SdpMLineIndex = (ushort)c.sdpMlineindex,
                        });
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
                        await CurrentRoom.Recvs[fromUid].AddIceCandidate(new RTCIceCandidate
                        {
                            SdpMid = c.sdpMid,
                            Candidate = c.candidate,
                            SdpMLineIndex = (ushort)c.sdpMlineindex,
                        });
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

        public void PollSdp(string sdpType)
        {
            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += async (o, e) =>
            {
                if (FromUid == 0)
                {
                    long.TryParse(fromUidTb.Text, out var fromUid);
                    if (fromUid == 0)
                    {
                        return;
                    }
                    FromUid = fromUid;
                }
                var m = new PollSdpModel
                {
                    sdpType = sdpType,
                    fromUid = FromUid,
                };
                var sdp = await Http.PostAsnyc(m, "pollSdp");
                if (sdp != "")
                {
                    await CurrentRoom.Pub.SetRemoteDescription(new RTCSessionDescription
                    {
                        Sdp = sdp,
                        Type = sdpType == "offer" ? RTCSdpType.Offer : RTCSdpType.Answer,
                    });

                    if (sdpType == "offer")
                    {
                        var answer = await CurrentRoom.Pub.CreateAnswer();
                        await CurrentRoom.Pub.SetLocalDescription(answer);
                        await SendSdp(answer.Sdp, "answer");
                    }

                    timer.Stop();
                }
            };
            timer.Start();

        }

        public void PollCandidate()
        {
            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += async (o, e) =>
            {
                if (FromUid == 0)
                {
                    long.TryParse(fromUidTb.Text, out var fromUid);
                    if (fromUid == 0)
                    {
                        return;
                    }
                    FromUid = fromUid;
                }
                var m = new PollCandModel
                {
                    fromUid = FromUid,
                };
                var cand = await Http.PostAsnyc(m, "pollCand");

                if (cand != "")
                {
                    var candidate = JsonConvert.DeserializeObject<CandidateModel>(cand);
                    await CurrentRoom.Pub.AddIceCandidate(new RTCIceCandidate
                    {
                        Candidate = candidate.candidate,
                        SdpMid = candidate.sdpMid,
                        SdpMLineIndex = candidate.sdpMlineindex,
                    });
                    if (CurrentRoom.Pub.IceConnectionState == RTCIceConnectionState.Connected)
                    {
                        timer.Stop();
                    }
                }
            };
            timer.Start();
        }

        public async Task SendSdp(string sdp, string sdpType)
        {
            var m = new SendSdpModel
            {
                sdp = new SdpModel { sdp = sdp, sdpType = sdpType },
                uid = Uid,
            };
            await Http.PostAsnyc(m, "sendSdp");
        }

        public async Task SendCand(RTCIceCandidate cand)
        {
            var m = new SendCadidatModel
            {
                candidate = new CandidateModel
                {
                    candidate = cand.Candidate,
                    sdpMid = cand.SdpMid,
                    sdpMlineindex = cand.SdpMLineIndex,
                },

                uid = Uid,
            };
            await Http.PostAsnyc(m, "sendCand");
        }
    }

}
