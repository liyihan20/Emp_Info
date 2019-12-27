using EmpInfo.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;

namespace EmpInfo.Services
{
    public class WxSv:BaseSv
    {
        public string APPID = "wx6fad42dd1649a40e";
        private string SECRET = "fc879614f3fc272aa49264491bd02ed8";
        

        /// <summary>
        /// 模拟get请求
        /// </summary>
        /// <param name="url">请求地址（带参数）</param>
        /// <returns></returns>
        public string Get(string url)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.ContentType = "text/html;charset=UTF-8";

            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            Stream myResponseStream = response.GetResponseStream();
            StreamReader myStreamReader = new StreamReader(myResponseStream, Encoding.GetEncoding("utf-8"));
            string retString = myStreamReader.ReadToEnd();
            myStreamReader.Close();
            myResponseStream.Close();

            return retString;
        }

        public string GetAccessToken()
        {
            var sixMinutesFuture = DateTime.Now.AddMinutes(6); //在过期前6分钟重新取新的
            var tokens = db.wx_accessToken.Where(a => a.FExpireDate > sixMinutesFuture).ToList();
            if (tokens.Count() > 0) {
                var token = tokens.OrderByDescending(t => t.id).First();
                return token.FAccessToken;
            }
            //如果数据库的令牌已过期，则到微信服务器获取
            string ACCESSTOKENURL = string.Format("https://api.weixin.qq.com/cgi-bin/token?grant_type=client_credential&appid={0}&secret={1}", APPID, SECRET);
            AccessTokenModel atm = JsonConvert.DeserializeObject<AccessTokenModel>(Get(ACCESSTOKENURL));
                       
            //将最新令牌保存在数据库中
            wx_accessToken tk = new wx_accessToken()
            {
                FAccessToken = atm.access_token,
                FExpireDate = DateTime.Now.AddSeconds(atm.expires_in)
            };
            db.wx_accessToken.Add(tk);
            db.SaveChanges();

            return atm.access_token;
        }

        public string GetJsApiTicket()
        {
            var sixMinutesFuture = DateTime.Now.AddMinutes(6); //在过期前6分钟重新取新的
            var tokens = db.wx_jsApiTicket.Where(a => a.FExpireDate > sixMinutesFuture).ToList();
            if (tokens.Count() > 0) {
                var token = tokens.OrderByDescending(t => t.id).First();
                return token.FJsApiTicket;
            }
            //如果数据库的令牌已过期，则到微信服务器获取
            string jsApiUrl = string.Format("https://api.weixin.qq.com/cgi-bin/ticket/getticket?access_token={0}&type=jsapi", GetAccessToken());
            JsApiTicketModel atm = JsonConvert.DeserializeObject<JsApiTicketModel>(Get(jsApiUrl));

            if (atm.errcode != 0) {
                throw new Exception("JsApi 获取失败：" + atm.errmsg);
            }
            //将最新令牌保存在数据库中
            wx_jsApiTicket tk = new wx_jsApiTicket()
            {
                FJsApiTicket = atm.ticket,
                FExpireDate = DateTime.Now.AddSeconds(atm.expires_in)
            };
            db.wx_jsApiTicket.Add(tk);
            db.SaveChanges();

            return atm.ticket;
        }

        public string GetSignature(string noncestr, string timestamp,string url)
        {
            return Sha1(string.Format("jsapi_ticket={0}&noncestr={1}&timestamp={2}&url={3}", GetJsApiTicket(), noncestr, timestamp, url));
        }

        public string Sha1(string str)
        {
             var buffer = Encoding.UTF8.GetBytes(str);
             var data = System.Security.Cryptography.SHA1.Create().ComputeHash(buffer);
             
             var sb = new StringBuilder();
             foreach (var t in data)
             {
                 sb.Append(t.ToString("X2"));
             }
             
             return sb.ToString();
         }

    }
}