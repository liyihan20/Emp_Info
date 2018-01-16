using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using EmpInfo.Util;
using Newtonsoft.Json;

namespace EmpInfo.Controllers
{
    public class WXController : BaseController
    {
        private string redirectIndex = "http://emp.truly.com.cn/WX/WIndex?cardnumber={0}&secret={1}";
        public string QueryOpenId(string openId)
        {
            try {
                openId = MyUtils.AESDecrypt(openId);
            }
            catch {
                return JsonConvert.SerializeObject(new { suc = 0 });
            }
            var emps = db.ei_users.Where(u => u.wx_openid == openId);
            if (emps.Count() == 0) {
                return JsonConvert.SerializeObject(new { suc = 0, url = "" });
            }
            var emp = emps.First();
            string url = string.Format(redirectIndex,emp.card_number,MyUtils.getMD5(emp.card_number));
            
            return JsonConvert.SerializeObject(new { suc = 1, url = MyUtils.AESEncrypt(url) });
        }

        public ActionResult WIndex(string cardnumber,string secret) {
            string msg = "";
            bool suc = false;
            if (!MyUtils.getMD5(cardnumber).Equals(secret)) {
                msg = "验证不通过，授权失败";
            }
            else {
                var users = db.ei_users.Where(u => u.card_number == cardnumber).ToList();
                if (users.Count() == 0) {
                    msg = "厂牌号不存在";
                }
                else {
                    var user = users.First();
                    if (user.forbit_flag == true) {
                        msg = "用户已被禁用";
                    }
                    else {
                        if (db.GetHREmpStatus(cardnumber).Count() == 0) {
                            msg = "用户已离职，不能使用";
                        }
                        else {
                            suc = true;
                            msg = "授权成功";
                            user.last_login_date = DateTime.Now;
                            ViewBag.tip = user.name + ",你好！欢迎登陆主界面";
                            setcookie(user, 1);
                        }
                    }
                }
            }
            WriteEventLog("微信公众号", "登陆" + (suc ? "成功" : "失败") + ";msg:" + msg);
            if (!suc) {
                ViewBag.tip = msg;
                return View("Error");
            }            
            return View();
        }

        public string BindOpenId(string cardNumber, string password, string openid)
        {
            try {
                cardNumber = MyUtils.AESDecrypt(cardNumber);
                password = MyUtils.AESDecrypt(password);
                openid = MyUtils.AESDecrypt(openid);
            }
            catch {
                return JsonConvert.SerializeObject(new { suc = 0, msg = "字段值不合法" });
            }

            string msg = "";
            bool suc = false;
            string url = "";
            var users = db.ei_users.Where(u => u.card_number == cardNumber).ToList();
            if (users.Count() == 0) {
                msg = "厂牌号不存在，请注册后再绑定";
            }
            else {
                var user=users.First();
                int maxFailTimes = 5;
                if (!MyUtils.getMD5(password).Equals(user.password)) {
                    user.fail_times = user.fail_times == null ? 1 : user.fail_times + 1;
                    if (user.fail_times >= maxFailTimes) {
                        user.fail_times = 0;
                        user.forbit_flag = true;
                        msg = "连续" + maxFailTimes + "次密码输入错误，已禁用";
                    }
                    else {
                        msg = "已连续" + user.fail_times + "次密码错误，你还剩下" + (maxFailTimes - user.fail_times) + "次尝试机会。";
                    }
                }
                else {
                    suc = true;
                    user.wx_openid = openid;
                    url = string.Format(redirectIndex, user.card_number, MyUtils.getMD5(user.card_number));
                }
            }
            WriteEventLog("微信服务号", "绑定openid：" + openid + ";card:" + cardNumber);
            return JsonConvert.SerializeObject(new { suc = suc ? 1 : 0, msg = msg, url = MyUtils.AESEncrypt(url) });
        }

    }
}
