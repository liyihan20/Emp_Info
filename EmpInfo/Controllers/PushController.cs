using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using cn.jpush.api;
using cn.jpush.api.push.mode;
using cn.jpush.api.common;
using cn.jpush.api.common.resp;
using cn.jpush.api.push.notification;
namespace EmpInfo.Controllers
{
    public class PushController : Controller
    {
        const string APP_KEY = "aa99e673ed652d8c9109abb7";
        const string MASTER_SECRET = "b956f7edcfa8663306b88499";

        public string SendToAll(string info)
        {
            JPushClient client = new JPushClient(APP_KEY, MASTER_SECRET);
            PushPayload pushPayload = new PushPayload();
            pushPayload.platform = Platform.android();
            pushPayload.audience = Audience.all();
            pushPayload.notification = new Notification().setAlert(info);
            AndroidNotification androidnotification = new AndroidNotification();
            androidnotification.AddExtra("hello", "world");
            androidnotification.setTitle("员工查询系统");
            androidnotification.setAlert(info);
            pushPayload.notification.AndroidNotification = androidnotification;
            

            try {
                var result = client.SendPush(pushPayload);
                //由于统计数据并非非是即时的,所以等待一小段时间再执行下面的获取结果方法
                System.Threading.Thread.Sleep(10000);
                //如需查询上次推送结果执行下面的代码
                //var apiResult = client.getReceivedApi(result.msg_id.ToString());
                var apiResultv3 = client.getReceivedApi_v3(result.msg_id.ToString());
                //如需查询某个messageid的推送结果执行下面的代码
                //var queryResultWithV2 = client.getReceivedApi("1739302794");
                //var querResultWithV3 = client.getReceivedApi_v3("1739302794");

            }
            catch (APIRequestException e) {
                //Console.WriteLine("Error response from JPush server. Should review and fix it. ");
                //Console.WriteLine("HTTP Status: " + e.Status);
                //Console.WriteLine("Error Code: " + e.ErrorCode);
                //Console.WriteLine("Error Message: " + e.ErrorMessage);
                return "HTTP Status: " + e.Status + ";Error Code: " + e.ErrorCode + ";Error Message: " + e.ErrorMessage;
            }
            catch (APIConnectionException e) {
                //Console.WriteLine(e.Message);
                return e.message;
            }  

            return "ok";
        }

    }
}
