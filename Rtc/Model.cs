using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rtc
{
    public class GetAnswerModel
    {
        public string offer { get; set; }
        public long uid { get; set; }
        public long fromUid { get; set; }
        public int roomId { get; set; }
    }

    public class SendCadidatModel
    {
        public int roomId { get; set; }
        public long uid { get; set; }
        public long fromUid { get; set; }
        public CandidateModel candidate { get; set; }
    }

    public class CandidateModel
    {
        public string candidate { get; set; }
        public string sdpMid { get; set; }
        public ushort sdpMlineindex { get; set; }
    }


    public class GetCandidateModel
    {

        public int roomId { get; set; }
        public long uid { get; set; }
        public long fromUid { get; set; }
    }
}
