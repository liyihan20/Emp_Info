using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EmpInfo.Models
{
    public class AccessTokenModel
    {
        public string access_token { get; set; }
        public int expires_in { get; set; }
    }

    public class JsApiTicketModel
    {
        public int errcode { get; set; }
        public string errmsg { get; set; }
        public string ticket { get; set; }
        public int expires_in { get; set; }
    }

    public class WxConfigParam
    {
        public string appId { get; set; }
        public long timestamp { get; set; }
        public string nonceStr { get; set; }
        public string signature { get; set; }
        public string debug { get; set; }
        public string actionType { get; set; }
    }

    

}