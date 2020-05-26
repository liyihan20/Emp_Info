using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using EmpInfo.QywxWebSrv;
using EmpInfo.Services;
using EmpInfo.Util;

namespace EmpInfo.Controllers
{
    public class QYWXController : BaseController
    {
        private const string SECRET = "wZRxdsuqeFAqJDG7VLaCTkImfsuce0qwyLO3ksBUkMY";
        private const string AGENTID = "1000007";
        private const string LOGIN_URL = "http://emp.truly.com.cn/emp/QYWX/Login";

        [AllowAnonymous]
        public ActionResult Login(string code, string returnUrl = "",string state="")
        {
            if (string.IsNullOrEmpty(state)) {
                Session["fromwx"] = true; //没有state的就是从手机端进入
            }
            else {
                if (Session["state"] == null || !state.Equals((string)Session["state"])) {
                    ViewBag.tip = "state验证错误，登录失败";
                    return View("Error");
                }
                Session["fromwx"] = false; //从网页端进入
            }

            string userID;
            if (userInfo == null) {
                if(string.IsNullOrEmpty(code)){
                    ViewBag.tip="获取不到code，授权失败";
                    return View("Error");
                }

                //没有cookie的，通过code获取userid，也即是厂牌
                QywxApiSrvSoapClient wx = new QywxApiSrvSoapClient();                
                try {
                    userID = wx.GetUserIdFromCode(SECRET, code);
                }
                catch (Exception ex) {
                    ViewBag.tip = ex.Message;
                    return View("Error");
                }

                if (!new HRDBSv().EmpIsNotQuit(userID)) {
                    ViewBag.tip = "不是在职员工，不能使用此应用";
                    return View("Error");
                }

            }
            else {
                userID = userInfo.cardNo;
            }
            var user = db.ei_users.Where(u => u.card_number == userID).FirstOrDefault();
            if (user == null) {
                return RedirectToAction("Login", "Account", new { });
            }
            
            user.last_login_date = DateTime.Now;
            setcookie(user, 1);
            
            WriteEventLog("企业微信", "进入应用：" + returnUrl);

            if (!string.IsNullOrEmpty(returnUrl)) {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home", new { });
        }

        public string test()
        {
            QywxApiSrvSoapClient wx = new QywxApiSrvSoapClient();
            return wx.GetOAthLink("http://emp.truly.com.cn/emp/home/index?from=hello");
        }

        public ActionResult ComputerWebLoginUrl()
        {
            QywxApiSrvSoapClient wx = new QywxApiSrvSoapClient();
            string state = MyUtils.CreateValidateNumber(6);
            Session["state"] = state;
            string url = wx.GetWebLink(AGENTID, LOGIN_URL, state);
            return Redirect(url);
        }

    }
}
